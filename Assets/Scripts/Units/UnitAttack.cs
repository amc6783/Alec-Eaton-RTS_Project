using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(UnitMovement))]
public class UnitAttack : NetworkBehaviour
{
    [Header("Attack")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 0.75f;
    [SerializeField] private float chaseRepathInterval = 0.15f;

    private Unit _unit;
    private UnitMovement _movement;
    private Unit _currentTarget;
    private float _nextAttackTime;
    private float _nextChaseMoveTime;

    public Unit CurrentTarget => _currentTarget;

    private void Awake()
    {
        _unit = GetComponent<Unit>();
        _movement = GetComponent<UnitMovement>();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        attackDamage = Mathf.Max(0, attackDamage);
        attackRange = Mathf.Max(0f, attackRange);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        chaseRepathInterval = Mathf.Max(0f, chaseRepathInterval);
    }

    private void Update()
    {
        if (!ShouldSimulateServer())
        {
            return;
        }

        if (_currentTarget == null)
        {
            return;
        }

        if (!IsValidTarget(_currentTarget))
        {
            ClearTargetServer();
            return;
        }

        float distanceToTarget = Vector2.Distance(transform.position, _currentTarget.transform.position);
        if (distanceToTarget > attackRange)
        {
            if (Time.time >= _nextChaseMoveTime)
            {
                _movement.RequestMove(_currentTarget.transform.position);
                _nextChaseMoveTime = Time.time + chaseRepathInterval;
            }

            return;
        }

        _movement.RequestStop();

        if (attackDamage <= 0 || Time.time < _nextAttackTime)
        {
            return;
        }

        _currentTarget.ApplyDamageServer(attackDamage);
        _nextAttackTime = Time.time + attackCooldown;
    }

    [Server]
    public void SetTargetServer(Unit target)
    {
        if (!IsValidTarget(target))
        {
            _currentTarget = null;
            return;
        }

        _currentTarget = target;
    }

    [Server]
    public void ClearTargetServer()
    {
        _currentTarget = null;
    }

    private bool IsValidTarget(Unit target)
    {
        if (target == null || target == _unit)
        {
            return false;
        }

        if (!target.gameObject.activeInHierarchy || !target.IsAlive)
        {
            return false;
        }

        if (_unit != null && target.Team == _unit.Team)
        {
            return false;
        }

        return true;
    }

    private bool ShouldSimulateServer()
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
}
