using System;
using UnityEngine;

namespace FreeBala
{
public static class FBDamageEvents
{
	public static event Action<FBDamage> OnDamage;

	public static FBDamageType TypeOf(string why, FBHitInfo hit)
	{
		switch (why)
		{
		case "zone":
			return FBDamageType.Zone;
		case "fall":
			return FBDamageType.Fall;
		case "bleed":
			return FBDamageType.Bleed;
		case "car":
			return FBDamageType.Vehicle;
		case "takedown":
			return FBDamageType.Takedown;
		case "heli":
		case "explosion":
		case "gunner":
			return FBDamageType.Explosion;
		case null:
			return (!hit.valid) ? FBDamageType.Other : FBDamageType.Bullet;
		default:
			return FBDamageType.Environment;
		}
	}

	public static void Raise(in FBDamage d)
	{
		Action<FBDamage> onDamage = FBDamageEvents.OnDamage;
		if (onDamage == null)
		{
			return;
		}
		Delegate[] invocationList = onDamage.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			Action<FBDamage> action = (Action<FBDamage>)invocationList[i];
			try
			{
				action(d);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
	}
}
}
