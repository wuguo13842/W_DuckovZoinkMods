using Cysharp.Threading.Tasks;
using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Modding;
using LeTai.TrueShadow;
using MiniMap.MonoBehaviours;
using MiniMap.Poi;
using MiniMap.Utils;
using SodaCraft.Localizations;
using System;
using System.Collections;
using System.Threading;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;
using ZoinkModdingLibrary.Extentions;
using ZoinkModdingLibrary.GameUI;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Utils;
using System.Linq;
using Duckov.Scenes;

namespace MiniMap.Managers
{
    [Log(LogOutput.Output, LogOutput.Output, LogLevel.Debug, LogLevel.Debug)]
    public static class MinimapManager
    {
        public static event Action? MiniMapApplied;
        public static bool isEnabled = false;
        public static bool isToggled = false;
        public static bool IsInitialized { get; private set; } = false;

        private static Vector2 miniMapSize = new Vector2(200f, 200f);
        private static float northFontSize = 18f;

        public static float MapBorderEulerZRotation = 0f;
        public static Vector2 displayZoomRange = new Vector2(0.25f, 4f);
        public static Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        public static GameObject? miniMapScaleContainer;

        private static GameObject? customCanvas;
        private static GameObject? miniMapContainer;
        private static GameObject? minimapObject;

        private static MiniMapDisplay? minimapDisplay;
        private static MiniMapDisplay? originalDisplay;
		
		private static float lastNonBaseZoom = 1f; // 保存上次非Base场景的缩放值
		private static bool isInBaseScene = false; // 标记是否在Base场景
		public static float CurrentMapWorldSize { get; private set; } = 1000f; // 默认1000米 添加字段记录当前地图尺寸

        public static MiniMapDisplay? MinimapDisplay => minimapDisplay;
        public static MiniMapDisplay? OriginalDisplay
        {
            get
            {
                if (originalDisplay == null || originalDisplay.IsDestroyed())
                    originalDisplay = GetOriginalDisplay();
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
                Log.Error($"MinimapManager 已初始化");
                return;
            }
            CreateMiniMapContainer();
            InitializeInputSystem();
            //ModSettingManager.ConfigLoaded += OnConfigLoaded;
            ModSettingManager.ConfigChanged += OnConfigChanged;
            ModSettingManager.ButtonClicked += OnButtonClicked;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneLoader.onFinishedLoadingScene += onFinishedLoadingScene;
			SceneLoader.onAfterSceneInitialize += OnAfterSceneInitialize;  // 整个场景加载流程完全结束，包括所有过渡动画完成后  不要动测试必须是OnAfterSceneInitialize， MultiSceneCore.ActiveSubSceneID才生效

            IsInitialized = true;
        }

        public static void Destroy()
        {
            Log.Info($"正在销毁小地图容器");
            GameObject.Destroy(miniMapContainer);
            CleanupInputSystem();
            //ModSettingManager.ConfigLoaded -= OnConfigLoaded;
            ModSettingManager.ConfigChanged -= OnConfigChanged;
            ModSettingManager.ButtonClicked -= OnButtonClicked;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneLoader.onFinishedLoadingScene -= onFinishedLoadingScene;
			SceneLoader.onAfterSceneInitialize -= OnAfterSceneInitialize;

            IsInitialized = false;
        }
		
private static void OnAfterSceneInitialize(SceneLoadingContext context)
{
    try
    {
        Log.Info($"调整缩放范围 - 当前场景: {context.sceneName}");
        
        if (MiniMapSettings.Instance == null) return;
        
        string mapSceneID = MultiSceneCore.ActiveSubSceneID;
        if (string.IsNullOrEmpty(mapSceneID)) return;
        
        var map = MiniMapSettings.Instance.maps.FirstOrDefault(e => e.sceneID == mapSceneID);
        if (map == null || map.imageWorldSize <= 0) return;
        
        // 判断当前场景
        bool isBaseScene = context.sceneName == "Base";
        
        if (isBaseScene)
        {
            // Base场景处理
            HandleBaseScene();
        }
        else
        {
            // 非Base场景处理
            HandleNonBaseScene(map, mapSceneID);
        }
    }
    catch (Exception e)
    {
        Log.Error($"错误: {e.Message}");
    }
}

private static void HandleBaseScene()
{
    // Base场景：固定4倍缩放
    displayZoomRange = new Vector2(4f, 4f);
    
    // 保存当前缩放到Mod设置（使用特殊键）
    float currentZoom = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "displayZoomScale");
    
