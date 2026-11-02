using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBRemote : MonoBehaviour
{
	private struct Snap
	{
		public float at;

		public float yaw;

		public float ayaw;

		public float pitch;

		public float lean;

		public float hp;

		public Vector3 p;

		public Vector3 v;

		public int f;

		public string w;

		public string dn;

		public List<object> ve;
	}

	public int netId;

	public FBCharacter ch;

	public string displayName;

	[NonSerialized]
	public float aimYaw;

	[NonSerialized]
	public float aimPitch;

	private readonly List<Snap> buf = new List<Snap>();

	private string curW = "\u0001";

	private string curDn = "";

	private string lastW;

	private string lastDn = "";

	private bool hadVe;

	private bool extrap;

	private float lastAt;

	private float gapAvg = 0.05f;

	private float gapJit = 0.01f;

	private float blend;

	public float Delay => Mathf.Clamp(gapAvg + 2f * gapJit + 0.04f, 0.08f, 0.3f);

	public int Buffered => buf.Count;

	public void Push(Dictionary<string, object> m)
	{
		float time = Time.time;
		if (lastAt > 0f)
		{
			float num = time - lastAt;
			if (num < 0.15f)
			{
				gapAvg = Mathf.Lerp(gapAvg, num, 0.1f);
				gapJit = Mathf.Lerp(gapJit, Mathf.Abs(num - gapAvg), 0.1f);
			}
		}
		lastAt = time;
		if (m.ContainsKey("w"))
		{
			lastW = Json.S(m, "w");
		}
		if (m.ContainsKey("dn"))
		{
			lastDn = Json.S(m, "dn") ?? "";
		}
		Snap snap = default(Snap);
		snap.at = time;
		snap.p = Coords.FromWeb(Json.L(m, "p"));
		snap.v = Coords.FromWeb(Json.L(m, "v"));
		snap.yaw = Coords.YawFromWeb(Json.F(m, "y"));
		snap.f = Json.I(m, "f");
		snap.w = lastW;
		snap.dn = lastDn;
		snap.hp = Json.F(m, "hp", 100f);
		snap.ve = Json.L(m, "ve");
		Snap item = snap;
		item.ayaw = (m.ContainsKey("ay") ? Coords.YawFromWeb(Json.F(m, "ay")) : item.yaw);
		item.pitch = Json.F(m, "pi") * 57.29578f;
		item.lean = Json.F(m, "ln");
		buf.Add(item);
		if (buf.Count > 30)
		{
			buf.RemoveAt(0);
		}
	}

	public void Shot()
	{
		if (ch.weapon != null)
		{
			ch.lastShot = 0f;
			FBAudio.PlayAt((ch.weapon.fire != null && ch.weapon.fire.Count > 0) ? (ch.weapon.fire[UnityEngine.Random.Range(0, ch.weapon.fire.Count)] as string) : "disparo1", ch.MuzzlePos, 0.8f, 6f);
			FBFx.Muzzle(ch.MuzzlePos, 0.55f, ch.weapon.cls);
			FBBotBrain.Noise(ch.Pos);
		}
	}

	public void Teleport(Vector3 p)
	{
		buf.Clear();
		base.transform.position = p;
		extrap = false;
		blend = 0f;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBRemote.Update");

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
		if (buf.Count == 0)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		float num = Time.time - Delay;
		while (buf.Count > 2 && buf[1].at <= num)
		{
			buf.RemoveAt(0);
		}
		Snap snap = buf[0];
		Snap snap2 = ((buf.Count > 1) ? buf[1] : snap);
		float num2 = ((snap2.at > snap.at) ? Mathf.Clamp01((num - snap.at) / (snap2.at - snap.at)) : 1f);
		List<object> list = snap2.ve;
		if (snap.ve != null && snap2.ve != null && snap.ve.Count == snap2.ve.Count && snap.ve.Count >= 12 && object.Equals(snap.ve[0]?.ToString(), snap2.ve[0]?.ToString()))
		{
			list = LerpVe(snap.ve, snap2.ve, num2);
		}
		if (list != null)
		{
			hadVe = FBDrive.ApplyRemote(ch, list, deltaTime);
			if (hadVe)
			{
				return;
			}
		}
		else if (hadVe)
		{
			hadVe = false;
			FBDrive.Release(ch);
		}
		bool flag = buf.Count == 1 || num > snap2.at;
		Vector3 vector;
		if (!flag)
		{
			vector = ((!((snap2.p - snap.p).sqrMagnitude > 36f)) ? Vector3.Lerp(snap.p, snap2.p, num2) : ((num2 < 0.5f) ? snap.p : snap2.p));
		}
		else
		{
			float num3 = Mathf.Clamp(num - snap2.at, 0f, 0.25f);
			Vector3 v = snap2.v;
			if (((uint)snap2.f & 0x10u) != 0)
			{
				v.y = 0f;
			}
			vector = snap2.p + v * num3;
		}
		if (extrap && !flag)
		{
			blend = 0.15f;
		}
		extrap = flag;
		if (blend > 0f)
		{
			blend -= deltaTime;
			vector = Vector3.Lerp(base.transform.position, vector, 1f - Mathf.Exp((0f - deltaTime) * 18f));
		}
		base.transform.position = vector;
		base.transform.rotation = Quaternion.Euler(0f, flag ? snap2.yaw : Mathf.LerpAngle(snap.yaw, snap2.yaw, num2), 0f);
		aimYaw = Mathf.LerpAngle(snap.ayaw, snap2.ayaw, num2);
		aimPitch = Mathf.Lerp(snap.pitch, snap2.pitch, num2);
		ch.velocity = (flag ? snap2.v : Vector3.Lerp(snap.v, snap2.v, num2));
		int f = snap2.f;
		ch.lean = snap2.lean;
		ch.sprint = (f & 1) != 0;
		ch.crouch = (f & 2) != 0;
		ch.aiming = (f & 4) != 0;
		ch.shooting = (f & 8) != 0;
		ch.grounded = (f & 0x10) != 0;
		bool flag2 = (f & 0x20) != 0;
		if (flag2 && !ch.alive)
		{
			ch.Revive(snap2.hp);
		}
		ch.hp = snap2.hp;
		ch.combat = ch.aiming || ch.shooting;
		if (snap2.w != curW)
		{
			curW = snap2.w;
			GameData.Weapons.TryGetValue(curW ?? "", out var value);
			FBWeaponUser component = ch.GetComponent<FBWeaponUser>();
			if (value != null)
			{
				component.Give(value.id);
			}
			else
			{
				component.Strip();
			}
		}
		if (snap2.dn != curDn && (flag2 || snap2.dn == ""))
		{
			curDn = snap2.dn;
			if (curDn == "")
			{
				ch.StopDance(force: true);
			}
			else
			{
				ch.PlayDance(curDn);
			}
		}
	}

	public static List<object> LerpVe(List<object> a, List<object> b, float k)
	{
		List<object> list = new List<object>(b.Count);
		for (int i = 0; i < b.Count; i++)
		{
			if (i == 0 || i == 10)
			{
				list.Add(b[i]);
				continue;
			}
			double num = Num(a[i]);
			double num2 = Num(b[i]);
			if (i == 4)
			{
				double num3 = Mathf.Repeat((float)(num2 - num) + (float)Math.PI, (float)Math.PI * 2f) - (float)Math.PI;
				list.Add(num + num3 * (double)k);
			}
			else
			{
				list.Add(num + (num2 - num) * (double)k);
			}
		}
		return list;
	}

	private static double Num(object v)
	{
		if (!(v is double result))
		{
			if (!(v is float num))
			{
				if (!(v is int num2))
				{
					if (v is long num3)
					{
						return num3;
					}
					return 0.0;
				}
				return num2;
			}
			return num;
		}
		return result;
	}
}
}
