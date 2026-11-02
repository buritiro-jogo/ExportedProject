using UnityEngine;

namespace FreeBala
{
internal static class V3Flat
{
	public static Vector3 Flat3(this Vector3 v, float y)
	{
		return new Vector3(v.x, y, v.z);
	}
}
}
