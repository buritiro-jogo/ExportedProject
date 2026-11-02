using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBTune
{
	private const string KEY = "fb_tune_v1_";

	private static Texture2D glow;

	public static FBTuneData Load(string id)
	{
		string @string = PlayerPrefs.GetString("fb_tune_v1_" + id, "");
		if (string.IsNullOrEmpty(@string))
		{
			return null;
		}
		try
		{
			return JsonUtility.FromJson<FBTuneData>(@string);
		}
		catch
		{
			return null;
		}
	}

	public static void Save(string id, FBTuneData T)
	{
		PlayerPrefs.SetString("fb_tune_v1_" + id, JsonUtility.ToJson(T));
		PlayerPrefs.Save();
	}

	private static Material Own(FBCar car, Material m, string kind)
	{
		if (car.ownMats == null)
		{
			car.ownMats = new Dictionary<string, Material>();
		}
		string key = kind + "|" + m.name;
		if (car.ownMats.TryGetValue(key, out var value) && (bool)value)
		{
			return value;
		}
		value = new Material(m)
		{
			name = m.name
		};
		car.ownMats[key] = value;
		return value;
	}

	public static void Apply(FBCar car, FBTuneData T)
	{
		if (!car || car.ie || !car.shell || T == null)
		{
			return;
		}
		_ = car.def;
		FBReb.Apply(car, T);
		if (T.pneu >= 0)
		{
			float[] array = FBReb.Pneus(car.id);
			T.wsize = array[Mathf.Clamp(T.pneu, 0, array.Length - 1)];
		}
		Renderer[] componentsInChildren = car.shell.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if ((bool)car.soundWallNode && renderer.transform.IsChildOf(car.soundWallNode))
			{
				continue;
			}
			Material[] sharedMaterials = renderer.sharedMaterials;
			bool flag = false;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if ((bool)sharedMaterials[j] && (sharedMaterials[j].name == "veh_paint" || sharedMaterials[j].name == "veh_glass"))
				{
					sharedMaterials[j] = Own(car, sharedMaterials[j], sharedMaterials[j].name);
					flag = true;
				}
			}
			if (flag)
			{
				renderer.sharedMaterials = sharedMaterials;
			}
		}
		Texture2D texture2D = ((!string.IsNullOrEmpty(T.skin)) ? Resources.Load<Texture2D>("CarSkins/" + T.skin) : null);
		if (car.ownMats != null)
		{
			foreach (KeyValuePair<string, Material> ownMat in car.ownMats)
			{
				Material value = ownMat.Value;
				if ((bool)value)
				{
					if (ownMat.Key.StartsWith("veh_paint"))
					{
						value.SetTexture("_BaseMap", texture2D);
						value.SetColor("_BaseColor", texture2D ? Color.white : (T.hasPaint ? T.paint : car.paint));
						value.SetFloat("_Metallic", 0.05f + T.metal * 0.6f);
						value.SetFloat("_Smoothness", 0.3f + T.brilho * 0.65f);
					}
					else if (ownMat.Key.StartsWith("veh_glass"))
					{
						Color value2 = (T.hasVidro ? T.vidro : new Color(0.043f, 0.055f, 0.07f));
						value2.a = T.fume;
						value.SetColor("_BaseColor", value2);
					}
				}
			}
		}
		string text = ((!string.IsNullOrEmpty(T.wheel) && GameData.Wheels.ContainsKey(T.wheel)) ? T.wheel : car.wid);
		if (text != car.curWid)
		{
			GameObject gameObject = ((text != null) ? FBRegistry.I.Wheel(text) : null);
			GameObject gameObject2 = (gameObject ? gameObject : (car.wheelTpl ? car.wheelTpl.gameObject : null));
			if ((bool)gameObject2)
			{
				foreach (FBWheel wheel in car.wheels)
				{
					GameObject gameObject3 = Object.Instantiate(gameObject2, wheel.obj.parent, worldPositionStays: false);
					gameObject3.SetActive(value: true);
					gameObject3.name = wheel.obj.name;
					Transform[] componentsInChildren2 = gameObject3.GetComponentsInChildren<Transform>(includeInactive: true);
					for (int i = 0; i < componentsInChildren2.Length; i++)
					{
						componentsInChildren2[i].gameObject.layer = 11;
					}
					Object.Destroy(wheel.obj.gameObject);
					wheel.obj = gameObject3.transform;
				}
			}
			car.curWid = text;
			car.wsrc = gameObject;
		}
		float num = (((bool)car.wsrc && car.curWid != null) ? Json.F(Json.O(GameData.Wheels, car.curWid), "r") : 0f);
		foreach (FBWheel wheel2 in car.wheels)
		{
			float num2 = (wheel2.front ? T.foff : T.roff);
			float num3 = (wheel2.front ? T.fcam : T.rcam);
			float num4 = Mathf.Sign((wheel2.webP.x == 0f) ? 1f : wheel2.webP.x);
			wheel2.obj.localPosition = new Vector3(0f - (wheel2.webP.x + num4 * num2), wheel2.webP.y, wheel2.webP.z);
			Quaternion quaternion = Quaternion.Euler(0f, (wheel2.webP.x < 0f) ? 180 : 0, 0f);
			wheel2.obj.localScale = Vector3.one * (((num > 0f) ? (wheel2.rr / num) : 1f) * T.wsize);
			wheel2.r = wheel2.rr * 1.12f * T.wsize;
			wheel2.bas = Quaternion.AngleAxis(num3 * num4, Vector3.forward) * quaternion;
			wheel2.obj.localRotation = wheel2.bas;
			componentsInChildren = wheel2.obj.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer2 in componentsInChildren)
			{
				Material[] sharedMaterials2 = renderer2.sharedMaterials;
				for (int k = 0; k < sharedMaterials2.Length; k++)
				{
					if ((bool)sharedMaterials2[k] && !Regex.IsMatch(sharedMaterials2[k].name.Replace("rim_own|", ""), "pneu|tire|tyre|borracha", RegexOptions.IgnoreCase) && T.hasRodaCor)
					{
						Material material = Own(car, sharedMaterials2[k], "rim");
						material.SetColor("_BaseColor", T.rodaCor);
						sharedMaterials2[k] = material;
					}
				}
				renderer2.sharedMaterials = sharedMaterials2;
			}
		}
		float hf = T.fh;
		float hr = T.rh;
		car.shell.localPosition = new Vector3(0f, car.shellY0 + (hf + hr) / 2f, 0f);
		car.shell.localRotation = Quaternion.Euler((0f - Mathf.Atan2(hf - hr, car.wbL)) * 57.29578f, 0f, 0f);
		car.shellP0 = car.shell.localPosition;
		car.shellR0 = car.shell.localRotation;
		car.seat = car.seat0 + Vector3.up * At(car.seat0.z);
		if (car.rigHands0 != null)
		{
			car.rigHands = Adj(car.rigHands0);
		}
		if (car.rigPoles0 != null)
		{
			car.rigPoles = Adj(car.rigPoles0);
		}
		if (car.rigFeet0 != null)
		{
			car.rigFeet = Adj(car.rigFeet0);
		}
		Vector3? rigWheel = car.rigWheel0;
		if (rigWheel.HasValue)
		{
			Vector3 valueOrDefault = rigWheel.GetValueOrDefault();
			car.rigWheel = valueOrDefault + Vector3.up * At(valueOrDefault.z);
		}
		car.vol = null;
		car.volTried = false;
		if ((bool)car.driver)
		{
			car.driver.fitCar = null;
		}
		Neon(car, T);
		Farol(car, T);
		Placa(car, T);
		car.engineProf = ((!string.IsNullOrEmpty(T.ronco) && Json.O(Json.O(GameData.RES, "profiles"), T.ronco) != null) ? T.ronco : FBCarSound.ProfileFor(car.id, car));
		car.turbo = T.turbo;
		car.tracao = (string.IsNullOrEmpty(T.tracao) ? FBReb.Tracao(car.id) : T.tracao);
		if (T.somSet)
		{
			BuildSom(car, T);
		}
		Vector3[] Adj(Vector3[] a)
		{
			return a?.Select((Vector3 p) => p + Vector3.up * At(p.z)).ToArray();
		}
		float At(float z)
		{
			return hr + (hf - hr) * Mathf.Clamp01((z - car.zb) / Mathf.Max(0.1f, car.wbL));
		}
	}

	private static Texture2D Glow()
	{
		if ((bool)glow)
		{
			return glow;
		}
		glow = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = new Vector2((float)j - 31.5f, (float)i - 31.5f).magnitude / 32f;
				float value = ((num < 0.17f) ? 1f : ((num < 1f) ? Mathf.Lerp(0.55f, 0f, (num - 0.35f) / 0.65f) : 0f));
				if (num >= 0.17f && num < 0.35f)
				{
					value = Mathf.Lerp(1f, 0.55f, (num - 0.17f) / 0.18f);
				}
				glow.SetPixel(j, i, new Color(1f, 1f, 1f, Mathf.Clamp01(value)));
			}
		}
		glow.Apply();
		return glow;
	}

	private static Material GlowMat(Color c)
	{
		Material material = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive: true);
		material.SetTexture("_BaseMap", Glow());
		material.mainTexture = Glow();
		material.SetColor("_BaseColor", c);
		material.SetFloat("_Cull", 0f);
		return material;
	}

	private static void Neon(FBCar car, FBTuneData T)
	{
		Transform transform = car.transform.Find("neon");
		if (!T.neon)
		{
			if ((bool)transform)
			{
				Object.Destroy(transform.gameObject);
			}
			return;
		}
		if (!transform)
		{
			List<float> source = car.wheels.Select((FBWheel w) => w.webP.z).DefaultIfEmpty(0f).ToList();
			List<float> source2 = car.wheels.Select((FBWheel w) => Mathf.Abs(w.webP.x)).DefaultIfEmpty(0.7f).ToList();
			float y = (source.Max() - source.Min()) * 1.55f + 1f;
			float x = source2.Max() * 2f * 1.7f + 0.6f;
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			Object.Destroy(gameObject.GetComponent<Collider>());
			gameObject.name = "neon";
			gameObject.transform.SetParent(car.transform, worldPositionStays: false);
			gameObject.transform.localPosition = new Vector3(0f, 0.035f, (source.Max() + source.Min()) / 2f);
			gameObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			gameObject.transform.localScale = new Vector3(x, y, 1f);
			gameObject.GetComponent<Renderer>().sharedMaterial = GlowMat(T.neonC);
			transform = gameObject.transform;
		}
		transform.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", T.neonC);
	}

	private static void Farol(FBCar car, FBTuneData T)
	{
		Transform transform = car.shell.Find("farol");
		if ((bool)transform)
		{
			Object.Destroy(transform.gameObject);
		}
		car.headTint = (T.hasFarol ? new Color?(T.farol) : null);
	}

	private static void Placa(FBCar car, FBTuneData T)
	{
		Transform transform = car.shell.Find("placa_txt");
		if (string.IsNullOrEmpty(T.placa))
		{
			if ((bool)transform)
			{
				Object.Destroy(transform.gameObject);
			}
			FBPlates.SetText(car.shell, FBPlates.Of(car.paint));
			return;
		}
		if (FBPlates.SetText(car.shell, T.placa.ToUpperInvariant().Substring(0, Mathf.Min(7, T.placa.Length))))
		{
			if ((bool)transform)
			{
				Object.Destroy(transform.gameObject);
			}
			return;
		}
		Dictionary<string, object> o = Json.O(car.def, "lights");
		List<object> list = Json.L(o, "BrakeLight") ?? Json.L(o, "ReverseLight");
		if (list != null && list.Count != 0)
		{
			float y = list.Average((object p) => Json.Fv((p as List<object>)[1])) - 0.26f;
			float z = list.Min((object p) => Json.Fv((p as List<object>)[2])) - 0.015f;
			if (!transform)
			{
				Transform transform2 = new GameObject("placa_txt").transform;
				transform2.SetParent(car.shell, worldPositionStays: false);
				transform2.localPosition = new Vector3(0f, y, z);
				transform2.localRotation = Quaternion.Euler(0f, 180f, 0f);
				GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
				Object.Destroy(gameObject.GetComponent<Collider>());
				gameObject.transform.SetParent(transform2, worldPositionStays: false);
				gameObject.transform.localScale = new Vector3(0.4f, 0.13f, 1f);
				gameObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
				Material material = new Material(FBShaders.Unlit);
				material.SetColor("_BaseColor", new Color(0.96f, 0.96f, 0.94f));
				gameObject.GetComponent<Renderer>().sharedMaterial = material;
				GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
				Object.Destroy(gameObject2.GetComponent<Collider>());
				gameObject2.transform.SetParent(transform2, worldPositionStays: false);
				gameObject2.transform.localScale = new Vector3(0.4f, 0.03f, 1f);
				gameObject2.transform.localPosition = new Vector3(0f, 0.05f, -0.001f);
				gameObject2.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
				Material material2 = new Material(FBShaders.Unlit);
				material2.SetColor("_BaseColor", new Color(0.11f, 0.25f, 0.6f));
				gameObject2.GetComponent<Renderer>().sharedMaterial = material2;
				GameObject gameObject3 = new GameObject("txt");
				gameObject3.transform.SetParent(transform2, worldPositionStays: false);
				gameObject3.transform.localPosition = new Vector3(0f, -0.012f, -0.002f);
				gameObject3.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
				TextMesh textMesh = gameObject3.AddComponent<TextMesh>();
				textMesh.anchor = TextAnchor.MiddleCenter;
				textMesh.alignment = TextAlignment.Center;
				textMesh.fontSize = 64;
				textMesh.characterSize = 0.0105f;
				textMesh.font = FBUI.Font;
				gameObject3.GetComponent<MeshRenderer>().sharedMaterial = FBUI.Font.material;
				transform = transform2;
			}
			TextMesh componentInChildren = transform.GetComponentInChildren<TextMesh>();
			componentInChildren.text = T.placa.ToUpperInvariant().Substring(0, Mathf.Min(7, T.placa.Length));
			componentInChildren.color = new Color(0.07f, 0.07f, 0.07f);
		}
	}

	public static (Vector3 p, float w, bool bed)? SomAnchor(Dictionary<string, object> d)
	{
		Dictionary<string, object> o = Json.O(d, "som");
		Dictionary<string, object> dictionary = Json.O(o, "bed");
		List<object> list = Json.L(o, "base");
		List<object> list2 = Json.L(Json.O(d, "doors"), "mala");
		if (dictionary != null)
		{
			return (new Vector3(0f, Json.F(dictionary, "floor") + 0.01f, Json.F(dictionary, "cab") - 0.04f), Json.F(dictionary, "w"), true);
		}
		if (list != null)
		{
			return (new Vector3(0f - Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2])), 1.1f, false);
		}
		if (list2 != null)
		{
			return (new Vector3(0f, Json.Fv(list2[1]) + 0.12f, Json.Fv(list2[2]) + 0.4f), 1.1f, false);
		}
		return null;
	}

	public static void BuildSom(FBCar car, FBTuneData T)
	{
		if ((bool)car.soundWallNode)
		{
			Object.Destroy(car.soundWallNode.gameObject);
		}
		car.soundWallNode = null;
		(Vector3, float, bool)? tuple = SomAnchor(car.def);
		List<FBSomSlot> list = T.som.Where((FBSomSlot s) => s != null && (bool)FBRegistry.I.Som(s.m)).ToList();
		FBParedao.Wall wall = FBParedao.Get(car);
		wall.leds.Clear();
		wall.vus.Clear();
		wall.spk.Clear();
		if (!tuple.HasValue || list.Count == 0)
		{
			return;
		}
		Transform transform = new GameObject("sound_wall").transform;
		transform.SetParent(car.shell, worldPositionStays: false);
		transform.localPosition = tuple.Value.Item1;
		transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
		foreach (FBSomSlot item in list)
		{
			GameObject gameObject = FBParedao.Module(item.m, wall);
			if ((bool)gameObject)
			{
				gameObject.transform.SetParent(transform, worldPositionStays: false);
				gameObject.transform.localPosition = new Vector3(0f - item.p.x, item.p.y, item.p.z);
				gameObject.transform.localRotation = Quaternion.Euler(0f, 0f - item.r, 0f);
				gameObject.transform.localScale = Vector3.one * item.s;
			}
		}
		Transform[] componentsInChildren = transform.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].gameObject.layer = 11;
		}
		car.soundWallNode = transform;
		SomColors(car, T);
		FBParedao.Rebuilt(car);
	}

	public static void SomColors(FBCar car, FBTuneData T)
	{
		if (!car.soundWallNode)
		{
			return;
		}
		Renderer[] componentsInChildren = car.soundWallNode.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if (renderer.name == "led_fita" || renderer.name == "led" || renderer.name == "vu" || ((bool)renderer.transform.parent && (renderer.transform.parent.name == "led" || renderer.transform.parent.name == "vu")))
			{
				continue;
			}
			bool flag = Regex.IsMatch((renderer.transform.parent ? renderer.transform.parent.name : "") + "|" + renderer.name, "(^|\\|)spk\\d");
			Material[] sharedMaterials = renderer.sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if ((bool)sharedMaterials[j])
				{
					string text = (sharedMaterials[j].name.StartsWith("Som_Carcaca") ? "caixa" : (flag ? "falante" : "corneta"));
					if ((text == "caixa") ? T.hasCaixa : ((text == "falante") ? T.hasFalante : T.hasCorneta))
					{
						Color value = ((text == "caixa") ? (T.caixa * 0.85f) : ((text == "falante") ? T.falante : T.corneta));
						value.a = 1f;
						Material material = Own(car, sharedMaterials[j], "som_" + text);
						material.SetColor("_BaseColor", value);
						sharedMaterials[j] = material;
					}
				}
			}
			renderer.sharedMaterials = sharedMaterials;
		}
	}
}
}
