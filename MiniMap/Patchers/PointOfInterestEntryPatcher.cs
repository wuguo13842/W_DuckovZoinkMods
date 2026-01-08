using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Utilities;
using MiniMap.Poi;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Patchers
{
    [TypePatcher(typeof(PointOfInterestEntry))]
    public class PointOfInterestEntryPatcher : PatcherBase
    {
        public static new PatcherBase Instance { get; } = new PointOfInterestEntryPatcher();
        private PointOfInterestEntryPatcher() { }

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
        public static bool UpdatePrefix(PointOfInterestEntry __instance, Image ___icon, MiniMapDisplay ___master)
        {
            if (__instance.Target == null || __instance.Target.IsDestroyed())
            {
                GameObject.Destroy(__instance.gameObject);
                return false;
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
