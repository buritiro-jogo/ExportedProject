using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
public class FBDynRes : MonoBehaviour
{
	private static FBDynRes I;

	private float avg = 16f;

	private float cool;

	public static float Scale { get; private set; } = 1f;


	public static void Setup()
	{
		Scale = FBQuality.P.renderScale;
		if (!Application.isPlaying)
		{
			return;
		}
		if (!FBQuality.P.dynamicResolution)
		{
			if ((bool)I)
			{
				Object.Destroy(I.gameObject);
			}
		}
		else if (!I)
		{
			GameObject obj = new GameObject("FB_DynRes");
			Object.DontDestroyOnLoad(obj);
			obj.hideFlags = HideFlags.HideInHierarchy;
			I = obj.AddComponent<FBDynRes>();
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBDynRes.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		FBQProfile p = FBQuality.P;
		float num = Time.unscaledDeltaTime * 1000f;
		if (num > 250f)
		{
			return;
		}
		avg = Mathf.Lerp(avg, num, 0.05f);
		cool -= Time.unscaledDeltaTime;
		if (cool > 0f)
		{
			return;
		}
		float num2 = 1000f / (float)((p.targetFps > 0) ? p.targetFps : 60);
		float scale = Scale;
		if (avg > num2 * 1.15f)
		{
			scale -= 0.05f;
		}
		else
		{
			if (!(avg < num2 * 0.8f))
			{
				return;
			}
			scale += 0.025f;
		}
		scale = Mathf.Clamp(scale, p.minRenderScale, p.renderScale);
		if (!(Mathf.Abs(scale - Scale) < 0.001f))
		{
			Scale = scale;
			cool = 1.5f;
			if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset universalRenderPipelineAsset)
			{
				universalRenderPipelineAsset.renderScale = scale;
			}
		}
	}
}
}
