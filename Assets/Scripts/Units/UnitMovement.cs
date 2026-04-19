using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody2D))]
public class UnitMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stoppingDistance = 0.08f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float idleVelocityDamping = 12f;
    [SerializeField] private float idleVelocityStopThreshold = 0.05f;

    private Vector2 _destination;
    private bool _hasDestination;
    private float _currentSpeed;
    private Rigidbody2D _rigidbody2D;

    public bool IsMoving => _hasDestination;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        acceleration = Mathf.Max(0f, acceleration);
        idleVelocityDamping = Mathf.Max(0f, idleVelocityDamping);
        idleVelocityStopThreshold = Mathf.Max(0f, idleVelocityStopThreshold);
    }

    private void FixedUpdate()
    {
        if (!ShouldSimulateMovement())
        {
            return;
        }

        SimulateMovement(Time.fixedDeltaTime);
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
            ApplyIdleVelocityDamping(deltaTime);
            return;
        }

        Vector2 currentPosition = _rigidbody2D != null
            ? _rigidbody2D.position
            : (Vector2)transform.position;
        Vector2 toDestination = _destination - currentPosition;
        float distance = toDestination.magnitude;

        if (distance <= stoppingDistance)
        {
            _hasDestination = false;
            _currentSpeed = 0f;
            ApplyIdleVelocityDamping(deltaTime);
            return;
        }

        float targetSpeed = moveSpeed;
        _currentSpeed = (acceleration <= 0f)
            ? targetSpeed
            : Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * deltaTime);

        float step = _currentSpeed * deltaTime;
        Vector2 direction = toDestination / Mathf.Max(0.0001f, distance);
        Vector2 nextPosition = currentPosition + direction * Mathf.Min(step, distance);
        if (_rigidbody2D != null)
        {
            _rigidbody2D.MovePosition(nextPosition);
            return;
        }

        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);
    }

    private void ApplyIdleVelocityDamping(float deltaTime)
    {
        if (_rigidbody2D == null)
        {
            return;
        }

        Vector2 velocity = _rigidbody2D.velocity;
        if (velocity.sqrMagnitude <= idleVelocityStopThreshold * idleVelocityStopThreshold)
        {
            _rigidbody2D.velocity = Vector2.zero;
        }
        else
        {
            _rigidbody2D.velocity = Vector2.MoveTowards(velocity, Vector2.zero, idleVelocityDamping * deltaTime);
        }

        _rigidbody2D.angularVelocity = 0f;
    }
}
