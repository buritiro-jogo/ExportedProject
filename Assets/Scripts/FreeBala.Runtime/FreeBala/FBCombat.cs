using System;
using UnityEngine;

namespace FreeBala
{
public static class FBCombat
{
	public struct Shot
	{
		public Vector3 end;

		public FBCharacter hit;

		public bool head;

		public string part;

		public float dist;

		public Vector3 normal;
	}

	public static readonly float[][] DIFF = new float[3][]
	{
		new float[3] { 0.55f, 0.75f, 1.4f },
		new float[3] { 1f, 1f, 1f },
		new float[3] { 1.55f, 1.35f, 0.7f }
	};

	private static float lastFlyby;

	// fisica 31/10: buffer do teste de ponto-a-queima-roupa (origem do raio dentro do hitbox do alvo)
	private static readonly Collider[] pbBuf = new Collider[16];

	public static float DiffAcc => DIFF[Mathf.Clamp(FBSession.diff, 0, 2)][0];

	public static float DiffDmg => DIFF[Mathf.Clamp(FBSession.diff, 0, 2)][1];

	public static float DiffReact => DIFF[Mathf.Clamp(FBSession.diff, 0, 2)][2];

	private static float Now => Time.time;

	public static string GetUpClip
	{
		get
		{
			if (!FBRegistry.I || !FBRegistry.I.Clip("IE_Downed_GetUp"))
			{
				return "FF_SYS_KO_GetUp";
			}
			return "IE_Downed_GetUp";
		}
	}

