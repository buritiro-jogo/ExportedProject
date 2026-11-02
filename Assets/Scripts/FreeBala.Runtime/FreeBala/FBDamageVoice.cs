using UnityEngine;

namespace FreeBala
{
public static class FBDamageVoice
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void Hook()
	{
		FBDamageEvents.OnDamage -= On;
		FBDamageEvents.OnDamage += On;
	}

	private static void On(FBDamage d)
	{
		if (d.Local && (bool)d.victim)
		{
			FBVoice.OnDamage(in d);
		}
	}
}
}
