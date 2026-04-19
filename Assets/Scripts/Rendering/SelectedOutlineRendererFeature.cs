using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SelectedOutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Only renderers on these layers are considered selected for outlining.")]
        public LayerMask selectedLayerMask;

        [Tooltip("Color of the selection outline.")]
        public Color outlineColor = Color.green;

        [Range(0.5f, 6f)]
        [Tooltip("Outline thickness in screen pixels.")]
        public float outlineThickness = 2f;
        
        [Tooltip("Debug: fill selected pixels instead of only drawing the edge.")]
        public bool debugFillSelected = false;
        
        [Tooltip("Debug: tint entire screen. If this does nothing, this feature is not executing for the active camera/renderer.")]
        public bool debugForceFullScreen = false;

        public RenderPassEvent maskPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public RenderPassEvent compositePassEvent = RenderPassEvent.AfterRendering;
    }

    [SerializeField] private Settings settings = new Settings();
    [SerializeField] private Shader selectionMaskShader;
    [SerializeField] private Shader compositeShader;

    private Material _selectionMaskMaterial;
    private Material _compositeMaterial;
    private SelectionMaskPass _selectionMaskPass;
    private CompositePass _compositePass;

    public override void Create()
    {
        if (selectionMaskShader == null)
        {
            selectionMaskShader = Shader.Find("Hidden/RTS/SelectionMask");
        }

        if (compositeShader == null)
        {
            compositeShader = Shader.Find("Hidden/RTS/SelectionOutlineComposite");
        }

        if (selectionMaskShader == null || compositeShader == null)
        {
            return;
        }

        _selectionMaskMaterial = CoreUtils.CreateEngineMaterial(selectionMaskShader);
        _compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);

        _selectionMaskPass = new SelectionMaskPass(settings, _selectionMaskMaterial);
        _compositePass = new CompositePass(settings, _compositeMaterial, _selectionMaskPass);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_selectionMaskMaterial == null || _compositeMaterial == null)
        {
            return;
        }

        _selectionMaskPass.renderPassEvent = settings.maskPassEvent;
        _compositePass.renderPassEvent = settings.compositePassEvent;

        renderer.EnqueuePass(_selectionMaskPass);
        renderer.EnqueuePass(_compositePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_selectionMaskMaterial);
        CoreUtils.Destroy(_compositeMaterial);
    }

    private sealed class SelectionMaskPass : ScriptableRenderPass
    {
        private static readonly List<ShaderTagId> ShaderTagIds = new List<ShaderTagId>
        {
            new ShaderTagId("Universal2D"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("LightweightForward"),
            new ShaderTagId("Always")
        };

        private readonly Settings _settings;
        private readonly Material _material;
        private readonly int _selectionMaskTexId = Shader.PropertyToID("_SelectionMaskTex");
        private readonly RenderTargetHandle _selectionMaskHandle;

        public RenderTargetIdentifier SelectionMaskTarget => _selectionMaskHandle.Identifier();
        public int SelectionMaskTexId => _selectionMaskTexId;

        public SelectionMaskPass(Settings settings, Material material)
        {
            _settings = settings;
            _material = material;
            _selectionMaskHandle.Init("_SelectionMaskTex");
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor descriptor = cameraTextureDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;

            cmd.GetTemporaryRT(_selectionMaskTexId, descriptor, FilterMode.Point);
            ConfigureTarget(_selectionMaskHandle.Identifier());
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("SelectionMaskPass");

            int drawnCount = 0;
            foreach (Renderer renderer in SelectionOutlineTarget.ActiveSelectedRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                cmd.DrawRenderer(renderer, _material, 0, 0);
                drawnCount++;
            }

            if (drawnCount == 0)
            {
                SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
                DrawingSettings drawingSettings = CreateDrawingSettings(ShaderTagIds, ref renderingData, sortingCriteria);
                drawingSettings.overrideMaterial = _material;
                drawingSettings.overrideMaterialPassIndex = 0;

                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.transparent, _settings.selectedLayerMask);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }
    }

    private sealed class CompositePass : ScriptableRenderPass
    {
        private static readonly int SelectionMaskTexGlobalId = Shader.PropertyToID("_SelectionMaskTexGlobal");
        private readonly Settings _settings;
        private readonly Material _material;
        private readonly SelectionMaskPass _maskPass;
        private readonly int _tempColorTexId = Shader.PropertyToID("_TemporaryColorTexture");

        public CompositePass(Settings settings, Material material, SelectionMaskPass maskPass)
        {
            _settings = settings;
            _material = material;
            _maskPass = maskPass;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor descriptor = cameraTextureDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            cmd.GetTemporaryRT(_tempColorTexId, descriptor, FilterMode.Bilinear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("SelectionOutlineComposite");
            RenderTargetIdentifier source = renderingData.cameraData.renderer.cameraColorTarget;

            _material.SetColor("_OutlineColor", _settings.outlineColor);
            _material.SetFloat("_OutlineThickness", _settings.outlineThickness);
            _material.SetFloat("_DebugFillSelected", _settings.debugFillSelected ? 1f : 0f);
            _material.SetFloat("_DebugForceFullScreen", _settings.debugForceFullScreen ? 1f : 0f);
            cmd.SetGlobalTexture(SelectionMaskTexGlobalId, _maskPass.SelectionMaskTarget);

            cmd.Blit(source, _tempColorTexId);
            cmd.Blit(_tempColorTexId, source, _material, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_maskPass.SelectionMaskTexId);
            cmd.ReleaseTemporaryRT(_tempColorTexId);
        }
    }
}
