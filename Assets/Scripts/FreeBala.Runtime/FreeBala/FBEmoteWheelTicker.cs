using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeBala
{
public class FBEmoteWheelTicker : MonoBehaviour
{
	private static readonly int __fbt_Update = FBTempo.Id("FBEmoteWheelTicker.Update");

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
		FBEmoteWheel.Tick();
		if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
		{
			FBEmoteWheel.Close(apply: true);
		}
	}
}
}
