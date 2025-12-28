using HarmonyLib;
using System.Reflection;
using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using UnityEngine;
using Duckov.Modding;
using MiniMap.Extenders;
using MiniMap.Managers;

namespace MiniMap
{

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        const string MOD_ID = "com.zoink.minimap";

        public static string MOD_NAME = "MiniMap";

        Harmony harmony = new Harmony(MOD_ID);
        public static ModBehaviour? Instance;


        void PatchSingleExtender(Type extenderType, Type ExtenderType, string methodName, BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            MethodInfo originMethod = extenderType.GetMethod(methodName, bindFlags);
            MethodInfo prefix = ExtenderType.GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);
            MethodInfo postfix = ExtenderType.GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
            MethodInfo transpiler = ExtenderType.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.Public);
            MethodInfo finalizer = ExtenderType.GetMethod("Finalizer", BindingFlags.Static | BindingFlags.Public);
            harmony.Patch(
                originMethod,
                prefix == null ? null : new HarmonyMethod(prefix),
                postfix == null ? null : new HarmonyMethod(postfix),
                transpiler == null ? null : new HarmonyMethod(transpiler),
                finalizer == null ? null : new HarmonyMethod(finalizer)
            );
        }

        void UnpatchSingleExtender(Type extenderType, string methodName, BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            MethodInfo originMethod = extenderType.GetMethod(methodName, bindFlags);
            harmony.Unpatch(originMethod, HarmonyPatchType.All, MOD_ID);
        }

        void ApplyHarmonyExtenders()
        {
            try
            {
                #region MiniMap
                PatchSingleExtender(typeof(MiniMapCompass), typeof(MiniMapCompassSetupRotationExtender), "SetupRotation", BindingFlags.Instance | BindingFlags.NonPublic);
                PatchSingleExtender(typeof(MiniMapDisplay), typeof(MiniMapDisplaySetupRotationExtender), "SetupRotation", BindingFlags.Instance | BindingFlags.NonPublic);
                #endregion
                #region Character POI
                PatchSingleExtender(typeof(PointOfInterestEntry), typeof(PointOfInterestEntryUpdateExtender), "Update", BindingFlags.Instance | BindingFlags.NonPublic);
                PatchSingleExtender(typeof(PointOfInterestEntry), typeof(PointOfInterestEntryUpdateRotationExtender), "UpdateRotation", BindingFlags.Instance | BindingFlags.NonPublic);
                PatchSingleExtender(typeof(CharacterSpawnerRoot), typeof(CharacterSpawnerRootAddCharacterExtender), "AddCreatedCharacter");
                PatchSingleExtender(typeof(CharacterMainControl), typeof(CharacterMainControlUpdateExtender), "Update", BindingFlags.Instance | BindingFlags.NonPublic);
                #endregion
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniMap] 应用扩展器失败: {e}");
            }
        }
        void CancelHarmonyExtender()
        {
            try
            {
                #region MiniMap
                UnpatchSingleExtender(typeof(MiniMapCompass), "SetupRotation", BindingFlags.Instance | BindingFlags.NonPublic);
                UnpatchSingleExtender(typeof(MiniMapDisplay), "SetupRotation", BindingFlags.Instance | BindingFlags.NonPublic);
                #endregion
                #region Character POI
                UnpatchSingleExtender(typeof(PointOfInterestEntry), "Update", BindingFlags.Instance | BindingFlags.NonPublic);
                UnpatchSingleExtender(typeof(PointOfInterestEntry), "UpdateRotation", BindingFlags.Instance | BindingFlags.NonPublic);
                UnpatchSingleExtender(typeof(CharacterSpawnerRoot), "AddCreatedCharacter");
                UnpatchSingleExtender(typeof(CharacterMainControl), "Update", BindingFlags.Instance | BindingFlags.NonPublic);
                #endregion
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniMap] 取消扩展器失败: {e}");
            }
        }
        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[MiniMap] ModBehaviour 已实例化");
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            try
            {
                CustomMinimapManager.Initialize();
                ApplyHarmonyExtenders();
                ModManager.OnModActivated += ModManager_OnModActivated;
                LevelManager.OnEvacuated += OnEvacuated;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniMap] 启用mod失败: {e}");
            }
        }

        void OnEvacuated(EvacuationInfo _info)
        {
            CustomMinimapManager.Hide();
        }

        void OnDisable()
        {
            try
            {
                CancelHarmonyExtender();
                ModManager.OnModActivated -= ModManager_OnModActivated;
                CustomMinimapManager.Destroy();
                Debug.Log($"[MiniMap] disable mod {MOD_NAME}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniMap] 禁用mod失败: {e}");
            }
        }

        //下面两个函数需要实现，实现后的效果是：ModSetting和mod之间不需要启动顺序，两者无论谁先启动都能正常添加设置
        private void ModManager_OnModActivated(ModInfo arg1, Duckov.Modding.ModBehaviour arg2)
        {
            //(触发时机:此mod在ModSetting之前启用)检查启用的mod是否是ModSetting,是进行初始化
            if (arg1.name != ModSettingAPI.MOD_NAME || !ModSettingAPI.Init(info)) return;
            ModSettingManager.needUpdate = true;
        }

        protected override void OnAfterSetup()
        {
            //(触发时机:此mod在ModSetting之后启用)此mod，Setup后,尝试进行初始化
            if (ModSettingAPI.Init(info))
            {
                ModSettingManager.needUpdate = true;
            }
        }

        void Update()
        {
            try
            {
                if (ModSettingManager.needUpdate)
                    ModSettingManager.Update();
                CustomMinimapManager.Update();
                CustomMinimapManager.CheckToggleKey();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniMap] 更新失败: {e}");
            }
        }
    }
}
