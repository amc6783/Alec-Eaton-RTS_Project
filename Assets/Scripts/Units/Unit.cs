using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(UnitMovement))]
public class Unit : NetworkBehaviour
{
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
}
