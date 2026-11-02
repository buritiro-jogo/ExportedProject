using System.Runtime.CompilerServices;
using UnityEngine;

namespace FreeBala
{
[DisallowMultipleComponent]
public class FBFootIK : MonoBehaviour
{
	public static class Q
	{
		public static float NearDist = -1f;

		public static float FarDist = -1f;

		public static int MidRayEvery = 3;

		public static bool Lock = true;

		public static bool Toe = true;

		public static bool Tilt = true;

		public static bool Clear = true;

		public static bool IgnoreVisibility;

		public static float Far
		{
			get
			{
				if (!(FarDist > 0f))
				{
					return Mathf.Max(8f, FBQuality.FootIKDistance);
				}
				return FarDist;
			}
		}

		public static float Near
		{
			get
			{
				if (!(NearDist > 0f))
				{
					return Mathf.Min(Far * 0.5f, 18f);
				}
				return NearDist;
			}
		}
	}

	public class Foot
	{
		public Transform th;

		public Transform kn;

		public Transform ft;

		public Transform toe;

		public bool Hit;

		public float GroundY;

		public float GroundS;

		public float Plant;

		public float LockW;

		public float RestH = -1f;

		public float ToeRestH = -1f;

		public float HitW;

		public Vector3 Normal = Vector3.up;

		public Vector3 NormalS = Vector3.up;

		public Vector3 Target;

		public Vector3 Ankle;

		public Vector3 LockPos;

		public Vector3 prevAnim;

		public bool Locked;

		public bool hasPrev;

		public bool relockBlock;

		public bool moving;

		public float aheadY;

		public float ClearW;

		public bool aheadHit;

		public Vector3 rayXZ;

		public int rayFrame = -99;

		public float rayY;

		public float toeY;

		public bool rayHit;

		public bool toeHit;

		public Vector3 rayN = Vector3.up;

		public Rigidbody rayBody;
	}

	private const float CastUp = 0.62f;

	private const float CastDown = 0.6f;

	private const float CastR = 0.045f;

	private const float MaxDown = 0.45f;

	private const float MaxUp = 0.06f;

	private const float MaxAlign = 35f;

	private const float MaxLock = 0.2f;

	private const float WallAngle = 60f;

	private const float AheadDist = 0.24f;

	private const float ClearMin = 0.03f;

	private const float ClearMax = 0.5f;

	private const float ClearMargin = 0.035f;

	public readonly Foot Left = new Foot();

	public readonly Foot Right = new Foot();

	private FBCharacter ch;

	private Animator an;

	private Transform spine;

	private Renderer vis;

	private float footW;

	private float hipsOff;

	private float hipsV;

	private float appliedOff;

	private float tiltS;

	private float slopeS;

	private float airT;

	private float ikBaseY;

	private bool ikHasBase;

	private Vector3 lastPos;

	private bool hasLast;

	private Foot[] pair;

	private static readonly RaycastHit[] hits = new RaycastHit[10];

	public float HipsOffset => hipsOff;

	public float Weight => footW;

	public int Lod { get; private set; }

	public float Tilt => tiltS;

	private float S
	{
		get
		{
			if (!ch)
			{
				return 1f;
			}
			return ch.ScaleK;
		}
	}

	private bool HardOff
	{
		get
		{
			if ((bool)ch)
			{
				return ch.vehiclePose;
			}
			return false;
		}
	}

	private static int Mask => 2305;

	public void Init(FBCharacter c)
	{
		ch = c;
		an = c.an;
		if ((bool)an && an.isHuman)
		{
			Leg(Left, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes);
			Leg(Right, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes);
			spine = an.GetBoneTransform(HumanBodyBones.Spine);
			pair = new Foot[2] { Left, Right };
			vis = GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
			if (!vis)
			{
				vis = GetComponentInChildren<Renderer>(includeInactive: true);
			}
		}
		void Leg(Foot f, HumanBodyBones a, HumanBodyBones b, HumanBodyBones cft, HumanBodyBones t)
		{
			f.th = an.GetBoneTransform(a);
			f.kn = an.GetBoneTransform(b);
			f.ft = an.GetBoneTransform(cft);
			f.toe = an.GetBoneTransform(t);
		}
	}

