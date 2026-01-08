using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Utilities;
using MiniMap.Managers;
using MiniMap.Poi;
using MiniMap.Utils;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using ZoinkModdingLibrary.Attributes;
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
            if (poi is CharacterPointOfInterestBase characterPoi)
            {
                return characterPoi.WillShow(__instance == CustomMinimapManager.OriginalMinimapDisplay);
            }
            return true;
        }

        //[MethodPatcher("HandlePointsOfInterests", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        //public static bool HandlePointsOfInterestsPrefix(MiniMapDisplay __instance, PrefabPool<PointOfInterestEntry> ___PointOfInterestEntryPool)
        //{
        //    try
        //    {
        //        if (___PointOfInterestEntryPool == null) { return false; }
        //        foreach (PointOfInterestEntry entry in ___PointOfInterestEntryPool.ActiveEntries.ToArray())
        //        {
        //            if (entry.Target == null || !PointsOfInterests.Points.Contains(entry.Target))
        //                ___PointOfInterestEntryPool.Release(entry);
        //        }
        //        foreach (MonoBehaviour monoBehaviour in PointsOfInterests.Points)
        //        {
        //            if (monoBehaviour != null && !___PointOfInterestEntryPool.ActiveEntries.Any(s => s.Target == monoBehaviour))
        //            {
        //                AssemblyOption.InvokeMethod(__instance, "HandlePointOfInterest", new object[] { monoBehaviour });
        //            }
        //        }
        //        return false;
        //    }
        //    catch (Exception e)
        //    {
        //        ModBehaviour.Logger.LogError($"处理小地图兴趣点时出错：" + e.ToString());
        //        return true;
        //    }
        //}

        [MethodPatcher("SetupRotation", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool SetupRotationPrefix(MiniMapDisplay __instance)
        {
            try
            {
                __instance.transform.rotation = ModSettingManager.GetValue<bool>("mapRotation")
                    ? MiniMapCommon.GetPlayerMinimapRotationInverse()
                    : Quaternion.Euler(0f, 0f, MiniMapCommon.originMapZRotation);
                return false;
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"设置小地图旋转时出错：" + e.ToString());
                return true;
            }
        }
    }
}