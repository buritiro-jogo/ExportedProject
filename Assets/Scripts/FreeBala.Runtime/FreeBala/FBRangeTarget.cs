using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBRangeTarget : MonoBehaviour
{
	private const float W = 0.78f;

	private const float H = 1.3f;

	private const float BASE = 0.85f;

	private Transform pivot;

	private bool up = true;

	private float rot;

	private float downT;

	private float ph;

	private Vector2? moving;

	public float upAt;

	public float dist;

	public Action<FBRangeTarget, bool> onHit;

	private static Texture2D tex;

	private static Texture2D Tex()
	{
		if ((bool)tex)
		{
			return tex;
		}
		tex = new Texture2D(256, 420);
		Color color = new Color(0.85f, 0.81f, 0.71f);
		Color color2 = new Color(0.11f, 0.13f, 0.16f);
		Color color3 = new Color(0.91f, 0.89f, 0.81f);
		for (int i = 0; i < 420; i++)
		{
			for (int j = 0; j < 256; j++)
			{
				int num = 419 - i;
				bool num2 = Mathf.Pow((float)(j - 128) / 42f, 2f) + Mathf.Pow((float)(num - 70) / 52f, 2f) <= 1f;
				bool flag = num >= 118 && num <= 420 && (float)j >= Mathf.Lerp(108f, 16f, Mathf.Clamp01((float)(num - 118) / 52f)) && (float)j <= Mathf.Lerp(148f, 240f, Mathf.Clamp01((float)(num - 118) / 52f));
				Color color4 = ((num2 || flag) ? color2 : color);
				(float, float, float)[] array = new(float, float, float)[4]
				{
					(260f, 46f, 60f),
					(260f, 80f, 100f),
					(260f, 108f, 140f),
					(70f, 20f, 26f)
				};
				for (int k = 0; k < array.Length; k++)
				{
					var (num3, num4, num5) = array[k];
					if (Mathf.Abs(Mathf.Pow((float)(j - 128) / num4, 2f) + Mathf.Pow(((float)num - num3) / num5, 2f) - 1f) < 0.03f)
					{
						color4 = color3;
					}
				}
				tex.SetPixel(j, i, color4);
			}
		}
		tex.Apply();
		return tex;
	}

	public static FBRangeTarget Make(Dictionary<string, object> T)
	{
		Vector3 position = Coords.FromWeb(Json.L(T, "p"));
		GameObject gameObject = new GameObject("Alvo");
		gameObject.transform.position = position;
		FBRangeTarget tg = gameObject.AddComponent<FBRangeTarget>();
		tg.dist = Json.F(T, "d");
		tg.upAt = Time.time;
		tg.ph = UnityEngine.Random.value * 6f;
		List<object> list = Json.L(T, "moving");
		if (list != null && list.Count == 2)
		{
			tg.moving = new Vector2(0f - Json.Fv(list[0]), 0f - Json.Fv(list[1]));
		}
		Material material = new Material(FBShaders.Lit);
		material.SetColor("_BaseColor", new Color(0.17f, 0.18f, 0.19f));
		int[] array = new int[2] { -1, 1 };
		foreach (int num in array)
		{
			GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
			obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
			obj.transform.localPosition = new Vector3((float)num * 0.78f * 0.42f, 0.425f, -0.06f);
			obj.transform.localScale = new Vector3(0.05f, 0.85f, 0.05f);
			obj.GetComponent<Renderer>().sharedMaterial = material;
		}
		tg.pivot = new GameObject("Pivo").transform;
		tg.pivot.SetParent(gameObject.transform, worldPositionStays: false);
		tg.pivot.localPosition = Vector3.up * 0.85f;
		GameObject obj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
		UnityEngine.Object.Destroy(obj2.GetComponent<BoxCollider>());
		obj2.transform.SetParent(tg.pivot, worldPositionStays: false);
		obj2.transform.localPosition = Vector3.up * 1.3f / 2f;
		obj2.transform.localScale = new Vector3(0.78f, 1.3f, 0.03f);
		Material material2 = new Material(FBShaders.Lit);
		material2.SetTexture("_BaseMap", Tex());
		obj2.GetComponent<Renderer>().sharedMaterial = material2;
		obj2.AddComponent<BoxCollider>().isTrigger = true;
		obj2.layer = 9;
		FBTarget fBTarget = obj2.AddComponent<FBTarget>();
		fBTarget.headY = 0.24f;
		fBTarget.onHit = delegate(FBTarget t, bool h)
		{
			tg.Hit(h);
		};
		return tg;
	}

	private void Hit(bool head)
	{
		if (up)
		{
			up = false;
			downT = UnityEngine.Random.Range(1.6f, 2.8f);
			onHit?.Invoke(this, head);
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBRangeTarget.Update");

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
		float deltaTime = Time.deltaTime;
		Vector2? vector = moving;
		if (vector.HasValue)
		{
			Vector2 valueOrDefault = vector.GetValueOrDefault();
			ph += deltaTime * 0.9f;
			Vector3 position = base.transform.position;
			position.x = (valueOrDefault.x + valueOrDefault.y) / 2f + (valueOrDefault.y - valueOrDefault.x) / 2f * Mathf.Sin(ph);
			base.transform.position = position;
		}
		if (!up)
		{
			downT -= deltaTime;
			if (downT <= 0f)
			{
				up = true;
				upAt = Time.time;
			}
		}
		float num = (up ? 0f : (-86.4f));
		rot += (num - rot) * Mathf.Min(1f, deltaTime * (float)(up ? 6 : 14));
		pivot.localRotation = Quaternion.Euler(rot, 0f, 0f);
	}
}
}