	public static Shot BulletFrom(Vector3 o, Vector3 dir, Vector3 muzzlePos, FBCharacter shooter, WeaponDef weapon, float near = 0f, bool tracer = true, float maxD = 300f, int pellets = 1)
	{
		Shot shot = default(Shot);
		shot.end = o + dir * maxD;
		shot.dist = maxD;
		Shot result = shot;
		if (weapon != null)
		{
			FBBotBrain.Noise(muzzlePos);
		}
		RaycastHit[] array = Physics.RaycastAll(o + dir * near, dir, maxD - near, FBWorld.ShotMask, QueryTriggerInteraction.Collide);
		Array.Sort(array, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
		RaycastHit? raycastHit = null;
		RaycastHit[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			RaycastHit value = array2[i];
			FBHitbox component = value.collider.GetComponent<FBHitbox>();
			if ((bool)component)
			{
				FBCharacter owner = component.owner;
				if ((bool)owner && !(owner == shooter) && owner.alive && (owner.civ || FBMatch.Hostile(owner, shooter)))
				{
					// fisica 31/10: a meia-esfera de cima da capsula Hips->Neck (centrada no Neck, r 0,1736) fica na frente do terco de baixo
					// da esfera da cabeca (queixo/mandibula) e o raio pegava "body" primeiro. Se o raio entra na cabeca do MESMO dono logo
					// depois (ate 12 cm), vale cabeca. So para o tronco: braco na frente do rosto continua sendo membro.
					// (encurtar a Hips->Neck com topAtB abriria um vao de 2-3 cm no pescoco, por onde a bala atravessaria)
					if (!component.head && component.part == "body")
					{
						float lim = value.distance + 0.12f * owner.ScaleK;
						for (int k = i + 1; k < array2.Length && array2[k].distance <= lim; k++)
						{
							FBHitbox hb = array2[k].collider.GetComponent<FBHitbox>();
							if ((bool)hb && hb.owner == owner && hb.head)
							{
								component = hb;
								value = array2[k];
								break;
							}
						}
					}
					result.hit = owner;
					result.head = component.head;
					result.part = component.part;
					result.dist = value.distance + near;
					result.end = value.point;
					result.normal = value.normal;
					break;
				}
				continue;
			}
			FBTarget componentInParent = value.collider.GetComponentInParent<FBTarget>();
			if ((bool)componentInParent && (bool)shooter && shooter.isPlayer)
			{
				result.dist = value.distance + near;
				result.end = value.point;
				componentInParent.Hit(value.point, result.dist);
				FBHUD.I?.Hit(componentInParent.IsHead(value.point));
				FBAudio.Play2D("hitmark", 0.55f);
				if (tracer)
				{
					FBFx.Tracer(muzzlePos, result.end);
				}
				return result;
			}
			if (value.collider.gameObject.layer == 12)
			{
				FBGlass component2 = value.collider.GetComponent<FBGlass>();
				if ((bool)component2)
				{
					component2.Break(value.point, dir);
				}
			}
			else if (!value.collider.isTrigger && !(value.collider.GetComponentInParent<FBCharacter>() == shooter))
			{
				raycastHit = value;
				result.dist = value.distance + near;
				result.end = value.point;
				break;
			}
		}
		// fisica 31/10: origem do raio dentro do hitbox do alvo (personagens encostados): o Raycast nao reporta collider que contem a origem
		Vector3 o0 = o + dir * near;
		int nPb = Physics.OverlapSphereNonAlloc(o0, 0.02f, pbBuf, 512, QueryTriggerInteraction.Collide);
		FBHitbox pb = null;
		for (int j = 0; j < nPb; j++)
		{
			FBHitbox fBHitbox = pbBuf[j].GetComponent<FBHitbox>();
			if ((bool)fBHitbox && (bool)fBHitbox.owner && fBHitbox.owner != shooter && fBHitbox.owner.alive && (fBHitbox.owner.civ || FBMatch.Hostile(fBHitbox.owner, shooter)) && (!pb || fBHitbox.head))
			{
				pb = fBHitbox;
			}
		}
		if ((bool)pb && result.hit != pb.owner)
		{
			result.hit = pb.owner;
			result.head = pb.head;
			result.part = pb.part;
			result.dist = near;
			result.end = o0;
			result.normal = -dir;
			raycastHit = null;
		}
		if (FBWorld.WaterHit(o, dir, result.dist, out var p))
		{
			FBFx.Splash(p);
		}
		Flyby(o, result.end, shooter, result.hit);
		FBStreetPoleFx.Ray(o, dir, result.dist);
		if (tracer)
		{
			FBFx.Tracer(muzzlePos, result.end);
		}
		if ((bool)result.hit)
		{
			FBCharacter hit = result.hit;
			float dmg;
			if (weapon != null && !hit.civ && FBSession.mode != "treino")
			{
				bool flag = (bool)FBMatch.I && FBMatch.I.FFDamage;
				FBBrCore.HitResult hitResult = FBBrCore.Hit(FBBrCore.FFWeapon(weapon.id, weapon.cls), result.part ?? (result.head ? "head" : "body"), flag ? result.dist : (result.dist / 1.2f), hit.vest, hit.helmetFF, Time.time - hit.poseT);
				dmg = hitResult.dmg * (flag ? 1f : 0.5f) * (((bool)shooter && !shooter.isPlayer && !shooter.isRemote) ? DiffDmg : 1f) * (float)Mathf.Max(1, (pellets > 1) ? 1 : 1);
				result.head = hitResult.part == "head";
			}
			else
			{
				dmg = ((weapon != null) ? (weapon.Damage(result.dist, result.head) / (float)Mathf.Max(1, pellets) * UnityEngine.Random.Range(0.97f, 1.03f)) : 20f);
			}
			if ((bool)shooter && shooter.isPlayer)
			{
				FBMatch.Stats.hits++;
			}
			DamageActor(hit, dmg, result.head, shooter, null, new FBHitInfo
			{
				valid = true,
				point = result.end,
				normal = result.normal,
				dir = dir,
				part = (result.head ? "head" : (result.part ?? "body")),
				weaponId = weapon?.id,
				dist = result.dist
			});
		}
		else if (raycastHit.HasValue)
		{
			RaycastHit valueOrDefault = raycastHit.GetValueOrDefault();
			Impact(valueOrDefault, dir);
			float dmg2 = weapon?.Damage(result.dist, head: false) ?? 20f;
			FBDestructible.TryHit(valueOrDefault.collider, dmg2, valueOrDefault.point, dir);
			FBCar componentInParent2 = valueOrDefault.collider.GetComponentInParent<FBCar>();
			if ((bool)componentInParent2)
			{
				FBCarDamage.OnShot(componentInParent2, valueOrDefault.point, dir, dmg2);
			}
			FBStreetPoleFx.Shot(valueOrDefault.collider, valueOrDefault.point, dir);
			FBFisica.Shot(valueOrDefault.collider, valueOrDefault.point, dir);
		}
		return result;
	}

	private static void Flyby(Vector3 a, Vector3 b, FBCharacter shooter, FBCharacter hit)
	{
		FBCharacter player = FBMatch.Player;
		if (!player || !player.alive || shooter == player || hit == player || !shooter || Time.time - lastFlyby < 0.07f)
		{
			return;
		}
		Vector3 vector = (player.Head ? player.Head.position : (player.Pos + Vector3.up * 1.6f * player.ScaleK));
		Vector3 vector2 = b - a;
		float magnitude = vector2.magnitude;
		if (magnitude < 0.5f)
		{
			return;
		}
		float num = Mathf.Clamp(Vector3.Dot(vector - a, vector2 / magnitude), 0f, magnitude);
		if (!(num < 2f) && !(num > magnitude - 0.3f))
		{
			Vector3 vector3 = a + vector2 / magnitude * num;
			float magnitude2 = (vector3 - vector).magnitude;
			if (!(magnitude2 > 2.6f))
			{
				lastFlyby = Time.time;
				FBAudio.PlayAt((magnitude2 < 1f) ? "crack" : ("flyby" + UnityEngine.Random.Range(1, 4)), vector3, Mathf.Lerp(0.85f, 0.35f, magnitude2 / 2.6f), 1.2f, 0.1f, 0f, 1.8f);
			}
		}
	}

	public static void Impact(RaycastHit h, Vector3 dir)
	{
		Vector3 normal = h.normal;
		if (Vector3.Dot(normal, dir) > 0f)
		{
			normal = -normal;
		}
		FBImpactFx.Bullet(h, dir);
		string text = "impact_" + FBImpactFx.Classify(h.collider, h.triangleIndex).ToString().ToLowerInvariant();
		FBAudio.PlayAt(FBAudio.Has(text) ? text : FBUtil.Pick(new string[4] { "impact1", "impact2", "impact3", "impact4" }), h.point, 0.7f, 3f);
	}

	public static void DamageActor(FBCharacter a, float dmg, bool head, FBCharacter src, string why = null, FBHitInfo hit = default(FBHitInfo))
	{
		if ((bool)a)
		{
			a.lastWhy = why;
			a.lastHitHead = head;
			if (hit.valid)
			{
				a.lastHit = hit;
			}
			a.lastHitFrom = (src ? new Vector3?(src.Pos) : null);
		}
		if (!a || !a.alive || (a.special && why != "takedown"))
		{
			return;
		}
		FBDamageType type = FBDamageEvents.TypeOf(why, hit);
		if (a.isRemote)
		{
			FBNet.I?.SendHit(a, dmg, head);
			if ((bool)src && src.isPlayer)
			{
				FBHUD.I?.Hit(head);
				a.Trigger("hit");
			}
			FBDamage d = default(FBDamage);
			d.victim = a;
			d.source = src;
			d.type = type;
			d.amount = dmg;
			d.applied = Mathf.Min(dmg, a.hp);
			d.hpBefore = a.hp;
			d.hpAfter = a.hp;
			d.head = head;
			d.hit = hit;
			d.why = why;
			FBDamageEvents.Raise(in d);
			return;
		}
		bool flag = why == "zone" || why == "fall" || why == "bleed";
		if (a.isPlayer && FBKillstreak2.gunner != null && !flag && why != "gunner")
		{
			FBKillstreak2.Hit(src);
		}
		else
		{
			if ((!flag && a.protUntil > Now) || (a.knocked && why != "bleed" && FBMechanics.KnockInvulnerable(a, Now)))
			{
				return;
			}
			if (a.isPlayer && FBMatch.God)
			{
				dmg = 0f;
			}
			if (!flag && why != "takedown" && (!FBMatch.I || !FBMatch.I.FFDamage))
			{
				if (head && a.helmet)
				{
					dmg *= 0.6f;
				}
				else if (!head && a.armor > 0f)
				{
					float num = Mathf.Min(a.armor, dmg * 0.45f);
					a.armor -= num;
					dmg -= num;
				}
			}
			float num2 = Mathf.Max(0f, a.hp);
			float num3 = Mathf.Min(num2, Mathf.Max(0f, dmg));
			Vector3 vector = DamagePoint(a, head);
			a.hp -= dmg;
			if (num3 > 0f && (bool)src && src.isPlayer && !flag)
			{
				FBHUD.I?.FloatDamage(vector, num3, head);
				FBAudio.Synth(head ? "hit_head" : "hit_body");
			}
			if (!flag)
			{
				if (!hit.valid)
				{
					Vector3 vector2 = (src ? (vector - (src.Pos + Vector3.up * 1.4f * src.ScaleK)).normalized : new Vector3(UnityEngine.Random.Range(-1f, 1f), 0.3f, UnityEngine.Random.Range(-1f, 1f)).normalized);
					FBHitInfo fBHitInfo = default(FBHitInfo);
					fBHitInfo.valid = false;
					fBHitInfo.point = vector;
					fBHitInfo.normal = -vector2;
					fBHitInfo.dir = vector2;
					fBHitInfo.part = (head ? "head" : "body");
					hit = fBHitInfo;
				}
				if ((bool)src && src.isPlayer)
				{
					FBHUD.I?.Hit(head);
					if (!FBMatch.FFA)
					{
						a.tagT = 4f;
					}
				}
				a.Trigger("hit");
			}
			if (!a.isPlayer && !a.isRemote && !a.knocked && (bool)src && src != a && src.team != a.team)
			{
				a.GetComponent<FBBot>()?.OnHit(src);
			}
			if (a.isPlayer && (bool)src && src != a)
			{
				FBDamageIndicators.Hit(src, src.Pos, num3);
			}
			if (!flag && num3 > 0f)
			{
				FBCombatStats.Damage(src, a, head);
			}
			bool flag2 = a.hp <= 0f && !a.civ && !a.knocked && why != "bleed" && FBMatch.KnockOn && FBMechanics.CanKnock(a, FBSession.mode, FBMatch.HasReviver(a));
			FBDamage d = default(FBDamage);
			d.victim = a;
			d.source = src;
			d.type = type;
			d.amount = dmg;
			d.applied = num3;
			d.hpBefore = num2;
			d.hpAfter = a.hp;
			d.head = head;
			d.killed = a.hp <= 0f && !flag2;
			d.knocked = flag2;
			d.hit = hit;
			d.why = why;
			FBDamageEvents.Raise(in d);
			if (a.isPlayer)
			{
				float num4 = Mathf.Clamp01(num3 / Mathf.Max(1f, a.maxHp));
				float k = ((why == "zone") ? 0.3f : ((why == "bleed") ? 0.12f : (Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(num4 * 3.5f)) + (head ? 0.15f : 0f))));
				FBHUD.I?.Damaged(k);
				if (why != "zone" && why != "bleed")
				{
					FBPlayer.I?.Trauma(Mathf.Lerp(0.15f, 0.6f, Mathf.Clamp01(num4 * 3f)));
				}
			}
			if ((bool)src && src.isPlayer && src != a && !a.civ && a.hp <= 0f)
			{
				FBHUD.I?.KillMark(flag2);
			}
			if (a.civ)
			{
				if (a.hp > 0f)
				{
					a.GetComponent<FBCivilian>()?.OnHit();
					return;
				}
				a.Die(src);
				if ((bool)src && src.isPlayer)
				{
					FBSession.money = Mathf.Max(0, FBSession.money - 150);
					FBHUD.I?.Toast("CIVIL ATINGIDO", a.Name.ToUpper(), "−$150 · NÃO ATIRE EM INOCENTES", new Color(0.86f, 0.17f, 0.15f));
				}
				FBMatch.Feed((src ? src.Name : "?") + " ✖ " + a.Name + " (civil)", (bool)src && src.isPlayer);
			}
			else if (a.hp <= 0f)
			{
				if (!a.knocked && why != "bleed" && FBMatch.KnockOn && FBMechanics.CanKnock(a, FBSession.mode, FBMatch.HasReviver(a)))
				{
					Knock(a, src, head);
					return;
				}
				if (a.knocked)
				{
					if (why == "bleed" && (bool)a.knockedBy)
					{
						src = a.knockedBy;
					}
					EndKnock(a);
				}
				if ((bool)src && src != a)
				{
					src.kills++;
				}
				a.Die(src);
				FBMatch.I?.OnKill(a, src, head, why);
			}
			else if (!a.isPlayer && (bool)src && !a.knocked)
			{
				a.GetComponent<FBBot>()?.Engage(src);
			}
		}
	}

