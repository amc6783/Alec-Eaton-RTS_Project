using System.Collections;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(UnitAttack))]
[RequireComponent(typeof(UnitController))]
[RequireComponent(typeof(SelectionOutlineTarget))]
public class Unit : NetworkBehaviour
{
    private static readonly int FadePropertyId = Shader.PropertyToID("_Fade");
    private static readonly int TeamColorPropertyId = Shader.PropertyToID("_TeamColor");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static bool _hasLoggedMissingPaletteWarning;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("Death Fade")]
    [SerializeField] private float deathFadeDuration = 0.75f;
    [SerializeField] private float aliveFadeValue = 1f;
    [SerializeField] private float deadFadeValue = 0f;

    [Header("Team")]
    [SerializeField] private int defaultTeam;

    [Header("Team Visual")]
    [SerializeField] private TeamVisualPalette teamVisualPalette;

    private SelectionOutlineTarget _selectionOutlineTarget;
    private Renderer[] _renderers;
    private Collider2D[] _colliders;
    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _fadeRoutine;
    private Coroutine _serverDeathRoutine;
    private bool _isDying;
    private readonly SyncVar<int> _currentHealth = new();
    private readonly SyncVar<int> _team = new();

    public int MaxHealth => maxHealth;
    public int CurrentHealth => _currentHealth.Value;
    public int Team => _team.Value;
    public bool IsAlive => _currentHealth.Value > 0 && !_isDying;

    private void Awake()
    {
        _selectionOutlineTarget = GetComponent<SelectionOutlineTarget>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider2D>(true);
        _propertyBlock = new MaterialPropertyBlock();
        _team.OnChange += HandleTeamChanged;
        _currentHealth.Value = maxHealth;
        _currentHealth.UpdateSendRate(0f);
        _team.Value = defaultTeam;
        _team.UpdateSendRate(0f);
        ResolveTeamVisualPaletteIfMissing();
        SetFadeValue(aliveFadeValue);
        ApplyTeamVisual(_team.Value);
    }

    private void OnDestroy()
    {
        _team.OnChange -= HandleTeamChanged;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        maxHealth = Mathf.Max(1, maxHealth);
        deathFadeDuration = Mathf.Max(0f, deathFadeDuration);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResetHealthServer();
        ResetTeamServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveTeamVisualPaletteIfMissing();
        SetFadeValue(IsAlive ? aliveFadeValue : deadFadeValue);
        ApplyTeamVisual(_team.Value);
    }

    public void SetTeamVisualPaletteRuntime(TeamVisualPalette palette)
    {
        if (palette == null)
        {
            return;
        }

        teamVisualPalette = palette;
        ApplyTeamVisual(_team.Value);
    }

    public void SetTeamRuntimeLocal(int value)
    {
        _team.Value = value;
        ApplyTeamVisual(value);
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

        if (!PlayerCommander.TryGetLocalPlayerTeam(out int localTeam))
        {
            return false;
        }

        return Team == localTeam;
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
        if (_isDying)
        {
            return;
        }

        int previousHealth = _currentHealth.Value;
        _currentHealth.Value = Mathf.Clamp(value, 0, maxHealth);

        if (previousHealth > 0 && _currentHealth.Value <= 0)
        {
            StartDeathServer();
        }
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
        _isDying = false;
        if (_serverDeathRoutine != null)
        {
            StopCoroutine(_serverDeathRoutine);
            _serverDeathRoutine = null;
        }

        _currentHealth.Value = maxHealth;
        SetCollidersEnabled(true);
        PlayFadeForCurrentNetworkMode(aliveFadeValue, aliveFadeValue, 0f, false);
    }

    [Server]
    public void SetTeamServer(int value)
    {
        _team.Value = value;
    }

    [Server]
    public void ResetTeamServer()
    {
        SetTeamServer(defaultTeam);
    }

    private void HandleTeamChanged(int previousTeam, int nextTeam, bool asServer)
    {
        ApplyTeamVisual(nextTeam);
    }

    [Server]
    private void StartDeathServer()
    {
        if (_isDying)
        {
            return;
        }

        _isDying = true;
        SetSelectionVisual(false);
        SetCollidersEnabled(false);
        PlayFadeForCurrentNetworkMode(aliveFadeValue, deadFadeValue, deathFadeDuration, true);

        if (_serverDeathRoutine != null)
        {
            StopCoroutine(_serverDeathRoutine);
        }

        _serverDeathRoutine = StartCoroutine(RemoveAfterDeathFadeServer());
    }

    [ObserversRpc(RunLocally = true)]
    private void PlayDeathFadeObserversRpc(float fromValue, float toValue, float duration, bool isDying)
    {
        _isDying = isDying;
        PlayFade(fromValue, toValue, duration);
    }

    private void PlayFadeForCurrentNetworkMode(float fromValue, float toValue, float duration, bool isDying)
    {
        if (!IsClientStarted && !IsServerStarted)
        {
            _isDying = isDying;
            PlayFade(fromValue, toValue, duration);
            return;
        }

        PlayDeathFadeObserversRpc(fromValue, toValue, duration, isDying);
    }

    private void PlayFade(float fromValue, float toValue, float duration)
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
        }

        _fadeRoutine = StartCoroutine(FadeRoutine(fromValue, toValue, duration));
    }

    private IEnumerator FadeRoutine(float fromValue, float toValue, float duration)
    {
        if (duration <= 0f)
        {
            SetFadeValue(toValue);
            _fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetFadeValue(Mathf.Lerp(fromValue, toValue, t));
            yield return null;
        }

        SetFadeValue(toValue);
        _fadeRoutine = null;
    }

    private IEnumerator RemoveAfterDeathFadeServer()
    {
        if (deathFadeDuration > 0f)
        {
            yield return new WaitForSeconds(deathFadeDuration);
        }

        if (NetworkObject != null && IsServerInitialized)
        {
            Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetFadeValue(float fadeValue)
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer targetRenderer = _renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(FadePropertyId, fadeValue);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void ApplyTeamVisual(int team)
    {
        ResolveTeamVisualPaletteIfMissing();

        Color teamColor = teamVisualPalette != null
            ? teamVisualPalette.GetColorForTeam(team)
            : Color.white;

        SetTeamColor(teamColor);
    }

    private void ResolveTeamVisualPaletteIfMissing()
    {
        if (teamVisualPalette != null)
        {
            return;
        }

        TeamVisualPalette[] availablePalettes = Resources.LoadAll<TeamVisualPalette>(string.Empty);
        if (availablePalettes != null && availablePalettes.Length > 0)
        {
            teamVisualPalette = availablePalettes[0];
            return;
        }

        if (_hasLoggedMissingPaletteWarning)
        {
            return;
        }

        _hasLoggedMissingPaletteWarning = true;
        Debug.LogWarning("Unit: TeamVisualPalette is not assigned and no palette was found in a Resources folder. Assign one on the unit prefab or debug spawner.");
    }

    private void SetTeamColor(Color teamColor)
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer targetRenderer = _renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            if (targetRenderer is SpriteRenderer spriteRenderer)
            {
                Color currentColor = spriteRenderer.color;
                spriteRenderer.color = new Color(teamColor.r, teamColor.g, teamColor.b, currentColor.a);
            }

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(TeamColorPropertyId, teamColor);
            _propertyBlock.SetColor(BaseColorPropertyId, teamColor);
            _propertyBlock.SetColor(ColorPropertyId, teamColor);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null || _colliders.Length == 0)
        {
            _colliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider2D targetCollider = _colliders[i];
            if (targetCollider != null)
            {
                targetCollider.enabled = enabled;
            }
        }
    }
}
