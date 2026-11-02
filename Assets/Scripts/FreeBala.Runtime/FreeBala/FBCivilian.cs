using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public class FBCivilian : MonoBehaviour
{
	private FBCharacter c;

	private CharacterController cc;

	private bool cvGround = true;

	private float cvSlide;

	private float cvStep = -1f;

	private FBMotor.Ground cvG;

	private string st = "walk";

	private float stT;

	private float vy;

	private Vector3? goal;

	private Vector3 vel;

	private List<Vector3> path;

	private float navT;

	private void Awake()
	{
		c = GetComponent<FBCharacter>();
		cc = GetComponent<CharacterController>();
		c.civ = true;
		c.combat = false;
		stT = Random.Range(2f, 6f);
	}

	public void OnHit()
	{
		c.Trigger("hit");
		st = "flee";
		stT = 6f;
		goal = FBNav.RandomPoint(c.Pos, 30f) ?? c.Pos;
	}

	private bool AimedAt()
	{
		FBCharacter player = FBMatch.Player;
		Camera main = Camera.main;
		if (!player || !player.alive || !main || (!player.aiming && !(player.lastShot < 1.2f)))
		{
			return false;
		}
		if ((c.Pos - player.Pos).magnitude > 32f)
		{
			return false;
		}
		Vector3 forward = main.transform.forward;
		Vector3 vector = c.Pos + Vector3.up * 1.2f * c.ScaleK - main.transform.position;
		float num = Vector3.Dot(vector, forward);
		if (num > 0f)
		{
			return (vector - forward * num).magnitude < 1.3f;
		}
		return false;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBCivilian.Update");

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
		if (deltaTime <= 0f)
		{
			return;
		}
		if (!c.alive)
		{
			vel = Vector3.zero;
			c.velocity = vel;
			return;
		}
		stT -= deltaTime;
		Vector3? lastNoiseP = FBBotBrain.lastNoiseP;
		int num;
		if (lastNoiseP.HasValue)
		{
			Vector3 valueOrDefault = lastNoiseP.GetValueOrDefault();
			if (Time.time - FBBotBrain.lastNoiseT < 2.5f)
			{
				num = (((valueOrDefault - c.Pos).magnitude < 40f) ? 1 : 0);
				goto IL_0095;
			}
		}
		num = 0;
		goto IL_0095;
		IL_0095:
		float num2 = 0f;
		Vector3? vector = null;
		if (num != 0 && st != "flee")
		{
			FBNpcVoz.Say(c, "perigo", 1f, 5f, 6f, 0.5f, Random.Range(0f, 0.6f));
			st = "flee";
			stT = Random.Range(5f, 9f);
			Vector3 vector2 = (c.Pos - FBBotBrain.lastNoiseP.Value).Flat().normalized * 30f;
			goal = FBNav.RandomPoint(c.Pos + vector2, 15f) ?? (c.Pos + vector2);
			c.talking = false;
		}
		else if (st != "flee" && AimedAt())
		{
			if (st != "surrender")
			{
				FBNpcVoz.Say(c, "namira", 1f, 4.5f, 5f, 0.5f);
			}
			st = "surrender";
			stT = 3f;
			c.talking = false;
		}
		if (st == "flee")
		{
			vector = goal;
			num2 = 3.6f;
			if (stT <= 0f)
			{
				st = "walk";
			}
		}
		else if (st == "surrender")
		{
			if (stT <= 0f)
			{
				st = "walk";
			}
			FBCharacter player = FBMatch.Player;
			if ((bool)player)
			{
				c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation((player.Pos - c.Pos).Flat()).eulerAngles.y, Mathf.Min(1f, deltaTime * 4f));
			}
		}
		else if (st == "talk")
		{
			if (stT <= 0f)
			{
				st = "walk";
				c.talking = false;
			}
		}
		else
		{
			if (!goal.HasValue || (c.Pos - goal.Value).magnitude < 1.5f || stT <= 0f)
			{
				FBCharacter fBCharacter = FBMatch.Civs.FirstOrDefault((FBCharacter o) => (bool)o && o != c && o.alive && (o.Pos - c.Pos).magnitude < 4f);
				if (goal.HasValue && (bool)fBCharacter && Random.value < 0.6f)
				{
					st = "talk";
					stT = Random.Range(6f, 12f);
					c.talking = true;
					c.Yaw = Quaternion.LookRotation((fBCharacter.Pos - c.Pos).Flat()).eulerAngles.y;
				}
				goal = FBNav.RandomPoint(c.Pos, 45f) ?? goal;
				stT = Random.Range(10f, 20f);
			}
			if (st == "walk")
			{
				vector = goal;
				num2 = 1.3f;
			}
		}
		c.surrender = st == "surrender";
		c.fleeing = st == "flee";
		Vector3 b = Vector3.zero;
		if (vector.HasValue)
		{
			Vector3 valueOrDefault2 = vector.GetValueOrDefault();
			navT -= deltaTime;
			if (path == null || navT <= 0f)
			{
				navT = 2f;
				path = FBNav.Path(c.Pos, valueOrDefault2) ?? new List<Vector3> { valueOrDefault2 };
			}
			while (path.Count > 1 && (path[0] - c.Pos).Flat().magnitude < 1.3f)
			{
				path.RemoveAt(0);
			}
			Vector3 forward = (path[0] - c.Pos).Flat();
			if (forward.magnitude > 0.3f)
			{
				b = forward.normalized * num2 * c.ScaleK;
				c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(forward).eulerAngles.y, Mathf.Min(1f, deltaTime * 5f));
			}
		}
		vel = Vector3.Lerp(vel, b, Mathf.Min(1f, deltaTime * 5f));
		if (cc.enabled)
		{
			if (cvStep < 0f)
			{
				cvStep = cc.stepOffset;
			}
			vy = (cvGround ? (-2f) : (vy - 19f * deltaTime));
			FBMotor.Move(cc, (vel + Vector3.up * vy) * deltaTime, cvGround, vy > 0f, cvStep, 0.45f * c.ScaleK, ref cvSlide, out cvGround, out cvG);
		}
		else
		{
			vy = (cc.isGrounded ? (-2f) : (vy - 19f * deltaTime));
		}
		float num3 = vel.magnitude / c.ScaleK;
		float num4 = ((num3 <= 1.3f) ? (num3 / 1.3f) : (1f + (num3 - 1.3f) / 2.3f * 1.5f));
		Vector3 vector3 = Quaternion.Inverse(Quaternion.Euler(0f, c.Yaw, 0f)) * vel;
		float magnitude = vector3.magnitude;
		c.animVel = ((magnitude > 0.001f) ? new Vector2(vector3.x / magnitude * num4, vector3.z / magnitude * num4) : Vector2.zero);
		c.velocity = vel;
		c.grounded = (cc.enabled ? cvGround : cc.isGrounded);
	}
}
}