	private static Vector3 DamagePoint(FBCharacter a, bool head)
	{
		Vector3 result = (a.Head ? a.Head.position : (a.Pos + Vector3.up * 1.6f * a.ScaleK));
		if (!head)
		{
			result.y -= 0.45f * a.ScaleK;
		}
		return result;
	}

	public static void Knock(FBCharacter a, FBCharacter src, bool head)
	{
		a.koAt = Now;
		a.koFall = ((new Vector2(a.velocity.x, a.velocity.z).magnitude > 3.5f) ? "FF_SYS_KO_FallRun" : "FF_SYS_KO_Fall");
		FBMechanics.KnockDown(a, Now);
		a.knockedBy = (((bool)src && src != a) ? src : null);
		a.velocity = new Vector3(0f, a.velocity.y, 0f);
		a.StopDance(force: true);
		a.StopWaitPose();
		a.SetWeaponVisible(v: false);
		a.prone = true;
		a.crouch = true;
		a.sprint = (a.shooting = (a.aiming = false));
		if (a.isPlayer)
		{
			FBPlayer.I?.OnKnocked();
			FBAudio.Play2D("ie_knocked", 0.6f);
			FBHUD.I?.Damaged(0.6f);
			FBHUD.I?.Center("VOCÊ FOI DERRUBADO", 2f);
		}
		else
		{
			a.GetComponent<FBBot>()?.OnKnocked();
			FBAudio.PlayAt("ie_knocked", a.Pos, 0.7f, 6f);
			if ((bool)src && src.isPlayer)
			{
				FBHUD.I?.Toast("INIMIGO DERRUBADO", a.Name.ToUpper(), "FINALIZE ANTES QUE REANIMEM", new Color(1f, 0.3f, 0.25f));
			}
		}
		FBMatch.Feed((src ? src.Name : "?") + " ✖ " + a.Name + " (derrubado)", ((bool)src && src.isPlayer) || a.isPlayer);
	}

