using Cysharp.Threading.Tasks;
using MiniMap.Poi;
using MiniMap.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;

namespace MiniMap.Managers
{
    public class PoiCacheManager : MonoBehaviour
    {
        private static PoiCacheManager? _instance;
        public static PoiCacheManager Instance => _instance!;

        // 视觉注意力三级策略
        private class VisualAttentionStrategy
        {
            public const float CLOSE_RANGE = 25f;      // 0-25米：太近，看敌人不看地图
            public const float OPTIMAL_RANGE = 40f;    // 25-40米：最佳地图观察距离
            public const float MAX_TRACK_RANGE = 100f; // 40-100米：远处参考
            
            public const float CLOSE_UPDATE_INTERVAL = 0.3f;    // 0.3秒：低频
            public const float OPTIMAL_UPDATE_INTERVAL = 0.2f;  // 0.2秒：中频
            public const float FAR_UPDATE_INTERVAL = 0.5f;      // 0.5秒：低频
            
            public static float GetUpdateInterval(float distance)
            {
                if (distance <= CLOSE_RANGE) return CLOSE_UPDATE_INTERVAL;
                if (distance <= OPTIMAL_RANGE) return OPTIMAL_UPDATE_INTERVAL;
                return FAR_UPDATE_INTERVAL;
            }
            
            public static string GetRangeName(float distance)
            {
                if (distance <= CLOSE_RANGE) return "近距离(0-25m)";
                if (distance <= OPTIMAL_RANGE) return "最佳距离(25-40m)";
                return "远距离(40-100m)";
            }
        }

        private class PoiInstanceData
        {
            public MonoBehaviour PoiInstance { get; set; }
            public IPointOfInterest IPoi { get; set; }
            public CharacterType CharacterType { get; set; }
            public CharacterMainControl? Character { get; set; }
            
            // 位置数据
            public Vector3 WorldPosition { get; set; }
            public Vector3 CachedMapPosition { get; set; }
            public bool HasValidMapPosition { get; set; } = false;
            
            // 角度/方向
            public Quaternion Rotation { get; set; } = Quaternion.identity;
            public Vector3 ForwardDirection { get; set; }
            public Vector3 TargetAimDirection { get; set; }
            
            // 其他属性
            public Color Color { get; set; } = Color.white;
            public string DisplayName { get; set; } = "";
            public bool HideIcon { get; set; } = false;
            public float ScaleFactor { get; set; } = 1f;
            
            // 距离和状态
            public float DistanceToPlayer { get; set; } = float.MaxValue;
            public bool IsActiveByGame { get; set; } = true;
            public bool IsAlive { get; set; } = true;
            
            // 视觉注意力级别
            public string CurrentRange { get; set; } = "未知";
            public float CurrentUpdateInterval { get; set; } = 0.5f;
            
            // 更新时间
            public float LastWorldUpdateTime { get; set; }
            public float LastMapUpdateTime { get; set; }
            public float LastDistanceCheckTime { get; set; }
            
            // 异步任务控制
            public CancellationTokenSource? UpdateTaskCts { get; set; }
            public bool IsUpdating { get; set; } = false;
            
            // 实例ID
            public string InstanceId { get; set; }
            
            // 性能统计
            public int UpdateCount { get; set; }
            public float TotalUpdateTime { get; set; }
        }

        // 按实例存储数据
        private Dictionary<MonoBehaviour, PoiInstanceData> _instanceData = new();
        private Dictionary<CharacterMainControl, PoiInstanceData> _activePois = new();
        
        // 特殊敌人白名单（需要特殊处理的敌人预设名）
        private HashSet<string> _specialEnemyWhiteList = new HashSet<string>();
        
        private CancellationTokenSource? _globalCts;
        
        // 配置
        private const float DISTANCE_CHECK_INTERVAL = 1.0f;  // 距离检查频率（从0.5秒改为1秒）
        
        private CharacterMainControl? _player;
        private MiniMapDisplay? _activeMapDisplay;
        private string? _currentSceneId;
        private Vector3 _playerPos;
        
