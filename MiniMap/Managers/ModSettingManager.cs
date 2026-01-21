using MiniMap.Extentions;
using MiniMap.Utils;
using Newtonsoft.Json.Linq;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using UnityEngine;
using ZoinkModdingLibrary.Utils;

namespace MiniMap.Managers
{
    public static class ModSettingManager
    {
        private static readonly string ModConfigFileName = "modConfig.json";
        private static readonly string ModConfigTemplateFileName = @"template\modConfigTemplate.json";

        public static bool needUpdate = false;

        private static bool isChinese = LocalizationManager.CurrentLanguage.ToString().Contains("Chinese");
        private static JObject? modSettingsTemplates;
        private static JObject? modSettings;

        public static bool IsChinese => isChinese;

        public static event Action<string, object>? ConfigChanged;
        public static event Action? ConfigLoaded;
        public static event Action<string>? ButtonClicked;
        public static JObject ModSettings
        {
            get
            {
                if (modSettings == null)
                {
                    modSettings = ModFileOperations.LoadJson(ModConfigFileName, ModBehaviour.Logger);
                    if (modSettings == null)
                    {
                        modSettings = new JObject();
                    }
                }
                return modSettings!;
            }
        }

        private static JObject? ModSettingsTemplates
        {
            get
            {
                if (modSettingsTemplates == null)
                {
                    modSettingsTemplates = ModFileOperations.LoadJson(ModConfigTemplateFileName, ModBehaviour.Logger);
                    if (modSettingsTemplates == null)
                    {
                        ModBehaviour.Logger.LogError($"加载Mod配置模板失败，请确保template文件夹下存在modConfigTemplate.json文件");
                    }
                }
                return modSettingsTemplates;
            }
        }

        private static void LoadDefultNoUISettings(bool save = false)
        {
            JToken? noUIConfigs = ModSettingsTemplates?["noUIConfigs"];
            if (noUIConfigs is JObject noUIConfigsObj)
            {
                foreach (KeyValuePair<string, JToken?> config in noUIConfigsObj)
                {
                    JToken? value = config.Value?["default"];
                    if (value == null)
                    {
                        continue;
                    }
                    ModSettings[config.Key] = value;
                }
            }
            if (save)
            {
                ModFileOperations.SaveJson(ModConfigFileName, ModSettings, ModBehaviour.Logger);
            }
        }

        public static void ResetAllConfigs()
        {
            if (modSettings == null)
            {
                modSettings = new JObject();
            }
            JToken? groups = ModSettingsTemplates?["groups"];
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    JToken? configs = group["configs"];
                    if (configs is JObject configsObj)
                    {
                        foreach (KeyValuePair<string, JToken?> config in configsObj)
                        {
                            SetUI(config);
                        }
                    }
                }
            }
            JToken? extraConfigs = ModSettingsTemplates?["extraConfigs"];
            if (extraConfigs is JObject extraConfigsObj)
            {
                foreach (KeyValuePair<string, JToken?> config in extraConfigsObj)
                {
                    SetUI(config);
                }
            }

            LoadDefultNoUISettings();

