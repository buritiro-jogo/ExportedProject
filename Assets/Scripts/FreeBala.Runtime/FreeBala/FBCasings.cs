using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBCasings
{
	private static readonly List<(Transform t, Vector3 v, float life, bool snd)> live = new List<(Transform, Vector3, float, bool)>();

	private static Mesh mesh;

	private static Material mat;

	private static int idx;

	private static readonly List<Transform> pool = new List<Transform>();

	public static void Eject(FBCharacter ch, WeaponDef d)
	{
		if (!ch || d == null || d.cls == "explosivo" || !Camera.main || Vector3.Distance(Camera.main.transform.position, ch.Pos) > 25f)
		{
			return;
		}
		if (pool.Count == 0)
		{
			GameObject gameObject = new GameObject("FB_Capsulas");
			Object.DontDestroyOnLoad(gameObject);
			mesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx") ?? MakeMesh();
			mat = new Material(FBShaders.Lit);
			mat.SetColor("_BaseColor", new Color(0.83f, 0.66f, 0.28f));
			mat.SetFloat("_Metallic", 0.9f);
			mat.SetFloat("_Smoothness", 0.6f);
			for (int i = 0; i < 40; i++)
			{
				GameObject gameObject2 = new GameObject("c");
				gameObject2.transform.SetParent(gameObject.transform);
				gameObject2.AddComponent<MeshFilter>().sharedMesh = mesh;
				gameObject2.AddComponent<MeshRenderer>().sharedMaterial = mat;
				gameObject2.transform.localScale = new Vector3(0.012f, 0.022f, 0.012f);
				gameObject2.SetActive(value: false);
				pool.Add(gameObject2.transform);
			}
			gameObject.AddComponent<FBCasingTicker>();
		}
		Transform t = pool[idx++ % pool.Count];
		t.gameObject.SetActive(value: true);
		Transform transform = (ch.Muzzle ? ch.Muzzle : ch.transform);
		t.position = (ch.RightHand ? ch.RightHand.position : transform.position) + Vector3.up * 0.05f;
		Vector3 item = ch.transform.right * Random.Range(1.5f, 2.6f) + Vector3.up * Random.Range(1.2f, 2.2f) - ch.transform.forward * 0.4f;
		live.RemoveAll(((Transform t, Vector3 v, float life, bool snd) x) => x.t == t);
		live.Add((t, item, 0f, false));
	}

	private static Mesh MakeMesh()
	{
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		Mesh sharedMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
		Object.Destroy(gameObject);
		return sharedMesh;
	}

	public static void Tick(float dt)
	{
		for (int num = live.Count - 1; num >= 0; num--)
		{
			(Transform t, Vector3 v, float life, bool snd) tuple = live[num];
			Transform item = tuple.t;
			Vector3 vector = tuple.v;
			float item2 = tuple.life;
			bool flag = tuple.snd;
			item2 += dt;
			if (!item || item2 > 4f)
			{
				if ((bool)item)
				{
					item.gameObject.SetActive(value: false);
				}
				live.RemoveAt(num);
			}
			else
			{
				vector.y -= 9.8f * dt;
				Vector3 vector2 = item.position + vector * dt;
				item.Rotate(720f * dt, 360f * dt, 0f);
				float? num2 = FBWorld.GroundY(vector2.x, vector2.y + 0.05f, vector2.z, 0.05f, 0.2f);
				if (num2.HasValue)
				{
					float valueOrDefault = num2.GetValueOrDefault();
					if (vector2.y <= valueOrDefault + 0.01f)
					{
						vector2.y = valueOrDefault + 0.01f;
						vector = new Vector3(vector.x * 0.4f, Mathf.Abs(vector.y) * 0.25f, vector.z * 0.4f);
						if (!flag)
						{
							flag = true;
							FBAudio.PlayAt("casing" + Random.Range(1, 4), vector2, 0.35f, 2f);
						}
					}
				}
				item.position = vector2;
				live[num] = (item, vector, item2, flag);
			}
		}
	}
}
}