        // 性能统计
        private float _lastStatLogTime;
        private int _totalUpdatesThisFrame;
        private StringBuilder _statsBuilder = new StringBuilder(256);

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            StartGlobalManagementLoop().Forget();
            PointsOfInterests.OnPointRegistered += OnPoiInstanceRegistered;
            PointsOfInterests.OnPointUnregistered += OnPoiInstanceUnregistered;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EmergencyCleanup();
            PointsOfInterests.OnPointRegistered -= OnPoiInstanceRegistered;
            PointsOfInterests.OnPointUnregistered -= OnPoiInstanceUnregistered;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                EmergencyCleanup();
            }
        }

        private void EmergencyCleanup()
        {
            _globalCts?.Cancel();
            _globalCts = null;
            
            foreach (var data in _instanceData.Values)
            {
                data.UpdateTaskCts?.Cancel();
                data.UpdateTaskCts = null;
                data.IsUpdating = false;
            }
            
            _activePois.Clear();
            
            ModBehaviour.Logger.Log("紧急清理POI系统");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EmergencyCleanup();
            _instanceData.Clear();
            _currentSceneId = null;
            _activeMapDisplay = null;
        }

        private void OnPoiInstanceRegistered(MonoBehaviour poiInstance)
        {
            if (poiInstance == null) return;
            
            UniTask.DelayFrame(2).ContinueWith(() =>
            {
                if (poiInstance == null || !poiInstance.gameObject.activeInHierarchy) 
                    return;
                    
                RegisterPoiInstance(poiInstance);
            }).Forget();
        }

        private void OnPoiInstanceUnregistered(MonoBehaviour poiInstance)
        {
            if (poiInstance != null && _instanceData.ContainsKey(poiInstance))
            {
                UnregisterPoiInstance(poiInstance);
            }
        }

        private void RegisterPoiInstance(MonoBehaviour poiInstance)
        {
            if (_instanceData.ContainsKey(poiInstance))
            {
                UnregisterPoiInstance(poiInstance);
            }

            IPointOfInterest ipoi = poiInstance as IPointOfInterest;
            if (ipoi == null) return;
            
            CharacterType charType = CharacterType.Unkown;
            CharacterMainControl character = null;
            
            if (poiInstance is CharacterPointOfInterestBase charPoi)
            {
                charType = charPoi.CharacterType;
                character = charPoi.Character;
            }

            var instanceData = new PoiInstanceData
            {
                PoiInstance = poiInstance,
                IPoi = ipoi,
                CharacterType = charType,
                Character = character,
                InstanceId = $"{charType}_{poiInstance.GetInstanceID()}",
                LastWorldUpdateTime = Time.time,
                LastDistanceCheckTime = Time.time,
                Color = ipoi.Color,
                DisplayName = ipoi.DisplayName,
                HideIcon = ipoi.HideIcon,
                ScaleFactor = ipoi.ScaleFactor,
                IsAlive = true,
                IsActiveByGame = character != null && character.gameObject.activeInHierarchy,
                CurrentUpdateInterval = 0.5f,
                CurrentRange = "未计算"
            };

            UpdateWorldPosition(instanceData);
            
            _instanceData[poiInstance] = instanceData;
            
            if (IsSpecialEnemy(instanceData))
            {
                HandleSpecialEnemyRegistration(instanceData);
            }
            
            ModBehaviour.Logger.Log($"注册POI: {instanceData.InstanceId}");
        }

        private void UnregisterPoiInstance(MonoBehaviour poiInstance)
        {
            if (_instanceData.TryGetValue(poiInstance, out var data))
            {
                data.UpdateTaskCts?.Cancel();
                data.UpdateTaskCts = null;
                data.IsAlive = false;
                
                if (data.Character != null && _activePois.ContainsKey(data.Character))
                {
                    _activePois.Remove(data.Character);
                }
                
                _instanceData.Remove(poiInstance);
                
                ModBehaviour.Logger.Log($"注销POI: {data.InstanceId}");
            }
        }

        private bool IsSpecialEnemy(PoiInstanceData data)
        {
            if (data.Character == null || data.Character.characterPreset == null)
                return false;
                
            string presetName = data.Character.characterPreset.name;
            return _specialEnemyWhiteList.Contains(presetName);
        }

        private void HandleSpecialEnemyRegistration(PoiInstanceData data)
        {
            ModBehaviour.Logger.Log($"注册特殊敌人: {data.InstanceId}");
        }

        private async UniTaskVoid StartGlobalManagementLoop()
        {
            _globalCts = new CancellationTokenSource();
            var token = _globalCts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(DISTANCE_CHECK_INTERVAL), cancellationToken: token);
                    
                    UpdateGlobalState();
                    ManageAllInstances();
                    
                    if (Time.time - _lastStatLogTime > 5f)
                    {
                        LogPerformanceStats();
                        _lastStatLogTime = Time.time;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    ModBehaviour.Logger.LogError($"全局管理循环错误: {e.Message}");
                    await UniTask.Delay(1000, cancellationToken: token);
                }
            }
        }

        private void UpdateGlobalState()
        {
            if (!LevelManager.LevelInited) return;

            if (_player == null)
            {
                _player = CharacterMainControl.Main;
            }
            
            if (_player != null)
            {
                _playerPos = _player.transform.position;
            }

            if (_activeMapDisplay == null)
            {
                _activeMapDisplay = CustomMinimapManager.DuplicatedMinimapDisplay ?? 
                                   CustomMinimapManager.OriginalMinimapDisplay;
            }

            if (string.IsNullOrEmpty(_currentSceneId))
            {
                _currentSceneId = SceneInfoCollection.GetSceneID(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void ManageAllInstances()
        {
            if (_player == null || string.IsNullOrEmpty(_currentSceneId)) return;

            var instancesToRemove = new List<MonoBehaviour>();
            
            foreach (var kvp in _instanceData)
            {
                var instance = kvp.Key;
                var data = kvp.Value;
                
                if (instance == null || !instance.gameObject.activeInHierarchy)
                {
                    instancesToRemove.Add(instance);
                    continue;
                }
                
                bool wasActiveByGame = data.IsActiveByGame;
                data.IsActiveByGame = data.Character != null && data.Character.gameObject.activeInHierarchy;
                
                if (data.IsActiveByGame != wasActiveByGame)
                {
                    OnGameActivationChanged(data, data.IsActiveByGame);
                }
                
                if (!data.IsActiveByGame)
                {
                    if (data.IsUpdating)
                    {
                        StopInstanceUpdateTask(data);
                    }
                    continue;
                }
                
                UpdateWorldPosition(data);
                
                if (data.Character != null)
                {
                    float distance = Vector3.Distance(data.WorldPosition, _playerPos);
                    data.DistanceToPlayer = distance;
                    
                    string newRange = VisualAttentionStrategy.GetRangeName(distance);
                    float newInterval = VisualAttentionStrategy.GetUpdateInterval(distance);
                    
                    if (data.CurrentRange != newRange || Mathf.Abs(data.CurrentUpdateInterval - newInterval) > 0.05f)
                    {
                        OnRangeChanged(data, data.CurrentRange, newRange, distance, newInterval);
                        data.CurrentRange = newRange;
                        data.CurrentUpdateInterval = newInterval;
                    }
                    
                    ManageUpdateTask(data, distance);
                    
                    if (data.IPoi != null)
                    {
                        data.Color = data.IPoi.Color;
                        data.DisplayName = data.IPoi.DisplayName;
                        data.HideIcon = data.IPoi.HideIcon;
                        data.ScaleFactor = data.IPoi.ScaleFactor;
                    }
                }
            }
            
            foreach (var instance in instancesToRemove)
            {
                UnregisterPoiInstance(instance);
            }
        }

        private void OnGameActivationChanged(PoiInstanceData data, bool isNowActive)
        {
            if (isNowActive)
            {
                if (data.Character != null)
                {
                    _activePois[data.Character] = data;
                }
                StartInstanceUpdateTask(data);
            }
            else
            {
                if (data.Character != null && _activePois.ContainsKey(data.Character))
                {
                    _activePois.Remove(data.Character);
                }
                StopInstanceUpdateTask(data);
            }
        }

        private void OnRangeChanged(PoiInstanceData data, string oldRange, string newRange, 
                                  float distance, float newInterval)
        {
            if (newRange.Contains("远距离") && data.CharacterType != CharacterType.Boss)
            {
                data.HideIcon = true;
            }
            else
            {
                data.HideIcon = false;
            }
        }

        private void ManageUpdateTask(PoiInstanceData data, float distance)
        {
            if (distance <= VisualAttentionStrategy.MAX_TRACK_RANGE && !data.IsUpdating)
            {
                StartInstanceUpdateTask(data);
            }
            else if (distance > VisualAttentionStrategy.MAX_TRACK_RANGE && data.IsUpdating)
            {
                StopInstanceUpdateTask(data);
            }
        }

        private void StartInstanceUpdateTask(PoiInstanceData data)
        {
            StopInstanceUpdateTask(data);
            
            data.UpdateTaskCts = new CancellationTokenSource();
            data.IsUpdating = true;
            
            StartVisualAttentionUpdateLoop(data, data.UpdateTaskCts.Token).Forget();
        }

        private void StopInstanceUpdateTask(PoiInstanceData data)
        {
            if (data.IsUpdating && data.UpdateTaskCts != null)
            {
                data.UpdateTaskCts.Cancel();
                data.UpdateTaskCts = null;
                data.IsUpdating = false;
            }
        }

        private async UniTaskVoid StartVisualAttentionUpdateLoop(PoiInstanceData data, CancellationToken token)
        {
            try
            {
                float lastUpdateTime = Time.time;
                
                while (!token.IsCancellationRequested && data.IsAlive)
                {
                    // 每次循环都检查游戏状态
                    if (data.Character == null || !data.Character.gameObject.activeInHierarchy)
                    {
                        data.IsActiveByGame = false;
                        break;
                    }
                    
                    float currentTime = Time.time;
                    
                    if (currentTime - lastUpdateTime >= data.CurrentUpdateInterval)
                    {
                        UpdatePoiBasedOnDistance(data);
                        lastUpdateTime = currentTime;
                        data.UpdateCount++;
                        data.TotalUpdateTime += data.CurrentUpdateInterval;
                        _totalUpdatesThisFrame++;
                    }
                    
                    await UniTask.Delay(
                        (int)(data.CurrentUpdateInterval * 1000), 
                        cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"更新循环错误 [{data.InstanceId}]: {e.Message}");
            }
            finally
            {
                data.IsUpdating = false;
            }
        }

        private void UpdatePoiBasedOnDistance(PoiInstanceData data)
        {
            if (data.Character == null) return;
            
            float distance = data.DistanceToPlayer;
            
            if (distance <= VisualAttentionStrategy.CLOSE_RANGE)
            {
                UpdateBasicPoiData(data);
            }
            else if (distance <= VisualAttentionStrategy.OPTIMAL_RANGE)
            {
                UpdateDetailedPoiData(data);
            }
            else if (distance <= VisualAttentionStrategy.MAX_TRACK_RANGE)
            {
                UpdateSimplifiedPoiData(data);
            }
        }

        private void UpdateBasicPoiData(PoiInstanceData data)
        {
            UpdateWorldPosition(data);
            
            if (data.Character.movementControl.Moving)
            {
                data.ForwardDirection = data.Character.modelRoot.forward;
                data.TargetAimDirection = data.Character.movementControl.targetAimDirection;
                UpdateBossRotation(data);
            }
            
            if (_activeMapDisplay != null && !string.IsNullOrEmpty(_currentSceneId))
            {
                UpdateMapPosition(data);
            }
        }

        private void UpdateDetailedPoiData(PoiInstanceData data)
        {
            UpdateWorldPosition(data);
            data.ForwardDirection = data.Character.modelRoot.forward;
            data.TargetAimDirection = data.Character.movementControl.targetAimDirection;
            UpdateBossRotation(data);
            
            if (_activeMapDisplay != null && !string.IsNullOrEmpty(_currentSceneId))
            {
                UpdateMapPosition(data);
            }
            
            data.Color = data.IPoi?.Color ?? Color.white;
        }

        private void UpdateSimplifiedPoiData(PoiInstanceData data)
        {
            UpdateWorldPosition(data);
            
            if (data.UpdateCount % 3 == 0)
            {
                data.ForwardDirection = data.Character.modelRoot.forward;
                UpdateBossRotation(data);
            }
            
            if (data.UpdateCount % 2 == 0 && _activeMapDisplay != null && !string.IsNullOrEmpty(_currentSceneId))
            {
                UpdateMapPosition(data);
            }
        }

        private void UpdateWorldPosition(PoiInstanceData data)
        {
            if (data.Character != null)
            {
                data.WorldPosition = data.Character.transform.position;
                data.LastWorldUpdateTime = Time.time;
            }
        }

        private void UpdateMapPosition(PoiInstanceData data)
        {
            try
            {
                if (_activeMapDisplay != null && !string.IsNullOrEmpty(_currentSceneId))
                {
                    if (_activeMapDisplay.TryConvertWorldToMinimap(data.WorldPosition, _currentSceneId, out Vector3 mapPos))
                    {
                        data.CachedMapPosition = mapPos;
                        data.HasValidMapPosition = true;
                        data.LastMapUpdateTime = Time.time;
                    }
                    else
                    {
                        data.HasValidMapPosition = false;
                    }
                }
            }
            catch (Exception e)
            {
                data.HasValidMapPosition = false;
            }
        }

        private void UpdateBossRotation(PoiInstanceData data)
        {
            if (data.CharacterType == CharacterType.Boss && data.Character != null)
            {
                Vector3 forward = data.Character.movementControl.targetAimDirection;
                data.Rotation = MiniMapCommon.GetChracterRotation(forward);
            }
        }

        public bool TryGetInstanceData(MonoBehaviour poiInstance, 
            out Vector3 mapPosition, 
            out Quaternion rotation, 
            out Color color, 
            out string displayName,
            out bool hideIcon,
            out float scaleFactor)
        {
            mapPosition = Vector3.zero;
            rotation = Quaternion.identity;
            color = Color.white;
            displayName = string.Empty;
            hideIcon = false;
            scaleFactor = 1f;

            if (!_instanceData.TryGetValue(poiInstance, out var data))
                return false;
            
            if (data.PoiInstance == null || !data.IsAlive)
                return false;
            
            if (data.CharacterType == CharacterType.Main)
                return false;
            
            if (!data.HasValidMapPosition)
                return false;
            
            mapPosition = data.CachedMapPosition;
            rotation = (data.CharacterType == CharacterType.Boss) ? data.Rotation : Quaternion.identity;
            color = data.Color;
            displayName = data.DisplayName;
            hideIcon = data.HideIcon;
            scaleFactor = data.ScaleFactor;
            
            return true;
        }

        public void ForceUpdateInstance(MonoBehaviour poiInstance)
        {
            if (_instanceData.TryGetValue(poiInstance, out var data))
            {
                UpdateWorldPosition(data);
                data.HasValidMapPosition = false;
                UpdateMapPosition(data);
            }
        }

        public void ClearAllInstances()
        {
            EmergencyCleanup();
            _instanceData.Clear();
            ModBehaviour.Logger.Log("清空所有POI实例");
        }

        private void LogPerformanceStats()
        {
            if (_activePois.Count == 0) return;
            
            int close = 0, optimal = 0, far = 0;
            foreach (var data in _activePois.Values)
            {
                float d = data.DistanceToPlayer;
                if (d <= 25f) close++;
                else if (d <= 40f) optimal++;
                else far++;
            }
            
            _statsBuilder.Clear();
            _statsBuilder.Append("POI统计: ");
            _statsBuilder.Append(_activePois.Count);
            _statsBuilder.Append("活跃 [近:");
            _statsBuilder.Append(close);
            _statsBuilder.Append(" 中:");
            _statsBuilder.Append(optimal);
            _statsBuilder.Append(" 远:");
            _statsBuilder.Append(far);
            _statsBuilder.Append(']');
            
            ModBehaviour.Logger.Log(_statsBuilder.ToString());
            _totalUpdatesThisFrame = 0;
        }

        public void LogInstanceStats()
        {
            int total = _instanceData.Count;
            int active = _activePois.Count;
            
            int enemies = 0, bosses = 0, npcs = 0, players = 0;

            foreach (var data in _instanceData.Values)
            {
                switch (data.CharacterType)
                {
                    case CharacterType.Enemy: enemies++; break;
                    case CharacterType.Boss: bosses++; break;
                    case CharacterType.NPC: npcs++; break;
                    case CharacterType.Main: players++; break;
                }
            }

            ModBehaviour.Logger.Log($"POI实例: 总数={total}, 活跃={active}");
            ModBehaviour.Logger.Log($"类型: 敌人={enemies}, BOSS={bosses}, NPC={npcs}, 玩家={players}");
        }
    }
}