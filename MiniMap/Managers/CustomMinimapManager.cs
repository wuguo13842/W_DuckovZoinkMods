using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using MiniMap.MonoBehaviours;
using MiniMap.Poi;
using MiniMap.Utils;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZoinkModdingLibrary.GameUI;
using ZoinkModdingLibrary.Patcher;
using ZoinkModdingLibrary.Utils;

namespace MiniMap.Managers
{
    public static class CustomMinimapManager
    {
        public static bool isEnabled = true;
        public static bool isToggled = false;
        public static bool IsInitialized { get; private set; } = false;

        public static Vector2 miniMapSize = new Vector2(200, 200);

        public static float MapBorderEulerZRotation = 0f;
        public static float MapNorthEulerZRotation = 45f;
        public static Vector2 displayZoomRange = new Vector2(0.1f, 30);

        public static Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        private static GameObject? customCanvas;
        private static GameObject? miniMapContainer;
        public static GameObject? miniMapScaleContainer;
        private static GameObject? duplicatedMinimapObject;

        public static MiniMapDisplay? DuplicatedMinimapDisplay
        {
            get
            {
                return duplicatedMinimapDisplay;
            }
        }
        private static MiniMapDisplay? duplicatedMinimapDisplay;
        public static MiniMapDisplay? OriginalMinimapDisplay
        {
            get
            {
                return originalMinimapDisplay;
            }
        }
        private static MiniMapDisplay? originalMinimapDisplay;
        private static RectTransform? miniMapRect;
        private static RectTransform? miniMapViewportRect;
        private static RectTransform? miniMapNorthRect;

        private static Coroutine? settingCor;
        private static Coroutine? initMapCor;

        public static void Initialize()
        {
            if (IsInitialized)
            {
                ModBehaviour.Logger.LogError($"MinimapManager 已初始化");
                return;
            }
            CreateMiniMapContainer();
            ModSettingManager.ConfigLoaded += OnConfigLoaded;
            ModSettingManager.ConfigChanged += OnConfigChanged;
            ModSettingManager.ButtonClicked += OnButtonClicked;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            SceneManager.sceneLoaded += OnSceneLoaded;

            IsInitialized = true;
        }

        public static void Destroy()
        {
            ModBehaviour.Logger.Log($"destroy minimap container");
            GameObject.Destroy(miniMapContainer);
            ModSettingManager.ConfigLoaded -= OnConfigLoaded;
            ModSettingManager.ConfigChanged -= OnConfigChanged;
            ModSettingManager.ButtonClicked -= OnButtonClicked;
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
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
            }
        }

