using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Managers
{
    public class HUDManager : MonoBehaviour
    {
        private const string RectangleName = "CenterHUDRectangle";

        [Header("HUD Canvas")]
        [SerializeField] private Canvas hudCanvas;

        [Header("Drag Selection Rectangle")]
        [SerializeField] private Vector2 lowerLeftCorner = Vector2.zero;
        [SerializeField] private Vector2 upperRightCorner = Vector2.zero;
        [SerializeField] private Color rectangleColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private float outlineThickness = 2f;
        [SerializeField] private bool rectangleVisible = false;
        [SerializeField] private GameObject unitSelectionRectangle;
        [SerializeField] private bool unitSelectionIsDragging = false;

        private RectTransform _rectangleRectTransform;
        private Image _rectangleImage;
        private RectTransform _topBorderRect;
        private RectTransform _bottomBorderRect;
        private RectTransform _leftBorderRect;
        private RectTransform _rightBorderRect;
        private Image _topBorderImage;
        private Image _bottomBorderImage;
        private Image _leftBorderImage;
        private Image _rightBorderImage;
#if UNITY_EDITOR
        private bool _validateApplyQueued;
#endif

        void Awake()
        {
            EnsureRectangleExists();
            ApplyRectangleSettings();
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            if (!_validateApplyQueued) return;

            EditorApplication.delayCall -= ApplyQueuedValidateChanges;
            _validateApplyQueued = false;
        }
#endif

        private void OnValidate()
        {
            outlineThickness = Mathf.Max(0f, outlineThickness);
#if UNITY_EDITOR
            QueueValidateApply();
#endif
        }

        public void BeginDragSelection(Vector2 startScreenPosition)
        {
            unitSelectionIsDragging = true;
            lowerLeftCorner = startScreenPosition;
            upperRightCorner = startScreenPosition;
            rectangleVisible = true;

            EnsureRectangleExists();
            ApplyRectangleSettings();
        }

        public void UpdateDragSelection(Vector2 currentScreenPosition)
        {
            if (!unitSelectionIsDragging) return;

            upperRightCorner = currentScreenPosition;
            ApplyRectangleSettings();
        }

        public void EndDragSelection()
        {
            unitSelectionIsDragging = false;
            rectangleVisible = false;
            ApplyRectangleSettings();
        }

        public Rect GetScreenDragRect()
        {
            Vector2 min = Vector2.Min(lowerLeftCorner, upperRightCorner);
            Vector2 max = Vector2.Max(lowerLeftCorner, upperRightCorner);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void EnsureRectangleExists()
        {
            if (hudCanvas == null)
            {
                hudCanvas = FindObjectOfType<Canvas>();
            }

            if (hudCanvas == null)
            {
                Debug.LogWarning("HUDManager: No Canvas found to render HUD rectangle.");
                return;
            }

            if (unitSelectionRectangle == null)
            {
                Transform existing = hudCanvas.transform.Find(RectangleName);
                if (existing != null)
                {
                    unitSelectionRectangle = existing.gameObject;
                }
                else
                {
                    unitSelectionRectangle = new GameObject(RectangleName, typeof(RectTransform), typeof(Image));
                    unitSelectionRectangle.transform.SetParent(hudCanvas.transform, false);
                }
            }

            EnsureRectangleReferences();
        }

        private void EnsureRectangleReferences()
        {
            if (unitSelectionRectangle == null) return;

            _rectangleRectTransform = unitSelectionRectangle.GetComponent<RectTransform>();
            _rectangleImage = unitSelectionRectangle.GetComponent<Image>();

            if (_rectangleRectTransform == null)
            {
                Debug.LogWarning("HUDManager: unitSelectionRectangle must use RectTransform.");
                return;
            }

            if (_rectangleImage == null)
            {
                _rectangleImage = unitSelectionRectangle.AddComponent<Image>();
            }
            _rectangleImage.raycastTarget = false;

            var existingOutline = unitSelectionRectangle.GetComponent<Outline>();
            if (existingOutline != null)
            {
                existingOutline.enabled = false;
            }

            EnsureBorderParts();
        }

        private void EnsureBorderParts()
        {
            EnsureBorderPart("TopBorder", out _topBorderRect, out _topBorderImage);
            EnsureBorderPart("BottomBorder", out _bottomBorderRect, out _bottomBorderImage);
            EnsureBorderPart("LeftBorder", out _leftBorderRect, out _leftBorderImage);
            EnsureBorderPart("RightBorder", out _rightBorderRect, out _rightBorderImage);
        }

        private void EnsureBorderPart(string name, out RectTransform partRect, out Image partImage)
        {
            Transform partTransform = unitSelectionRectangle.transform.Find(name);
            if (partTransform == null)
            {
                var partObject = new GameObject(name, typeof(RectTransform), typeof(Image));
                partObject.transform.SetParent(unitSelectionRectangle.transform, false);
                partRect = partObject.GetComponent<RectTransform>();
                partImage = partObject.GetComponent<Image>();
                return;
            }

            partRect = partTransform as RectTransform;
            partImage = partTransform.GetComponent<Image>();

            if (partImage == null)
            {
                partImage = partTransform.gameObject.AddComponent<Image>();
            }

            partImage.raycastTarget = false;
        }

        private void ApplyRectangleSettings()
        {
            if (_rectangleRectTransform == null || _rectangleImage == null) return;

            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);

            Vector2 min = Vector2.Min(lowerLeftCorner, upperRightCorner);
            Vector2 max = Vector2.Max(lowerLeftCorner, upperRightCorner);

            Vector2 anchorMin = new Vector2(
                Mathf.Clamp01(min.x / screenWidth),
                Mathf.Clamp01(min.y / screenHeight)
            );
            Vector2 anchorMax = new Vector2(
                Mathf.Clamp01(max.x / screenWidth),
                Mathf.Clamp01(max.y / screenHeight)
            );

            _rectangleRectTransform.anchorMin = anchorMin;
            _rectangleRectTransform.anchorMax = anchorMax;
            _rectangleRectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectangleRectTransform.offsetMin = Vector2.zero;
            _rectangleRectTransform.offsetMax = Vector2.zero;
            _rectangleImage.color = rectangleColor;

            ApplyBorderSettings();

            if (unitSelectionRectangle != null)
            {
                unitSelectionRectangle.SetActive(rectangleVisible);
            }
        }

        private void ApplyBorderSettings()
        {
            if (_topBorderRect == null || _bottomBorderRect == null || _leftBorderRect == null || _rightBorderRect == null) return;
            if (_topBorderImage == null || _bottomBorderImage == null || _leftBorderImage == null || _rightBorderImage == null) return;

            float t = Mathf.Max(0f, outlineThickness);

            _topBorderRect.anchorMin = new Vector2(0f, 1f);
            _topBorderRect.anchorMax = new Vector2(1f, 1f);
            _topBorderRect.pivot = new Vector2(0.5f, 1f);
            _topBorderRect.offsetMin = new Vector2(0f, -t);
            _topBorderRect.offsetMax = Vector2.zero;

            _bottomBorderRect.anchorMin = new Vector2(0f, 0f);
            _bottomBorderRect.anchorMax = new Vector2(1f, 0f);
            _bottomBorderRect.pivot = new Vector2(0.5f, 0f);
            _bottomBorderRect.offsetMin = Vector2.zero;
            _bottomBorderRect.offsetMax = new Vector2(0f, t);

            _leftBorderRect.anchorMin = new Vector2(0f, 0f);
            _leftBorderRect.anchorMax = new Vector2(0f, 1f);
            _leftBorderRect.pivot = new Vector2(0f, 0.5f);
            _leftBorderRect.offsetMin = new Vector2(0f, t);
            _leftBorderRect.offsetMax = new Vector2(t, -t);

            _rightBorderRect.anchorMin = new Vector2(1f, 0f);
            _rightBorderRect.anchorMax = new Vector2(1f, 1f);
            _rightBorderRect.pivot = new Vector2(1f, 0.5f);
            _rightBorderRect.offsetMin = new Vector2(-t, t);
            _rightBorderRect.offsetMax = new Vector2(0f, -t);

            _topBorderImage.color = outlineColor;
            _bottomBorderImage.color = outlineColor;
            _leftBorderImage.color = outlineColor;
            _rightBorderImage.color = outlineColor;
        }

#if UNITY_EDITOR
        private void QueueValidateApply()
        {
            if (_validateApplyQueued) return;

            _validateApplyQueued = true;
            EditorApplication.delayCall += ApplyQueuedValidateChanges;
        }

        private void ApplyQueuedValidateChanges()
        {
            _validateApplyQueued = false;

            if (this == null) return;

            EnsureRectangleExists();
            ApplyRectangleSettings();
        }
#endif
    }
}
