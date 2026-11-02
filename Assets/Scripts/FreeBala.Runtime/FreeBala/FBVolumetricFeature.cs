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
	private class Pass : ScriptableRenderPass
	{
		private class MarchData
		{
			public Material mat;
		}

		private class CompData
		{
			public Material mat;

			public TextureHandle src;

			public TextureHandle vol;

			public MaterialPropertyBlock mpb;

			public Vector4 texel;
		}

		public Material mat;

		public float scale = 0.5f;

		private static readonly int VolTex = Shader.PropertyToID("_FBVolTex");

		private static readonly int BlitTex = Shader.PropertyToID("_BlitTexture");

		private static readonly int BlitSB = Shader.PropertyToID("_BlitScaleBias");

		private static readonly int TexelId = Shader.PropertyToID("_FBVolTex_TexelSize");

		private readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

		public Pass()
		{
			base.profilingSampler = new ProfilingSampler("FB Luz Volumetrica");
		}

		public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
		{
			UniversalResourceData universalResourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData universalCameraData = frameData.Get<UniversalCameraData>();
			if (!universalResourceData.cameraDepthTexture.IsValid() || !universalResourceData.cameraColor.IsValid())
			{
				return;
			}
			RenderTextureDescriptor cameraTargetDescriptor = universalCameraData.cameraTargetDescriptor;
			TextureDesc textureDesc = new TextureDesc(Mathf.Max(1, (int)((float)cameraTargetDescriptor.width * scale)), Mathf.Max(1, (int)((float)cameraTargetDescriptor.height * scale)));
			textureDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
			textureDesc.name = "_FBVolTex";
			textureDesc.clearBuffer = false;
			textureDesc.filterMode = FilterMode.Bilinear;
			textureDesc.wrapMode = TextureWrapMode.Clamp;
			TextureDesc desc = textureDesc;
			TextureHandle textureHandle = rg.CreateTexture(in desc);
			MarchData passData;
			TextureHandle cameraDepthTexture;
			using (IRasterRenderGraphBuilder rasterRenderGraphBuilder = rg.AddRasterRenderPass<MarchData>("FB Volumetrica Raymarch", out passData, base.profilingSampler, "C:\\Users\\Rodrigo\\Desktop\\favela\\game\\unity\\FreeBala\\Assets\\FreeBala\\Runtime\\World\\FBVolumetricFeature.cs", 49))
			{
				passData.mat = mat;
				cameraDepthTexture = universalResourceData.cameraDepthTexture;
				rasterRenderGraphBuilder.UseTexture(in cameraDepthTexture);
				rasterRenderGraphBuilder.UseAllGlobalTextures(enable: true);
				rasterRenderGraphBuilder.SetRenderAttachment(textureHandle, 0);
				rasterRenderGraphBuilder.SetRenderFunc(delegate(MarchData x, RasterGraphContext c)
				{
					Blitter.BlitTexture(c.cmd, new Vector4(1f, 1f, 0f, 0f), x.mat, 0);
				});
			}
			cameraDepthTexture = universalResourceData.cameraColor;
			TextureDesc desc2 = rg.GetTextureDesc(in cameraDepthTexture);
			desc2.name = "_FBVolCor";
			desc2.clearBuffer = false;
			TextureHandle textureHandle2 = rg.CreateTexture(in desc2);
			rg.AddBlitPass(universalResourceData.cameraColor, textureHandle2, Vector2.one, Vector2.zero, 0, 0, -1, 0, 0, 1, RenderGraphUtils.BlitFilterMode.ClampBilinear, "FB Volumetrica Copia", returnBuilder: false, "C:\\Users\\Rodrigo\\Desktop\\favela\\game\\unity\\FreeBala\\Assets\\FreeBala\\Runtime\\World\\FBVolumetricFeature.cs", 59);
			CompData passData2;
			using IRasterRenderGraphBuilder rasterRenderGraphBuilder2 = rg.AddRasterRenderPass<CompData>("FB Volumetrica Composicao", out passData2, base.profilingSampler, "C:\\Users\\Rodrigo\\Desktop\\favela\\game\\unity\\FreeBala\\Assets\\FreeBala\\Runtime\\World\\FBVolumetricFeature.cs", 60);
			passData2.mat = mat;
			passData2.src = textureHandle2;
			passData2.vol = textureHandle;
			passData2.mpb = mpb;
			passData2.texel = new Vector4(1f / (float)desc.width, 1f / (float)desc.height, desc.width, desc.height);
			rasterRenderGraphBuilder2.UseTexture(in textureHandle2);
			rasterRenderGraphBuilder2.UseTexture(in textureHandle);
			cameraDepthTexture = universalResourceData.cameraDepthTexture;
			rasterRenderGraphBuilder2.UseTexture(in cameraDepthTexture);
			rasterRenderGraphBuilder2.SetRenderAttachment(universalResourceData.activeColorTexture, 0);
			rasterRenderGraphBuilder2.SetRenderFunc(delegate(CompData x, RasterGraphContext c)
			{
				x.mpb.Clear();
				x.mpb.SetTexture(BlitTex, (RTHandle)x.src);
				x.mpb.SetTexture(VolTex, (RTHandle)x.vol);
				x.mpb.SetVector(BlitSB, new Vector4(1f, 1f, 0f, 0f));
				x.mpb.SetVector(TexelId, x.texel);
				c.cmd.DrawProcedural(Matrix4x4.identity, x.mat, 1, MeshTopology.Triangles, 3, 1, x.mpb);
			});
		}
	}

	public Material material;

	[Range(0.25f, 1f)]
	public float resolution = 0.5f;

	private Pass pass;

	public override void Create()
	{
		pass = new Pass
		{
			renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
		};
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		CameraData cameraData = renderingData.cameraData;
		if ((bool)material && FBVolumetric.Active && cameraData.renderType == CameraRenderType.Base && (cameraData.cameraType == CameraType.Game || cameraData.cameraType == CameraType.SceneView))
		{
			pass.mat = material;
			pass.scale = ((FBVolumetric.ResolutionScale > 0f) ? FBVolumetric.ResolutionScale : resolution);
			pass.ConfigureInput(ScriptableRenderPassInput.Depth);
			pass.requiresIntermediateTexture = true;
			renderer.EnqueuePass(pass);
		}
	}
}
}
