using System.Collections.Generic;
using Managers;
using UnityEngine;

public class UnitSelectionManager : MonoBehaviour
{
    [SerializeField] private List<Unit> selectedUnits;
    [SerializeField] private float dragThreshold = 4f;
    [SerializeField] private float commandFormationSpacing = 0.6f;

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
        if (UnitSpawnDebugUI.IsBlockingWorldInput())
        {
            if (_isDragging)
            {
                _isDragging = false;
                if (hudManager != null)
                {
                    hudManager.EndDragSelection();
                }
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = false;
            _dragStartScreenPos = Input.mousePosition;
        }

        if (Input.GetMouseButtonDown(1))
        {
            IssueMoveCommandAtMouse();
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
        ClearCurrentSelection();

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
            TrySelectUnit(unit);
        }
    }

    private void SelectUnitsInDragArea(Rect screenRect)
    {
        ClearCurrentSelection();

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
                TrySelectUnit(unit);
            }
        }
    }

    private void TrySelectUnit(Unit unit)
    {
        if (unit == null) return;
        if (!unit.IsSelectableByLocalClient()) return;
        if (selectedUnits.Contains(unit)) return;

        selectedUnits.Add(unit);
        unit.SetSelectionVisual(true);
    }

    private void ClearCurrentSelection()
    {
        if (selectedUnits == null)
        {
            selectedUnits = new List<Unit>();
            return;
        }

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            if (unit == null) continue;
            unit.SetSelectionVisual(false);
        }

        selectedUnits.Clear();
    }

    private void OnDisable()
    {
        ClearCurrentSelection();
    }

    private static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 min = Vector2.Min(start, end);
        Vector2 max = Vector2.Max(start, end);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void IssueMoveCommandAtMouse()
    {
        if (selectedUnits == null || selectedUnits.Count == 0)
        {
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector3 worldPoint = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 destination = new Vector2(worldPoint.x, worldPoint.y);

        List<UnitMovement> commandableUnits = new List<UnitMovement>(selectedUnits.Count);

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            if (unit == null) continue;
            if (!unit.TryGetComponent<UnitMovement>(out var movement) || movement == null) continue;
            if (!movement.CanReceiveLocalCommands()) continue;

            commandableUnits.Add(movement);
        }

        if (commandableUnits.Count == 0)
        {
            return;
        }

        int columns = Mathf.CeilToInt(Mathf.Sqrt(commandableUnits.Count));
        int rows = Mathf.CeilToInt(commandableUnits.Count / (float)columns);
        Vector2 formationOrigin = destination - new Vector2((columns - 1) * commandFormationSpacing * 0.5f,
            (rows - 1) * commandFormationSpacing * 0.5f);

        for (int i = 0; i < commandableUnits.Count; i++)
        {
            int row = i / columns;
            int col = i % columns;
            Vector2 offset = new Vector2(col * commandFormationSpacing, row * commandFormationSpacing);
            commandableUnits[i].RequestMove(formationOrigin + offset);
        }
    }
}
