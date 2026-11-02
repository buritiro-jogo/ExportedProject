using UnityEngine;

namespace FreeBala
{
internal static class V3Ext
{
	public static Vector3 Abs(this Vector3 v)
	{
		return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
	}
}
}
