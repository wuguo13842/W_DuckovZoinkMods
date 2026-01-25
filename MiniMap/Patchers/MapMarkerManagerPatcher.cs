using Duckov.MiniMaps;
using MiniMap.Managers;
using System;
using System.Collections.Generic;
using System.Text;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Patchers
{
    [TypePatcher(typeof(MapMarkerManager))]
    public class MapMarkerManagerPatcher : PatcherBase
    {
        public static new MapMarkerManagerPatcher Instance { get; } = new MapMarkerManagerPatcher();
        private MapMarkerManagerPatcher() { }

        [MethodPatcher("Load", PatchType.Prefix, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)]
        public static bool LoadPrefix(bool ___loaded)
        {
            return !___loaded;
        }

        [MethodPatcher("Load", PatchType.Postfix, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)]
        public static void LoadPostfix(MapMarkerManager __instance)
        {
            ModBehaviour.Logger.Log("MapMarkerManagers Loaded");
            MinimapManager.MinimapDisplay.InvokeMethod("HandlePointsOfInterests");
        }
    }
}
