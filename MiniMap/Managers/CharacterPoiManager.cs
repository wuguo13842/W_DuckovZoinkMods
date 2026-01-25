using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Scenes;
using Duckov.Utilities;
using LeTai.TrueShadow;
using MiniMap.Poi;
using MiniMap.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;
using ZoinkModdingLibrary.GameUI;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Managers
{
    public static class CharacterPoiManager
    {
        private static GameObject? characterPoiEntryPrefabObj;
        private static CharacterPoiEntry? characterPoiEntryPrefab;
        private static PrefabPool<CharacterPoiEntry>? _mapCharacterPoiEntryPool;
        private static PrefabPool<CharacterPoiEntry>? _miniMapCharacterPoiEntryPool;
        public static GameObject? CharacterPoiEntryPrefab => characterPoiEntryPrefabObj;

        private static List<CharacterPoiBase> points = new List<CharacterPoiBase>();

        private static ReadOnlyCollection<CharacterPoiBase>? points_ReadOnly;
        private static PrefabPool<CharacterPoiEntry>? MapCharacterPoiEntryPool
        {
            get
            {
                if (characterPoiEntryPrefab == null)
                {
                    CreatePrefab();
                }
                if (MinimapManager.OriginalDisplay == null)
                {
                    return null;
                }

                if (_mapCharacterPoiEntryPool == null)
                {
                    MiniMapDisplay display = MinimapManager.OriginalDisplay;
                    CharacterPoiEntry? prefab = display.transform.Find("CharacterPoiEntry").GetComponent<CharacterPoiEntry>();
                    if (prefab == null)
                    {
                        return null;
                    }
                    _mapCharacterPoiEntryPool = new PrefabPool<CharacterPoiEntry>(prefab, display.transform, OnGetCharacterPoiEntry);
                }

                return _mapCharacterPoiEntryPool;
            }
        }
        private static PrefabPool<CharacterPoiEntry>? MiniMapCharacterPoiEntryPool
        {
            get
            {
                if (characterPoiEntryPrefab == null)
                {
                    CreatePrefab();
                }
                if (MinimapManager.MinimapDisplay == null)
                {
                    return null;
                }

                if (_miniMapCharacterPoiEntryPool == null)
                {
                    MiniMapDisplay display = MinimapManager.MinimapDisplay;
                    CharacterPoiEntry? prefab = display.transform.Find("CharacterPoiEntry").GetComponent<CharacterPoiEntry>();
                    if (prefab == null)
                    {
                        return null;
                    }
                    _miniMapCharacterPoiEntryPool = new PrefabPool<CharacterPoiEntry>(prefab, display.transform, OnGetCharacterPoiEntry);
                }

                return _miniMapCharacterPoiEntryPool;
            }
        }
        public static ReadOnlyCollection<CharacterPoiBase> Points
        {
            get
            {
                if (points_ReadOnly == null)
                {
                    points_ReadOnly = new ReadOnlyCollection<CharacterPoiBase>(points);
                }

                return points_ReadOnly;
            }
        }

        public static event Action<MonoBehaviour>? PoiRegistered;
        public static event Action<MonoBehaviour>? PoiUnregistered;

        private static void OnGetCharacterPoiEntry(CharacterPoiEntry entry)
        {
            entry.gameObject.hideFlags |= HideFlags.DontSave;
        }

        public static void HandlePointsOfInterests(bool isOriginalMap)
        {
            PrefabPool<CharacterPoiEntry>? pool = isOriginalMap ? MapCharacterPoiEntryPool : MiniMapCharacterPoiEntryPool;
            if (pool == null) return;
            pool.ReleaseAll();
            foreach (CharacterPoiBase point in Points)
            {
                if (!(point == null))
                {
                    HandlePointOfInterest(point, isOriginalMap);
                }
            }
        }

        public static void HandlePointOfInterest(CharacterPoiBase poi, bool isOriginalMap)
        {
            PrefabPool<CharacterPoiEntry>? pool = isOriginalMap ? MapCharacterPoiEntryPool : MiniMapCharacterPoiEntryPool;
            MiniMapDisplay? display = isOriginalMap ? MinimapManager.OriginalDisplay : MinimapManager.MinimapDisplay;
            if (pool == null || display == null)
            {
                ModBehaviour.Logger.LogError($"CharacterPoiEntryPool:{pool?.ToString() ?? "null"}, MinimapDisplay: {display?.ToString() ?? "null"}");
                return;
            }
            ModBehaviour.Logger.Log($"正在创建标记点: {display.name}");
            //if (!poi.WillShow(isOriginalMap))
            //{
            //    return;
            //}
            int targetSceneIndex = poi.OverrideScene >= 0 ? poi.OverrideScene : poi.gameObject.scene.buildIndex;

            if (MultiSceneCore.ActiveSubScene.HasValue && targetSceneIndex == MultiSceneCore.ActiveSubScene.Value.buildIndex)
            {
                PrefabPool<MiniMapDisplayEntry>? mapEntryPool = display.GetProperty<PrefabPool<MiniMapDisplayEntry>>("MapEntryPool");
                if (mapEntryPool == null)
                {
                    ModBehaviour.Logger.LogError("MapEntryPool 为空");
                    return;
                }

                MiniMapDisplayEntry miniMapDisplayEntry = mapEntryPool.ActiveEntries.FirstOrDefault(e => e.SceneReference != null && e.SceneReference.BuildIndex == targetSceneIndex);
                if (miniMapDisplayEntry == null || miniMapDisplayEntry.Hide)
                {
                    ModBehaviour.Logger.LogError($"MiniMapDisplayEntry: {miniMapDisplayEntry?.ToString() ?? "null"}, Hide: {miniMapDisplayEntry?.Hide}");
                    return;
                }
                pool.Get().Setup(display, poi, miniMapDisplayEntry);
            }
            else
            {
                ModBehaviour.Logger.LogError($"目标场景不匹配：Target: {targetSceneIndex}, Active: {MultiSceneCore.ActiveSubScene?.buildIndex}");
            }
        }

        public static void ReleasePointOfInterest(CharacterPoiBase poi, bool isOriginalMap)
        {
            ModBehaviour.Logger.Log("正在删除标记点");
            PrefabPool<CharacterPoiEntry>? pool = isOriginalMap ? MapCharacterPoiEntryPool : _miniMapCharacterPoiEntryPool;
            if (pool == null) return;
            CharacterPoiEntry pointOfInterestEntry = pool.ActiveEntries.FirstOrDefault(e => e != null && e.Target == poi);
            if ((bool)pointOfInterestEntry)
            {
                pool.Release(pointOfInterestEntry);
            }
        }

        public static void Register(CharacterPoiBase point)
        {
            points.Add(point);
            PoiRegistered?.Invoke(point);
            CleanUp();
        }

        public static void Unregister(CharacterPoiBase point)
        {
            if (points.Remove(point))
            {
                PoiUnregistered?.Invoke(point);
            }
            CleanUp();
        }

        private static void CleanUp()
        {
            points.RemoveAll((CharacterPoiBase e) => e == null);
        }

        public static void OnEnable()
        {
            MinimapManager.MiniMapApplied += OnMiniMapApplied;
        }

        public static void OnDisable()
        {
            GameObject.Destroy(characterPoiEntryPrefabObj);
            MinimapManager.MiniMapApplied -= OnMiniMapApplied;
        }

        private static void OnMiniMapApplied()
        {
            if (characterPoiEntryPrefabObj == null || characterPoiEntryPrefabObj.IsDestroyed())
            {
                CreatePrefab();
            }
        }

        private static void CreatePrefab()
        {
            if (MinimapManager.OriginalDisplay == null)
            {
                return;
            }
            CharacterPoiEntryData entryData = new CharacterPoiEntryData();

            GameObject prefabObj = new("CharacterPoiEntry");
            RectTransform prefabRect = prefabObj.AddComponent<RectTransform>();
            prefabRect.SetParent(MinimapManager.OriginalDisplay.transform);
            prefabRect.localScale = Vector3.one;
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
                    areaDisplayFill.color = new Color(1f, 1f, 1f, 0.1f);
                    entryData.areaFill = areaDisplayFill;
                }
            }

            GameObject indicatorContainerObj = new("IndicatorContainer");
            RectTransform indicatorContainerRect = indicatorContainerObj.AddComponent<RectTransform>();
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
                    shadow.ColorBleedMode = ColorBleedMode.Black;
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
                    arrowRect.anchoredPosition = Vector2.zero;
                    Image arrow = arrowObj.AddComponent<Image>();
                    entryData.arrow = arrow;
                }
                GameObject displayNameObj = new GameObject("DiaplayName");
                RectTransform displayNameRect = displayNameObj.AddComponent<RectTransform>();
                displayNameRect.SetParent(indicatorContainerRect!);
                displayNameRect.sizeDelta = new Vector2(50f, 50f);
                displayNameRect.pivot = new Vector2(0.5f, 1f);
                displayNameRect.anchorMin = new Vector2(0.5f, 0f);
                displayNameRect.anchorMax = new Vector2(0.5f, 0f);
                displayNameRect.anchoredPosition = Vector2.zero;
                TextMeshProUGUI displayName = displayNameObj.AddComponent<TextMeshProUGUI>();
                displayName.alignment = TextAlignmentOptions.Top;
                displayName.fontSize = 24f;
                entryData.displayName = displayName;
            }

            CharacterPoiEntry characterPoiEntry = prefabObj.AddComponent<CharacterPoiEntry>();
            characterPoiEntry.Initialize(entryData);
            prefabObj.SetActive(false);

            characterPoiEntryPrefabObj = prefabObj;
            characterPoiEntryPrefab = characterPoiEntry;
        }
    }
}