    // 检查是否已经保存过（默认值设为-1表示没保存过）
    float savedValue = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "savedZoomBeforeBase", -1f);
    if (savedValue < 0)
    {
        ModSettingManager.SaveValue(ModBehaviour.ModInfo, "savedZoomBeforeBase", currentZoom);
        Log.Info($"进入Base场景，保存缩放到设置: {currentZoom:F2}x");
    }
    
    // 强制设置为4倍
    ModSettingManager.SaveValue(ModBehaviour.ModInfo, "displayZoomScale", 4f);
    UpdateDisplayZoom();
    
    Log.Info($"Base场景固定缩放: 4.00x");
}

private static void HandleNonBaseScene(MiniMapSettings.MapEntry map, string mapSceneID)
{
	CurrentMapWorldSize = map.imageWorldSize; // 保存当前地图尺寸
	
    // 非Base场景：正常计算缩放
    float minZoom = Mathf.Clamp(0.25f * (1000f / map.imageWorldSize), 0.25f, 4f);
    displayZoomRange = new Vector2(minZoom, 4f);
    
    // 检查是否有保存的缩放值（默认值-1表示没有保存过）
    float savedZoom = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "savedZoomBeforeBase", -1f);
    float targetZoom;
    
    if (savedZoom >= 0)
    {
        // 使用保存的缩放值，并清除保存
        targetZoom = savedZoom;
        ModSettingManager.SaveValue(ModBehaviour.ModInfo, "savedZoomBeforeBase", -1f);
        Log.Info($"使用保存的缩放: {targetZoom:F2}x");
    }
    else
    {
        // 使用当前的缩放值
        targetZoom = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "displayZoomScale");
    }
    
    // 调整到范围内
    float clampedZoom = Mathf.Clamp(targetZoom, minZoom, 4f);
    
    if (Mathf.Abs(targetZoom - clampedZoom) > 0.01f)
    {
        Log.Info($"缩放调整: {targetZoom:F2}x → {clampedZoom:F2}x");
    }
    
    ModSettingManager.SaveValue(ModBehaviour.ModInfo, "displayZoomScale", clampedZoom);
    UpdateDisplayZoom();
    
    Log.Info($"非Base场景: {map.sceneID}, 尺寸: {map.imageWorldSize:F0}米, 缩放: {clampedZoom:F2}x");
}

        private static void onFinishedLoadingScene(SceneLoadingContext context)
        {    
			// ============ 直接在这里处理，不通过 ApplyConfigs ============
			try
			{
				// 只更新位置、缩放、按键绑定
				OnMinimapPositionChanged();
				OnMinimapContainerScaleChanged();
				UpdateInputBindings();
                bool enabled = ModSettingManager.GetValue<bool>(ModBehaviour.ModInfo, "enableMiniMap");
                isEnabled = enabled;
                isToggled = enabled;
				
				// 不调用 OnEnabledChanged()
			}
			catch { }
			
            if (string.IsNullOrEmpty(context.sceneName)) return;
            // 预加载场景中心点（触发缓存填充）
            Duckov.MiniMaps.UI.CharacterPoiEntry.GetSceneCenterFromSettings(context.sceneName);
        }

        private static void OnConfigChanged(ModInfo modInfo, string key, object? value)
        {
            if (!modInfo.ModIdEquals(ModBehaviour.Instance!.info)) return;
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

        private static void ApplyConfigs()
        {
            OnEnabledChanged();
            OnMinimapPositionChanged();
            OnMinimapContainerScaleChanged();
            UpdateInputBindings();
        }

        private static void OnButtonClicked(ModInfo modInfo, string key)
        {
            if (!modInfo.ModIdEquals(ModBehaviour.Instance!.info)) return;
            switch (key)
            {
                case "resetAllButton":
                    ModSettingManager.CreateUI(ModBehaviour.ModInfo, true);
                    break;
            }
        }

        public static void OnEnabledChanged()
        {
            try
            {
                bool enabled = ModSettingManager.GetValue<bool>(ModBehaviour.ModInfo, "enableMiniMap");
                isEnabled = enabled;
                isToggled = enabled;
                customCanvas?.SetActive(isEnabled && LevelManager.Instance != null);
            }
            catch (Exception e)
            {
                Log.Error($"设置小地图开关时发生错误: {e.Message}");
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
                Log.Error($"尝试显示小地图时发生错误: {e.Message}");
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
                Log.Error($"隐藏小地图时发生错误: {e.Message}");
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
                Log.Error($"调整设置时发生错误: {e.Message}");
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
            float displayZoomScale = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "displayZoomScale");
            float miniMapZoomStep = 0.1f;
            displayZoomScale += symbol * miniMapZoomStep;
            displayZoomScale = Mathf.Clamp(displayZoomScale, displayZoomRange.x, displayZoomRange.y);
            ModSettingManager.SaveValue(ModBehaviour.ModInfo, "displayZoomScale", displayZoomScale);
            UpdateDisplayZoom();
        }

        private static void UpdateDisplayZoom()
        {
            try
            {
                float displayZoomScale = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "displayZoomScale");
                if (minimapDisplay != null)
                {
                    minimapDisplay.transform.localScale = Vector3.one * displayZoomScale;
                }
            }
            catch (Exception e)
            {
                Log.Error($"更新小地图缩放时发生错误: {e.Message}");
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
                Log.Error($"设置小地图大小时发生错误: {e.Message}");
            }
        }

        private static void RefreshMinimapContainerScale()
        {
            if (miniMapContainer == null)
            {
                return;
            }
            float miniMapWindowScale = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "miniMapWindowScale");
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
                    Log.Warning($"父Canvas尺寸异常: {parentRect.rect.width} x {parentRect.rect.height}");
                    return;
                }
                float miniMapPositionX = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "miniMapPositionX");
                float miniMapPositionY = ModSettingManager.GetValue<float>(ModBehaviour.ModInfo, "miniMapPositionY");
                float offsetX = (miniMapPositionX - 0.5f) * parentWidth;
                float offsetY = (miniMapPositionY - 0.5f) * parentHeight;
                if (miniMapRect != null)
                    miniMapRect.anchoredPosition = new Vector2(offsetX, offsetY);
            }
            catch (Exception e)
            {
                Log.Error($"更新小地图UI位置时发生错误: {e.Message}");
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                Log.Info($"正在加载场景 {scene.name}, 模式: {mode}");
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
                Log.Error($"加载场景时发生错误: {e.Message}");
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
                Log.Info($"等待角色初始化...");
                while (LevelManager.Instance.MainCharacter == null || LevelManager.Instance.PetCharacter == null)
                {
                    await UniTask.Delay(200);
                }
                Log.Info($"角色已完成初始化");
                Log.Info($"已生成小地图");
                PoiCommon.CreatePoiIfNeeded(LevelManager.Instance.MainCharacter, out _, out DirectionPointOfInterest? mainDirectionPoi);
                PoiCommon.CreatePoiIfNeeded(LevelManager.Instance.PetCharacter, out CharacterPointOfInterest? petPoi, out DirectionPointOfInterest? petDirectionPoi);
                MapMarkerManager.Instance.InvokeMethod("Load");
                customCanvas?.SetActive(true);
                await UniTask.Delay(500);
                minimapDisplay.InvokeMethod("HandlePointsOfInterests");
            }
            catch (Exception e)
            {
                Log.Error($"生成小地图时发生错误: {e.Message}");
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
                Log.Error($"处理UI遮挡时发生错误: {e.Message}");
            }
        }

        public static void Update()
        {
            try
            {
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
                Log.Error($"更新小地图时发生错误: {e.Message}");
            }
        }

        private static void CreateMiniMapContainer()
        {
            try
            {
                Log.Info($"创建小地图容器");
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
                Log.Info($"已创建小地图容器");
            }
            catch (Exception e)
            {
                Log.Error($"创建小地图容器时发生错误: {e.Message}");
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
                Log.Error($"获取原始地图时发生错误:{e.Message}");
                return null;
            }
        }

        public static bool DuplicateDisplay()
        {
            try
            {
                if (OriginalDisplay == null)
                {
                    Log.Error($"原始地图为空！");
                    return false;
                }
                if (minimapObject != null)
                {
                    Log.Error($"原始地图已复制，请勿重复复制！");
                    return false;
                }

                minimapObject = GameObject.Instantiate(OriginalDisplay.gameObject);
                minimapDisplay = minimapObject.GetComponent<MiniMapDisplay>();
                minimapDisplay.SetField("autoSetupOnEnable", true);
                minimapDisplay.InvokeMethod("UnregisterEvents");
                minimapDisplay.InvokeMethod("RegisterEvents");
                if (minimapDisplay == null || minimapObject == null)
                {
                    Log.Error($"获取 MinimapDisplay 复制体失败！");
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
                Log.Error($"复制地图时发生错误: {e.Message}");
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
                float rotationAngle = ModSettingManager.GetValue<bool>(ModBehaviour.ModInfo, "miniMapRotation") ? MiniMapCommon.GetMinimapRotation() : MiniMapCommon.originMapZRotation;
                var rotation = Quaternion.Euler(0, 0, rotationAngle);
                minimapDisplay.transform.rotation = rotation;
                miniMapNorthRect.localRotation = rotation;
            }
            catch (Exception e)
            {
                Log.Error($"更新小地图旋转时发生错误: {e.Message}");
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
                Log.Error($"更新小地图移动时发生错误: {e.Message}");
            }
        }

        // ==================== 新增的 Input System 方法 ====================


        // 新增的 Input System 相关字段
        private static Key _zoomInKey = ModSettingManager.GetValue<Key>(ModBehaviour.ModInfo, "MiniMapZoomInKey");
        private static Key _zoomOutKey = ModSettingManager.GetValue<Key>(ModBehaviour.ModInfo, "MiniMapZoomOutKey");
		// private static Key _zoomInKey = Key.Equals;
        // private static Key _zoomOutKey = Key.Minus;
		
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


                Key _toggleKey = ModSettingManager.GetValue<Key>(ModBehaviour.ModInfo, "MiniMapToggleKey");

                // Key _toggleKey = Key.Digit9;
                // Key _zoomInKey = Key.Equals;
                // Key _zoomOutKey = Key.Minus;

                // 切换小地图按键：默认 M 键
                Log.Debug($"_toggleKey: {_toggleKey}");
                if (_toggleKey != Key.None)
                {
                    string ToggleKey = Keyboard.current[_toggleKey].path;
                    _toggleAction = new InputAction("ToggleMiniMap", InputActionType.Button, ToggleKey);
                    _toggleAction.performed += OnTogglePerformed;  // 按下时触发一次
                    _toggleAction.Enable();
                }

                // 放大按键：默认 = 键
                Log.Debug($"_zoomInKey: {_zoomInKey}");
                if (_zoomInKey != Key.None)
                {
                    string ZoomInKey = Keyboard.current[_zoomInKey].path;
                    _zoomInAction = new InputAction("ZoomInMiniMap", InputActionType.Button, ZoomInKey);
                    _zoomInAction.started += OnZoomInStarted;      // 按键按下开始
                    _zoomInAction.canceled += OnZoomInCanceled;   // 按键释放
                    _zoomInAction.Enable();
                }

                // 缩小按键：默认 - 键
                Log.Debug($"_zoomOutKey: {_zoomOutKey}");
                if (_zoomOutKey != Key.None)
                {
                    string ZoomOutKey = Keyboard.current[_zoomOutKey].path;
                    _zoomOutAction = new InputAction("ZoomOutMiniMap", InputActionType.Button, ZoomOutKey);
                    _zoomOutAction.started += OnZoomOutStarted;    // 按键按下开始
                    _zoomOutAction.canceled += OnZoomOutCanceled; // 按键释放
                    _zoomOutAction.Enable();
                }
            }
            catch (Exception e)
            {
                Log.Error($"初始化 Input System 失败: {e.Message}");
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
			Log.Info($"放大按键：默认 = 键");
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
			Log.Info($"缩小按键：默认 - 键");
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
                        Log.Info($"另一个缩放键被按下，停止当前缩放");
                        await UniTask.Delay(100, DelayType.Realtime, PlayerLoopTiming.Update, token);
                        continue;
                    }
					// Log.Info($"放大缩小循环");
                    await UniTask.SwitchToMainThread(); // 回到主线程
                    DisplayZoom(direction);  // 执行节流缩放
                    await UniTask.Delay(20, DelayType.Realtime, PlayerLoopTiming.Update, token);
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
            Key _toggleKey = ModSettingManager.GetValue<Key>(ModBehaviour.ModInfo, "MiniMapToggleKey");
            _zoomInKey = ModSettingManager.GetValue<Key>(ModBehaviour.ModInfo, "MiniMapZoomInKey");
            _zoomOutKey = ModSettingManager.GetValue<Key>(ModBehaviour.ModInfo, "MiniMapZoomOutKey");
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