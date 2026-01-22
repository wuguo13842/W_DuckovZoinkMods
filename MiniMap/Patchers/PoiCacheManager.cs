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
            // 核心引用：这个POI对应的游戏对象和角色
            public MonoBehaviour PoiInstance { get; set; }
            public IPointOfInterest IPoi { get; set; }
            public CharacterType CharacterType { get; set; }
            public CharacterMainControl? Character { get; set; }
            
            // 缓存上一次的数据，用于变化检测
            private Vector3 _lastPosition;
            private Vector3 _lastForward;
            private Color _lastColor;
            private bool _forceUpdateNextTime = false;
            
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
            
            // 从CharacterMainControl直接获取的状态（这个POI自己的角色状态）
            public bool IsMoving { get; private set; }
            public bool IsRunning { get; private set; }
            public bool IsOnGround { get; private set; }
            public float CurrentSpeed { get; private set; }
            
            // 视觉注意力级别
            public string CurrentRange { get; set; } = "未知";
            public float CurrentUpdateInterval { get; set; } = 0.5f;
            
            // 更新时间
            public float LastWorldUpdateTime { get; set; }
            public float LastMapUpdateTime { get; set; }
            public float LastDistanceCheckTime { get; set; }
            public float LastPropertyCheckTime { get; set; }
            
            // 异步任务控制
            public CancellationTokenSource? UpdateTaskCts { get; set; }
            public bool IsUpdating { get; set; } = false;
            
            // 变化检测标志
            public bool PositionChanged { get; private set; }
            public bool RotationChanged { get; private set; }
            public bool ColorChanged { get; private set; }
            
            // 实例ID
            public string InstanceId { get; set; }
            
            // 性能统计
            public int UpdateCount { get; set; }
            public float TotalUpdateTime { get; set; }
            
            // ========== 核心优化方法：从自己的CharacterMainControl获取数据 ==========
            public void UpdateFromOwnCharacter()
            {
                if (Character == null) 
                {
                    IsActiveByGame = false;
                    return;
                }
                
                try
                {
                    // 检查角色是否有效
                    if (!Character.gameObject.activeInHierarchy || (Character.Health != null && Character.Health.IsDead))
                    {
                        IsActiveByGame = false;
                        return;
                    }
                    
                    IsActiveByGame = true;
                    
                    // 获取这个POI自己的角色组件
                    var myCharacter = Character;
                    var transform = myCharacter.transform;
                    var movement = myCharacter.movementControl;
                    
                    // ========== 1. 位置更新（总是更新，但检测变化） ==========
                    Vector3 currentPos = transform.position;
                    
                    // 计算与上一次的位置变化（使用平方距离提高性能）
                    float sqrDistance = (currentPos - _lastPosition).sqrMagnitude;
                    
                    // 关键：降低变化阈值，确保靠近时能检测到
                    bool positionChanged = sqrDistance > 0.001f || _forceUpdateNextTime;
                    
                    if (positionChanged)
                    {
                        WorldPosition = currentPos;
                        _lastPosition = currentPos;
                        PositionChanged = true;
                        LastWorldUpdateTime = Time.time;
                    }
                    
                    // ========== 2. 朝向更新 ==========
                    Vector3 currentForward = myCharacter.modelRoot.forward;
                    
                    // 降低角度变化阈值
                    bool rotationChanged = Vector3.Angle(currentForward, _lastForward) > 0.5f || _forceUpdateNextTime;
                    
                    if (rotationChanged)
                    {
                        ForwardDirection = currentForward;
                        _lastForward = currentForward;
                        RotationChanged = true;
                        
                        // 目标方向
                        if (movement != null)
                        {
                            TargetAimDirection = movement.targetAimDirection;
                        }
                        
                        // BOSS旋转
                        if (CharacterType == CharacterType.Boss)
                        {
                            Rotation = myCharacter.modelRoot.rotation;
                        }
                    }
                    
                    // ========== 3. 状态更新（总是更新，无需变化检测） ==========
                    if (movement != null)
                    {
                        IsMoving = movement.Moving;
                        IsRunning = myCharacter.Running;
                        IsOnGround = myCharacter.IsOnGround;
                        CurrentSpeed = myCharacter.Velocity.magnitude;
                    }
                    
                    // ========== 4. 颜色更新 ==========
                    if (IPoi != null)
                    {
                        Color currentColor = IPoi.Color;
                        if (currentColor != _lastColor || _forceUpdateNextTime)
                        {
                            Color = currentColor;
                            _lastColor = currentColor;
                            ColorChanged = true;
                        }
                    }
                    
                    // 重置强制更新标志
                    _forceUpdateNextTime = false;
                }
                catch (Exception e)
                {
                    ModBehaviour.Logger.LogError($"UpdateFromOwnCharacter错误 [{InstanceId}]: {e.Message}");
                    IsActiveByGame = false;
                }
            }
            
            // 重置变化标志
            public void ResetChangeFlags()
            {
                PositionChanged = false;
                RotationChanged = false;
                ColorChanged = false;
            }
            
            // 强制下次更新
            public void ForceUpdateNextTime()
            {
                _forceUpdateNextTime = true;
            }
            
            // 初始化缓存
            public void InitializeCache()
            {
                if (Character != null)
                {
                    _lastPosition = Character.transform.position;
                    _lastForward = Character.modelRoot.forward;
                }
                if (IPoi != null)
                {
                    _lastColor = IPoi.Color;
                }
            }
        }

        // 按实例存储数据
        private Dictionary<MonoBehaviour, PoiInstanceData> _instanceData = new();
        private Dictionary<CharacterMainControl, PoiInstanceData> _activePois = new();
        
        // 特殊敌人白名单（需要特殊处理的敌人预设名）
        private HashSet<string> _specialEnemyWhiteList = new HashSet<string>();
        
        private CancellationTokenSource? _globalCts;
        
        // 配置
        private const float DISTANCE_CHECK_INTERVAL = 1.0f;  // 距离检查频率
        
        private CharacterMainControl? _player;
        private MiniMapDisplay? _activeMapDisplay;
        private string? _currentSceneId;
        private Vector3 _playerPos;
        private float _lastGlobalDistanceCheckTime;
        
        // 性能统计
        private float _lastStatLogTime;
        private int _totalUpdatesThisFrame;
        private int _totalUpdatesLastSecond;
        private float _lastSecondUpdateTime;
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
                LastPropertyCheckTime = Time.time,
                LastMapUpdateTime = 0f, // 初始化为0，强制第一次更新
                Color = ipoi.Color,
                DisplayName = ipoi.DisplayName,
                HideIcon = ipoi.HideIcon,
                ScaleFactor = ipoi.ScaleFactor,
                IsAlive = true,
                IsActiveByGame = character != null && character.gameObject.activeInHierarchy,
                CurrentUpdateInterval = 0.5f,
                CurrentRange = "未计算"
            };

            // 初始化缓存
            instanceData.InitializeCache();
            
            // 强制第一次更新
            instanceData.ForceUpdateNextTime();
            
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
                    ManageAllInstancesOptimized();
                    
                    // 每秒更新统计
                    if (Time.time - _lastSecondUpdateTime >= 1f)
                    {
                        _totalUpdatesLastSecond = _totalUpdatesThisFrame;
                        _totalUpdatesThisFrame = 0;
                        _lastSecondUpdateTime = Time.time;
                    }
                    
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

        private void ManageAllInstancesOptimized()
        {
            if (_player == null || string.IsNullOrEmpty(_currentSceneId)) return;

            var instancesToRemove = new List<MonoBehaviour>();
            float currentTime = Time.time;
            bool shouldCheckDistance = currentTime - _lastGlobalDistanceCheckTime > 0.5f;
            
            if (shouldCheckDistance)
            {
                _lastGlobalDistanceCheckTime = currentTime;
            }
            
            foreach (var kvp in _instanceData)
            {
                var instance = kvp.Key;
                var data = kvp.Value;
                
                if (instance == null || !instance.gameObject.activeInHierarchy)
                {
                    instancesToRemove.Add(instance);
                    continue;
                }
                
                // 1. 从这个POI自己的CharacterMainControl获取数据并检测变化
                data.UpdateFromOwnCharacter();
                
                // 2. 更新游戏激活状态（已经在UpdateFromOwnCharacter中更新了）
                
                if (!data.IsActiveByGame)
                {
                    if (data.IsUpdating)
                    {
                        StopInstanceUpdateTask(data);
                    }
                    continue;
                }
                
                // 3. 距离计算优化：降低频率（计算这个POI到玩家的距离）
                if (shouldCheckDistance)
                {
                    // 计算这个POI角色位置到玩家位置的距离
                    float distance = Vector3.Distance(data.WorldPosition, _playerPos);
                    
                    // 总是更新距离，确保数据准确
                    data.DistanceToPlayer = distance;
                    data.LastDistanceCheckTime = currentTime;
                    
                    string newRange = VisualAttentionStrategy.GetRangeName(distance);
                    float newInterval = VisualAttentionStrategy.GetUpdateInterval(distance);
                    
                    if (data.CurrentRange != newRange || Mathf.Abs(data.CurrentUpdateInterval - newInterval) > 0.05f)
                    {
                        OnRangeChanged(data, data.CurrentRange, newRange, distance, newInterval);
                        data.CurrentRange = newRange;
                        data.CurrentUpdateInterval = newInterval;
                    }
                }
                
                // 4. 其他POI属性（这些很少变化，降低更新频率）
                if (currentTime - data.LastPropertyCheckTime > 2f)
                {
                    if (data.IPoi != null)
                    {
                        data.DisplayName = data.IPoi.DisplayName;
                        data.HideIcon = data.IPoi.HideIcon;
                        data.ScaleFactor = data.IPoi.ScaleFactor;
                    }
                    data.LastPropertyCheckTime = currentTime;
                }
                
                // 5. 管理更新任务
                ManageUpdateTask(data, data.DistanceToPlayer);
                
                // 6. 重置变化标志
                data.ResetChangeFlags();
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
            
            StartVisualAttentionUpdateLoopOptimized(data, data.UpdateTaskCts.Token).Forget();
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

        private async UniTaskVoid StartVisualAttentionUpdateLoopOptimized(PoiInstanceData data, CancellationToken token)
        {
            try
            {
                float lastUpdateTime = Time.time;
                int consecutiveNoChangeCount = 0;
                int consecutivePositionNoChangeCount = 0;
                
                while (!token.IsCancellationRequested && data.IsAlive)
                {
                    // 检查这个POI对应的角色是否有效
                    if (data.Character == null || !data.Character.gameObject.activeInHierarchy)
                    {
                        data.IsActiveByGame = false;
                        break;
                    }
                    
                    float currentTime = Time.time;
                    float timeSinceLastUpdate = currentTime - lastUpdateTime;
                    
                    // 关键：使用CurrentUpdateInterval，但确保不会太久不更新
                    float updateInterval = Mathf.Max(data.CurrentUpdateInterval, 0.1f); // 最小0.1秒
                    
                    if (timeSinceLastUpdate >= updateInterval)
                    {
                        // 1. 强制每隔几次更新一次，避免漏掉变化
                        if (consecutivePositionNoChangeCount >= 10) // 连续10次位置没变化
                        {
                            data.ForceUpdateNextTime();
                            consecutivePositionNoChangeCount = 0;
                        }
                        
                        // 2. 从这个POI自己的Character获取数据
                        data.UpdateFromOwnCharacter();
                        
                        // 3. 如果角色不活跃，停止更新
                        if (!data.IsActiveByGame)
                        {
                            break;
                        }
                        
                        // 4. 根据距离和变化情况更新地图位置
                        UpdatePoiBasedOnDistanceOptimized(data);
                        
                        // 5. 统计位置变化情况
                        if (data.PositionChanged)
                        {
                            consecutivePositionNoChangeCount = 0;
                        }
                        else
                        {
                            consecutivePositionNoChangeCount++;
                            
                            // 即使位置没变化，也要定期更新地图位置（防止卡住）
                            if (Time.time - data.LastMapUpdateTime > 10f) // 最多10秒更新一次
                            {
                                UpdateMapPosition(data);
                            }
                        }
                        
                        // 6. 统计和计数
                        data.UpdateCount++;
                        _totalUpdatesThisFrame++;
                        
                        // 7. 重置变化标志
                        data.ResetChangeFlags();
                        
                        lastUpdateTime = currentTime;
                    }
                    
                    // 等待下一个更新周期
                    await UniTask.Delay((int)(updateInterval * 1000), cancellationToken: token);
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

        private void UpdatePoiBasedOnDistanceOptimized(PoiInstanceData data)
        {
            float distance = data.DistanceToPlayer;
            
            // 关键：根据距离和变化情况决定更新什么
            if (distance <= VisualAttentionStrategy.CLOSE_RANGE)
            {
                // 近距离（0-25米）：总是更新地图位置
                UpdateMapPosition(data);
            }
            else if (distance <= VisualAttentionStrategy.OPTIMAL_RANGE)
            {
                // 中距离（25-40米）：位置变化或定期更新
                if (data.PositionChanged || Time.time - data.LastMapUpdateTime > 1.0f)
                {
                    UpdateMapPosition(data);
                }
            }
            else if (distance <= VisualAttentionStrategy.MAX_TRACK_RANGE)
            {
                // 远距离（40-100米）：降低更新频率
                if ((data.UpdateCount % 2 == 0 && data.PositionChanged) || 
                    Time.time - data.LastMapUpdateTime > 3.0f)
                {
                    UpdateMapPosition(data);
                }
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
                        
                        // 调试：记录近距离更新
                        if (data.DistanceToPlayer <= VisualAttentionStrategy.CLOSE_RANGE)
                        {
                            ModBehaviour.Logger.Log($"近距离更新地图位置 [{data.InstanceId}]: 距离={data.DistanceToPlayer:F1}m");
                        }
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
                ModBehaviour.Logger.LogError($"更新地图位置失败 [{data.InstanceId}]: {e.Message}");
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
                // 强制更新所有数据
                data.ForceUpdateNextTime();
                data.UpdateFromOwnCharacter();
                UpdateMapPosition(data);
                ModBehaviour.Logger.Log($"强制更新POI: {data.InstanceId}");
            }
        }

        public void ClearAllInstances()
        {
            EmergencyCleanup();
            _instanceData.Clear();
            ModBehaviour.Logger.Log("清空所有POI实例");
        }

        // ========== 调试方法 ==========
        
        public void DebugPoiStatus(MonoBehaviour poiInstance)
        {
            if (_instanceData.TryGetValue(poiInstance, out var data))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"=== POI调试信息 [{data.InstanceId}] ===");
                sb.AppendLine($"角色: {data.Character?.name ?? "null"}");
                sb.AppendLine($"类型: {data.CharacterType}");
                sb.AppendLine($"活跃: {data.IsActiveByGame}");
                sb.AppendLine($"正在更新: {data.IsUpdating}");
                sb.AppendLine($"位置: {data.WorldPosition}");
                sb.AppendLine($"距离玩家: {data.DistanceToPlayer:F1}m");
                sb.AppendLine($"上次地图更新: {Time.time - data.LastMapUpdateTime:F1}秒前");
                sb.AppendLine($"上次世界更新: {Time.time - data.LastWorldUpdateTime:F1}秒前");
                sb.AppendLine($"更新间隔: {data.CurrentUpdateInterval:F2}s");
                sb.AppendLine($"更新次数: {data.UpdateCount}");
                sb.AppendLine($"移动状态: {data.IsMoving}");
                sb.AppendLine($"奔跑状态: {data.IsRunning}");
                sb.AppendLine($"有效地图位置: {data.HasValidMapPosition}");
                sb.AppendLine($"地图位置: {data.CachedMapPosition}");
                
                ModBehaviour.Logger.Log(sb.ToString());
            }
            else
            {
                ModBehaviour.Logger.Log($"POI未注册: {poiInstance?.name ?? "null"}");
            }
        }

        public void ForceRefreshAllPois()
        {
            foreach (var data in _instanceData.Values)
            {
                data.ForceUpdateNextTime();
            }
            ModBehaviour.Logger.Log($"强制刷新所有POI，共{_instanceData.Count}个");
        }

        public void LogAllPoiDistances()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== 所有POI距离统计 ===");
            
            foreach (var data in _instanceData.Values)
            {
                if (data.IsActiveByGame)
                {
                    sb.AppendLine($"{data.InstanceId}: {data.DistanceToPlayer:F1}m - {data.CurrentRange} - 更新中:{data.IsUpdating}");
                }
            }
            
            ModBehaviour.Logger.Log(sb.ToString());
        }

        private void LogPerformanceStats()
        {
            if (_activePois.Count == 0) return;
            
            int close = 0, optimal = 0, far = 0;
            int updating = 0;
            float totalUpdateInterval = 0f;
            
            foreach (var data in _activePois.Values)
            {
                float d = data.DistanceToPlayer;
                if (d <= 25f) close++;
                else if (d <= 40f) optimal++;
                else far++;
                
                if (data.IsUpdating)
                {
                    updating++;
                    totalUpdateInterval += data.CurrentUpdateInterval;
                }
            }
            
            float avgUpdateInterval = updating > 0 ? totalUpdateInterval / updating : 0f;
            
            _statsBuilder.Clear();
            _statsBuilder.AppendLine("=== POI性能统计 ===");
            _statsBuilder.Append("活跃实例: ").Append(_activePois.Count)
                        .Append(" [近:").Append(close)
                        .Append(" 中:").Append(optimal)
                        .Append(" 远:").Append(far).AppendLine("]");
            _statsBuilder.Append("正在更新: ").Append(updating)
                        .Append(" 平均间隔:").Append(avgUpdateInterval.ToString("F2")).Append("s")
                        .Append(" 更新频率:").Append(_totalUpdatesLastSecond).AppendLine("/秒");
            _statsBuilder.Append("总实例数: ").Append(_instanceData.Count);
            
            ModBehaviour.Logger.Log(_statsBuilder.ToString());
        }

        public void LogInstanceStats()
        {
            int total = _instanceData.Count;
            int active = _activePois.Count;
            int updating = 0;
            float totalUpdateInterval = 0f;
            
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
                
                if (data.IsUpdating)
                {
                    updating++;
                    totalUpdateInterval += data.CurrentUpdateInterval;
                }
            }

            float avgUpdateInterval = updating > 0 ? totalUpdateInterval / updating : 0f;
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== POI详细统计 ===");
            sb.AppendLine($"实例总数: {total}, 活跃: {active}, 正在更新: {updating}");
            sb.AppendLine($"类型分布: 敌人={enemies}, BOSS={bosses}, NPC={npcs}, 玩家={players}");
            sb.AppendLine($"平均更新间隔: {avgUpdateInterval:F2}s");
            sb.AppendLine($"当前更新频率: {_totalUpdatesLastSecond}/秒");
            
            ModBehaviour.Logger.Log(sb.ToString());
        }
    }
}