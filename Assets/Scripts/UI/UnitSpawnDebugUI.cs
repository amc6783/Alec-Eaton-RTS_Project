using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class UnitSpawnDebugUI : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private Unit unitPrefab;
    [SerializeField] private Transform spawnParent;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float spawnSpacing = 0.6f;

    [Header("UI")]
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private bool startVisible = false;

    private UIDocument _uiDocument;
    private VisualElement _panel;
    private TextField _countField;
    private TextField _teamField;
    private Label _statusLabel;
    private Button _spawnButton;

    private bool _isVisible;
    private bool _isDraggingPanel;
    private bool _isPointerOverPanel;
    private Vector2 _panelDragPointerStart;
    private Vector2 _panelDragStartPosition;
    private static UnitSpawnDebugUI _activeInstance;
    private bool _clickSpawnEnabled;

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null)
        {
            _uiDocument = gameObject.AddComponent<UIDocument>();
        }

        if (_uiDocument.panelSettings == null)
        {
            _uiDocument.panelSettings = panelSettings;
        }

        if (_uiDocument.panelSettings == null)
        {
            Debug.LogWarning("UnitSpawnDebugUI: No PanelSettings assigned. Assign one in the inspector.");
        }
    }

    private void OnEnable()
    {
        _activeInstance = this;
        BuildUi();
        SetVisible(startVisible);
    }

    private void OnDisable()
    {
        if (_panel == null) return;

        _panel.UnregisterCallback<PointerDownEvent>(OnPanelPointerDown);
        _panel.UnregisterCallback<PointerMoveEvent>(OnPanelPointerMove);
        _panel.UnregisterCallback<PointerUpEvent>(OnPanelPointerUp);
        _panel.UnregisterCallback<PointerCaptureOutEvent>(OnPanelPointerCaptureOut);
        _panel.UnregisterCallback<PointerEnterEvent>(OnPanelPointerEnter);
        _panel.UnregisterCallback<PointerLeaveEvent>(OnPanelPointerLeave);

        if (_activeInstance == this)
        {
            _activeInstance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetVisible(!_isVisible);
        }

        if (_clickSpawnEnabled && Input.GetMouseButtonDown(0) && !IsPointerOverDebugUi())
        {
            SpawnUnitsAt(GetMouseWorldPosition2D());
        }
    }

    private void BuildUi()
    {
        VisualElement root = _uiDocument.rootVisualElement;
        root.Clear();

        _panel = new VisualElement
        {
            style =
            {
                position = Position.Absolute,
                left = 12,
                top = 12,
                width = 280,
                paddingLeft = 10,
                paddingRight = 10,
                paddingTop = 10,
                paddingBottom = 10,
                backgroundColor = new Color(0f, 0f, 0f, 0.75f),
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6,
                color = Color.white,
            }
        };

        Label titleLabel = new Label("Unit Spawn Debug")
        {
            style =
            {
                color = Color.white,
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 6
            }
        };

        Label hintLabel = new Label("Open: " + toggleKey)
        {
            style =
            {
                color = Color.white,
                fontSize = 11,
                marginBottom = 6
            }
        };

        _countField = new TextField("Count") { value = "1" };
        _countField.style.marginBottom = 4;

        _teamField = new TextField("Team") { value = "0" };
        _teamField.style.marginBottom = 8;

        _countField.style.marginBottom = 8;

        _spawnButton = new Button(ToggleClickSpawnMode)
        {
            text = "Spawn Units"
        };
        _spawnButton.style.marginBottom = 6;

        _statusLabel = new Label("Ready")
        {
            style =
            {
                color = Color.white,
                fontSize = 11,
                whiteSpace = WhiteSpace.Normal
            }
        };

        _panel.Add(titleLabel);
        _panel.Add(hintLabel);
        _panel.Add(_countField);
        _panel.Add(_teamField);
        _panel.Add(_spawnButton);
        _panel.Add(_statusLabel);
        _panel.RegisterCallback<PointerDownEvent>(OnPanelPointerDown);
        _panel.RegisterCallback<PointerMoveEvent>(OnPanelPointerMove);
        _panel.RegisterCallback<PointerUpEvent>(OnPanelPointerUp);
        _panel.RegisterCallback<PointerCaptureOutEvent>(OnPanelPointerCaptureOut);
        _panel.RegisterCallback<PointerEnterEvent>(OnPanelPointerEnter);
        _panel.RegisterCallback<PointerLeaveEvent>(OnPanelPointerLeave);

        root.Add(_panel);
    }

    private void ToggleClickSpawnMode()
    {
        if (unitPrefab == null)
        {
            _statusLabel.text = "Unit prefab is required. Assign Assets/Prefabs/Unit.prefab.";
            return;
        }
        
        _clickSpawnEnabled = !_clickSpawnEnabled;
        RefreshSpawnModeUi();
    }

    private void SpawnUnitsAt(Vector3 origin)
    {
        int count = Mathf.Max(1, ParseInt(_countField.value, 1));
        int team = ParseInt(_teamField?.value, 0);

        int createdCount = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3((i % 5) * spawnSpacing, (i / 5) * spawnSpacing, 0f);
            Vector3 spawnPosition = origin + offset;

            Unit createdUnit = Instantiate(unitPrefab, spawnPosition, Quaternion.identity, spawnParent);

            if (createdUnit != null && FinalizeSpawn(createdUnit, team))
            {
                createdCount++;
            }
        }

        _statusLabel.text = "Created " + createdCount + " unit(s) on team " + team + ".";
    }

    private Vector3 GetMouseWorldPosition2D()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (worldCamera == null)
        {
            return Vector3.zero;
        }

        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = -worldCamera.transform.position.z;

        Vector3 world = worldCamera.ScreenToWorldPoint(mousePosition);
        world.z = 0f;
        return world;
    }

    private bool FinalizeSpawn(Unit unit, int team)
    {
        if (unit == null)
        {
            return false;
        }

        NetworkObject networkObject = unit.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            _statusLabel.text = "Unit prefab is missing NetworkObject.";
            return false;
        }

        if (!InstanceFinder.IsClientStarted && !InstanceFinder.IsServerStarted)
        {
            _statusLabel.text = "Team assignment requires server/host.";
            return true;
        }
        if (!InstanceFinder.IsServerStarted)
        {
            _statusLabel.text = "Only server/host may spawn network units.";
            Destroy(unit.gameObject);
            return false;
        }

        NetworkConnection ownerConnection = InstanceFinder.IsClientStarted ? InstanceFinder.ClientManager.Connection : null;
        InstanceFinder.ServerManager.Spawn(networkObject, ownerConnection);
        unit.SetTeamServer(team);
        return true;
    }

    private void RefreshSpawnModeUi()
    {
        if (_spawnButton != null)
        {
            _spawnButton.text = _clickSpawnEnabled ? "Stop Spawning" : "Spawn Units";
        }

        _statusLabel.text = _clickSpawnEnabled
            ? "Click Spawn Mode enabled. Left-click world to spawn."
            : "Click Spawn Mode disabled.";
    }

    private static int ParseInt(string text, int fallback)
    {
        return int.TryParse(text, out int value) ? value : fallback;
    }

    private void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;
        if (_panel != null)
        {
            _panel.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (!_isVisible)
        {
            _isDraggingPanel = false;
            _isPointerOverPanel = false;
        }
    }

    public static bool IsBlockingWorldInput()
    {
        if (_activeInstance == null || !_activeInstance._isVisible || _activeInstance._panel == null)
        {
            return false;
        }
        if (_activeInstance._clickSpawnEnabled)
        {
            return true;
        }

        if (_activeInstance._isDraggingPanel || _activeInstance._isPointerOverPanel)
        {
            return true;
        }

        return _activeInstance.IsPointerOverDebugUi();
    }

    private bool IsPointerOverDebugUi()
    {
        if (!_isVisible || _panel == null)
        {
            return false;
        }

        Vector2 panelPosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (_panel.worldBound.Contains(panelPosition))
        {
            return true;
        }

        var panel = _uiDocument?.rootVisualElement?.panel;
        if (panel == null)
        {
            return false;
        }

        return panel.Pick(panelPosition) != null;
    }

    private void OnPanelPointerDown(PointerDownEvent evt)
    {
        if (_panel == null || evt.button != 0) return;

        _isDraggingPanel = true;
        _isPointerOverPanel = true;
        _panelDragPointerStart = new Vector2(evt.position.x, evt.position.y);
        _panelDragStartPosition = new Vector2(_panel.resolvedStyle.left, _panel.resolvedStyle.top);
        _panel.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPanelPointerMove(PointerMoveEvent evt)
    {
        if (_panel == null || !_isDraggingPanel || !_panel.HasPointerCapture(evt.pointerId)) return;

        Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
        Vector2 delta = pointerPosition - _panelDragPointerStart;
        _panel.style.left = _panelDragStartPosition.x + delta.x;
        _panel.style.top = _panelDragStartPosition.y + delta.y;
        evt.StopPropagation();
    }

    private void OnPanelPointerUp(PointerUpEvent evt)
    {
        if (_panel == null || evt.button != 0) return;
        EndPanelDrag(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPanelPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        EndPanelDrag(evt.pointerId);
    }

    private void OnPanelPointerEnter(PointerEnterEvent evt)
    {
        _isPointerOverPanel = true;
    }

    private void OnPanelPointerLeave(PointerLeaveEvent evt)
    {
        _isPointerOverPanel = false;
    }

    private void EndPanelDrag(int pointerId)
    {
        if (_panel == null) return;

        if (_panel.HasPointerCapture(pointerId))
        {
            _panel.ReleasePointer(pointerId);
        }

        _isDraggingPanel = false;
    }
}
