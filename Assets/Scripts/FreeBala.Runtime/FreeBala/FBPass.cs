using UnityEngine;

namespace FreeBala
{
public static class FBPass
{
	public const int PER_TIER = 1000;

	public const int TIERS = 30;

	public static int Tier => Mathf.Min(30, FBSession.passXp / 1000);

	public static int Claimed
	{
		get
		{
			return PlayerPrefs.GetInt("fb_passClaimed", 0);
		}
		set
		{
			PlayerPrefs.SetInt("fb_passClaimed", value);
		}
	}

	public static int Claimable()
	{
		return Mathf.Max(0, Mathf.Min(30, Tier + 1) - Claimed);
	}

	public static void AddXP(int v, string why)
	{
		int tier = Tier;
		FBSession.passXp += v;
		if (Tier > tier)
		{
			FBHUD.I?.Toast("PASSE DE BATALHA", $"NÍVEL {Tier}", "RECOMPENSA LIBERADA NO MENU", new Color(0.96f, 0.67f, 0.09f));
		}
	}
}
}
