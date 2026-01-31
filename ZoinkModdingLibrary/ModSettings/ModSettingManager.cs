using Duckov.Modding;
using Newtonsoft.Json.Linq;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using ZoinkModdingLibrary.Extentions;
using ZoinkModdingLibrary.Logging;
using ZoinkModdingLibrary.Utils;

namespace ZoinkModdingLibrary.ModSettings
{
    public static class ModSettingManager
    {
        private static readonly string ModConfigFileName = "modConfig.json";
        private static readonly string ModConfigTemplateFileName = @"template\modConfigTemplate.json";

        //public bool needUpdate = false;

        private static Type? modBehaviour;

        private static bool IsChinese => LocalizationManager.CurrentLanguage.ToString().Contains("Chinese");
        private static string DescKey => IsChinese ? "descCN" : "descEN";
        private static string ButtonTextKey => IsChinese ? "textCN" : "textEN";
        private static string DropDownOptionsKey => IsChinese ? "optionsCN" : "optionsEN";

        public static event Action<ModInfo, string, object?>? ConfigChanged;
        public static event Action? ConfigLoaded;
        public static event Action<ModInfo, string>? ButtonClicked;
        public static event Action? Initialized;

        private static Dictionary<string, (JObject? Template, JObject Config)> configs = new();

        private static (JObject? Template, JObject Config)? GetOrAddModConfig(ModInfo modInfo)
        {
            if (configs.TryGetValue(modInfo.GetModId(), out (JObject? Template, JObject Config) modConfigs))
            {
                return modConfigs;
            }
            else
            {
                JObject? templates = ModFileOperations.LoadConfig(modInfo, ModConfigTemplateFileName);
                JObject config = ModFileOperations.LoadConfig(modInfo, ModConfigFileName) ?? new JObject();
                modConfigs = (templates, config);
                configs.Add(modInfo.GetModId(), modConfigs);
                return modConfigs;
            }
        }

        public static bool Initialize(Action? initialized = null)
        {
            Initialized -= initialized;
            Initialized += initialized;
            if (modBehaviour == null)
            {
                modBehaviour = AssemblyOperations.FindTypeInAssemblies("ModSetting", "ModSetting.ModBehaviour");
                if (modBehaviour == null)
                {
                    ModManager.OnModActivated += OnModActivated;
                    return false;
                }
            }
            Initialized?.Invoke();
            Initialized -= initialized;
            ModManager.OnModActivated -= OnModActivated;
            return true;
        }

        private static void OnModActivated(ModInfo arg1, ModBehaviour arg2)
        {
            Initialize();
        }

        private static Action<T> GetAction<T>(ModInfo modInfo, string key)
        {
            return (v) =>
            {
                SaveValue(modInfo, key, v);
                ConfigChanged?.Invoke(modInfo, key, v);
            };
        }

        private static void SetUI(ModInfo modInfo, string key, JToken? value, string type)
        {
            if (value == null)
            {
                return;
            }
            switch (value.Type)
            {
                case JTokenType.Boolean:
                    modBehaviour.InvokeGenericMethod<bool>("SetValue", parameters: new object?[] { modInfo, key, value.ToObject<bool>(), null });
                    break;
                case JTokenType.Integer:
                    if (type == "dropdownList")
                    {
                        int index = value.ToObject<int>();
                        string optionString = GetActualDropdownValue(modInfo, key, IsChinese);
                        modBehaviour.InvokeGenericMethod<int>("SetValue", parameters: new object?[] { modInfo, key, optionString, null });
                    }
                    else
                    {
                        modBehaviour.InvokeGenericMethod<int>("SetValue", parameters: new object?[] { modInfo, key, value.ToObject<int>(), null });
                    }
                    break;
                case JTokenType.Float:
                    modBehaviour.InvokeGenericMethod<float>("SetValue", parameters: new object?[] { modInfo, key, value.ToObject<float>(), null });
                    break;
                case JTokenType.String:
                    modBehaviour.InvokeGenericMethod<string>("SetValue", parameters: new object?[] { modInfo, key, value.ToString(), null });
                    break;
                default:
                    return;
            }

            SaveValue(modInfo, key, value);
        }

