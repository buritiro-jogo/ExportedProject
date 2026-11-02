using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBKillstreak2
{
	public class G
	{
		public string kind;

		public float t;

		public float total;

		public float hp = 1f;

		public float exitHold;

		public bool armed;

		public bool dead;

		public Vector3 back;

		public float backYaw;

		public FBCharacter lastSrc;
	}

	public static G gunner;

	private static readonly Dictionary<string, Vector2> GUN_VEH = new Dictionary<string, Vector2>
	{
		["heli"] = new Vector2(0.03f, 0.08f),
		["caveirao"] = new Vector2(0.02f, 0.06f)
	};

	public static bool On
	{
		get
		{
			if (FBSession.mode == "classico" || FBSession.mode == "ffa")
			{
				return !FBSession.online;
			}
			return false;
		}
	}

	public static void Call()
	{
		FBMatch i = FBMatch.I;
		FBCharacter player = FBMatch.Player;
		if (On && gunner == null && (bool)i && i.ks.Ready(Time.time) && (bool)player && player.alive && !player.knocked && !FBVehicles.Driving(player) && !(i.state != "play"))
		{
			string reward = i.ks.reward;
			FBAmbientScenery i2 = FBAmbientScenery.I;
			if (!i2 || !((reward == "heli") ? i2.HeliUp : i2.CavUp))
			{
				FBHUD.I?.Center(FBKillstreak.STREAK[reward].name + " INDISPONÍVEL AGORA", 1.6f);
				FBAudio.Synth("deny");
				return;
			}
			i.ks.Consume(Time.time);
			gunner = new G
			{
				kind = reward,
				total = FBKillstreak.STREAK[reward].run,
				back = player.Pos,
				backYaw = player.Yaw
			};
			FBAudio.Synth("go");
			FBHUD.I?.Center("ATIRADOR · " + FBKillstreak.STREAK[reward].name, 1.6f);
		}
	}

	public static void Hit(FBCharacter src)
	{
		G g = gunner;
		if (g == null || g.dead)
		{
			return;
		}
		Vector2 vector = GUN_VEH[g.kind];
		g.hp -= Random.Range(vector.x, vector.y) * FBKillstreak.STREAK[g.kind].dmgMul;
		if ((bool)src)
		{
			g.lastSrc = src;
		}
		FBHUD.I?.Damaged(0.1f);
		FBAudio.Play2D("thud", 0.35f, 0f, Random.Range(1.3f, 1.6f));
		if (!(g.hp > 0f))
		{
			g.hp = 0f;
			g.dead = true;
			if (g.kind == "heli")
			{
				FBAmbientScenery.I?.ShootDown();
				FBHUD.I?.Center("HELICÓPTERO ABATIDO!");
				return;
			}
			Vector3 p = FBAmbientScenery.I.Cav.position + Vector3.up * 1.5f;
			FBAmbientScenery.I.KillCav();
			Boom(p);
			GunnerDie();
		}
	}

	public static void Boom(Vector3 p)
	{
		FBFx.Explosion(p, 4f);
		FBAudio.PlayAt("grenade", p, 1f, 20f);
		foreach (FBCharacter item in FBMatch.All.ToList())
		{
			if ((bool)item && item.alive && !item.isPlayer && (item.Pos - p).magnitude < 7f)
			{
				FBCombat.DamageActor(item, 120f * (1f - (item.Pos - p).magnitude / 7f), head: false, null, "zone");
			}
		}
	}

	public static void GunnerDie()
	{
		FBCharacter src = gunner?.lastSrc;
		End(back: false);
		if ((bool)FBMatch.Player)
		{
			FBCombat.DamageActor(FBMatch.Player, 999f, head: false, src, "gunner");
		}
	}

	public static void End(bool back)
	{
		G g = gunner;
		if (g != null)
		{
			gunner = null;
			FBCharacter player = FBMatch.Player;
			if (back && (bool)player && player.alive)
			{
				FBMatch.Teleport(player, g.back, g.backYaw);
			}
			CharacterController characterController = (player ? player.GetComponent<CharacterController>() : null);
			if ((bool)characterController)
			{
				characterController.enabled = true;
			}
		}
	}

	public static bool Tick(FBCharacter p, float dt, bool gHeld)
	{
		G g = gunner;
		if (g == null)
		{
			return false;
		}
		g.t += dt;
		if (!gHeld)
		{
			g.armed = true;
		}
		g.exitHold = ((g.armed && gHeld && !g.dead) ? (g.exitHold + dt) : 0f);
		Vector3 position = (FBAmbientScenery.I ? FBAmbientScenery.I.GunnerSeat(g.kind) : p.Pos);
		CharacterController component = p.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		p.transform.position = position;
		p.velocity = Vector3.zero;
		FBHUD.I?.SetBox($"{FBKillstreak.STREAK[g.kind].name} · {Mathf.CeilToInt(g.total - g.t)}s · BLINDAGEM {Mathf.RoundToInt(g.hp * 100f)}% · SEGURE G PARA SAIR", on: true);
		if (!g.dead && (g.t >= g.total || g.exitHold >= 0.6f))
		{
			End(back: true);
			FBHUD.I?.SetBox("", on: false);
		}
		return true;
	}
}
}
