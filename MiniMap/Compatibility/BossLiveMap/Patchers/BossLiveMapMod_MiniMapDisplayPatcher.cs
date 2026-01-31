using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using MiniMap.Managers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Compatibility.BetterMapMarker.Patchers
{
    [TypePatcher(typeof(MiniMapDisplay))]
    public class BossLiveMapMod_MiniMapDisplayPatcher : PatcherBase
    {
        public static new BossLiveMapMod_MiniMapDisplayPatcher Instance { get; } = new BossLiveMapMod_MiniMapDisplayPatcher();
        private BossLiveMapMod_MiniMapDisplayPatcher() { }

        [MethodPatcher("HandlePointOfInterest", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool HandlePointOfInterestPrefix(MiniMapDisplay __instance, MonoBehaviour poi)
        {
            if (poi is SimplePointOfInterest pointOfInterest && poi.name.StartsWith("CharacterMarker:"))
            {
                if (__instance == MinimapManager.MinimapDisplay)
                    return false;
                pointOfInterest.ScaleFactor = ModSettingManager.GetValue(ModBehaviour.ModInfo, "bossLiveMap.poiScaleFactor", 1.0f);
            }
            return true;
        }
    }
}
