using Duckov.MiniMaps.UI;
using LeTai.TrueShadow;
using MiniMap.Poi;
using MiniMap.Utils;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;
using ZoinkModdingLibrary.GameUI;

namespace MiniMap.Managers
{
    public static class PoiManager
    {
        private static GameObject? characterPoiEntryPrefab;
        public static GameObject? CharacterPoiEntryPrefab => characterPoiEntryPrefab;

        public static void OnEnable()
        {
            MinimapManager.MiniMapApplied += OnMiniMapApplied;
        }

        public static void OnDisable()
        {
            MinimapManager.MiniMapApplied -= OnMiniMapApplied;
        }

        private static void OnMiniMapApplied()
        {
            if (characterPoiEntryPrefab == null || characterPoiEntryPrefab.IsDestroyed())
            {
                CreatePrefab();
            }
        }

        public static void CreatePrefab()
        {
            if (MinimapManager.OriginalDisplay == null)
            {
                return;
            }
            CharacterPoiEntryData entryData = new CharacterPoiEntryData();

            GameObject prefabObj = new("CharacterPoiEntry");
            RectTransform prefabRect = prefabObj.AddComponent<RectTransform>();
            prefabRect.SetParent(MinimapManager.OriginalDisplay.transform);
            prefabRect.sizeDelta = new Vector2(100f, 100f);
            prefabRect.localPosition = Vector3.zero;

            if (UIElements.CreateFilledRectTransform(prefabRect, "AreaDisplay", out GameObject? areaDisplayObj, out RectTransform? areaDisplayRect))
            {
                ProceduralImage areaDisplay = areaDisplayObj!.AddComponent<ProceduralImage>();
                areaDisplayObj.AddComponent<RoundModifier>();
                areaDisplay.BorderWidth = 1f;
                areaDisplay.color = Color.white;
                entryData.areaDisplay = areaDisplay;
                if (UIElements.CreateFilledRectTransform(areaDisplayRect!, "AreaDisplay_Fill", out GameObject? areaDisplayFillObj, out RectTransform? areaDisplayFillRect))
                {
                    ProceduralImage areaDisplayFill = areaDisplayFillObj!.AddComponent<ProceduralImage>();
                    areaDisplayFillObj.AddComponent<RoundModifier>();
                    areaDisplayFill.BorderWidth = 0f;
                    areaDisplayFill.color = Color.white;
                    entryData.areaFill = areaDisplayFill;
                }
            }

            GameObject indicatorContainerObj = new("IndicatorContainer");
            RectTransform indicatorContainerRect = prefabObj.AddComponent<RectTransform>();
            indicatorContainerRect.SetParent(prefabRect);
            indicatorContainerRect.sizeDelta = new Vector2(50, 50);
            indicatorContainerRect.localPosition = Vector3.zero;
            entryData.indicatorContainer = indicatorContainerRect;

            if (UIElements.CreateFilledRectTransform(indicatorContainerRect, "IconContainer", out GameObject? iconContainerObj, out RectTransform? iconContainerRect))
            {
                entryData.iconContainer = iconContainerRect;
                if (UIElements.CreateFilledRectTransform(iconContainerRect!, "Icon", out GameObject? iconObj, out RectTransform? iconRect))
                {
                    Image icon = iconObj!.AddComponent<Image>();
                    TrueShadow shadow = iconObj.AddComponent<TrueShadow>();
                    entryData.icon = icon;
                    entryData.shadow = shadow;
                }
                if (UIElements.CreateFilledRectTransform(iconContainerRect!, "Direction", out GameObject? directionObj, out RectTransform? directionRect))
                {
                    entryData.direction = directionRect;
                    GameObject arrowObj = new GameObject("Arrow");
                    RectTransform arrowRect = arrowObj.AddComponent<RectTransform>();
                    arrowRect.SetParent(directionRect!);
                    arrowRect.sizeDelta = new Vector2(50f, 50f);
                    arrowRect.pivot = new Vector2(0.5f, 0f);
                    arrowRect.anchorMin = new Vector2(0.5f, 1f);
                    arrowRect.anchorMax = new Vector2(0.5f, 1f);
                    Image arrow = arrowObj.AddComponent<Image>();
                    entryData.arrow = arrow;
                }
                GameObject displayNameObj = new GameObject("DiaplayName");
                RectTransform displayNameRect = displayNameObj.AddComponent<RectTransform>();
                displayNameRect.SetParent(directionRect!);
                displayNameRect.sizeDelta = new Vector2(50f, 50f);
                displayNameRect.pivot = new Vector2(0.5f, 1f);
                displayNameRect.anchorMin = new Vector2(0.5f, 0f);
                displayNameRect.anchorMax = new Vector2(0.5f, 0f);
                TextMeshProUGUI displayName = displayNameObj.AddComponent<TextMeshProUGUI>();
                displayName.alignment = TextAlignmentOptions.Top;
                displayName.fontSize = 24f;
                entryData.displayName = displayName;
            }

            CharacterPoiEntry characterPoiEntry = prefabObj.AddComponent<CharacterPoiEntry>();
            characterPoiEntry.Initialize(entryData);
            prefabObj.SetActive(false);

            characterPoiEntryPrefab = prefabObj;
        }
    }
}
