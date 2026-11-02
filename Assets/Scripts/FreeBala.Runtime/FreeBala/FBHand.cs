using UnityEngine;

namespace FreeBala
{
public static class FBHand
{
	public static readonly Vector3 RefFwd = new Vector3(-0.0855f, 0.9963f, -0.0096f);

	public static readonly Vector3 RefSide = new Vector3(-0.9963f, -0.0854f, 0.0024f);

	public const float RefPalm = 0.0639f;

	public const float SameRigDeg = 10f;

	public static Quaternion RefRight
	{
		get
		{
			Vector3 normalized = RefFwd.normalized;
			Vector3 normalized2 = Vector3.ProjectOnPlane(RefSide, normalized).normalized;
			return Quaternion.LookRotation(normalized, Vector3.Cross(normalized, normalized2));
		}
	}

	public static bool Frame(Animator an, bool right, out Quaternion local, out float palm)
	{
		local = Quaternion.identity;
		palm = 0.0639f;
		if (!an || !an.isHuman)
		{
			return false;
		}
		Transform transform = B(HumanBodyBones.RightHand, HumanBodyBones.LeftHand);
		Transform transform2 = B(HumanBodyBones.RightIndexProximal, HumanBodyBones.LeftIndexProximal);
		Transform transform3 = B(HumanBodyBones.RightMiddleProximal, HumanBodyBones.LeftMiddleProximal);
		Transform transform4 = B(HumanBodyBones.RightLittleProximal, HumanBodyBones.LeftLittleProximal);
		if (!transform4)
		{
			transform4 = B(HumanBodyBones.RightRingProximal, HumanBodyBones.LeftRingProximal);
		}
		if (!transform || !transform2 || !transform3 || !transform4)
		{
			return false;
		}
		Vector3 vector = transform.InverseTransformPoint(transform3.position);
		Vector3 vector2 = transform.InverseTransformPoint(transform2.position);
		Vector3 vector3 = transform.InverseTransformPoint(transform4.position);
		if (vector.sqrMagnitude < 1E-10f)
		{
			return false;
		}
		Vector3 normalized = vector.normalized;
		Vector3 vector4 = Vector3.ProjectOnPlane(vector2 - vector3, normalized);
		if (vector4.sqrMagnitude < 1E-12f)
		{
			return false;
		}
		vector4.Normalize();
		Vector3 upwards = (right ? Vector3.Cross(normalized, vector4) : Vector3.Cross(vector4, normalized));
		local = Quaternion.LookRotation(normalized, upwards);
		palm = vector.magnitude;
		return true;
		Transform B(HumanBodyBones r, HumanBodyBones l)
		{
			return an.GetBoneTransform(right ? r : l);
		}
	}

	public static Quaternion RightCorrection(Animator an, out float palmK, out float deg)
	{
		palmK = 1f;
		deg = 0f;
		if (!Frame(an, right: true, out var local, out var palm))
		{
			return Quaternion.identity;
		}
		Quaternion quaternion = local * Quaternion.Inverse(RefRight);
		deg = Quaternion.Angle(quaternion, Quaternion.identity);
		if (deg < 10f)
		{
			return Quaternion.identity;
		}
		palmK = Mathf.Clamp(palm / 0.0639f, 0.85f, 1.2f);
		return quaternion;
	}
}
}
