using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FreeBala
{
[CreateAssetMenu(menuName = "FreeBala/Registro")]
public class FBRegistry : ScriptableObject
{
	public const string RChars = "FBPrefabs/Chars/";

	public const string RImp = "FBImported/";

	public List<string> charIds = new List<string>();

	public List<string> weaponIds = new List<string>();

	public List<string> vehicleIds = new List<string>();

	public List<string> somIds = new List<string>();

	public List<string> wheelIds = new List<string>();

	public List<string> intIds = new List<string>();

	public List<string> accIds = new List<string>();

	public List<string> partIds = new List<string>();

	public List<AnimationClip> clips = new List<AnimationClip>();

	public List<Mesh> lodSrc = new List<Mesh>();

	public List<Mesh> lodDst = new List<Mesh>();

	private Dictionary<Mesh, Mesh> lodMap;

	public RuntimeAnimatorController mainController;

	public RuntimeAnimatorController pedestrianController;

	public GameObject fpArms;

	public AnimationClip birdClip;

	private static FBRegistry inst;

	private Dictionary<string, AnimationClip> k;

	private readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

	private readonly Dictionary<List<string>, HashSet<string>> sets = new Dictionary<List<string>, HashSet<string>>();

	public static FBRegistry I
	{
		get
		{
			if (!inst)
			{
				inst = Resources.Load<FBRegistry>("FBRegistry");
			}
			return inst;
		}
	}

	public Mesh LodOf(Mesh m)
	{
		if (!m)
		{
			return m;
		}
		if (lodMap == null)
		{
			lodMap = new Dictionary<Mesh, Mesh>();
			for (int i = 0; i < lodSrc.Count && i < lodDst.Count; i++)
			{
				if ((bool)lodSrc[i] && (bool)lodDst[i])
				{
					lodMap[lodSrc[i]] = lodDst[i];
				}
			}
		}
		if (!lodMap.TryGetValue(m, out var value))
		{
			return m;
		}
		return value;
	}

	private bool Has(List<string> l, string id)
	{
		if (id == null)
		{
			return false;
		}
		if (!sets.TryGetValue(l, out var value))
		{
			value = (sets[l] = new HashSet<string>(l));
		}
		return value.Contains(id);
	}

	private GameObject Load(List<string> l, string dir, string id)
	{
		if (!Has(l, id))
		{
			return null;
		}
		string text = dir + id;
		if (!cache.TryGetValue(text, out var value) || !value)
		{
			value = Resources.Load<GameObject>(text);
			cache[text] = value;
			if (!value)
			{
				Debug.LogWarning("[FreeBala] não carregou " + text);
			}
		}
		return value;
	}

	public GameObject Char(string id)
	{
		return Load(charIds, "FBPrefabs/Chars/", id);
	}

	public GameObject Weapon(string id)
	{
		return Load(weaponIds, "FBImported/Weapons/", id);
	}

	public GameObject Vehicle(string id)
	{
		return Load(vehicleIds, "FBImported/Vehicles/", id);
	}

	public GameObject Som(string id)
	{
		return Load(somIds, "FBImported/Som/", id);
	}

	public GameObject Wheel(string id)
	{
		return Load(wheelIds, "FBImported/Wheels/", id);
	}

	public GameObject Interior(string id)
	{
		return Load(intIds, "FBImported/Int/", id);
	}

	public GameObject Acc(string id)
	{
		return Load(accIds, "FBImported/Acc/", id);
	}

	public GameObject Parts(string id)
	{
		return Load(partIds, "FBImported/Parts/", id);
	}

	public bool HasChar(string id)
	{
		return Has(charIds, id);
	}

	public AnimationClip Clip(string id)
	{
		if (id == null)
		{
			return null;
		}
		if (k == null)
		{
			k = new Dictionary<string, AnimationClip>();
			foreach (AnimationClip clip in clips)
			{
				if ((bool)clip && !k.ContainsKey(clip.name))
				{
					k[clip.name] = clip;
				}
			}
		}
		if (k.TryGetValue(id, out var value) || k.TryGetValue(Safe(id), out value))
		{
			return value;
		}
		return null;
	}

	public static string Safe(string n)
	{
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			n = n.Replace(oldChar, '_');
		}
		return n.Replace('|', '_').Replace(':', '_');
	}
}
}
