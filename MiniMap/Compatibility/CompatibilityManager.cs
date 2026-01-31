using HarmonyLib;
using MiniMap.Compatibility.BetterMapMarker.Patchers;
using MiniMap.Compatibility.ShoulderSurfing.Patchers;
using System.Collections.Generic;
using UnityEngine;
using ZoinkModdingLibrary;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Compatibility
{
    public class CompatibilityManager : MonoBehaviour
    {
        public static MonoBehaviour? Instance { get; private set; }

        public static Harmony Harmony { get; } = new Harmony($"{ModBehaviour.MOD_ID}.compatibility");

        private List<CompatibilityPatcherBase> patchers = new List<CompatibilityPatcherBase>()
        {
             BossLiveMapMod_ModBehaviourPatcher.Instance,
             BetterMapMarker_ModBehaviourPatcher.Instance, // BetterMapMarker 兼容补丁
             ShoulderSurfing_PlayerArrowPatcher.Instance,
        };

        private void Awake()
        {
            if (Instance != null)
            {
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            Initialize();
            foreach (CompatibilityPatcherBase patcher in patchers)
            {
                patcher.Setup(Harmony).Patch();
            }
        }

        private void OnDisable()
        {
            foreach (var patcher in patchers)
            {
                patcher.Unpatch();
            }
        }

        private static void Initialize()
        {

        }
    }
}
