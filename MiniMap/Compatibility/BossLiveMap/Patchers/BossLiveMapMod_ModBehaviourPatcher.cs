using Duckov.MiniMaps;
using ItemStatsSystem;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Patcher;
using ZoinkModdingLibrary.Utils;

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

        [MethodPatcher("FormatListEntry", PatchType.Prefix, BindingFlags.Static | BindingFlags.NonPublic)]
        public static bool FormatListEntryPrefix(object entry, ref string __result)
        {
            if (entry == null)
            {
                __result = string.Empty;
                return false;
            }

            string name = entry.GetField<string>("DisplayName")?.ToPlainText() ?? string.Empty;
            if (!entry.GetField<bool>("Alive"))
            {
                name = "<s>" + name + "</s>";
            }
            Type? modType = AssemblyOperations.FindTypeInAssemblies("BossLiveMapMod", "BossLiveMapMod.ModBehaviour");
            Color color = modType?.InvokeMethod<Color>("GetBossListTextColor", parameters: new object[] { entry }) ?? Color.white;

            string colorHex = ColorUtility.ToHtmlStringRGBA(color);
            __result = $"<color=#{colorHex}>{name}</color>";
            return false;
        }
    }
}
