using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBCombatStats
{
	public static bool On;

	public static readonly List<(float d, bool hit)> botShots = new List<(float, bool)>();

	public static readonly List<(float d, string who, bool head)> playerHits = new List<(float, string, bool)>();

	public static void BotShot(float d)
	{
		if (On)
		{
			botShots.Add((d, false));
		}
	}

	public static void Damage(FBCharacter src, FBCharacter victim, bool head)
	{
		if (!On || !src || !victim || src == victim)
		{
			return;
		}
		float magnitude = (src.Pos - victim.Pos).magnitude;
		if (src.isBot && botShots.Count > 0)
		{
			int num = botShots.Count - 1;
			while (num >= 0 && num >= botShots.Count - 8)
			{
				if (!botShots[num].hit && Mathf.Abs(botShots[num].d - magnitude) < 3f)
				{
					botShots[num] = (botShots[num].d, true);
					break;
				}
				num--;
			}
		}
		if (victim.isPlayer)
		{
			playerHits.Add((magnitude, src.Name, head));
		}
	}
}
}
