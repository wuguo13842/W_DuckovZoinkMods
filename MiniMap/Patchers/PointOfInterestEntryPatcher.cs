using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Utilities;
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
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Patchers
{
    [TypePatcher(typeof(PointOfInterestEntry))]
    public class PointOfInterestEntryPatcher : PatcherBase
    {
        public static new PatcherBase Instance { get; } = new PointOfInterestEntryPatcher();
        private PointOfInterestEntryPatcher() { }

[MethodPatcher("UpdateScale", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
public static bool UpdateScalePrefix(
    CharacterPoiEntry __instance,
    MiniMapDisplay ___master,
    IPointOfInterest ___pointOfInterest,
    Transform ___iconContainer,
    ProceduralImage ___areaDisplay,
    float ___areaLineThickness
)
{
    try
    {
        float d = ___pointOfInterest?.ScaleFactor ?? 1f;
        
        // 如果是CharacterPoiBase（包括位置图标和方向箭头）
        if (___pointOfInterest is CharacterPoiBase characterPoi)
        {
            // 判断当前显示的是系统地图还是Mod地图
            bool isInMiniMap = ___master == CustomMinimapManager.DuplicatedMinimapDisplay;
            
            // 判断是否是中心图标（玩家自己的图标）
            bool isCenterIcon = characterPoi.CharacterType == CharacterType.Main;
            
            if (isCenterIcon)
            {
                // 中心图标：区分小地图和系统地图
                if (isInMiniMap)
                {
                    // 在小地图中：直接读取 miniMapCenterIconSize 配置
                    d = characterPoi.ScaleFactor * ModSettingManager.GetValue("miniMapCenterIconSize", 1.0f);
                }
                else
                {
                    // 在系统地图中：使用原始大小
                    d = characterPoi.ScaleFactor;
                }
            }
            else
            {
                // 根据角色类型获取对应的图标大小配置
                float iconSizeFactor = 1.0f;
                switch (characterPoi.CharacterType)
                {
                    case CharacterType.Pet:
                        iconSizeFactor = ModSettingManager.GetValue("petIconSize", 0.8f);
                        break;
                    case CharacterType.Boss:
                        iconSizeFactor = ModSettingManager.GetValue("bossIconSize", 1.2f);
                        break;
                    case CharacterType.Enemy:
                    case CharacterType.NPC:
                    case CharacterType.Neutral:
                        iconSizeFactor = ModSettingManager.GetValue("enemyIconSize", 1.0f);
                        break;
                }
                
                d = characterPoi.ScaleFactor * iconSizeFactor;
            }
        }
        
        float parentLocalScale = __instance.GetProperty<float>("ParentLocalScale");
        int iconScaleType = ModSettingManager.GetValue("iconScaleType", 0);
        var baseScale = Vector3.one * d / parentLocalScale;
        
        // 应用缩放
        ___iconContainer.localScale = baseScale;
        
        if (___pointOfInterest != null && ___pointOfInterest.IsArea)
        {
            ___areaDisplay.BorderWidth = ___areaLineThickness / parentLocalScale;
            ___areaDisplay.FalloffDistance = 1f / parentLocalScale;
        }

        return false;
    }
    catch (Exception e)
    {
        ModBehaviour.Logger.LogError($"UpdateScalePrefix failed: {e.Message}");
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
                ModBehaviour.Logger.LogError($"PointOfInterestEntry UpdateRotation failed: {e.Message}");
                return true;
            }
        }

        [MethodPatcher("Update", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool UpdatePrefix(CharacterPoiEntry __instance, Image ___icon, MiniMapDisplay ___master, TextMeshProUGUI ___displayName)
        {
            if (__instance.Target == null || __instance.Target.IsDestroyed())
            {
                GameObject.Destroy(__instance.gameObject);
                return false;
            }
            //if (___master == CustomMinimapManager.DuplicatedMinimapDisplay && !(__instance.Target?.gameObject.activeInHierarchy ?? false))
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
                ___displayName.text = ___master == CustomMinimapManager.DuplicatedMinimapDisplay && ModSettingManager.GetValue("hideDisplayName", false) ? "" : poi.DisplayName;
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
