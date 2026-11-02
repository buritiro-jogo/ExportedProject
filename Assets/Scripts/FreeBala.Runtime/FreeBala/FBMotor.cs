using UnityEngine;

namespace FreeBala
{
public static class FBMotor
{
	public struct Ground
	{
		public bool ok;

		public bool steep;

		public float dist;

		public float angle;

		public Vector3 point;

		public Vector3 normal;

		public Collider col;
	}

	private static readonly RaycastHit[] hits = new RaycastHit[8];

	private static readonly Collider[] overlaps = new Collider[8];

	public static bool StepAssist = true;

	public static Ground Probe(CharacterController cc, float extra)
	{
		Ground result = default(Ground);
		Transform transform = cc.transform;
		float y = transform.lossyScale.y;
		float num = cc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
		float num2 = Mathf.Max(cc.height * y, 2f * num);
		Vector3 vector = transform.TransformPoint(cc.center) - Vector3.up * (num2 * 0.5f - num);
		float num3 = cc.skinWidth * y;
		float num4 = num * 0.9f;
		float num5 = num * 0.1f + num3;
		float maxDistance = num5 + num3 + extra;
		int num6 = Physics.SphereCastNonAlloc(vector + Vector3.up * num5, num4, Vector3.down, hits, maxDistance, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		float num7 = 1E+09f;
		for (int i = 0; i < num6; i++)
		{
			RaycastHit raycastHit = hits[i];
			if (!(raycastHit.distance <= 0f) && !(raycastHit.distance >= num7) && (bool)raycastHit.collider && !raycastHit.collider.transform.IsChildOf(transform))
			{
				num7 = raycastHit.distance;
				result.ok = true;
				result.point = raycastHit.point;
				result.normal = raycastHit.normal;
				result.col = raycastHit.collider;
				result.dist = Mathf.Max(0f, raycastHit.distance - num5 - (num - num4));
			}
		}
		if (!result.ok)
		{
			return result;
		}
		if (Physics.Raycast(result.point + Vector3.up * 0.1f, Vector3.down, out var hitInfo, 0.2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && hitInfo.collider == result.col && hitInfo.normal.y > result.normal.y)
		{
			result.normal = hitInfo.normal;
		}
		if (Physics.Raycast(vector + Vector3.up * (num5 + 0.05f), Vector3.down, out var hitInfo2, num5 + 0.05f + num + num3 + extra, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && (bool)hitInfo2.collider && !hitInfo2.collider.transform.IsChildOf(transform) && hitInfo2.normal.y > result.normal.y && Mathf.Abs(hitInfo2.point.y - result.point.y) < 0.1f * y + num3)
		{
			result.normal = hitInfo2.normal;
		}
		result.angle = Vector3.Angle(result.normal, Vector3.up);
		result.steep = result.angle > cc.slopeLimit + 2f;
		return result;
	}

	private static bool StepUp(CharacterController cc, Vector3 flat, float maxStep, out float rise)
	{
		rise = 0f;
		Transform transform = cc.transform;
		float y = transform.lossyScale.y;
		float num = cc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
		float num2 = Mathf.Max(cc.height * y, 2f * num);
		float num3 = cc.skinWidth * y;
		if (maxStep <= num3 * 2f || flat.sqrMagnitude < 1E-08f)
		{
			return false;
		}
		Vector3 vector = transform.TransformPoint(cc.center);
		float num4 = num2 * 0.5f - num;
		Vector3 normalized = flat.normalized;
		float num5 = num * 0.95f;
		float num6 = maxStep + num3;
		float num7 = Mathf.Max(flat.magnitude, num * 0.6f) + num3;
		Vector3 vector2 = vector - Vector3.up * num4;
		Vector3 vector3 = vector + Vector3.up * num4;
		Vector3 origin = vector2 + Vector3.up * num6 + normalized * num7;
		Collider collider = null;
		float num8 = 1E+09f;
		Vector3 from = Vector3.up;
		Vector3 vector4 = Vector3.zero;
		int num9 = Physics.SphereCastNonAlloc(origin, num5, Vector3.down, hits, num6 + num3 * 2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < num9; i++)
		{
			RaycastHit raycastHit = hits[i];
			if (!(raycastHit.distance <= 0f) && !(raycastHit.distance >= num8) && (bool)raycastHit.collider && !raycastHit.collider.transform.IsChildOf(transform))
			{
				num8 = raycastHit.distance;
				collider = raycastHit.collider;
				from = raycastHit.normal;
				vector4 = raycastHit.point;
			}
		}
		if (!collider)
		{
			return false;
		}
		if (Physics.Raycast(vector4 + Vector3.up * 0.1f, Vector3.down, out var hitInfo, 0.2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && hitInfo.collider == collider && hitInfo.normal.y > from.y)
		{
			from = hitInfo.normal;
		}
		if (Vector3.Angle(from, Vector3.up) > cc.slopeLimit + 2f)
		{
			return false;
		}
		float num10 = num6 - num8 + (num - num5);
		if (num10 <= num3 || num10 > maxStep + num3)
		{
			return false;
		}
		float num11 = num10 + num3;
		Vector3 vector5 = vector2 + Vector3.up * num11;
		Vector3 vector6 = vector3 + Vector3.up * num11;
		num9 = Physics.OverlapCapsuleNonAlloc(vector5, vector6, num5, overlaps, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		for (int j = 0; j < num9; j++)
		{
			if ((bool)overlaps[j] && !overlaps[j].transform.IsChildOf(transform))
			{
				return false;
			}
		}
		num9 = Physics.CapsuleCastNonAlloc(vector5, vector6, num5, normalized, hits, num7, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		for (int k = 0; k < num9; k++)
		{
			RaycastHit raycastHit2 = hits[k];
			if ((bool)raycastHit2.collider && raycastHit2.distance > 0f && !raycastHit2.collider.transform.IsChildOf(transform))
			{
				return false;
			}
		}
		rise = num10;
		return true;
	}

	public static CollisionFlags Move(CharacterController cc, Vector3 motion, bool wasGrounded, bool ascending, float stepLocal, float snapWorld, ref float slide, out bool grounded, out Ground g)
	{
		if (!cc || !cc.enabled || !cc.gameObject.activeInHierarchy)
		{
			grounded = wasGrounded;
			g = default(Ground);
			return CollisionFlags.None;
		}
		Transform transform = cc.transform;
		float y = transform.lossyScale.y;
		bool flag = wasGrounded;
		if (!flag && !ascending)
		{
			Ground ground = Probe(cc, 0.15f);
			flag = ground.ok && !ground.steep && ground.dist <= 0.15f;
		}
		cc.stepOffset = (flag ? stepLocal : (stepLocal * 0.18f));
		Vector3 position = transform.position;
		CollisionFlags collisionFlags = cc.Move(motion);
		if (StepAssist && flag && !ascending && (collisionFlags & CollisionFlags.Sides) != 0)
		{
			Vector3 vector = new Vector3(motion.x, 0f, motion.z);
			Vector3 vector2 = transform.position - position;
			vector2.y = 0f;
			if (vector.magnitude > 0.0001f && vector2.magnitude < vector.magnitude * 0.35f && StepUp(cc, vector, stepLocal * y, out var rise))
			{
				collisionFlags |= cc.Move(Vector3.up * rise);
				collisionFlags |= cc.Move(vector - vector2);
			}
		}
		g = Probe(cc, snapWorld + 0.05f);
		if (!cc.isGrounded && flag && !ascending && g.ok && !g.steep && g.dist > 0.001f && g.dist <= snapWorld + 0.05f)
		{
			collisionFlags |= cc.Move(Vector3.down * (g.dist + cc.skinWidth * y * 0.5f));
			g = Probe(cc, 0.1f);
		}
		bool flag2 = g.ok && g.dist <= cc.skinWidth * y + 0.03f;
		grounded = !ascending && (cc.isGrounded || flag2) && !(g.ok && g.steep && flag2);
		if (g.ok && g.steep && flag2)
		{
			slide = Mathf.Min(slide + 18f * Time.deltaTime, 9f);
			Vector3 normalized = Vector3.ProjectOnPlane(Vector3.down, g.normal).normalized;
			collisionFlags |= cc.Move(normalized * slide * Time.deltaTime);
		}
		else
		{
			slide = 0f;
		}
		return collisionFlags;
	}
}
}
