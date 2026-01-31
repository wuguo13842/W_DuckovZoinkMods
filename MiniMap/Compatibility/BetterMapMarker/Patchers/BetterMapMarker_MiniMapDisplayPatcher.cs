using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Modding;
using MiniMap.Managers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Extentions;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Patcher;
using ZoinkModdingLibrary.Utils;

namespace MiniMap.Compatibility.BetterMapMarker.Patchers
{
    [TypePatcher(typeof(MiniMapDisplay))]
    public class BetterMapMarker_MiniMapDisplayPatcher : PatcherBase
    {
        public static new BetterMapMarker_MiniMapDisplayPatcher Instance { get; } = new BetterMapMarker_MiniMapDisplayPatcher();
        private BetterMapMarker_MiniMapDisplayPatcher() { }

        [MethodPatcher("HandlePointOfInterest", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool HandlePointOfInterestPrefix(MiniMapDisplay __instance, MonoBehaviour poi)
        {
            if (poi is SimplePointOfInterest pointOfInterest && poi.name.StartsWith("LootboxMarker:"))
            {
                pointOfInterest.ScaleFactor = ModSettingManager.GetValue(ModBehaviour.ModInfo, "betterMapMarker.poiScaleFactor", 1.0f);
                if (__instance == MinimapManager.MinimapDisplay)
                    return ModSettingManager.GetValue(ModBehaviour.ModInfo, "betterMapMarker.showInMiniMap", false);
            }
            return true;
        }

        public override bool Patch()
        {
            bool result = base.Patch();
            if (result)
            {
                ModSettingManager.ConfigChanged += OnConfigChanged;
            }

            return result;
        }

        public override void Unpatch()
        {
            base.Unpatch();
            ModSettingManager.ConfigChanged -= OnConfigChanged;
        }

        private void OnConfigChanged(ModInfo modInfo, string key, object? value)
        {
            if (modInfo.ModIdEquals(ModBehaviour.Instance!.info)) return;
            switch (key)
            {
                case "betterMapMarker.poiScaleFactor":
                case "betterMapMarker.showInMiniMap":
                    MinimapManager.MinimapDisplay.InvokeMethod("HandlePointsOfInterests");
                    break;
            }
        }
    }
}
