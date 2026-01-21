using LeTai.TrueShadow;
using MiniMap.Poi;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace Duckov.MiniMaps.UI
{
    public class CharacterPoiEntry : MonoBehaviour
    {
        private RectTransform? rectTransform;

        private MiniMapDisplay? master;

        private MonoBehaviour? target;

        private CharacterPoiBase? characterPoi;

        private MiniMapDisplayEntry? minimapEntry;

        [SerializeField]
        private Transform? indicatorContainer;

        [SerializeField]
        private Transform? iconContainer;

        [SerializeField]
        private Sprite? defaultIcon;

        [SerializeField]
        private Color defaultColor = Color.white;

        [SerializeField]
        private Image? icon;

        [SerializeField]
        private Transform? direction;

        [SerializeField]
        private Image? arrow;

        [SerializeField]
        private TrueShadow? shadow;

        [SerializeField]
        private TextMeshProUGUI? displayName;

        [SerializeField]
        private ProceduralImage? areaDisplay;

        [SerializeField]
        private Image? areaFill;

        [SerializeField]
        private float areaLineThickness = 1f;

        [SerializeField]
        private string? caption;

        private Vector3 cachedWorldPosition = Vector3.zero;

        public MonoBehaviour? Target => target;

        private float ParentLocalScale => transform.parent.localScale.x;

        public void Initialize(CharacterPoiEntryData entryData)
        {
            areaDisplay = entryData.areaDisplay;
            areaFill = entryData.areaFill;
            indicatorContainer = entryData.indicatorContainer;
            iconContainer = entryData.iconContainer;
            icon = entryData.icon;
            shadow = entryData.shadow;
            direction = entryData.direction;
            arrow = entryData.arrow;
            displayName = entryData.displayName;
        }

        internal void Setup(MiniMapDisplay master, MonoBehaviour target, MiniMapDisplayEntry minimapEntry)
        {
            rectTransform = transform as RectTransform;
            this.master = master;
            this.target = target;
            this.minimapEntry = minimapEntry;
            this.characterPoi = null;
            icon.sprite = defaultIcon;
            icon.color = defaultColor;
            areaDisplay.color = defaultColor;
            Color color = defaultColor;
            color.a *= 0.1f;
            areaFill.color = color;
            caption = target.name;
            icon.gameObject.SetActive(value: true);
            if (target is CharacterPoiBase characterPoi)
            {
                this.characterPoi = characterPoi;
                icon.gameObject.SetActive(!this.characterPoi.HideIcon);
                icon.sprite = ((characterPoi.Icon != null) ? characterPoi.Icon : defaultIcon);
                icon.color = characterPoi.Color;
                if ((bool)shadow)
                {
                    shadow.Color = characterPoi.ShadowColor;
                    shadow.OffsetDistance = characterPoi.ShadowDistance;
                }

                string value = this.characterPoi.DisplayName;
                caption = characterPoi.DisplayName;
                if (string.IsNullOrEmpty(value))
                {
                    displayName.gameObject.SetActive(value: false);
                }
                else
                {
                    displayName.gameObject.SetActive(value: true);
                    displayName.text = this.characterPoi.DisplayName;
                }

                if (characterPoi.IsArea)
                {
                    areaDisplay.gameObject.SetActive(value: true);
                    rectTransform.sizeDelta = this.characterPoi.AreaRadius * Vector2.one * 2f;
                    areaDisplay.color = characterPoi.Color;
                    color = characterPoi.Color;
                    color.a *= 0.1f;
                    areaFill.color = color;
                    areaDisplay.BorderWidth = areaLineThickness / ParentLocalScale;
                }
                else
                {
                    icon.enabled = true;
                    areaDisplay.gameObject.SetActive(value: false);
                }

                RefreshPosition();
                base.gameObject.SetActive(value: true);
            }
        }

        private void RefreshPosition()
        {
            try
            {
                cachedWorldPosition = target.transform.position;
                Vector3 centerOfObjectScene = (Vector3)typeof(MiniMapCenter).GetMethod("GetCenterOfObjectScene", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { target });
                Vector3 vector = target.transform.position - centerOfObjectScene;
                Vector3 point = new Vector2(vector.x, vector.z);
                Vector3 position = minimapEntry.transform.localToWorldMatrix.MultiplyPoint(point);
                base.transform.position = position;
                UpdateScale();
                UpdateRotation();
            }
            catch { }
        }

        private void Update()
        {
            UpdateScale();
            UpdatePosition();
            UpdateRotation();
        }

        private void UpdateScale()
        {
            float num = ((characterPoi != null) ? characterPoi.ScaleFactor : 1f);
            iconContainer.localScale = Vector3.one * num / ParentLocalScale;
            if (characterPoi != null && characterPoi.IsArea)
            {
                areaDisplay.BorderWidth = areaLineThickness / ParentLocalScale;
                areaDisplay.FalloffDistance = 1f / ParentLocalScale;
            }
        }

        private void UpdatePosition()
        {
            if (cachedWorldPosition != target.transform.position)
            {
                RefreshPosition();
            }
        }

        private void UpdateRotation()
        {
            base.transform.rotation = Quaternion.identity;
        }
    }
}