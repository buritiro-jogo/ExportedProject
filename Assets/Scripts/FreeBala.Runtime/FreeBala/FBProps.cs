using System;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBProps
{
	private static Material Lit(Color c)
	{
		Material material = new Material(FBShaders.Lit);
		material.SetColor("_BaseColor", c);
		material.SetFloat("_Smoothness", 0.15f);
		return material;
	}

	private static GameObject Prim(PrimitiveType t, Transform p, Vector3 pos, Vector3 sc, Material m)
	{
		GameObject gameObject = GameObject.CreatePrimitive(t);
		UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
		gameObject.transform.SetParent(p, worldPositionStays: false);
		gameObject.transform.localPosition = pos;
		gameObject.transform.localScale = sc;
		gameObject.GetComponent<Renderer>().sharedMaterial = m;
		return gameObject;
	}

	private static void Glow(Transform p, Color c)
	{
		Material material = new Material(FBShaders.Unlit);
		material.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.35f));
		material.SetFloat("_Surface", 1f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		if (material.renderQueue < 3000)
		{
			material.renderQueue = 3000;
		}
		material.SetInt("_SrcBlend", 5);
		material.SetInt("_DstBlend", 1);
		material.SetFloat("_ZWrite", 0f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.renderQueue = 3050;
		GameObject gameObject = FBZone.Cylinder(8, 40f, material);
		gameObject.transform.SetParent(p, worldPositionStays: false);
		gameObject.transform.localPosition = Vector3.up * 20.9f;
		gameObject.transform.localScale = new Vector3(0.12f, 1f, 0.12f);
	}

	public static GameObject FarinhaSaco()
	{
		GameObject gameObject = new GameObject("Farinha");
		Material m = Lit(new Color(0.9f, 0.84f, 0.66f));
		Material m2 = Lit(new Color(0.96f, 0.94f, 0.85f));
		Prim(PrimitiveType.Sphere, gameObject.transform, Vector3.up * 0.44f, new Vector3(0.65f, 0.85f, 0.49f), m);
		Prim(PrimitiveType.Cylinder, gameObject.transform, Vector3.up * 0.9f, new Vector3(0.2f, 0.1f, 0.2f), m);
		Prim(PrimitiveType.Cylinder, gameObject.transform, Vector3.up * 0.86f, new Vector3(0.22f, 0.02f, 0.22f), Lit(new Color(0.48f, 0.29f, 0.13f)));
		Prim(PrimitiveType.Sphere, gameObject.transform, Vector3.up * 0.02f, new Vector3(0.92f, 0.2f, 0.92f), m2);
		Prim(PrimitiveType.Quad, gameObject.transform, new Vector3(0f, 0.5f, 0.25f), new Vector3(0.4f, 0.2f, 1f), Lit(new Color(0.96f, 0.94f, 0.85f))).transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
		Glow(gameObject.transform, new Color(0.95f, 0.87f, 0.63f));
		gameObject.transform.localScale = Vector3.one * 1.3f;
		return gameObject;
	}

	public static GameObject BuritiCacho()
	{
		GameObject gameObject = new GameObject("Buriti");
		Material m = Lit(new Color(0.66f, 0.45f, 0.23f));
		Prim(PrimitiveType.Cylinder, gameObject.transform, Vector3.up * 0.15f, new Vector3(0.66f, 0.15f, 0.66f), m);
		Material[] array = new Color[4]
		{
			new Color(0.56f, 0.18f, 0.08f),
			new Color(0.72f, 0.27f, 0.1f),
			new Color(0.85f, 0.4f, 0.12f),
			new Color(0.44f, 0.14f, 0.07f)
		}.Select(Lit).ToArray();
		int num = 0;
		for (int i = 0; i < 3; i++)
		{
			int num2 = 0;
			while (num2 < 7 - i * 2)
			{
				float f = (float)num2 / (float)(7 - i * 2) * (float)Math.PI * 2f + (float)i;
				float num3 = 0.22f - (float)i * 0.07f;
				Prim(PrimitiveType.Sphere, gameObject.transform, new Vector3(Mathf.Cos(f) * num3, 0.38f + (float)i * 0.13f, Mathf.Sin(f) * num3), new Vector3(0.2f, 0.24f, 0.2f), array[num % 4]);
				num2++;
				num++;
			}
		}
		Prim(PrimitiveType.Cylinder, gameObject.transform, Vector3.up * 0.72f, new Vector3(0.06f, 0.25f, 0.06f), Lit(new Color(0.35f, 0.24f, 0.11f)));
		Glow(gameObject.transform, new Color(0.85f, 0.31f, 0.11f));
		gameObject.transform.localScale = Vector3.one * 1.4f;
		return gameObject;
	}
}
}
