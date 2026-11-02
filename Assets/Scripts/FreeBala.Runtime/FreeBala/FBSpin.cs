using UnityEngine;

namespace FreeBala
{
public class FBSpin : MonoBehaviour
{
	public float speed = 25f;

	private static readonly int __fbt_Update = FBTempo.Id("FBSpin.Update");

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
		base.transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.World);
	}
}
}
