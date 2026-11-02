using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBDistanceCull : MonoBehaviour
{
	private class Item
	{
		public Renderer[] on;

		public Renderer[] off;

		public Bounds b;

		public float d2;

		public bool state = true;
	}

	private Item[] items;

	private float t;

	private int i0;

	public static float SmallDist => FBQuality.Mobile ? 90 : 180;

	public static float MidDist => FBQuality.Mobile ? 180 : 360;

	public static float PoleDist => FBQuality.Mobile ? 110 : 180;

	public static float NearDist => FBQuality.Mobile ? 90 : 120;

	public static float InteriorDist => FBQuality.Mobile ? 40 : 60;

	public static float MatoDist => FBQuality.Mobile ? 90 : 150;

	public static int Count { get; private set; }

	public static int Hidden { get; private set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		SceneManager.sceneLoaded -= OnScene;
		SceneManager.sceneLoaded += OnScene;
		OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
	}

	private static void OnScene(Scene s, LoadSceneMode m)
	{
		if (!s.IsValid() || !s.name.StartsWith("Map_"))
		{
			return;
		}
		GameObject[] rootGameObjects = s.GetRootGameObjects();
		foreach (GameObject gameObject in rootGameObjects)
		{
			if (gameObject.name.StartsWith("Map_") && !gameObject.GetComponent<FBDistanceCull>())
			{
				gameObject.AddComponent<FBDistanceCull>();
			}
		}
	}

	private static bool IsPole(Transform t)
	{
		while ((bool)t)
		{
			string text = t.name;
			if (text.Contains("Postes") || text.Contains("Fios") || text.StartsWith("VJ_Poste") || text.Contains("poste_br"))
			{
				return true;
			}
			t = t.parent;
		}
		return false;
	}

	private static string Tag(Transform t, out string kind)
	{
		while ((bool)t)
		{
			string text = t.name;
			int length;
			if ((length = text.IndexOf("_PERTO", StringComparison.Ordinal)) > 0)
			{
				kind = "perto";
				return text.Substring(0, length);
			}
			if ((length = text.IndexOf("_LONGE", StringComparison.Ordinal)) > 0)
			{
				kind = "longe";
				return text.Substring(0, length);
			}
			if (text.EndsWith("_interior"))
			{
				kind = "interior";
				return text;
			}
			if (text.StartsWith("VJ11_Mato"))
			{
				kind = "mato";
				return text;
			}
			t = t.parent;
		}
		kind = null;
		return null;
	}

	private void Start()
	{
		List<Item> list = new List<Item>();
		Dictionary<string, (List<Renderer>, List<Renderer>, string)> dictionary = new Dictionary<string, (List<Renderer>, List<Renderer>, string)>();
		Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer || (bool)renderer.GetComponentInParent<LODGroup>())
			{
				continue;
			}
			string kind;
			string text = Tag(renderer.transform, out kind);
			if (text != null)
			{
				if (!dictionary.TryGetValue(text, out var value))
				{
					value = (dictionary[text] = (new List<Renderer>(), new List<Renderer>(), (kind == "longe") ? "perto" : kind));
				}
				List<Renderer> list2;
				if (!(kind == "longe"))
				{
					(list2, _, _) = value;
				}
				else
				{
					list2 = value.Item2;
				}
				list2.Add(renderer);
				continue;
			}
			float magnitude = renderer.bounds.extents.magnitude;
			bool flag = IsPole(renderer.transform);
			if ((!flag || !renderer.sharedMaterials.Any((Material m) => (bool)m && m.IsKeywordEnabled("_EMISSION"))) && (!(magnitude > 6f) || flag))
			{
				float num = (flag ? PoleDist : ((magnitude <= 2.5f) ? SmallDist : MidDist));
				list.Add(new Item
				{
					on = new Renderer[1] { renderer },
					off = new Renderer[0],
					b = renderer.bounds,
					d2 = num * num
				});
			}
		}
		foreach (var value2 in dictionary.Values)
		{
			List<Renderer> list3 = value2.Item1.Concat(value2.Item2).ToList();
			Bounds bounds = list3[0].bounds;
			foreach (Renderer item2 in list3)
			{
				bounds.Encapsulate(item2.bounds);
			}
			float num2 = ((value2.Item3 == "interior") ? InteriorDist : ((value2.Item3 == "mato") ? MatoDist : NearDist));
			Item item = new Item
			{
				on = value2.Item1.ToArray(),
				off = value2.Item2.ToArray(),
				b = bounds,
				d2 = num2 * num2
			};
			componentsInChildren = item.off;
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].forceRenderingOff = true;
			}
			list.Add(item);
		}
		items = list.ToArray();
		Count = items.Length;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBDistanceCull.Update");

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
		if (items == null || items.Length == 0)
		{
			return;
		}
		Camera main = Camera.main;
		if (!main)
		{
			return;
		}
		Vector3 position = main.transform.position;
		int num = Mathf.Max(1, items.Length / 6);
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			Item item = items[(i0 + i) % items.Length];
			float num3 = item.b.SqrDistance(position);
			bool flag = (item.state ? (num3 < item.d2 * 1.21f) : (num3 < item.d2));
			if (flag != item.state)
			{
				Apply(item, flag);
			}
		}
		i0 = (i0 + num) % items.Length;
		if (!((t += Time.unscaledDeltaTime) > 1f))
		{
			return;
		}
		t = 0f;
		Item[] array = items;
		for (int j = 0; j < array.Length; j++)
		{
			if (!array[j].state)
			{
				num2++;
			}
		}
		Hidden = num2;
	}

	private static void Apply(Item it, bool near)
	{
		it.state = near;
		Renderer[] on = it.on;
		foreach (Renderer renderer in on)
		{
			if ((bool)renderer)
			{
				renderer.forceRenderingOff = !near;
			}
		}
		on = it.off;
		foreach (Renderer renderer2 in on)
		{
			if ((bool)renderer2)
			{
				renderer2.forceRenderingOff = near;
			}
		}
	}

	public static void Refresh()
	{
		FBDistanceCull[] array = UnityEngine.Object.FindObjectsByType<FBDistanceCull>(FindObjectsSortMode.None);
		for (int i = 0; i < array.Length; i++)
		{
			array[i].RefreshAll();
		}
	}

	private void RefreshAll()
	{
		Camera main = Camera.main;
		if (!main || items == null)
		{
			return;
		}
		Vector3 position = main.transform.position;
		Item[] array = items;
		foreach (Item item in array)
		{
			bool flag = item.b.SqrDistance(position) < item.d2;
			if (flag != item.state)
			{
				Apply(item, flag);
			}
		}
	}

	private void OnDestroy()
	{
		if (items == null)
		{
			return;
		}
		Item[] array = items;
		foreach (Item item in array)
		{
			Renderer[] on = item.on;
			foreach (Renderer renderer in on)
			{
				if ((bool)renderer)
				{
					renderer.forceRenderingOff = false;
				}
			}
			on = item.off;
			foreach (Renderer renderer2 in on)
			{
				if ((bool)renderer2)
				{
					renderer2.forceRenderingOff = false;
				}
			}
		}
	}
}
}
