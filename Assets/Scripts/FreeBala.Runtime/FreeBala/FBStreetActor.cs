using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
[DefaultExecutionOrder(1002)]
public class FBStreetActor : MonoBehaviour
{
	private FBCharacter c;

	private CharacterController cc;

	private bool gnd = true;

	private float slide;

	private float step = -1f;

	private float vy;

	private float navT;

	private float breath;

	private FBMotor.Ground g;

	private Vector3 vel;

	private List<Vector3> path;

	private Vector3? pathGoal;

	public Vector3? goal;

	public float speed = 1.3f;

	public float arriveR = 0.3f;

	public float? faceYaw;

	public bool run;

	public bool flatFeet;

	public bool life;

	public bool ready;

	public Action onArrive;

	private Transform lFoot;

	private Transform rFoot;

	private Transform lToe;

	private Transform rToe;

	private Transform neck;

	private Transform spine;

	private float seed;

	public bool Arrived => !goal.HasValue;

	public void Init(FBCharacter ch)
	{
		c = ch;
		cc = GetComponent<CharacterController>();
		seed = UnityEngine.Random.value * 10f;
		Animator an = c.an;
		if ((bool)an && an.isHuman)
		{
			lFoot = an.GetBoneTransform(HumanBodyBones.LeftFoot);
			rFoot = an.GetBoneTransform(HumanBodyBones.RightFoot);
			lToe = an.GetBoneTransform(HumanBodyBones.LeftToes);
			rToe = an.GetBoneTransform(HumanBodyBones.RightToes);
			neck = an.GetBoneTransform(HumanBodyBones.Neck);
			spine = an.GetBoneTransform(HumanBodyBones.Spine);
		}
	}

	public void Go(Vector3 p, float spd, float r = 0.3f)
	{
		goal = p;
		speed = spd;
		arriveR = r;
		path = null;
		pathGoal = null;
	}

