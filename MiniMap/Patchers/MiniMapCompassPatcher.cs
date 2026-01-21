using Duckov.MiniMaps;
using MiniMap.Managers;
using MiniMap.Utils;
using System;
using System.Reflection;
using UnityEngine;
using ZoinkModdingLibrary.Attributes;
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
                        ModBehaviour.Logger.Log($"无法获取指南针对象");
                    }
                }

                Transform? trans = arrowField?.GetValue(__instance) as Transform;
                if (trans == null)
                {
                    return false;
                }
                trans.localRotation = ModSettingManager.GetValue<bool>("mapRotation")
                    ? MiniMapCommon.GetChracterRotation()
                    : Quaternion.Euler(0f, 0f, MiniMapCommon.originMapZRotation);
                return false;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"设置指南针旋转时出错：" + e.ToString());
                return true;
            }
        }

    }
}