        public static void CreateUI(ModInfo modInfo, bool isReset = false)
        {
            try
            {
                if (!isReset) Log.Info($"Start Create Setting UI");
                var modConfigs = GetOrAddModConfig(modInfo);
                if (modConfigs == null) return;
                if (modConfigs.Value.Template == null || modBehaviour == null) return;
                foreach (KeyValuePair<string, JToken?> option in modConfigs.Value.Template)
                {
                    CreateOneOption(modInfo, modConfigs.Value.Config, option, isReset);
                }
                if (!isReset) Log.Info($"Setting UI Created");
                ConfigLoaded?.Invoke();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to Create Setting UI:{e.Message}");
            }
        }

        private static void CreateOneOption(ModInfo modInfo, JObject modSettings, KeyValuePair<string, JToken?> option, bool isReset = false)
        {
            try
            {
                string? type = option.Value?["type"]?.ToString();
                if (type == null || modBehaviour == null)
                {
                    Log.Error($"创建失败: Key:{option.Key}, type:{type ?? "null"}, Requirement: {modBehaviour?.ToString() ?? "null"}");
                    return;
                }
                string desc = option.Value?[DescKey]?.ToString() ?? "";
                List<object?> parameters = new List<object?> { modInfo, option.Key, desc };
                JToken? value = null;
                if (type != "group")
                {
                    if (!isReset)
                        value = modSettings[option.Key];
                    value ??= option.Value?["default"];
                    if (isReset)
                    {
                        SetUI(modInfo, option.Key, value, type);
                    }
                    else
                    {
                        SaveValue(modInfo, modSettings, option.Key, value);
                    }
                }
                else
                {
                    JToken? configs = option.Value?["configs"];
                    if (configs is JObject configsObj)
                    {
                        List<string> keys = new List<string>();
                        foreach (KeyValuePair<string, JToken?> config in configsObj)
                        {
                            CreateOneOption(modInfo, modSettings, config, isReset);
                            keys.Add(config.Key);
                        }
                        if (isReset) return;
                        float scale = configsObj["scale"]?.ToObject<float>() ?? 0.7f;
                        parameters.AddRange(new object?[] { keys, scale, false, false });
                        modBehaviour.InvokeMethod("AddGroup", parameters: parameters.ToArray());
                    }
                    return;
                }

                if (isReset) return;

                switch (type)
                {
                    case "noUI":
                        return;
                    case "slider":
                        float min = option.Value?["min"]?.ToObject<float>() ?? 0f;
                        float max = option.Value?["max"]?.ToObject<float>() ?? 1f;
                        Type[]? types = new Type[] { typeof(ModInfo), typeof(string), typeof(string), typeof(float), typeof(Vector2), typeof(Action<float>), typeof(int), typeof(int) };
                        parameters.AddRange(new object?[] { value?.ToObject<float>(), new Vector2(min, max), GetAction<float>(modInfo, option.Key), option.Value?["decimalPlaces"]?.ToObject<int>() ?? 1, 5 });
                        modBehaviour.InvokeMethod("AddSlider", types, parameters.ToArray());
                        return;
                    case "toggle":
                        parameters.AddRange(new object?[] { value?.ToObject<bool>(), GetAction<bool>(modInfo, option.Key) });
                        modBehaviour.InvokeMethod("AddToggle", parameters: parameters.ToArray());
                        return;
                    case "keyBinding":
                        // 先尝试解析为 Key（新 Input System）
                        string? valueString = value?.ToString();
                        Key keyValue;
                        if (!Enum.TryParse(valueString, true, out keyValue)) { keyValue = Key.None; }
                        string? defaultString = option.Value?["default"]?.ToString();
                        Key defaultKey;
                        if (!Enum.TryParse(defaultString, true, out defaultKey)) { defaultKey = Key.None; }
                        parameters.AddRange(new object?[] { keyValue, defaultKey, GetAction<Key>(modInfo, option.Key) });
                        modBehaviour.InvokeMethod("AddKeybindingWithKey", parameters: parameters.ToArray());
                        return;
                    case "dropdownList":
                        List<string> options = option.Value?[DropDownOptionsKey] is JArray array ? array.ToList<string>() : new List<string>();
                        int index = value?.ToObject<int>() ?? 0;
                        if (index < 0 || index >= options.Count) index = 0;
                        string optionString = options.Count > 0 ? options[index] : string.Empty;
                        Action<string> action = (v) =>
                        {
                            int index = options.IndexOf(v);
                            if (index == -1)
                            {
                                index = 0;
                            }
                            SaveValue(modInfo, option.Key, index);
                            ConfigChanged?.Invoke(modInfo, option.Key, index);
                        };
                        parameters.AddRange(new object?[] { options, optionString, action });
                        modBehaviour.InvokeMethod("AddDropDownList", parameters: parameters.ToArray());
                        return;
                    case "input":
                        int characterLimit = option.Value?["characterLimit"]?.ToObject<int>() ?? 40;
                        parameters.AddRange(new object?[] { value?.ToString(), characterLimit, GetAction<string>(modInfo, option.Key) });
                        modBehaviour?.InvokeMethod("AddInput", parameters: parameters.ToArray());
                        return;
                    case "button":
                        string buttonText = option.Value?[ButtonTextKey]?.ToString() ?? "";
                        Action onButtonClick = () => { ButtonClicked?.Invoke(modInfo, option.Key); };
                        parameters.AddRange(new object?[] { buttonText, onButtonClick });
                        modBehaviour?.InvokeMethod("AddButton", parameters: parameters.ToArray());
                        return;
                    default:
                        Log.Warning($"未知的配置类型: {type}");
                        return;
                }
            }
            catch (Exception e)
            {
                Log.Error($"{option.Key}创建失败:{e.Message}\n{e.StackTrace}");
            }
        }

