using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class Coords
{
	public static Vector3 FromWeb(float x, float y, float z)
	{
		return new Vector3(0f - x, y, z);
	}

	public static Vector3 FromWeb(List<object> p)
	{
		if (p != null && p.Count >= 3)
		{
			return FromWeb(Json.Fv(p[0]), Json.Fv(p[1]), Json.Fv(p[2]));
		}
		return Vector3.zero;
	}

	public static float[] ToWeb(Vector3 v)
	{
		return new float[3]
		{
			0f - v.x,
			v.y,
			v.z
		};
	}

	public static float YawFromWeb(float webYawRad)
	{
		return (0f - webYawRad) * 57.29578f;
	}

	public static float YawToWeb(float unityYawDeg)
	{
		return (0f - unityYawDeg) * ((float)Math.PI / 180f);
	}
}
}
