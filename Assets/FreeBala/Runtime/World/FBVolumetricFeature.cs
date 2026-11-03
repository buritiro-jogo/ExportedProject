// Renderer feature da LUZ VOLUMÉTRICA (FBVolumetric.shader). Arquivo próprio: ScriptableObject precisa do nome do arquivo = nome da classe.
// Só na câmera base do jogo (a câmera da arma é overlay) e só quando o FBVolumetric está ativo no nível gráfico (PC Médio/Alto/Ultra).
// RenderGraph (URP 17): raymarch em meia resolução -> textura temporária; cópia da cor; composição na cor da câmera antes dos transparentes.
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    public class FBVolumetricFeature : ScriptableRendererFeature
    {
        public Material material;
        [Range(.25f, 1f)] public float resolution = .5f;
        Pass pass;

        public override void Create() { pass = new Pass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents }; }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cd = renderingData.cameraData;
            if (!material || !FBVolumetric.Active || cd.renderType != CameraRenderType.Base) return;
            if (cd.cameraType != CameraType.Game && cd.cameraType != CameraType.SceneView) return;
            pass.mat = material; pass.scale = FBVolumetric.ResolutionScale > 0 ? FBVolumetric.ResolutionScale : resolution;
            pass.ConfigureInput(ScriptableRenderPassInput.Depth); pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(pass);
        }

        class Pass : ScriptableRenderPass
        {
            public Material mat; public float scale = .5f;
            static readonly int VolTex = Shader.PropertyToID("_FBVolTex"), BlitTex = Shader.PropertyToID("_BlitTexture"), BlitSB = Shader.PropertyToID("_BlitScaleBias"), TexelId = Shader.PropertyToID("_FBVolTex_TexelSize");
            class MarchData { public Material mat; }
            class CompData { public Material mat; public TextureHandle src, vol; public MaterialPropertyBlock mpb; public Vector4 texel; }
            readonly MaterialPropertyBlock mpb = new();

            public Pass() { profilingSampler = new ProfilingSampler("FB Luz Volumetrica"); }

            public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
            {
                var res = frameData.Get<UniversalResourceData>(); var cam = frameData.Get<UniversalCameraData>();
                if (!res.cameraDepthTexture.IsValid() || !res.cameraColor.IsValid()) return;
                var cdesc = cam.cameraTargetDescriptor;
                var vd = new TextureDesc(Mathf.Max(1, (int)(cdesc.width * scale)), Mathf.Max(1, (int)(cdesc.height * scale)))
                { colorFormat = GraphicsFormat.R16G16B16A16_SFloat, name = "_FBVolTex", clearBuffer = false, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var vol = rg.CreateTexture(vd);
                using (var b = rg.AddRasterRenderPass<MarchData>("FB Volumetrica Raymarch", out var d, profilingSampler))
                {
                    d.mat = mat;
                    b.UseTexture(res.cameraDepthTexture);
                    b.UseAllGlobalTextures(true);                                                  // shadow map do sol/lua
                    b.SetRenderAttachment(vol, 0, AccessFlags.Write);
                    b.SetRenderFunc(static (MarchData x, RasterGraphContext c) => Blitter.BlitTexture(c.cmd, new Vector4(1, 1, 0, 0), x.mat, 0));
                }
                var desc = rg.GetTextureDesc(res.cameraColor); desc.name = "_FBVolCor"; desc.clearBuffer = false;
                var copy = rg.CreateTexture(desc);
                rg.AddBlitPass(res.cameraColor, copy, Vector2.one, Vector2.zero, passName: "FB Volumetrica Copia");
                using (var b = rg.AddRasterRenderPass<CompData>("FB Volumetrica Composicao", out var d, profilingSampler))
                {
                    d.mat = mat; d.src = copy; d.vol = vol; d.mpb = mpb; d.texel = new Vector4(1f / vd.width, 1f / vd.height, vd.width, vd.height);
                    b.UseTexture(copy); b.UseTexture(vol); b.UseTexture(res.cameraDepthTexture);
                    b.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.Write);
                    b.SetRenderFunc(static (CompData x, RasterGraphContext c) =>
                    {
                        x.mpb.Clear(); x.mpb.SetTexture(BlitTex, (RTHandle)x.src); x.mpb.SetTexture(VolTex, (RTHandle)x.vol); x.mpb.SetVector(BlitSB, new Vector4(1, 1, 0, 0));
                        x.mpb.SetVector(TexelId, x.texel);
                        c.cmd.DrawProcedural(Matrix4x4.identity, x.mat, 1, MeshTopology.Triangles, 3, 1, x.mpb);
                    });
                }
            }
        }
    }
}
