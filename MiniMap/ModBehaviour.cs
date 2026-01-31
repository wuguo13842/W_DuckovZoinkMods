using Duckov.Modding;
using HarmonyLib;
using MiniMap.Compatibility;
using MiniMap.Compatibility.BetterMapMarker.Patchers;
using MiniMap.Managers;
using MiniMap.Patchers;
using MiniMap.Utils;
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using ZoinkModdingLibrary;
using ZoinkModdingLibrary.Logging;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Patcher;
using ZoinkModdingLibrary.Utils;

namespace MiniMap
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static readonly string MOD_ID = "com.zoink.minimap";

        public static readonly string MOD_NAME = "MiniMap";
        public static Harmony Harmony { get; } = new Harmony(MOD_ID);

        public static ModBehaviour? Instance { get; private set; }

        public static ModInfo ModInfo => Instance?.info ?? default;

        private List<PatcherBase> patchers = new List<PatcherBase>() {
            CharacterSpawnerRootPatcher.Instance,
            PointOfInterestEntryPatcher.Instance,
            MiniMapCompassPatcher.Instance,
            MiniMapDisplayPatcher.Instance,
            MapMarkerManagerPatcher.Instance,
        };

        public bool PatchSingleExtender(Type targetType, Type extenderType, string methodName, BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            MethodInfo originMethod = targetType.GetMethod(methodName, bindFlags);
            if (originMethod == null)
            {
                Debug.LogWarning($"[{MOD_NAME}] Original method not found: {targetType.Name}.{methodName}");
                return false;
            }

            try
            {
                MethodInfo prefix = extenderType.GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);
                MethodInfo postfix = extenderType.GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
                MethodInfo transpiler = extenderType.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.Public);
                MethodInfo finalizer = extenderType.GetMethod("Finalizer", BindingFlags.Static | BindingFlags.Public);
                Harmony.Unpatch(originMethod, HarmonyPatchType.All, Harmony.Id);
                Harmony.Patch(
                    originMethod,
                    prefix != null ? new HarmonyMethod(prefix) : null,
                    postfix != null ? new HarmonyMethod(postfix) : null,
                    transpiler != null ? new HarmonyMethod(transpiler) : null,
                    finalizer != null ? new HarmonyMethod(finalizer) : null
                );
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{MOD_NAME}] Failed to patch {originMethod}: {ex.Message}");
                return false;
            }
        }

        public bool UnpatchSingleExtender(string assembliyName, string targetTypeName, string methodName, BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            Type? targetType = AssemblyOperations.FindTypeInAssemblies(assembliyName, targetTypeName);
            if (targetType == null)
            {
                Debug.LogWarning($"[{MOD_NAME}] Target Type \"{targetTypeName}\" Not Found!");
                return false;
            }
            MethodInfo originMethod = targetType.GetMethod(methodName, bindFlags);
            Harmony.Unpatch(originMethod, HarmonyPatchType.All, MOD_ID);
            return true;
        }

        void ApplyHarmonyPatchers()
        {
            try
            {
                Log.Info($"Patching Patchers");
                foreach (var patcher in patchers)
                {
                    patcher.Setup(Harmony).Patch();
                }
            }
            catch (Exception e)
            {
                Log.Error($"应用扩展器失败: {e}");
            }
        }
        void CancelHarmonyPatchers()
        {
            try
            {
                foreach (var patcher in patchers)
                {
                    patcher.Unpatch();
                }
            }
            catch (Exception e)
            {
                Log.Error($"取消扩展器失败: {e}");
            }
        }
        void Awake()
        {
            if (Instance != null)
            {
                Log.Error($"ModBehaviour 已实例化");
                return;
            }
            Instance = this;
            gameObject.GetOrAddComponent<CompatibilityManager>();
        }

        void OnEnable()
        {
            try
            {
                MinimapManager.Initialize();
                ApplyHarmonyPatchers();
				DeathEventHandler.Initialize(); // 初始化死亡事件处理器
                LevelManager.OnEvacuated += OnEvacuated;
                
                //SceneLoader.onFinishedLoadingScene += PoiManager.OnFinishedLoadingScene;
                //LevelManager.OnAfterLevelInitialized += PoiManager.OnLenvelIntialized;
				//SceneLoader.onStartedLoadingScene += onStartedLoadingScene;  // 场景加载流程开始时，在显示加载界面之前
				SceneLoader.onFinishedLoadingScene += onFinishedLoadingScene;  // 场景已经加载完成（资源加载完毕），但还没有被设置为活动场景之前
				//SceneLoader.onBeforeSetSceneActive += OnBeforeSetSceneActive;  // 新场景已经被设置为活动场景，初始化完成后
				//SceneLoader.onAfterSceneInitialize += OnAfterSceneInitialize;  // 整个场景加载流程完全结束，包括所有过渡动画完成后

            }
            catch (Exception e)
            {
                Log.Error($"启用mod失败: {e}");
            }
        }

        void OnEvacuated(EvacuationInfo _info)
        {
            MinimapManager.Hide();
        }

        void OnDisable()
        {
            try
            {
                CancelHarmonyPatchers();
                DeathEventHandler.Cleanup(); // 清理死亡事件处理器
                LevelManager.OnEvacuated -= OnEvacuated;
				
                //SceneLoader.onFinishedLoadingScene -= PoiManager.OnFinishedLoadingScene;
                //LevelManager.OnAfterLevelInitialized -= PoiManager.OnLenvelIntialized;
				SceneLoader.onStartedLoadingScene -= onFinishedLoadingScene;
                MinimapManager.Destroy();
                Log.Info($"disable mod {MOD_NAME}");
            }
            catch (Exception e)
            {
                Log.Error($"禁用mod失败: {e}");
            }
        }

        protected override void OnAfterSetup()
        {
            Log.Info("Mod已设定");
            ModSettingManager.Initialize(OnInitialized);
        }

        private void OnInitialized()
        {
            Log.Info("$SettingManager初始化成功，开始创建UI");
            ModSettingManager.CreateUI(ModInfo);
        }

        void Update()
        {
            try
            {
                // if (ModSettingManager.needUpdate)  ModSettingManager.Update();
                MinimapManager.Update();
                // CustomMinimapManager.CheckToggleKey();
                //PoiManager.Update();
            }
            catch (Exception e)
            {
                Log.Error($"更新失败: {e}");
            }
        }
		
		private static void onFinishedLoadingScene(SceneLoadingContext context)
		{
            if (string.IsNullOrEmpty(context.sceneName)) return;
                
			// 预加载场景中心点（触发缓存填充）
			Duckov.MiniMaps.UI.CharacterPoiEntry.GetSceneCenterFromSettings(context.sceneName);
		}
    }
}
