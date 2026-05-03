using FishNet.Connection;
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

    public bool IsLocalPlayerCommander()
    {
        if (NetworkObject == null)
        {
            return true;
        }

        if (!IsClientStarted && !IsServerStarted)
        {
            return true;
        }

        if (!IsClientStarted)
        {
            return false;
        }

        return IsOwner || Owner.IsLocalClient;
    }

    public static bool TryGetLocalPlayerTeam(out int team)
    {
        PlayerCommander[] commanders = FindObjectsOfType<PlayerCommander>();
        for (int i = 0; i < commanders.Length; i++)
        {
            PlayerCommander commander = commanders[i];
            if (commander == null || !commander.IsLocalPlayerCommander())
            {
                continue;
            }

            team = commander.Team;
            return true;
        }

        team = default;
        return false;
    }

    public static bool TryGetTeamForConnection(NetworkConnection connection, out int team)
    {
        if (connection == null || !connection.IsValid)
        {
            team = default;
            return false;
        }

        PlayerCommander[] commanders = FindObjectsOfType<PlayerCommander>();
        for (int i = 0; i < commanders.Length; i++)
        {
            PlayerCommander commander = commanders[i];
            if (commander == null || commander.NetworkObject == null || commander.Owner != connection)
            {
                continue;
            }

            team = commander.Team;
            return true;
        }

        team = default;
        return false;
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
