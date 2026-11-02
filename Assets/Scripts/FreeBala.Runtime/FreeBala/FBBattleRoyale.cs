using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBBattleRoyale
{
	private class S
	{
		public string st = "plane";

		public Vector2 hv;

		public float fallT;

		public float jumpAt = -1f;
	}

	private static readonly Dictionary<FBCharacter, S> st = new Dictionary<FBCharacter, S>();

	private static FBBrCore.Route route;

	private static float planeT;

	private static float planeSpeed = 60f;

	private static float alt = 220f;

	private static GameObject plane;

	private static bool on;

	public static bool PlayerHigh
	{
		get
		{
			if (on && (bool)FBMatch.Player && st.TryGetValue(FBMatch.Player, out var value))
			{
				return value.st != "land";
			}
			return false;
		}
	}

	private static Vector3 PlanePos => new Vector3(route.a.x + route.dir.x * planeT, alt, route.a.y + route.dir.y * planeT);

	public static bool Active(FBCharacter c)
	{
		if (on && (bool)c && st.TryGetValue(c, out var value))
		{
			return value.st != "land";
		}
		return false;
	}

	public static bool InPlane(FBCharacter c)
	{
		if (on && (bool)c && st.TryGetValue(c, out var value))
		{
			return value.st == "plane";
		}
		return false;
	}

	public static bool Airborne()
	{
		if (on)
		{
			return st.Any((KeyValuePair<FBCharacter, S> kv) => (bool)kv.Key && kv.Key.alive && kv.Value.st != "land");
		}
		return false;
	}

	public static void Setup(FBMatch G, FBZone.Z zone)
	{
		on = true;
		st.Clear();
		route = FBBrCore.Airline(zone.c.x, zone.c.z, zone.r, null, 0.25f);
		planeT = 0f;
		alt = zone.c.y + 180f;
		plane = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		Object.Destroy(plane.GetComponent<Collider>());
		plane.name = "Aviao";
		plane.transform.localScale = new Vector3(4f, 14f, 4f);
		GameObject gameObject = FBSkyPlanes.Boeing();
		if ((bool)gameObject)
		{
			Object.Destroy(plane);
			plane = gameObject;
		}
		foreach (FBCharacter item in FBMatch.Actors())
		{
			S s = new S();
			if (!item.isPlayer)
			{
				s.jumpAt = FBBrCore.JumpPointFor(route, item.Pos.x + Random.Range(-60f, 60f), item.Pos.z + Random.Range(-60f, 60f)) + Random.Range(-20f, 20f);
			}
			st[item] = s;
			Hide(item, h: true);
		}
		FBLoot.Clear();
		List<Vector3> list = FBNav.Points();
		List<string> l = GameData.Weapons.Keys.Where((string id) => (bool)FBRegistry.I.Weapon(id) && GameData.Weapons[id].cls != "explosivo").ToList();
		for (int i = 0; i < Mathf.Min(140, list.Count); i++)
		{
			Vector3 pos = list[i * 7 % list.Count] + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
			if (Random.value < 0.72f)
			{
				FBLoot.DropWeapon(FBUtil.Pick(l), pos);
				continue;
			}
			FBLoot.DropArmor(FBUtil.Pick(new string[6] { "vest1", "vest2", "vest3", "helmet1", "helmet2", "helmet3" }), pos);
		}
	}

	private static void Hide(FBCharacter a, bool h)
	{
		Renderer[] componentsInChildren = a.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = !h;
		}
		CharacterController component = a.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = !h;
		}
	}

	public static void Tick(float dt)
	{
		if (!on)
		{
			return;
		}
		planeT += planeSpeed * dt;
		if ((bool)plane)
		{
			plane.transform.position = PlanePos;
			plane.transform.rotation = Quaternion.LookRotation(new Vector3(route.dir.x, 0f, route.dir.y)) * ((plane.name == "Aviao") ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity);
		}
		if (planeT > route.len)
		{
			foreach (KeyValuePair<FBCharacter, S> item in st.ToList())
			{
				if ((bool)item.Key && item.Value.st == "plane")
				{
					Jump(item.Key);
				}
			}
		}
		if (planeT > route.len + 400f && (bool)plane)
		{
			Object.Destroy(plane);
		}
	}

	private static void Jump(FBCharacter a)
	{
		S s = st[a];
		s.st = "dive";
		s.fallT = 0f;
		s.hv = Vector2.zero;
		FBMatch.Teleport(a, PlanePos - new Vector3(route.dir.x, 0f, route.dir.y) * 12f + Vector3.down * 4f, a.Yaw);
		Hide(a, h: false);
		CharacterController component = a.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		a.sys = (FBRegistry.I.Clip("FF_SYS_BR_Fall") ? "FF_SYS_BR_Fall" : "FF_SYS_Prone");
	}

	private static bool Fall(FBCharacter a, float dt, float fwd, float side, bool surf, bool open, float lookYawDeg)
	{
		S s = st[a];
		if (s.st == "plane")
		{
			a.transform.position = PlanePos;
			return true;
		}
		RaycastHit hit;
		float num = (FBWorld.RayHit(a.Pos, Vector3.down, 2000f, out hit) ? hit.distance : 999f);
		if (s.st == "dive" || s.st == "surf")
		{
			s.fallT += dt;
			s.st = ((surf && FBBrCore.CanSurf(num)) ? "surf" : "dive");
			if (open || FBBrCore.ShouldOpen(s.fallT, num))
			{
				s.st = "para";
				FBAudio.PlayAt("ie_slide", a.Pos, 0.6f, 8f);
			}
		}
		Vector2 vector = (s.hv = FBBrCore.SkyVelocity(s.st, fwd, s.hv, dt));
		float num2 = FBBrCore.SkyTurnRate(s.st, vector.x) * 57.29578f * dt;
		a.Yaw = Mathf.MoveTowardsAngle(a.Yaw, lookYawDeg, num2 * 4f + Mathf.Abs(side) * num2);
		Vector3 vector2 = Quaternion.Euler(0f, a.Yaw, 0f) * Vector3.forward;
		a.transform.position += (vector2 * vector.x + Vector3.up * vector.y) * dt;
		a.sys = ((!(s.st == "para")) ? (FBRegistry.I.Clip("FF_SYS_BR_Fall") ? "FF_SYS_BR_Fall" : "FF_SYS_Prone") : (FBRegistry.I.Clip("FF_SYS_BR_Parachute") ? "FF_SYS_BR_Parachute" : "FF_SYS_Prone"));
		a.velocity = vector2 * vector.x + Vector3.up * vector.y;
		a.grounded = false;
		if (num < 0.6f)
		{
			s.st = "land";
			a.sys = null;
			CharacterController component = a.GetComponent<CharacterController>();
			if ((bool)component)
			{
				component.enabled = true;
			}
			FBAudio.PlayAt("ie_land", a.Pos, 0.7f, 6f);
			if (a.isPlayer)
			{
				FBHUD.I?.Damaged(0.1f);
			}
		}
		return true;
	}

	public static bool StepPlayer(FBCharacter p, float dt, FBInputState inp, float camYaw)
	{
		if (!st.TryGetValue(p, out var value) || value.st == "land")
		{
			return false;
		}
		if (value.st == "plane")
		{
			p.transform.position = PlanePos;
			if (inp.jump)
			{
				Jump(p);
			}
			FBHUD.I?.SetBox("ESPAÇO = SALTAR DO AVIÃO", on: true);
			return true;
		}
		FBHUD.I?.SetBox((value.st == "para") ? "PARAQUEDAS · W/S CONTROLA" : "QUEDA LIVRE · ESPAÇO ABRE O PARAQUEDAS · SHIFT MERGULHA", on: true);
		return Fall(p, dt, inp.move.y, inp.move.x, inp.sprintHeld, inp.jump, camYaw);
	}

	public static bool StepBot(FBCharacter b, float dt)
	{
		if (!st.TryGetValue(b, out var value) || value.st == "land")
		{
			return false;
		}
		if (value.st == "plane")
		{
			b.transform.position = PlanePos;
			if (planeT >= value.jumpAt)
			{
				Jump(b);
			}
			return true;
		}
		return Fall(b, dt, (value.st == "para") ? 0.4f : 1f, 0f, surf: false, open: false, b.Yaw);
	}

	public static bool Camera(Camera cam, float dt, float yaw, float pitch, FBCharacter p)
	{
		if (!Active(p))
		{
			return false;
		}
		S s = st[p];
		float num = ((s.st == "para") ? 2.5f : 2.8f);
		float num2 = ((s.st == "para") ? 6f : ((s.st == "plane") ? 30f : 4.7f));
		Quaternion quaternion = Quaternion.Euler(0f - pitch + 15f, yaw, 0f);
		Vector3 vector = ((s.st == "plane") ? PlanePos : p.Pos) + Vector3.up * num;
		cam.transform.position = vector - quaternion * Vector3.forward * num2;
		cam.transform.rotation = Quaternion.LookRotation(vector - cam.transform.position);
		return true;
	}

	public static bool BotLoot(FBCharacter b, FBBot bot, float dt)
	{
		FBLoot.L l = null;
		float num = 60f;
		foreach (FBLoot.L loot in FBLoot.loots)
		{
			if (loot.id != null && (bool)loot.obj)
			{
				float magnitude = (loot.pos - b.Pos).magnitude;
				if (magnitude < num)
				{
					num = magnitude;
					l = loot;
				}
			}
		}
		if (l == null)
		{
			return false;
		}
		if (num < 1.5f)
		{
			Object.Destroy(l.obj);
			FBLoot.loots.Remove(l);
			b.GetComponent<FBWeaponUser>().Give(l.id);
			return false;
		}
		bot.goalP = l.pos;
		return false;
	}

	public static void End()
	{
		on = false;
		st.Clear();
		if ((bool)plane)
		{
			Object.Destroy(plane);
		}
	}
}
}
