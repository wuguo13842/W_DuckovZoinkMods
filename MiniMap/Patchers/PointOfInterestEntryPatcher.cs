using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Utilities;
using MiniMap.Managers;
using MiniMap.Poi;
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
            PointOfInterestEntry __instance,
            MiniMapDisplay ___master,
            IPointOfInterest ___pointOfInterest,
            Transform ___iconContainer,
            ProceduralImage ___areaDisplay,
            float ___areaLineThickness
        )
        {
            float d = ___pointOfInterest?.ScaleFactor ?? 1f;
            float parentLocalScale = __instance.GetProperty<float>("ParentLocalScale");
            int iconScaleType = ModSettingManager.GetValue("iconScaleType", 0);
            var baseScale = Vector3.one * d / parentLocalScale;
            ___iconContainer.localScale = ___master != CustomMinimapManager.DuplicatedMinimapDisplay ?
                baseScale :
                iconScaleType switch
                {
                    1 => baseScale * ModSettingManager.GetValue("miniMapWindowScale", 1f) / 1.5f,
                    2 => baseScale * ModSettingManager.GetValue("displayZoomScale", 5f) / 5,
                    _ or 0 => baseScale,
                };
            if (___pointOfInterest != null && ___pointOfInterest.IsArea)
            {
                ___areaDisplay.BorderWidth = ___areaLineThickness / parentLocalScale;
                ___areaDisplay.FalloffDistance = 1f / parentLocalScale;
            }

            return false;
        }

        [MethodPatcher("UpdateRotation", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool UpdateRotationPrefix(PointOfInterestEntry __instance, MiniMapDisplayEntry ___minimapEntry)
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
        public static bool UpdatePrefix(
            PointOfInterestEntry __instance, 
            Image ___icon, 
            MiniMapDisplay ___master, 
            TextMeshProUGUI ___displayName,
            Transform ___iconContainer,
            MonoBehaviour ___target)
        {
            if (___target == null || ___target.IsDestroyed())
            {
                GameObject.Destroy(__instance.gameObject);
                return false;
            }

            // 尝试使用缓存数据
            if (PoiCacheManager.Instance != null && 
                PoiCacheManager.Instance.TryGetInstanceData(___target, 
                    out Vector3 mapPosition, 
                    out Quaternion rotation,
                    out Color color,
                    out string displayName,
                    out bool hideIcon,
                    out float scaleFactor))
            {
                // 设置位置
                __instance.transform.position = mapPosition;
                
                // 设置缩放
                float parentLocalScale = __instance.GetProperty<float>("ParentLocalScale");
                int iconScaleType = ModSettingManager.GetValue("iconScaleType", 0);
                var baseScale = Vector3.one * scaleFactor / parentLocalScale;
                
                ___iconContainer.localScale = ___master != CustomMinimapManager.DuplicatedMinimapDisplay ?
                    baseScale :
                    iconScaleType switch
                    {
                        1 => baseScale * ModSettingManager.GetValue("miniMapWindowScale", 1f) / 1.5f,
                        2 => baseScale * ModSettingManager.GetValue("displayZoomScale", 5f) / 5,
                        _ or 0 => baseScale,
                    };
                
                // 设置颜色和文本
                ___icon.color = color;
                ___displayName.text = ___master == CustomMinimapManager.DuplicatedMinimapDisplay && 
                    ModSettingManager.GetValue("hideDisplayName", false) ? "" : displayName;
                
                // 设置旋转
                __instance.transform.rotation = rotation;
                
                // 设置图标显示
                ___icon.gameObject.SetActive(!hideIcon);
                
                // 标记为已使用缓存（通过反射设置私有字段）
                FieldInfo usedCacheField = typeof(PointOfInterestEntry).GetField("_usedCacheThisFrame", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (usedCacheField != null)
                {
                    usedCacheField.SetValue(__instance, true);
                }
                
                return false; // 跳过原版Update
            }
            
            // 缓存不可用，使用原版逻辑
            FieldInfo usedCacheField2 = typeof(PointOfInterestEntry).GetField("_usedCacheThisFrame", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (usedCacheField2 != null)
            {
                usedCacheField2.SetValue(__instance, false);
            }
            
            if (__instance.Target is IPointOfInterest poi2)
            {
                if (poi2.Color != ___icon.color)
                {
                    ___icon.color = poi2.Color;
                }
                ___displayName.text = ___master == CustomMinimapManager.DuplicatedMinimapDisplay && 
                    ModSettingManager.GetValue("hideDisplayName", false) ? "" : poi2.DisplayName;
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
        public static void SetupPrefix(PointOfInterestEntry __instance, MonoBehaviour target, Image ___icon)
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