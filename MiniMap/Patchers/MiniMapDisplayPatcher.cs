using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Utilities;
using MiniMap.Managers;
using MiniMap.Poi;
using MiniMap.Utils;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Logging;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Patchers
{
    [TypePatcher(typeof(MiniMapDisplay))]
    public class MiniMapDisplayPatcher : PatcherBase
    {
        public static new PatcherBase Instance { get; } = new MiniMapDisplayPatcher();

        private MiniMapDisplayPatcher() { }

        [MethodPatcher("HandlePointOfInterest", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool HandlePointOfInterestPrefix(MiniMapDisplay __instance, MonoBehaviour poi)
        {
            if (poi == null) return false;
            if (poi is CharacterPoiBase characterPoi)
            {
                return characterPoi.WillShow(__instance == MinimapManager.OriginalDisplay);
            }
            return true;
        }

        [MethodPatcher("SetupRotation", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool SetupRotationPrefix(MiniMapDisplay __instance)
        {
            try
            {
                float rotationAngle = ModSettingManager.GetValue<bool>(ModBehaviour.ModInfo, "mapRotation") ? MiniMapCommon.GetMinimapRotation() : MiniMapCommon.originMapZRotation;
                __instance.transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"设置小地图旋转时出错：" + e.ToString());
                return true;
            }
        }
    }
}