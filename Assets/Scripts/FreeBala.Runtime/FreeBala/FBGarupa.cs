using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
[DefaultExecutionOrder(1002)]
public class FBGarupa : MonoBehaviour
{
	public static int Seated;

	public static int Dismounted;

	public static readonly string[] CLIPS = new string[3] { "X171_Anim_MB_Idle_Passenger", "X171_Anim_MB_Idle_Passenger_Base", "X171_bike_ar_garupa" };

	private FBCharacter c;

	public FBCar car;

	private CharacterController cc;

	private Animator an;

	private int LU = -1;

	private int LE = -1;

	private int LEU = -1;

	private readonly List<int> zeroF = new List<int>();

	private readonly List<int> offB = new List<int>();

	public bool owned;

	private float noDrv;

	private static readonly string[] ZERO = new string[5] { "VelX", "VelZ", "Speed", "VerticalVelocity", "CrouchAlpha" };

	private static readonly string[] OFF = new string[7] { "IsRunning", "IsAiming", "IsShooting", "InCombat", "InAir", "IsCrouching", "Tactical Sprint" };

	public static string Clip
	{
		get
		{
			if (!FBRegistry.I)
			{
				return null;
			}
			string[] cLIPS = CLIPS;
			foreach (string text in cLIPS)
			{
				if ((bool)FBRegistry.I.Clip(text))
				{
					return text;
				}
			}
			return null;
		}
	}

