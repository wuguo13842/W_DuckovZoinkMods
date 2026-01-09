using Duckov.MiniMaps;
using MiniMap.Managers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Compatibility.BetterMapMarker.Patchers
{
    [TypePatcher("BetterMapMarker", "BetterMapMarker.ModBehaviour")]
    public class ModBehaviourPatcher : PatcherBase
    {
        public static new ModBehaviourPatcher Instance { get; } = new ModBehaviourPatcher();
        private ModBehaviourPatcher() { }

        [MethodPatcher("UpdateMarker", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static void UpdateMarkerPrefix(object marker)
        {
            SimplePointOfInterest? poi = AssemblyOption.GetField<SimplePointOfInterest>(marker, "Poi");
            if (poi != null)
            {
                poi.name = poi.name.Replace("CharacterMarker", "LootboxMarker");
                poi.ScaleFactor = ModSettingManager.GetValue("betterMapMarker.poiScaleFactor", 1f);
            }
        }
    }
}
