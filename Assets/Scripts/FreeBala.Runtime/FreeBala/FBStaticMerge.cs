using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace FreeBala
{
public static class FBStaticMerge
{
	private class Cell
	{
		public readonly List<Renderer> src = new List<Renderer>();

		public readonly List<GameObject> merged = new List<GameObject>();

		public bool broken;
	}

	private static readonly Regex GROUP = new Regex("(_Postes_.*_p\\d+$|^VJ_ArCond)", RegexOptions.IgnoreCase);

	private const float CELL = 48f;

	private static readonly Dictionary<FBPole, Cell> poleCell = new Dictionary<FBPole, Cell>();

	public static int Sources;

	public static int Merged;

	public static int Unreadable;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbsemjuncao") < 0)
		{
			SceneManager.sceneLoaded -= OnScene;
			SceneManager.sceneLoaded += OnScene;
			OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
		}
	}

	private static void OnScene(Scene s, LoadSceneMode m)
	{
		if (m == LoadSceneMode.Single)
		{
			poleCell.Clear();
		}
		GameObject[] rootGameObjects = s.GetRootGameObjects();
		foreach (GameObject gameObject in rootGameObjects)
		{
			if (gameObject.name.StartsWith("Map_"))
			{
				Run(gameObject);
			}
		}
	}

	private static string Group(Transform t)
	{
		int num = 0;
		while ((bool)t && num < 3)
		{
			Match match = GROUP.Match(t.name);
			if (match.Success)
			{
				if (!match.Value.StartsWith("_"))
				{
					return "arcond";
				}
				return "postes";
			}
			num++;
			t = t.parent;
		}
		return null;
	}

	private static void Run(GameObject root)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		Dictionary<(string, int, int, int, ShadowCastingMode), (Cell, Dictionary<Material, List<CombineInstance>>)> dictionary = new Dictionary<(string, int, int, int, ShadowCastingMode), (Cell, Dictionary<Material, List<CombineInstance>>)>();
		int num = 0;
		int num2 = 0;
		HashSet<string> hashSet = new HashSet<string>();
		MeshRenderer[] componentsInChildren = root.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
		foreach (MeshRenderer meshRenderer in componentsInChildren)
		{
			if (!meshRenderer.gameObject.activeInHierarchy || meshRenderer.forceRenderingOff)
			{
				continue;
			}
			string text = Group(meshRenderer.transform);
			if (text == null)
			{
				continue;
			}
			MeshFilter component = meshRenderer.GetComponent<MeshFilter>();
			Mesh mesh = (component ? component.sharedMesh : null);
			if (!mesh)
			{
				continue;
			}
			if (!mesh.isReadable)
			{
				num2++;
				if (hashSet.Count < 4)
				{
					hashSet.Add(text + ":" + meshRenderer.name);
				}
				continue;
			}
			Vector3 center = meshRenderer.bounds.center;
			(string, int, int, int, ShadowCastingMode) key = (text, Mathf.FloorToInt(center.x / 48f), Mathf.FloorToInt(center.z / 48f), meshRenderer.gameObject.layer, meshRenderer.shadowCastingMode);
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = (new Cell(), new Dictionary<Material, List<CombineInstance>>()));
			}
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			Matrix4x4 localToWorldMatrix = meshRenderer.transform.localToWorldMatrix;
			for (int j = 0; j < mesh.subMeshCount && j < sharedMaterials.Length; j++)
			{
				if ((bool)sharedMaterials[j] && mesh.GetIndexCount(j) != 0)
				{
					if (!value.Item2.TryGetValue(sharedMaterials[j], out var value2))
					{
						value2 = (value.Item2[sharedMaterials[j]] = new List<CombineInstance>());
					}
					value2.Add(new CombineInstance
					{
						mesh = mesh,
						subMeshIndex = j,
						transform = localToWorldMatrix
					});
				}
			}
			value.Item1.src.Add(meshRenderer);
			num++;
			FBPole componentInParent = meshRenderer.GetComponentInParent<FBPole>();
			if ((bool)componentInParent)
			{
				poleCell[componentInParent] = value.Item1;
			}
		}
		int num3 = 0;
		foreach (KeyValuePair<(string, int, int, int, ShadowCastingMode), (Cell, Dictionary<Material, List<CombineInstance>>)> item in dictionary)
		{
			var (cell, dictionary2) = item.Value;
			if (cell.src.Count < 2)
			{
				(from p in poleCell
					where p.Value == cell
					select p.Key).ToList().ForEach(delegate(FBPole p)
				{
					poleCell.Remove(p);
				});
				continue;
			}
			foreach (KeyValuePair<Material, List<CombineInstance>> item2 in dictionary2)
			{
				Mesh mesh2 = new Mesh();
				mesh2.name = $"juncao_{item.Key.Item1}_{item.Key.Item2}_{item.Key.Item3}_{item2.Key.name}";
				Mesh mesh3 = mesh2;
				if (((IEnumerable<CombineInstance>)item2.Value).Sum((Func<CombineInstance, long>)((CombineInstance ci) => ci.mesh.vertexCount)) > 65000)
				{
					mesh3.indexFormat = IndexFormat.UInt32;
				}
				mesh3.CombineMeshes(item2.Value.ToArray(), mergeSubMeshes: true, useMatrices: true);
				mesh3.RecalculateBounds();
				mesh3.UploadMeshData(markNoLongerReadable: true);
				GameObject gameObject = new GameObject(mesh3.name)
				{
					layer = item.Key.Item4
				};
				gameObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
				gameObject.transform.SetParent(root.transform, worldPositionStays: true);
				gameObject.AddComponent<MeshFilter>().sharedMesh = mesh3;
				MeshRenderer meshRenderer2 = gameObject.AddComponent<MeshRenderer>();
				meshRenderer2.sharedMaterial = item2.Key;
				meshRenderer2.shadowCastingMode = item.Key.Item5;
				meshRenderer2.receiveShadows = cell.src[0].receiveShadows;
				meshRenderer2.lightProbeUsage = LightProbeUsage.Off;
				cell.merged.Add(gameObject);
				num3++;
			}
			foreach (Renderer item3 in cell.src)
			{
				item3.forceRenderingOff = true;
			}
		}
		Sources += num;
		Merged += num3;
		Unreadable += num2;
		Debug.Log(string.Format("[FreeBala][juncao] {0}: {1} peças -> {2} malhas por célula de {3:0} m ({4} células · não legíveis {5}: {6}) em {7} ms", root.name, num, num3, 48f, dictionary.Count, num2, string.Join(", ", hashSet), stopwatch.ElapsedMilliseconds));
	}

	public static void Break(FBPole p)
	{
		if (!p || !poleCell.TryGetValue(p, out var value) || value.broken)
		{
			return;
		}
		value.broken = true;
		foreach (GameObject item in value.merged)
		{
			if ((bool)item)
			{
				UnityEngine.Object.Destroy(item);
			}
		}
		foreach (Renderer item2 in value.src)
		{
			if ((bool)item2)
			{
				item2.forceRenderingOff = false;
			}
		}
	}
}
}