	public static FBGarupa Sit(FBCharacter ch, FBCar u, bool owned)
	{
		if (!ch || !u || Clip == null)
		{
			return null;
		}
		FBGarupa orAdd = ch.gameObject.GetOrAdd<FBGarupa>();
		orAdd.c = ch;
		orAdd.car = u;
		orAdd.owned = owned;
		orAdd.cc = ch.GetComponent<CharacterController>();
		if ((bool)orAdd.cc)
		{
			orAdd.cc.enabled = false;
		}
		ch.aiming = (ch.hasAim = false);
		ch.SetWeaponVisible(v: false);
		ch.vehiclePose = true;
		ch.velocity = Vector3.zero;
		ch.animVel = Vector2.zero;
		ch.StopDance(force: true);
		ch.sys = Clip;
		Collider[] componentsInChildren = ch.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			foreach (Collider col in u.cols)
			{
				if ((bool)collider && (bool)col)
				{
					Physics.IgnoreCollision(collider, col);
				}
			}
		}
		orAdd.an = ch.an;
		orAdd.zeroF.Clear();
		orAdd.offB.Clear();
		Seated++;
		if ((bool)orAdd.an)
		{
			orAdd.LU = orAdd.an.GetLayerIndex("Upper Body");
			orAdd.LE = orAdd.an.GetLayerIndex("Emote Full Body");
			orAdd.LEU = orAdd.an.GetLayerIndex("Emote Upper Body");
			AnimatorControllerParameter[] parameters = orAdd.an.parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				string value = animatorControllerParameter.name.Trim();
				if (animatorControllerParameter.type == AnimatorControllerParameterType.Float && Array.IndexOf(ZERO, value) >= 0)
				{
					orAdd.zeroF.Add(animatorControllerParameter.nameHash);
				}
				else if (animatorControllerParameter.type == AnimatorControllerParameterType.Bool && Array.IndexOf(OFF, value) >= 0)
				{
					orAdd.offB.Add(animatorControllerParameter.nameHash);
				}
			}
		}
		return orAdd;
	}

	public void Leave()
	{
		if ((bool)c)
		{
			c.vehiclePose = false;
			if (c.sys == Clip)
			{
				c.sys = null;
			}
			if ((bool)cc)
			{
				cc.enabled = true;
			}
		}
		car = null;
		UnityEngine.Object.Destroy(this);
	}

	public static Vector3 SeatOf(FBCar u, float k)
	{
		Transform transform = (u.mx ? u.mx.transform : u.transform);
		return FBDrive.SeatHips(u) - transform.forward * 0.42f * k + transform.up * 0.06f * k;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBGarupa.Update");

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
		if (!car || !c || !an || !an.runtimeAnimatorController || !c.alive)
		{
			return;
		}
		foreach (int item in zeroF)
		{
			an.SetFloat(item, 0f);
		}
		foreach (int item2 in offB)
		{
			an.SetBool(item2, value: false);
		}
		if (LU >= 0)
		{
			an.SetLayerWeight(LU, 0f);
		}
		if (LEU >= 0)
		{
			an.SetLayerWeight(LEU, 0f);
		}
		if (LE >= 0 && c.sys != null)
		{
			an.SetLayerWeight(LE, 1f);
		}
		an.speed = 1f;
		if (c.sys == null)
		{
			c.sys = Clip;
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBGarupa.LateUpdate");

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
		if (!car || !c)
		{
			if ((bool)c)
			{
				if (owned && !car)
				{
					UnityEngine.Object.Destroy(c.gameObject);
					return;
				}
				c.vehiclePose = false;
				c.sys = null;
				if ((bool)cc)
				{
					cc.enabled = true;
				}
			}
			UnityEngine.Object.Destroy(this);
			return;
		}
		if (!c.alive)
		{
			Leave();
			return;
		}
		FBCharacter driver = car.driver;
		if ((bool)driver && FBVehicles.CarOf(driver) == car && !FBFall.Busy(driver))
		{
			noDrv = 0f;
		}
		else if ((noDrv += Time.deltaTime) > 0.6f)
		{
			Transform transform = (car.mx ? car.mx.transform : car.transform);
			Vector3 p = SeatOf(car, c.ScaleK) + transform.right * 0.75f * c.ScaleK;
			float? num = FBWorld.GroundY(p.x, p.y + 0.5f, p.z, 0.4f, 3f);
			if (num.HasValue)
			{
				float valueOrDefault = num.GetValueOrDefault();
				p.y = valueOrDefault;
			}
			FBCharacter g = c;
			bool num2 = owned;
			Leave();
			FBMatch.Teleport(g, p, transform.eulerAngles.y + 90f);
			if (num2)
			{
				FBTransito.GarupaDesce(g);
			}
			Dismounted++;
			return;
		}
		Transform transform2 = (an ? an.GetBoneTransform(HumanBodyBones.Hips) : null);
		if (!transform2)
		{
			return;
		}
		Transform transform3 = (car.mx ? car.mx.transform : car.transform);
		float scaleK = c.ScaleK;
		base.transform.rotation = transform3.rotation;
		base.transform.position += SeatOf(car, scaleK) - transform2.position;
		FBCharacter driver2 = car.driver;
		Transform transform4 = (((bool)driver2 && (bool)driver2.an) ? driver2.an.GetBoneTransform(HumanBodyBones.Hips) : null);
		if ((bool)transform4)
		{
			Transform transform5 = B(HumanBodyBones.LeftUpperArm);
			Transform transform6 = B(HumanBodyBones.LeftLowerArm);
			Transform transform7 = B(HumanBodyBones.LeftHand);
			Transform transform8 = B(HumanBodyBones.RightUpperArm);
			Transform transform9 = B(HumanBodyBones.RightLowerArm);
			Transform transform10 = B(HumanBodyBones.RightHand);
			Vector3 up = transform3.up;
			Vector3 right = transform3.right;
			Vector3 forward = transform3.forward;
			Vector3 vector = transform4.position + up * 0.1f * scaleK + forward * 0.03f * scaleK;
			if ((bool)transform5 && (bool)transform6 && (bool)transform7)
			{
				FBIK.TwoBonePole(transform5, transform6, transform7, vector - right * 0.17f * scaleK, transform6.position - right * 0.25f - up * 0.15f, 0.85f);
			}
			if ((bool)transform8 && (bool)transform9 && (bool)transform10)
			{
				FBIK.TwoBonePole(transform8, transform9, transform10, vector + right * 0.17f * scaleK, transform9.position + right * 0.25f - up * 0.15f, 0.85f);
			}
		}
		Transform B(HumanBodyBones x)
		{
			return an.GetBoneTransform(x);
		}
	}
}
}
