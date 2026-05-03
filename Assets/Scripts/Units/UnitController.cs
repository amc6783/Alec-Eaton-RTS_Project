using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(UnitAttack))]
public class UnitController : NetworkBehaviour
{
    private enum UnitActionType
    {
        Move,
        Attack
    }

    private struct UnitActionCommand
    {
        public UnitActionType Type;
        public Vector2 WorldDestination;
        public Unit Target;

        public static UnitActionCommand Move(Vector2 worldDestination)
        {
            return new UnitActionCommand
            {
                Type = UnitActionType.Move,
                WorldDestination = worldDestination
            };
        }

        public static UnitActionCommand Attack(Unit target)
        {
            return new UnitActionCommand
            {
                Type = UnitActionType.Attack,
                Target = target
            };
        }
    }

    private Unit _unit;
    private UnitMovement _movement;
    private UnitAttack _attack;
    private readonly Queue<UnitActionCommand> _actionQueue = new Queue<UnitActionCommand>();
    private UnitActionCommand _activeAction;
    private bool _hasActiveAction;

    public int QueuedActionCount => _actionQueue.Count;
    public bool HasActiveAction => _hasActiveAction;

    private void Awake()
    {
        _unit = GetComponent<Unit>();
        _movement = GetComponent<UnitMovement>();
        _attack = GetComponent<UnitAttack>();
    }

    private void Update()
    {
        if (!ShouldProcessActions())
        {
            return;
        }

        ProcessActionQueueServer();
    }

    public bool CanReceiveLocalCommands()
    {
        if (_movement == null)
        {
            return false;
        }

        return _movement.CanReceiveLocalCommands();
    }

    public void RequestMoveCommand(Vector2 worldDestination)
    {
        RequestMoveCommand(worldDestination, false);
    }

    public void RequestMoveCommand(Vector2 worldDestination, bool queueAction)
    {
        if (!CanReceiveLocalCommands())
        {
            return;
        }

        if (NetworkObject == null)
        {
            SetMoveCommandServer(worldDestination, queueAction);
            return;
        }

        if (!IsClientStarted && !IsServerStarted)
        {
            SetMoveCommandServer(worldDestination, queueAction);
            return;
        }

        if (IsServerInitialized)
        {
            SetMoveCommandServer(worldDestination, queueAction);
            return;
        }

        RequestMoveCommandServerRpc(worldDestination, queueAction);
    }

    public void RequestAttackCommand(Unit target)
    {
        RequestAttackCommand(target, false);
    }

    public void RequestAttackCommand(Unit target, bool queueAction)
    {
        if (!CanReceiveLocalCommands())
        {
            return;
        }

        NetworkObject targetNetworkObject = target != null ? target.NetworkObject : null;

        if (NetworkObject == null)
        {
            SetAttackCommandServer(target, queueAction);
            return;
        }

        if (!IsClientStarted && !IsServerStarted)
        {
            SetAttackCommandServer(target, queueAction);
            return;
        }

        if (IsServerInitialized)
        {
            SetAttackCommandServer(target, queueAction);
            return;
        }

        RequestAttackCommandServerRpc(targetNetworkObject, queueAction);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMoveCommandServerRpc(Vector2 worldDestination, bool queueAction, NetworkConnection sender = null)
    {
        if (!IsCommandAuthorized(sender))
        {
            return;
        }

        SetMoveCommandServer(worldDestination, queueAction);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestAttackCommandServerRpc(NetworkObject targetNetworkObject, bool queueAction, NetworkConnection sender = null)
    {
        if (!IsCommandAuthorized(sender))
        {
            return;
        }

        Unit target = targetNetworkObject != null ? targetNetworkObject.GetComponent<Unit>() : null;
        SetAttackCommandServer(target, queueAction);
    }

    [Server]
    private void SetMoveCommandServer(Vector2 worldDestination, bool queueAction)
    {
        SubmitActionCommandServer(UnitActionCommand.Move(worldDestination), queueAction);
    }

    [Server]
    private void SetAttackCommandServer(Unit target, bool queueAction)
    {
        SubmitActionCommandServer(UnitActionCommand.Attack(target), queueAction);
    }

    [Server]
    private void SubmitActionCommandServer(UnitActionCommand command, bool queueAction)
    {
        if (!queueAction)
        {
            ClearActionQueueServer();
        }

        _actionQueue.Enqueue(command);
        ProcessActionQueueServer();
    }

    [Server]
    private void ClearActionQueueServer()
    {
        _actionQueue.Clear();
        _hasActiveAction = false;
    }

    [Server]
    private void ProcessActionQueueServer()
    {
        if (_hasActiveAction && IsActiveActionCompleteServer())
        {
            _hasActiveAction = false;
        }

        while (!_hasActiveAction && _actionQueue.Count > 0)
        {
            UnitActionCommand nextAction = _actionQueue.Dequeue();
            StartActionServer(nextAction);
        }
    }

    [Server]
    private void StartActionServer(UnitActionCommand action)
    {
        switch (action.Type)
        {
            case UnitActionType.Move:
                _activeAction = action;
                _hasActiveAction = true;
                _attack?.ClearTargetServer();
                _movement?.RequestMove(action.WorldDestination);
                break;
            case UnitActionType.Attack:
                if (!IsValidAttackTarget(action.Target))
                {
                    _attack?.ClearTargetServer();
                    break;
                }

                _activeAction = action;
                _hasActiveAction = true;
                _attack?.SetTargetServer(action.Target);
                break;
        }
    }

    private bool IsActiveActionCompleteServer()
    {
        switch (_activeAction.Type)
        {
            case UnitActionType.Move:
                return _movement == null || !_movement.IsMoving;
            case UnitActionType.Attack:
                return _attack == null || _attack.CurrentTarget == null || !IsValidAttackTarget(_attack.CurrentTarget);
            default:
                return true;
        }
    }

    private bool IsValidAttackTarget(Unit target)
    {
        if (_attack == null || _unit == null)
        {
            return false;
        }

        if (target == null || target == _unit)
        {
            return false;
        }

        if (!target.gameObject.activeInHierarchy || !target.IsAlive)
        {
            return false;
        }

        return target.Team != _unit.Team;
    }

    private bool ShouldProcessActions()
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

    private bool IsCommandAuthorized(NetworkConnection sender)
    {
        if (_unit == null)
        {
            _unit = GetComponent<Unit>();
        }

        if (_unit == null || !PlayerCommander.TryGetTeamForConnection(sender, out int senderTeam))
        {
            return false;
        }

        return _unit.Team == senderTeam;
    }
}
