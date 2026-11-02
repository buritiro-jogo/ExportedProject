using UnityEngine;

namespace FreeBala
{
public class FBPropBody : MonoBehaviour
{
	public string mat;

	public string snd;

	public Rigidbody rb;

	private float lastT;

	private static readonly Collider[] near = new Collider[8];

	private float chkT;

	private void OnEnable()
	{
		FBFisica.All.Add(this);
	}

	private void OnDisable()
	{
		FBFisica.All.Remove(this);
	}

	private void OnCollisionEnter(Collision c)
	{
		if ((bool)rb && !(Time.time - lastT < 0.12f))
		{
			float num = c.impulse.magnitude / Mathf.Max(0.2f, rb.mass);
			if (!(num < 0.45f))
			{
				lastT = Time.time;
				FBFisica.Impacts++;
				Vector3 p = ((c.contactCount > 0) ? c.GetContact(0).point : base.transform.position);
				FBFisica.Sound(snd, p, Mathf.Clamp01(0.15f + num * 0.12f) * Mathf.Clamp(rb.mass / 4f, 0.5f, 1.2f), 4f + Mathf.Min(6f, rb.mass * 0.5f));
			}
		}
	}

	private static readonly int __fbt_FixedUpdate = FBTempo.Id("FBPropBody.FixedUpdate");

	private void FixedUpdate()
	{
		if (!FBTempo.On)
		{
			FixedUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		FixedUpdate__fbt();
		FBTempo.Soma(__fbt_FixedUpdate, t0);
	}

	private void FixedUpdate__fbt()
	{
		if (!rb || rb.isKinematic || (chkT -= Time.fixedDeltaTime) > 0f)
		{
			return;
		}
		chkT = 0.1f;
		Vector3 position = base.transform.position;
		int num = Physics.OverlapSphereNonAlloc(position, 0.9f, near, 1024, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < num; i++)
		{
			FBCharacter componentInParent = near[i].GetComponentInParent<FBCharacter>();
			if (!componentInParent || componentInParent.vehiclePose)
			{
				continue;
			}
			Vector3 rhs = componentInParent.velocity.Flat();
			// fisica 31/10: bot publica em ch.velocity o deslocamento real (~0 quando o objeto o trava); empurra com a velocidade pedida
			FBBot component = componentInParent.GetComponent<FBBot>();
			if ((bool)component && component.enabled)
			{
				Vector3 wishVel = component.WishVel;
				if (wishVel.sqrMagnitude > rhs.sqrMagnitude)
				{
					rhs = wishVel;
				}
			}
			if (!(rhs.sqrMagnitude < 0.5f))
			{
				Vector3 lhs = (position - componentInParent.Pos).Flat();
				if (!(Vector3.Dot(lhs, rhs) <= 0f) && !(lhs.magnitude > 0.75f))
				{
					rb.WakeUp();
					rb.AddForceAtPosition(rhs.normalized * Mathf.Min(rhs.magnitude, 6f) * Mathf.Min(rb.mass, 12f) * 0.35f, position + Vector3.up * 0.35f, ForceMode.Impulse);
				}
			}
		}
	}
}
}
