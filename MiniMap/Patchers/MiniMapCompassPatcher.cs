using Duckov.MiniMaps;
using MiniMap.Utils;
using System;
using System.Reflection;
using UnityEngine;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Logging;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Patchers
{
    [TypePatcher(typeof(MiniMapCompass))]
    public class MiniMapCompassPatcher : PatcherBase
    {
        public static new PatcherBase Instance { get; } = new MiniMapCompassPatcher();

        private MiniMapCompassPatcher() { }
        static FieldInfo? arrowField;

        [MethodPatcher("SetupRotation", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool SetupRotationPrefix(MiniMapCompass __instance)
        {
            try
            {
                if (arrowField == null)
                {
                    arrowField = typeof(MiniMapCompass).GetField("arrow", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (arrowField == null)
                    {
                        Log.Info($"无法获取指南针对象");
                    }
                }

                Transform? trans = arrowField?.GetValue(__instance) as Transform;
                if (trans == null)
                {
                    return false;
                }
                float rotationAngle = ModSettingManager.GetValue<bool>(ModBehaviour.ModInfo, "mapRotation") ? MiniMapCommon.GetMinimapRotation() : MiniMapCommon.originMapZRotation;
                trans.localRotation = Quaternion.Euler(0f, 0f, rotationAngle);
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"设置指南针旋转时出错：" + e.ToString());
                return true;
            }
        }

    }
}

