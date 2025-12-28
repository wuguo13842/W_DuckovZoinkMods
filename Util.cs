using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace MiniMap
{
    public static class Util
    {
        public static List<T> ToList<T>(this JArray jArray)
        {
            List<T> result = new List<T>();
            foreach (var item in jArray)
            {
                T? value = item.ToObject<T>();
                if (value == null)
                    throw new NullReferenceException($"Failed to convert to {typeof(T)}");
                result.Add(value);
            }
            return result;
        }

        public static string? DirectoryName = null;
        public static Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();

        public static string GetDirectory()
        {
            if (DirectoryName == null)
            {
                DirectoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
            return DirectoryName;
        }

        public static string? GetFilePath(string fileName, string? subFolder = null)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }
            string directoryName = GetDirectory();
            if (!string.IsNullOrEmpty(subFolder))
            {
                directoryName = Path.Combine(directoryName, subFolder);
            }
            return Path.Combine(directoryName, fileName);
        }

        public static Texture2D CreateCircleTexture(int radius, Color color, int padding = 0)
        {
            int size = radius * 2 + padding * 2;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) +
                                              (y - center) * (y - center));

                    if (distance <= radius)
                    {
                        texture.SetPixel(x, y, color);
                    }
                    else
                    {
                        texture.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                }
            }
            texture.Apply();
            return texture;
        }

        public static bool CreateFilledRectTransform(Transform parent, string objectName, out GameObject? gameObject, out RectTransform? rectTransform)
        {
            try
            {
                if (string.IsNullOrEmpty(objectName))
                {
                    objectName = "Zoink_NewGameObject";
                }
                if (!objectName.StartsWith("Zoink_"))
                {
                    objectName = "Zoink_" + objectName;
                }
                gameObject = new GameObject(objectName);
                rectTransform = gameObject.AddComponent<RectTransform>();

                rectTransform.SetParent(parent);
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                return true;
            }
            catch (Exception)
            {
                gameObject = null;
                rectTransform = null;
                return false;
            }
        }

        public static Sprite? LoadSprite(string? textureName)
        {
            lock (LoadedSprites)
            {
                if (string.IsNullOrEmpty(textureName))
                {
                    return null;
                }
                if (LoadedSprites.ContainsKey(textureName))
                {
                    return LoadedSprites[textureName];
                }
                string directoryName = GetDirectory();
                string path = Path.Combine(directoryName, "textures");
                string text = Path.Combine(path, textureName);
                if (File.Exists(text))
                {
                    byte[] data = File.ReadAllBytes(text);
                    Texture2D texture2D = new Texture2D(2, 2);
                    if (texture2D.LoadImage(data))
                    {
                        Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), new Vector2(0.5f, 0.5f));
                        if (!LoadedSprites.ContainsKey(textureName))
                        {
                            LoadedSprites[textureName] = sprite;
                        }
                        return sprite;
                    }
                }
                return null;
            }
        }


        public static JObject? LoadConfig(string fileName)
        {
            string directoryName = GetDirectory();
            string path = Path.Combine(directoryName, "config");
            string text = Path.Combine(path, fileName);
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
                Debug.LogError($"[MiniMap] Failed to Load Congig({text}):\n{e.Message}");
                return null;
            }
        }

        public static void SaveConfig(string modConfigFileName, JObject modConfig)
        {
            string directoryName = GetDirectory();
            string path = Path.Combine(directoryName, "config");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string text = Path.Combine(path, modConfigFileName);
            File.WriteAllText(text, modConfig.ToString(Formatting.Indented));
        }
    }
}