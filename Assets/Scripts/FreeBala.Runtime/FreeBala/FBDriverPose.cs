using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
[DefaultExecutionOrder(1000)]
public class FBDriverPose : MonoBehaviour
{
	[NonSerialized]
	public float reachLean;

	private FBCar car;

	private FBCharacter ch;

	private Animator an;

	private int posed = -1;

	private int LU = -1;

	private int LE = -1;

	private int LEU = -1;

	private int groundedH;

	private bool hasGrounded;

	private float w;

	[NonSerialized]
	public string trans;

	[NonSerialized]
	public float transT;

	[NonSerialized]
	public float transLen;

	[NonSerialized]
	public Vector3 baseL;

	[NonSerialized]
	public bool carAnim;

	[NonSerialized]
	public Action<FBCharacter, FBCar> onOut;

	[NonSerialized]
	public bool path;

	[NonSerialized]
	public string pathClip;

	[NonSerialized]
	public Vector3 pathS0;

	[NonSerialized]
	public Vector3 pathS;

	[NonSerialized]
	public Vector3 pathE;

	[NonSerialized]
	public float yaw0;

	private static readonly string[] ZERO = new string[5] { "VelX", "VelZ", "Speed", "VerticalVelocity", "CrouchAlpha" };

	private static readonly string[] OFF = new string[7] { "IsRunning", "IsAiming", "IsShooting", "InCombat", "InAir", "IsCrouching", "Tactical Sprint" };

	private readonly List<int> zeroF = new List<int>();

	private readonly List<int> offB = new List<int>();

	public FBDrive.PoseTargets Last { get; private set; }

	public FBCar Car => car;

	public float Weight => w;

	public void Set(FBCar c)
	{
		if (car != c)
		{
			w = 0f;
		}
		car = c;
		ch = GetComponent<FBCharacter>();
		an = (ch ? ch.an : null);
		if (!an)
		{
			return;
		}
		LU = an.GetLayerIndex("Upper Body");
		LE = an.GetLayerIndex("Emote Full Body");
		LEU = an.GetLayerIndex("Emote Upper Body");
		zeroF.Clear();
		offB.Clear();
		hasGrounded = false;
		AnimatorControllerParameter[] parameters = an.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			string text = animatorControllerParameter.name.Trim();
			if (animatorControllerParameter.type == AnimatorControllerParameterType.Float && Array.IndexOf(ZERO, text) >= 0)
			{
				zeroF.Add(animatorControllerParameter.nameHash);
			}
			else if (animatorControllerParameter.type == AnimatorControllerParameterType.Bool && Array.IndexOf(OFF, text) >= 0)
			{
				offB.Add(animatorControllerParameter.nameHash);
			}
			else if (animatorControllerParameter.type == AnimatorControllerParameterType.Bool && text == "IsGrounded")
			{
				groundedH = animatorControllerParameter.nameHash;
				hasGrounded = true;
			}
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBDriverPose.Update");

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
		if (!car || !ch || !an || !an.runtimeAnimatorController)
		{
			return;
		}
		w = Mathf.MoveTowards(w, 1f, Time.deltaTime / 0.2f);
		foreach (int item in zeroF)
		{
			an.SetFloat(item, 0f);
		}
		foreach (int item2 in offB)
		{
			an.SetBool(item2, value: false);
		}
		if (hasGrounded)
		{
			an.SetBool(groundedH, value: true);
		}
		if (ch.alive && !ch.special)
		{
			if (LU >= 0)
			{
				an.SetLayerWeight(LU, 0f);
			}
			if (LEU >= 0)
			{
				an.SetLayerWeight(LEU, 0f);
			}
			if (LE >= 0 && ch.sys != null)
			{
				an.SetLayerWeight(LE, Mathf.Max(ch.DanW, (trans == "mount" || trans == "dismount") ? 1f : Mathf.SmoothStep(0f, 1f, w)));
			}
			an.speed = (((trans == "mount" || trans == "dismount") && !carAnim) ? 1.5f : 1f);
		}
	}

	public void PoseNow()
	{
		if (posed != Time.frameCount && (bool)car && (bool)ch)
		{
			posed = Time.frameCount;
			Last = FBDrive.PoseActor(ch, car, Mathf.SmoothStep(0f, 1f, w), this);
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBDriverPose.LateUpdate");

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
		if (!car || !ch)
		{
			UnityEngine.Object.Destroy(this);
			return;
		}
		if (trans != null && (!ch.alive || FBVehicles.CarOf(ch) != car))
		{
			trans = null;
		}
		PoseNow();
		if (trans == null)
		{
			return;
		}
		transT += Time.deltaTime;
		if (transT < transLen)
		{
			return;
		}
		if (trans == "mount")
		{
			trans = "blend";
			transT = 0f;
			transLen = 0.35f;
			ch.sys = FBDrive.SitClip(car);
			if (carAnim && FBDoors.HasPart(car, 1))
			{
				FBDoors.SetMask(car, FBDoors.Mask(car) & -2, sound: true);
			}
		}
		else if (trans == "blend")
		{
			trans = null;
			carAnim = false;
		}
		else if (trans == "dismount")
		{
			trans = null;
			if (onOut != null)
			{
				Action<FBCharacter, FBCar> action = onOut;
				onOut = null;
				action(ch, car);
			}
			else
			{
				FBDrive.FinishDismount(ch, car);
			}
		}
	}
}
}
