using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBDeath
{
	public class Clip
	{
		public string id;

		public string face;

		public string kind;

		public float fwd;

		public float right;

		public float dur;

		public float land;

		public float Len => Mathf.Sqrt(fwd * fwd + right * right);
	}

	private static List<Clip> lib;

	private static readonly List<string> recent = new List<string>();

	public static string LastPick;

	public static string LastWhy;

	private static List<Clip> Lib()
	{
		if (lib != null)
		{
			return lib;
		}
		lib = new List<Clip>();
		TextAsset textAsset = Resources.Load<TextAsset>("FBDeathLib");
		if (!textAsset)
		{
			return lib;
		}
		foreach (object item in Json.L(Json.Parse(textAsset.text), "clips") ?? new List<object>())
		{
			Clip clip = new Clip
			{
				id = Json.S(item, "id"),
				fwd = Json.F(item, "fwd"),
				right = Json.F(item, "right"),
				dur = Json.F(item, "dur"),
				land = Json.F(item, "land"),
				face = Json.S(item, "face"),
				kind = Json.S(item, "kind", "stand")
			};
			if ((bool)FBRegistry.I && (bool)FBRegistry.I.Clip(clip.id))
			{
				lib.Add(clip);
			}
		}
		return lib;
	}

	public static string Begin(FBCharacter ch)
	{
		FBRagdoll fBRagdoll = FBRagdoll.Of(ch);
		FBHitInfo lastHit = ch.lastHit;
		string lastWhy = ch.lastWhy;
		Vector3 vector;
		if (!(lastHit.dir.sqrMagnitude > 0.01f))
		{
			Vector3? lastHitFrom = ch.lastHitFrom;
			if (lastHitFrom.HasValue)
			{
				Vector3 valueOrDefault = lastHitFrom.GetValueOrDefault();
				vector = ch.Pos - valueOrDefault;
			}
			else
			{
				vector = -ch.transform.forward;
			}
		}
		else
		{
			vector = lastHit.dir;
		}
		Vector3 vector2 = vector;
		vector2.y = 0f;
		if (vector2.sqrMagnitude < 0.0001f)
		{
			vector2 = -ch.transform.forward;
		}
		vector2.Normalize();
		float num;
		switch (lastWhy)
		{
		default:
			num = (ch.lastHitHead ? 4.5f : 3f);
			break;
		case "car":
			num = 9f;
			break;
		case "explosion":
		case "gunner":
		case "heli":
			num = 14f;
			break;
		}
		float num2 = num;
		int num3;
		switch (lastWhy)
		{
		default:
			num3 = ((lastWhy == "fall") ? 1 : 0);
			break;
		case "explosion":
		case "gunner":
		case "heli":
		case "car":
			num3 = 1;
			break;
		}
		bool flag = (byte)num3 != 0;
		bool flag2 = ch.prone || ch.knocked;
		bool flag3 = !ch.grounded && ch.velocity.y < -2f;
		if (flag || flag2 || flag3)
		{
			LastPick = null;
			LastWhy = (flag ? lastWhy : (flag2 ? "caido" : "no_ar"));
			if ((bool)fBRagdoll)
			{
				fBRagdoll.Go(vector2 * num2 + Vector3.up * (flag ? (num2 * 0.35f) : 0f), lastHit.valid ? lastHit.point : (ch.Pos + Vector3.up), flag ? 0f : 0.05f);
			}
			return null;
		}
		List<Clip> list = Lib();
		if (list.Count == 0)
		{
			LastWhy = "sem biblioteca";
			return null;
		}
		Transform transform = ch.transform;
		Vector3 vector3 = transform.InverseTransformDirection(vector2);
		Vector2 normalized = new Vector2(vector3.x, vector3.z).normalized;
		bool flag4 = transform.InverseTransformDirection(ch.velocity).z > 4.5f && !ch.crouch;
		bool crouch = ch.crouch;
		bool lastHitHead = ch.lastHitHead;
		float num4 = Mathf.Max(0.5f, ch.ScaleK);
		Vector3 vector4 = (ch.Chest ? ch.Chest.position : (ch.Pos + Vector3.up * 1.2f * num4));
		float y = ch.Pos.y;
		Clip clip = null;
		float num5 = -1E+09f;
		int num6 = 0;
		foreach (Clip item in list)
		{
			bool num7;
			if (!flag4)
			{
				num7 = item.kind == "run";
			}
			else
			{
				if (item.kind == "run")
				{
					goto IL_034b;
				}
				num7 = !(item.fwd > 1.2f);
			}
			if (num7)
			{
				continue;
			}
			goto IL_034b;
			IL_034b:
			if ((crouch && item.Len > 0.8f) || (lastHitHead && item.land > 1.3f))
			{
				continue;
			}
			Vector3 vector5 = transform.TransformDirection(new Vector3(item.right, 0f, item.fwd)) * num4;
			float magnitude = vector5.magnitude;
			if (magnitude > 0.05f && Physics.SphereCast(vector4, 0.22f * num4, vector5 / magnitude, out var _, magnitude + 0.45f * num4, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
			{
				continue;
			}
			Vector3 vector6 = ch.Pos + vector5;
			float? num8 = FBWorld.GroundY(vector6.x, y + 0.6f, vector6.z, 0.6f, 1.2f);
			if (!num8.HasValue)
			{
				continue;
			}
			float valueOrDefault2 = num8.GetValueOrDefault();
			if (!(Mathf.Abs(valueOrDefault2 - y) > 0.3f))
			{
				num6++;
				Vector2 vector7 = new Vector2(item.right, item.fwd);
				float num9 = ((vector7.sqrMagnitude > 0.01f) ? Vector2.Dot(vector7.normalized, normalized) : 0.2f);
				float num10 = (flag4 ? 0.3f : 1f) * num9 + Random.value * 0.45f - (recent.Contains(item.id) ? 1.2f : 0f);
				if (num10 > num5)
				{
					num5 = num10;
					clip = item;
				}
			}
		}
		if (clip == null)
		{
			LastPick = null;
			LastWhy = $"sem espaço ({num6})";
			if ((bool)fBRagdoll)
			{
				fBRagdoll.Go(vector2 * num2, lastHit.valid ? lastHit.point : vector4, 0.05f);
			}
			return null;
		}
		recent.Add(clip.id);
		if (recent.Count > 6)
		{
			recent.RemoveAt(0);
		}
		LastPick = clip.id;
		LastWhy = string.Format("empurrão ({0:0.0};{1:0.0}){2}{3}{4}", normalized.x, normalized.y, flag4 ? " correndo" : "", crouch ? " agachado" : "", lastHitHead ? " cabeça" : "");
		if ((bool)fBRagdoll)
		{
			fBRagdoll.After(clip.dur - 0.05f, clip.land, vector2 * num2 * 0.25f);
		}
		return clip.id;
	}
}
}
