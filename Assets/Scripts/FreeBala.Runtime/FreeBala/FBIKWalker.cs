using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBIKWalker : MonoBehaviour
{
	public Vector3[] route;

	public float speed = 1.6f;

	public float pause = 1.2f;

	[NonSerialized]
	public FBCharacter ch;

	private CharacterController cc;

	private int i = 1;

	private int dir = 1;

	private float vy;

	private float slide;

	private float wait;

	private float stepLocal = -1f;

	private float stuckT;

	private Vector3 hv;

	private Vector3 lastP;

	private bool onGround = true;

	private FBMotor.Ground ground;

	public bool OnGround => onGround;

	public int Leg => i;

	private void Awake()
	{
		ch = GetComponent<FBCharacter>();
		cc = GetComponent<CharacterController>();
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBIKWalker.Update");

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
		if (deltaTime <= 0f || !ch || !cc || route == null || route.Length < 2)
		{
			return;
		}
		float scaleK = ch.ScaleK;
		if (stepLocal < 0f)
		{
			stepLocal = cc.stepOffset;
		}
		Vector3 vector = (route[i] - base.transform.position).Flat();
		Vector3 b = Vector3.zero;
		if (wait > 0f)
		{
			wait -= deltaTime;
		}
		else if (vector.magnitude < 0.35f * scaleK)
		{
			if (i + dir < 0 || i + dir >= route.Length)
			{
				dir = -dir;
				wait = pause;
			}
			i += dir;
		}
		else
		{
			b = vector.normalized * speed * scaleK;
		}
		stuckT = ((b.sqrMagnitude > 0.01f && (base.transform.position - lastP).Flat().magnitude < 0.2f * speed * deltaTime) ? (stuckT + deltaTime) : 0f);
		lastP = base.transform.position;
		if (stuckT > 2.5f)
		{
			stuckT = 0f;
			dir = -dir;
			i = Mathf.Clamp(i + dir, 0, route.Length - 1);
		}
		hv = Vector3.Lerp(hv, b, 1f - Mathf.Exp((0f - deltaTime) * (onGround ? 9f : 1.7f)));
		vy = ((onGround && vy < 0f) ? (-2f) : (vy - 35f * Mathf.Sqrt(scaleK) * deltaTime));
		if ((FBMotor.Move(cc, (hv + Vector3.up * vy) * deltaTime, onGround, vy > 0f, stepLocal, 0.45f * scaleK, ref slide, out onGround, out ground) & CollisionFlags.Above) != 0 && vy > 0f)
		{
			vy = 0f;
		}
		ch.velocity = new Vector3(hv.x, vy, hv.z);
		ch.grounded = onGround;
		if (hv.Flat().magnitude > 0.3f)
		{
			ch.Yaw = Mathf.LerpAngle(ch.Yaw, Mathf.Atan2(hv.x, hv.z) * 57.29578f, 1f - Mathf.Exp((0f - deltaTime) * 8f));
		}
		Dictionary<string, object> o = GameData.Section("puppet");
		float num = Json.F(o, "playerWalkSpeed", 4f);
		float num2 = Json.F(o, "playerRunSpeed", 6.8f);
		float num3 = Json.F(o, "aiWalkAnimSpeed", 2f);
		float num4 = Json.F(o, "aiRunAnimSpeed", 5f);
		float num5 = hv.Flat().magnitude / Mathf.Max(0.01f, scaleK);
		float num6 = ((num5 <= num) ? (num5 / num * num3) : (num3 + (num5 - num) / (num2 - num) * (num4 - num3)));
		Vector3 vector2 = Quaternion.Inverse(Quaternion.Euler(0f, ch.Yaw, 0f)) * hv.Flat();
		float magnitude = vector2.magnitude;
		ch.animVel = ((magnitude > 0.001f) ? new Vector2(vector2.x / magnitude * num6, vector2.z / magnitude * num6) : Vector2.zero);
		ch.sprint = num5 > 5.5f;
	}
}
}
