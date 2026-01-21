﻿using Duckov.MiniMaps;
using Duckov.Scenes;
using MiniMap.Extentions;
using MiniMap.Managers;
using MiniMap.Utils;
using SodaCraft.Localizations;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

namespace MiniMap.Poi
{
    public abstract class CharacterPointOfInterestBase : MonoBehaviour, IPointOfInterest
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

        private Vector3 _lastPosition;
        private const float POSITION_CHANGE_THRESHOLD = 0.1f; // 位置变化阈值

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
            _lastPosition = transform.position;
            
            // 初始时通知缓存管理器
            if (PoiCacheManager.Instance != null)
            {
               PoiCacheManager.Instance.ForceUpdateInstance(this); 
            }
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
    ShowOnlyActivated = ModSettingManager.GetValue("showOnlyActivated", false);
    ModSettingManager.ConfigChanged += OnConfigChanged;
    initialized = true;
    
    // 特殊标记：如果是玩家，强制更新一次
    if (characterType == CharacterType.Main && PoiCacheManager.Instance != null)
    {
        PoiCacheManager.Instance.ForceUpdateInstance(this); // 修改这里
    }
    
    _lastPosition = transform.position;
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
    ShowOnlyActivated = ModSettingManager.GetValue("showOnlyActivated", false);
    ModSettingManager.ConfigChanged += OnConfigChanged;
    initialized = true;
    
    // 初始位置
    _lastPosition = transform.position;
    
    // 通知缓存管理器
    if (PoiCacheManager.Instance != null)
    {
        PoiCacheManager.Instance.ForceUpdateInstance(this); // 修改这里
    }
}

        private void OnConfigChanged(string key, object? value)
        {
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
                        CustomMinimapManager.CallDisplayMethod("HandlePointsOfInterests");
                    });
                    break;
            }
        }

protected virtual void Update()
{
    if (character != null && characterType != CharacterType.Main && PoiCommon.IsDead(character))
    {
        Destroy(gameObject);
        return;
    }
    
    // 检查位置是否有明显变化，如果有则通知缓存管理器
    if (PoiCacheManager.Instance != null)
    {
        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, _lastPosition);
        
        if (distanceMoved > POSITION_CHANGE_THRESHOLD)
        {
            _lastPosition = currentPosition;
            PoiCacheManager.Instance.ForceUpdateInstance(this); // 修改这里
        }
    }
}

        protected void OnDestroy()
        {
            ModSettingManager.ConfigChanged -= OnConfigChanged;
            
            // 缓存管理器会自动清理，但我们可以主动通知（可选）
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
            bool willShowInThisMap = isOriginalMap ? ModSettingManager.GetValue("showPoiInMap", true) : ModSettingManager.GetValue("showPoiInMiniMap", true);
            return characterType switch
            {
                CharacterType.Main or CharacterType.NPC => true,
                CharacterType.Pet => ModSettingManager.GetValue("showPetPoi", true),
                CharacterType.Boss => ModSettingManager.GetValue("showBossPoi", true) && willShowInThisMap,
                CharacterType.Enemy => ModSettingManager.GetValue("showEnemyPoi", true) && willShowInThisMap,
                CharacterType.Neutral => ModSettingManager.GetValue("showNeutralPoi", true) && willShowInThisMap,
                _ => false,
            };
        }
    }
}