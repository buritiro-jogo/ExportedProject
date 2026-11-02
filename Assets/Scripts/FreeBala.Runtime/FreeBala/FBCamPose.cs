using UnityEngine;

namespace FreeBala
{
[DefaultExecutionOrder(32000)]
public class FBCamPose : MonoBehaviour
{
	public Pose? pose;

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBCamPose.LateUpdate");

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
		Camera main = Camera.main;
		if ((bool)main && pose.HasValue)
		{
			main.transform.SetPositionAndRotation(pose.Value.position, pose.Value.rotation);
		}
	}
}
}
