using Duckov.MiniMaps;
using Duckov.Modding;
using Duckov.Scenes;
using MiniMap.Managers;
using MiniMap.Utils;
using SodaCraft.Localizations;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ZoinkModdingLibrary.Extentions;
using ZoinkModdingLibrary.ModSettings;
using ZoinkModdingLibrary.Utils;

namespace MiniMap.Poi
{
    public abstract class CharacterPoiBase : MonoBehaviour, IPointOfInterest
    {
        private bool initialized = false;

        private CharacterMainControl? character;
        private CharacterType characterType;
        private string? cachedName;
        private bool showOnlyActivated;
        private Sprite? icon;
        private Color color = Color.white;
        private Color shadowColor = Color.clear;
        private float shadowDistance = 0f;
        private bool localized = true;
        private bool followActiveScene;
        private bool isArea;
        private float areaRadius;
        private float scaleFactor = 1f;
        private bool hideIcon = false;
        private string? overrideSceneID;
        


        public virtual bool Initialized => initialized;
        public virtual CharacterMainControl? Character => character;
        public virtual CharacterType CharacterType => characterType;
        public virtual string? CachedName { get => cachedName; set => cachedName = value; }
        public virtual bool ShowOnlyActivated
        {
            get => showOnlyActivated;
            protected set
            {
                showOnlyActivated = value;
                if (value && !(character?.gameObject.activeSelf ?? false))
                {
                    Unregister();
                }
                else
                {
                    Register();
                }
            }
        }
        public virtual string DisplayName => CachedName?.ToPlainText() ?? string.Empty;
        public virtual float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
        public virtual Color Color { get => color; set => color = value; }
        public virtual Color ShadowColor { get => shadowColor; set => shadowColor = value; }
        public virtual float ShadowDistance { get => shadowDistance; set => shadowDistance = value; }
        public virtual bool Localized { get => localized; set => localized = value; }
        public virtual Sprite? Icon => icon;
        
        public virtual int OverrideScene
        {
            get
            {
                if (followActiveScene && MultiSceneCore.ActiveSubScene.HasValue)
                {
                    return MultiSceneCore.ActiveSubScene.Value.buildIndex;
                }

                if (!string.IsNullOrEmpty(overrideSceneID))
                {
                    List<SceneInfoEntry>? entries = SceneInfoCollection.Entries;
                    SceneInfoEntry? sceneInfo = entries?.Find(e => e.ID == overrideSceneID);
                    return sceneInfo?.BuildIndex ?? -1;
                }
                return -1;
            }
        }
        public virtual bool IsArea { get => isArea; set => isArea = value; }
        public virtual float AreaRadius { get => areaRadius; set => areaRadius = value; }
        public virtual bool HideIcon { get => hideIcon; set => hideIcon = value; }
        protected virtual void OnEnable()
        {
            Register();
        }

        protected virtual void OnDisable()
        {
            if (ShowOnlyActivated)
            {
                Unregister();
            }
        }

        public virtual void Setup(Sprite? icon, CharacterMainControl character, CharacterType characterType, string? cachedName = null, bool followActiveScene = false, string? overrideSceneID = null)
        {
            if (initialized) return;
            this.character = character;
            this.characterType = characterType;
            this.icon = icon;
            this.cachedName = cachedName;
            this.followActiveScene = followActiveScene;
            this.overrideSceneID = overrideSceneID;
            ShowOnlyActivated = ModSettingManager.GetValue(ModBehaviour.ModInfo, "showOnlyActivated", false);

            // ============ 修改：根据敌人类型设置图标大小 ============
            SetIconSizeFactorByCharacterType(characterType);
			// ============ 修改结束 ============
            
            ModSettingManager.ConfigChanged += OnConfigChanged;
            initialized = true;
        }

