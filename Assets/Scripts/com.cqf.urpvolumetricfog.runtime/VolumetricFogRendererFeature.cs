using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Tooltip("Adds support to render volumetric fog.")]
[DisallowMultipleRendererFeature("Volumetric Fog")]
public sealed class VolumetricFogRendererFeature : ScriptableRendererFeature
{
	[HideInInspector]
	[SerializeField]
	private Shader downsampleDepthShader;

	[HideInInspector]
	[SerializeField]
	private Shader volumetricFogShader;

	private Material downsampleDepthMaterial;

	private Material volumetricFogMaterial;

	private VolumetricFogRenderPass volumetricFogRenderPass;

	public override void Create()
	{
		ValidateResourcesForVolumetricFogRenderPass(forceRefresh: true);
		volumetricFogRenderPass = new VolumetricFogRenderPass(downsampleDepthMaterial, volumetricFogMaterial, RenderPassEvent.BeforeRenderingPostProcessing);
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (renderingData.postProcessingEnabled && renderingData.cameraData.postProcessEnabled && ShouldAddVolumetricFogRenderPass(renderingData.cameraData.cameraType))
		{
			volumetricFogRenderPass.renderPassEvent = GetRenderPassEvent();
			volumetricFogRenderPass.ConfigureInput(ScriptableRenderPassInput.Depth);
			renderer.EnqueuePass(volumetricFogRenderPass);
		}
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		CoreUtils.Destroy(downsampleDepthMaterial);
		CoreUtils.Destroy(volumetricFogMaterial);
	}

	private bool ValidateResourcesForVolumetricFogRenderPass(bool forceRefresh)
	{
		if (forceRefresh)
		{
			CoreUtils.Destroy(downsampleDepthMaterial);
			downsampleDepthMaterial = CoreUtils.CreateEngineMaterial(downsampleDepthShader);
			CoreUtils.Destroy(volumetricFogMaterial);
			volumetricFogMaterial = CoreUtils.CreateEngineMaterial(volumetricFogShader);
		}
		bool num = downsampleDepthShader != null && downsampleDepthMaterial != null;
		bool flag = volumetricFogShader != null && volumetricFogMaterial != null;
		return num & flag;
	}

	private bool ShouldAddVolumetricFogRenderPass(CameraType cameraType)
	{
		VolumetricFogVolumeComponent component = VolumeManager.instance.stack.GetComponent<VolumetricFogVolumeComponent>();
		bool flag = component != null && component.IsActive();
		bool flag2 = cameraType != CameraType.Preview && cameraType != CameraType.Reflection;
		bool flag3 = ValidateResourcesForVolumetricFogRenderPass(forceRefresh: false);
		return base.isActive & flag & flag2 & flag3;
	}

	private RenderPassEvent GetRenderPassEvent()
	{
		return (RenderPassEvent)VolumeManager.instance.stack.GetComponent<VolumetricFogVolumeComponent>().renderPassEvent.value;
	}
}
