using UnityEngine;

namespace FreeBala
{
public class FBFxTicker : MonoBehaviour
{
	private static readonly int __fbt_Update = FBTempo.Id("FBFxTicker.Update");

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
		float deltaTime = Time.deltaTime;
		FBFx.Tick(deltaTime);
		FBDecalFx.Tick(deltaTime);
		FBExplosionFx.Tick(deltaTime);
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBFxTicker.LateUpdate");

	private void LateUpdate()
	{
		if (!FBTempo.On)
		{
			LateUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		LateUpdate__fbt();
		FBTempo.Soma(__fbt_LateUpdate, t0);
	}

	private void LateUpdate__fbt()
	{
		FBWoundFx.Tick(Time.deltaTime);
	}
}
}
