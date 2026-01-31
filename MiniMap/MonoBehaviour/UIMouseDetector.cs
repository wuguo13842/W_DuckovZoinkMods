using Unity.VisualScripting;
using UnityEngine;
using ZoinkModdingLibrary.ModSettings;

namespace MiniMap.MonoBehaviours
{
    public class UIMouseDetector : MonoBehaviour
    {
        private Canvas? canvas;
        private CanvasGroup? canvasGroup;
        private RectTransform? rectTransform;
        private bool isMouseOver = false;

        void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetCanvasGroup();
        }

        void Update()
        {
            CheckMouseOverWithRect();
        }

        void CheckMouseOverWithRect()
        {
            if (rectTransform == null || canvas == null)
            {
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                Input.mousePosition,
                canvas.worldCamera,
                out Vector2 mousePos
            );

            bool currentlyOver = rectTransform.rect.Contains(mousePos);

            if (currentlyOver && !isMouseOver)
            {
                OnPointerEnter();
            }
            else if (!currentlyOver && isMouseOver)
            {
                OnPointerExit();
            }

            isMouseOver = currentlyOver;
        }

        private CanvasGroup? GetCanvasGroup()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return null;
            }
            CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = canvas.AddComponent<CanvasGroup>();
            }
            return canvasGroup;
        }

        public void OnPointerEnter()
        {
            if (canvasGroup == null)
            {
                return;
            }
            float alpha = ModSettingManager.GetValue(ModBehaviour.ModInfo, "alphaOnMouseEnter", 1f);
            canvasGroup.alpha = alpha;
        }

        public void OnPointerExit()
        {
            if (canvasGroup == null)
            {
                return;
            }
            canvasGroup.alpha = 1f;
        }
    }
}
