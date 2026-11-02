using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public class FBRoleNpc : MonoBehaviour
{
	public FBCharacter c;

	private CharacterController cc;

	private bool cvGround = true;

	private float cvSlide;

	private float cvStep = -1f;

	private FBMotor.Ground cvG;

	public string st = "walk";

	public string prevSt;

	public float stT;

	public float partyCd;

	public float danceT;

	public float vy;

	public float stkT;

	public float hornT;

	public Vector3? goalP;

	public Vector3? dodgeP;

	public Vector3? stkP;

	public bool dancer;

	public bool atParty;

	public bool greet;

	public int stkN;

	public Vector3? lookP;

	public Vector3? fleeP;

	private Vector3 dodgeFrom;

	private Vector3 dodgeFwd;

	private float reactT;

	public FBSeat seat;

	public Vector3? talkC;

	public FBParedao.Wall partyW;

	private Vector3 vel;

	private List<Vector3> path;

	private float navT;

	private float crossWait;

	private Vector3? pathGoal;

	private string seatClip;

	private float pace;

	private float phase;

	private float seatY;

	private float seatYaw;

	private Vector3 seatXZ;

	private Vector3? seatBase;

	public bool Free
	{
		get
		{
			if (!(st == "walk") && !(st == "pause"))
			{
				return st == "talk";
			}
			return true;
		}
	}

	public void TalkWith(FBRoleNpc mate, float T = 25f)
	{
		Vector3 vector = (c.Pos + mate.c.Pos) / 2f;
		Vector3 vector2 = (mate.c.Pos - c.Pos).Flat();
		vector2 = ((vector2.sqrMagnitude > 0.0001f) ? vector2.normalized : Vector3.forward);
		(FBRoleNpc, int)[] array = new(FBRoleNpc, int)[2]
		{
			(this, -1),
			(mate, 1)
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (fBRoleNpc, num) = array[i];
			fBRoleNpc.c.StopDance(force: true);
			fBRoleNpc.LeaveSeat();
			fBRoleNpc.Teleport(new Vector3(vector.x + vector2.x * 0.55f * (float)num, fBRoleNpc.c.Pos.y, vector.z + vector2.z * 0.55f * (float)num));
			fBRoleNpc.JoinTalk(vector, T);
		}
	}

	public void LookAt(Vector3 p, float T)
	{
		if (Free)
		{
			c.StopDance(force: true);
			c.talking = false;
			talkC = null;
			st = "look";
			stT = T;
			lookP = p;
			vel = Vector3.zero;
		}
	}

	private void Flee(Vector3 from)
	{
		c.StopDance(force: true);
		c.talking = false;
		talkC = null;
		c.surrender = false;
		LeaveSeat();
		Vector3 vector = (c.Pos - from).Flat().normalized * 28f;
		fleeP = FBNav.RandomPoint(c.Pos + vector, 10f) ?? (c.Pos + vector);
		st = "flee";
		stT = UnityEngine.Random.Range(5f, 9f);
	}

	private bool Aimed(FBCharacter p)
	{
		Camera main = Camera.main;
		if (!main || (!p.aiming && !(p.lastShot < 1.2f)) || p.Unarmed)
		{
			return false;
		}
		Vector3 forward = main.transform.forward;
		Vector3 vector = c.Pos + Vector3.up * 1.2f * c.ScaleK - main.transform.position;
		float num = Vector3.Dot(vector, forward);
		if (num > 0f)
		{
			return (vector - forward * num).magnitude < 0.9f;
		}
		return false;
	}

	private bool React(float dt)
	{
		FBCharacter player = FBMatch.Player;
		Vector3? vector;
		if (st == "react" || st == "look" || st == "scared")
		{
			vel = Vector3.zero;
			c.velocity = Vector3.zero;
			if (st == "look")
			{
				vector = lookP;
				if (vector.HasValue)
				{
					Vector3 valueOrDefault = vector.GetValueOrDefault();
					if ((valueOrDefault - c.Pos).Flat().sqrMagnitude > 0.01f)
					{
						c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation((valueOrDefault - c.Pos).Flat()).eulerAngles.y, Mathf.Min(1f, dt * 4f));
					}
				}
			}
			if (st == "scared" && (bool)player)
			{
				if ((player.Pos - c.Pos).Flat().sqrMagnitude > 0.01f)
				{
					c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation((player.Pos - c.Pos).Flat()).eulerAngles.y, Mathf.Min(1f, dt * 4f));
				}
				if (Aimed(player))
				{
					stT = Mathf.Max(stT, 1f);
				}
			}
			if (stT <= 0f)
			{
				if (st == "scared")
				{
					c.surrender = false;
					if ((bool)player)
					{
						Flee(player.Pos);
					}
					else
					{
						st = "walk";
					}
					return false;
				}
				if (st == "react" && c.dance != null && FBNpcRotina.Moves(c.dance.clip) && (bool)c.an)
				{
					Transform boneTransform = c.an.GetBoneTransform(HumanBodyBones.Hips);
					if ((object)boneTransform != null)
					{
						Transform boneTransform2 = c.an.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
						Transform boneTransform3 = c.an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
						Vector3 vector2 = (boneTransform.position - c.Pos).Flat();
						Teleport(c.Pos + vector2);
						if ((bool)boneTransform2 && (bool)boneTransform3)
						{
							Vector3 forward = Vector3.Cross(boneTransform3.position - boneTransform2.position, Vector3.up).Flat();
							if (forward.sqrMagnitude > 1E-06f)
							{
								c.Yaw = Quaternion.LookRotation(forward).eulerAngles.y;
							}
						}
						c.CutDance();
						goto IL_0361;
					}
				}
				c.StopDance(force: true);
				goto IL_0361;
			}
			return true;
		}
		if (!Free || (reactT -= dt) > 0f)
		{
			return false;
		}
		reactT = 0.25f + UnityEngine.Random.value * 0.1f;
		if ((bool)player && player.alive && !player.vehiclePose)
		{
			float magnitude = (player.Pos - c.Pos).Flat().magnitude;
			if (magnitude < 0.8f && player.velocity.Flat().magnitude > 1.2f)
			{
				c.StopDance(force: true);
				c.talking = false;
				talkC = null;
				float num = FBNpcRotina.Bump(c, player.Pos);
				if (num > 0f)
				{
					st = "react";
					stT = num;
					return true;
				}
			}
			if (magnitude < 25f && Aimed(player))
			{
				c.StopDance(force: true);
				c.talking = false;
				talkC = null;
				st = "scared";
				stT = UnityEngine.Random.Range(3f, 5f);
				c.surrender = true;
				FBNpcVoz.Say(c, "namira", 1f, 4.5f, 5f, 0.5f);
				return true;
			}
			if (magnitude < 12f && !player.Unarmed && (bool)player.weaponGO && player.weaponGO.activeInHierarchy && UnityEngine.Random.value < 0.2f)
			{
				FBNpcVoz.Say(c, "armado", 0.95f, 4f, 30f, 4f);
			}
		}
		vector = FBBotBrain.lastNoiseP;
		if (vector.HasValue)
		{
			Vector3 valueOrDefault2 = vector.GetValueOrDefault();
			if (Time.time - FBBotBrain.lastNoiseT < 2f && (valueOrDefault2 - c.Pos).sqrMagnitude < 1225f)
			{
				Flee(valueOrDefault2);
				FBNpcVoz.Say(c, "perigo", 1f, 5f, 6f, 0.5f, UnityEngine.Random.Range(0.1f, 0.6f));
			}
		}
		return false;
		IL_0361:
		st = "walk";
		goalP = null;
		stT = 0f;
		lookP = null;
		return false;
	}

	private static bool PlayerCarComing(Vector3 p)
	{
		FBCar cur = FBDrive.Cur;
		if (!cur || Mathf.Abs(cur.v) < 2f)
		{
			return false;
		}
		Vector3 lhs = (p - cur.transform.position).Flat();
		Vector3 rhs = FBDrive.Fwd(cur.yaw) * Mathf.Sign(cur.v);
		float num = Vector3.Dot(lhs, rhs);
		float num2 = Mathf.Abs(lhs.x * rhs.z - lhs.z * rhs.x);
		if (num > -2f && num < Mathf.Abs(cur.v) * 4f + 4f)
		{
			return num2 < 3.5f;
		}
		return false;
	}

	private void Awake()
	{
		c = GetComponent<FBCharacter>();
		cc = GetComponent<CharacterController>();
		c.civ = true;
		c.combat = false;
		stT = UnityEngine.Random.Range(2f, 8f);
		partyCd = UnityEngine.Random.Range(2f, 25f);
		dancer = UnityEngine.Random.value < 0.7f;
		float value = UnityEngine.Random.value;
		pace = ((value < 0.25f) ? UnityEngine.Random.Range(0.85f, 1.05f) : ((value < 0.8f) ? UnityEngine.Random.Range(1.15f, 1.42f) : UnityEngine.Random.Range(1.5f, 1.8f)));
		phase = UnityEngine.Random.value * 10f;
		c.walkSpeed = pace;
	}

	private Vector3 Carrot(float L)
	{
		if (path == null || path.Count == 0)
		{
			return c.Pos;
		}
		Vector3 vector = c.Pos;
		float num = L;
		for (int i = 0; i < path.Count; i++)
		{
			Vector3 vector2 = path[i];
			Vector3 vector3 = (vector2 - vector).Flat();
			float magnitude = vector3.magnitude;
			if (magnitude >= num)
			{
				return vector + vector3 / Mathf.Max(0.0001f, magnitude) * num;
			}
			num -= magnitude;
			vector = vector2;
		}
		List<Vector3> list = path;
		return list[list.Count - 1];
	}

	public void Relocate(Vector3 p)
	{
		Teleport(p);
		path = null;
		pathGoal = null;
		goalP = null;
		stT = 0f;
		vel = Vector3.zero;
		LeaveSeat();
	}

	private void OnDisable()
	{
		if (seat != null && seat.busy == c)
		{
			seat.busy = null;
		}
	}

	public bool SeatNow(FBSeat S, bool enter)
	{
		List<string> list = c.npcSet?.sit.Where((string n) => FBRegistry.I.Clip(n)).ToList();
		if (list == null || list.Count == 0)
		{
			return false;
		}
		string key = c.npcSet.sex ?? "m";
		Vector3 vector = Quaternion.Euler(0f, S.yaw, 0f) * Vector3.forward;
		Teleport(new Vector3(S.p.x + vector.x * 0.3f, S.p.y, S.p.z + vector.z * 0.3f));
		c.Yaw = S.yaw;
		vel = Vector3.zero;
		seat = S;
		S.busy = c;
		c.seated = true;
		seatClip = null;
		seatY = 0f;
		seatYaw = 0f;
		seatXZ = Vector3.zero;
		seatBase = null;
		if (float.IsNaN(S.h))
		{
			List<float> list2 = (from h in Physics.RaycastAll(new Vector3(S.p.x, S.p.y + 1.6f, S.p.z), Vector3.down, 4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)
				where h.normal.y > 0.7f
				select h.point.y into y
				orderby y
				select y).ToList();
			if (list2.Count > 0)
			{
				float gnd = list2[0];
				float num = list2.Where((float y) => y - gnd > 0.28f && y - gnd < 0.75f).DefaultIfEmpty(0f / 0f).Max();
				S.p = new Vector3(S.p.x, gnd, S.p.z);
				S.h = (float.IsNaN(num) ? (gnd + 0.45f) : num);
			}
			else
			{
				S.h = S.p.y + 0.45f;
			}
		}
		(string, string) tuple = FBNpcAnims.SIT[key];
		string text = FBNpcAnims.SitEnter(c);
		if (c.npcSet != null && GameData.Skins.Exists((SkinDef s) => s.id == c.skinId && !string.IsNullOrEmpty(s.npc2)))
		{
			tuple = (FBRegistry.I.Clip(text) ? text : null, null);
		}
		c.StopDance(force: true);
		FBCharacter fBCharacter = c;
		string clip = FBUtil.Pick(list);
		object sitEnter;
		if (enter)
		{
			(sitEnter, _) = tuple;
		}
		else
		{
			sitEnter = null;
		}
		if (!fBCharacter.PlayDance(clip, (string)sitEnter, tuple.Item2))
		{
			return false;
		}
		st = "sit";
		stT = UnityEngine.Random.Range(30f, 90f);
		return true;
	}

	private void Teleport(Vector3 p)
	{
		if ((bool)cc)
		{
			cc.enabled = false;
		}
		base.transform.position = p;
		if ((bool)cc)
		{
			cc.enabled = true;
		}
	}

	private void Social()
	{
		List<FBRoleNpc> source = (from o in FBRole.npcs
			where (bool)o && o != c && o.alive && (o.Pos - c.Pos).magnitude < 6f
			select o.GetComponent<FBRoleNpc>() into o
			where o
			select o).ToList();
		FBRoleNpc fBRoleNpc = source.FirstOrDefault((FBRoleNpc o) => o.st == "talk" && o.talkC.HasValue && FBRole.npcs.Count((FBCharacter x) => (bool)x && x.GetComponent<FBRoleNpc>()?.talkC == o.talkC && x.GetComponent<FBRoleNpc>().st == "talk") < 4);
		if ((bool)fBRoleNpc && UnityEngine.Random.value < 0.45f)
		{
			JoinTalk(fBRoleNpc.talkC.Value, Mathf.Max(4f, fBRoleNpc.stT + UnityEngine.Random.Range(-2f, 4f)));
			return;
		}
		FBRoleNpc fBRoleNpc2 = source.FirstOrDefault((FBRoleNpc o) => o.st == "walk" && (o.c.Pos - c.Pos).magnitude < 4.5f);
		if ((bool)fBRoleNpc2 && UnityEngine.Random.value < 0.55f)
		{
			Vector3 vector = (c.Pos + fBRoleNpc2.c.Pos) / 2f;
			Vector3 normalized = (fBRoleNpc2.c.Pos - c.Pos).Flat().normalized;
			float t = UnityEngine.Random.Range(10f, 24f);
			bool shake = (c.npcSet?.sex == "m" || fBRoleNpc2.c.npcSet?.sex == "m") && UnityEngine.Random.value < 0.55f && (bool)FBRegistry.I.Clip("NPC_Greeting_Handshakes_M");
			(FBRoleNpc, int)[] array = new(FBRoleNpc, int)[2]
			{
				(this, -1),
				(fBRoleNpc2, 1)
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (fBRoleNpc3, num) = array[i];
				fBRoleNpc3.Teleport(new Vector3(vector.x + normalized.x * 0.55f * (float)num, fBRoleNpc3.c.Pos.y, vector.z + normalized.z * 0.55f * (float)num));
				fBRoleNpc3.JoinTalk(vector, t, shake);
			}
		}
		else if (FBRole.seats.Count > 0 && UnityEngine.Random.value < 0.3f)
		{
			FBSeat fBSeat = (from s in FBRole.seats
				where s.busy == null && (s.p - c.Pos).magnitude < 70f
				orderby (s.p - c.Pos).sqrMagnitude
				select s).Skip(UnityEngine.Random.Range(0, 3)).FirstOrDefault();
			if (fBSeat != null)
			{
				fBSeat.busy = c;
				seat = fBSeat;
				st = "gosit";
				stT = 45f;
			}
		}
	}

	private void JoinTalk(Vector3 C, float T, bool shake = false)
	{
		c.StopDance(force: true);
		st = "talk";
		stT = T;
		c.talking = true;
		talkC = C;
		vel = Vector3.zero;
		c.Yaw = Mathf.Atan2(C.x - c.Pos.x, C.z - c.Pos.z) * 57.29578f;
		if (shake && c.PlayDance("NPC_Greeting_Handshakes_M"))
		{
			greet = true;
			if (c.dance != null)
			{
				c.dance.len = Mathf.Min(c.dance.len, 6f);
			}
			return;
		}
		List<string> list = c.npcSet?.talk.Where((string n) => FBRegistry.I.Clip(n)).ToList();
		if (list != null && list.Count > 0)
		{
			c.PlayDance(FBUtil.Pick(list));
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBRoleNpc.Update");

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
		if (deltaTime <= 0f || !c)
		{
			return;
		}
		Vector3 tv;
		Vector3 side0;
		float steer;
		if (!c.alive)
		{
			vel = Vector3.zero;
			c.velocity = vel;
		}
		else
		{
			if (c.vehiclePose)
			{
				return;
			}
			stT -= deltaTime;
			float num = 0f;
			Vector3? vector = null;
			if (FBNpcRotina.On)
			{
				FBNpcVoz.Tick();
				if (React(deltaTime))
				{
					return;
				}
			}
			c.fleeing = st == "flee";
			FBCar cur = FBDrive.Cur;
			if (st != "dodge" && st != "sit" && st != "gocar")
			{
				if ((bool)cur)
				{
					Check(cur.transform.position, FBDrive.Fwd(cur.yaw) * Mathf.Sign(cur.v), Mathf.Abs(cur.v));
				}
				foreach (FBTransito.Car item in FBTransito.List)
				{
					if ((bool)item.car && item.mode == "drive")
					{
						Check(item.pos, item.fwd, item.v);
					}
				}
			}
			if (st == "gocar")
			{
				float num2 = (goalP.HasValue ? (c.Pos - goalP.Value).Flat().magnitude : 0f);
				if (goalP.HasValue && num2 < 1.2f)
				{
					FBTransito.Arrive(c);
					return;
				}
				if (stT <= 0f)
				{
					st = "walk";
				}
				if (num2 < 4f && goalP.HasValue)
				{
					Vector3 normalized = (goalP.Value - c.Pos).Flat().normalized;
					Move(normalized * 1.4f, deltaTime);
					return;
				}
				vector = goalP;
				num = 1.5f;
			}
			else if (st == "dodge")
			{
				vector = dodgeP;
				num = 4.6f;
				if (stT <= 0f)
				{
					st = ((prevSt == "party" && partyW != null && partyW.on) ? "party" : "walk");
					stT = ((st == "party") ? UnityEngine.Random.Range(10f, 30f) : UnityEngine.Random.Range(4f, 10f));
					if (FBNpcRotina.On && st == "walk")
					{
						Vector3 vector2 = (c.Pos - dodgeFrom).Flat();
						float num3 = Mathf.Abs(vector2.x * dodgeFwd.z - vector2.z * dodgeFwd.x);
						float num4 = FBNpcRotina.NearMiss(c, c.Pos - Vector3.Cross(Vector3.up, dodgeFwd) * Mathf.Sign(vector2.x * dodgeFwd.z - vector2.z * dodgeFwd.x) * num3, num3);
						if (num4 > 0f)
						{
							st = "react";
							stT = num4;
						}
					}
				}
			}
			else if (st == "pause")
			{
				vel = Vector3.zero;
				if (stT <= 0f || c.dance == null)
				{
					c.StopDance(force: true);
					st = "walk";
					goalP = FBTransito.SidewalkPoint(c.Pos, 50f) ?? goalP;
					stT = UnityEngine.Random.Range(10f, 22f);
				}
			}
			else if (st == "flee")
			{
				vector = fleeP;
				num = 3.8f;
				if (stT <= 0f || !fleeP.HasValue || (c.Pos - fleeP.Value).Flat().magnitude < 1.5f)
				{
					st = "walk";
					goalP = null;
					stT = 0f;
					c.fleeing = false;
				}
			}
			else if (st == "party")
			{
				FBParedao.Wall wall = partyW;
				FBCar fBCar = wall?.car;
				if (wall == null || !wall.on || !wall.playing || !fBCar || stT <= 0f)
				{
					c.StopDance();
					st = "walk";
					partyCd = UnityEngine.Random.Range(25f, 70f);
					goalP = FBTransito.SidewalkPoint(c.Pos, 60f);
					stT = UnityEngine.Random.Range(10f, 20f);
				}
				else
				{
					Vector3 vector3 = (fBCar.soundWallNode ? fBCar.soundWallNode.position : fBCar.transform.position);
					if (c.dance == null && goalP.HasValue && (c.Pos - goalP.Value).magnitude > 1.1f && !atParty)
					{
						vector = goalP;
						num = 1.5f;
					}
					else
					{
						atParty = true;
						c.Yaw = Mathf.LerpAngle(c.Yaw, Mathf.Atan2(vector3.x - c.Pos.x, vector3.z - c.Pos.z) * 57.29578f, Mathf.Min(1f, deltaTime * 3f));
						if (c.dance == null && dancer && (danceT -= deltaTime) <= 0f)
						{
							List<string> list = c.npcSet?.dance.Where((string n) => FBRegistry.I.Clip(n)).ToList();
							c.PlayDance(FBUtil.Pick((list != null && list.Count > 0) ? list : FBRole.Dances().ToList()));
							danceT = UnityEngine.Random.Range(0.6f, 3.5f);
						}
					}
				}
			}
			else if (st == "gosit" || st == "sit" || st == "standup")
			{
				FBSeat fBSeat = seat;
				if (fBSeat == null)
				{
					st = "walk";
				}
				else if (st == "gosit")
				{
					Vector3 vector4 = FBRole.SeatApproach(fBSeat);
					float magnitude = (c.Pos - vector4).Flat().magnitude;
					if (stT <= 0f || ((bool)fBSeat.busy && fBSeat.busy != c))
					{
						st = "walk";
						seat = null;
						if (fBSeat.busy == c)
						{
							fBSeat.busy = null;
						}
					}
					else if (magnitude < 0.7f)
					{
						if (!SeatNow(fBSeat, enter: true))
						{
							st = "walk";
							fBSeat.busy = null;
							seat = null;
						}
					}
					else
					{
						vector = vector4;
						num = c.walkSpeed;
					}
				}
				else if (st == "sit")
				{
					vel = Vector3.zero;
					c.Yaw = fBSeat.yaw + seatYaw;
					if (stT <= 0f || c.dance == null)
					{
						c.StopDance();
						st = "standup";
						stT = 8f;
					}
				}
				else
				{
					vel = Vector3.zero;
					if (c.dance == null || stT <= 0f)
					{
						c.StopDance(force: true);
						LeaveSeat();
						st = "walk";
						stT = 0f;
						goalP = null;
					}
				}
			}
			else
			{
				if (st == "talk")
				{
					if (talkC.HasValue)
					{
						c.Yaw = Mathf.LerpAngle(c.Yaw, Mathf.Atan2(talkC.Value.x - c.Pos.x, talkC.Value.z - c.Pos.z) * 57.29578f, Mathf.Min(1f, deltaTime * 4f));
					}
					if (talkC.HasValue && !greet)
					{
						FBNpcRotina.TalkTick(c, talkC.Value, deltaTime);
					}
					if (greet && c.dance == null)
					{
						greet = false;
						List<string> list2 = c.npcSet?.talk.Where((string n) => FBRegistry.I.Clip(n)).ToList();
						if (list2 != null && list2.Count > 0)
						{
							c.PlayDance(FBUtil.Pick(list2));
						}
					}
					if (stT <= 0f)
					{
						st = "walk";
						c.talking = false;
						talkC = null;
						c.StopDance(force: true);
					}
				}
				if ((partyCd -= deltaTime) <= 0f)
				{
					partyCd = 2.5f;
					foreach (FBParedao.Wall W in FBParedao.List())
					{
						if (W.on && W.playing && (bool)W.car && !W.local && !((W.car.transform.position - c.Pos).magnitude > 95f) && FBRole.npcs.Count(delegate(FBCharacter o)
						{
							if ((bool)o)
							{
								FBRoleNpc component = o.GetComponent<FBRoleNpc>();
								if ((object)component != null && component.partyW == W)
								{
									return component.st == "party";
								}
							}
							return false;
						}) < 14)
						{
							FBCar car = W.car;
							Vector3 vector5 = -car.transform.forward.Flat().normalized;
							Vector3 vector6 = new Vector3(vector5.z, 0f, 0f - vector5.x);
							float num5 = (0f - car.lbox.min.z) * car.scale;
							Vector3 vector7 = car.transform.position + vector5 * (num5 + UnityEngine.Random.Range(3f, 9f)) + vector6 * UnityEngine.Random.Range(-5f, 5f);
							goalP = FBNav.RandomPoint(vector7, 2.5f).GetValueOrDefault(vector7);
							st = "party";
							partyW = W;
							atParty = false;
							stT = UnityEngine.Random.Range(20f, 60f);
							danceT = UnityEngine.Random.Range(0.5f, 4f);
							c.talking = false;
							LeaveSeat();
							break;
						}
					}
				}
				if (st == "walk")
				{
					if (!goalP.HasValue || (c.Pos - goalP.Value).magnitude < 1.5f || stT <= 0f)
					{
						if (goalP.HasValue)
						{
							Social();
						}
						if (goalP.HasValue && st == "walk" && FBNpcRotina.On)
						{
							float num6 = FBNpcRotina.Pause(c);
							if (num6 > 0f)
							{
								st = "pause";
								stT = num6;
								vel = Vector3.zero;
							}
						}
						if (st == "walk")
						{
							goalP = FBTransito.SidewalkPoint(c.Pos, 50f) ?? goalP;
							stT = UnityEngine.Random.Range(10f, 22f);
						}
					}
					vector = goalP;
					num = c.walkSpeed;
					FBNpcRotina.WalkTick(c, deltaTime);
				}
			}
			tv = Vector3.zero;
			if (vector.HasValue)
			{
				Vector3 valueOrDefault = vector.GetValueOrDefault();
				navT -= deltaTime;
				if (path == null || !pathGoal.HasValue || (pathGoal.Value - valueOrDefault).sqrMagnitude > 4f || navT <= 0f)
				{
					navT = 9f + UnityEngine.Random.value * 5f;
					pathGoal = valueOrDefault;
					path = (FBPedNav.Ready ? FBPedNav.Path(c.Pos, valueOrDefault) : null) ?? FBNav.Path(c.Pos, valueOrDefault) ?? new List<Vector3> { valueOrDefault };
				}
				while (path.Count > 1 && (path[0] - c.Pos).Flat().magnitude < 1f)
				{
					path.RemoveAt(0);
				}
				Vector3 vector8 = (Carrot(1.7f) - c.Pos).Flat();
				if (vel.sqrMagnitude > 0.04f && st == "walk")
				{
					float num7 = Vector3.Angle(vel.Flat(), vector8);
					if (num7 > 45f)
					{
						num *= Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(45f, 140f, num7));
					}
					num *= 1f + 0.06f * Mathf.Sin(Time.time * 0.31f + phase);
				}
				if (vector8.magnitude > 0.3f)
				{
					Vector3 normalized2 = vector8.normalized;
					bool flag = false;
					if (FBRoads.Ready)
					{
						bool flag2 = FBRoads.IsRoad(c.Pos);
						if (!flag2 && (FBRoads.IsRoad(c.Pos + normalized2 * 1.1f) || FBRoads.IsRoad(c.Pos + normalized2 * 2.1f)))
						{
							Vector3 p = c.Pos + normalized2 * 4f;
							if (FBTransito.Danger(p, 3.2f, (crossWait < 14f) ? 5.5f : 2f) || PlayerCarComing(p))
							{
								crossWait += deltaTime;
								flag = true;
							}
							else
							{
								crossWait = 0f;
							}
						}
						else if (flag2)
						{
							crossWait = 0f;
							num *= 1.3f;
						}
					}
					if (!flag)
					{
						tv = normalized2 * num;
						c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(vector8).eulerAngles.y, Mathf.Min(1f, deltaTime * 5f));
					}
					else
					{
						c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(vector8).eulerAngles.y, Mathf.Min(1f, deltaTime * 3f));
					}
				}
			}
			if (tv.sqrMagnitude > 0.04f)
			{
				side0 = new Vector3(tv.z, 0f, 0f - tv.x).normalized;
				steer = 0f;
				foreach (FBCharacter npc in FBRole.npcs)
				{
					if ((bool)npc && !(npc == c) && npc.alive)
					{
						Avoid(npc.Pos, npc.velocity, 0.9f);
					}
				}
				FBCharacter player = FBMatch.Player;
				if ((bool)player && !player.vehiclePose)
				{
					Avoid(player.Pos, player.velocity, 1.1f);
				}
				if (Mathf.Abs(steer) > 0.01f)
				{
					tv = (tv + side0 * Mathf.Clamp(steer, -1f, 1f) * tv.magnitude * 0.8f).normalized * tv.magnitude;
				}
			}
			if (tv.sqrMagnitude > 0f)
			{
				Vector3 zero = Vector3.zero;
				foreach (FBCharacter npc2 in FBRole.npcs)
				{
					if ((bool)npc2 && !(npc2 == c))
					{
						Vector3 vector9 = (c.Pos - npc2.Pos).Flat();
						float sqrMagnitude = vector9.sqrMagnitude;
						if (sqrMagnitude < 1.21f && sqrMagnitude > 1E-06f)
						{
							sqrMagnitude = Mathf.Sqrt(sqrMagnitude);
							zero += vector9 / sqrMagnitude * (1.1f - sqrMagnitude);
						}
					}
				}
				FBCharacter player2 = FBMatch.Player;
				if ((bool)player2 && !player2.vehiclePose)
				{
					Vector3 vector10 = (c.Pos - player2.Pos).Flat();
					float magnitude2 = vector10.magnitude;
					if (magnitude2 < 1.3f && magnitude2 > 0.001f)
					{
						zero += vector10 / magnitude2 * (1.3f - magnitude2) * 1.6f;
					}
				}
				if (zero.sqrMagnitude > 1E-06f)
				{
					Vector3 normalized3 = new Vector3(tv.z, 0f, 0f - tv.x).normalized;
					tv += normalized3 * Vector3.Dot(zero, normalized3) * 2.2f + zero * 0.6f;
				}
			}
			if (c.dance != null)
			{
				tv = Vector3.zero;
			}
			if (tv.sqrMagnitude > 0f)
			{
				if ((stkT += deltaTime) > 1.5f)
				{
					float num8 = (stkP.HasValue ? (c.Pos - stkP.Value).Flat().magnitude : 9f);
					stkT = 0f;
					stkP = c.Pos;
					if (num8 < 0.45f)
					{
						stkN++;
						path = null;
						if (st == "gosit")
						{
							LeaveSeat();
							st = "walk";
						}
						if (st == "walk" || stkN > 1)
						{
							goalP = FBTransito.SidewalkPoint(c.Pos, 35f) ?? goalP;
							stT = UnityEngine.Random.Range(8f, 16f);
						}
						c.Yaw += ((UnityEngine.Random.value < 0.5f) ? 1 : (-1)) * 68;
						tv = Quaternion.Euler(0f, c.Yaw, 0f) * Vector3.forward * 1.2f;
					}
					else
					{
						stkN = 0;
					}
				}
			}
			else
			{
				stkT = 0f;
				stkP = null;
			}
			if (seat != null && (st == "sit" || st == "standup"))
			{
				Vector3 position = base.transform.position;
				position.y = seat.p.y + seatY;
				if ((bool)cc)
				{
					cc.enabled = false;
				}
				base.transform.position = position;
				vel = Vector3.zero;
				c.velocity = Vector3.zero;
				c.animVel = Vector2.zero;
				c.grounded = true;
			}
			else
			{
				if ((bool)cc && !cc.enabled)
				{
					cc.enabled = true;
				}
				Move(tv, deltaTime);
			}
		}
		void Avoid(Vector3 op, Vector3 ov, float rad)
		{
			Vector3 vector11 = (op - c.Pos).Flat();
			float sqrMagnitude2 = vector11.sqrMagnitude;
			if (!(sqrMagnitude2 > 36f) && !(sqrMagnitude2 < 0.0001f))
			{
				Vector3 vector12 = (ov - tv).Flat();
				float sqrMagnitude3 = vector12.sqrMagnitude;
				if (!(sqrMagnitude3 < 0.0001f))
				{
					float num9 = (0f - Vector3.Dot(vector11, vector12)) / sqrMagnitude3;
					if (!(num9 <= 0f) && !(num9 > 2.5f))
					{
						Vector3 vector13 = vector11 + vector12 * num9;
						if (!(vector13.magnitude > rad))
						{
							float num10 = Vector3.Dot(vector11, side0);
							steer += (float)((!(num10 > 0.15f)) ? 1 : (-1)) * (1f - num9 / 2.5f) * (rad - vector13.magnitude) / rad;
						}
					}
				}
			}
		}
		void Check(Vector3 cp, Vector3 f, float v)
		{
			if (!(st == "dodge") && !(v < 3.5f))
			{
				Vector3 lhs = (c.Pos - cp).Flat();
				if (!(lhs.sqrMagnitude > 196f))
				{
					float num11 = Vector3.Dot(lhs, f);
					float num12 = lhs.x * f.z - lhs.z * f.x;
					if (num11 > 0f && num11 < 4f + v * 1.1f && Mathf.Abs(num12) < 2.6f)
					{
						float num13 = ((num12 == 0f) ? 1f : Mathf.Sign(num12));
						c.StopDance();
						prevSt = ((st == "party") ? "party" : "walk");
						st = "dodge";
						stT = 1.3f;
						dodgeP = new Vector3(c.Pos.x + f.z * num13 * 4.5f, c.Pos.y, c.Pos.z - f.x * num13 * 4.5f);
						LeaveSeat();
						dodgeFrom = cp;
						dodgeFwd = f;
						c.talking = false;
						talkC = null;
					}
				}
			}
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBRoleNpc.LateUpdate");

	private void LateUpdate()
	{
		if (!FBTempo.On)
		{
			LateUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		LateUpdate__fbt();
		FBTempo.Soma(__fbt_LateUpdate, t0);
	}

	private void LateUpdate__fbt()
	{
		if (seat == null || st != "sit" || !c || !c.an || float.IsNaN(seat.h))
		{
			return;
		}
		FBCharacter.Dance dance = c.dance;
		if (dance == null || dance.t < 0.35f || Regex.IsMatch(dance.clip ?? "", "stand_to_sit|sit_to_stand", RegexOptions.IgnoreCase))
		{
			return;
		}
		Transform boneTransform = c.an.GetBoneTransform(HumanBodyBones.Hips);
		Transform boneTransform2 = c.an.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		Transform boneTransform3 = c.an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
		if (!boneTransform)
		{
			return;
		}
		float y = base.transform.lossyScale.y;
		float num = Mathf.Min(boneTransform.position.y, ((bool)boneTransform2 && (bool)boneTransform3) ? ((boneTransform2.position.y + boneTransform3.position.y) / 2f) : boneTransform.position.y) - 0.085f * y;
		float num2 = Mathf.Min(1f, Time.deltaTime * 6f);
		if ((bool)boneTransform2 && (bool)boneTransform3)
		{
			Vector3 from = Vector3.Cross(boneTransform3.position - boneTransform2.position, Vector3.up);
			from.y = 0f;
			Vector3 vector = Quaternion.Euler(0f, seat.yaw, 0f) * Vector3.forward;
			if (from.sqrMagnitude > 1E-06f)
			{
				float num3 = Vector3.SignedAngle(from, vector, Vector3.up);
				seatYaw = Mathf.Clamp(seatYaw + num3 * num2, -180f, 180f);
				base.transform.rotation = Quaternion.Euler(0f, seat.yaw + seatYaw, 0f);
			}
			float num4 = ((seat.kind == "cadeira") ? 0.07f : 0.02f);
			Vector3 vector2 = seat.p - vector * num4;
			Vector3 vector3 = new Vector3(vector2.x - boneTransform.position.x, 0f, vector2.z - boneTransform.position.z);
			seatXZ = Vector3.ClampMagnitude(seatXZ + vector3 * num2, 1.2f);
		}
		float num5 = seat.h - num;
		float num6 = ((Mathf.Abs(num5) < 0.006f) ? 0f : (Mathf.Clamp(num5, -0.6f, 0.6f) * Mathf.Min(1f, Time.deltaTime * 10f)));
		seatY = Mathf.Clamp(seatY + num6, -1.1f, 0.4f);
		Vector3 valueOrDefault = seatBase.GetValueOrDefault();
		if (!seatBase.HasValue)
		{
			valueOrDefault = base.transform.position - seatXZ;
			seatBase = valueOrDefault;
		}
		base.transform.position = new Vector3(seatBase.Value.x + seatXZ.x, seat.p.y + seatY, seatBase.Value.z + seatXZ.z);
	}

	private void LeaveSeat()
	{
		if ((bool)c)
		{
			c.seated = false;
		}
		if (seat != null && seat.busy == c)
		{
			seat.busy = null;
		}
		seat = null;
		seatY = 0f;
		seatYaw = 0f;
		seatXZ = Vector3.zero;
		seatBase = null;
		if ((bool)cc && !cc.enabled)
		{
			cc.enabled = true;
		}
	}

	private void Move(Vector3 tv, float dt)
	{
		float magnitude = vel.Flat().magnitude;
		float magnitude2 = tv.Flat().magnitude;
		bool flag = st == "dodge";
		float num = (flag ? 7f : 1.5f);
		float num2 = (flag ? 8f : 2.8f);
		float num3 = Mathf.MoveTowards(magnitude, magnitude2, ((magnitude2 > magnitude) ? num : num2) * dt);
		Vector3 vector = ((magnitude > 0.05f) ? (vel.Flat() / magnitude) : ((magnitude2 > 0.01f) ? (tv.Flat() / magnitude2) : base.transform.forward.Flat().normalized));
		if (magnitude2 > 0.01f)
		{
			Vector3 target = tv.Flat() / magnitude2;
			float num4 = (float)(flag ? 540 : ((magnitude < 0.4f) ? 260 : 170)) * dt;
			vector = Vector3.RotateTowards(vector, target, num4 * ((float)Math.PI / 180f), 0f);
		}
		vel = vector * num3;
		if ((bool)cc && cc.enabled)
		{
			if (cvStep < 0f)
			{
				cvStep = cc.stepOffset;
			}
			vy = (cvGround ? (-2f) : (vy - 19f * dt));
			FBMotor.Move(cc, (vel + Vector3.up * vy) * dt, cvGround, vy > 0f, cvStep, 0.45f * c.ScaleK, ref cvSlide, out cvGround, out cvG);
		}
		else
		{
			vy = (((bool)cc && cc.isGrounded) ? (-2f) : (vy - 19f * dt));
		}
		float magnitude3 = vel.magnitude;
		float num5 = magnitude3 * 1.12f;
		Vector3 vector2 = Quaternion.Inverse(Quaternion.Euler(0f, c.Yaw, 0f)) * vel;
		float magnitude4 = vector2.magnitude;
		float num6 = ((magnitude3 > 0.35f && magnitude3 < 2.2f) ? Mathf.Max(num5, 1.57f) : num5);
		c.animVel = ((magnitude3 < 0.05f) ? Vector2.zero : new Vector2(vector2.x / magnitude4 * num6, vector2.z / magnitude4 * num6));
		c.velocity = vel;
		c.grounded = !cc || (cc.enabled ? cvGround : cc.isGrounded);
		if (tv.sqrMagnitude > 0f && vel.sqrMagnitude > 0.01f)
		{
			c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation((vel.Flat().sqrMagnitude > 0.0001f) ? vel.Flat() : base.transform.forward).eulerAngles.y, Mathf.Min(1f, dt * 6f));
		}
	}
}
}
