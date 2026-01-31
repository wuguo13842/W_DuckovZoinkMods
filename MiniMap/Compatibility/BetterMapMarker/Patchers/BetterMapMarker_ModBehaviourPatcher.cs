using Duckov.MiniMaps;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Patcher;
using ZoinkModdingLibrary.Utils;

namespace MiniMap.Compatibility.BetterMapMarker.Patchers
{
    [TypePatcher("BetterMapMarker", "BetterMapMarker.ModBehaviour")]
    public class BetterMapMarker_ModBehaviourPatcher : CompatibilityPatcherBase
    {
        public static new BetterMapMarker_ModBehaviourPatcher Instance { get; } = new BetterMapMarker_ModBehaviourPatcher();
        protected override List<PatcherBase>? SubPatchers { get; } = new List<PatcherBase>()
        {
            BetterMapMarker_MiniMapDisplayPatcher.Instance
        };
        private BetterMapMarker_ModBehaviourPatcher() { }

        [MethodPatcher("UpdateMarker", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static void UpdateMarkerPrefix(object marker)
        {
            SimplePointOfInterest? poi = marker.GetField<SimplePointOfInterest>("Poi");
            if (poi != null && !poi.name.StartsWith("LootboxMarker:"))
            {
                poi.name = "LootboxMarker:" + poi.name;
            }
        }

        [MethodPatcher("Update", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool UpdatePrefix()
        {
            return false;
        }

        [MethodPatcher("GetDisplayName", PatchType.Prefix, BindingFlags.Static | BindingFlags.NonPublic)]
        public static bool GetDisplayNamePrefix(InteractableLootbox lootbox, ref string __result)
        {
            bool flag = lootbox.name.Contains("Formula", StringComparison.OrdinalIgnoreCase);
            string? name = null;
            if (flag && lootbox.Inventory.Content.Count > 0)
            {
                Item item = lootbox.Inventory[0];
                name = item.GetField<string>("displayName");
            }
            __result = (name ?? lootbox.GetField<string>("displayNameKey")) ?? "";

            return false;
        }
    }
}
