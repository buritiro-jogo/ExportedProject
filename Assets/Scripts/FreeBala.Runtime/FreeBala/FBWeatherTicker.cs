using UnityEngine;

namespace FreeBala
{
public class FBWeatherTicker : MonoBehaviour
{
	private static readonly int __fbt_Update = FBTempo.Id("FBWeatherTicker.Update");

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
		FBWeather.Tick(Time.deltaTime);
		FBAmbience.Tick(Time.deltaTime);
	}

	private void OnDestroy()
	{
		FBWeather.RestoreWet();
		FBWeather.RestoreLampMats();
		FBVolumetric.Clear();
		FBWeather.ResetGlobals();
	}
}
}