	private void OnAnimatorIK(int layer)
	{
		if (layer != 0 || !an)
		{
			return;
		}
		if (HardOff)
		{
			appliedOff = 0f;
			ikHasBase = false;
			return;
		}
		float y = base.transform.position.y;
		float num = (ikHasBase ? (y - ikBaseY) : 0f);
		ikBaseY = y;
		ikHasBase = true;
		if (footW > 0.02f && airT <= 0f && Mathf.Abs(num) > 0.02f * S && Mathf.Abs(num) < 0.6f * S)
		{
			hipsOff = Mathf.Clamp(hipsOff - num, -0.7f * S, 0.55f * S);
		}
		appliedOff = hipsOff;
		if (hipsOff != 0f)
		{
			an.bodyPosition += Vector3.up * hipsOff;
		}
	}

	private bool Active(out int lod)
	{
		lod = 2;
		if (!FBQuality.FootIK || !ch || !Left.ft || !Right.ft)
		{
			return false;
		}
		if (ch.special || !ch.alive || ch.DanW > 0.05f || ch.vehiclePose || airT > 0.15f)
		{
			return false;
		}
		if ((bool)vis && !vis.isVisible && !Q.IgnoreVisibility)
		{
			return false;
		}
		Camera main = Camera.main;
		float num = (main ? (main.transform.position - base.transform.position).magnitude : 0f);
		if (ch.isPlayer)
		{
			num = 0f;
		}
		lod = ((!(num < Q.Near * S)) ? ((num < Q.Far * S) ? 1 : 2) : 0);
		return lod < 2;
	}

	private void Reset0()
	{
		footW = 0f;
		hipsOff = (hipsV = (appliedOff = (tiltS = 0f)));
		ikHasBase = (hasLast = false);
		Foot[] array = pair ?? new Foot[0];
		foreach (Foot obj in array)
		{
			obj.hasPrev = (obj.Locked = false);
			obj.LockW = (obj.HitW = (obj.ClearW = 0f));
			obj.aheadHit = false;
			obj.rayFrame = -99;
		}
	}

	public void Tick(float dt)
	{
		if (dt <= 0f || !an || !an.isHuman)
		{
			return;
		}
		if (HardOff)
		{
			Reset0();
			return;
		}
		airT = (((bool)ch && ch.grounded) ? 0f : (airT + dt));
		int lod;
		bool flag = Active(out lod);
		Lod = lod;
		footW = Mathf.Lerp(footW, flag ? 1 : 0, 1f - Mathf.Exp((0f - dt) * 10f));
		TrackSlope(dt);
		if (footW < 0.02f)
		{
			hipsOff = Mathf.SmoothDamp(hipsOff, 0f, ref hipsV, 0.1f, 1f / 0f, dt);
			tiltS = Mathf.Lerp(tiltS, 0f, 1f - Mathf.Exp((0f - dt) * 8f));
			Left.hasPrev = (Right.hasPrev = false);
			Left.Locked = (Right.Locked = false);
			Left.LockW = (Right.LockW = 0f);
			return;
		}
		float y = base.transform.position.y;
		float num = 1E+09f;
		bool flag2 = false;
		Foot[] array = pair;
		foreach (Foot f in array)
		{
			if (Solve(f, y, lod, dt, out var need))
			{
				num = Mathf.Min(num, need);
				flag2 = true;
			}
		}
		float num2 = (flag2 ? (Mathf.Clamp(num, -0.45f * S, 0.06f * S) * footW) : 0f);
		hipsOff = Mathf.SmoothDamp(hipsOff, num2, ref hipsV, (hipsOff > num2) ? 0.05f : 0.085f, 1f / 0f, dt);
		TorsoTilt(dt, lod);
	}

