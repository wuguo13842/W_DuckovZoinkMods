using Cysharp.Threading.Tasks;
using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using LeTai.TrueShadow;
using MiniMap.MonoBehaviours;
using MiniMap.Poi;
using MiniMap.Utils;
using SodaCraft.Localizations;
using System;
using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;
using ZoinkModdingLibrary.GameUI;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Managers
{
    public static class MinimapManager
    {
        public static event Action? MiniMapApplied;
        public static bool isEnabled = true;
        public static bool isToggled = false;
        public static bool IsInitialized { get; private set; } = false;

        private static Vector2 miniMapSize = new Vector2(200f, 200f);
        private static float northFontSize = 18f;

        public static float MapBorderEulerZRotation = 0f;
        public static Vector2 displayZoomRange = new Vector2(0.1f, 30f);

        public static Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        private static GameObject? customCanvas;
        private static GameObject? miniMapContainer;
        public static GameObject? miniMapScaleContainer;
        private static GameObject? minimapObject;

        private static MiniMapDisplay? minimapDisplay;
        private static MiniMapDisplay? originalDisplay;

        public static MiniMapDisplay? MinimapDisplay
        {
            get
            {
                return minimapDisplay;
            }
        }
        public static MiniMapDisplay? OriginalDisplay
        {
            get
            {
                return originalDisplay;
            }
        }

        private static RectTransform? miniMapRect;
        private static RectTransform? miniMapViewportRect;
        private static RectTransform? miniMapNorthRect;
        private static RectTransform? northRect;
        private static TextMeshProUGUI? northText;

        private static Coroutine? settingCor;

        public static void Initialize()
        {
            if (IsInitialized)
            {
                ModBehaviour.Logger.LogError($"MinimapManager 已初始化");
                return;
            }
            CreateMiniMapContainer();
            InitializeInputSystem();
            ModSettingManager.ConfigLoaded += OnConfigLoaded;
            ModSettingManager.ConfigChanged += OnConfigChanged;
            ModSettingManager.ButtonClicked += OnButtonClicked;
            SceneManager.sceneLoaded += OnSceneLoaded;

            IsInitialized = true;
        }

        public static void Destroy()
        {
            ModBehaviour.Logger.Log($"正在销毁小地图容器");
            GameObject.Destroy(miniMapContainer);
            CleanupInputSystem();
            ModSettingManager.ConfigLoaded -= OnConfigLoaded;
            ModSettingManager.ConfigChanged -= OnConfigChanged;
            ModSettingManager.ButtonClicked -= OnButtonClicked;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            IsInitialized = false;
        }

        private static void OnConfigChanged(string key, object value)
        {
            switch (key)
            {
                case "enableMiniMap":
                    OnEnabledChanged();
                    break;
                case "miniMapWindowScale":
                    OnMinimapContainerScaleChanged();
                    break;
                case "miniMapPositionX":
                    OnMinimapPositionChanged();
                    break;
                case "miniMapPositionY":
                    OnMinimapPositionChanged();
                    break;
                case "MiniMapToggleKey":
                case "MiniMapZoomInKey":
                case "MiniMapZoomOutKey":
                    UpdateInputBindings();
                    break;
            }
        }

        private static void OnConfigLoaded()
        {
            OnEnabledChanged();
            OnMinimapPositionChanged();
            OnMinimapContainerScaleChanged();
            UpdateInputBindings();
        }

        private static void OnButtonClicked(string key)
        {
            switch (key)
            {
                case "resetAllButton":
                    ModSettingManager.ResetAllConfigs();
                    break;
            }
        }

        public static void OnEnabledChanged()
        {
            try
            {
                bool enabled = ModSettingManager.GetValue<bool>("enableMiniMap");
                isEnabled = enabled;
                isToggled = enabled;
                customCanvas?.SetActive(isEnabled);
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"设置小地图开关时发生错误: {e.Message}");
            }
        }

        public static void TryShow()
        {
            try
            {
                if (customCanvas != null && isEnabled)
                {
                    if (customCanvas.activeInHierarchy)
                        return;
                    customCanvas.SetActive(true);
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"尝试显示小地图时发生错误: {e.Message}");
            }
        }

        public static void Hide()
        {
            try
            {
                if (!IsInitialized)
                    return;
                if (customCanvas != null)
                {
                    customCanvas.SetActive(false);
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"隐藏小地图时发生错误: {e.Message}");
            }
        }

        public static void StartSetting()
        {
            try
            {
                miniMapContainer?.transform.SetParent(GameManager.PauseMenu.transform);
                if (settingCor != null)
                {
                    ModBehaviour.Instance?.StopCoroutine(settingCor);
                    settingCor = null;
                }
                settingCor = ModBehaviour.Instance?.StartCoroutine(FinishSetting());
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"调整设置时发生错误: {e.Message}");
            }
        }

        public static IEnumerator FinishSetting()
        {
            yield return new WaitForSecondsRealtime(1f);
            miniMapContainer?.transform.SetParent(customCanvas?.transform);
        }

        public static bool HasMap()
        {
            return isEnabled && IsInitialized && minimapObject != null && (customCanvas?.activeInHierarchy ?? false);
        }

        public static void DisplayZoom(int symbol)
        {
            if (!HasMap())
                return;

            float displayZoomScale = ModSettingManager.GetValue<float>("displayZoomScale");
            float miniMapZoomStep = ModSettingManager.GetValue<float>("miniMapZoomStep");
            displayZoomScale += symbol * miniMapZoomStep;
            displayZoomScale = Mathf.Clamp(displayZoomScale, displayZoomRange.x, displayZoomRange.y);
            ModSettingManager.SaveValue("displayZoomScale", displayZoomScale, true);
            UpdateDisplayZoom();
        }

        private static void UpdateDisplayZoom()
        {
            try
            {
                float displayZoomScale = ModSettingManager.GetValue<float>("displayZoomScale");
                if (minimapDisplay != null)
                {
                    minimapDisplay.transform.localScale = Vector3.one * displayZoomScale;
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"更新小地图缩放时发生错误: {e.Message}");
            }
        }

        public static void OnMinimapContainerScaleChanged()
        {
            try
            {
                if (miniMapContainer == null)
                {
                    return;
                }
                RefreshMinimapContainerScale();
                if (!HasMap())
                    return;
                StartSetting();
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"设置小地图大小时发生错误: {e.Message}");
            }
        }

        private static void RefreshMinimapContainerScale()
        {
            if (miniMapContainer == null)
            {
                return;
            }
            float miniMapWindowScale = ModSettingManager.GetValue<float>("miniMapWindowScale");
            //Vector3 scaleVector = Vector3.one * miniMapWindowScale;
            //if (miniMapContainer.transform.localScale != scaleVector)
            //    miniMapContainer.transform.localScale = scaleVector;
            if (miniMapContainer.transform is RectTransform rect)
            {
                Vector2 targetSize = miniMapSize * miniMapWindowScale;
                if (!rect.sizeDelta.Equals(targetSize))
                {
                    rect.sizeDelta = targetSize;
                    if (northRect != null)
                    {
                        northRect.anchoredPosition = new Vector2(0, targetSize.y / 2f);
                    }
                    if (northText != null)
                    {
                        northText.fontSize = northFontSize * miniMapWindowScale;
                    }
                }
            }
        }

        private static void OnMinimapPositionChanged()
        {
            RefreshMinimapPosition();
            if (!HasMap())
                return;
            StartSetting();
        }

        private static void RefreshMinimapPosition()
        {
            try
            {
                var parentRect = GameManager.PauseMenu.GetComponent<RectTransform>();
                if (parentRect == null)
                {
                    return;
                }
                float parentWidth = parentRect.rect.width;
                float parentHeight = parentRect.rect.height;
                if (parentRect.rect.width <= 0 || parentRect.rect.height <= 0)
                {
                    ModBehaviour.Logger.LogWarning($"父Canvas尺寸异常: {parentRect.rect.width} x {parentRect.rect.height}");
                    return;
                }
                float miniMapPositionX = ModSettingManager.GetValue<float>("miniMapPositionX");
                float miniMapPositionY = ModSettingManager.GetValue<float>("miniMapPositionY");
                float offsetX = (miniMapPositionX - 0.5f) * parentWidth;
                float offsetY = (miniMapPositionY - 0.5f) * parentHeight;
                if (miniMapRect != null)
                    miniMapRect.anchoredPosition = new Vector2(offsetX, offsetY);
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"更新小地图UI位置时发生错误: {e.Message}");
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                ModBehaviour.Logger.Log($"正在加载场景 {scene.name}, 模式: {mode}");
                if (LevelManager.Instance == null || !isEnabled)
                {
                    customCanvas?.SetActive(false);
                    return;
                }
                if (minimapDisplay == null)
                {
                    DuplicateDisplay();
                }
                //if (ModBehaviour.Instance == null)
                //{
                //    return;
                //}
                ////if (initMapCor != null)
                ////    ModBehaviour.Instance.StopCoroutine(initMapCor);
                ////ClearMap();
                //customCanvas?.SetActive(false);
                //initMapCor = ModBehaviour.Instance.StartCoroutine(ApplyMiniMapCoroutine());
                ApplyMiniMap().Forget();
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"加载场景时发生错误: {e.Message}");
            }
        }

        private async static UniTask ApplyMiniMap()
        {
            try
            {
                if (LevelManager.Instance == null)
                {
                    return;
                }
                ModBehaviour.Logger.Log($"等待角色初始化...");
                while (LevelManager.Instance.MainCharacter == null || LevelManager.Instance.PetCharacter == null)
                {
                    await UniTask.Delay(200);
                }
                ModBehaviour.Logger.Log($"角色已完成初始化");
                ModBehaviour.Logger.Log($"已生成小地图");
                PoiCommon.CreatePoiIfNeeded(LevelManager.Instance.MainCharacter, out _, out DirectionPointOfInterest? mainDirectionPoi);
                PoiCommon.CreatePoiIfNeeded(LevelManager.Instance.PetCharacter, out CharacterPointOfInterest? petPoi, out DirectionPointOfInterest? petDirectionPoi);
                MapMarkerManager.Instance.InvokeMethod("Load");
                customCanvas?.SetActive(true);
                await UniTask.Delay(500);
                minimapDisplay.InvokeMethod("HandlePointsOfInterests");
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"生成小地图时发生错误: {e.Message}");
            }
        }

        private static void HandleUIBlockState()
        {
            try
            {
                bool minimapIsOn = isEnabled && IsInitialized && minimapObject != null;
                bool inputActive = Application.isFocused && InputManager.InputActived && CharacterInputControl.Instance;
                if (!inputActive)
                {
                    if (PauseMenu.Instance.Shown)
                        return;
                    Hide();
                }
                else
                {
                    if (minimapIsOn && isToggled)
                    {
                        TryShow();
                    }
                    else if (minimapIsOn && !isToggled)
                    {
                        Hide();
                    }
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"处理UI遮挡时发生错误: {e.Message}");
            }
        }

        public static void Update()
        {
            try
            {
                var displayEntries = minimapDisplay?.GetComponentsInChildren<MiniMapDisplayEntry>();
                if (displayEntries == null)
                {
                    return;
                }
                foreach (var entry in displayEntries)
                {
                    GameObject entryObject = entry.gameObject;
                    Image image = entryObject.GetComponent<Image>();
                    if (image != null && (image.material == null || image.material.name == "MapSprite"))
                    {
                        image.material = default;
                    }
                }

                if (!LevelManager.LevelInited)
                {
                    return;
                }

                HandleUIBlockState();
                bool minimapIsOn = HasMap();
                if (!minimapIsOn)
                {
                    return;
                }

                UpdateMiniMapRotation();
                UpdateMiniMapPosition();
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"更新小地图时发生错误: {e.Message}");
            }
        }

        private static void CreateMiniMapContainer()
        {
            try
            {
                ModBehaviour.Logger.Log($"创建小地图容器");
                // 创建 Canvas
                customCanvas = new GameObject("Zoink_MinimapCanvas");
                customCanvas.SetActive(false);
                var targetCanvas = customCanvas.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.sortingOrder = 0; // 确保在最前面

                // 添加 CanvasScaler 用于适应不同分辨率
                CanvasScaler scaler = customCanvas.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2560, 1440);

                // 创建 CanvasGroup 用于控制透明度
                customCanvas.AddComponent<CanvasGroup>();

                // 创建小地图UI容器
                miniMapContainer = new GameObject("Zoink_MiniMapContainer");
                miniMapRect = miniMapContainer.AddComponent<RectTransform>();
                miniMapContainer.transform.SetParent(customCanvas.transform);
                miniMapContainer.AddComponent<UIMouseDetector>();

                // 设置位置和大小（左上角）
                miniMapRect.anchorMin = new Vector2(0.5f, 0.5f);
                miniMapRect.anchorMax = new Vector2(0.5f, 0.5f);
                miniMapRect.pivot = new Vector2(0.5f, 0.5f);
                miniMapRect.sizeDelta = miniMapSize;
                miniMapRect.anchoredPosition = Vector2.zero;

                // 创建遮罩区域
                UIElements.CreateFilledRectTransform(miniMapRect, "Zoink_MiniMapMask", out GameObject? maskObject, out RectTransform? miniMapMaskRect);
                if (maskObject == null || miniMapMaskRect == null)
                {
                    return;
                }
                ProceduralImage image = maskObject.AddComponent<ProceduralImage>();
                maskObject.AddComponent<RoundModifier>();
                image.BorderWidth = 0;
                image.color = Color.white;
                Mask mask = maskObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                // 创建边框区域
                UIElements.CreateFilledRectTransform(miniMapRect, "Zoink_MiniMapBorder", out GameObject? borderObject, out RectTransform? miniMapBorderRect);
                if (borderObject == null || miniMapBorderRect == null)
                {
                    return;
                }
                ProceduralImage border = borderObject.AddComponent<ProceduralImage>();
                borderObject.AddComponent<RoundModifier>();
                border.BorderWidth = 4f;
                border.FalloffDistance = 3f;
                border.color = Color.white;
                TrueShadow shadow = borderObject.AddComponent<TrueShadow>();
                shadow.ColorBleedMode = ColorBleedMode.Black;
                shadow.Color = ColorUtility.TryParseHtmlString("#3BADEE", out Color color) ? color : Color.clear;
                //shadow.OffsetDistance = 1;
                shadow.Size = 10f;
                shadow.Spread = 0.2f;
                miniMapBorderRect.eulerAngles = new Vector3(0f, 0f, MapBorderEulerZRotation);

                // 创建指北针区域
                UIElements.CreateFilledRectTransform(miniMapRect, "Zoink_MiniMapNorth", out GameObject? northObject, out miniMapNorthRect);
                if (northObject == null || miniMapNorthRect == null)
                {
                    return;
                }
                GameObject north = new GameObject("North");
                northRect = north.AddComponent<RectTransform>();
                northRect.SetParent(miniMapNorthRect);
                northRect.sizeDelta = new Vector2(0f, 40f);
                northRect.pivot = new Vector2(0.5f, 0f);
                northRect.anchorMin = new Vector2(0.5f, 0.5f);
                northRect.anchorMax = new Vector2(0.5f, 0.5f);
                northRect.anchoredPosition = new Vector2(0, miniMapSize.y / 2f);
                northText = north.AddComponent<TextMeshProUGUI>();
                northText.alignment = TextAlignmentOptions.Bottom;
                northText.color = new Color(1f, 0.4f, 0.4f);
                northText.fontSize = northFontSize;
                northText.fontStyle = FontStyles.Bold;
                TextLocalizor localizor = north.AddComponent<TextLocalizor>();
                localizor.Key = "Dir_N";

                // 创建视窗区域
                GameObject viewportObject = new GameObject("Zoink_MiniMapViewport");
                miniMapViewportRect = viewportObject.AddComponent<RectTransform>();

                // 添加背景
                Image background = viewportObject.AddComponent<Image>();
                background.color = backgroundColor;

                miniMapScaleContainer = new GameObject("Zoink_MiniMapScaleContainer");
                var scaleRect = miniMapScaleContainer.AddComponent<RectTransform>();
                scaleRect.localScale = Vector3.one * 0.8f;
                scaleRect.SetParent(miniMapViewportRect);

                // 设置遮罩为容器的子对象，并填满容器
                miniMapViewportRect.SetParent(miniMapMaskRect);
                miniMapViewportRect.anchorMin = Vector2.zero;
                miniMapViewportRect.anchorMax = Vector2.one;
                miniMapViewportRect.offsetMin = Vector2.zero;
                miniMapViewportRect.offsetMax = Vector2.zero;

                GameObject.DontDestroyOnLoad(customCanvas);
                ModBehaviour.Logger.Log($"已创建小地图容器");
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"创建小地图容器时发生错误: {e.Message}");
            }
        }

        public static MiniMapDisplay? GetOriginalDisplay()
        {
            try
            {
                MiniMapView? minimapView = MiniMapView.Instance;
                return minimapView?.GetField<MiniMapDisplay>("display");
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"获取原始地图时发生错误:{e.Message}");
                return null;
            }
        }

        public static bool DuplicateDisplay()
        {
            try
            {
                MiniMapDisplay? originalDisplay = GetOriginalDisplay();
                if (originalDisplay == null)
                {
                    ModBehaviour.Logger.LogError($"原始地图为空！");
                    return false;
                }
                if (minimapObject != null)
                {
                    ModBehaviour.Logger.LogError($"原始地图已复制，请勿重复复制！");
                    return false;
                }

                MinimapManager.originalDisplay = originalDisplay;
                minimapObject = GameObject.Instantiate(originalDisplay.gameObject);
                minimapDisplay = minimapObject.GetComponent<MiniMapDisplay>();
                minimapDisplay.SetField("autoSetupOnEnable", true);
                minimapDisplay.InvokeMethod("UnregisterEvents");
                minimapDisplay.InvokeMethod("RegisterEvents");
                if (minimapDisplay == null || minimapObject == null)
                {
                    ModBehaviour.Logger.LogError($"获取 MinimapDisplay 复制体失败！");
                    GameObject.Destroy(minimapObject);
                    GameObject.Destroy(minimapDisplay);
                    return false;
                }
                minimapObject.name = "Zoink_MiniMap_Duplicate";

                minimapObject.transform.SetParent(miniMapScaleContainer?.transform);
                RectTransform rectTransform = minimapObject.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                    rectTransform.localScale = Vector3.one;
                }

                minimapObject.transform.localPosition = Vector3.zero;
                minimapDisplay.transform.localRotation = Quaternion.identity;
                MiniMapApplied?.Invoke();
                UpdateDisplayZoom();

                return true;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"复制地图时发生错误: {e.Message}");
                return false;
            }
        }

        private static void UpdateMiniMapRotation()
        {
            try
            {
                if (minimapDisplay == null || miniMapNorthRect == null)
                {
                    return;
                }
                float rotationAngle = ModSettingManager.GetValue<bool>("miniMapRotation") ? MiniMapCommon.GetMinimapRotation() : MiniMapCommon.originMapZRotation;
                var rotation = Quaternion.Euler(0, 0, rotationAngle);
                minimapDisplay.transform.rotation = rotation;
                miniMapNorthRect.localRotation = rotation;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"更新小地图旋转时发生错误: {e.Message}");
            }
        }

        private static void UpdateMiniMapPosition()
        {
            try
            {
                if (minimapDisplay == null)
                {
                    return;
                }
                CharacterMainControl main = CharacterMainControl.Main;
                if (main == null)
                {
                    return;
                }
                Vector3 minimapPos;
                if (!minimapDisplay.TryConvertWorldToMinimap(main.transform.position, SceneInfoCollection.GetSceneID(SceneManager.GetActiveScene().buildIndex), out minimapPos))
                {
                    return;
                }
                if (minimapDisplay.transform is not RectTransform rectTransform || rectTransform.parent is not RectTransform parentTransform)
                {
                    return;
                }
                Vector3 b = rectTransform.localToWorldMatrix.MultiplyPoint(minimapPos);
                Vector3 b2 = parentTransform.position - b;
                rectTransform.position += b2;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"更新小地图移动时发生错误: {e.Message}");
            }
        }

        // ==================== 新增的 Input System 方法 ====================


        // 新增的 Input System 相关字段
        private static Key _zoomInKey = ModSettingManager.GetValue<Key>("MiniMapZoomInKey");
        private static Key _zoomOutKey = ModSettingManager.GetValue<Key>("MiniMapZoomOutKey");
        private static InputAction? _toggleAction;       // 切换小地图显示/隐藏的按键动作
        private static InputAction? _zoomInAction;       // 放大按键动作
        private static InputAction? _zoomOutAction;      // 缩小按键动作
        private static bool _isKeyLook = false;         // 通用按键锁

        // UniTask 取消令牌源：用于控制异步任务的取消
        private static CancellationTokenSource? _zoomInCTS;   // 放大按键的异步任务取消令牌
        private static CancellationTokenSource? _zoomOutCTS;  // 缩小按键的异步任务取消令牌

        /// <summary>
        /// 初始化 Input System，创建三个按键动作并绑定事件
        /// 切换键：使用 performed 事件（按下时触发一次）
        /// 缩放键：使用 started（按下开始）和 canceled（释放）事件实现长按检测
        /// </summary>
        private static void InitializeInputSystem()
        {
            try
            {
                Key _toggleKey = ModSettingManager.GetValue<Key>("MiniMapToggleKey");
                // Key _toggleKey = Key.Digit9;
                // Key _zoomInKey = Key.Equals;
                // Key _zoomOutKey = Key.Minus;

                // 切换小地图按键：默认 M 键
                string ToggleKey = Keyboard.current[_toggleKey].path;
                _toggleAction = new InputAction("ToggleMiniMap", InputActionType.Button, ToggleKey);
                _toggleAction.performed += OnTogglePerformed;  // 按下时触发一次
                _toggleAction.Enable();

                // 放大按键：默认 = 键
                string ZoomInKey = Keyboard.current[_zoomInKey].path;
                _zoomInAction = new InputAction("ZoomInMiniMap", InputActionType.Button, ZoomInKey);
                _zoomInAction.started += OnZoomInStarted;      // 按键按下开始
                _zoomInAction.canceled += OnZoomInCanceled;   // 按键释放
                _zoomInAction.Enable();

                // 缩小按键：默认 - 键
                string ZoomOutKey = Keyboard.current[_zoomOutKey].path;
                _zoomOutAction = new InputAction("ZoomOutMiniMap", InputActionType.Button, ZoomOutKey);
                _zoomOutAction.started += OnZoomOutStarted;    // 按键按下开始
                _zoomOutAction.canceled += OnZoomOutCanceled; // 按键释放
                _zoomOutAction.Enable();
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"初始化 Input System 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 切换小地图显示/隐藏（按下切换键时触发）
        /// 和原有的 CheckToggleKey() 功能相同，但使用 Input System 事件驱动
        /// </summary>
        private static void OnTogglePerformed(InputAction.CallbackContext context)
        {
            if (isEnabled && minimapObject != null)
            {
                isToggled = !isToggled;           // 反转显示状态
                customCanvas?.SetActive(isToggled); // 显示/隐藏 Canvas
            }
        }

        /// <summary>
        /// 放大按键按下开始（Input System 的 started 事件）
        /// 启动缩放控制：先取消之前的任务，再创建新任务
        /// </summary>
        private static void OnZoomInStarted(InputAction.CallbackContext context)
        {
            CancelZoomIn();  // 取消可能还在运行的放大任务
            _zoomInCTS = new CancellationTokenSource();  // 创建新的取消令牌源
            ZoomThrottleLoop(1, _zoomInCTS.Token, _zoomOutKey).Forget();  // 启动缩放控制任务（1 = 放大方向）
        }

        /// <summary>
        /// 放大按键释放（Input System 的 canceled 事件）
        /// 停止缩放控制
        /// </summary>
        private static void OnZoomInCanceled(InputAction.CallbackContext context)
        {
            CancelZoomIn();  // 取消放大任务
        }

        /// <summary>
        /// 缩小按键按下开始
        /// 启动缩放控制：先取消之前的任务，再创建新任务
        /// </summary>
        private static void OnZoomOutStarted(InputAction.CallbackContext context)
        {
            CancelZoomOut();  // 取消可能还在运行的缩小任务
            _zoomOutCTS = new CancellationTokenSource();  // 创建新的取消令牌源
            ZoomThrottleLoop(-1, _zoomOutCTS.Token, _zoomInKey).Forget();  // 启动缩放控制任务（-1 = 缩小方向）
        }

        /// <summary>
        /// 缩小按键释放
        /// 停止缩放控制
        /// </summary>
        private static void OnZoomOutCanceled(InputAction.CallbackContext context)
        {
            CancelZoomOut();  // 取消缩小任务
        }

        /// <summary>
        /// 取消放大按键的 UniTask 异步任务
        /// 1. 发送取消信号给正在运行的 ZoomThrottleLoop
        /// 2. 释放 CancellationTokenSource 资源
        /// 3. 清空引用以便垃圾回收
        /// </summary>
        private static void CancelZoomIn()
        {
            _zoomInCTS?.Cancel();   // 发送取消信号，触发 OperationCanceledException
            _zoomInCTS?.Dispose();  // 释放资源
            _zoomInCTS = null;      // 清空引用
        }

        /// <summary>
        /// 取消缩小按键的 UniTask 异步任务
        /// </summary>
        private static void CancelZoomOut()
        {
            _zoomOutCTS?.Cancel();   // 发送取消信号
            _zoomOutCTS?.Dispose();  // 释放资源
            _zoomOutCTS = null;      // 清空引用
        }

        /// <summary>
        /// 缩放控制的核心异步任务
        /// 实现节流控制：0.5秒内立即缩放一次，0.5秒后每0.25秒缩放一次
        /// 使用 CancellationToken 实现按键释放时立即停止
        /// </summary>
        /// <param name="direction">缩放方向：1 = 放大，-1 = 缩小</param>
        /// <param name="token">取消令牌，按键释放时自动取消任务</param>
        private static async UniTaskVoid ZoomThrottleLoop(int direction, CancellationToken token, Key otherKey)
        {
            try
            {
                // 阶段1：立即执行一次缩放（0.2秒内）
                if (!Keyboard.current[otherKey].isPressed)
                {
                    await UniTask.SwitchToMainThread(); // 回到主线程
                    DisplayZoom(direction);
                }

                // 等待0.2秒（使用 UniTask.Delay 而不是协程的 WaitForSeconds）
                await UniTask.Delay(200, DelayType.Realtime, PlayerLoopTiming.Update, token);

                // 阶段2：进入节流模式（0.2秒后，每0.75秒执行一次）
                while (!token.IsCancellationRequested)
                {
                    // 检查另一个缩放键是否也被按下，如果同时按下则停止，松开继续
                    if (Keyboard.current[otherKey].isPressed)
                    {
                        ModBehaviour.Logger.Log($"另一个缩放键被按下，停止当前缩放");
                        await UniTask.Delay(100, DelayType.Realtime, PlayerLoopTiming.Update, token);
                        continue;
                    }
                    await UniTask.SwitchToMainThread(); // 回到主线程
                    DisplayZoom(direction);  // 执行节流缩放
                    await UniTask.Delay(75, DelayType.Realtime, PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消：按键释放时 CancellationTokenSource.Cancel() 触发此异常
                // 不需要处理，直接退出任务
            }
        }

        /// <summary>
        /// 清理 Input System 相关资源
        /// 在 Mod 禁用或销毁时调用
        /// 1. 取消所有正在运行的缩放任务
        /// 2. 释放 InputAction 资源
        /// 3. 清空所有引用
        /// </summary>
        private static void CleanupInputSystem()
        {
            // 取消所有缩放任务
            CancelZoomIn();
            CancelZoomOut();

            // 释放 InputAction 资源（会自动取消事件绑定）
            _toggleAction?.Dispose();
            _zoomInAction?.Dispose();
            _zoomOutAction?.Dispose();

            // 清空引用
            _toggleAction = null;
            _zoomInAction = null;
            _zoomOutAction = null;
        }

        /// <summary>
        /// 更新按键绑定
        /// 当用户修改按键配置时调用，动态更新 Input System 的绑定
        /// 需要先禁用 InputAction，修改绑定，再重新启用
        /// </summary>
        private static void UpdateInputBindings()
        {
            // 从配置获取最新的按键绑定
            Key _toggleKey = ModSettingManager.GetValue<Key>("MiniMapToggleKey");
            _zoomInKey = ModSettingManager.GetValue<Key>("MiniMapZoomInKey");
            _zoomOutKey = ModSettingManager.GetValue<Key>("MiniMapZoomOutKey");
            // Key _toggleKey = Key.Digit9;
            // Key _zoomInKey = Key.Equals;
            // Key _zoomOutKey = Key.Minus;

            string? ToggleKey = Keyboard.current[_toggleKey].path;
            string? ZoomInKey = Keyboard.current[_zoomInKey].path;
            string? ZoomOutKey = Keyboard.current[_zoomOutKey].path;

            // 更新切换键绑定
            if (_toggleAction != null)
            {
                _toggleAction.Disable();  // 必须先禁用才能修改绑定
                _toggleAction.ApplyBindingOverride(0, ToggleKey);  // 应用新的按键绑定
                _toggleAction.Enable();   // 重新启用
            }

            // 更新放大键绑定
            if (_zoomInAction != null)
            {
                _zoomInAction.Disable();
                _zoomInAction.ApplyBindingOverride(0, ZoomInKey);
                _zoomInAction.Enable();
            }

            // 更新缩小键绑定
            if (_zoomOutAction != null)
            {
                _zoomOutAction.Disable();
                _zoomOutAction.ApplyBindingOverride(0, ZoomOutKey);
                _zoomOutAction.Enable();
            }
        }
    }
}