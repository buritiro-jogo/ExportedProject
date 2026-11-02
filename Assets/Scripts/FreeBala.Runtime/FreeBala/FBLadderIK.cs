using System;
using UnityEngine;

namespace FreeBala
{
[DefaultExecutionOrder(1001)]
public class FBLadderIK : MonoBehaviour
{
	private FBCharacter c;

	private Transform piv;

	private float len;

	private float top;

	private float s;

	private float dir;

	private float rung = 0.36f;

	private Vector3 toPole;

	private bool climbing;

	private bool onTop;

	private bool holdRails;

	private Vector3? gripP;

	private float holdH;

	private float w;

	public bool ClimbDone { get; private set; }

	public void Clear()
	{
		climbing = (onTop = (holdRails = false));
		gripP = null;
		piv = null;
		ClimbDone = false;
		w = 0f;
	}

	public void Climb(Transform p, float length, float workH, Vector3 pole, bool atTop, int direction = 1)
	{
		c = GetComponent<FBCharacter>();
		piv = p;
		len = length;
		top = workH;
		toPole = pole.Flat().normalized;
		climbing = !atTop;
		onTop = atTop;
		dir = direction;
		holdRails = false;
		gripP = null;
		ClimbDone = false;
		s = (atTop ? top : ((direction > 0) ? 0f : top));
		rung = 0.3f * (c ? c.ScaleK : 1.2f) * 1f;
	}

	public void HoldRails(Transform p, float h, Vector3 pole)
	{
		c = GetComponent<FBCharacter>();
		piv = p;
		holdH = h;
		toPole = pole.Flat().normalized;
		holdRails = true;
		climbing = (onTop = false);
		gripP = null;
	}

	public void Grip(Vector3 at, Transform p)
	{
		c = GetComponent<FBCharacter>();
		piv = p;
		gripP = at;
		climbing = (onTop = (holdRails = false));
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBLadderIK.LateUpdate");

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
		if (!c)
		{
			c = GetComponent<FBCharacter>();
		}
		if (!c || !c.an || !c.an.isHuman || !piv)
		{
			return;
		}
		Animator an = c.an;
		w = Mathf.MoveTowards(w, 1f, Time.deltaTime / 0.25f);
		Vector3 up = piv.up;
		Vector3 face = piv.forward;
		Vector3 side = Vector3.Cross(up, face).normalized;
		float i = c.ScaleK;
		if (climbing || onTop)
		{
			if (climbing)
			{
				s += dir * 0.55f * i * Time.deltaTime;
				if (dir > 0f && s >= top)
				{
					s = top;
					climbing = false;
					ClimbDone = true;
				}
				else if (dir < 0f && s <= 0f)
				{
					s = 0f;
					climbing = false;
					ClimbDone = true;
				}
			}
			float num = s / rung;
			Transform transform = B(HumanBodyBones.Hips);
			if (!transform)
			{
				return;
			}
			c.transform.rotation = Quaternion.LookRotation((-face).Flat().normalized, Vector3.up);
			Vector3 vector = piv.position + up * (s + 0.95f * i) + face * (0.3f * i);
			c.transform.position += vector - transform.position;
			(Transform, Transform, Transform, float, int)[] array = new(Transform, Transform, Transform, float, int)[2]
			{
				(B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot), -1f, 0),
				(B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot), 1f, 1)
			};
			for (int j = 0; j < array.Length; j++)
			{
				var (upper, transform2, transform3, sd2, num2) = array[j];
				if ((bool)transform3)
				{
					float n2;
					float num3;
					if (!onTop)
					{
						(n2, num3) = Step(num, num2);
					}
					else
					{
						float num4 = Mathf.Round(num) - (float)num2;
						num3 = 0f;
						n2 = num4;
					}
					Vector3 target = Rung(n2, sd2, 0.05f + num3 * 0.12f);
					Quaternion rotation = transform3.rotation;
					FBIK.TwoBonePole(upper, transform2, transform3, target, transform2.position + face * 0.5f * i + up * 0.3f, w);
					transform3.rotation = rotation;
				}
			}
			if (onTop)
			{
				return;
			}
			array = new(Transform, Transform, Transform, float, int)[2]
			{
				(B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand), -1.25f, 1),
				(B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand), 1.25f, 0)
			};
			for (int j = 0; j < array.Length; j++)
			{
				var (upper2, transform4, transform5, num5, off2) = array[j];
				if ((bool)transform5)
				{
					(float n, float lift) tuple4 = Step(num, off2);
					float item = tuple4.n;
					float item2 = tuple4.lift;
					Vector3 target2 = Rung(item + 5.2f, num5, 0.02f + item2 * 0.1f);
					FBIK.TwoBonePole(upper2, transform4, transform5, target2, transform4.position - up * 0.4f * i + face * 0.2f * i + side * num5 * 0.3f * i, w);
				}
			}
		}
		else
		{
			if (!holdRails && !gripP.HasValue)
			{
				return;
			}
			Vector3 vector2 = gripP ?? (piv.position + up * holdH * c.ScaleK);
			Vector3 vector3 = (gripP.HasValue ? Vector3.Cross(Vector3.up, up).normalized : side);
			if (vector3.sqrMagnitude < 0.0001f)
			{
				vector3 = side;
			}
			(Transform, Transform, Transform, float)[] obj = new(Transform, Transform, Transform, float)[2]
			{
				(B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand), -1f),
				(B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand), 1f)
			};
			Vector3 right = c.transform.right;
			(Transform, Transform, Transform, float)[] array2 = obj;
			for (int j = 0; j < array2.Length; j++)
			{
				var (transform6, transform7, transform8, num6) = array2[j];
				if ((bool)transform8)
				{
					Vector3 vector4 = vector2 + (gripP.HasValue ? (right * num6 * 0.2f * i) : (vector3 * num6 * 0.2f * i));
					if (!((vector4 - transform6.position).magnitude > 0.75f * i))
					{
						FBIK.TwoBonePole(transform6, transform7, transform8, vector4, transform7.position - Vector3.up * 0.3f * i + right * num6 * 0.25f * i, w);
					}
				}
			}
		}
		Transform B(HumanBodyBones b)
		{
			return an.GetBoneTransform(b);
		}
		Vector3 Rung(float n, float sd, float out_)
		{
			return piv.position + up * (n * rung) + side * sd * 0.19f * i + face * out_ * i;
		}
		static (float n, float lift) Step(float uu, int off)
		{
			float num7 = (uu + (float)off) / 2f;
			float num8 = Mathf.Floor(num7);
			float num9 = num7 - num8;
			float num10 = ((num9 < 0.5f) ? 0f : Mathf.SmoothStep(0f, 1f, (num9 - 0.5f) * 2f));
			return (n: 2f * num8 - (float)off + 2f * num10, lift: Mathf.Sin(num10 * (float)Math.PI));
		}
	}
}
}
