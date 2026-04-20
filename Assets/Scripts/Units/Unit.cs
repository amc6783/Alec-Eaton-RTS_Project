using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(SelectionOutlineTarget))]
public class Unit : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [Header("Team")]
    [SerializeField] private int defaultTeam;

    private SelectionOutlineTarget _selectionOutlineTarget;
    private readonly SyncVar<int> _currentHealth = new();
    private readonly SyncVar<int> _team = new();

    public int MaxHealth => maxHealth;
    public int CurrentHealth => _currentHealth.Value;
    public int Team => _team.Value;

    private void Awake()
    {
        _selectionOutlineTarget = GetComponent<SelectionOutlineTarget>();
        _currentHealth.Value = maxHealth;
        _currentHealth.UpdateSendRate(0f);
        _team.Value = defaultTeam;
        _team.UpdateSendRate(0f);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        maxHealth = Mathf.Max(1, maxHealth);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResetHealthServer();
        ResetTeamServer();
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

    [Server]
    public void SetHealthServer(int value)
    {
        _currentHealth.Value = Mathf.Clamp(value, 0, maxHealth);
    }

    [Server]
    public void ApplyDamageServer(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetHealthServer(_currentHealth.Value - amount);
    }

    [Server]
    public void HealServer(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetHealthServer(_currentHealth.Value + amount);
    }

    [Server]
    public void ResetHealthServer()
    {
        _currentHealth.Value = maxHealth;
    }

    [Server]
    public void SetTeamServer(int value)
    {
        _team.Value = value;
    }

    [Server]
    public void ResetTeamServer()
    {
        _team.Value = defaultTeam;
    }
}
