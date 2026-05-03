using System.Collections.Generic;
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
        private const string HealthBarContainerName = "UnitHealthBars";

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

        [Header("Unit Health Bars")]
        [SerializeField] private bool showUnitHealthBars = true;
        [SerializeField] private bool showFullHealthBars = true;
        [SerializeField] private Vector2 healthBarSize = new Vector2(42f, 6f);
        [SerializeField] private float healthBarWorldOffset = 0.35f;
        [SerializeField] private float unitRefreshInterval = 0.25f;
        [SerializeField] private Color healthBarBackColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private Color healthBarFillColor = new Color(0.15f, 0.9f, 0.25f, 0.95f);
        [SerializeField] private Color healthBarLowFillColor = new Color(0.95f, 0.2f, 0.15f, 0.95f);

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
        private RectTransform _canvasRectTransform;
        private RectTransform _healthBarContainer;
        private readonly List<Unit> _trackedUnits = new List<Unit>();
        private readonly Dictionary<Unit, HealthBarView> _healthBars = new Dictionary<Unit, HealthBarView>();
        private float _nextUnitRefreshTime;
#if UNITY_EDITOR
        private bool _validateApplyQueued;
#endif

        void Awake()
        {
            EnsureRectangleExists();
            EnsureHealthBarContainerExists();
            ApplyRectangleSettings();
        }

        private void Update()
        {
            UpdateUnitHealthBars();
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
            healthBarSize.x = Mathf.Max(1f, healthBarSize.x);
            healthBarSize.y = Mathf.Max(1f, healthBarSize.y);
            unitRefreshInterval = Mathf.Max(0.05f, unitRefreshInterval);
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

            _canvasRectTransform = hudCanvas.transform as RectTransform;

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

        private void EnsureHealthBarContainerExists()
        {
            if (hudCanvas == null)
            {
                hudCanvas = FindObjectOfType<Canvas>();
            }

            if (hudCanvas == null)
            {
                return;
            }

            _canvasRectTransform = hudCanvas.transform as RectTransform;
            Transform existing = hudCanvas.transform.Find(HealthBarContainerName);
            if (existing == null)
            {
                var containerObject = new GameObject(HealthBarContainerName, typeof(RectTransform));
                containerObject.transform.SetParent(hudCanvas.transform, false);
                _healthBarContainer = containerObject.GetComponent<RectTransform>();
            }
            else
            {
                _healthBarContainer = existing as RectTransform;
            }

            if (_healthBarContainer == null)
            {
                return;
            }

            _healthBarContainer.anchorMin = Vector2.zero;
            _healthBarContainer.anchorMax = Vector2.one;
            _healthBarContainer.pivot = new Vector2(0.5f, 0.5f);
            _healthBarContainer.offsetMin = Vector2.zero;
            _healthBarContainer.offsetMax = Vector2.zero;
            _healthBarContainer.SetAsLastSibling();
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

        private void UpdateUnitHealthBars()
        {
            if (!showUnitHealthBars)
            {
                HideAllHealthBars();
                return;
            }

            EnsureHealthBarContainerExists();
            if (_healthBarContainer == null || _canvasRectTransform == null)
            {
                return;
            }

            if (Time.unscaledTime >= _nextUnitRefreshTime)
            {
                RefreshTrackedUnits();
                _nextUnitRefreshTime = Time.unscaledTime + unitRefreshInterval;
            }

            Camera worldCamera = Camera.main;
            Camera canvasCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;

            for (int i = 0; i < _trackedUnits.Count; i++)
            {
                Unit unit = _trackedUnits[i];
                if (unit == null)
                {
                    RemoveUnusedHealthBars();
                    continue;
                }

                HealthBarView healthBar = GetOrCreateHealthBar(unit);
                if (healthBar == null)
                {
                    continue;
                }

                float maxHealth = Mathf.Max(1f, unit.MaxHealth);
                float healthPercent = Mathf.Clamp01(unit.CurrentHealth / maxHealth);
                bool shouldShow = unit.IsAlive && (showFullHealthBars || healthPercent < 1f);

                if (!shouldShow || worldCamera == null)
                {
                    healthBar.Root.SetActive(false);
                    continue;
                }

                Vector3 worldPosition = GetHealthBarWorldPosition(unit);
                Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
                if (screenPosition.z < 0f || !ScreenPointToCanvasPosition(screenPosition, canvasCamera, out Vector2 canvasPosition))
                {
                    healthBar.Root.SetActive(false);
                    continue;
                }

                healthBar.Root.SetActive(true);
                healthBar.Rect.anchoredPosition = canvasPosition;
                healthBar.FillRect.anchorMax = new Vector2(healthPercent, 1f);
                healthBar.FillImage.color = Color.Lerp(healthBarLowFillColor, healthBarFillColor, healthPercent);
            }
        }

        private void RefreshTrackedUnits()
        {
            _trackedUnits.Clear();
            Unit[] units = FindObjectsOfType<Unit>();

            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (unit != null)
                {
                    _trackedUnits.Add(unit);
                }
            }

            RemoveUnusedHealthBars();
        }

        private HealthBarView GetOrCreateHealthBar(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (_healthBars.TryGetValue(unit, out HealthBarView existing) && existing != null)
            {
                return existing;
            }

            var root = new GameObject(unit.name + "_HealthBar", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(_healthBarContainer, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = healthBarSize;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            Image backImage = root.GetComponent<Image>();
            backImage.color = healthBarBackColor;
            backImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(root.transform, false);

            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);

            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = healthBarFillColor;
            fillImage.raycastTarget = false;

            var healthBar = new HealthBarView(root, rootRect, fillRect, fillImage);
            _healthBars[unit] = healthBar;
            return healthBar;
        }

        private Vector3 GetHealthBarWorldPosition(Unit unit)
        {
            Renderer renderer = unit.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                return new Vector3(bounds.center.x, bounds.max.y + healthBarWorldOffset, bounds.center.z);
            }

            return unit.transform.position + Vector3.up * healthBarWorldOffset;
        }

        private bool ScreenPointToCanvasPosition(Vector3 screenPosition, Camera canvasCamera, out Vector2 canvasPosition)
        {
            if (_canvasRectTransform == null)
            {
                canvasPosition = Vector2.zero;
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRectTransform,
                screenPosition,
                canvasCamera,
                out canvasPosition
            );
        }

        private void RemoveUnusedHealthBars()
        {
            List<Unit> removedUnits = null;
            foreach (KeyValuePair<Unit, HealthBarView> pair in _healthBars)
            {
                if (pair.Key != null && _trackedUnits.Contains(pair.Key))
                {
                    continue;
                }

                if (removedUnits == null)
                {
                    removedUnits = new List<Unit>();
                }

                removedUnits.Add(pair.Key);

                if (pair.Value != null && pair.Value.Root != null)
                {
                    Destroy(pair.Value.Root);
                }
            }

            if (removedUnits == null)
            {
                return;
            }

            for (int i = 0; i < removedUnits.Count; i++)
            {
                _healthBars.Remove(removedUnits[i]);
            }
        }

        private void HideAllHealthBars()
        {
            foreach (KeyValuePair<Unit, HealthBarView> pair in _healthBars)
            {
                if (pair.Value != null && pair.Value.Root != null)
                {
                    pair.Value.Root.SetActive(false);
                }
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

        private sealed class HealthBarView
        {
            public HealthBarView(GameObject root, RectTransform rect, RectTransform fillRect, Image fillImage)
            {
                Root = root;
                Rect = rect;
                FillRect = fillRect;
                FillImage = fillImage;
            }

            public GameObject Root { get; }
            public RectTransform Rect { get; }
            public RectTransform FillRect { get; }
            public Image FillImage { get; }
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
