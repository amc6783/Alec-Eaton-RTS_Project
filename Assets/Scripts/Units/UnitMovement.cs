using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class UnitMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stoppingDistance = 0.08f;
    [SerializeField] private float acceleration = 20f;

    private Vector2 _destination;
    private bool _hasDestination;
    private float _currentSpeed;

    public bool IsMoving => _hasDestination;

    protected override void OnValidate()
    {
        base.OnValidate();

        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        acceleration = Mathf.Max(0f, acceleration);
    }

    private void Update()
    {
        if (!ShouldSimulateMovement())
        {
            return;
        }

        SimulateMovement(Time.deltaTime);
    }

    public bool CanReceiveLocalCommands()
    {
        if (NetworkObject == null)
        {
            return true;
        }

        if (!IsClientStarted && !IsServerStarted)
        {
            return true;
        }

        if (IsServerInitialized)
        {
            return true;
        }

        return IsOwner;
    }

    public void RequestMove(Vector2 worldDestination)
    {
        if (NetworkObject == null)
        {
            SetDestinationServer(worldDestination);
            return;
        }

        if (!IsClientStarted && !IsServerStarted)
        {
            SetDestinationServer(worldDestination);
            return;
        }

        if (IsServerInitialized)
        {
            SetDestinationServer(worldDestination);
            return;
        }

        RequestMoveServerRpc(worldDestination);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMoveServerRpc(Vector2 worldDestination, NetworkConnection sender = null)
    {
        if (Owner.IsValid && sender != Owner)
        {
            return;
        }

        SetDestinationServer(worldDestination);
    }

    private void SetDestinationServer(Vector2 worldDestination)
    {
        _destination = worldDestination;
        _hasDestination = true;
    }

    private bool ShouldSimulateMovement()
    {
        if (NetworkObject == null)
        {
            return true;
        }

        if (!IsClientStarted && !IsServerStarted)
        {
            return true;
        }

        return IsServerInitialized;
    }

    private void SimulateMovement(float deltaTime)
    {
        if (!_hasDestination)
        {
            _currentSpeed = 0f;
            return;
        }

        Vector3 currentPosition3 = transform.position;
        Vector2 currentPosition = new Vector2(currentPosition3.x, currentPosition3.y);
        Vector2 toDestination = _destination - currentPosition;
        float distance = toDestination.magnitude;

        if (distance <= stoppingDistance)
        {
            _hasDestination = false;
            _currentSpeed = 0f;
            return;
        }

        float targetSpeed = moveSpeed;
        _currentSpeed = (acceleration <= 0f)
            ? targetSpeed
            : Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * deltaTime);

        float step = _currentSpeed * deltaTime;
        Vector2 direction = toDestination / Mathf.Max(0.0001f, distance);
        Vector2 nextPosition = currentPosition + direction * Mathf.Min(step, distance);

        transform.position = new Vector3(nextPosition.x, nextPosition.y, currentPosition3.z);
    }
}
