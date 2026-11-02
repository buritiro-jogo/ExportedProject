using UnityEngine;

namespace FreeBala
{
public static class FBBotBrain
{
	private class Intel
	{
		public Vector3 p;

		public float t;
	}

	private static readonly Intel[] INTEL = new Intel[2];

	public static Vector3? lastNoiseP;

	public static float lastNoiseT = -99f;

	public static void TeamIntel(int team, Vector3 p)
	{
		if (team == 0 || team == 1)
		{
			INTEL[team] = new Intel
			{
				p = p,
				t = Time.time
			};
		}
	}

	public static Vector3? FreshIntel(int team, float s = 9f)
	{
		if (team < 0 || team > 1)
		{
			return null;
		}
		Intel intel = INTEL[team];
		if (intel == null || !(Time.time - intel.t < s))
		{
			return null;
		}
		return intel.p;
	}

	public static void Noise(Vector3 p)
	{
		lastNoiseP = p;
		lastNoiseT = Time.time;
	}

	public static void Alert(Vector3 p, float r, int team)
	{
		foreach (FBCharacter bot in FBMatch.Bots)
		{
			if ((bool)bot && bot.alive)
			{
				FBBot component = bot.GetComponent<FBBot>();
				if ((bool)component && (FBMatch.FFA || bot.team != team) && bot.team < 2 && component.ai != "combat" && component.ai != "cover" && component.ai != "dummy" && component.ai != "guard" && (bot.Pos - p).magnitude < r)
				{
					Vector3 vector = p + Random.insideUnitSphere.Flat() * (bot.Pos - p).magnitude * 0.12f;
					component.lastSeen = vector;
					component.goalP = component.FlankPoint(vector);
					component.goalT = FBUtil.Rnd(10f, 16f);
					component.pauseT = 0f;
					TeamIntel(bot.team, vector);
				}
			}
		}
	}

	public static void Reset()
	{
		INTEL[0] = (INTEL[1] = null);
		lastNoiseP = null;
	}
}
}
