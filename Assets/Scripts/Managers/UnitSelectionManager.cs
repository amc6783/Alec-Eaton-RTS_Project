using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UnitSelectionManager : MonoBehaviour
{
    // Stores selected units. Keeping ArrayList as per existing codebase, though List<Unit> is preferred.
    [SerializeField] private List<Unit> selectedUnits;

    private bool _isDragging = false;
    private Vector2 _startDragPosition; // in screen space
    private Camera _mainCamera;

    // Pixels mouse must travel while held before we consider it a drag selection
    [SerializeField] private float dragThreshold = 4f;

    void Awake()
    {
        _mainCamera = Camera.main;
    }
    
    void Update()
    {
        // Mouse down: record start position
        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = false; // reset; we don't know it's a drag yet
            _startDragPosition = Input.mousePosition;
            StartSelection();
        }

        // While holding: decide if it's a drag based on distance
        if (Input.GetMouseButton(0))
        {
            if (!_isDragging)
            {
                if (Vector2.Distance(Input.mousePosition, _startDragPosition) > dragThreshold)
                {
                    _isDragging = true;
                }
            }

            // TODO: If you add a selection rectangle visual, update it here using _startDragPosition and Input.mousePosition.
        }
        
        // Mouse up: finish either drag selection or single click
        if (Input.GetMouseButtonUp(0))
        {
            if (_isDragging)
            {
                // Finish drag selection
                SelectUnitsInDragArea(_startDragPosition, Input.mousePosition);
                _isDragging = false;
            }
            else
            {
                // Single click selection
                SelectUnitAtMouse();
            }
        }
    }

    void StartSelection()
    {
        // Reserved for starting selection visuals if needed later
        // e.g., enabling a UI selection rectangle
    }
    
    void SelectUnitAtMouse()
    {
        selectedUnits.Clear();

        Vector3 worldPoint = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point2D = new Vector2(worldPoint.x, worldPoint.y);

        RaycastHit2D hit = Physics2D.Raycast(point2D, Vector2.zero);
        
        if (hit.collider.IsUnityNull()) return;
        
        if (hit.collider.TryGetComponent<Unit>(out var unit) && !unit.IsUnityNull())
        {
            selectedUnits.Add(unit);
        }
    }

    void SelectUnitsInDragArea(Vector2 screenStart, Vector2 screenEnd)
    {
        selectedUnits.Clear();

        // Convert screen corners to world space (2D)
        Vector3 w0 = _mainCamera.ScreenToWorldPoint(screenStart);
        Vector3 w1 = _mainCamera.ScreenToWorldPoint(screenEnd);

        Vector2 bottomLeft = new Vector2(Mathf.Min(w0.x, w1.x), Mathf.Min(w0.y, w1.y));
        Vector2 topRight   = new Vector2(Mathf.Max(w0.x, w1.x), Mathf.Max(w0.y, w1.y));

        // Query all colliders in the area
        Collider2D[] hits = Physics2D.OverlapAreaAll(bottomLeft, topRight);
        if (hits == null || hits.Length == 0) return;

        foreach (var col in hits)
        {
            if (col == null || col.IsUnityNull()) continue;
            if (col.TryGetComponent<Unit>(out var unit) && !unit.IsUnityNull())
            {
                if (!selectedUnits.Contains(unit))
                {
                    selectedUnits.Add(unit);
                }
            }
        }
    }
}
