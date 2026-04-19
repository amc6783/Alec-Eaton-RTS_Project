using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SelectionOutlineTarget : MonoBehaviour
{
    private static readonly HashSet<Renderer> SelectedRenderers = new HashSet<Renderer>();
    public static IReadOnlyCollection<Renderer> ActiveSelectedRenderers => SelectedRenderers;

    [SerializeField] private string selectedLayerName = "SelectedUnits";

    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<Transform> _transformsWithRenderer = new List<Transform>();
    private readonly Dictionary<Transform, int> _originalLayers = new Dictionary<Transform, int>();
    private bool _isInitialized;
    private bool _isSelected;
    private int _selectedLayer = -1;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (_isInitialized)
        {
            return;
        }

        Initialize();
    }

    public void SetSelected(bool selected)
    {
        Initialize();
        _isSelected = selected;

        for (int i = 0; i < _transformsWithRenderer.Count; i++)
        {
            Transform target = _transformsWithRenderer[i];
            if (target == null)
            {
                continue;
            }

            if (selected)
            {
                if (_selectedLayer >= 0)
                {
                    target.gameObject.layer = _selectedLayer;
                }
            }
            else if (_originalLayers.TryGetValue(target, out int originalLayer))
            {
                target.gameObject.layer = originalLayer;
            }
        }

        if (selected)
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer != null)
                {
                    SelectedRenderers.Add(renderer);
                }
            }
        }
        else
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer != null)
                {
                    SelectedRenderers.Remove(renderer);
                }
            }
        }
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _transformsWithRenderer.Clear();
        _originalLayers.Clear();
        _renderers.Clear();
        _selectedLayer = LayerMask.NameToLayer(selectedLayerName);

        if (_selectedLayer < 0)
        {
            Debug.LogWarning($"SelectionOutlineTarget on {name}: Layer '{selectedLayerName}' does not exist.");
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Transform target = renderer.transform;
            if (_originalLayers.ContainsKey(target))
            {
                continue;
            }

            _transformsWithRenderer.Add(target);
            _originalLayers[target] = target.gameObject.layer;
            _renderers.Add(renderer);
        }

        _isInitialized = true;
    }

    private void OnDisable()
    {
        if (_isSelected)
        {
            SetSelected(false);
        }
    }

    private void OnDestroy()
    {
        if (_isSelected)
        {
            SetSelected(false);
        }
    }
}
