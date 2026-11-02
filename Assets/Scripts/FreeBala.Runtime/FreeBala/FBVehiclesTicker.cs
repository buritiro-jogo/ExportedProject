using UnityEngine;

namespace FreeBala
{
public class FBVehiclesTicker : MonoBehaviour
{
	private static readonly int __fbt_Update = FBTempo.Id("FBVehiclesTicker.Update");

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
		FBVehicles.Tick(Time.deltaTime);
	}
}
}
