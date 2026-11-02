using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Custom/Volumetric Fog")]
[VolumeRequiresRendererFeatures(new Type[] { typeof(VolumetricFogRendererFeature) })]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "Volumetric Fog")]
public sealed class VolumetricFogVolumeComponent : VolumeComponent, IPostProcessComponent
{
	[Header("Distances")]
	[Tooltip("The maximum distance from the camera that the fog will be rendered up to.")]
	public ClampedFloatParameter distance = new ClampedFloatParameter(64f, 0f, 512f);

	[Tooltip("The world height at which the fog will have the density specified in the volume.")]
	public FloatParameter baseHeight = new FloatParameter(0f, overrideState: true);

	[Tooltip("The world height at which the fog will have no density at all.")]
	public FloatParameter maximumHeight = new FloatParameter(50f, overrideState: true);

	[Header("Ground")]
	[Tooltip("When enabled, allows to define a world height. Below it, fog will have no density at all.")]
	public BoolParameter enableGround = new BoolParameter(value: false, BoolParameter.DisplayType.Checkbox, overrideState: true);

	[Tooltip("Below this world height, fog will have no density at all.")]
	public FloatParameter groundHeight = new FloatParameter(0f);

	[Header("Lighting")]
	[Tooltip("How dense is the fog.")]
	public ClampedFloatParameter density = new ClampedFloatParameter(0.2f, 0f, 1f);

	[Tooltip("Value that defines how much the fog attenuates light as distance increases. Lesser values lead to a darker image.")]
	public MinFloatParameter attenuationDistance = new MinFloatParameter(128f, 0.05f);

	[Tooltip("When enabled, adaptive probe volumes (APV) will be sampled to contribute to fog.")]
	public BoolParameter enableAPVContribution = new BoolParameter(value: false, BoolParameter.DisplayType.Checkbox, overrideState: true);

	[Tooltip("A weight factor for the light coming from adaptive probe volumes (APV) when the probe volume contribution is enabled.")]
	public ClampedFloatParameter APVContributionWeight = new ClampedFloatParameter(1f, 0f, 1f);

	[Header("Main Light")]
	[Tooltip("Disabling this will avoid computing the main light contribution to fog, which in most cases will lead to better performance.")]
	public BoolParameter enableMainLightContribution = new BoolParameter(value: false, BoolParameter.DisplayType.Checkbox, overrideState: true);

	[Tooltip("Higher positive values will make the fog affected by the main light to appear brighter when directly looking to it, while lower negative values will make the fog to appear brighter when looking away from it. The closer the value is closer to 1 or -1, the less the brightness will spread. Most times, positive values higher than 0 and lower than 1 should be used.")]
	public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.4f, -1f, 1f);

	[Tooltip("Higher values will make fog affected by the main light to appear brighter.")]
	public ClampedFloatParameter scattering = new ClampedFloatParameter(0.15f, 0f, 1f);

	[Tooltip("A multiplier color to tint the main light fog.")]
	public ColorParameter tint = new ColorParameter(Color.white, hdr: true, showAlpha: false, showEyeDropper: true);

	[Header("Additional Lights")]
	[Tooltip("Disabling this will avoid computing additional lights contribution to fog, which in most cases will lead to better performance.")]
	public BoolParameter enableAdditionalLightsContribution = new BoolParameter(value: false, BoolParameter.DisplayType.Checkbox, overrideState: true);

	[Header("Performance & Quality")]
	[Tooltip("Raymarching steps. Greater values will increase the fog quality at the expense of performance.")]
	public ClampedIntParameter maxSteps = new ClampedIntParameter(128, 8, 256);

	[Tooltip("The number of times that the fog texture will be blurred. Higher values lead to softer volumetric god rays at the cost of some performance.")]
	public ClampedIntParameter blurIterations = new ClampedIntParameter(2, 1, 4);

	[Tooltip("Disabling this will completely remove any feature from the volumetric fog from being rendered at all.")]
	public BoolParameter enabled = new BoolParameter(value: false, BoolParameter.DisplayType.Checkbox, overrideState: true);

	[Header("Render Pass Event")]
	[Tooltip("The URP render pass event to render the volumetric fog.")]
	public VolumetricFogRenderPassEventParameter renderPassEvent = new VolumetricFogRenderPassEventParameter(VolumetricFogRenderPassEvent.BeforeRenderingPostProcessing);

	private void OnValidate()
	{
		maximumHeight.overrideState = baseHeight.overrideState;
		maximumHeight.value = Mathf.Max(baseHeight.value, maximumHeight.value);
		baseHeight.value = Mathf.Min(baseHeight.value, maximumHeight.value);
	}

	public bool IsActive()
	{
		if (enabled.value && distance.value > 0f && groundHeight.value < maximumHeight.value)
		{
			return density.value > 0f;
		}
		return false;
	}
}
