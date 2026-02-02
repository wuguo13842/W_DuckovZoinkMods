using UnityEngine;
using ZoinkModdingLibrary.ModSettings;
using Newtonsoft.Json.Linq;
using ZoinkModdingLibrary.Utils; 

namespace MiniMap.Utils
{
    public static class MiniMapCommon
    {
        public const float originMapZRotation = -30f;
        
        // 从配置读取的值
        private static float _cascadeScalingUnits = 2.5f;
        private static float _centerIconSize = 1.3f;
        
        // 属性提供对外访问，带默认值
        public static float CascadeScalingUnits 
        { 
            get 
            {
                if (!_configLoaded)
                    LoadConfig();
                return _cascadeScalingUnits;
            }
        }
        
        public static float CenterIconSize 
        { 
            get 
            {
                if (!_configLoaded)
                    LoadConfig();
                return _centerIconSize;
            }
        }
        
        private static bool _configLoaded = false;
        
        // 加载配置
        private static void LoadConfig()
        {
            try
            {
                JObject? config = ModFileOperations.LoadConfig(ModBehaviour.ModInfo, "iconConfig.json");
                if (config != null)
                {
                    // 读取级联缩放单位
                    if (config.TryGetValue("cascadeScalingUnits", out JToken? cascadeToken))
                    {
                        _cascadeScalingUnits = cascadeToken.Value<float>();
                    }
                    
                    // 读取中心图标大小
                    if (config.TryGetValue("centerIconSize", out JToken? centerToken))
                    {
                        _centerIconSize = centerToken.Value<float>();
                    }
                }
                _configLoaded = true;
                
                Log.Info($"从配置加载: CascadeScalingUnits={_cascadeScalingUnits}, CenterIconSize={_centerIconSize}");
            }
            catch (System.Exception e)
            {
                Log.Error($"加载iconConfig.json失败: {e.Message}");
                // 使用默认值
                _configLoaded = true;
            }
        }
        
        // 重新加载配置（如果需要动态更新）
        public static void ReloadConfig()
        {
            _configLoaded = false;
            LoadConfig();
        }

        private static float GetAngle()
        {
            Vector3 to = LevelManager.Instance.InputManager.InputAimPoint - CharacterMainControl.Main.transform.position;
            return Vector3.SignedAngle(Vector3.forward, to, Vector3.up);
        }

        public static float GetChracterRotation(CharacterMainControl? character)
        {
            if (character == null)
            {
                return 0;
            }
            string facingBase = ModSettingManager.GetActualDropdownValue(ModBehaviour.ModInfo, "facingBase", false);
            if (character.IsMainCharacter && facingBase == "Mouse")
            {
                return -GetAngle();
            }
            else
            {
                return -character.modelRoot.rotation.eulerAngles.y;
            }
        }

        public static float GetMinimapRotation()
        {
            return -GetChracterRotation(CharacterMainControl.Main);
        }
    }
}