	private void TrackSlope(float dt)
	{
		Vector3 position = base.transform.position;
		if (hasLast && ch.grounded)
		{
			float magnitude = new Vector2(position.x - lastPos.x, position.z - lastPos.z).magnitude;
			if (magnitude > 0.001f)
			{
				slopeS = Mathf.Lerp(slopeS, Mathf.Clamp((position.y - lastPos.y) / magnitude, -1.2f, 1.2f), 1f - Mathf.Exp((0f - dt) * 4f));
			}
		}
		else
		{
			slopeS = Mathf.Lerp(slopeS, 0f, 1f - Mathf.Exp((0f - dt) * 4f));
		}
		lastPos = position;
		hasLast = true;
	}

	private void TorsoTilt(float dt, int lod)
	{
		float num = ch.velocity.Flat().magnitude / Mathf.Max(0.01f, S);
		bool flag = ch.aiming || ch.shooting || ch.lastShot < 0.6f || ch.prone;
		float num2 = ((Q.Tilt && lod == 0 && !flag) ? (Mathf.Clamp(Mathf.Atan(slopeS) * 57.29578f * 0.18f, -4f, 6f) * Mathf.Clamp01((num - 0.4f) / 1.5f)) : 0f);
		tiltS = Mathf.Lerp(tiltS, num2 * footW, 1f - Mathf.Exp((0f - dt) * 5f));
		if ((bool)spine && Mathf.Abs(tiltS) > 0.05f)
		{
			spine.Rotate(base.transform.right, tiltS, Space.World);
		}
	}

	private bool Ground(Vector3 at, float baseY, float r, out float y, out Vector3 n, out Rigidbody body)
	{
		y = 0f;
		n = Vector3.up;
		body = null;
		float y2 = baseY + 0.62f * S;
		float maxDistance = 1.22f * S;
		Vector3 origin = new Vector3(at.x, y2, at.z);
		float num = 1E+09f;
		for (int i = 0; i < 2; i++)
		{
			if (!(num >= 1E+09f))
			{
				break;
			}
			int num2 = ((i == 0 && r > 0f) ? Physics.SphereCastNonAlloc(origin, r, Vector3.down, hits, maxDistance, Mask, QueryTriggerInteraction.Ignore) : Physics.RaycastNonAlloc(origin, Vector3.down, hits, maxDistance, Mask, QueryTriggerInteraction.Ignore));
			for (int j = 0; j < num2; j++)
			{
				RaycastHit raycastHit = hits[j];
				if (!(raycastHit.distance <= 0f) && !(raycastHit.distance >= num) && (bool)raycastHit.collider && !raycastHit.collider.transform.IsChildOf(base.transform) && !(Vector3.Angle(raycastHit.normal, Vector3.up) > 60f))
				{
					Rigidbody attachedRigidbody = raycastHit.collider.attachedRigidbody;
					if (!attachedRigidbody || attachedRigidbody.isKinematic || !(attachedRigidbody.linearVelocity.sqrMagnitude > 0.25f))
					{
						num = raycastHit.distance;
						y = raycastHit.point.y;
						n = raycastHit.normal;
						body = attachedRigidbody;
					}
				}
			}
		}
		return num < 1E+09f;
	}

