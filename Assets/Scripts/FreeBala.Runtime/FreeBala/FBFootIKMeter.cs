using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBFootIKMeter
{
	public readonly List<float> contact = new List<float>();

	public readonly List<float> slip = new List<float>();

	public readonly List<float> jitter = new List<float>();

	public int edgeSkips;

	private readonly Vector3[] prevA = new Vector3[2];

	private readonly float[] prevLock = new float[2];

	private float y1;

	private float y2;

	private int hipsN;

	private static readonly RaycastHit[] hits = new RaycastHit[6];

	public void Reset()
	{
		contact.Clear();
		slip.Clear();
		jitter.Clear();
		edgeSkips = 0;
		hipsN = 0;
		prevLock[0] = (prevLock[1] = 0f);
	}

	public void Sample(FBCharacter ch, float dt)
	{
		if (!ch || !ch.feet || dt <= 0f)
		{
			return;
		}
		FBFootIK feet = ch.feet;
		if (feet.Weight < 0.9f)
		{
			hipsN = 0;
			prevLock[0] = (prevLock[1] = 0f);
			return;
		}
		for (int i = 0; i < 2; i++)
		{
			FBFootIK.Foot foot = ((i == 0) ? feet.Left : feet.Right);
			Vector3 ankle = foot.Ankle;
			if (foot.Hit && foot.Plant > 0.85f && foot.RestH > 0f && Ray(ch.transform, ankle, out var y))
			{
				if (Mathf.Abs(y - foot.GroundY) > 0.04f * ch.ScaleK)
				{
					edgeSkips++;
				}
				else
				{
					contact.Add(ankle.y - foot.RestH / Mathf.Max(0.8f, foot.NormalS.y) - y);
				}
			}
			if (foot.LockW >= 0.99f && prevLock[i] >= 0.99f)
			{
				slip.Add((ankle - prevA[i]).Flat().magnitude / dt);
			}
			prevA[i] = ankle;
			prevLock[i] = foot.LockW;
		}
		if ((bool)ch.Hips)
		{
			float y2 = ch.Hips.position.y;
			if (hipsN >= 2)
			{
				jitter.Add(Mathf.Abs(y2 - 2f * y1 + this.y2));
			}
			this.y2 = y1;
			y1 = y2;
			hipsN++;
		}
	}

	private static bool Ray(Transform self, Vector3 a, out float y)
	{
		y = 0f;
		int num = Physics.RaycastNonAlloc(new Vector3(a.x, a.y + 0.5f, a.z), Vector3.down, hits, 1.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		float num2 = 1E+09f;
		for (int i = 0; i < num; i++)
		{
			RaycastHit raycastHit = hits[i];
			if ((bool)raycastHit.collider && !raycastHit.collider.transform.IsChildOf(self) && !(raycastHit.distance >= num2))
			{
				num2 = raycastHit.distance;
				y = raycastHit.point.y;
			}
		}
		return num2 < 1E+09f;
	}

	public static float P(List<float> l, float q, bool abs = false)
	{
		if (l.Count == 0)
		{
			return 0f;
		}
		List<float> list = new List<float>(l.Count);
		foreach (float item in l)
		{
			list.Add(abs ? Mathf.Abs(item) : item);
		}
		list.Sort();
		return list[Mathf.Clamp(Mathf.RoundToInt(q * (float)(list.Count - 1)), 0, list.Count - 1)];
	}

	public string Report()
	{
		return $"contato p95 {P(contact, 0.95f, abs: true) * 100f:0.0} cm (pen. p05 {P(contact, 0.05f) * 100f:+0.0;-0.0} cm, flut. p95 {P(contact, 0.95f) * 100f:+0.0;-0.0} cm, n {contact.Count}, quinas {edgeSkips}) · " + $"desliza p95 {P(slip, 0.95f) * 100f:0.0} cm/s (n {slip.Count}) · tremor quadril p95 {P(jitter, 0.95f) * 1000f:0.0} mm/q² máx {P(jitter, 1f) * 1000f:0.0}";
	}
}
}
