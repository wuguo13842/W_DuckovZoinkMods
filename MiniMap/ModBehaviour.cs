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
            MiniMapDisplayEntryPatcher.Instance,
        };

		[Obsolete("请使用预定义的补丁列表，这个方法仅用于兼容性")]
		public bool PatchSingleExtender(Type targetType, Type extenderType, string methodName, BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public)
		{
			Log.Warning($"使用动态补丁方法，建议迁移到预定义补丁列表");
			
			try
			{
				// 简化版本，只做基本检查
				var originMethod = HarmonyLib.AccessTools.Method(targetType, methodName);
				if (originMethod == null) return false;
				
				// 让Harmony处理大部分工作
				Harmony.Patch(originMethod, 
					prefix: new HarmonyMethod(extenderType, "Prefix"),
					postfix: new HarmonyMethod(extenderType, "Postfix")
				);
				
				return true;
			}
			catch
			{
				return false;
			}
		}

        // 这个方法可以被简化或删除，因为很少使用
        public bool UnpatchSingleExtender(string assembliyName, string targetTypeName, string methodName, BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            // 使用Type.GetType替代AssemblyOperations.FindTypeInAssemblies
            Type targetType = Type.GetType($"{targetTypeName}, {assembliyName}");
            if (targetType == null)
            {
                Debug.LogWarning($"[{MOD_NAME}] Target Type \"{targetTypeName}\" Not Found!");
                return false;
            }
            
            // 使用Harmony的AccessTools替代反射
            var originMethod = HarmonyLib.AccessTools.Method(targetType, methodName);
            if (originMethod == null)
            {
                Debug.LogWarning($"[{MOD_NAME}] Method \"{methodName}\" not found in {targetTypeName}");
                return false;
            }
            
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
                ApplyHarmonyPatchers();
                DeathEventHandler.Initialize(); // 初始化死亡事件处理器
                LevelManager.OnEvacuated += OnEvacuated;
                
                //SceneLoader.onFinishedLoadingScene += PoiManager.OnFinishedLoadingScene;
                //LevelManager.OnAfterLevelInitialized += PoiManager.OnLenvelIntialized;
                //SceneLoader.onStartedLoadingScene += onStartedLoadingScene;  // 场景加载流程开始时，在显示加载界面之前
                //SceneLoader.onFinishedLoadingScene += onFinishedLoadingScene;  // 场景已经加载完成（资源加载完毕），但还没有被设置为活动场景之前
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
                //SceneLoader.onStartedLoadingScene -= onFinishedLoadingScene;
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
            MinimapManager.Initialize();
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
    }
}