	public static void EndKnock(FBCharacter a)
	{
		a.knocked = false;
		a.prone = false;
		a.crouch = false;
		a.sys = null;
		a.revivedBy = null;
		a.reviveP = 0f;
	}

	public static void CompleteRevive(FBCharacter a, FBCharacter by)
	{
		FBMechanics.ReviveDone(a);
		EndKnock(a);
		AnimationClip animationClip = (FBRegistry.I ? FBRegistry.I.Clip(GetUpClip) : null);
		a.getUpT = (animationClip ? (animationClip.length - 0.05f) : 0f);
		a.SetWeaponVisible(a.weapon != null);
		a.combat = a.weapon != null;
		if (a.isPlayer)
		{
			FBHUD.I?.Center("REANIMADO POR " + by.Name.ToUpper(), 1.6f);
			FBAudio.Synth("heal");
		}
		else
		{
			a.GetComponent<FBBot>()?.OnRevived();
			if (by.isPlayer)
			{
				FBAudio.Synth("heal");
				FBHUD.I?.Toast("ALIADO REANIMADO", a.Name.ToUpper(), "VOLTOU COM " + 50f + " DE VIDA", new Color(0.93f, 0.91f, 0.86f));
			}
		}
		FBMatch.Feed(by.Name + " reanimou " + a.Name, by.isPlayer || a.isPlayer);
	}

