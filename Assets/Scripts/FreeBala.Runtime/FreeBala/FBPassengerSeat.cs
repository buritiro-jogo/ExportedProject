using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
[DefaultExecutionOrder(1001)]
public class FBPassengerSeat : MonoBehaviour
{
	private FBCharacter c;

	private FBCar car;

	private CharacterController cc;

	private Animator an;

	private int LU = -1;

	private int LE = -1;

	private int LEU = -1;

	private readonly List<int> zeroF = new List<int>();

	private readonly List<int> offB = new List<int>();

	private static readonly string[] ZERO = new string[5] { "VelX", "VelZ", "Speed", "VerticalVelocity", "CrouchAlpha" };

	private static readonly string[] OFF = new string[7] { "IsRunning", "IsAiming", "IsShooting", "InCombat", "InAir", "IsCrouching", "Tactical Sprint" };

	public void Sit(FBCar u)
	{
		c = GetComponent<FBCharacter>();
		cc = GetComponent<CharacterController>();
		car = u;
		if (!c || !u)
		{
			return;
		}
		if ((bool)cc)
		{
			cc.enabled = false;
		}
		c.aiming = (c.hasAim = false);
		c.SetWeaponVisible(v: false);
		c.vehiclePose = true;
		c.velocity = Vector3.zero;
		c.animVel = Vector2.zero;
		string sys = FBDrive.SitClip(u) ?? c.npcSet?.sit.FirstOrDefault((string n) => (bool)FBRegistry.I.Clip(n) && !Regex.IsMatch(n, "_to_|Stand_to", RegexOptions.IgnoreCase));
		c.StopDance(force: true);
		c.sys = sys;
		an = c.an;
		zeroF.Clear();
		offB.Clear();
		if (!an)
		{
			return;
		}
		LU = an.GetLayerIndex("Upper Body");
		LE = an.GetLayerIndex("Emote Full Body");
		LEU = an.GetLayerIndex("Emote Upper Body");
		AnimatorControllerParameter[] parameters = an.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			string value = animatorControllerParameter.name.Trim();
			if (animatorControllerParameter.type == AnimatorControllerParameterType.Float && Array.IndexOf(ZERO, value) >= 0)
			{
				zeroF.Add(animatorControllerParameter.nameHash);
			}
			else if (animatorControllerParameter.type == AnimatorControllerParameterType.Bool && Array.IndexOf(OFF, value) >= 0)
			{
				offB.Add(animatorControllerParameter.nameHash);
			}
		}
	}

	public void Leave()
	{
		if ((bool)c)
		{
			c.vehiclePose = false;
			c.sys = null;
			if ((bool)cc)
			{
				cc.enabled = true;
			}
		}
		car = null;
		UnityEngine.Object.Destroy(this);
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBPassengerSeat.Update");

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
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBPassengerSeat.LateUpdate");

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
		Animator an = c.an;
		Transform transform = (an ? an.GetBoneTransform(HumanBodyBones.Hips) : null);
		if (!transform)
		{
			return;
		}
		base.transform.rotation = car.transform.rotation;
		Vector3 vector = car.L2W(new Vector3(0f - car.seat.x, car.seat.y, car.seat.z));
		base.transform.position += vector - transform.position;
		Vector3 up = car.transform.up;
		Vector3 forward = car.transform.forward;
		Vector3 right = car.transform.right;
		if (c.fitCar != car)
		{
			c.fitCar = car;
			c.seatDrop = 0f;
			Transform boneTransform = an.GetBoneTransform(HumanBodyBones.Head);
			if ((bool)boneTransform)
			{
				float num = 9f;
				RaycastHit[] array = Physics.RaycastAll(boneTransform.position + up * 1.4f, -up, 1.4f, 2048, QueryTriggerInteraction.Ignore);
				for (int i = 0; i < array.Length; i++)
				{
					RaycastHit raycastHit = array[i];
					if (raycastHit.collider.GetComponentInParent<FBCar>() == car && raycastHit.distance < num)
					{
						num = raycastHit.distance;
					}
				}
				if (num < 9f)
				{
					float num2 = 1.4f - num - 0.035f - 0.22f * (base.transform.lossyScale.y / 2.2f * 1.25f);
					c.seatDrop = Mathf.Min(0.35f, Mathf.Max(0f, 0.04f - num2));
				}
			}
		}
		if (c.seatDrop > 0f)
		{
			base.transform.position -= up * c.seatDrop;
		}
		if (car.rigFeet != null && car.rigFeet.Length >= 2)
		{
			for (int j = 0; j < 2; j++)
			{
				Transform transform4;
				Transform transform5;
				Transform transform6;
				if (j != 0)
				{
					Transform obj = B(HumanBodyBones.RightUpperLeg);
					Transform transform2 = B(HumanBodyBones.RightLowerLeg);
					Transform transform3 = B(HumanBodyBones.RightFoot);
					transform4 = transform3;
					transform5 = transform2;
					transform6 = obj;
				}
				else
				{
					Transform obj2 = B(HumanBodyBones.LeftUpperLeg);
					Transform transform3 = B(HumanBodyBones.LeftLowerLeg);
					Transform transform2 = B(HumanBodyBones.LeftFoot);
					transform4 = transform2;
					transform5 = transform3;
					transform6 = obj2;
				}
				if ((bool)transform6 && (bool)transform5 && (bool)transform4)
				{
					Vector3 vector2 = car.rigFeet[1 - j];
					Quaternion rotation = transform4.rotation;
					FBIK.TwoBonePole(transform6, transform5, transform4, car.L2W(new Vector3(0f - vector2.x, vector2.y, vector2.z)), transform5.position + forward * 0.6f + up * 0.35f);
					transform4.rotation = rotation;
				}
			}
		}
		for (int k = 0; k < 2; k++)
		{
			Transform transform9;
			Transform transform10;
			Transform transform11;
			Transform transform12;
			Transform transform13;
			if (k != 0)
			{
				Transform obj3 = B(HumanBodyBones.RightUpperArm);
				Transform transform2 = B(HumanBodyBones.RightLowerArm);
				Transform transform3 = B(HumanBodyBones.RightHand);
				Transform transform7 = B(HumanBodyBones.RightUpperLeg);
				Transform transform8 = B(HumanBodyBones.RightLowerLeg);
				transform9 = transform8;
				transform10 = transform7;
				transform11 = transform3;
				transform12 = transform2;
				transform13 = obj3;
			}
			else
			{
				Transform obj4 = B(HumanBodyBones.LeftUpperArm);
				Transform transform8 = B(HumanBodyBones.LeftLowerArm);
				Transform transform7 = B(HumanBodyBones.LeftHand);
				Transform transform3 = B(HumanBodyBones.LeftUpperLeg);
				Transform transform2 = B(HumanBodyBones.LeftLowerLeg);
				transform9 = transform2;
				transform10 = transform3;
				transform11 = transform7;
				transform12 = transform8;
				transform13 = obj4;
			}
			if ((bool)transform13 && (bool)transform12 && (bool)transform11 && (bool)transform10 && (bool)transform9)
			{
				float num3 = ((Vector3.Dot(transform13.position - transform.position, right) > 0f) ? 1 : (-1));
				Vector3 target = Vector3.Lerp(transform10.position, transform9.position, 0.55f) + up * 0.09f - right * num3 * 0.02f;
				FBIK.TwoBonePole(transform13, transform12, transform11, target, transform12.position - forward * 0.35f + right * num3 * 0.25f - up * 0.1f);
			}
		}
		bool flag = (car.transform.position - FBStreetEvents.PlayerPos).sqrMagnitude > 12100f;
		Renderer[] componentsInChildren = c.GetComponentsInChildren<Renderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = !flag;
		}
		Transform B(HumanBodyBones x)
		{
			return an.GetBoneTransform(x);
		}
	}
}
}
