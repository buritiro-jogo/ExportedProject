using System.Linq;
using UnityEngine;

namespace FreeBala
{
public class FBKnockdown : MonoBehaviour
{
	private FBRagdoll rd;

	private float t;

	private FBCharacter c;

	private bool up;

	public void Begin(FBRagdoll r, float secs)
	{
		rd = r;
		t = secs;
		c = GetComponent<FBCharacter>();
		up = false;
		base.enabled = true;
		CharacterController component = c.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		c.fleeing = true;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBKnockdown.Update");

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
		if (!c || !c.alive)
		{
			Object.Destroy(this);
		}
		else if (up)
		{
			if (c.dance == null)
			{
				CharacterController component = c.GetComponent<CharacterController>();
				if ((bool)component)
				{
					component.enabled = true;
				}
				c.fleeing = false;
				FBAtropelo.GotUp++;
				Object.Destroy(this);
			}
		}
		else if (!((t -= Time.deltaTime) > 0f))
		{
			Transform hips = c.Hips;
			bool flag = (bool)hips && Vector3.Dot(hips.forward, Vector3.up) > 0f;
			Vector3 vector = (hips ? hips.position : c.Pos);
			Vector3 vector2 = (hips ? Vector3.ProjectOnPlane(flag ? (-hips.up) : hips.up, Vector3.up) : c.Fwd);
			if (vector2.sqrMagnitude < 0.001f)
			{
				vector2 = c.Fwd;
			}
			Vector3 p = vector;
			FBStreetEvents.Ground(ref p, vector.y, 2.5f);
			rd.Clear();
			c.transform.SetPositionAndRotation(new Vector3(vector.x, p.y, vector.z), Quaternion.LookRotation(vector2.normalized, Vector3.up));
			string text = ((!flag) ? new string[2] { "X171_HM_Levantando", "FF_SYS_KO_GetUp" } : new string[3] { "FF_SYS_KO_GetUp", "IE_Downed_GetUp", "FF_SYS_REB_Levantar" }).FirstOrDefault((string x) => (bool)FBRegistry.I && (bool)FBRegistry.I.Clip(x));
			if (text != null)
			{
				c.PlayDance(text);
			}
			up = true;
			FBNpcVoz.Say(c, "atropelado", 1f, 6f, 2f, 0f, 0.8f);
		}
	}
}
}