	private bool Solve(Foot f, float baseY, int lod, float dt, out float need)
	{
		need = 0f;
		if (!f.th || !f.kn || !f.ft)
		{
			return false;
		}
		float s = S;
		Vector3 position = f.ft.position;
		Vector3 prevAnim = position - Vector3.up * appliedOff;
		float num = prevAnim.y - baseY;
		if (f.RestH < 0f)
		{
			f.RestH = Mathf.Clamp(num, 0.02f * s, 0.2f * s);
		}
		f.RestH = Mathf.Clamp(Mathf.Min(f.RestH + dt * 0.01f * s, num), 0.02f * s, 0.2f * s);
		float num2 = Mathf.Max(0f, num - f.RestH);
		float value = 0f;
		if (f.hasPrev)
		{
			value = new Vector2(prevAnim.x - f.prevAnim.x, prevAnim.z - f.prevAnim.z).magnitude / dt;
		}
		f.prevAnim = prevAnim;
		f.hasPrev = true;
		float num3;
		Vector3 n;
		if (!ch)
		{
			num3 = 0f;
		}
		else
		{
			n = ch.velocity.Flat();
			num3 = n.magnitude;
		}
		float num4 = num3;
		float a = 0.3f * s + 0.45f * num4;
		float b = 1f * s + 0.8f * num4;
		float b2 = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f * s, 0.08f * s, num2))) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, value)));
		f.Plant = Mathf.Lerp(f.Plant, b2, 1f - Mathf.Exp((0f - dt) * 30f));
		bool flag = Q.Lock && lod == 0 && f.HitW > 0.5f && (!f.rayBody || f.rayBody.isKinematic);
		Vector3 vector = new Vector3(prevAnim.x, 0f, prevAnim.z);
		if (f.relockBlock && f.Plant < 0.35f)
		{
			f.relockBlock = false;
		}
		if (!f.Locked && flag && !f.relockBlock && f.Plant > 0.7f && f.LockW < 0.05f)
		{
			f.Locked = true;
			f.LockPos = vector;
		}
		if (f.Locked)
		{
			n = vector - f.LockPos;
			bool flag2 = n.magnitude > 0.2f * s;
			if (flag2 || f.Plant < 0.35f || !flag)
			{
				f.Locked = false;
				f.relockBlock = flag2;
			}
		}
		f.LockW = Mathf.MoveTowards(f.LockW, f.Locked ? 1 : 0, dt * (float)(f.Locked ? 14 : 8));
		Vector3 vector2 = Vector3.Lerp(vector, f.LockPos, f.LockW);
		int num5 = ((lod == 0) ? Mathf.Max(1, FBQuality.FootIKInterval) : Mathf.Max(Q.MidRayEvery, FBQuality.FootIKInterval));
		bool num6;
		if (lod != 0)
		{
			num6 = Time.frameCount - f.rayFrame >= num5;
		}
		else
		{
			if (FBQuality.FootIKFor(base.transform.position, ch.isPlayer, RuntimeHelpers.GetHashCode(this)))
			{
				goto IL_03b8;
			}
			num6 = Time.frameCount - f.rayFrame >= num5;
		}
		if (!num6)
		{
			n = vector2 - f.rayXZ;
			if (!(n.magnitude > 0.04f * s))
			{
				goto IL_04f7;
			}
		}
		goto IL_03b8;
		IL_04f7:
		f.HitW = Mathf.MoveTowards(f.HitW, f.rayHit ? 1 : 0, dt * 8f);
		f.Hit = f.rayHit;
		if (!f.rayHit)
		{
			f.Ankle = position;
			return false;
		}
		f.GroundY = f.rayY;
		f.Normal = f.rayN;
		f.GroundS = ((Mathf.Abs(f.GroundS - f.GroundY) > 0.8f * s || f.Plant > 0.5f) ? f.GroundY : Mathf.Lerp(f.GroundS, f.GroundY, 1f - Mathf.Exp((0f - dt) * 22f)));
		Vector3 vector3 = f.Normal;
		float num7 = Vector3.Angle(vector3, Vector3.up);
		if (num7 > 35f)
		{
			vector3 = Vector3.Slerp(Vector3.up, vector3, 35f / num7);
		}
		f.NormalS = Vector3.Slerp(f.NormalS, vector3, 1f - Mathf.Exp((0f - dt) * 15f)).normalized;
		float num8 = Mathf.Max(0.8f, f.NormalS.y);
		float num9 = Mathf.Max(f.GroundS + f.RestH / num8 + num2, f.GroundY + f.RestH * 0.9f);
		float num10 = (f.aheadHit ? (f.aheadY - f.GroundY) : 0f);
		float num11 = ((Q.Clear && num10 > 0.03f * s && num10 < 0.5f * s) ? ((1f - f.Plant) * footW) : 0f);
		f.ClearW = Mathf.Lerp(f.ClearW, num11, 1f - Mathf.Exp((0f - dt) * (float)((num11 > f.ClearW) ? 22 : 12)));
		if (f.ClearW > 0.005f)
		{
			num9 = Mathf.Lerp(num9, Mathf.Max(num9, f.aheadY + f.RestH + 0.035f * s), f.ClearW);
		}
		Vector3 vector4 = new Vector3(vector2.x, num9, vector2.z);
		float t = (0.3f + 0.7f * f.Plant) * footW;
		Quaternion quaternion = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, f.NormalS), t);
		if (f.toeHit && (bool)f.toe)
		{
			Vector3 vector5 = vector4 + quaternion * (f.toe.position - position);
			float num12 = f.toe.position.y - appliedOff - baseY;
			if (f.ToeRestH < 0f)
			{
				f.ToeRestH = Mathf.Clamp(num12, 0.005f * s, 0.1f * s);
			}
			f.ToeRestH = Mathf.Clamp(Mathf.Min(f.ToeRestH + dt * 0.01f * s, num12), 0.005f * s, 0.1f * s);
			float num13 = f.toeY + f.ToeRestH * 0.8f - vector5.y;
			if (num13 > 0f && f.toeY - f.GroundY < 0.1f * s)
			{
				vector4.y += Mathf.Min(num13, 0.04f * s);
			}
		}
		f.Target = vector4;
		need = vector4.y - prevAnim.y;
		float t2 = footW * f.HitW;
		Vector3 vector6 = Vector3.Lerp(position, vector4, t2);
		if ((vector6 - position).sqrMagnitude > 1E-06f)
		{
			Quaternion rotation = f.ft.rotation;
			Vector3 vector7 = f.kn.position - (f.th.position + position) * 0.5f;
			float num14 = Mathf.Clamp01(vector7.magnitude / (0.06f * s));
			Vector3 vector8 = ((num14 > 0f) ? (vector7.normalized * num14) : Vector3.zero) + base.transform.forward * (1f - num14);
			FBIK.TwoBonePole(f.th, f.kn, f.ft, vector6, f.kn.position + vector8.normalized * 0.5f * s);
			f.ft.rotation = rotation;
		}
		f.ft.rotation = Quaternion.Slerp(Quaternion.identity, quaternion, t2) * f.ft.rotation;
		f.Ankle = f.ft.position;
		return true;
		IL_03b8:
		f.rayHit = Ground(vector2, baseY, 0.045f * s, out f.rayY, out f.rayN, out f.rayBody);
		f.rayXZ = vector2;
		f.rayFrame = Time.frameCount;
		f.toeHit = false;
		f.aheadHit = false;
		Rigidbody body;
		if (Q.Clear && lod == 0 && (bool)ch)
		{
			Vector3 vector9 = ch.velocity.Flat();
			if (vector9.magnitude > 0.3f * s)
			{
				f.aheadHit = Ground(new Vector3(vector2.x, 0f, vector2.z) + vector9.normalized * 0.24f * s, baseY, 0.045f * s, out f.aheadY, out n, out body);
			}
		}
		if (Q.Toe && lod == 0 && (bool)f.toe)
		{
			Vector3 at = f.toe.position - position + new Vector3(vector2.x, 0f, vector2.z);
			f.toeHit = Ground(at, baseY, 0f, out f.toeY, out n, out body);
		}
		goto IL_04f7;
	}

	public string DebugText()
	{
		return $"w {footW:0.00} lod {Lod} pelve {hipsOff:+0.000;-0.000} tronco {tiltS:0.0}° · E {F(Left)} · D {F(Right)}";
	}

	private static string F(Foot f)
	{
		return $"chão {f.GroundY:0.00} apoio {f.Plant:0.0} trava {f.LockW:0.0} desc {f.RestH:0.00}";
	}
}
}
