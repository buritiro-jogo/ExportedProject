using UnityEngine;

namespace FreeBala
{
public class FBHitbox : MonoBehaviour
{
	public FBCharacter owner;

	public bool head;

	public string part = "body";

	public float mult = 1f;

	public static void Build(FBCharacter ch)
	{
		Animator an = ch.GetComponent<Animator>();
		float s;
		if ((bool)an && an.isHuman)
		{
			FBHitbox[] componentsInChildren = ch.GetComponentsInChildren<FBHitbox>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				Object.Destroy(componentsInChildren[i].gameObject);
			}
			s = ch.transform.lossyScale.y / 2.2f * 2.2f / 2.2f;
			Transform boneTransform = an.GetBoneTransform(HumanBodyBones.Head);
			if ((bool)boneTransform)
			{
				SphereCollider sphereCollider = Add<SphereCollider>(boneTransform, "Cabeca", "head", head: true);
				sphereCollider.radius = 0.17f * s / boneTransform.lossyScale.x;
				sphereCollider.center = boneTransform.InverseTransformVector(Vector3.up * 0.07f * s);
			}
			Capsule(HumanBodyBones.Hips, HumanBodyBones.Neck, 0.1736f, "body");
			// fisica 31/10: topo da capsula do tronco termina no Neck (antes a meia-esfera r 0,28 centrada no Neck engolia a esfera da cabeca e headshot virava body)
			Capsule(HumanBodyBones.Spine, HumanBodyBones.Neck, 0.28f, "body", topAtB: true);
			Capsule(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, 0.11f, "limb");
			Capsule(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot, 0.11f, "limb");
			Capsule(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand, 0.075f, "limb");
			Capsule(HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand, 0.075f, "limb");
		}
		T Add<T>(Transform parent, string n, string part, bool head) where T : Collider
		{
			GameObject obj = new GameObject("HB_" + n);
			obj.layer = 9;
			obj.transform.SetParent(parent, worldPositionStays: false);
			T val = obj.AddComponent<T>();
			val.isTrigger = true;
			FBHitbox fBHitbox = obj.AddComponent<FBHitbox>();
			fBHitbox.owner = ch;
			fBHitbox.head = head;
			fBHitbox.part = part;
			return val;
		}
		void Capsule(HumanBodyBones a, HumanBodyBones b, float r, string part, bool topAtB = false)
		{
			Transform boneTransform2 = an.GetBoneTransform(a);
			Transform boneTransform3 = an.GetBoneTransform(b);
			if ((bool)boneTransform2 && (bool)boneTransform3)
			{
				CapsuleCollider capsuleCollider = Add<CapsuleCollider>(boneTransform2, a.ToString(), part, head: false);
				Vector3 vector = boneTransform2.InverseTransformPoint(boneTransform3.position);
				float x = boneTransform2.lossyScale.x;
				capsuleCollider.center = vector * 0.5f;
				capsuleCollider.radius = r * s / x;
				capsuleCollider.height = vector.magnitude + capsuleCollider.radius * 2f;
				if (topAtB)
				{
					// fisica 31/10: a superficie de cima termina no osso b (Neck) em vez de a meia-esfera ficar centrada nele; a base (a - r) fica onde estava
					capsuleCollider.height -= capsuleCollider.radius;
					capsuleCollider.center -= vector.normalized * (capsuleCollider.radius * 0.5f);
				}
				capsuleCollider.direction = ((!(Mathf.Abs(vector.x) > Mathf.Abs(vector.y))) ? ((Mathf.Abs(vector.y) > Mathf.Abs(vector.z)) ? 1 : 2) : ((!(Mathf.Abs(vector.x) > Mathf.Abs(vector.z))) ? 2 : 0));
			}
		}
	}
}
}
