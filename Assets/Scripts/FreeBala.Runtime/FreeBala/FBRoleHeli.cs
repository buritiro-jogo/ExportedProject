using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBRoleHeli
{
	private static GameObject g;

	private static List<(Transform t, Quaternion b, bool main)> rotors = new List<(Transform, Quaternion, bool)>();

	private static (Vector3 a, Vector3 b, float alt)[] routes;

	private static int ri;

	private static float t = -1f;

	private static float wait;

	private static float len;

	private static float ang;

	private static Vector3 dir;

	public static void Spawn()
	{
		GameObject gameObject = FBRegistry.I.Vehicle("heli");
		if (!gameObject)
		{
			return;
		}
		g = Object.Instantiate(gameObject);
		g.name = "Heli_Passagem";
		Collider[] componentsInChildren = g.GetComponentsInChildren<Collider>();
		for (int j = 0; j < componentsInChildren.Length; j++)
		{
			Object.Destroy(componentsInChildren[j]);
		}
		rotors.Clear();
		Transform[] componentsInChildren2 = g.GetComponentsInChildren<Transform>();
		foreach (Transform transform in componentsInChildren2)
		{
			switch (transform.name)
			{
			case "Helices":
			case "Rotor":
				rotors.Add((transform, transform.localRotation, true));
				break;
			case "Helices-02":
			case "Rotor-02":
				rotors.Add((transform, transform.localRotation, false));
				break;
			}
		}
		Bounds box = new Bounds(Vector3.zero, new Vector3(300f, 30f, 300f));
		bool flag = false;
		MeshRenderer[] array = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
		foreach (MeshRenderer meshRenderer in array)
		{
			if (meshRenderer.gameObject.layer == 8)
			{
				if (!flag)
				{
					box = meshRenderer.bounds;
					flag = true;
				}
				else
				{
					box.Encapsulate(meshRenderer.bounds);
				}
			}
		}
		Vector3 c0 = box.center;
		float R = Mathf.Max(box.size.x, box.size.z) / 2f + 160f;
		routes = new float[4] { 0.4f, 2.1f, 3.7f, 5.2f }.Select(delegate(float a, int i)
		{
			Vector3 vector = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
			Vector3 vector2 = new Vector3(vector.z, 0f, 0f - vector.x) * (new float[4] { -30f, 25f, 0f, 45f })[i];
			return (c0 + vector2 - vector * R, c0 + vector2 + vector * R, box.max.y + 45f + (float)(i * 6));
		}).ToArray();
		ri = Random.Range(0, 4);
		t = -1f;
		wait = Random.Range(25f, 50f);
		g.transform.position = c0 + Vector3.down * 400f;
	}

	public static void Tick(float dt)
	{
		if (!g)
		{
			return;
		}
		if (t < 0f)
		{
			if ((wait -= dt) > 0f)
			{
				return;
			}
			t = 0f;
			ri = (ri + 1) % routes.Length;
			(Vector3, Vector3, float) tuple = routes[ri];
			len = Vector3.Distance(tuple.Item1, tuple.Item2);
			dir = (tuple.Item2 - tuple.Item1).normalized;
		}
		(Vector3, Vector3, float) tuple2 = routes[ri];
		t += dt * 26f;
		if (t > len)
		{
			t = -1f;
			wait = Random.Range(70f, 150f);
			g.transform.position += Vector3.down * 400f;
			FBHeliSound component = g.GetComponent<FBHeliSound>();
			if ((bool)component)
			{
				component.vol = 0f;
			}
			return;
		}
		Vector3 position = tuple2.Item1 + dir * t;
		position.y = tuple2.Item3 + Mathf.Sin(t * 0.02f) * 2f;
		g.transform.SetPositionAndRotation(position, Quaternion.LookRotation(dir) * Quaternion.Euler(5.7f, 0f, 0f));
		ang += dt * 1500f;
		foreach (var (transform, quaternion, flag) in rotors)
		{
			if ((bool)transform)
			{
				transform.localRotation = quaternion * Quaternion.AngleAxis(flag ? ang : (ang * 1.6f), flag ? Vector3.up : Vector3.right);
			}
		}
		FBHeliSound.Attach(g, 0.95f);
	}
}
}
