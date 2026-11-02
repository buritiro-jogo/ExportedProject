using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light), typeof(UniversalAdditionalLightData))]
public sealed class VolumetricAdditionalLight : MonoBehaviour
{
	[Tooltip("Higher positive values will make the fog affected by this light to appear brighter when directly looking to it, while lower negative values will make the fog to appear brighter when looking away from it. The closer the value is closer to 1 or -1, the less the brightness will spread. Most times, positive values higher than 0 and lower than 1 should be used.")]
	[Range(-1f, 1f)]
	[SerializeField]
	private float anisotropy = 0.25f;

	[Tooltip("Higher values will make fog affected by this light to appear brighter.")]
	[Range(0f, 16f)]
	[SerializeField]
	private float scattering = 1f;

	[Tooltip("Sets a falloff radius for this light. A higher value reduces noise towards the origin of the light.")]
	[Range(0f, 1f)]
	[SerializeField]
	private float radius = 0.2f;

	public float Anisotropy
	{
		get
		{
			return anisotropy;
		}
		set
		{
			anisotropy = Mathf.Clamp(value, -1f, 1f);
		}
	}

	public float Scattering
	{
		get
		{
			return scattering;
		}
		set
		{
			scattering = Mathf.Clamp(value, 0f, 16f);
		}
	}

	public float Radius
	{
		get
		{
			return radius;
		}
		set
		{
			radius = Mathf.Clamp01(value);
		}
	}
}
