using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitSelectionManager : MonoBehaviour
{
    private Unit selectedUnit;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SelectUnitAtMouse();
        }
    }

    void SelectUnitAtMouse()
    {
        Vector3 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point2D = new Vector2(worldPoint.x, worldPoint.y);

        RaycastHit2D hit = Physics2D.Raycast(point2D, Vector2.zero);

        if (hit.collider != null)
        {
            //Unit unit = hit.collider.GetComponent<Unit>();
            //if (unit != null)
            //{
            //    if (selectedUnit != null) selectedUnit.Deselect();
            //    selectedUnit = unit;
            //    unit.Select();
            //}
        }
        else
        {
            //if (selectedUnit != null)
            //{
            //    selectedUnit.Deselect();
            //    selectedUnit = null;
            //}
        }
    }
}
