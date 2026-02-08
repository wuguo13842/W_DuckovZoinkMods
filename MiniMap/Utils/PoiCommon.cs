using Duckov.MiniMaps;
using MiniMap.Poi;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Utils;
using MiniMap.Utils;

namespace MiniMap.Utils
{
	public static class PoiCommon
	{
		private static Sprite? GetIcon(JObject? config, string? presetName, 
			out float iconScale, out float nameScale, out CharacterType characterType)
		{
			// 设置默认值
			iconScale = 0.5f;
			nameScale = 1.0f;
			characterType = CharacterType.Enemy;
			
			if (config == null || string.IsNullOrEmpty(presetName))
			{
				return null;
			}
			
			float defaultIconScale = config.Value<float?>("defaultIconScale") ?? 0.6f;
			float defaultNameScale = config.Value<float?>("defaultNameScale") ?? 1.0f;
			string? defaultIconName = config.Value<string?>("defaultIcon");
			
			foreach (KeyValuePair<string, JToken?> item in config)
			{
				if (item.Value is not JObject jObject) { continue; }
				
				// 检查这个分组是否有此预设的配置
				bool hasConfig = false;
				JToken? presetConfig = null;
				
				// 预设可能是字符串（图标名）或对象（详细配置）
				if (jObject.TryGetValue(presetName, out presetConfig))
				{
					hasConfig = true;
				}
				
				if (hasConfig)
				{
					string? iconName = null;
					float groupIconScale = jObject.Value<float?>("iconScale") ?? defaultIconScale;
					float groupNameScale = jObject.Value<float?>("nameScale") ?? defaultNameScale;
					
					// 处理不同的配置格式
					if (presetConfig is JObject presetObj)
					{
						// 对象格式：{"icon": "xxx.png", "iconScale": 0.7, "nameScale": 1.2}
						iconName = presetObj.Value<string?>("icon");
						iconScale = presetObj.Value<float?>("iconScale") ?? groupIconScale;
						nameScale = presetObj.Value<float?>("nameScale") ?? groupNameScale;
					}
					else if (presetConfig.Type == JTokenType.String)
					{
						// 字符串格式："xxx.png"
						iconName = presetConfig.Value<string>();
						iconScale = groupIconScale;
						nameScale = groupNameScale;
					}
					
					if (string.IsNullOrEmpty(iconName))
					{
						iconName = jObject.Value<string?>("defaultIcon");
					}
					if (string.IsNullOrEmpty(iconName))
					{
						iconName = defaultIconName;
					}
					
					// 确定角色类型
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
					
					return ModFileOperations.LoadSprite(ModBehaviour.ModInfo, iconName);
				}
			}
			
			// 没有找到配置，使用默认值
			iconScale = defaultIconScale;
			nameScale = defaultNameScale;
			characterType = CharacterType.Enemy;
			return ModFileOperations.LoadSprite(ModBehaviour.ModInfo, defaultIconName);
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
            float iconScale = 1f;
            if (!characterPoi.Initialized)
            {
                JObject? iconConfig = ModFileOperations.LoadConfig(ModBehaviour.ModInfo, "iconConfig.json");
                Sprite? icon = GetIcon(iconConfig, preset?.name, out iconScale, out float nameScale, out characterType);
                if (character.IsMainCharacter)
                {
                    characterType = CharacterType.Main;
                    iconScale = MiniMapCommon.CenterIconSize;  //中心图标 大小地图一起 （不包括文字 和 与文字间距）
                }
				
				// 存储缩放值到POI中，需要在CharacterPoiBase中添加新属性
				characterPoi.ScaleFactor = iconScale; // characterPoi 大小地图一起 只角色(全部) 位置图标 、名字文字、文字间距
				// 存储名字缩放值（需要在CharacterPoiBase中添加NameScaleFactor属性）
				characterPoi.NameScaleFactor = nameScale / iconScale; // 文字逆更正
				
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
                Sprite? icon = ModFileOperations.LoadSprite(ModBehaviour.ModInfo, "CharactorDirection.png");
                directionPoi.BaseEulerAngle = 45f;
                directionPoi.ScaleFactor = iconScale;  // directionPoi 大小地图一起 只角色全部 箭头（不包括文字 和 与文字间距）
                directionPoi.Setup(icon, character, characterType, cachedName: preset?.DisplayName, followActiveScene: true);
            }
        }

		public static bool IsDead(CharacterMainControl? character)
		{
			return !(character != null && character.Health && !character.Health.IsDead);
		}
	}
}
