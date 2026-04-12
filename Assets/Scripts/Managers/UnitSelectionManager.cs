using System.Collections.Generic;
using Managers;
using UnityEngine;

public class UnitSelectionManager : MonoBehaviour
{
    [SerializeField] private List<Unit> selectedUnits;
    [SerializeField] private float dragThreshold = 4f;

    private HUDManager hudManager;
    private Camera _mainCamera;
    private bool _isDragging;
    private Vector2 _dragStartScreenPos;

    void Awake()
    {
        _mainCamera = Camera.main;

        if (selectedUnits == null)
        {
            selectedUnits = new List<Unit>();
        }

        hudManager = FindObjectOfType<HUDManager>();
        if (hudManager == null)
        {
            Debug.LogError("HUDManager not found");
        }
    }
    
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = false;
            _dragStartScreenPos = Input.mousePosition;
        }

        if (Input.GetMouseButton(0))
        {
            if (!_isDragging && Vector2.Distance(Input.mousePosition, _dragStartScreenPos) > dragThreshold)
            {
                _isDragging = true;
                if (hudManager != null)
                {
                    hudManager.BeginDragSelection(_dragStartScreenPos);
                }
            }

            if (_isDragging && hudManager != null)
            {
                hudManager.UpdateDragSelection(Input.mousePosition);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (_isDragging)
            {
                if (hudManager != null)
                {
                    hudManager.UpdateDragSelection(Input.mousePosition);
                    SelectUnitsInDragArea(hudManager.GetScreenDragRect());
                    hudManager.EndDragSelection();
                }
                else
                {
                    SelectUnitsInDragArea(GetScreenRect(_dragStartScreenPos, Input.mousePosition));
                }
            }
            else
            {
                SelectUnitAtMouse();
            }

            _isDragging = false;
        }
    }

    private void SelectUnitAtMouse()
    {
        selectedUnits.Clear();

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector3 worldPoint = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point2D = new Vector2(worldPoint.x, worldPoint.y);

        RaycastHit2D hit = Physics2D.Raycast(point2D, Vector2.zero);
        if (hit.collider == null) return;

        if (hit.collider.TryGetComponent<Unit>(out var unit) && unit != null)
        {
            selectedUnits.Add(unit);
        }
    }

    private void SelectUnitsInDragArea(Rect screenRect)
    {
        selectedUnits.Clear();

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector3 worldBottomLeft = _mainCamera.ScreenToWorldPoint(new Vector3(screenRect.xMin, screenRect.yMin, 0f));
        Vector3 worldTopRight = _mainCamera.ScreenToWorldPoint(new Vector3(screenRect.xMax, screenRect.yMax, 0f));

        Vector2 bottomLeft = new Vector2(Mathf.Min(worldBottomLeft.x, worldTopRight.x), Mathf.Min(worldBottomLeft.y, worldTopRight.y));
        Vector2 topRight = new Vector2(Mathf.Max(worldBottomLeft.x, worldTopRight.x), Mathf.Max(worldBottomLeft.y, worldTopRight.y));

        Collider2D[] hits = Physics2D.OverlapAreaAll(bottomLeft, topRight);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            if (col.TryGetComponent<Unit>(out var unit) && unit != null && !selectedUnits.Contains(unit))
            {
                selectedUnits.Add(unit);
            }
        }
    }

    private static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 min = Vector2.Min(start, end);
        Vector2 max = Vector2.Max(start, end);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
