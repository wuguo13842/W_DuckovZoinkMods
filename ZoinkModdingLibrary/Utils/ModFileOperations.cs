using Duckov.Modding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using ZoinkModdingLibrary.Extentions;
using ZoinkModdingLibrary.Logging;
using ZoinkModdingLibrary.ModSettings;

namespace ZoinkModdingLibrary.Utils
{
    public static class ModFileOperations
    {
        private static Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();

        public static Sprite? LoadSprite(ModInfo modInfo, string? texturePath)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                return null;
            }
            var key = $"{modInfo.GetModId()}-{texturePath}";
            lock (LoadedSprites)
            {
                if (LoadedSprites.ContainsKey(key))
                {
                    return LoadedSprites[key];
                }
                string? directoryName = modInfo.path;
                if (string.IsNullOrEmpty(directoryName))
                {
                    Log.Error("Failed to get directory for loading sprite.");
                    return null;
                }
                string path = Path.Combine(directoryName, "textures");
                string text = Path.Combine(path, texturePath);
                if (File.Exists(text))
                {
                    byte[] data = File.ReadAllBytes(text);
                    Texture2D texture2D = new Texture2D(2, 2);
                    if (texture2D.LoadImage(data))
                    {
                        Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), new Vector2(0.5f, 0.5f));
                        if (!LoadedSprites.ContainsKey(key))
                        {
                            LoadedSprites[key] = sprite;
                        }
                        return sprite;
                    }
                }
                return null;
            }
        }

        public static JObject? LoadConfig(ModInfo modInfo, string filePath)
        {
            string? directoryName = modInfo.path;
            if (string.IsNullOrEmpty(directoryName))
            {
                Log.Error("Failed to get directory for loading json.");
                return null;
            }
            string path = Path.Combine(directoryName, "config");
            string text = Path.Combine(path, filePath);
            try
            {
                if (File.Exists(text))
                {
                    string jsonText = File.ReadAllText(text);
                    return JObject.Parse(jsonText);
                }
                return null;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to Load Json({text}):\n{e.Message}");
                return null;
            }
        }

        public static void SaveConfig(ModInfo modInfo, string modConfigFileName, JObject modConfig)
        {
            string? directoryName = modInfo.path;
            if (directoryName == null)
            {
                Log.Error("Failed to get directory for saving json.");
                return;
            }
            string path = Path.Combine(directoryName, "config");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string text = Path.Combine(path, modConfigFileName);
            try
            {
                File.WriteAllText(text, modConfig.ToString(Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Error($"Failed to Save Json: {e.Message}");
                throw;
            }
        }
    }
}
