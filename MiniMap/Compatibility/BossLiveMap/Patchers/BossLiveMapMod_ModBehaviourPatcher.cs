using Duckov.MiniMaps;
using ItemStatsSystem;
using System.Reflection;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Compatibility.BetterMapMarker.Patchers
{
    [TypePatcher("BossLiveMapMod", "BossLiveMapMod.ModBehaviour")]
    public class BossLiveMapMod_ModBehaviourPatcher : CompatibilityPatcherBase
    {
        public static new BossLiveMapMod_ModBehaviourPatcher Instance { get; } = new BossLiveMapMod_ModBehaviourPatcher();
        protected override List<PatcherBase>? SubPatchers { get; } = new List<PatcherBase>()
        {
            BossLiveMapMod_MiniMapDisplayPatcher.Instance
        };
        private BossLiveMapMod_ModBehaviourPatcher() { }

        [MethodPatcher("GetDisplayName", PatchType.Prefix, BindingFlags.Static | BindingFlags.NonPublic)]
        public static bool GetDisplayNamePrefix(CharacterMainControl character, ref string __result)
        {
            string text = "";
            if (character != null)
            {
                CharacterRandomPreset characterPreset = character.characterPreset;
                text = characterPreset.GetField<string>("nameKey") ?? "";
            }
            __result = text;
            return false;
        }

    }
}