	public static void KnockTick(FBCharacter a, float dt)
	{
		if (a.revivedBy != null && Now - a.reviveTick > 0.25f)
		{
			a.revivedBy = null;
			a.reviveP = 0f;
		}
		if (a.revivedBy == null)
		{
			float num = FBMechanics.BleedStep(a, dt);
			if (!(num <= 0f))
			{
				DamageActor(a, FBMatch.HasReviver(a) ? num : (a.hp + 1f), head: false, a.knockedBy, "bleed");
			}
		}
	}

	public static string KoPose(FBCharacter a, bool moving)
	{
		if (Now - a.koAt < 0.8f && H(a.koFall))
		{
			return a.koFall;
		}
		if (a.revivedBy != null && Now - a.reviveTick < 0.35f && H("IE_Downed_Treated"))
		{
			return "IE_Downed_Treated";
		}
		if (moving && H("FF_SYS_KO_Move"))
		{
			return "FF_SYS_KO_Move";
		}
		if (moving && H("FF_SYS_KO_Crawl"))
		{
			return "FF_SYS_KO_Crawl";
		}
		if (!H("FF_SYS_KO_Idle"))
		{
			if (!moving || !H("FF_SYS_Crawl"))
			{
				return "FF_SYS_Prone";
			}
			return "FF_SYS_Crawl";
		}
		return "FF_SYS_KO_Idle";
		static bool H(string c)
		{
			if ((bool)FBRegistry.I)
			{
				return FBRegistry.I.Clip(c);
			}
			return false;
		}
	}
}
}