	public void Stop()
	{
		goal = null;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBStreetActor.Update");

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
		if (deltaTime <= 0f || !c || c.vehiclePose)
		{
			return;
		}
		if (!c.alive)
		{
			vel = Vector3.zero;
			c.velocity = vel;
			c.animVel = Vector2.zero;
			return;
		}
		Vector3 vector = Vector3.zero;
		Vector3? vector2 = goal;
		if (vector2.HasValue)
		{
			Vector3 valueOrDefault = vector2.GetValueOrDefault();
			if (c.dance == null)
			{
				float magnitude = (valueOrDefault - c.Pos).Flat().magnitude;
				if (magnitude <= arriveR)
				{
					goal = null;
					Action action = onArrive;
					onArrive = null;
					action?.Invoke();
				}
				else
				{
					navT -= deltaTime;
					if (path == null || !pathGoal.HasValue || (pathGoal.Value - valueOrDefault).sqrMagnitude > 1f || navT <= 0f)
					{
						navT = 3f;
						pathGoal = valueOrDefault;
						path = ((!(magnitude > 5f)) ? null : ((FBPedNav.Ready ? FBPedNav.Path(c.Pos, valueOrDefault) : null) ?? FBNav.Path(c.Pos, valueOrDefault)));
						if (path == null)
						{
							path = new List<Vector3> { valueOrDefault };
						}
					}
					while (path.Count > 1 && (path[0] - c.Pos).Flat().magnitude < 0.8f)
					{
						path.RemoveAt(0);
					}
					Vector3 vector3 = (path[0] - c.Pos).Flat();
					float num = Mathf.Min(speed, Mathf.Max(0.5f, magnitude * 1.4f));
					if (vector3.sqrMagnitude > 0.0001f)
					{
						vector = vector3.normalized * num;
					}
				}
			}
		}
		float magnitude2 = vel.magnitude;
		float magnitude3 = vector.magnitude;
		float num2 = Mathf.MoveTowards(magnitude2, magnitude3, ((!(magnitude3 > magnitude2)) ? 3.4f : (run ? 7f : 2.4f)) * deltaTime);
		Vector3 vector4 = ((magnitude2 > 0.05f) ? (vel / magnitude2) : ((magnitude3 > 0.01f) ? (vector / magnitude3) : base.transform.forward.Flat().normalized));
		if (magnitude3 > 0.01f)
		{
			vector4 = Vector3.RotateTowards(vector4, vector / magnitude3, (float)((magnitude2 < 0.4f) ? 320 : 210) * ((float)Math.PI / 180f) * deltaTime, 0f);
		}
		vel = vector4 * num2;
		vel.y = 0f;
		if ((bool)cc && cc.enabled)
		{
			if (step < 0f)
			{
				step = cc.stepOffset;
			}
			vy = (gnd ? (-2f) : (vy - 19f * deltaTime));
			FBMotor.Move(cc, (vel + Vector3.up * vy) * deltaTime, gnd, vy > 0f, step, 0.45f * c.ScaleK, ref slide, out gnd, out g);
		}
		float magnitude4 = vel.magnitude;
		Vector3 vector5 = Quaternion.Inverse(Quaternion.Euler(0f, c.Yaw, 0f)) * vel;
		float magnitude5 = vector5.magnitude;
		float num8;
		if (!c.Unarmed)
		{
			Dictionary<string, object> o = GameData.Section("puppet");
			float num3 = magnitude4 / Mathf.Max(0.01f, c.ScaleK);
			float num4 = Json.F(o, "playerWalkSpeed", 4f);
			float num5 = Json.F(o, "playerRunSpeed", 6.8f);
			float num6 = Json.F(o, "aiWalkAnimSpeed", 2f);
			float num7 = Json.F(o, "aiRunAnimSpeed", 5f);
			num8 = ((num3 <= num4) ? (num3 / num4 * num6) : (num6 + (num3 - num4) / (num5 - num4) * (num7 - num6)));
		}
		else
		{
			num8 = ((magnitude4 > 0.35f && magnitude4 < 2.2f) ? Mathf.Max(magnitude4 * 1.12f, 1.57f) : (magnitude4 * 1.12f));
		}
		c.animVel = ((magnitude4 < 0.05f) ? Vector2.zero : new Vector2(vector5.x / magnitude5 * num8, vector5.z / magnitude5 * num8));
		c.velocity = vel;
		c.grounded = !cc || gnd;
		c.sprint = run && magnitude4 > 3f;
		if (magnitude4 > 0.2f)
		{
			c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(vel).eulerAngles.y, Mathf.Min(1f, deltaTime * 7f));
			return;
		}
		float? num9 = faceYaw;
		if (num9.HasValue)
		{
			float valueOrDefault2 = num9.GetValueOrDefault();
			c.Yaw = Mathf.LerpAngle(c.Yaw, valueOrDefault2, Mathf.Min(1f, deltaTime * 4f));
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBStreetActor.LateUpdate");

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
		if (!c || !c.alive || c.vehiclePose)
		{
			return;
		}
		float num = Time.time + seed;
		if (flatFeet && c.dance != null)
		{
			Flat(lFoot, lToe);
			Flat(rFoot, rToe);
		}
		if (life && c.dance != null)
		{
			breath = Mathf.Lerp(breath, 1f, Time.deltaTime * 2f);
			if ((bool)spine)
			{
				spine.rotation = Quaternion.AngleAxis(Mathf.Sin(num * 1.7f) * 1.2f * breath, base.transform.right) * Quaternion.AngleAxis(Mathf.Sin(num * 0.37f) * 2.2f * breath, base.transform.up) * spine.rotation;
			}
			if ((bool)neck)
			{
				neck.rotation = Quaternion.AngleAxis((Mathf.PerlinNoise(num * 0.18f, 3f) - 0.5f) * 38f * breath, Vector3.up) * Quaternion.AngleAxis((Mathf.PerlinNoise(num * 0.13f, 7f) - 0.35f) * 14f * breath, base.transform.right) * neck.rotation;
			}
		}
	}

	private void Flat(Transform foot, Transform toe)
	{
		if (!foot)
		{
			return;
		}
		Vector3 vector = (toe ? toe.position : (foot.position + foot.rotation * Vector3.forward * 0.12f)) - foot.position;
		float magnitude = vector.magnitude;
		if (magnitude < 0.0001f)
		{
			return;
		}
		vector /= magnitude;
		if (!(Mathf.Asin(Mathf.Clamp(vector.y, -1f, 1f)) * 57.29578f > -48f))
		{
			Vector3 vector2 = vector.Flat();
			if (vector2.sqrMagnitude < 0.0001f)
			{
				vector2 = base.transform.forward;
			}
			vector2.Normalize();
			Vector3 normalized = (vector2 * Mathf.Cos((float)Math.PI * 8f / 45f) + Vector3.down * Mathf.Sin((float)Math.PI * 8f / 45f)).normalized;
			foot.rotation = Quaternion.FromToRotation(vector, normalized) * foot.rotation;
		}
	}
}
}
