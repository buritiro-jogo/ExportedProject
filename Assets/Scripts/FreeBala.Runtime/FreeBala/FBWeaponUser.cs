using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBWeaponUser : MonoBehaviour
{
	public const int HANDS = 3;

	[NonSerialized]
	public FBCharacter ch;

	public readonly WeaponDef[] slots = new WeaponDef[3];

	public readonly int[] ammo = new int[3];

	public readonly int[] reserve = new int[3];

	public int cur = 3;

	public int pending = -1;

	public float switching;

	public float cool;

	public float reloadT;

	public float boltT;

	[NonSerialized]
	public FBRecoil recoil = new FBRecoil();

	private int shotsBurst;

	public Action onEquip;

	private float shellT = -1f;

	private int shellsLeft;

	public WeaponDef Cur
	{
		get
		{
			if (cur >= 3)
			{
				return null;
			}
			return slots[cur];
		}
	}

	public int Ammo
	{
		get
		{
			if (cur >= 3)
			{
				return 0;
			}
			return ammo[cur];
		}
	}

	public int Reserve
	{
		get
		{
			if (cur >= 3)
			{
				return 0;
			}
			return reserve[cur];
		}
	}

	public bool Unarmed => Cur == null;

	public bool HasAny
	{
		get
		{
			if (slots[0] == null && slots[1] == null)
			{
				return slots[2] != null;
			}
			return true;
		}
	}

	private void Awake()
	{
		ch = GetComponent<FBCharacter>();
	}

	public bool Has(string id)
	{
		WeaponDef[] array = slots;
		foreach (WeaponDef weaponDef in array)
		{
			if (weaponDef != null && weaponDef.id == id)
			{
				return true;
			}
		}
		return false;
	}

	public int SlotOfId(string id)
	{
		for (int i = 0; i < 3; i++)
		{
			if (slots[i] != null && slots[i].id == id)
			{
				return i;
			}
		}
		return -1;
	}

	private static GameObject Model(WeaponDef d)
	{
		if (d == null || !FBRegistry.I)
		{
			return null;
		}
		return FBRegistry.I.Weapon(d.id);
	}

	public void Give(params string[] ids)
	{
		int num = -1;
		foreach (string text in ids)
		{
			if (text != null && GameData.Weapons.TryGetValue(text, out var value) && (bool)Model(value))
			{
				int num2 = Mathf.Clamp(value.slot, 0, 2);
				slots[num2] = value;
				ammo[num2] = value.mag;
				reserve[num2] = value.mag * ((value.cls == "explosivo") ? 3 : 5);
				num = num2;
			}
		}
		int i2 = ((ids.Length <= 1) ? ((num >= 0) ? num : ((slots[0] == null) ? ((slots[1] != null) ? 1 : ((slots[2] != null) ? 2 : 3)) : 0)) : ((slots[0] == null) ? ((slots[1] != null) ? 1 : ((slots[2] != null) ? 2 : 3)) : 0));
		Equip(i2, ids.Length != 1 || ch.isBot);
	}

	public void Strip()
	{
		for (int i = 0; i < 3; i++)
		{
			slots[i] = null;
			ammo[i] = (reserve[i] = 0);
		}
		Equip(3, instant: true);
	}

	public void RefillAll(int mags)
	{
		for (int i = 0; i < 3; i++)
		{
			if (slots[i] != null)
			{
				ammo[i] = slots[i].mag;
				reserve[i] = Mathf.Max(reserve[i], slots[i].mag * mags);
			}
		}
		reloadT = 0f;
	}

	public void Equip(int i, bool instant = false)
	{
		if (i < 3 && slots[i] == null)
		{
			i = 3;
		}
		pending = -1;
		switching = 0f;
		cur = i;
		recoil.Reset();
		reloadT = 0f;
		WeaponDef weaponDef = Cur;
		ch.SetWeapon(weaponDef, Model(weaponDef));
		if (ch.vehiclePose)
		{
			// Correção: troca de arma que termina já dentro do carro ou na moto não aparece na mão do piloto.
			ch.SetWeaponVisible(v: false);
			instant = true;
		}
		ch.combat = weaponDef != null && !ch.vehiclePose;
		List<(WeaponDef, GameObject)> list = new List<(WeaponDef, GameObject)>();
		for (int j = 0; j < 3; j++)
		{
			if (j != cur && slots[j] != null)
			{
				list.Add((slots[j], Model(slots[j])));
			}
		}
		ch.SetHolsters(list);
		if (!instant && weaponDef != null)
		{
			ch.DrawAnim();
			PlayAt(FBWeaponSound.Draw(weaponDef) ?? S(weaponDef, "Unholster") ?? "unholster", 0.75f);
		}
		onEquip?.Invoke();
	}

	public void SwitchTo(int i)
	{
		switch (i)
		{
		case 0:
		case 1:
		case 2:
			if (slots[i] == null)
			{
				break;
			}
			goto case 3;
		case 3:
			if (i != cur && !(switching > 0f) && ch.alive)
			{
				if (ch.isPlayer && FBMed.Busy)
				{
					FBMed.Cancel();
					FBAudio.Synth("medcancel");
				}
				reloadT = 0f;
				pending = i;
				float b = ((Cur != null) ? ch.HolsterAnim() : 0f);
				switching = ((ch.isPlayer && (bool)FBPlayer.I && FBPlayer.I.firstPerson) ? 0.26f : Mathf.Max(0.14f, b));
				if (Cur != null)
				{
					PlayAt(FBWeaponSound.Holster(Cur) ?? S(Cur, "Holster") ?? "unholster", 0.6f);
				}
			}
			break;
		}
	}

	public void Cycle(int dir)
	{
		List<int> list = new List<int>();
		for (int i = 0; i < 3; i++)
		{
			if (slots[i] != null)
			{
				list.Add(i);
			}
		}
		list.Add(3);
		int num = Mathf.Max(0, list.IndexOf(cur));
		SwitchTo(list[(num + ((dir > 0) ? 1 : (-1)) + list.Count) % list.Count]);
	}

	public bool Reload()
	{
		WeaponDef weaponDef = Cur;
		if (weaponDef == null || reloadT > 0f || switching > 0f || ammo[cur] >= weaponDef.mag || reserve[cur] <= 0 || !ch.alive)
		{
			return false;
		}
		bool flag = ammo[cur] == 0;
		reloadT = (weaponDef.cycled ? (0.5f + 0.45f * (float)Mathf.Min(weaponDef.mag - ammo[cur], reserve[cur])) : (weaponDef.IsPistol ? 1.6f : (weaponDef.IsSniper ? 2.6f : ((weaponDef.cls == "escopeta") ? 2.4f : 2.2f))));
		if (flag && !weaponDef.cycled)
		{
			reloadT += 0.35f;
		}
		ch.Trigger("reload");
		shellT = -1f;
		shellsLeft = 0;
		if (weaponDef.cycled && FBWeaponSound.Insert(weaponDef) != null)
		{
			shellsLeft = Mathf.Min(weaponDef.mag - ammo[cur], reserve[cur]);
			shellT = 0.3f;
		}
		else
		{
			PlayAt(FBWeaponSound.ReloadOf(weaponDef) ?? S(weaponDef, flag ? "ReloadEmpty" : "Reload") ?? FBWeaponSound.Reload(weaponDef) ?? (flag ? "reload_empty" : "reload"), 0.7f);
		}
		if (ch.isPlayer && FBMed.Busy)
		{
			FBMed.Cancel();
			FBAudio.Synth("medcancel");
		}
		return true;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBWeaponUser.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		float deltaTime = Time.deltaTime;
		cool -= deltaTime;
		if (boltT > 0f)
		{
			boltT -= deltaTime;
		}
		if (switching > 0f)
		{
			switching -= deltaTime;
			if (switching <= 0f && pending >= 0)
			{
				Equip(pending);
			}
		}
		if (reloadT > 0f)
		{
			reloadT -= deltaTime;
			if (shellT > 0f && Cur != null && (shellT -= deltaTime) <= 0f)
			{
				if (shellsLeft-- > 0)
				{
					PlayAt(FBWeaponSound.Insert(Cur), 0.65f);
					shellT = 0.45f;
				}
				else
				{
					PlayAt(FBWeaponSound.Close(Cur), 0.7f);
					shellT = -1f;
				}
			}
			if (reloadT <= 0f && Cur != null)
			{
				int num = Cur.mag - ammo[cur];
				int num2 = (ch.isBot ? num : Mathf.Min(num, reserve[cur]));
				ammo[cur] += num2;
				if (!ch.isBot)
				{
					reserve[cur] -= num2;
				}
			}
		}
		if (ch.isBot && Cur != null && reserve[cur] < Cur.mag)
		{
			reserve[cur] = Cur.mag * 3;
		}
	}

	public bool Fire(Vector3 origin, Vector3 dir, FBRecoil.State st, out Vector3 hitPoint, float extraSpread = 0f, Vector3? muzzleFrom = null, float near = 0f, Func<Vector3, Vector3, Vector3> aimTo = null, Vector3? shotFrom = null)
	{
		hitPoint = origin + dir * 200f;
		WeaponDef weaponDef = Cur;
		if (weaponDef == null || cool > 0f || switching > 0f || reloadT > 0f || !ch.alive || (weaponDef.bolt && boltT > 0f) || ch.DrawBusy)
		{
			return false;
		}
		if (ammo[cur] <= 0)
		{
			if (cool <= 0f)
			{
				PlayAt(FBWeaponSound.Empty(weaponDef) ?? S(weaponDef, "FireEmpty") ?? "empty", 0.8f);
				cool = 0.3f;
			}
			if (ch.isBot || reserve[cur] > 0)
			{
				Reload();
			}
			return false;
		}
		ammo[cur]--;
		cool = weaponDef.Interval;
		shotsBurst++;
		if (weaponDef.bolt)
		{
			boltT = weaponDef.Interval;
		}
		ch.lastShot = 0f;
		ch.shooting = true;
		ch.protUntil = 0f;
		if (ch.isPlayer)
		{
			FBMatch.Stats.shots++;
		}
		bool flag = FBWeaponSound.Silenced(weaponDef);
		Vector3 muzzlePos = ch.MuzzlePos;
		FBFx.Muzzle(muzzlePos, (ch.isPlayer && (bool)FBPlayer.I && FBPlayer.I.firstPerson) ? 0.16f : 0.55f, weaponDef.cls, flag);
		if (!flag)
		{
			FBBotBrain.Alert(ch.Pos, ch.isPlayer ? 60 : 45, ch.team);
		}
		FBBotBrain.Noise(ch.Pos);
		float num = ((extraSpread <= -50f) ? 0f : (recoil.Spread(weaponDef, st) + extraSpread));
		Vector3 normalized = Vector3.Cross(dir, (Mathf.Abs(dir.y) > 0.95f) ? Vector3.right : Vector3.up).normalized;
		Vector3 vector = Vector3.Cross(normalized, dir);
		Vector3 valueOrDefault = muzzleFrom.GetValueOrDefault(muzzlePos);
		for (int i = 0; i < Mathf.Max(1, weaponDef.pellets); i++)
		{
			float f = UnityEngine.Random.value * (float)Math.PI * 2f;
			float num2 = Mathf.Sqrt(UnityEngine.Random.value) * Mathf.Tan(num * ((float)Math.PI / 180f));
			Vector3 normalized2 = (dir + normalized * Mathf.Cos(f) * num2 + vector * Mathf.Sin(f) * num2).normalized;
			if (aimTo != null)
			{
				Vector3 vector2 = aimTo(origin, normalized2);
				Vector3 valueOrDefault2 = shotFrom.GetValueOrDefault(muzzlePos);
				Vector3 vector3 = vector2 - valueOrDefault2;
				float magnitude = vector3.magnitude;
				if (magnitude > 0.05f)
				{
					FBCombat.Shot shot = FBCombat.BulletFrom(valueOrDefault2, vector3 / magnitude, valueOrDefault, ch, weaponDef, 0f, i < 3, magnitude + 0.05f, weaponDef.pellets);
					if (i == 0)
					{
						hitPoint = shot.end;
					}
					continue;
				}
			}
			FBCombat.Shot shot2 = FBCombat.BulletFrom(origin, normalized2, valueOrDefault, ch, weaponDef, near, i < 3, 300f, weaponDef.pellets);
			if (i == 0)
			{
				hitPoint = shot2.end;
			}
		}
		recoil.Shot(weaponDef, st);
		string text = FBWeaponSound.Fire(weaponDef, flag);
		string text2 = (flag ? null : FBWeaponSound.Tail(weaponDef));
		if (ch.isPlayer)
		{
			FBAudio.Play2D(text, flag ? 0.6f : 0.85f, 0.06f);
			if (text2 != null)
			{
				FBAudio.Play2D(text2, 0.35f, 0.06f);
			}
		}
		else
		{
			float num3 = (Camera.main ? Vector3.Distance(Camera.main.transform.position, muzzlePos) : 0f);
			if (flag)
			{
				if (num3 < 45f)
				{
					FBAudio.PlayShotAt(text, muzzlePos, 0.55f, 5f, 1.2f);
				}
			}
			else
			{
				float num4 = Mathf.Clamp01((110f - num3) / 60f);
				float num5 = Mathf.Clamp01((num3 - 30f) / 50f);
				if (num4 > 0.02f)
				{
					FBAudio.PlayShotAt(text, muzzlePos, 0.95f * num4, 15f, 1f);
				}
				if (num5 > 0.02f)
				{
					FBAudio.PlayShotAt(FBWeaponSound.Far(weaponDef) ?? "shot_far", muzzlePos, 0.95f * num5, 40f, 1f);
				}
			}
		}
		FBCasings.Eject(ch, weaponDef);
		return true;
	}

	private static string S(WeaponDef d, string k)
	{
		return Json.S(d.sounds, k);
	}

	private void PlayAt(string name, float vol)
	{
		if (!string.IsNullOrEmpty(name))
		{
			if (ch.isPlayer)
			{
				FBAudio.Play2D(name, vol);
			}
			else
			{
				FBAudio.PlayAt(name, ch.Pos + Vector3.up, vol);
			}
		}
	}
}
}