        private static void OnConfigLoaded()
        {
            OnEnabledChanged();
            OnMinimapPositionChanged();
            OnMinimapContainerScaleChanged();
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

        public static void CheckToggleKey()
        {
            try
            {
                if (!(isEnabled && duplicatedMinimapObject != null))
                    return;
                if (Input.GetKeyDown(ModSettingManager.GetValue<KeyCode>("miniMapToggleKey")))
                {
                    if (customCanvas != null)
                    {
                        isToggled = !isToggled;
                        customCanvas.SetActive(isToggled);
                    }
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error Checking ToggleKey: {e.Message}");
            }
        }

        public static void OnEnabledChanged()
        {
            try
            {
                bool enabled = ModSettingManager.GetValue<bool>("enableMiniMap");
                isEnabled = enabled;
                isToggled = enabled;
                if (isEnabled)
                {
                    ApplyMiniMap();
                    customCanvas?.SetActive(true);
                }
                else
                {
                    customCanvas?.SetActive(false);
                    ClearMap();
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error setting Enable: {e.Message}");
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
                ModBehaviour.Logger.LogError($"Error trying to show minimap: {e.Message}");
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
                ModBehaviour.Logger.LogError($"Error hiding minimap: {e.Message}");
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
                ModBehaviour.Logger.LogError($"Error starting setting minimap: {e.Message}");
            }
        }

        public static IEnumerator FinishSetting()
        {
            yield return new WaitForSecondsRealtime(1f);
            miniMapContainer?.transform.SetParent(customCanvas?.transform);
        }

        public static bool HasMap()
        {
            return isEnabled && IsInitialized && duplicatedMinimapObject != null && (customCanvas?.activeInHierarchy ?? false);
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
                if (duplicatedMinimapDisplay != null)
                {
                    duplicatedMinimapDisplay.transform.localScale = Vector3.one * displayZoomScale;
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error updating display zoom: {e.Message}");
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
                ModBehaviour.Logger.LogError($"Error setting minimap container scale: {e.Message}");
            }
        }

        private static void RefreshMinimapContainerScale()
        {
            if (miniMapContainer == null)
            {
                return;
            }
            float miniMapWindowScale = ModSettingManager.GetValue<float>("miniMapWindowScale");
            Vector3 scaleVector = Vector3.one * miniMapWindowScale;
            if (miniMapContainer.transform.localScale != scaleVector)
                miniMapContainer.transform.localScale = scaleVector;
        }

        private static void OnMinimapPositionChanged()
        {
            try
            {
                RefreshMinimapPosition();
                if (!HasMap())
                    return;
                StartSetting();
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error setting minimap position: {e.Message}");
            }
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
                ModBehaviour.Logger.LogError($"Error setting minimap position: {e.Message}");
            }
        }

        public static void OnAfterLevelInitialized()
        {
            try
            {
                initMapCor = ModBehaviour.Instance?.StartCoroutine(ApplyMiniMapCoroutine());
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error on scene loaded: {e.Message}");
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                ModBehaviour.Logger.Log($"加载场景 {scene.name} {mode}");
                ModBehaviour.Logger.Log($"Level Initialized");
                if (LevelManager.Instance == null || !isEnabled)
                {
                    customCanvas?.SetActive(false);
                    return;
                }
                ModBehaviour.Logger.Log($"初始化场景 {scene} {mode}");

                if (ModBehaviour.Instance == null)
                {
                    ModBehaviour.Logger.LogError($"ModBehaviour Instance is null!");
                    return;
                }
                if (initMapCor != null)
                    ModBehaviour.Instance.StopCoroutine(initMapCor);
                ClearMap();
                customCanvas?.SetActive(false);
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error on scene loaded: {e.Message}");
            }
        }

        public static void ClearMap()
        {
            try
            {
                if (duplicatedMinimapDisplay != null)
                {
                    CallDisplayMethod("UnregisterEvents");
                }
                GameObject.Destroy(duplicatedMinimapObject);
                GameObject.Destroy(duplicatedMinimapDisplay);
                duplicatedMinimapDisplay = null;
                duplicatedMinimapObject = null;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error clearing map: {e.Message}");
            }
        }

        private static void HandleUIBlockState()
        {
            try
            {
                bool minimapIsOn = isEnabled && IsInitialized && duplicatedMinimapObject != null;
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
                ModBehaviour.Logger.LogError($"Error handling UI block state: {e.Message}");
            }
        }

        public static void Update()
        {
            try
            {
                var displayEntries = duplicatedMinimapDisplay?.GetComponentsInChildren<MiniMapDisplayEntry>();
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

                KeyCode MiniMapZoomInKey = ModSettingManager.GetValue<KeyCode>("MiniMapZoomInKey");
                KeyCode MiniMapZoomOutKey = ModSettingManager.GetValue<KeyCode>("MiniMapZoomOutKey");

                if (Input.GetKeyDown(MiniMapZoomOutKey))
                {
                    DisplayZoom(-1);
                }
                else if (Input.GetKeyDown(MiniMapZoomInKey))
                {
                    DisplayZoom(1);
                }
                UpdateMiniMapRotation();
                UpdateMiniMapPosition();
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error updating minimap: {e.Message}");
            }
        }

        public static IEnumerator ApplyMiniMapCoroutine()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            ModBehaviour.Logger.Log($"初始化小地图");
            ApplyMiniMap();
            customCanvas?.SetActive(true);
        }

        public static void ApplyMiniMap()
        {
            try
            {
                if (DuplicateMinimapDisplay())
                {
                    if (ApplyDuplicatedMinimap())
                    {
                        ModBehaviour.Logger.Log($"MiniMap Applied!");
                        PoiCommon.CreatePoiIfNeeded(LevelManager.Instance?.MainCharacter, out _, out DirectionPointOfInterest? mainDirectionPoi);
                        PoiCommon.CreatePoiIfNeeded(LevelManager.Instance?.PetCharacter, out CharacterPointOfInterest? petPoi, out DirectionPointOfInterest? petDirectionPoi);
                        AssemblyOption.InvokeMethod(MapMarkerManager.Instance, "Load");
                    }
                }
                else
                {
                    ModBehaviour.Logger.LogError($"Failed to Apply MiniMap!");
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error Applying minimap: {e.Message}");
            }
        }

        private static void CreateMiniMapContainer()
        {
            try
            {
                ModBehaviour.Logger.Log($"Creating Mini Map Container");
                // 创建 Canvas
                customCanvas = new GameObject("Zoink_MinimapCanvas");
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
                Image image = maskObject.AddComponent<Image>();
                image.sprite = ModFileOperations.LoadSprite("MiniMapMask.png");
                Mask mask = maskObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                // 创建边框区域
                UIElements.CreateFilledRectTransform(miniMapRect, "Zoink_MiniMapBorder", out GameObject? borderObject, out RectTransform? miniMapBorderRect);
                if (borderObject == null || miniMapBorderRect == null)
                {
                    return;
                }
                Image border = borderObject.AddComponent<Image>();
                border.sprite = ModFileOperations.LoadSprite("MiniMapBorder.png");
                miniMapBorderRect.eulerAngles = new Vector3(0f, 0f, MapBorderEulerZRotation);

                // 创建指北针区域
                UIElements.CreateFilledRectTransform(miniMapRect, "Zoink_MiniMapNorth", out GameObject? northObject, out miniMapNorthRect);
                if (northObject == null || miniMapNorthRect == null)
                {
                    return;
                }
                Image north = northObject.AddComponent<Image>();
                north.sprite = ModFileOperations.LoadSprite("MiniMapNorth.png");

                // 创建视窗区域
                GameObject viewportObject = new GameObject("Zoink_MiniMapViewport");
                miniMapViewportRect = viewportObject.AddComponent<RectTransform>();

                // 添加背景
                Image background = viewportObject.AddComponent<Image>();
                background.color = backgroundColor;

                miniMapScaleContainer = new GameObject("Zoink_MiniMapScaleContainer");
                var scaleRect = miniMapScaleContainer.AddComponent<RectTransform>();
                scaleRect.localScale = Vector3.one * 0.5f;
                scaleRect.SetParent(miniMapViewportRect);

                // 设置遮罩为容器的子对象，并填满容器
                miniMapViewportRect.SetParent(miniMapMaskRect);
                miniMapViewportRect.anchorMin = Vector2.zero;
                miniMapViewportRect.anchorMax = Vector2.one;
                miniMapViewportRect.offsetMin = Vector2.zero;
                miniMapViewportRect.offsetMax = Vector2.zero;

                customCanvas.SetActive(false);
                GameObject.DontDestroyOnLoad(customCanvas);
                ModBehaviour.Logger.Log($"Mini Map Container Created");
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error creating minimap container: {e.Message}");
            }
        }
        public static MiniMapDisplay? GetOriginalDisplay()
        {
            try
            {
                // 使用反射获取MinimapView单例
                Type minimapViewType = typeof(MiniMapView);
                if (minimapViewType == null)
                {
                    ModBehaviour.Logger.LogError($"MinimapView type not found!");
                    return null;
                }
                MiniMapView minimapView = MiniMapView.Instance;
                if (minimapView == null)
                {
                    ModBehaviour.Logger.LogError($"MinimapView instance not found!");
                    return null;
                }

                FieldInfo minimapDisplayField = minimapViewType.GetField("display",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (minimapDisplayField == null)
                {
                    PropertyInfo minimapDisplayProperty = minimapViewType.GetProperty("display",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (minimapDisplayProperty == null)
                    {
                        ModBehaviour.Logger.LogError($"display field/property not found!");
                        return null;
                    }

                    return minimapDisplayProperty.GetValue(minimapView) as MiniMapDisplay;
                }
                else
                {
                    return minimapDisplayField.GetValue(minimapView) as MiniMapDisplay;
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool DuplicateMinimapDisplay()
        {
            try
            {
                return DuplicateGameObject(GetOriginalDisplay());
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error duplicating minimapdisplay: {e.Message}");
                return false;
            }
        }

        private static bool DuplicateGameObject(MiniMapDisplay? originalDisplay)
        {
            try
            {
                if (originalDisplay == null)
                {
                    ModBehaviour.Logger.LogError($"Original minimapdisplay is null!");
                    return false;
                }

                originalMinimapDisplay = originalDisplay;

                GameObject originalGameObject = originalDisplay.gameObject;
                if (duplicatedMinimapObject != null)
                {
                    GameObject.Destroy(duplicatedMinimapObject);
                    GameObject.Destroy(duplicatedMinimapDisplay);
                }
                duplicatedMinimapObject = GameObject.Instantiate(originalGameObject);
                ModBehaviour.Logger.Log($"duplicated minimap object: {duplicatedMinimapObject}");

                duplicatedMinimapDisplay = duplicatedMinimapObject?.GetComponent(originalDisplay.GetType()) as MiniMapDisplay;
                AssemblyOption.SetField(duplicatedMinimapDisplay, "autoSetupOnEnable", true);
                //CallDisplayMethod("UnregisterEvents");
                //CallDisplayMethod("RegisterEvents");
                if (duplicatedMinimapDisplay == null || duplicatedMinimapObject == null)
                {
                    ModBehaviour.Logger.LogError($"Failed to get duplicated MinimapDisplay component!");
                    GameObject.Destroy(duplicatedMinimapObject);
                    GameObject.Destroy(duplicatedMinimapDisplay);
                    return false;
                }
                duplicatedMinimapObject.name = "Zoink_MiniMap_Duplicate";
                return true;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error duplicating minimap GameObject: {e.Message}");
                return false;
            }
        }

        private static bool ApplyDuplicatedMinimap()
        {
            try
            {
                ModBehaviour.Logger.Log($"Applying MiniMap");
                if (duplicatedMinimapObject == null || duplicatedMinimapDisplay == null || miniMapScaleContainer == null)
                {
                    ModBehaviour.Logger.LogError($"关键组件为null，无法应用当前地图！");
                    return false;
                }
                duplicatedMinimapObject.transform.SetParent(miniMapScaleContainer.transform);
                RectTransform rectTransform = duplicatedMinimapObject.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                    rectTransform.localScale = Vector3.one;
                }

                duplicatedMinimapObject.transform.localPosition = Vector3.zero;
                duplicatedMinimapDisplay.transform.localRotation = Quaternion.identity;
                CallDisplayMethod("AutoSetup");
                UpdateDisplayZoom();
                return true;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error setting up duplicated minimap: {e.Message}");
                return false;
            }
        }

        public static void CallDisplayMethod(string methodName)
        {
            if (duplicatedMinimapDisplay == null)
            {
                ModBehaviour.Logger.LogError($"Cannot call {methodName} - duplicatedMinimapDisplay is null!");
                return;
            }

            try
            {
                Type minimapDisplayType = duplicatedMinimapDisplay.GetType();
                MethodInfo autoSetupMethod = minimapDisplayType.GetMethod(methodName,
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (autoSetupMethod != null)
                {
                    autoSetupMethod.Invoke(duplicatedMinimapDisplay, null);
                }
                else
                {
                    ModBehaviour.Logger.LogWarning($"{methodName} method not found in MinimapDisplay type. This might be expected if the method doesn't exist.");
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error calling {methodName} method: {e.Message}");
            }
        }

        private static void UpdateMiniMapRotation()
        {
            try
            {
                if (duplicatedMinimapDisplay == null || miniMapNorthRect == null)
                {
                    return;
                }
                if (ModSettingManager.GetValue<bool>("miniMapRotation"))
                {
                    var rotation = MiniMapCommon.GetPlayerMinimapRotationInverse();
                    duplicatedMinimapDisplay.transform.rotation = rotation;
                    miniMapNorthRect.localRotation = Quaternion.Euler(0, 0, rotation.eulerAngles.z + MapNorthEulerZRotation);
                }
                else
                {
                    var rotation = Quaternion.Euler(0f, 0f, MiniMapCommon.originMapZRotation);
                    duplicatedMinimapDisplay.transform.rotation = rotation;
                    miniMapNorthRect.localRotation = Quaternion.Euler(0, 0, rotation.eulerAngles.z + MapNorthEulerZRotation);
                }
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error updating minimap rotation: {e.Message}");
            }
        }

        private static void UpdateMiniMapPosition()
        {
            try
            {
                if (duplicatedMinimapDisplay == null)
                {
                    return;
                }
                CharacterMainControl main = CharacterMainControl.Main;
                if (main == null)
                {
                    return;
                }
                Vector3 minimapPos;
                if (!duplicatedMinimapDisplay.TryConvertWorldToMinimap(main.transform.position, SceneInfoCollection.GetSceneID(SceneManager.GetActiveScene().buildIndex), out minimapPos))
                {
                    return;
                }
                if (duplicatedMinimapDisplay.transform is not RectTransform rectTransform || rectTransform.parent is not RectTransform parentTransform)
                {
                    return;
                }
                Vector3 b = rectTransform.localToWorldMatrix.MultiplyPoint(minimapPos);
                Vector3 b2 = parentTransform.position - b;
                rectTransform.position += b2;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Error updating minimap position: {e.Message}");
            }
        }
    }
}