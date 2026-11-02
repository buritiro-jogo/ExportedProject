using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
[DisallowMultipleComponent]
public class FBLampHead : MonoBehaviour
{
	public static readonly List<FBLampHead> All = new List<FBLampHead>();

	[NonSerialized]
	public int lamp = -1;

	private Renderer r;

	private static MaterialPropertyBlock mpb;

	private static readonly int idE = Shader.PropertyToID("_EmissionColor");

	private static readonly int idB = Shader.PropertyToID("_BaseColor");

	private static readonly Dictionary<int, List<(Renderer r, int sub)>> fallback = new Dictionary<int, List<(Renderer, int)>>();

	private static readonly Regex HEAD = new Regex("(^|_)(lamp|lampada|luminaria)(_on|_sodio|_sodium)?$|street_?light", RegexOptions.IgnoreCase);

	public Vector3 Center
	{
		get
		{
			if (!r)
			{
				return base.transform.position;
			}
			return r.bounds.center;
		}
	}

	private void OnEnable()
	{
		if (!All.Contains(this))
		{
			All.Add(this);
		}
		r = GetComponent<Renderer>();
	}

	private void OnDisable()
	{
		All.Remove(this);
	}

	public void SetLit(bool on)
	{
		Apply(r, on, -1);
	}

	public static void Apply(Renderer rd, bool on, int onlySub)
	{
		if (!rd)
		{
			return;
		}
		if (mpb == null)
		{
			mpb = new MaterialPropertyBlock();
		}
		Material[] sharedMaterials = rd.sharedMaterials;
		for (int i = 0; i < sharedMaterials.Length; i++)
		{
			if (onlySub >= 0 && i != onlySub)
			{
				continue;
			}
			Material material = sharedMaterials[i];
			if (!material)
			{
				continue;
			}
			if (on)
			{
				rd.SetPropertyBlock(null, i);
				continue;
			}
			mpb.Clear();
			if (material.HasProperty(idE))
			{
				mpb.SetColor(idE, Color.black);
			}
			if (material.HasProperty(idB))
			{
				Color color = material.GetColor(idB);
				mpb.SetColor(idB, new Color(color.r * 0.22f, color.g * 0.22f, color.b * 0.24f, color.a));
			}
			rd.SetPropertyBlock(mpb, i);
		}
	}

	public static void Bind(IReadOnlyList<FBStreetLights.Lamp> lamps)
	{
		foreach (FBLampHead item in All)
		{
			if (!item)
			{
				continue;
			}
			item.lamp = -1;
			float num = 4.84f;
			Vector3 center = item.Center;
			for (int i = 0; i < lamps.Count; i++)
			{
				float sqrMagnitude = (lamps[i].p - center).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					item.lamp = i;
				}
			}
		}
	}

	public static void SetLamp(int i, bool on, Vector3 p, IReadOnlyList<FBStreetLights.Lamp> lamps)
	{
		bool flag = false;
		foreach (FBLampHead item in All)
		{
			if ((bool)item && item.lamp == i)
			{
				item.SetLit(on);
				flag = true;
			}
		}
		if (flag)
		{
			return;
		}
		if (!fallback.TryGetValue(i, out List<(Renderer, int)> value))
		{
			value = (fallback[i] = FindFallback(p, lamps));
		}
		foreach (var (rd, onlySub) in value)
		{
			Apply(rd, on, onlySub);
		}
	}

	public static void ResetScene()
	{
		fallback.Clear();
	}

	private static List<(Renderer, int)> FindFallback(Vector3 p, IReadOnlyList<FBStreetLights.Lamp> lamps)
	{
		List<(Renderer, int)> list = new List<(Renderer, int)>();
		Collider[] array = Physics.OverlapSphere(p, 1.2f, 256, QueryTriggerInteraction.Ignore);
		foreach (Collider collider in array)
		{
			Renderer renderer = collider.GetComponent<Renderer>().OrNull() ?? collider.GetComponentInParent<Renderer>();
			if (!renderer)
			{
				continue;
			}
			Bounds bounds = renderer.bounds;
			int num = 0;
			foreach (FBStreetLights.Lamp lamp in lamps)
			{
				if (bounds.SqrDistance(lamp.p) < 1.44f)
				{
					num++;
				}
			}
			if (num > 1)
			{
				continue;
			}
			Material[] sharedMaterials = renderer.sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if ((bool)sharedMaterials[j] && HEAD.IsMatch(sharedMaterials[j].name.Replace(" (Instance)", "")))
				{
					list.Add((renderer, j));
				}
			}
		}
		if (list.Count == 0)
		{
			MeshRenderer[] array2 = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
			foreach (MeshRenderer meshRenderer in array2)
			{
				if (meshRenderer.gameObject.layer != 8 || meshRenderer.bounds.SqrDistance(p) > 1.44f)
				{
					continue;
				}
				int num2 = 0;
				foreach (FBStreetLights.Lamp lamp2 in lamps)
				{
					if (meshRenderer.bounds.SqrDistance(lamp2.p) < 1.44f)
					{
						num2++;
					}
				}
				if (num2 > 1)
				{
					continue;
				}
				Material[] sharedMaterials2 = meshRenderer.sharedMaterials;
				for (int k = 0; k < sharedMaterials2.Length; k++)
				{
					if ((bool)sharedMaterials2[k] && HEAD.IsMatch(sharedMaterials2[k].name.Replace(" (Instance)", "")))
					{
						list.Add((meshRenderer, k));
					}
				}
			}
		}
		return list;
	}
}
}
