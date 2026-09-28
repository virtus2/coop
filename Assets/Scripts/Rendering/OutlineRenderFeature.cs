using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Coop.Rendering
{
    /// <summary>
    /// 상호작용 대상 오브젝트의 렌더러를 깊이 테스트와 함께 마스킹한 뒤,
    /// 스크린 스페이스 엣지 디텍션으로 외곽선을 오버레이하는 URP Renderer Feature입니다.
    /// Unity 6 Render Graph API를 지원합니다.
    /// </summary>
    [DisallowMultipleRendererFeature("Outline Render Feature")]
    public class OutlineRenderFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class OutlineSettings
        {
            [Tooltip("외곽선 렌더링을 주입할 파이프라인 이벤트")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

            [Tooltip("외곽선 두께 (픽셀 단위, 기본 2px)")]
            [Range(1.0f, 5.0f)]
            public float outlineThickness = 2.0f;
        }

        [SerializeField] private OutlineSettings _settings = new OutlineSettings();

        private Material _maskMaterial;
        private Material _compositeMaterial;
        private OutlinePass _outlinePass;

        public OutlineSettings Settings => _settings;

        public override void Create()
        {
            Shader maskShader = Shader.Find("Hidden/Coop/OutlineMask");
            Shader compositeShader = Shader.Find("Hidden/Coop/OutlineComposite");

            if (maskShader != null)
            {
                _maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            }

            if (compositeShader != null)
            {
                _compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
            }

            _outlinePass = new OutlinePass(_settings, _maskMaterial, _compositeMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            // 하이라이트할 대상 렌더러가 없으면 패스 실행을 완전히 건너뛰어 성능 오버헤드 0을 유지
            if (!OutlineManager.HasTarget)
            {
                return;
            }

            if (_maskMaterial == null || _compositeMaterial == null)
            {
                return;
            }

            renderer.EnqueuePass(_outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_maskMaterial);
            CoreUtils.Destroy(_compositeMaterial);
            _outlinePass = null;
        }

        /// <summary>
        /// 외곽선 마스크 생성 및 화면 합성을 담당하는 Render Graph 기반 ScriptableRenderPass
        /// </summary>
        private class OutlinePass : ScriptableRenderPass
        {
            private readonly OutlineSettings _settings;
            private readonly Material _maskMaterial;
            private readonly Material _compositeMaterial;

            private static readonly int MaskTexPropertyId = Shader.PropertyToID("_MaskTex");
            private static readonly int OutlineColorPropertyId = Shader.PropertyToID("_OutlineColor");
            private static readonly int OutlineThicknessPropertyId = Shader.PropertyToID("_OutlineThickness");

            public OutlinePass(OutlineSettings settings, Material maskMaterial, Material compositeMaterial)
            {
                _settings = settings;
                _maskMaterial = maskMaterial;
                _compositeMaterial = compositeMaterial;
                renderPassEvent = settings.renderPassEvent;
            }

            private class MaskPassData
            {
                public Material maskMaterial;
                public List<Renderer> renderers;
            }

            private class CompositePassData
            {
                public Material compositeMaterial;
                public TextureHandle maskTexture;
                public Color outlineColor;
                public float outlineThickness;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                List<Renderer> activeRenderers = OutlineManager.GetActiveRenderers();
                if (activeRenderers == null || activeRenderers.Count == 0)
                {
                    return;
                }

                if (_maskMaterial == null || _compositeMaterial == null)
                {
                    return;
                }

                // 1. 마스크 텍스처 생성 (깊이 버퍼는 None, R8 형식으로 최소 메모리 사용)
                RenderTextureDescriptor maskDesc = cameraData.cameraTargetDescriptor;
                maskDesc.colorFormat = RenderTextureFormat.R8;
                maskDesc.depthStencilFormat = GraphicsFormat.None;
                maskDesc.msaaSamples = 1;

                TextureHandle maskTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    maskDesc,
                    "OutlineMaskTexture",
                    false,
                    FilterMode.Bilinear
                );

                if (!maskTexture.IsValid())
                {
                    return;
                }

                // 2. 마스크 패스: 대상 렌더러들의 표면을 마스크 버퍼에 렌더링 (깊이 테스트를 거쳐 가려진 부분 제외)
                using (var maskBuilder = renderGraph.AddRasterRenderPass<MaskPassData>("Outline Mask Pass", out var maskPassData))
                {
                    maskPassData.maskMaterial = _maskMaterial;
                    maskPassData.renderers = activeRenderers;

                    maskBuilder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
                    if (resourceData.activeDepthTexture.IsValid())
                    {
                        maskBuilder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    }

                    maskBuilder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                    {
                        ExecuteMaskPass(data, context);
                    });
                }

                // 3. 합성 패스: 마스크 버퍼의 엣지를 검출하여 활성 컬러 버퍼에 오버레이
                using (var compositeBuilder = renderGraph.AddRasterRenderPass<CompositePassData>("Outline Composite Pass", out var compositePassData))
                {
                    compositePassData.compositeMaterial = _compositeMaterial;
                    compositePassData.maskTexture = maskTexture;
                    compositePassData.outlineColor = OutlineManager.CurrentColor;
                    compositePassData.outlineThickness = _settings.outlineThickness;

                    compositeBuilder.UseTexture(maskTexture, AccessFlags.Read);
                    compositeBuilder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                    compositeBuilder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                    {
                        ExecuteCompositePass(data, context);
                    });
                }
            }

            private static void ExecuteMaskPass(MaskPassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(false, true, Color.clear);

                for (int i = 0; i < data.renderers.Count; i++)
                {
                    Renderer r = data.renderers[i];
                    if (r == null || !r.gameObject.activeInHierarchy || !r.enabled)
                    {
                        continue;
                    }

                    int submeshCount = r.sharedMaterials != null ? r.sharedMaterials.Length : 1;
                    for (int s = 0; s < submeshCount; s++)
                    {
                        context.cmd.DrawRenderer(r, data.maskMaterial, s, 0);
                    }
                }
            }

            private static void ExecuteCompositePass(CompositePassData data, RasterGraphContext context)
            {
                data.compositeMaterial.SetTexture(MaskTexPropertyId, data.maskTexture);
                data.compositeMaterial.SetColor(OutlineColorPropertyId, data.outlineColor);
                data.compositeMaterial.SetFloat(OutlineThicknessPropertyId, data.outlineThickness);

                Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), data.compositeMaterial, 0);
            }
        }
    }
}
