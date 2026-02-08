using Duckov.Scenes;
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
				// 直接使用缓存的场景中心点 替代反射调用
                // Vector3 centerOfObjectScene = (Vector3)typeof(MiniMapCenter).GetMethod("GetCenterOfObjectScene", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { target });
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
			if (target == null || !target.gameObject.activeSelf) return;
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
		
		// ==================== 新增的 事件获取场景地图中心点 方法 ====================
		
        // 添加静态字段存储当前场景中心点
        private static Vector3 centerOfObjectScene = Vector3.zero;

        /// <summary>
        /// 设置当前场景中心点（在onFinishedLoadingScene事件中调用）
		/// 从MiniMapSettings获取场景中心点（使用公共属性）
        /// </summary>
        public static void GetSceneCenterFromSettings(string sceneID)
        {
			var settings = MiniMapSettings.Instance;
            if (settings == null)
			{
				centerOfObjectScene = Vector3.zero;
				return;
			}
            
            // 直接访问public的maps列表
            foreach (var mapEntry in settings.maps)
            {
                // 直接访问public的sceneID和mapWorldCenter字段
                if (mapEntry.sceneID == sceneID)
                {
					centerOfObjectScene =  mapEntry.mapWorldCenter;
                    return;
                }
            }
            
            // 没找到则使用合并中心点（也是public的）
			centerOfObjectScene = settings.combinedCenter;
        }
    }
}