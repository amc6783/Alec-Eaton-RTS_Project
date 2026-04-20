using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerCommander : NetworkBehaviour
{
    [Header("Team")]
    [SerializeField] private int defaultTeam;

    private readonly SyncVar<int> _team = new();

    public int Team => _team.Value;

    private void Awake()
    {
        _team.Value = defaultTeam;
        _team.UpdateSendRate(0f);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResetTeamServer();
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