        private static void SaveValue(ModInfo modInfo, JObject config, string key, object? value)
        {
            config[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
            ModFileOperations.SaveConfig(modInfo, ModConfigFileName, config);
        }

        public static void SaveValue(ModInfo modInfo, string key, object? value)
        {
            var modConfigs = GetOrAddModConfig(modInfo);
            if (modConfigs == null) return;
            SaveValue(modInfo, modConfigs.Value.Config, key, value);
        }

        public static T? GetValue<T>(ModInfo modInfo, string key, T? failBack = default)
        {
            var modConfigs = GetOrAddModConfig(modInfo);
            if (modConfigs == null) return failBack;
            JToken? token = modConfigs.Value.Config[key];
            if (token == null)
                return failBack;
            return token.ToObject<T>();
        }

        public static JObject? GetTemplate(string key, JObject? templates)
        {
            if (templates == null)
            {
                return null;
            }
            foreach (KeyValuePair<string, JToken?> item in templates)
            {
                if (item.Key == key)
                {
                    return item.Value as JObject;
                }
                else if (item.Value is JObject sub)
                {
                    JObject? result = GetTemplate(key, sub);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        public static string GetActualDropdownValue(ModInfo modInfo, string key, bool isChinese = true)
        {
            var modConfigs = GetOrAddModConfig(modInfo);
            if (modConfigs == null) return string.Empty;
            int index = GetValue(modInfo, key, 0);
            JObject? template = GetTemplate(key, modConfigs.Value.Template);
            List<string>? options = (template?[isChinese ? "optionsCN" : "optionsEN"] as JArray)?.ToList<string>();
            if (options == null)
            {
                return string.Empty;
            }
            if (index < 0 || index >= options.Count)
            {
                index = 0;
            }
            return options.Count > 0 ? options[index] : string.Empty;
        }
    }
}