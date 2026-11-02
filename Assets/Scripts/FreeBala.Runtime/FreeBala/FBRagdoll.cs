using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBRagdoll : MonoBehaviour
{
	private class Part
	{
		public Transform t;

		public Rigidbody rb;

		public Collider col;

		public Joint j;

		public int layer;

		public Vector3 prev;
	}

	private class Hinge
	{
		public string name;

		public Transform up;

		public Transform low;

		public Transform end;

		public Vector3 nLocal;

		public float max;

		public float a0;

		public Rigidbody rbU;

		public Rigidbody rbL;
	}

	public static int FlexSign = 1;

	public static int FlexSignArm = -1;

	public static int FlexSignLeg = 1;

	public static int FlexForce;

	public const int Layer = 15;

	private static bool layerSet;

	public static readonly List<FBRagdoll> Active = new List<FBRagdoll>();

	private const int MAX_SIM = 8;

	public static int Built;

	public static int Settled;

	private FBCharacter ch;

	private Animator an;

	private readonly List<Part> parts = new List<Part>();

	private readonly List<Behaviour> paused = new List<Behaviour>();

	private float waitT = -1f;

	private float landT;

	private float simT;

	private float restT;

	private Vector3 waitImp;

	private bool sim;

	private bool done;

	private Vector3 lastImpPoint;

	// fisica 31/10: ragdoll de personagem VIVO (atropelo, eletricista caindo): o LateUpdate nao desfaz no mesmo quadro
	[System.NonSerialized]
	public bool aliveHold;

	private static readonly HumanBodyBones[] TRACK = new HumanBodyBones[11]
	{
		HumanBodyBones.Hips,
		HumanBodyBones.Spine,
		HumanBodyBones.Head,
		HumanBodyBones.LeftUpperArm,
		HumanBodyBones.LeftLowerArm,
		HumanBodyBones.RightUpperArm,
		HumanBodyBones.RightLowerArm,
		HumanBodyBones.LeftUpperLeg,
		HumanBodyBones.LeftLowerLeg,
		HumanBodyBones.RightUpperLeg,
		HumanBodyBones.RightLowerLeg
	};

	public static int AnatomyFixes;

	private readonly Dictionary<HumanBodyBones, Vector3> prevPos = new Dictionary<HumanBodyBones, Vector3>();

	private readonly List<Hinge> hinges = new List<Hinge>();

	public bool Simulating => sim;

	public bool Done => done;

	public float SimTime => simT;

	public static FBRagdoll Of(FBCharacter ch)
	{
		if (!ch)
		{
			return null;
		}
		Animator component = ch.GetComponent<Animator>();
		if (!component || !component.isHuman)
		{
			return null;
		}
		if (!layerSet)
		{
			layerSet = true;
			for (int i = 0; i < 32; i++)
			{
				Physics.IgnoreLayerCollision(15, i, i != 0 && i != 8 && i != 11 && i != 15);
			}
		}
		FBRagdoll fBRagdoll = ch.GetComponent<FBRagdoll>();
		if (!fBRagdoll)
		{
			fBRagdoll = ch.gameObject.AddComponent<FBRagdoll>();
			fBRagdoll.enabled = false;
		}
		fBRagdoll.ch = ch;
		fBRagdoll.an = component;
		return fBRagdoll;
	}

	private Transform B(HumanBodyBones b)
	{
		return an.GetBoneTransform(b);
	}

	public void After(float secs, float land, Vector3 imp)
	{
		waitT = secs;
		landT = land;
		waitImp = imp;
		base.enabled = true;
	}

	public void Go(Vector3 imp, Vector3 at, float delay = 0f)
	{
		aliveHold = (bool)ch && ch.alive; // fisica 31/10: Go em vivo e intencional (FBAtropelo/FBEletricista); FBDeath so chama depois do Die
		waitT = delay;
		landT = 0f;
		waitImp = imp;
		lastImpPoint = at;
		base.enabled = true;
		if (delay <= 0f)
		{
			Start2();
		}
	}

	private static readonly int __fbt_FixedUpdate = FBTempo.Id("FBRagdoll.FixedUpdate");

	private void FixedUpdate()
	{
		if (!FBTempo.On)
		{
			FixedUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		FixedUpdate__fbt();
		FBTempo.Soma(__fbt_FixedUpdate, t0);
	}

	private void FixedUpdate__fbt()
	{
		if (sim)
		{
			Anatomy(physics: true);
		}
	}

	private void Anatomy(bool physics)
	{
		foreach (Hinge hinge in hinges)
		{
			if (!hinge.up || !hinge.low || !hinge.end || !hinge.rbL || hinge.rbL.isKinematic)
			{
				continue;
			}
			Vector3 vector = hinge.up.TransformDirection(hinge.nLocal);
			Vector3 normalized = (hinge.low.position - hinge.up.position).normalized;
			Vector3 normalized2 = (hinge.end.position - hinge.low.position).normalized;
			float num = Vector3.SignedAngle(normalized, normalized2, vector);
			float num2 = ((num < -2f) ? (-2f - num) : ((num > hinge.max) ? (hinge.max - num) : 0f));
			if (num2 != 0f)
			{
				Quaternion quaternion = Quaternion.AngleAxis(num2, vector);
				Vector3 position = hinge.low.position;
				if (physics)
				{
					hinge.rbL.position = position + quaternion * (hinge.rbL.position - position);
					hinge.rbL.rotation = quaternion * hinge.rbL.rotation;
				}
				else
				{
					hinge.low.SetPositionAndRotation(position + quaternion * (hinge.low.position - position), quaternion * hinge.low.rotation);
				}
				float num3 = Vector3.Dot(hinge.rbL.angularVelocity - (hinge.rbU ? hinge.rbU.angularVelocity : Vector3.zero), vector);
				if ((num2 > 0f && num3 < 0f) || (num2 < 0f && num3 > 0f))
				{
					hinge.rbL.angularVelocity -= vector * num3;
				}
				AnatomyFixes++;
			}
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBRagdoll.LateUpdate");

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
		if (sim)
		{
			Anatomy(physics: false);
		}
		if (!ch)
		{
			return;
		}
		// fisica 31/10: so limpa no Revive; ragdoll vivo intencional (aliveHold) segue simulando ate o FBKnockdown levantar
		if (ch.alive && !aliveHold)
		{
			if (parts.Count > 0 || paused.Count > 0)
			{
				Clear();
			}
			base.enabled = false;
			return;
		}
		float num = Mathf.Max(Time.deltaTime, 0.001f);
		if (!sim && !done && waitT >= 0f)
		{
			waitT -= num;
			landT -= num;
			bool flag = false;
			Transform transform = B(HumanBodyBones.Head);
			if (landT > 0f && (bool)transform && Physics.CheckSphere(transform.position, 0.1f * ch.ScaleK, 2049, QueryTriggerInteraction.Ignore))
			{
				flag = true;
			}
			if (waitT <= 0f || flag)
			{
				Start2();
			}
			else
			{
				TrackPrev();
			}
		}
		else
		{
			if (!sim)
			{
				return;
			}
			simT += num;
			float num2 = 0f;
			foreach (Part part in parts)
			{
				if ((bool)part.rb)
				{
					num2 = Mathf.Max(num2, part.rb.linearVelocity.magnitude);
				}
			}
			restT = ((num2 < 0.18f) ? (restT + num) : 0f);
			if (restT > 0.7f || simT > 6f || (Active.Count > 8 && Active[0] == this && simT > 1.5f))
			{
				Freeze();
			}
		}
	}

	private void TrackPrev()
	{
		HumanBodyBones[] tRACK = TRACK;
		foreach (HumanBodyBones humanBodyBones in tRACK)
		{
			Transform transform = B(humanBodyBones);
			if ((bool)transform)
			{
				prevPos[humanBodyBones] = transform.position;
			}
		}
	}

	private void Start2()
	{
		if (sim || done || !an)
		{
			return;
		}
		float num = Mathf.Max(Time.deltaTime, 0.001f);
		Build();
		an.enabled = false;
		Behaviour[] components = GetComponents<Behaviour>();
		foreach (Behaviour behaviour in components)
		{
			// fisica 31/10: em ragdoll VIVO tambem pausa os controladores de NPC/bot (Clear() religa)
			if (behaviour != this && behaviour.enabled && (behaviour is FBFootIK || behaviour is FBStreetActor || behaviour is FBLobbyDirector || (ch.alive && (behaviour is FBCivilian || behaviour is FBRoleNpc || behaviour is FBBot))))
			{
				behaviour.enabled = false;
				paused.Add(behaviour);
			}
		}
		foreach (Part part2 in parts)
		{
			part2.rb.isKinematic = false;
			Vector3 value;
			Vector3 vector = (prevPos.TryGetValue(BoneOf(part2.t), out value) ? ((part2.t.position - value) / num) : Vector3.zero);
			if (vector.magnitude > 9f)
			{
				vector = vector.normalized * 9f;
			}
			part2.rb.linearVelocity = vector + ch.velocity * 0.6f;
		}
		if (waitImp.sqrMagnitude > 0.0001f)
		{
			Part part = parts[0];
			float num2 = 1E+09f;
			Vector3 vector2 = ((lastImpPoint != Vector3.zero) ? lastImpPoint : (ch.lastHit.valid ? ch.lastHit.point : parts[0].t.position));
			foreach (Part part3 in parts)
			{
				float sqrMagnitude = (part3.t.position - vector2).sqrMagnitude;
				if (sqrMagnitude < num2)
				{
					num2 = sqrMagnitude;
					part = part3;
				}
			}
			// fisica 31/10: limita o braco de alavanca: o ponto guardado pode ser um lastHit.point velho (pose em pe antes do clipe de morte,
			// ou tiro de antes nesta vida em morte por carro/queda/zona/sangramento), a metros ou dezenas de metros do corpo
			Vector3 position = part.t.position;
			Vector3 position2 = position + Vector3.ClampMagnitude(vector2 - position, 0.25f * Mathf.Max(0.5f, ch.ScaleK));
			part.rb.AddForceAtPosition(waitImp * part.rb.mass * 0.8f, position2, ForceMode.Impulse);
			parts[0].rb.AddForce(waitImp * parts[0].rb.mass * 0.35f, ForceMode.Impulse);
		}
		sim = true;
		simT = 0f;
		restT = 0f;
		Active.Add(this);
		Built++;
	}

	private HumanBodyBones BoneOf(Transform t)
	{
		HumanBodyBones[] tRACK = TRACK;
		foreach (HumanBodyBones humanBodyBones in tRACK)
		{
			if (B(humanBodyBones) == t)
			{
				return humanBodyBones;
			}
		}
		return HumanBodyBones.LastBone;
	}

	private void Freeze()
	{
		if (!sim)
		{
			return;
		}
		sim = false;
		done = true;
		Active.Remove(this);
		Settled++;
		foreach (Part part in parts)
		{
			if ((bool)part.j)
			{
				Object.Destroy(part.j);
			}
		}
		foreach (Part part2 in parts)
		{
			if ((bool)part2.rb)
			{
				part2.rb.isKinematic = true;
			}
			if ((bool)part2.col)
			{
				part2.col.enabled = false;
			}
		}
	}

	public void Clear()
	{
		Active.Remove(this);
		foreach (Part part in parts)
		{
			if ((bool)part.j)
			{
				Object.Destroy(part.j);
			}
		}
		foreach (Part part2 in parts)
		{
			if ((bool)part2.col)
			{
				Object.Destroy(part2.col);
			}
			if ((bool)part2.rb)
			{
				Object.Destroy(part2.rb);
			}
			if ((bool)part2.t)
			{
				part2.t.gameObject.layer = part2.layer;
			}
		}
		parts.Clear();
		prevPos.Clear();
		hinges.Clear();
		foreach (Behaviour item in paused)
		{
			if ((bool)item)
			{
				item.enabled = true;
			}
		}
		paused.Clear();
		if ((bool)an)
		{
			an.enabled = true;
			an.Rebind();
		}
		sim = (done = false);
		aliveHold = false; // fisica 31/10
		waitT = -1f;
		restT = (simT = 0f);
		lastImpPoint = Vector3.zero;
	}

	private void Build()
	{
		if (parts.Count > 0)
		{
			return;
		}
		float num = Mathf.Max(0.5f, ch.ScaleK);
		Transform transform = B(HumanBodyBones.Hips);
		Transform transform2 = B(HumanBodyBones.Spine) ?? B(HumanBodyBones.Chest);
		if ((object)B(HumanBodyBones.UpperChest) == null)
		{
			B(HumanBodyBones.Chest);
		}
		Transform transform3 = B(HumanBodyBones.Neck);
		Transform transform4 = B(HumanBodyBones.Head);
		if (!transform || !transform2 || !transform4)
		{
			return;
		}
		Transform transform5 = B(HumanBodyBones.LeftUpperArm);
		Transform transform6 = B(HumanBodyBones.LeftLowerArm);
		Transform transform7 = B(HumanBodyBones.LeftHand);
		Transform transform8 = B(HumanBodyBones.RightUpperArm);
		Transform transform9 = B(HumanBodyBones.RightLowerArm);
		Transform transform10 = B(HumanBodyBones.RightHand);
		Transform transform11 = B(HumanBodyBones.LeftUpperLeg);
		Transform transform12 = B(HumanBodyBones.LeftLowerLeg);
		Transform transform13 = B(HumanBodyBones.LeftFoot);
		Transform transform14 = B(HumanBodyBones.RightUpperLeg);
		Transform transform15 = B(HumanBodyBones.RightLowerLeg);
		Transform transform16 = B(HumanBodyBones.RightFoot);
		Vector3 normalized = (((bool)transform14 && (bool)transform11) ? (transform14.position - transform11.position) : transform.right).normalized;
		Vector3 normalized2 = ((transform3 ? transform3.position : transform4.position) - transform.position).normalized;
		Vector3 normalized3 = Vector3.Cross(normalized, normalized2).normalized;
		Part part = Add(transform, null, 12f * num);
		CapsuleBetween(part, transform11 ? transform11.position : (transform.position - normalized * 0.1f * num), transform14 ? transform14.position : (transform.position + normalized * 0.1f * num), 0.11f * num, 0.06f * num);
		Part part2 = Add(transform2, part, 12f * num);
		CapsuleBetween(part2, transform2.position, transform3 ? transform3.position : transform4.position, 0.13f * num, 0f);
		Ball(part2, part, normalized2, normalized, 22f, 22f, 18f);
		Part part3 = Add(transform4, part2, 5f * num);
		SphereCollider sphereCollider = transform4.gameObject.AddComponent<SphereCollider>();
		sphereCollider.radius = 0.1f * num / Sc(transform4);
		sphereCollider.center = transform4.InverseTransformPoint(transform4.position + normalized2 * 0.08f * num);
		part3.col = sphereCollider;
		Ball(part3, part2, normalized2, normalized, 35f, 30f, 25f);
		if ((bool)transform5 && (bool)transform6 && (bool)transform7)
		{
			Limb(part2, transform5, transform6, transform7, 2.5f * num, 1.5f * num, 0.055f * num, 0.045f * num, normalized3, 145f, 60f, 40f);
		}
		if ((bool)transform8 && (bool)transform9 && (bool)transform10)
		{
			Limb(part2, transform8, transform9, transform10, 2.5f * num, 1.5f * num, 0.055f * num, 0.045f * num, normalized3, 145f, 60f, 40f);
		}
		if ((bool)transform11 && (bool)transform12 && (bool)transform13)
		{
			Limb(part, transform11, transform12, transform13, 7f * num, 4f * num, 0.085f * num, 0.06f * num, -normalized3, 150f, 45f, 30f);
		}
		if ((bool)transform14 && (bool)transform15 && (bool)transform16)
		{
			Limb(part, transform14, transform15, transform16, 7f * num, 4f * num, 0.085f * num, 0.06f * num, -normalized3, 150f, 45f, 30f);
		}
		foreach (Part part4 in parts)
		{
			part4.rb.isKinematic = true;
			part4.rb.solverIterations = 14;
			part4.rb.solverVelocityIterations = 6;
			part4.rb.interpolation = RigidbodyInterpolation.Interpolate;
			part4.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
			part4.rb.maxDepenetrationVelocity = 2.5f;
			part4.rb.angularDamping = 0.6f;
			part4.rb.linearDamping = 0.05f;
		}
		foreach (Part part5 in parts)
		{
			foreach (Part part6 in parts)
			{
				if (part5 != part6 && (bool)part5.col && (bool)part6.col)
				{
					Physics.IgnoreCollision(part5.col, part6.col, Linked(part5, part6));
				}
			}
		}
	}

	private static bool Linked(Part a, Part b)
	{
		if (!a.j || !(a.j.connectedBody == b.rb))
		{
			if ((bool)b.j)
			{
				return b.j.connectedBody == a.rb;
			}
			return false;
		}
		return true;
	}

	private static float Sc(Transform t)
	{
		return Mathf.Max(0.0001f, t.lossyScale.x);
	}

	private Part Add(Transform t, Part parent, float mass)
	{
		Part part = new Part
		{
			t = t,
			layer = t.gameObject.layer
		};
		t.gameObject.layer = 15;
		part.rb = t.gameObject.GetComponent<Rigidbody>();
		if (!part.rb)
		{
			part.rb = t.gameObject.AddComponent<Rigidbody>();
		}
		part.rb.mass = mass;
		parts.Add(part);
		return part;
	}

	private static void CapsuleBetween(Part p, Vector3 a, Vector3 b, float r, float extra)
	{
		Transform t = p.t;
		Vector3 vector = t.InverseTransformPoint(a);
		Vector3 vector2 = t.InverseTransformPoint(b);
		Vector3 vector3 = vector2 - vector;
		float num = Sc(t);
		CapsuleCollider capsuleCollider = t.gameObject.AddComponent<CapsuleCollider>();
		int index = (capsuleCollider.direction = ((!(Mathf.Abs(vector3.x) > Mathf.Abs(vector3.y))) ? ((Mathf.Abs(vector3.y) > Mathf.Abs(vector3.z)) ? 1 : 2) : ((!(Mathf.Abs(vector3.x) > Mathf.Abs(vector3.z))) ? 2 : 0)));
		capsuleCollider.center = (vector + vector2) * 0.5f;
		capsuleCollider.radius = r / num;
		capsuleCollider.height = Mathf.Abs(vector3[index]) + 2f * capsuleCollider.radius + extra / num;
		p.col = capsuleCollider;
	}

	private void Limb(Part root, Transform up, Transform low, Transform end, float m1, float m2, float r1, float r2, Vector3 flexTo, float flexMax, float sw1, float sw2)
	{
		Part part = Add(up, root, m1);
		CapsuleBetween(part, up.position, low.position, r1, 0f);
		Vector3 normalized = (low.position - up.position).normalized;
		Vector3 rhs = ((Vector3.Cross(normalized, flexTo).sqrMagnitude > 0.01f) ? flexTo : Vector3.Cross(normalized, Vector3.up));
		Ball(part, root, normalized, Vector3.Cross(normalized, rhs).normalized, 30f, sw1, sw2);
		Part part2 = Add(low, part, m2);
		CapsuleBetween(part2, low.position, end.position, r2, r2 * 0.5f);
		Vector3 normalized2 = (end.position - low.position).normalized;
		Vector3 vector = Vector3.Cross(normalized, normalized2);
		if (vector.sqrMagnitude < 0.02f)
		{
			vector = Vector3.Cross(normalized, flexTo);
		}
		if (vector.sqrMagnitude < 0.0001f)
		{
			vector = Vector3.Cross(normalized, Vector3.forward);
		}
		vector.Normalize();
		float num = Vector3.Angle(normalized, normalized2);
		Vector3 vector2 = Quaternion.AngleAxis(20f, vector) * normalized2;
		bool flag = Vector3.Angle(normalized, vector2) > num + 1f;
		if (num < 8f)
		{
			flag = Vector3.Dot(vector2 - normalized2, flexTo) > 0f;
		}
		if (!flag)
		{
			vector = -vector;
		}
		CharacterJoint characterJoint = low.gameObject.AddComponent<CharacterJoint>();
		characterJoint.connectedBody = part.rb;
		characterJoint.enableProjection = true;
		characterJoint.enablePreprocessing = false;
		characterJoint.axis = low.InverseTransformDirection(vector);
		characterJoint.swingAxis = low.InverseTransformDirection(Vector3.Cross(vector, normalized2).normalized);
		float num2 = Mathf.Clamp(flexMax - num, 0f, 177f);
		float num3 = Mathf.Clamp(num, 0f, 177f);
		int num4 = ((FlexForce != 0) ? FlexForce : ((flexTo == Vector3.zero) ? FlexSign : ((Vector3.Dot(flexTo, base.transform.forward) < 0f) ? FlexSignLeg : FlexSignArm)));
		float limit = ((num4 > 0) ? (0f - num3) : (0f - num2));
		float limit2 = ((num4 > 0) ? num2 : num3);
		characterJoint.lowTwistLimit = new SoftJointLimit
		{
			limit = limit
		};
		characterJoint.highTwistLimit = new SoftJointLimit
		{
			limit = limit2
		};
		characterJoint.swing1Limit = new SoftJointLimit
		{
			limit = 3f
		};
		characterJoint.swing2Limit = new SoftJointLimit
		{
			limit = 3f
		};
		part2.j = characterJoint;
		hinges.Add(new Hinge
		{
			name = low.name,
			up = up,
			low = low,
			end = end,
			nLocal = up.InverseTransformDirection(vector),
			max = flexMax,
			a0 = Vector3.SignedAngle(normalized, normalized2, vector),
			rbU = part.rb,
			rbL = part2.rb
		});
	}

	public IEnumerable<(string name, float flex, float max)> Hinges()
	{
		foreach (Hinge hinge in hinges)
		{
			if ((bool)hinge.up && (bool)hinge.low && (bool)hinge.end)
			{
				yield return (name: hinge.name, flex: Vector3.SignedAngle((hinge.low.position - hinge.up.position).normalized, (hinge.end.position - hinge.low.position).normalized, hinge.up.TransformDirection(hinge.nLocal)), max: hinge.max);
			}
		}
	}

	public IEnumerable<(string name, float a0)> HingesAtStart()
	{
		foreach (Hinge hinge in hinges)
		{
			yield return (name: hinge.name, a0: hinge.a0);
		}
	}

	private static void Ball(Part p, Part parent, Vector3 twistAxisW, Vector3 swingAxisW, float twist, float sw1, float sw2)
	{
		CharacterJoint characterJoint = p.t.gameObject.AddComponent<CharacterJoint>();
		characterJoint.connectedBody = parent.rb;
		characterJoint.enableProjection = true;
		characterJoint.enablePreprocessing = false;
		characterJoint.axis = p.t.InverseTransformDirection(twistAxisW.normalized);
		characterJoint.swingAxis = p.t.InverseTransformDirection(swingAxisW.normalized);
		characterJoint.lowTwistLimit = new SoftJointLimit
		{
			limit = 0f - twist
		};
		characterJoint.highTwistLimit = new SoftJointLimit
		{
			limit = twist
		};
		characterJoint.swing1Limit = new SoftJointLimit
		{
			limit = sw1
		};
		characterJoint.swing2Limit = new SoftJointLimit
		{
			limit = sw2
		};
		SoftJointLimitSpring softJointLimitSpring = default(SoftJointLimitSpring);
		softJointLimitSpring.spring = 0f;
		softJointLimitSpring.damper = 0f;
		SoftJointLimitSpring swingLimitSpring = (characterJoint.twistLimitSpring = softJointLimitSpring);
		characterJoint.swingLimitSpring = swingLimitSpring;
		p.j = characterJoint;
	}
}
}
