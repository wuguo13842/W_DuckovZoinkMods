using MiniMap.Managers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Compatibility.ShoulderSurfing.Patchers
{
    [TypePatcher("ShoulderSurfing", "ShoulderSurfing.PlayerArrow")]
    public class ShoulderSurfing_PlayerArrowPatcher : CompatibilityPatcherBase
    {
        public static new ShoulderSurfing_PlayerArrowPatcher Instance { get; } = new ShoulderSurfing_PlayerArrowPatcher();
        protected override List<PatcherBase>? SubPatchers { get; } = null;
        private ShoulderSurfing_PlayerArrowPatcher() { }

        [MethodPatcher("Update", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static void UpdatePrefix(object __instance, ref Vector3 ___offset)
        {
            ___offset = Vector3.zero;
        }

        [MethodPatcher("CreateOrGetPlayerArrow", PatchType.Postfix, BindingFlags.Static | BindingFlags.Public)]
        public static void CreateOrGetPlayerArrowPostfix(GameObject __result)
        {
            bool show = ModSettingManager.GetValue("shoulderSurfing.showPlayerArrow", false);
            __result.SetActive(show);
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

        private void OnConfigChanged(string key, object value)
        {
            switch (key)
            {
                case "shoulderSurfing.showPlayerArrow":
                    MinimapManager.MinimapDisplay.InvokeMethod("AutoSetup");
                    break;
            }
        }
    }
}
