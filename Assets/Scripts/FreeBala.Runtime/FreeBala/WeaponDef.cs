using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class WeaponDef
{
	public string id;

	public string name;

	public string cls;

	public int slot;

	public bool auto;

	public bool bolt;

	public bool cycled;

	public int pellets;

	public int mag;

	public float rpm;

	public float spread;

	public float recoilScale;

	public float move;

	public float[] dmg;

	public float[] dmgHead;

	public Dictionary<string, object> sounds;

	public List<object> fire;

	public List<object> tail;

	public float Interval => 60f / Mathf.Max(1f, rpm);

	public bool IsPistol => cls == "pistola";

	public bool IsSniper => cls == "sniper";

	public float Damage(float dist, bool head)
	{
		float[] array = (head ? dmgHead : dmg);
		float[] dist2 = GameData.Dist;
		if (array == null || array.Length == 0)
		{
			return 20f;
		}
		for (int i = 1; i < dist2.Length; i++)
		{
			if (dist <= dist2[i])
			{
				return Mathf.Lerp(array[i - 1], array[i], Mathf.InverseLerp(dist2[i - 1], dist2[i], dist));
			}
		}
		return array[array.Length - 1];
	}
}
}
