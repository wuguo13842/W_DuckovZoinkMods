using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using MiniMap.Managers;
using MiniMap.Poi;
using System;
using System.Reflection;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Patcher;
using ZoinkModdingLibrary.Utils;

namespace MiniMap.Patchers
{
    [TypePatcher(typeof(PointOfInterestEntry))]
    public class PointOfInterestEntryPatcher : PatcherBase
    {
        public static new PatcherBase Instance { get; } = new PointOfInterestEntryPatcher();
        private PointOfInterestEntryPatcher() { }

[MethodPatcher("UpdateScale", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
public static bool UpdateScalePrefix(
    PointOfInterestEntry __instance,
    MiniMapDisplay ___master,
    IPointOfInterest ___pointOfInterest,
    Transform ___iconContainer,
    ProceduralImage ___areaDisplay,
    float ___areaLineThickness,
    TextMeshProUGUI ___displayName
)
{
    try
    {
        if (___pointOfInterest == null) return true;
		
        bool isInMiniMap = ___master == MinimapManager.MinimapDisplay; // 判断当前显示的是系统地图还是Mod地图
        bool isCharacterPoi = ___pointOfInterest is CharacterPoiBase;
        
        // 处理图标缩放
        float d = ___pointOfInterest.ScaleFactor;
        if (isCharacterPoi)
        {
            CharacterPoiBase characterPoi = ___pointOfInterest as CharacterPoiBase;
			if (characterPoi == null && !isInMiniMap) return true;
            d = characterPoi?.IconSize ?? d;
        }
		
        float displayZoomScale = ModSettingManager.GetValue(ModBehaviour.ModInfo, "displayZoomScale", 5f);
		float CascadeScalingUnits = ModSettingManager.GetValue(ModBehaviour.ModInfo, "CascadeScalingUnits", 2.5f);

        // 获取父对象缩放
        float parentLocalScale = __instance.transform.parent.localScale.x;
        var baseScale = Vector3.one * (d / parentLocalScale);
        
        // ============ 统一处理逻辑 ============
        if (isInMiniMap) // 小地图
        {
            if (!isCharacterPoi) // 场景片区名字
            {
                // 显示名称处理
                bool shouldShowName = displayZoomScale >= 0.4f;
                if (___displayName != null)
                {
                    ___displayName.gameObject.SetActive(shouldShowName);
                    if (shouldShowName) // 场景片区名字
                    {
                        ___displayName.transform.localScale = Vector3.one * (CascadeScalingUnits * 0.6f);
                    }
                }
                
                // 图标容器处理
                if (___iconContainer != null)
                {
                    ___iconContainer.gameObject.SetActive(true);
                    if (shouldShowName)  // 场景片区名
                    {
                        ___iconContainer.localScale = baseScale / CascadeScalingUnits;
                    }
                    else
                    {
                        ___iconContainer.localScale = baseScale / (CascadeScalingUnits * 0.8f);
                    }
                }
            }
            else // 角色POI
            {
                CharacterPoiBase characterPoi = ___pointOfInterest as CharacterPoiBase;
                if (characterPoi != null && ___iconContainer != null && ___displayName != null)
                {
                    CharacterType type = characterPoi.CharacterType;
                    bool isCenterIcon = characterPoi.CharacterType == CharacterType.Main;
                    
                    // 图标容器显示/隐藏
                    if (type == CharacterType.Enemy || type == CharacterType.NPC || type == CharacterType.Neutral)
                    {
                        ___iconContainer.gameObject.SetActive(displayZoomScale >= 0.8f);
                    }
                    else
                    {
                        ___iconContainer.gameObject.SetActive(true);
                    }
                    
                    // 显示名称处理
                    if (isCenterIcon)
                    {
                        ___displayName.gameObject.SetActive(false);
                    }
                    else
                    {
                        bool shouldShowName = displayZoomScale >= 1.5f;
                        ___displayName.gameObject.SetActive(shouldShowName);
                        if (shouldShowName)
                        {
                            ___displayName.transform.localScale = Vector3.one * (CascadeScalingUnits * 0.8f);
                        }
                    }
                    
                    // 图标缩放
                    ___iconContainer.localScale = baseScale;
                }
            }
        }
        else // 大地图
        {
            if (!isCharacterPoi) // 场景片区名字
            {
                if (___displayName != null)
                {
                    ___displayName.gameObject.SetActive(true);
                    ___displayName.transform.localScale = Vector3.one;
                }
                if (___iconContainer != null)
                {
                    ___iconContainer.gameObject.SetActive(true);
                    ___iconContainer.localScale = baseScale;
                }
            }
            else // 角色POI
            {
                CharacterPoiBase characterPoi = ___pointOfInterest as CharacterPoiBase;
                if (characterPoi != null && ___iconContainer != null && ___displayName != null)
                {
                    bool isCenterIcon = characterPoi.CharacterType == CharacterType.Main;
                    
                    // 显示名称处理
                    if (isCenterIcon)
                    {
                        ___displayName.gameObject.SetActive(true);
                        ___displayName.transform.localScale = Vector3.one;
                    }
                    else
                    {
                        ___displayName.gameObject.SetActive(true);
                        ___displayName.transform.localScale = Vector3.one * (CascadeScalingUnits * 0.8f);
                    }
                    
                    // 图标容器始终显示
                    ___iconContainer.gameObject.SetActive(true);
                    
                    // 图标缩放（中心图标放大2.5倍）
                    if (isCenterIcon)
                    {
                        ___iconContainer.localScale = baseScale * CascadeScalingUnits;
                    }
                    else
                    {
                        ___iconContainer.localScale = baseScale;
                    }
                }
            }
        }
        // ============ 逻辑结束 ============
        
        // 处理区域显示
        if (___pointOfInterest.IsArea)
        {
            ___areaDisplay.BorderWidth = ___areaLineThickness / parentLocalScale;
            ___areaDisplay.FalloffDistance = 1f / parentLocalScale;
        }

                return false;
            }
            catch (Exception e)
            {
                Log.Error($"UpdateScalePrefix failed: {e.Message}");
                return true;
            }
        }

        [MethodPatcher("UpdateRotation", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool UpdateRotationPrefix(CharacterPoiEntry __instance, MiniMapDisplayEntry ___minimapEntry)
        {
            try
            {
                if (__instance.Target is DirectionPointOfInterest poi)
                {
                    MiniMapDisplay? display = ___minimapEntry.GetComponentInParent<MiniMapDisplay>();
                    if (display == null)
                    {
                        return true;
                    }
                    __instance.transform.rotation = Quaternion.Euler(0f, 0f, poi.RealEulerAngle + display.transform.rotation.eulerAngles.z);
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"PointOfInterestEntry UpdateRotation failed: {e.Message}");
                return true;
            }
        }

        [MethodPatcher("Update", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool UpdatePrefix(CharacterPoiEntry __instance, Image ___icon, MiniMapDisplay ___master, TextMeshProUGUI ___displayName)
        {
            if (__instance.Target == null || __instance.Target.IsDestroyed())
            {
                __instance.gameObject.SetActive(false);
                return false;
            }
            //if (___master == MinimapManager.MinimapDisplay && !(__instance.Target?.gameObject.activeInHierarchy ?? false))
            //{
            //    return false;
            //}
            //lastUpdateTime = Time.time;
            if (__instance.Target is IPointOfInterest poi)
            {
                if (poi.Color != ___icon.color)
                {
                    ___icon.color = poi.Color;
                }
                ___displayName.text = ___master == MinimapManager.MinimapDisplay && ModSettingManager.GetValue(ModBehaviour.ModInfo, "hideDisplayName", false) ? "" : poi.DisplayName;
            }
            RectTransform icon = ___icon.rectTransform;
            RectTransform? layout = icon.parent as RectTransform;
            if (layout == null) { return true; }
            if (layout.localPosition + icon.localPosition != Vector3.zero)
            {
                layout.localPosition = Vector3.zero - icon.localPosition;
            }
            return true;
        }

        [MethodPatcher("Setup", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static void SetupPrefix(CharacterPoiEntry __instance, MonoBehaviour target, Image ___icon)
        {
            if (target is IPointOfInterest poi)
            {
                VerticalLayoutGroup? layout = __instance.GetComponentInChildren<VerticalLayoutGroup>();
                if (layout == null) { return; }
                layout.transform.localPosition = poi.HideIcon || string.IsNullOrEmpty(poi.DisplayName) ? Vector3.zero : Vector3.zero - ___icon.transform.localPosition;
            }
        }
    }
}
