using Duckov.MiniMaps;
using MiniMap.Extentions;
using MiniMap.Managers;
using MiniMap.Poi;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using ZoinkModdingLibrary.Utils;

namespace MiniMap.Utils
{
    public static class PoiCommon
    {
        private static Sprite? GetIcon(JObject? config, string? presetName, out float scale, out CharacterType characterType)
        {
            if (config == null || string.IsNullOrEmpty(presetName))
            {
                scale = 0.5f;
                characterType = CharacterType.Enemy;
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
                    if (presetName == "PetPreset_NormalPet")
                    {
                        characterType = CharacterType.Pet;
                    }
                    else
                    {
                        characterType = item.Key switch
                        {
                            "friendly" => CharacterType.NPC,
                            "neutral" => CharacterType.Neutral,
                            "boss" => CharacterType.Boss,
                            _ => CharacterType.Enemy,
                        };
                    }
                    return ModFileOperations.LoadSprite(iconName);
                }
            }
            scale = defaultScale;
            characterType = CharacterType.Enemy;
            return ModFileOperations.LoadSprite(defaultIconName);
        }

        public static void CreatePoiIfNeeded(CharacterMainControl? character, out CharacterPointOfInterest? characterPoi, out DirectionPointOfInterest? directionPoi)
        {
            if (!LevelManager.LevelInited || character == null)
            {
                characterPoi = null;
                directionPoi = null;
                return;
            }
            if (character.transform.parent?.name == "Level_Factory_Main")
            {
                if (character.gameObject != null)
                {
                    GameObject.Destroy(character.gameObject);
                }
                characterPoi = null;
                directionPoi = null;
                return;
            }
            SimplePointOfInterest? originPoi = character.GetComponentInChildren<SimplePointOfInterest>() ?? character.GetComponent<SimplePointOfInterest>();
            GameObject poiObject = originPoi != null ? originPoi.gameObject : character.gameObject;
            if (poiObject == null)
            {
                characterPoi = null;
                directionPoi = null;
                return;
            }
            CharacterRandomPreset? preset = character.characterPreset;
            if (preset == null && !character.IsMainCharacter)
            {
                characterPoi = null;
                directionPoi = null;
                return;
            }
            characterPoi = poiObject.GetOrAddComponent<CharacterPointOfInterest>();
            directionPoi = poiObject.GetOrAddComponent<DirectionPointOfInterest>();
            CharacterType characterType;
            float scaleFactor = 1;
            if (!characterPoi.Initialized)
            {
                JObject? iconConfig = ModFileOperations.LoadJson("iconConfig.json", ModBehaviour.Logger);
                Sprite? icon = GetIcon(iconConfig, preset?.name, out scaleFactor, out characterType);
                if (character.IsMainCharacter)
                {
                    characterType = CharacterType.Main;
                    scaleFactor = 1f;  //中心图标 大小地图一起 （不包括文字 和 与文字间距）
                }
                characterPoi.ScaleFactor = scaleFactor / 2.5f;  // characterPoi 大小地图一起 只角色(全部) 位置图标 、名字文字、文字间距
                if (originPoi == null)
                {
                    characterPoi.Setup(icon, character, characterType, preset?.nameKey, followActiveScene: true);
                }
                else
                {
                    characterPoi.Setup(originPoi, character, characterType, followActiveScene: true);
                }
                if (originPoi)
                {
                    GameObject.Destroy(originPoi);
                }
            }
            else
            {
                characterType = characterPoi.CharacterType;
            }

            if (!directionPoi.Initialized)
            {
                Sprite? icon = ModFileOperations.LoadSprite("CharactorDirection.png");
                directionPoi.BaseEulerAngle = 45f;
                directionPoi.ScaleFactor = scaleFactor / 2.5f;  // directionPoi 大小地图一起 只角色全部 箭头（不包括文字 和 与文字间距）
                directionPoi.Setup(icon, character, characterType, cachedName: preset?.DisplayName, followActiveScene: true);
            }
        }

        public static bool IsDead(CharacterMainControl? character)
        {
            return !(character != null && character.Health && !character.Health.IsDead);
        }
    }
}