        public virtual void Setup(SimplePointOfInterest poi, CharacterMainControl character, CharacterType characterType, bool followActiveScene = false, string? overrideSceneID = null)
        {
            if (initialized) return;
            this.character = character;
            this.characterType = characterType;
            this.icon = GameObject.Instantiate(poi.Icon);
            FieldInfo? field = typeof(SimplePointOfInterest).GetField("displayName", BindingFlags.NonPublic | BindingFlags.Instance);
            this.cachedName = field.GetValue(poi) as string;
            this.followActiveScene = followActiveScene;
            this.overrideSceneID = overrideSceneID;
            this.isArea = poi.IsArea;
            this.areaRadius = poi.AreaRadius;
            this.color = poi.Color;
            this.shadowColor = poi.ShadowColor;
            this.shadowDistance = poi.ShadowDistance;
            ShowOnlyActivated = ModSettingManager.GetValue(ModBehaviour.ModInfo, "showOnlyActivated", false);

            // ============ 修改：根据敌人类型设置图标大小 ============
            SetIconSizeFactorByCharacterType(characterType);
			// ============ 修改结束 ============
            
            ModSettingManager.ConfigChanged += OnConfigChanged;
            initialized = true;
        }
        
        // ============ 修改：返回图标大小因子 ============
        private float IconSizeFactor = 1f;
        public virtual float IconSize => ScaleFactor * IconSizeFactor;
        // ============ 修改结束 ============
		
		// ============ 修改：根据敌人类型设置图标大小 ============
		private void SetIconSizeFactorByCharacterType(CharacterType characterType)
		{
			switch (characterType)
			{
				// case CharacterType.Enemy:
				// case CharacterType.NPC:
				// case CharacterType.Neutral:
					// // 普通敌人、NPC、中立单位共用同一个大小设置
					// IconSizeFactor = 1.0f;
					// break;
				// case CharacterType.Boss:
					// IconSizeFactor = 1.2f;
					// break;
				case CharacterType.Pet:
					IconSizeFactor = 1.6f;
					break;
				// case CharacterType.Main:
					// // 玩家自己（中心图标）使用专门的小地图中心图标大小
					// IconSizeFactor = 1.0f;
					// break;
				// default:
					// IconSizeFactor = 1.0f;
					// break;
			}
		}
		// ============ 修改结束 ============

        private void OnConfigChanged(ModInfo modInfo,string key, object? value)
        {
            if (!modInfo.ModIdEquals(ModBehaviour.Instance!.info)) return;
            if (value == null) return;
            switch (key)
            {
                case "showOnlyActivated":
                    ShowOnlyActivated = (bool)value;
                    break;
                case "showPoiInMiniMap":
                case "showPetPoi":
                case "showBossPoi":
                case "showEnemyPoi":
                case "showNeutralPoi":
                    ModBehaviour.Instance?.ExecuteWithDebounce(() =>
                    {

                    }, () =>
                    {
                        MinimapManager.MinimapDisplay.InvokeMethod("HandlePointsOfInterests");
                    });
                    break;
            }
        }

        // 移除死亡检查，只保留空Update方法用于子类扩展
        protected virtual void Update()
        {
            // 不再进行死亡检查，由事件驱动处理
        }

        protected void OnDestroy()
        {
            ModSettingManager.ConfigChanged -= OnConfigChanged;
        }

        public virtual void Register(bool force = false)
        {
            if (force)
            {
                PointsOfInterests.Unregister(this);
            }
            if (!PointsOfInterests.Points.Contains(this))
            {
                PointsOfInterests.Register(this);
            }
        }

        public virtual void Unregister()
        {
            PointsOfInterests.Unregister(this);
        }

        public virtual bool WillShow(bool isOriginalMap = true)
        {
            bool willShowInThisMap = isOriginalMap ? ModSettingManager.GetValue(ModBehaviour.ModInfo, "showPoiInMap", true) : ModSettingManager.GetValue(ModBehaviour.ModInfo, "showPoiInMiniMap", true);
            return characterType switch
            {
                CharacterType.Main or CharacterType.NPC => true,
                CharacterType.Pet => ModSettingManager.GetValue(ModBehaviour.ModInfo, "showPetPoi", true),
                CharacterType.Boss => ModSettingManager.GetValue(ModBehaviour.ModInfo, "showBossPoi", true) && willShowInThisMap,
                CharacterType.Enemy => ModSettingManager.GetValue(ModBehaviour.ModInfo, "showEnemyPoi", true) && willShowInThisMap,
                CharacterType.Neutral => ModSettingManager.GetValue(ModBehaviour.ModInfo, "showNeutralPoi", true) && willShowInThisMap,
                _ => false,
            };
        }
    }
}
