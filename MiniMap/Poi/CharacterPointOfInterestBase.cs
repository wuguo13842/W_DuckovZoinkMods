using Duckov.MiniMaps;
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
        private bool isMain = false;
        private bool isPet = false;
        private bool showPet = true;

        private CharacterMainControl? character;
        private CharacterType characterType;
        private string? cachedName;
        private bool showInMap;
        private bool showInMiniMap;
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
            set
            {
                if (showOnlyActivated != value)
                {
                    showOnlyActivated = value;
                    if (value && !gameObject.activeSelf)
                    {
                        Unregister();
                    }
                    else
                    {
                        Register();
                    }
                }
            }
        }
        public virtual bool ShowInMap
        {
            get => showInMap;
            set
            {
                if (showInMap != value)
                {
                    showInMap = value || isMain || (isPet && showPet);
                }
            }
        }
        public virtual bool ShowInMiniMap
        {
            get => showInMiniMap;
            set
            {
                if (value != showInMiniMap)
                {
                    ModBehaviour.Instance?.ExecuteWithDebounce(() =>
                        {
                            showInMiniMap = value || isMain || (isPet && showPet);
                        }, () =>
                        {
                            CustomMinimapManager.CallDisplayMethod("HandlePointsOfInterests");
                        });
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

        public virtual void Setup(Sprite? icon, CharacterMainControl character, CharacterType characterType, PoiShows? poiShows, string? cachedName = null, bool followActiveScene = false, string? overrideSceneID = null)
        {
            if (initialized) return;
            this.character = character;
            this.characterType = characterType;
            this.icon = icon;
            this.cachedName = cachedName;
            this.followActiveScene = followActiveScene;
            this.overrideSceneID = overrideSceneID;
            isMain = characterType == CharacterType.Main;
            isPet = characterType == CharacterType.Pet;
            ModSettingManager.ConfigChanged += OnConfigChanged;
            SetShows(poiShows);
            initialized = true;
        }

        public virtual void Setup(SimplePointOfInterest poi, CharacterMainControl character, CharacterType characterType, PoiShows? poiShows, bool followActiveScene = false, string? overrideSceneID = null)
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
            this.shadowColor = poi.ShadowColor;
            this.shadowDistance = poi.ShadowDistance;
            isMain = characterType == CharacterType.Main;
            isPet = characterType == CharacterType.Pet;
            ModSettingManager.ConfigChanged += OnConfigChanged;
            SetShows(poiShows);
            initialized = true;
        }

        private void OnConfigChanged(string key, object? value)
        {
            if (value == null) return;
            switch (key)
            {
                case "showOnlyActivated":
                    ShowOnlyActivated = (bool)value;
                    break;
                case "showPoiInMap":
                    ShowInMap = (bool)value;
                    break;
                case "showPoiInMiniMap":
                    ShowInMiniMap = (bool)value;
                    break;
                case "showPetPoi":
                    showPet = (bool)value;
                    if (isPet)
                    {
                        ShowInMap = ShowInMiniMap = showPet;
                    }
                    break;
            }
        }

        protected virtual void Update()
        {
            if (character != null && !isMain && PoiCommon.IsDead(character))
            {
                Destroy(this.gameObject);
                return;
            }
            //if (isMain)
            //{
            //    ShowInMap = ShowInMiniMap = true;
            //}

        }

        public virtual void Register(bool force = false)
        {
            if (force)
            {
                PointsOfInterests.Unregister(this);
            }
            if (!PointsOfInterests.Points.Contains(this))
            {
                ModBehaviour.Instance?.ExecuteWithDebounce(() =>
                {
                    PointsOfInterests.Register(this);
                }, () =>
                {
                    //ModBehaviour.Logger.Log($"Handling Points Of Interests");
                    //CustomMinimapManager.CallDisplayMethod("HandlePointsOfInterests");
                });
            }
        }

        public virtual void Unregister()
        {
            ModBehaviour.Instance?.ExecuteWithDebounce(() =>
                {
                    PointsOfInterests.Unregister(this);
                }, () =>
                {
                    //ModBehaviour.Logger.Log($"Handling Points Of Interests");
                    //CustomMinimapManager.CallDisplayMethod("HandlePointsOfInterests");
                });
        }

        public virtual void SetShows(PoiShows? poiShows)
        {
            bool showOnlyActivated = poiShows?.ShowOnlyActivated ?? ModSettingManager.GetValue("showOnlyActivated", false);
            bool showPetPoi = poiShows?.ShowPetPoi ?? ModSettingManager.GetValue("showPetPoi", true);
            bool showInMap = poiShows?.ShowInMap ?? ModSettingManager.GetValue("showPoiInMap", true);
            bool showInMiniMap = poiShows?.ShowInMiniMap ?? ModSettingManager.GetValue("showPoiInMiniMap", true);
            ShowOnlyActivated = showOnlyActivated;
            showPet = showPetPoi;
            if (isPet)
            {
                ShowInMap = ShowInMiniMap = showPetPoi;
            }
            else
            {
                ShowInMap = showInMap || isMain;
                ShowInMiniMap = showInMiniMap || isMain;
            }
            if (ShowOnlyActivated && !(Character?.gameObject.activeSelf ?? false))
            {
                Unregister();
            }
            else
            {
                Register();
            }
        }

        protected void OnDestroy()
        {
            ModSettingManager.ConfigChanged -= OnConfigChanged;
        }
    }
}
