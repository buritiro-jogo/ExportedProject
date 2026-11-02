using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FreeBala
{
public class FBWardrobe : MonoBehaviour
{
	public class Part
	{
		public string node;

		public string cat;

		public string name;

		public bool on;

		public List<string> covers = new List<string>();

		[NonSerialized]
		public Transform tr;
	}

	public class Look
	{
		public HashSet<int> on = new HashSet<int>();

		public int tone = -1;

		public Dictionary<string, int> tint = new Dictionary<string, int>();

		public Look Clone()
		{
			return new Look
			{
				on = new HashSet<int>(on),
				tone = tone,
				tint = new Dictionary<string, int>(tint)
			};
		}

		public int Tint(string cat)
		{
			if (!tint.TryGetValue(cat, out var value))
			{
				return 0;
			}
			return value;
		}
	}

	private enum Slot
	{
		None,
		Skin,
		Hair,
		Cloth
	}

	private class MaskDef
	{
		public string id;

		public string tex;

		public Color skin;

		public Texture2D mask;
	}

	private struct MatSlot
	{
		public Renderer r;

		public int idx;

		public Slot slot;
	}

	[TextArea]
	public string sheetJson;

	public static readonly Color[] TONES = new Color[6]
	{
		new Color(1.1f, 1.04f, 1f),
		new Color(0.96f, 0.84f, 0.74f),
		new Color(0.8f, 0.62f, 0.5f),
		new Color(0.66f, 0.48f, 0.37f),
		new Color(0.5f, 0.35f, 0.27f),
		new Color(0.36f, 0.25f, 0.2f)
	};

	public static readonly string[] TONE_NAMES = new string[6] { "Clara", "Média", "Morena", "Parda", "Escura", "Retinta" };

	public static readonly Color?[] TINTS = new Color?[8]
	{
		null,
		new Color(0.08f, 0.08f, 0.09f),
		new Color(0.92f, 0.92f, 0.9f),
		new Color(0.62f, 0.1f, 0.1f),
		new Color(0.12f, 0.25f, 0.55f),
		new Color(0.18f, 0.32f, 0.16f),
		new Color(0.55f, 0.45f, 0.3f),
		new Color(0.45f, 0.45f, 0.47f)
	};

	public static readonly string[] TINT_NAMES = new string[8] { "Original", "Preto", "Branco", "Vermelho", "Azul", "Verde", "Caqui", "Cinza" };

	public static readonly Color?[] HAIR = new Color?[8]
	{
		null,
		new Color(0.12f, 0.1f, 0.09f),
		new Color(0.32f, 0.2f, 0.13f),
		new Color(0.55f, 0.36f, 0.2f),
		new Color(1.25f, 1.05f, 0.7f),
		new Color(0.85f, 0.3f, 0.12f),
		new Color(0.75f, 0.74f, 0.72f),
		new Color(1.5f, 1.45f, 1.35f)
	};

	public static readonly string[] HAIR_NAMES = new string[8] { "Original", "Preto", "Castanho escuro", "Castanho", "Loiro", "Ruivo", "Grisalho", "Platinado" };

	public static readonly string[] EXCL = new string[4] { "top", "bottom", "shoes", "head" };

	public static readonly string[] TINTABLE = new string[5] { "top", "vest", "bottom", "hat", "shoes" };

	private List<Part> parts;

	private List<(string key, string name)> cats;

	public string current;

	public Look look;

	public string skinId;

	private readonly Dictionary<Renderer, Material[]> inst = new Dictionary<Renderer, Material[]>();

	private readonly Dictionary<Material, Color> baseCol = new Dictionary<Material, Color>();

	private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

	private static readonly int GltfColor = Shader.PropertyToID("baseColorFactor");

	private static readonly int LegacyColor = Shader.PropertyToID("_Color");

	public static readonly Color[] TONE_SKIN = new Color[6]
	{
		new Color(0.86f, 0.7f, 0.6f),
		new Color(0.84f, 0.66f, 0.53f),
		new Color(0.72f, 0.53f, 0.4f),
		new Color(0.6f, 0.42f, 0.31f),
		new Color(0.43f, 0.29f, 0.21f),
		new Color(0.3f, 0.2f, 0.15f)
	};

	private static Dictionary<string, MaskDef> maskIdx;

	private static readonly Dictionary<(Texture, int), Texture2D> toned = new Dictionary<(Texture, int), Texture2D>();

	private Renderer maskR;

	private int maskMat = -1;

	private Texture maskOrig;

	private static readonly int[] TexProps = new int[3]
	{
		Shader.PropertyToID("_BaseMap"),
		Shader.PropertyToID("baseColorTexture"),
		Shader.PropertyToID("_MainTex")
	};

	private List<MatSlot> slots;

	private int nSkin;

	private int nHair;

	private int nCloth;

	public List<Part> Parts
	{
		get
		{
			Parse();
			return parts ?? new List<Part>();
		}
	}

	public List<(string key, string name)> Cats
	{
		get
		{
			Parse();
			return cats ?? new List<(string, string)>();
		}
	}

	public bool HasSheet
	{
		get
		{
			Parse();
			if (parts != null)
			{
				return parts.Count > 0;
			}
			return false;
		}
	}

	public bool HasSkinMats
	{
		get
		{
			ScanMats();
			return nSkin > 0;
		}
	}

	public bool HasHairMats
	{
		get
		{
			ScanMats();
			return nHair > 0;
		}
	}

	public bool HasClothMats
	{
		get
		{
			ScanMats();
			if (nCloth > 0)
			{
				return nSkin > 0;
			}
			return false;
		}
	}

	public bool Customizable
	{
		get
		{
			if (!HasSheet && !HasSkinMats && !HasHairMats)
			{
				return HasSkinMask;
			}
			return true;
		}
	}

	public bool HasSkinMask
	{
		get
		{
			ScanMats();
			if (!HasSheet && nSkin == 0)
			{
				return MaskInfo(SkinId) != null;
			}
			return false;
		}
	}

	private string SkinId
	{
		get
		{
			if (!string.IsNullOrEmpty(skinId))
			{
				return skinId;
			}
			FBCharacter component = GetComponent<FBCharacter>();
			if (!component || string.IsNullOrEmpty(component.skinId))
			{
				return base.name.Replace("(Clone)", "").Trim();
			}
			return component.skinId;
		}
	}

	private void Awake()
	{
		if (string.IsNullOrEmpty(skinId) && base.name.EndsWith("(Clone)"))
		{
			skinId = base.name.Replace("(Clone)", "").Trim();
		}
	}

	private void Parse()
	{
		if (parts != null || string.IsNullOrEmpty(sheetJson))
		{
			return;
		}
		parts = new List<Part>();
		cats = new List<(string, string)>();
		object o = Json.Parse(sheetJson);
		foreach (object item in Json.L(o, "parts") ?? new List<object>())
		{
			Part part = new Part
			{
				node = Json.S(item, "node"),
				cat = Json.S(item, "cat"),
				name = Json.S(item, "name"),
				on = Json.B(item, "on")
			};
			List<object> list = Json.L(item, "covers");
			if (list != null)
			{
				foreach (object item2 in list)
				{
					part.covers.Add(item2 as string);
				}
			}
			parts.Add(part);
		}
		foreach (object item3 in Json.L(o, "cats") ?? new List<object>())
		{
			if (item3 is List<object> { Count: >1 } list2)
			{
				cats.Add((list2[0] as string, list2[1] as string));
			}
		}
		Dictionary<string, Transform> dictionary = new Dictionary<string, Transform>();
		Transform[] componentsInChildren = GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			dictionary[transform.name] = transform;
		}
		foreach (Part part2 in parts)
		{
			if (part2.node != null && dictionary.TryGetValue(part2.node, out var value))
			{
				part2.tr = value;
			}
		}
	}

	public Look Default()
	{
		Look look = new Look();
		List<Part> list = Parts;
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].on && list[i].cat != "body")
			{
				look.on.Add(i);
			}
		}
		return look;
	}

	public Look Decode(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			return Default();
		}
		List<Part> list = Parts;
		Look look = new Look();
		string[] array = code.Split('.');
		string text = array[0];
		for (int i = 0; i < text.Length; i++)
		{
			int num;
			try
			{
				num = Convert.ToInt32(text[i].ToString(), 16);
			}
			catch
			{
				num = 0;
			}
			for (int j = 0; j < 4; j++)
			{
				if ((num & (1 << j)) != 0)
				{
					int num2 = i * 4 + j;
					if (num2 < list.Count && list[num2].cat != "body")
					{
						look.on.Add(num2);
					}
				}
			}
		}
		if (array.Length > 1 && int.TryParse(array[1], out var result))
		{
			look.tone = Mathf.Clamp(result, -1, TONES.Length - 1);
		}
		if (array.Length > 2)
		{
			string[] array2 = array[2].Split(',');
			foreach (string text2 in array2)
			{
				if (text2.Length > 1 && int.TryParse(text2.Substring(text2.Length - 1), out var result2) && result2 >= 0 && result2 < 8)
				{
					look.tint[text2.Substring(0, text2.Length - 1)] = result2;
				}
			}
		}
		return look;
	}

	public string Encode(Look L)
	{
		List<Part> list = Parts;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < list.Count; i += 4)
		{
			int num = 0;
			for (int j = 0; j < 4; j++)
			{
				if (L.on.Contains(i + j))
				{
					num |= 1 << j;
				}
			}
			stringBuilder.Append(num.ToString("x"));
		}
		stringBuilder.Append('.').Append(L.tone);
		List<string> list2 = new List<string>();
		foreach (KeyValuePair<string, int> item in L.tint)
		{
			if (item.Value > 0)
			{
				list2.Add(item.Key + item.Value);
			}
		}
		list2.Sort();
		if (list2.Count > 0)
		{
			stringBuilder.Append('.').Append(string.Join(",", list2));
		}
		return stringBuilder.ToString();
	}

	public bool IsDefault(Look L)
	{
		Look look = Default();
		if (L.tone < 0 && L.on.SetEquals(look.on))
		{
			return !HasTint(L);
		}
		return false;
	}

	private static bool HasTint(Look L)
	{
		foreach (KeyValuePair<string, int> item in L.tint)
		{
			if (item.Value > 0)
			{
				return true;
			}
		}
		return false;
	}

	public Look RandomLook(Func<float> rnd = null)
	{
		if (rnd == null)
		{
			rnd = () => UnityEngine.Random.value;
		}
		Look look = Default();
		List<Part> P = Parts;
		Dictionary<string, List<int>> dictionary = new Dictionary<string, List<int>>();
		for (int j = 0; j < P.Count; j++)
		{
			if (!(P[j].cat == "body"))
			{
				if (!dictionary.ContainsKey(P[j].cat))
				{
					dictionary[P[j].cat] = new List<int>();
				}
				dictionary[P[j].cat].Add(j);
			}
		}
		foreach (KeyValuePair<string, List<int>> item2 in dictionary)
		{
			List<int> list = item2.Value.FindAll((int i) => !P[i].on);
			if (list.Count == 0 || rnd() < 0.5f)
			{
				continue;
			}
			if (Array.IndexOf(EXCL, item2.Key) >= 0)
			{
				foreach (int item3 in item2.Value)
				{
					look.on.Remove(item3);
				}
			}
			look.on.Add(list[Mathf.Min(list.Count - 1, (int)(rnd() * (float)list.Count))]);
		}
		if (rnd() < 0.6f && (HasSheet || HasSkinMats))
		{
			look.tone = Mathf.Min(TONES.Length - 1, (int)(rnd() * (float)TONES.Length));
		}
		if (HasHairMats && rnd() < 0.5f)
		{
			look.tint["hair"] = 1 + Mathf.Min(HAIR.Length - 2, (int)(rnd() * (float)(HAIR.Length - 1)));
		}
		return look;
	}

	public void Toggle(Look L, int i)
	{
		List<Part> list = Parts;
		if (i < 0 || i >= list.Count || list[i].cat == "body")
		{
			return;
		}
		if (L.on.Contains(i))
		{
			L.on.Remove(i);
			return;
		}
		if (Array.IndexOf(EXCL, list[i].cat) >= 0)
		{
			for (int j = 0; j < list.Count; j++)
			{
				if (list[j].cat == list[i].cat)
				{
					L.on.Remove(j);
				}
			}
		}
		L.on.Add(i);
	}

	public void ClearCat(Look L, string cat)
	{
		List<Part> list = Parts;
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].cat == cat)
			{
				L.on.Remove(i);
			}
		}
	}

	public void Apply(string code, bool random = false)
	{
		Look l = (random ? RandomLook() : Decode(code));
		ApplyLook(l);
	}

	public void ApplyLook(Look L)
	{
		look = L;
		current = Encode(L);
		List<Part> list = Parts;
		bool[] array = new bool[list.Count];
		for (int j = 0; j < list.Count; j++)
		{
			array[j] = list[j].cat == "body" || L.on.Contains(j);
		}
		for (int k = 0; k < list.Count; k++)
		{
			if (!(list[k].cat != "body") || !array[k])
			{
				continue;
			}
			foreach (string i in list[k].covers)
			{
				int num = list.FindIndex((Part q) => q.node == i);
				if (num >= 0)
				{
					array[num] = false;
				}
			}
		}
		bool flag = false;
		bool flag2 = false;
		for (int l = 0; l < list.Count; l++)
		{
			if (array[l] && list[l].cat == "body")
			{
				flag = true;
			}
			if (list[l].cat == "head")
			{
				flag2 = true;
			}
		}
		if (!flag && !flag2)
		{
			int num2 = list.FindIndex((Part q) => q.cat == "body");
			if (num2 >= 0)
			{
				array[num2] = true;
			}
		}
		Color? color = ((L.tone >= 0 && L.tone < TONES.Length) ? new Color?(TONES[L.tone]) : null);
		for (int m = 0; m < list.Count; m++)
		{
			Transform tr = list[m].tr;
			if ((bool)tr)
			{
				if (tr.gameObject.activeSelf != array[m])
				{
					tr.gameObject.SetActive(array[m]);
				}
				Color? obj;
				if (!(list[m].cat == "body") && !(list[m].cat == "head"))
				{
					int num3 = L.Tint(list[m].cat);
					obj = ((num3 > 0 && num3 < TINTS.Length) ? TINTS[num3] : null);
				}
				else
				{
					obj = color;
				}
				Color? col = obj;
				Renderer[] componentsInChildren = tr.GetComponentsInChildren<Renderer>(includeInactive: true);
				foreach (Renderer r in componentsInChildren)
				{
					Paint(r, -1, col);
				}
			}
		}
		ScanMats();
		int num4 = L.Tint("hair");
		int num5 = L.Tint("roupa");
		foreach (MatSlot slot in slots)
		{
			if (slot.slot == Slot.Hair)
			{
				Paint(slot.r, slot.idx, (num4 > 0 && num4 < HAIR.Length) ? HAIR[num4] : null);
			}
			else if (!HasSheet && slot.slot == Slot.Skin)
			{
				Paint(slot.r, slot.idx, color);
			}
			else if (!HasSheet && slot.slot == Slot.Cloth && nSkin > 0)
			{
				Paint(slot.r, slot.idx, (num5 > 0 && num5 < TINTS.Length) ? TINTS[num5] : null);
			}
		}
		if (HasSkinMask)
		{
			MaskTone(L.tone);
		}
	}

	private static int ColorProp(Material m)
	{
		if (!m.HasProperty(BaseColor))
		{
			if (!m.HasProperty(GltfColor))
			{
				if (!m.HasProperty(LegacyColor))
				{
					return -1;
				}
				return LegacyColor;
			}
			return GltfColor;
		}
		return BaseColor;
	}

	private void Paint(Renderer r, int idx, Color? col)
	{
		if (!r)
		{
			return;
		}
		if (!inst.TryGetValue(r, out var value))
		{
			if (!col.HasValue)
			{
				return;
			}
			value = r.materials;
			inst[r] = value;
			Material[] array = value;
			foreach (Material material in array)
			{
				int num = (material ? ColorProp(material) : (-1));
				if (num >= 0)
				{
					baseCol[material] = material.GetColor(num);
				}
			}
		}
		for (int j = 0; j < value.Length; j++)
		{
			if (idx < 0 || j == idx)
			{
				Material material2 = value[j];
				if ((bool)material2 && baseCol.TryGetValue(material2, out var value2))
				{
					Color value3 = (col.HasValue ? new Color(value2.r * col.Value.r, value2.g * col.Value.g, value2.b * col.Value.b, value2.a) : value2);
					material2.SetColor(ColorProp(material2), value3);
				}
			}
		}
	}

	private void OnDestroy()
	{
		foreach (KeyValuePair<Renderer, Material[]> item in inst)
		{
			Material[] value = item.Value;
			foreach (Material material in value)
			{
				if ((bool)material)
				{
					UnityEngine.Object.Destroy(material);
				}
			}
		}
	}

	private static MaskDef MaskInfo(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		if (maskIdx == null)
		{
			maskIdx = new Dictionary<string, MaskDef>();
			TextAsset textAsset = Resources.Load<TextAsset>("UI/Personagem/Mascaras/index");
			if ((bool)textAsset && Json.Parse(textAsset.text) is Dictionary<string, object> dictionary)
			{
				foreach (KeyValuePair<string, object> item in dictionary)
				{
					List<object> list = Json.L(item.Value, "skin");
					if (list != null && list.Count >= 3)
					{
						maskIdx[item.Key] = new MaskDef
						{
							id = item.Key,
							tex = Json.S(item.Value, "tex"),
							skin = new Color(Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2]))
						};
					}
				}
			}
		}
		if (!maskIdx.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	private void MaskTone(int tone)
	{
		MaskDef maskDef = MaskInfo(SkinId);
		if (maskDef == null)
		{
			return;
		}
		if (!maskR)
		{
			SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
			{
				Material[] sharedMaterials = skinnedMeshRenderer.sharedMaterials;
				for (int j = 0; j < sharedMaterials.Length; j++)
				{
					Texture texture = MainTex(sharedMaterials[j]);
					if ((bool)texture && texture.name == maskDef.tex)
					{
						maskR = skinnedMeshRenderer;
						maskMat = j;
						maskOrig = texture;
						break;
					}
				}
				if ((bool)maskR)
				{
					break;
				}
			}
			if (!maskR)
			{
				return;
			}
		}
		if (tone < 0 && !inst.ContainsKey(maskR))
		{
			return;
		}
		if (!inst.TryGetValue(maskR, out var value))
		{
			value = maskR.materials;
			inst[maskR] = value;
			Material[] array = value;
			foreach (Material material in array)
			{
				int num = (material ? ColorProp(material) : (-1));
				if (num >= 0)
				{
					baseCol[material] = material.GetColor(num);
				}
			}
		}
		Material material2 = value[maskMat];
		Texture value2 = maskOrig;
		if (tone >= 0 && tone < TONE_SKIN.Length)
		{
			Texture2D texture2D = Toned(maskOrig, maskDef, tone);
			if ((bool)texture2D)
			{
				value2 = texture2D;
			}
		}
		int[] texProps = TexProps;
		foreach (int nameID in texProps)
		{
			if (material2.HasProperty(nameID))
			{
				material2.SetTexture(nameID, value2);
			}
		}
	}

	private static Texture MainTex(Material m)
	{
		if (!m)
		{
			return null;
		}
		int[] texProps = TexProps;
		foreach (int nameID in texProps)
		{
			if (m.HasProperty(nameID) && (bool)m.GetTexture(nameID))
			{
				return m.GetTexture(nameID);
			}
		}
		return null;
	}

	private static Texture2D Toned(Texture src, MaskDef md, int tone)
	{
		(Texture, int) key = (src, tone);
		if (toned.TryGetValue(key, out var value) && (bool)value)
		{
			return value;
		}
		if (!md.mask)
		{
			TextAsset textAsset = Resources.Load<TextAsset>("UI/Personagem/Mascaras/" + md.id);
			if (!textAsset)
			{
				return null;
			}
			md.mask = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			if (!md.mask.LoadImage(textAsset.bytes, markNonReadable: false))
			{
				return null;
			}
		}
		int num = Mathf.Min(1024, src.width);
		int num2 = Mathf.Min(1024, src.height);
		RenderTexture temporary = RenderTexture.GetTemporary(num, num2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
		RenderTexture active = RenderTexture.active;
		Graphics.Blit(src, temporary);
		RenderTexture.active = temporary;
		Texture2D texture2D = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false);
		texture2D.ReadPixels(new Rect(0f, 0f, num, num2), 0, 0);
		RenderTexture.active = active;
		RenderTexture.ReleaseTemporary(temporary);
		Color32[] pixels = texture2D.GetPixels32();
		UnityEngine.Object.Destroy(texture2D);
		Texture2D mask = md.mask;
		int width = mask.width;
		int height = mask.height;
		Color32[] pixels2 = mask.GetPixels32();
		Color color = TONE_SKIN[tone];
		byte[] array = Lut(color.r, md.skin.r);
		byte[] array2 = Lut(color.g, md.skin.g);
		byte[] array3 = Lut(color.b, md.skin.b);
		for (int i = 0; i < num2; i++)
		{
			float num3 = ((float)i + 0.5f) / (float)num2 * (float)height - 0.5f;
			int num4 = Mathf.Clamp((int)num3, 0, height - 1);
			int num5 = Mathf.Min(height - 1, num4 + 1);
			float t2 = Mathf.Clamp01(num3 - (float)num4);
			int num6 = num4 * width;
			int num7 = num5 * width;
			for (int j = 0; j < num; j++)
			{
				float num8 = ((float)j + 0.5f) / (float)num * (float)width - 0.5f;
				int num9 = Mathf.Clamp((int)num8, 0, width - 1);
				int num10 = Mathf.Min(width - 1, num9 + 1);
				float t3 = Mathf.Clamp01(num8 - (float)num9);
				float num11 = Mathf.Lerp(Mathf.Lerp((int)pixels2[num6 + num9].r, (int)pixels2[num6 + num10].r, t3), Mathf.Lerp((int)pixels2[num7 + num9].r, (int)pixels2[num7 + num10].r, t3), t2) / 255f;
				if (!(num11 < 0.01f))
				{
					ref Color32 reference = ref pixels[i * num + j];
					reference.r = (byte)((float)(int)reference.r + (float)(array[reference.r] - reference.r) * num11);
					reference.g = (byte)((float)(int)reference.g + (float)(array2[reference.g] - reference.g) * num11);
					reference.b = (byte)((float)(int)reference.b + (float)(array3[reference.b] - reference.b) * num11);
				}
			}
		}
		Texture2D texture2D2 = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: true)
		{
			name = src.name + "_tom" + tone,
			wrapMode = src.wrapMode,
			filterMode = FilterMode.Bilinear,
			anisoLevel = 2
		};
		texture2D2.SetPixels32(pixels);
		texture2D2.Apply(updateMipmaps: true, makeNoLongerReadable: false);
		if (!Application.isMobilePlatform && SystemInfo.SupportsTextureFormat(TextureFormat.DXT5))
		{
			texture2D2.Compress(highQuality: false);
		}
		texture2D2.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		toned[key] = texture2D2;
		return texture2D2;
		static byte[] Lut(float t, float sk)
		{
			float p = Mathf.Clamp(Mathf.Log(t) / Mathf.Log(Mathf.Clamp(sk, 0.03f, 0.97f)), 0.3f, 3.5f);
			byte[] array4 = new byte[256];
			for (int k = 0; k < 256; k++)
			{
				array4[k] = (byte)Mathf.RoundToInt(255f * Mathf.Pow((float)k / 255f, p));
			}
			return array4;
		}
	}

	private static Slot Classify(string n)
	{
		n = (n ?? "").ToLowerInvariant().Replace(" (instance)", "");
		if (n.Length == 0)
		{
			return Slot.None;
		}
		if (n.Contains("eye") || n.Contains("teeth") || n.Contains("tongue") || n.Contains("cornea") || n.Contains("occlusion") || n.Contains("tearline"))
		{
			return Slot.None;
		}
		if (n.Contains("hair") || n.Contains("scalp") || n.Contains("beard") || n.Contains("bushy") || n.Contains("brow") || n.Contains("mustache") || n.Contains("moustache"))
		{
			return Slot.Hair;
		}
		if (n.Contains("skin") || n.Contains("nails"))
		{
			return Slot.Skin;
		}
		return Slot.Cloth;
	}

	private void ScanMats()
	{
		if (slots != null)
		{
			return;
		}
		slots = new List<MatSlot>();
		Parse();
		HashSet<Renderer> hashSet = new HashSet<Renderer>();
		Renderer[] componentsInChildren;
		if (parts != null)
		{
			foreach (Part part in parts)
			{
				if ((bool)part.tr)
				{
					componentsInChildren = part.tr.GetComponentsInChildren<Renderer>(includeInactive: true);
					foreach (Renderer item in componentsInChildren)
					{
						hashSet.Add(item);
					}
				}
			}
		}
		componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if (!(renderer is SkinnedMeshRenderer))
			{
				continue;
			}
			Material[] sharedMaterials = renderer.sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				Slot slot = Classify(sharedMaterials[j] ? sharedMaterials[j].name : null);
				if (slot != 0 && (!hashSet.Contains(renderer) || slot == Slot.Hair))
				{
					slots.Add(new MatSlot
					{
						r = renderer,
						idx = j,
						slot = slot
					});
					switch (slot)
					{
					case Slot.Skin:
						nSkin++;
						break;
					case Slot.Hair:
						nHair++;
						break;
					default:
						nCloth++;
						break;
					}
				}
			}
		}
	}

	public static FBWardrobe For(GameObject go, string id = null)
	{
		if (!go)
		{
			return null;
		}
		FBWardrobe fBWardrobe = go.GetComponent<FBWardrobe>();
		if (!fBWardrobe)
		{
			fBWardrobe = go.AddComponent<FBWardrobe>();
		}
		if (id != null)
		{
			fBWardrobe.skinId = id;
		}
		return fBWardrobe;
	}

	public static void ApplyTo(GameObject go, string code, bool random = false)
	{
		if (!go)
		{
			return;
		}
		FBWardrobe fBWardrobe = go.GetComponent<FBWardrobe>();
		if (!fBWardrobe)
		{
			if (!random && string.IsNullOrEmpty(code))
			{
				return;
			}
			fBWardrobe = go.AddComponent<FBWardrobe>();
			if (!fBWardrobe.Customizable)
			{
				return;
			}
		}
		fBWardrobe.Apply(code, random);
		go.GetComponent<FBCharacter>()?.HolsterRemeasure();
	}
}
}
