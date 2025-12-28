using Duckov.MiniMaps;
using Duckov.Scenes;
using SodaCraft.Localizations;
using UnityEngine;

namespace MiniMap.Managers
{
    public static class CharacterCommon
    {
        public static bool IsDead(CharacterMainControl? character)
        {
            return !(character != null && character.Health && !character.Health.IsDead);
        }
    }

    public class CharacterPointOfInterest : MonoBehaviour, IPointOfInterest
    {
        private Sprite? icon;
        private CharacterMainControl? character;
        private int characterID;
        private bool localized = true;
        private string? displayName = "";
        private bool followActiveScene;
        private bool isArea;
        private float areaRadius;
        private float scaleFactor = 1f;
        private bool hideIcon;
        private string? overrideSceneID;

        public CharacterMainControl? Character => character;
        public int CharacterID => characterID;
        public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
        public Color ShadowColor => Color.clear;
        public float ShadowDistance => 0f;
        public bool Localized { get => localized; set => localized = value; }
        public string? DisplayName => localized ? displayName.ToPlainText() : displayName;
        public Sprite? Icon => icon;
        public int OverrideScene
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
        public bool IsArea { get => isArea; set => isArea = value; }
        public float AreaRadius { get => areaRadius; set => areaRadius = value; }
        public bool HideIcon { get => hideIcon; set => hideIcon = value; }

        private void OnEnable()
        {
            PointsOfInterests.Register(this);
        }

        private void OnDisable()
        {
            PointsOfInterests.Unregister(this);
        }

        public void Setup(Sprite? icon, CharacterMainControl character, string? displayName = null, bool followActiveScene = false, string? overrideSceneID = null)
        {
            this.character = character;
            this.characterID = character.GetInstanceID();
            this.icon = icon;
            this.displayName = displayName;
            this.followActiveScene = followActiveScene;
            this.overrideSceneID = overrideSceneID;
            PointsOfInterests.Unregister(this);
            PointsOfInterests.Register(this);
        }

        private void Update()
        {
            if (CharacterCommon.IsDead(character))
            {
                Destroy(this.gameObject);
                return;
            }
        }
    }
    public class DirectionPointOfInterest : MonoBehaviour, IPointOfInterest
    {
        private Sprite? icon;
        private float rotationEulerAngle;
        private float baseEulerAngle;
        private string? tagName;
        private CharacterMainControl? character;
        private int characterID;
        private bool followActiveScene;
        private bool isArea;
        private float areaRadius;
        private float scaleFactor = 1f;
        private bool hideIcon;
        private string? overrideSceneID;

        public CharacterMainControl? Character => character;
        public int CharacterID => characterID;

        public float RotationEulerAngle { get => rotationEulerAngle % 360; set => rotationEulerAngle = value % 360; }
        public float BaseEulerAngle { get => baseEulerAngle % 360; set => baseEulerAngle = value % 360; }
        public float RealEulerAngle => (baseEulerAngle + rotationEulerAngle) % 360;
        public string DisplayName => string.Empty;
        public string? TagName => tagName;
        public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
        public Color ShadowColor => Color.clear;
        public float ShadowDistance => 0f;
        public Sprite? Icon => icon;
        public int OverrideScene
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
        public bool IsArea { get => isArea; set => isArea = value; }
        public float AreaRadius { get => areaRadius; set => areaRadius = value; }
        public bool HideIcon { get => hideIcon; set => hideIcon = value; }

        private void OnEnable()
        {
            PointsOfInterests.Register(this);
        }

        private void OnDisable()
        {
            PointsOfInterests.Unregister(this);
        }

        public void Setup(Sprite? icon, CharacterMainControl character, string? tagName = null, bool followActiveScene = false, string? overrideSceneID = null)
        {
            this.icon = icon;
            this.tagName = tagName;
            this.character = character;
            this.characterID = character.GetInstanceID();
            this.followActiveScene = followActiveScene;
            this.overrideSceneID = overrideSceneID;
            PointsOfInterests.Unregister(this);
            PointsOfInterests.Register(this);
        }

        private void Update()
        {
            if (CharacterCommon.IsDead(character))
            {
                Destroy(this.gameObject);
                return;
            }
            if (character!.IsMainCharacter)
            {
                RotationEulerAngle = MiniMapCommon.GetPlayerMinimapRotation().eulerAngles.z;
            }
            else
            {
                RotationEulerAngle = MiniMapCommon.GetPlayerMinimapRotation(character.movementControl.targetAimDirection).eulerAngles.z;
            }
        }
    }

    public class BossCharacterBehaviour : MonoBehaviour
    {
        private void Update()
        {
            if (enabled && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }
    }
}
