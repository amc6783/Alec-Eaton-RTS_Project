using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(SelectionOutlineTarget))]
public class Unit : NetworkBehaviour
{
    private SelectionOutlineTarget _selectionOutlineTarget;

    private void Awake()
    {
        _selectionOutlineTarget = GetComponent<SelectionOutlineTarget>();
    }

    public bool IsSelectableByLocalClient()
    {
        if (NetworkObject == null)
        {
            return true;
        }

        // If networking is not running, keep editor/local behavior.
        if (!IsClientStarted)
        {
            return true;
        }

        // Allow local player to select owned units, plus unowned/server-owned units.
        return IsOwner || !Owner.IsValid;
    }

    public void SetSelectionVisual(bool selected)
    {
        if (_selectionOutlineTarget == null)
        {
            _selectionOutlineTarget = GetComponent<SelectionOutlineTarget>();
        }

        _selectionOutlineTarget?.SetSelected(selected);
    }
}
