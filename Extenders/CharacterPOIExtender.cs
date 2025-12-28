using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using HarmonyLib;
using MiniMap.Managers;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MiniMap.Extenders
{
    [HarmonyPatch(typeof(CharacterSpawnerRoot))]
    [HarmonyPatch("AddCreatedCharacter")]
    public static class CharacterSpawnerRootAddCharacterExtender
    {
        public static bool Prefix(CharacterMainControl c)
        {
            try
            {
                Debug.Log($"[MiniMap] Adding characterPoi for {c.characterPreset?.nameKey}");
                CharacterMainControlUpdateExtender.CreatePoiIfNeeded(c, out _, out _);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniMap] characterPoi add failed: {e.Message}");
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterMainControl))]
    [HarmonyPatch("Update")]
    public static class CharacterMainControlUpdateExtender
    {
        private static Sprite? GetIcon(JObject? config, string presetName, out float scale, out bool isBoss)
        {
            if (config == null)
            {
                scale = 0.5f;
                isBoss = false;
                return null;
            }
            float defaultScale = config.Value<float?>("defaultScale") ?? 1f;
            string? defaultIconName = config.Value<string?>("defaultIcon");
            foreach (KeyValuePair<string, JToken?> item in config)
            {
                if (item.Value is not JObject jObject) { continue; }
                if (jObject.ContainsKey(presetName))
                {
                    string? iconName = jObject.Value<string?>(presetName);
                    if (string.IsNullOrEmpty(iconName))
                    {
                        iconName = jObject.Value<string?>("defaultIcon");
                    }
                    if (string.IsNullOrEmpty(iconName))
                    {
                        iconName = defaultIconName;
                    }
                    scale = jObject.Value<float?>("scale") ?? defaultScale;
                    isBoss = item.Key.ToLower() == "boss";
                    return Util.LoadSprite(iconName);
                }
            }
            scale = defaultScale;
            isBoss = false;
            return Util.LoadSprite(defaultIconName);
        }

        public static void CreatePoiIfNeeded(CharacterMainControl character, out IPointOfInterest? characterPoi, out DirectionPointOfInterest? directionPoi)
        {
            if (!LevelManager.LevelInited)
            {
                characterPoi = null;
                directionPoi = null;
                return;
            }

            GameObject poiObject = character.gameObject;
            if (poiObject == null)
            {
                characterPoi = null;
                directionPoi = null;
                return;
            }
            directionPoi = poiObject.GetComponent<DirectionPointOfInterest>();
            characterPoi = poiObject.GetComponent<SimplePointOfInterest>();
            characterPoi ??= poiObject.GetComponent<CharacterPointOfInterest>();
            if (character.transform.parent?.name == "Level_Factory_Main")
            {
                if (poiObject != null)
                {
                    GameObject.Destroy(poiObject);
                }
                return;
            }
            if (characterPoi == null)
            {
                CharacterRandomPreset preset = character.characterPreset;
                if (preset == null)
                {
                    return;
                }
                characterPoi = poiObject.AddComponent<CharacterPointOfInterest>();
                CharacterPointOfInterest pointOfInterest = (CharacterPointOfInterest)characterPoi;
                JObject? iconConfig = Util.LoadConfig("iconConfig.json");

                Sprite? icon = GetIcon(iconConfig, preset.name, out float scale, out bool isBoss);
                pointOfInterest.Setup(icon, character, displayName: preset.nameKey, followActiveScene: true);
                pointOfInterest.ScaleFactor = scale;
                CustomMinimapManager.CallDisplayMethod("HandlePointsOfInterests");
            }
            if (directionPoi == null)
            {
                CharacterRandomPreset preset = character.characterPreset;
                directionPoi = poiObject.AddComponent<DirectionPointOfInterest>();
                Sprite? icon = Util.LoadSprite("CharactorDirection.png");
                directionPoi.BaseEulerAngle = 45f;
                directionPoi.Setup(icon, character: character, tagName: preset?.DisplayName, followActiveScene: true);
                directionPoi.ScaleFactor = characterPoi.ScaleFactor;
            }
        }

        public static void Postfix(CharacterMainControl __instance)
        {
            try
            {
                CreatePoiIfNeeded(__instance, out _, out _);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniMap] characterPoi update failed: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PointOfInterestEntry))]
    [HarmonyPatch("UpdateRotation")]
    public static class PointOfInterestEntryUpdateRotationExtender
    {
        public static bool Prefix(PointOfInterestEntry __instance, MiniMapDisplayEntry ___minimapEntry)
        {
            try
            {
                if (__instance.Target is DirectionPointOfInterest poi)
                {
                    MiniMapDisplay? display = ___minimapEntry.GetComponentInParent<MiniMapDisplay>();
                    if (display == null)
                    {
                        return true;
                    }
                    __instance.transform.rotation = Quaternion.Euler(0f, 0f, poi.RealEulerAngle + display.transform.rotation.eulerAngles.z);
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniMap] PointOfInterestEntry UpdateRotation failed: {e.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(PointOfInterestEntry))]
    [HarmonyPatch("Update")]
    public static class PointOfInterestEntryUpdateExtender
    {
        public static bool Prefix(PointOfInterestEntry __instance, Image ___icon)
        {
            var character = __instance.Target as CharacterMainControl;
            if (__instance.Target == null || character != null && CharacterCommon.IsDead(character))
            {
                GameObject.Destroy(__instance.gameObject);
                return false;
            }
            if (character?.IsMainCharacter ?? false)
            {
                var parent = __instance.transform.parent;
                if (parent != null && parent.GetChild(parent.childCount - 1) != __instance)
                {
                    __instance.transform.SetAsLastSibling();
                    Debug.Log("[MiniMap] Move main character POI entry to top");
                }
            }

            RectTransform icon = ___icon.rectTransform;
            RectTransform? layout = icon.parent as RectTransform;
            if (layout == null) { return true; }
            if (layout.localPosition + icon.localPosition != Vector3.zero)
            {
                layout.localPosition = Vector3.zero - icon.localPosition;
            }
            return true;
        }
    }
}
