using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBKillstreak
{
	public class Reward
	{
		public int kills;

		public float run;

		public float dmgMul;

		public float cooldown;

		public string name;
	}

	public static readonly Dictionary<string, Reward> STREAK = new Dictionary<string, Reward>
	{
		["caveirao"] = new Reward
		{
			kills = 10,
			run = 45f,
			dmgMul = 0.5f,
			cooldown = 6f,
			name = "CAVEIRÃO"
		},
		["heli"] = new Reward
		{
			kills = 15,
			run = 40f,
			dmgMul = 0.4f,
			cooldown = 6f,
			name = "HELICÓPTERO"
		}
	};

	public const float RevealDelay = 2f;

	public const float RevealMaxWait = 5f;

	public const float HoldToExit = 0.6f;

	public const bool ResetOnDeath = true;

	public const bool CountWhileGunner = false;

	public int kills;

	public string reward;

	public string pending;

	public float revealAt;

	public float firstKillAt;

	public float cooldownUntil;

	public FBKillstreak()
	{
		ResetState();
	}

	public void ResetState()
	{
		kills = 0;
		reward = null;
		pending = null;
		revealAt = 0f;
		firstKillAt = 0f;
	}

	public string Kill(float now, bool gunner = false)
	{
		if (gunner)
		{
			return null;
		}
		kills++;
		string text = null;
		if (kills >= STREAK["heli"].kills && reward != "heli")
		{
			text = "heli";
		}
		else if (kills >= STREAK["caveirao"].kills && reward == null)
		{
			text = "caveirao";
		}
		if (text != null)
		{
			reward = text;
			pending = text;
			if (firstKillAt == 0f)
			{
				firstKillAt = now;
			}
			revealAt = Mathf.Min(now + 2f, firstKillAt + 5f);
		}
		return text;
	}

	public string Tick(float now)
	{
		if (pending != null && now >= revealAt)
		{
			string result = pending;
			pending = null;
			firstKillAt = 0f;
			return result;
		}
		return null;
	}

	public int? Next()
	{
		if (!(reward == "heli"))
		{
			return (reward == "caveirao") ? STREAK["heli"].kills : STREAK["caveirao"].kills;
		}
		return null;
	}

	public bool Ready(float now)
	{
		if (reward != null && pending == null)
		{
			return now >= cooldownUntil;
		}
		return false;
	}

	public string Consume(float now)
	{
		string text = reward;
		if (text == null)
		{
			return null;
		}
		ResetState();
		cooldownUntil = now + STREAK[text].run + STREAK[text].cooldown;
		return text;
	}

	public void Died()
	{
		ResetState();
	}
}
}
