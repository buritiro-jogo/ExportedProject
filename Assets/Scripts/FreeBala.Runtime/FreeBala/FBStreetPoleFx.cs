using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBStreetPoleFx
{
	public static void Shot(Collider c, Vector3 p, Vector3 dir)
	{
		int num = FBStreetLights.Nearest(p, 0.8f);
		if (num >= 0)
		{
			FBStreetLights.Kill(num, Random.Range(0.12f, 0.35f), hideGlowNow: false, shot: true);
		}
	}

	public static void Ray(Vector3 o, Vector3 dir, float dist)
	{
		IReadOnlyList<FBStreetLights.Lamp> lamps = FBStreetLights.Lamps;
		if (lamps.Count == 0)
		{
			return;
		}
		float num = 0.3f;
		int num2 = -1;
		for (int i = 0; i < lamps.Count; i++)
		{
			if (FBStreetLights.IsDead(i))
			{
				continue;
			}
			Vector3 vector = lamps[i].p - o;
			float num3 = Vector3.Dot(vector, dir);
			if (!(num3 < 0f) && !(num3 > dist))
			{
				float magnitude = (vector - dir * num3).magnitude;
				if (magnitude < num)
				{
					num = magnitude;
					num2 = i;
				}
			}
		}
		if (num2 >= 0)
		{
			FBStreetLights.Kill(num2, Random.Range(0.12f, 0.35f), hideGlowNow: false, shot: true);
		}
	}

	public static FBPole PoleOf(Collider c)
	{
		if (!c)
		{
			return null;
		}
		return c.GetComponentInParent<FBPole>();
	}
}
}
