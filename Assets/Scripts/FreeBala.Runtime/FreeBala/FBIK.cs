using UnityEngine;

namespace FreeBala
{
public static class FBIK
{
	// poleW (v16): quanto o cotovelo vai para o plano do polo. 1 = como sempre foi (todas as chamadas antigas);
	// menor que 1 mistura com o plano atual do braço, para o cotovelo entrar e sair junto com o peso, sem estalo.
	public static void TwoBonePole(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 pole, float w = 1f, float poleW = 1f)
	{
		if (!upper || !fore || !hand)
		{
			return;
		}
		Vector3 position = upper.position;
		Vector3 position2 = fore.position;
		Vector3 position3 = hand.position;
		float magnitude = (position2 - position).magnitude;
		float magnitude2 = (position3 - position2).magnitude;
		Vector3 vector = Vector3.Lerp(position3, target, w) - position;
		float magnitude3 = vector.magnitude;
		if (!(magnitude3 < 1E-05f))
		{
			magnitude3 = Mathf.Clamp(magnitude3, Mathf.Abs(magnitude - magnitude2) + 0.001f, magnitude + magnitude2 - 0.001f);
			Vector3 normalized = vector.normalized;
			Vector3 vector2 = pole - position;
			vector2 -= normalized * Vector3.Dot(vector2, normalized);
			if (vector2.sqrMagnitude < 1E-08f)
			{
				vector2 = position2 - position;
				vector2 -= normalized * Vector3.Dot(vector2, normalized);
			}
			vector2.Normalize();
			if (poleW < 0.999f)
			{
				// giro atual do cotovelo medido em volta da linha ombro-mão de agora e levado para a linha nova
				Vector3 h0 = position3 - position;
				if (h0.sqrMagnitude > 1E-08f)
				{
					h0.Normalize();
					Vector3 cur = position2 - position;
					cur -= h0 * Vector3.Dot(cur, h0);
					if (cur.sqrMagnitude > 1E-08f)
					{
						cur = Quaternion.FromToRotation(h0, normalized) * cur;
						vector2 = Vector3.Slerp(cur.normalized, vector2, Mathf.Clamp01(poleW)).normalized;
					}
				}
			}
			float num = (magnitude * magnitude - magnitude2 * magnitude2 + magnitude3 * magnitude3) / (2f * magnitude3);
			float num2 = Mathf.Sqrt(Mathf.Max(0f, magnitude * magnitude - num * num));
			Vector3 vector3 = position + normalized * num + vector2 * num2;
			Vector3 vector4 = position + normalized * magnitude3;
			upper.rotation = Quaternion.FromToRotation(position2 - position, vector3 - position) * upper.rotation;
			Vector3 position4 = fore.position;
			Vector3 position5 = hand.position;
			fore.rotation = Quaternion.FromToRotation(position5 - position4, vector4 - position4) * fore.rotation;
		}
	}
}
}
