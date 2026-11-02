using UnityEngine;

namespace FreeBala
{
public class FBAudioTicker : MonoBehaviour
{
	private static readonly int __fbt_Update = FBTempo.Id("FBAudioTicker.Update");

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
		FBAudio.Tick(Time.deltaTime);
		Camera main = Camera.main;
		if ((bool)main && (bool)GetComponent<AudioListener>())
		{
			base.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
		}
	}
}
}