            ModFileOperations.SaveJson(ModConfigFileName, modSettings, ModBehaviour.Logger);
        }

        private static void SetUI(KeyValuePair<string, JToken?> config)
        {
            string key = config.Key;
            JToken? value = config.Value?["default"];
            if (value == null)
            {
                return;
            }
            switch (value.Type)
            {
                case JTokenType.Boolean:
                    Api.ModSettingAPI.SetValue(key, value.ToObject<bool>());
                    break;
                case JTokenType.Float:
                    Api.ModSettingAPI.SetValue(key, value.ToObject<float>());
                    break;
                case JTokenType.String:
                    Api.ModSettingAPI.SetValue(key, value.ToString());
                    break;
                default:
                    return;
            }
            ModSettings[config.Key] = value;
        }

        private static void CreateUI()
        {
            ModBehaviour.Logger.Log($"Start Create Setting UI");
            try
            {
                JToken? groups = ModSettingsTemplates?["groups"];
                if (groups != null)
                {
                    if (groups is JObject groupsObj)
                    {
                        foreach (KeyValuePair<string, JToken?> group in groupsObj)
                        {
                            JToken? configs = group.Value?["configs"];
                            if (configs is JObject configsObj)
                            {
                                List<string> keys = new List<string>();
                                foreach (KeyValuePair<string, JToken?> config in configsObj)
                                {
                                    CreateOneSetting(config);
                                    keys.Add(config.Key);
                                }
                                Api.ModSettingAPI.AddGroup(
                                    group.Key,
                                    group.Value?[isChinese ? "descCN" : "descEN"]?.ToString() ?? "",
                                    keys
                                    );
                            }
                        }
                    }
                }
                JToken? extraConfigs = ModSettingsTemplates?["extraConfigs"];
                if (extraConfigs is JObject extraConfigsObj)
                {
                    foreach (KeyValuePair<string, JToken?> config in extraConfigsObj)
                    {
                        CreateOneSetting(config);
                    }
                }
                JToken? noUIConfigs = ModSettingsTemplates?["noUIConfigs"];
                if (noUIConfigs is JObject noUIConfigsObj)
                {
                    foreach (KeyValuePair<string, JToken?> config in noUIConfigsObj)
                    {
                        CreateOneSetting(config, true);
                    }
                }
                needUpdate = false;
                ModBehaviour.Logger.Log($"Setting UI Created");
                ConfigLoaded?.Invoke();
            }
            catch (Exception e)
            {
                ModBehaviour.Logger.LogError($"Failed to Create Setting UI:{e.Message}");
                throw;
            }
        }

        private static void CreateOneSetting(KeyValuePair<string, JToken?> option, bool noUI = false)
        {
            string? type = option.Value?["type"]?.ToString();
            if (type == null)
            {
                return;
            }
            switch (type)
            {
                case "slider":
                    {
                        float? value = ModSettings[option.Key]?.ToObject<float>();
                        if (value == null)
                        {
                            value = option.Value?["default"]?.ToObject<float>() ?? 0f;
                            SaveValue(option.Key, value, true);
                        }
                        if (!noUI)
                        {
                            float min = option.Value?["min"]?.ToObject<float>() ?? 0f;
                            float max = option.Value?["max"]?.ToObject<float>() ?? 1f;
                            Api.ModSettingAPI.AddSlider(
                                option.Key,
                                option.Value?[isChinese ? "descCN" : "descEN"]?.ToString() ?? "",
                                value,
                                new Vector2(min, max),
                                (v) =>
                                {
                                    SaveValue(option.Key, v);
                                    ConfigChanged?.Invoke(option.Key, v);
                                },
                                option.Value?["decimalPlaces"]?.ToObject<int>() ?? 1
                            );
                        }
                        break;
                    }
                case "toggle":
                    {
                        bool? value = ModSettings[option.Key]?.ToObject<bool>();
                        if (value == null)
                        {
                            value = option.Value?["default"]?.ToObject<bool>() ?? false;
                            SaveValue(option.Key, value, true);
                        }
                        if (!noUI)
                        {
                            Api.ModSettingAPI.AddToggle(
                                option.Key,
                                option.Value?[isChinese ? "descCN" : "descEN"]?.ToString() ?? "",
                                value.Value,
                                (v) =>
                                {
                                    SaveValue(option.Key, v);
                                    ConfigChanged?.Invoke(option.Key, v);
                                }
                            );
                        }
                        break;
                    }
                case "keyBinding":
                    {
                        string? valueString = ModSettings[option.Key]?.ToString();
                        if (string.IsNullOrEmpty(valueString))
                        {
                            valueString = option.Value?["default"]?.ToString() ?? "None";
                            SaveValue(option.Key, valueString, true);
                        }
                        if (!noUI)
                        {
                            KeyCode value = Enum.Parse<KeyCode>(valueString);
                            Api.ModSettingAPI.AddKeybinding(
                                option.Key,
                                option.Value?[isChinese ? "descCN" : "descEN"]?.ToString() ?? "",
                                value,
                                (v) =>
                                {
                                    SaveValue(option.Key, v.ToString());
                                    ConfigChanged?.Invoke(option.Key, v);
                                }
                            );
                        }
                        break;
                    }
                case "dropdownList":
                    {
                        List<string> options = option.Value?[isChinese ? "optionsCN" : "optionsEN"] is JArray array ? array.ToList<string>() : new List<string>();
                        int? value = ModSettings[option.Key]?.ToObject<int>();
                        if (value == null || value < 0 || value >= options.Count)
                        {
                            value = option.Value?["default"]?.ToObject<int>();
                            if (value == null || value < 0 || value >= options.Count)
                            {
                                value = 0;
                            }
                            SaveValue(option.Key, value, true);
                        }
                        string valueString = options.Count > 0 ? options[value.Value] : string.Empty;
                        if (!noUI)
                        {
                            Api.ModSettingAPI.AddDropdownList(
                                option.Key,
                                option.Value?[isChinese ? "descCN" : "descEN"]?.ToString() ?? "",
                                options,
                                valueString,
                                (v) =>
                                {
                                    int index = options.IndexOf(v);
                                    if (index == -1)
                                    {
                                        index = 0;
                                    }
                                    SaveValue(option.Key, index);
                                    ConfigChanged?.Invoke(option.Key, index);
                                }
                            );
                        }
                        break;
                    }
                case "input":
                    {
                        string? value = ModSettings[option.Key]?.ToString();
                        if (value == null)
                        {
                            value = option.Value?["default"]?.ToString() ?? string.Empty;
                            SaveValue(option.Key, value, true);
                        }
                        if (!noUI)
                        {
                            int characterLimit = option.Value?["characterLimit"]?.ToObject<int>() ?? 40;
                            Api.ModSettingAPI.AddInput(
                                option.Key,
                                option.Value?[isChinese ? "descCN" : "descEN"]?.ToString() ?? "",
                                value,
                                characterLimit,
                                (v) =>
                                {
                                    SaveValue(option.Key, v);
                                    ConfigChanged?.Invoke(option.Key, v);
                                }
                            );
                        }
                        break;
                    }
                case "button":
                    {
                        if (!noUI)
                        {
                            Api.ModSettingAPI.AddButton(
                                option.Key,
                                option.Value?[isChinese ? "descCN" : "descEN"]?.ToString() ?? "",
                                option.Value?[isChinese ? "textCN" : "textEN"]?.ToString() ?? "",
                                () => ButtonClicked?.Invoke(option.Key)
                            );
                        }
                        break;
                    }
                default:
                    {
                        ModBehaviour.Logger.LogWarning($"未知的配置类型: {type}");
                        break;
                    }
            }
        }

        public static void SaveValue(string key, object value, bool create = false)
        {
            if (ModSettings.ContainsKey(key) || create)
            {
                ModSettings[key] = JToken.FromObject(value);
                ModFileOperations.SaveJson(ModConfigFileName, ModSettings, ModBehaviour.Logger);
            }
        }

        public static T? GetValue<T>(string key, T? failBack = default) where T : notnull
        {
            JToken? token = ModSettings[key];
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

        public static string GetActualDropdownValue(string key, bool isChinese = true)
        {
            int index = GetValue(key, 0);
            JObject? template = GetTemplate(key, ModSettingsTemplates);
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

        public static void Update()
        {
            if (!needUpdate)
                return;
            if (!LevelManager.LevelInited)
            {
                return;
            }
            CreateUI();
        }
    }
}