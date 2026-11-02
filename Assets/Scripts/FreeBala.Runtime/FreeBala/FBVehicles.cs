using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBVehicles
{
	public const float SCALE = 1.12f;

	public const float RIDE = 0.1f;

	public const float SEAT_HIP = 0.3f;

	public static readonly List<FBCar> list = new List<FBCar>();

	private static readonly Dictionary<FBCharacter, FBCar> driving = new Dictionary<FBCharacter, FBCar>();

	public static bool fp;

	private static readonly (string id, float w)[] CIVIL = new(string, float)[38]
	{
		("GolG2", 5f),
		("GolQuadrado", 5f),
		("UnoMille", 5f),
		("Celta", 4f),
		("Fusca", 3f),
		("Chevett", 3f),
		("Palio2010", 4f),
		("CorsaClassic", 3f),
		("GolG5", 4f),
		("HB20", 4f),
		("Up", 3f),
		("UnoWay", 3f),
		("SaveiroG4", 4.5f),
		("SaveiroQuadrada", 4.5f),
		("Santana2002", 2f),
		("OmegaCD1997", 1.5f),
		("OpalaDiplomata85", 1.5f),
		("Kombi", 2f),
		("Montana", 3f),
		("Fiorino", 2f),
		("CrossFox", 2f),
		("ParatiSurf1994", 2f),
		("Clio2003", 3f),
		("Kadett", 3f),
		("S10_2017", 2.5f),
		("Hilux2023Conquest", 1.5f),
		("Toro", 2.5f),
		("Amarok", 2f),
		("Corolla2018", 1.5f),
		("CivicLXR_2014", 1.5f),
		("Jetta2012", 1f),
		("L200", 1.5f),
		("F250", 1.5f),
		("Sw4", 1f),
		("Golf", 1.5f),
		("i30", 1f),
		("SaveiroCross", 3f),
		("RAM3500", 1f)
	};

	private static readonly uint[] PAINTS = new uint[12]
	{
		13224909u, 15658730u, 1579291u, 9312018u, 1916794u, 5593438u, 13220239u, 3037754u, 14266908u, 10988205u,
		15921902u, 7150874u
	};

	public static readonly string[] BIKES = new string[5] { "pop100", "motorizada", "xj6", "falcon400", "start" };

	private static readonly Dictionary<string, (float s, float seatY)> IE_FIX = new Dictionary<string, (float, float)>
	{
		["hilux"] = (1.16f, 0.42f),
		["caveirao"] = (1.32f, 0.3f),
		["pop100"] = (1.2f, 0f),
		["motorizada"] = (1.2f, 0f),
		["xj6"] = (1.2f, 0f),
		["falcon400"] = (1.2f, 0f),
		["start"] = (1.2f, 0f)
	};

	private static readonly Dictionary<string, string[]> IE_WHEELS = new Dictionary<string, string[]>
	{
		["moto"] = new string[2] { "RodaDianteira", "RodaTraseira" },
		["car"] = new string[4] { "FL", "FR", "RL", "RR" }
	};

	private static readonly Dictionary<string, string[]> IE_STEER = new Dictionary<string, string[]>
	{
		["moto"] = new string[1] { "SteeringMain" },
		["car"] = new string[0]
	};

	private static readonly Dictionary<string, Material> MATS = new Dictionary<string, Material>();

	private static readonly Dictionary<string, (float stock, float low)> PORT_RIDE = new Dictionary<string, (float, float)>
	{
		["ViaturaPMAzul"] = (-0.16f, -0.19f),
		["ViaturaBM"] = (-0.16f, -0.19f),
		["Clio2003"] = (-0.11f, -0.14f)
	};

	private static Material shadowMat;

	private static readonly string[] VIATURAS = new string[2] { "ViaturaPMAzul", "ViaturaBM" };

	public static readonly List<string> ParkLog = new List<string>();

	public static int ParkMoved;

	public static int ParkDropped;

	public static int ParkOk;

	public static float SettleErr;

	public static bool Allowed => AllowedIn(FBSession.mode);

	public static bool Driving(FBCharacter c)
	{
		if ((bool)c)
		{
			return driving.ContainsKey(c);
		}
		return false;
	}

	public static FBCar CarOf(FBCharacter c)
	{
		if (!c || !driving.TryGetValue(c, out var value))
		{
			return null;
		}
		if (!value)
		{
			driving.Remove(c);
			return null;
		}
		return value;
	}

	internal static void SetDriving(FBCharacter c, FBCar car)
	{
		if ((bool)c)
		{
			if ((bool)car)
			{
				driving[c] = car;
			}
			else
			{
				driving.Remove(c);
			}
		}
	}

	private static uint HashSeed(string v)
	{
		uint num = 2166136261u;
		foreach (char c in v)
		{
			num = (num ^ c) * 16777619;
		}
		return num;
	}

	public static Func<float> Seeded(uint seed)
	{
		return delegate
		{
			seed += 1831565813u;
			uint num = seed;
			num = (num ^ (num >> 15)) * (num | 1);
			num ^= num + (num ^ (num >> 7)) * (num | 0x3D);
			return (float)(num ^ (num >> 14)) / 4.2949673E+09f;
		};
	}

	private static string F(float x)
	{
		return x.ToString(CultureInfo.InvariantCulture);
	}

	private static Vector3 W2U(List<object> p, float dy = 0f)
	{
		if (p != null)
		{
			return new Vector3(0f - Json.Fv(p[0]), Json.Fv(p[1]) + dy, Json.Fv(p[2]));
		}
		return Vector3.zero;
	}

	private static Color Hex(uint c)
	{
		return new Color((float)((c >> 16) & 0xFFu) / 255f, (float)((c >> 8) & 0xFFu) / 255f, (float)(c & 0xFFu) / 255f);
	}

	private static Material GameMat(Material m, Color paint)
	{
		if (!m)
		{
			return m;
		}
		string text = m.name.Replace(" (Instance)", "");
		string text2 = (text.StartsWith("carpaint") ? "paint" : (text.StartsWith("carglass") ? "glass" : (text.StartsWith("carmetal") ? "metal" : null)));
		if (text2 == null)
		{
			return m;
		}
		string key = ((text2 == "paint") ? ("p|" + text + "|" + ColorUtility.ToHtmlStringRGB(paint)) : (text2 + "|" + text));
		if (MATS.TryGetValue(key, out var value) && (bool)value)
		{
			return value;
		}
		value = new Material(FBShaders.Lit)
		{
			name = "veh_" + text2
		};
		if (text2 == "paint")
		{
			value.SetColor("_BaseColor", paint);
			value.SetFloat("_Smoothness", 0.72f);
			value.SetFloat("_Metallic", 0.22f);
		}
		else if (text2 == "glass")
		{
			value.SetColor("_BaseColor", new Color(0.043f, 0.055f, 0.07f, 0.62f));
			value.SetFloat("_Smoothness", 0.95f);
			value.SetFloat("_Metallic", 0.1f);
			value = FBShaders.Transparent(value);
		}
		else
		{
			Color value2 = (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : Color.gray));
			value.SetColor("_BaseColor", value2);
			value.SetFloat("_Smoothness", 0.7f);
			value.SetFloat("_Metallic", 0.85f);
		}
		MATS[key] = value;
		return value;
	}

	private static void Dress(GameObject o, Color paint)
	{
		Renderer[] componentsInChildren = o.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			Material[] sharedMaterials = renderer.sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				sharedMaterials[j] = GameMat(sharedMaterials[j], paint);
			}
			renderer.sharedMaterials = sharedMaterials;
			renderer.shadowCastingMode = ShadowCastingMode.On;
			renderer.receiveShadows = true;
		}
	}

	public static void DressParts(GameObject o, FBCar car)
	{
		Material material = null;
		Renderer[] componentsInChildren;
		if ((bool)car.shell)
		{
			componentsInChildren = car.shell.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (renderer.transform.IsChildOf(o.transform))
				{
					continue;
				}
				Material[] sharedMaterials = renderer.sharedMaterials;
				foreach (Material material2 in sharedMaterials)
				{
					if ((bool)material2 && material2.name.StartsWith("atlas_"))
					{
						material = material2;
						break;
					}
				}
				if ((bool)material)
				{
					break;
				}
			}
		}
		componentsInChildren = o.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer2 in componentsInChildren)
		{
			Material[] sharedMaterials2 = renderer2.sharedMaterials;
			for (int k = 0; k < sharedMaterials2.Length; k++)
			{
				if ((bool)sharedMaterials2[k])
				{
					if (sharedMaterials2[k].name.StartsWith("atlas_") && (bool)material)
					{
						sharedMaterials2[k] = material;
					}
					else
					{
						sharedMaterials2[k] = GameMat(sharedMaterials2[k], car.paint);
					}
				}
			}
			renderer2.sharedMaterials = sharedMaterials2;
		}
	}

	private static Transform Find(Transform r, Regex rx)
	{
		Transform[] componentsInChildren = r.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform != r && rx.IsMatch(transform.name))
			{
				return transform;
			}
		}
		return null;
	}

	private static string WheelFor(string id, Dictionary<string, object> d, bool civil, Func<float> rnd)
	{
		Dictionary<string, object> W = GameData.Wheels;
		if (W == null)
		{
			return null;
		}
		List<string> list = W.Keys.Where((string k) => !Json.B(W[k], "stock")).ToList();
		string text = Json.S(d, "stockWheel");
		if (text != null && W.ContainsKey(text) && (!civil || !(rnd() < 0.25f)))
		{
			return text;
		}
		if (list.Count <= 0)
		{
			return null;
		}
		return list[Mathf.Min(list.Count - 1, (int)(rnd() * (float)list.Count))];
	}

	public static FBCar Make(string id, Vector3 pos, float yawDeg, bool civil, Func<float> rnd, bool? stockOpt = null, uint? paintOpt = null, bool? som = null, string somMod = null)
	{
		GameObject gameObject = FBRegistry.I.Vehicle(id);
		Dictionary<string, object> dictionary = Json.O(GameData.Vehicles, id);
		if (!gameObject || dictionary == null)
		{
			return null;
		}
		if (rnd == null)
		{
			rnd = () => UnityEngine.Random.value;
		}
		GameObject gameObject2 = new GameObject("Carro_" + id);
		Transform transform = new GameObject("body").transform;
		transform.SetParent(gameObject2.transform, worldPositionStays: false);
		Transform transform2 = new GameObject("shell").transform;
		transform2.SetParent(transform, worldPositionStays: false);
		float num = 0f;
		foreach (object item in Json.L(dictionary, "wheels") ?? new List<object>())
		{
			num = Mathf.Max(num, Json.F(item, "r") - Json.Fv(Json.L(item, "p")[1]));
		}
		transform.localPosition = new Vector3(0f, num, 0f);
		bool flag = Json.B(dictionary, "port");
		bool flag2 = stockOpt ?? (!civil || rnd() < 0.55f);
		float y = ((!flag) ? (0.1f + (flag2 ? (0.1f + rnd() * 0.04f) : (rnd() * 0.03f))) : (flag2 ? (-0.02f) : (-0.07f - rnd() * 0.03f)));
		if (PORT_RIDE.TryGetValue(id, out (float, float) value))
		{
			float num2;
			if (flag2)
			{
				(num2, _) = value;
			}
			else
			{
				num2 = value.Item2 - rnd() * 0.02f;
			}
			y = num2;
		}
		transform2.localPosition = new Vector3(0f, y, 0f);
		Color paint = Hex(paintOpt ?? PAINTS[Mathf.Min(PAINTS.Length - 1, (int)(rnd() * (float)PAINTS.Length))]);
		GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject);
		Transform[] array = new Transform[3];
		Transform transform3 = null;
		Transform[] componentsInChildren = gameObject3.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform4 in componentsInChildren)
		{
			if (transform4 == gameObject3.transform)
			{
				continue;
			}
			string text = Regex.Replace(transform4.name, "_\\d+$", "");
			if (text.StartsWith("body_lod1"))
			{
				Transform[] array2 = array;
				if ((object)array2[1] == null)
				{
					array2[1] = transform4;
				}
			}
			else if (text.StartsWith("body_lod2"))
			{
				Transform[] array2 = array;
				if ((object)array2[2] == null)
				{
					array2[2] = transform4;
				}
			}
			else if (text.StartsWith("body"))
			{
				Transform[] array2 = array;
				if ((object)array2[0] == null)
				{
					array2[0] = transform4;
				}
			}
			else if (text.StartsWith("wheel") && (object)transform3 == null)
			{
				transform3 = transform4;
			}
		}
		List<LOD> list = new List<LOD>();
		float[] array3 = ((Json.Get(dictionary, "port") == null) ? new float[3] { 0.14f, 0.056f, 0.012f } : new float[3] { 0.4f, 0.16f, 0.03f });
		for (int j = 0; j < 3; j++)
		{
			if ((bool)array[j])
			{
				array[j].SetParent(transform2, worldPositionStays: false);
				Dress(array[j].gameObject, paint);
				if (!flag)
				{
					FBPlates.Split(array[j], FBPlates.Of(paint));
					FBMirrors.Split(array[j], transform2, id);
				}
				else
				{
					FBPlates.SetText(array[j], FBPlates.Of(paint));
				}
				list.Add(new LOD(array3[j], array[j].GetComponentsInChildren<Renderer>()));
			}
		}
		if (list.Count > 0)
		{
			LODGroup lODGroup = transform2.gameObject.AddComponent<LODGroup>();
			lODGroup.fadeMode = LODFadeMode.CrossFade;
			lODGroup.animateCrossFading = true;
			LOD[] array4 = list.ToArray();
			array4[array4.Length - 1].screenRelativeTransitionHeight = 0.004f;
			lODGroup.SetLODs(array4);
			lODGroup.RecalculateBounds();
		}
		if ((bool)transform3)
		{
			transform3.SetParent(transform, worldPositionStays: false);
			transform3.gameObject.SetActive(value: false);
			transform3.name = "wheel_tpl";
		}
		Transform transform5 = null;
		if (Json.O(dictionary, "int") != null)
		{
			GameObject gameObject4 = FBRegistry.I.Interior(id);
			if ((object)gameObject4 != null)
			{
				GameObject gameObject5 = UnityEngine.Object.Instantiate(gameObject4, transform2, worldPositionStays: false);
				gameObject5.name = "interior";
				Dress(gameObject5, paint);
				transform5 = Find(gameObject5.transform, new Regex("^volante$"));
			}
		}
		FBCar fBCar = gameObject2.AddComponent<FBCar>();
		fBCar.id = id;
		fBCar.def = dictionary;
		fBCar.heavy = Regex.IsMatch(id, "F250|RAM|Kombi");
		fBCar.engineProf = FBCarSound.ProfileFor(id, fBCar);
		fBCar.tracao = FBReb.Tracao(id);
		string text2 = ((civil && FBParedao.PICKUPS.Contains(id) && (som ?? (rnd() < 0.75f))) ? (somMod ?? FBParedao.PickModule(dictionary, rnd)) : null);
		string text3 = WheelFor(id, dictionary, civil, rnd);
		GameObject gameObject6 = ((text3 != null) ? FBRegistry.I.Wheel(text3) : null);
		float num3 = ((text3 != null) ? Json.F(Json.O(GameData.Wheels, text3), "r") : 0f);
		foreach (object item2 in Json.L(dictionary, "wheels") ?? new List<object>())
		{
			List<object> list2 = Json.L(item2, "p");
			float num4 = Json.Fv(list2[0]);
			float num5 = Json.F(item2, "r");
			GameObject gameObject7;
			if ((bool)gameObject6)
			{
				gameObject7 = UnityEngine.Object.Instantiate(gameObject6, transform, worldPositionStays: false);
				Find(gameObject7.transform, new Regex("^wheel"));
				if (num3 > 0f)
				{
					gameObject7.transform.localScale = Vector3.one * (num5 / num3);
				}
			}
			else
			{
				if (!transform3)
				{
					continue;
				}
				gameObject7 = UnityEngine.Object.Instantiate(transform3.gameObject, transform, worldPositionStays: false);
			}
			gameObject7.name = "roda_" + Json.S(item2, "id");
			Dress(gameObject7, paint);
			gameObject7.transform.localPosition = new Vector3(0f - num4, Json.Fv(list2[1]), Json.Fv(list2[2]));
			if (num4 < 0f)
			{
				gameObject7.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
			}
			gameObject7.SetActive(value: true);
			fBCar.wheels.Add(new FBWheel
			{
				obj = gameObject7.transform,
				front = (Json.Fv(list2[2]) > 0f),
				r = num5 * 1.12f,
				rr = num5,
				sgn = ((!(num4 < 0f)) ? 1 : (-1)),
				bas = gameObject7.transform.localRotation,
				webP = new Vector3(num4, Json.Fv(list2[1]), Json.Fv(list2[2]))
			});
		}
		UnityEngine.Object.Destroy(gameObject3);
		fBCar.wheelTpl = transform3;
		fBCar.wid = text3;
		fBCar.curWid = text3;
		fBCar.wsrc = gameObject6;
		fBCar.paint = paint;
		gameObject2.transform.localScale = Vector3.one * 1.12f;
		fBCar.scale = 1.12f;
		List<object> source = Json.L(dictionary, "wheels") ?? new List<object>();
		List<float> source2 = source.Select((object w) => Json.Fv(Json.L(w, "p")[2])).ToList();
		List<float> source3 = source.Select((object w) => Mathf.Abs(Json.Fv(Json.L(w, "p")[0]))).ToList();
		float num6 = Mathf.Max(source2.DefaultIfEmpty(1f).Max(), 1f);
		float num7 = Mathf.Min(source2.DefaultIfEmpty(-1f).Min(), -1f);
		float num8 = Mathf.Max(source3.DefaultIfEmpty(0.7f).Max(), 0.7f);
		fBCar.wheelbase = (num6 - num7) * 1.12f;
		fBCar.track = 2f * num8 * 1.12f;
		Dictionary<string, object> R = Json.O(dictionary, "rig");
		float oy = transform.localPosition.y + transform2.localPosition.y;
		if (R != null)
		{
			List<object> list3 = Json.L(R, "seat");
			fBCar.seat = new Vector3(0f - Json.Fv(list3[0]), Json.Fv(list3[1]) + oy + 0.3f, Json.Fv(list3[2]) + 0.06f);
			if (Json.L(R, "handL") != null)
			{
				fBCar.rigHands = new Vector3[2]
				{
					Rp("handL").Value,
					Rp("handR").Value
				};
			}
			if (Json.L(R, "poleL") != null)
			{
				fBCar.rigPoles = new Vector3[2]
				{
					Rp("poleL").Value,
					Rp("poleR").Value
				};
			}
			if (Json.L(R, "footL") != null)
			{
				fBCar.rigFeet = new Vector3[2]
				{
					Rp("footL").Value,
					Rp("footR").Value
				};
			}
			fBCar.rigWheel = Rp("wheel");
			List<object> list4 = Json.L(R, "out");
			if (list4 != null)
			{
				fBCar.rigOut = new Vector3(0f - Json.Fv(list4[0]), 0f, Json.Fv(list4[2]));
			}
		}
		else
		{
			fBCar.seat = new Vector3((0f - num8) * 0.42f, num + 0.62f, num7 * 0.12f);
		}
		if ((bool)transform5)
		{
			fBCar.volante = transform5;
			fBCar.volBase = transform5.localRotation;
		}
		fBCar.shell = transform2;
		fBCar.shellY0 = transform2.localPosition.y;
		fBCar.shellP0 = transform2.localPosition;
		fBCar.shellR0 = transform2.localRotation;
		fBCar.zb = num7;
		fBCar.wbL = num6 - num7;
		fBCar.seat0 = fBCar.seat;
		fBCar.rigHands0 = fBCar.rigHands?.ToArray();
		fBCar.rigPoles0 = fBCar.rigPoles?.ToArray();
		fBCar.rigFeet0 = fBCar.rigFeet?.ToArray();
		fBCar.rigWheel0 = fBCar.rigWheel;
		Finish(fBCar, gameObject2, pos, yawDeg);
		LodExtras(fBCar);
		if (text2 != null)
		{
			FBParedao.Mount(fBCar, text2, dictionary);
			componentsInChildren = (fBCar.soundWallNode ? fBCar.soundWallNode.GetComponentsInChildren<Transform>(includeInactive: true) : new Transform[0]);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].gameObject.layer = 11;
			}
		}
		return fBCar;
		Vector3? Rp(string k)
		{
			List<object> list5 = Json.L(R, k);
			if (list5 != null)
			{
				return W2U(list5, oy);
			}
			return null;
		}
	}

	public static FBCar MakeIE(string id, Vector3 pos, float yawDeg)
	{
		Dictionary<string, object> dictionary = Json.O(GameData.IE, id);
		GameObject gameObject = FBRegistry.I.Vehicle(id);
		if (dictionary == null || !gameObject)
		{
			return null;
		}
		GameObject gameObject2 = new GameObject("IE_" + id);
		GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, gameObject2.transform, worldPositionStays: false);
		gameObject3.name = "modelo";
		Transform transform = Find(gameObject3.transform, new Regex("^destroyed$"));
		if ((bool)transform)
		{
			transform.gameObject.SetActive(value: false);
		}
		Collider[] componentsInChildren = gameObject3.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		string text = ((Json.S(dictionary, "kind") == "moto") ? "moto" : "car");
		List<object> source = Json.L(dictionary, "wheels") ?? new List<object>();
		List<object> list = Json.L(dictionary, "wheelNodes");
		float num = ((list == null) ? (0f - Bounds(gameObject3.transform, gameObject2.transform).min.y) : (0f - source.Min((object w) => Json.Fv(Json.L(w, "p")[1]) - Json.F(w, "r"))));
		gameObject3.transform.localPosition = new Vector3(0f, num, 0f);
		FBCar fBCar = gameObject2.AddComponent<FBCar>();
		fBCar.id = id;
		fBCar.ie = true;
		fBCar.meta = dictionary;
		fBCar.kind = text;
		fBCar.heavy = id == "caveirao";
		fBCar.armored = id == "caveirao";
		fBCar.grau = (Json.S(dictionary, "src") ?? "").ToLowerInvariant().Contains("grau");
		foreach (var item4 in (List<(string, bool, bool)>)((list != null) ? ((IList)list.Select((object x) => (Json.S(x, "name"), Json.B(x, "front"), Json.B(x, "noSteer"))).ToList()) : ((IList)IE_WHEELS[text].Select((string n) => (n: n, Regex.IsMatch(n, "Dianteira|^F"), false)).ToList())))
		{
			string item = item4.Item1;
			bool item2 = item4.Item2;
			bool item3 = item4.Item3;
			Transform transform2 = Find(gameObject3.transform, new Regex("^" + Regex.Escape(item) + "$"));
			if ((bool)transform2)
			{
				bool fz = gameObject2.transform.InverseTransformPoint(transform2.position).z > 0f;
				float r = (from w in source
					where Json.Fv(Json.L(w, "p")[2]) > 0f == fz
					select Json.F(w, "r")).DefaultIfEmpty(0.4f).First();
				fBCar.wheels.Add(new FBWheel
				{
					obj = transform2,
					front = item2,
					noSteer = item3,
					r = r,
					sgn = 1,
					bas = transform2.localRotation
				});
			}
		}
		foreach (string item5 in Json.L(dictionary, "steerNodes")?.Select((object x) => x as string) ?? IE_STEER[text])
		{
			Transform transform3 = Find(gameObject3.transform, new Regex("^" + Regex.Escape(item5) + "$"));
			if ((bool)transform3)
			{
				fBCar.steerNodes.Add((transform3, transform3.localRotation));
			}
		}
		List<float> source2 = source.Select((object w) => Json.Fv(Json.L(w, "p")[2])).DefaultIfEmpty(0f).ToList();
		List<object> source3 = Json.L(dictionary, "seats") ?? new List<object>();
		object obj = source3.FirstOrDefault((object x) => Json.B(x, "driver")) ?? source3.FirstOrDefault();
		fBCar.wheelbase = Mathf.Max(1.2f, source2.Max() - source2.Min());
		fBCar.track = ((text == "moto") ? 0.3f : (2f * source.Select((object w) => Mathf.Abs(Json.Fv(Json.L(w, "p")[0]))).DefaultIfEmpty(0.7f).Max()));
		fBCar.seat = ((obj != null) ? W2U(Json.L(obj, "p"), num) : new Vector3(0f, 1f, 0f));
		List<object> list2 = Json.L(obj, "feet");
		if (list2 != null)
		{
			fBCar.feet = W2U(list2, num);
		}
		fBCar.footSide = Json.F(obj, "footSide", 0.22f);
		Dictionary<string, object> o = Json.O(dictionary, "points");
		List<object> list3 = Json.L(o, "steerPoint");
		if (list3 != null)
		{
			fBCar.grip = W2U(list3, num);
		}
		fBCar.gripSide = Json.F(o, "gripSide");
		List<object> list4 = Json.L(dictionary, "steerAxis");
		if (list4 != null)
		{
			fBCar.steerAxis = new Vector3(0f - Json.Fv(list4[0]), Json.Fv(list4[1]), Json.Fv(list4[2])).normalized;
		}
		foreach (object item6 in Json.L(dictionary, "simpleCol") ?? new List<object>())
		{
			BoxCollider boxCollider = gameObject2.AddComponent<BoxCollider>();
			List<object> list5 = Json.L(item6, "c");
			List<object> list6 = Json.L(item6, "size");
			boxCollider.center = new Vector3(0f - Json.Fv(list5[0]), Json.Fv(list5[1]) + num, Json.Fv(list5[2]));
			boxCollider.size = new Vector3(Json.Fv(list6[0]), Json.Fv(list6[1]), Json.Fv(list6[2]));
			fBCar.cols.Add(boxCollider);
		}
		if (IE_FIX.TryGetValue(id, out (float, float) value))
		{
			gameObject2.transform.localScale = Vector3.one * value.Item1;
			(fBCar.scale, _) = value;
			fBCar.wheelbase *= value.Item1;
			fBCar.track *= value.Item1;
			foreach (FBWheel wheel in fBCar.wheels)
			{
				wheel.r *= value.Item1;
			}
			fBCar.seat.y += value.Item2;
		}
		fBCar.engineProf = FBCarSound.ProfileFor(id, fBCar);
		Finish(fBCar, gameObject2, pos, yawDeg, Json.L(dictionary, "simpleCol") == null);
		LodExtras(fBCar);
		return fBCar;
	}

	private static void LodExtras(FBCar car)
	{
		Renderer[] second = car.wheels.Where((FBWheel w) => w.obj).SelectMany((FBWheel w) => w.obj.GetComponentsInChildren<Renderer>(includeInactive: true)).ToArray();
		LODGroup lODGroup = (car.shell ? car.shell.GetComponent<LODGroup>() : null);
		if ((bool)lODGroup)
		{
			Transform transform = car.shell.Find("interior");
			Renderer[] array = (transform ? transform.GetComponentsInChildren<Renderer>(includeInactive: true) : new Renderer[0]);
			Renderer[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i].shadowCastingMode = ShadowCastingMode.Off;
			}
			LOD[] lODs = lODGroup.GetLODs();
			for (int j = 0; j < lODs.Length; j++)
			{
				lODs[j].renderers = (from r in lODs[j].renderers.Concat(second).Concat((j == 0) ? array : new Renderer[0])
					where r
					select r).Distinct().ToArray();
			}
			lODGroup.SetLODs(lODs);
			lODGroup.RecalculateBounds();
		}
		else if (car.ie && !car.GetComponentInChildren<LODGroup>())
		{
			Renderer[] componentsInChildren = car.GetComponentsInChildren<Renderer>(includeInactive: true);
			if (componentsInChildren.Length != 0)
			{
				lODGroup = car.gameObject.AddComponent<LODGroup>();
				lODGroup.fadeMode = LODFadeMode.CrossFade;
				lODGroup.animateCrossFading = true;
				lODGroup.SetLODs(new LOD[1]
				{
					new LOD((car.kind == "moto") ? 0.01f : 0.006f, componentsInChildren)
				});
				lODGroup.RecalculateBounds();
			}
		}
	}

	private static Bounds Bounds(Transform root, Transform space)
	{
		Matrix4x4 worldToLocalMatrix = space.worldToLocalMatrix;
		Bounds result = default(Bounds);
		bool flag = false;
		Renderer[] componentsInChildren = root.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren)
		{
			if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.name == "sombra_contato" || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
			{
				continue;
			}
			Bounds bounds;
			Matrix4x4 matrix4x;
			if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
			{
				bounds = skinnedMeshRenderer.localBounds;
				matrix4x = worldToLocalMatrix * skinnedMeshRenderer.transform.localToWorldMatrix;
			}
			else
			{
				MeshFilter component = renderer.GetComponent<MeshFilter>();
				if ((bool)component && (bool)component.sharedMesh)
				{
					bounds = component.sharedMesh.bounds;
					matrix4x = worldToLocalMatrix * renderer.transform.localToWorldMatrix;
				}
				else
				{
					bounds = renderer.bounds;
					matrix4x = worldToLocalMatrix;
				}
			}
			for (int j = 0; j < 8; j++)
			{
				Vector3 vector = matrix4x.MultiplyPoint3x4(new Vector3(((j & 1) == 0) ? bounds.min.x : bounds.max.x, ((j & 2) == 0) ? bounds.min.y : bounds.max.y, ((j & 4) == 0) ? bounds.min.z : bounds.max.z));
				if (!flag)
				{
					result = new Bounds(vector, Vector3.zero);
					flag = true;
				}
				else
				{
					result.Encapsulate(vector);
				}
			}
		}
		return result;
	}

	private static void Finish(FBCar car, GameObject grp, Vector3 pos, float yawDeg, bool meshCol = true)
	{
		float num = 3.4028235E+38f;
		float y = pos.y;
		bool flag = false;
		RaycastHit[] array = Physics.RaycastAll(new Vector3(pos.x, pos.y + 2.8f, pos.z), Vector3.down, 8.8f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if (raycastHit.normal.y > 0.6f && Mathf.Abs(raycastHit.point.y - pos.y) < num)
			{
				num = Mathf.Abs(raycastHit.point.y - pos.y);
				y = raycastHit.point.y;
				flag = true;
			}
		}
		if (flag)
		{
			pos.y = y;
		}
		grp.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
		car.lbox = Bounds(grp.transform, grp.transform);
		car.box = car.lbox;
		if (meshCol)
		{
			Transform transform = grp.transform.Find("body/shell");
			Transform transform2 = null;
			if ((bool)transform)
			{
				foreach (Transform item in transform)
				{
					if (item.name.StartsWith("body_lod1"))
					{
						transform2 = item;
					}
				}
			}
			if (!transform2 && (bool)transform)
			{
				foreach (Transform item2 in transform)
				{
					if (item2.name.StartsWith("body"))
					{
						transform2 = item2;
						break;
					}
				}
			}
			Transform obj = (transform2 ? transform2 : grp.transform);
			bool flag2 = false;
			MeshFilter[] componentsInChildren = obj.GetComponentsInChildren<MeshFilter>(includeInactive: true);
			foreach (MeshFilter meshFilter in componentsInChildren)
			{
				if ((bool)meshFilter.sharedMesh && meshFilter.sharedMesh.isReadable)
				{
					MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
					meshCollider.sharedMesh = meshFilter.sharedMesh;
					car.cols.Add(meshCollider);
					flag2 = true;
				}
			}
			if (!flag2)
			{
				BoxCollider boxCollider = grp.AddComponent<BoxCollider>();
				boxCollider.center = car.lbox.center + Vector3.up * car.lbox.size.y * 0.05f;
				boxCollider.size = Vector3.Scale(car.lbox.size, new Vector3(0.96f, 0.9f, 0.97f));
				car.cols.Add(boxCollider);
			}
			foreach (FBWheel wheel in car.wheels)
			{
				Bounds bounds = Bounds(wheel.obj, grp.transform);
				BoxCollider boxCollider2 = grp.AddComponent<BoxCollider>();
				boxCollider2.center = bounds.center;
				boxCollider2.size = bounds.size;
				car.cols.Add(boxCollider2);
			}
		}
		ContactShadow(grp.transform, car.lbox);
		Transform[] componentsInChildren2 = grp.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].gameObject.layer = 11;
		}
		Rigidbody rigidbody = grp.AddComponent<Rigidbody>();
		rigidbody.isKinematic = true;
		rigidbody.interpolation = RigidbodyInterpolation.None;
		car.yaw = (0f - yawDeg) * ((float)Math.PI / 180f);
		list.Add(car);
	}

	private static void ContactShadow(Transform grp, Bounds lb)
	{
		if (!shadowMat)
		{
			Texture2D texture2D = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false)
			{
				wrapMode = TextureWrapMode.Clamp
			};
			Color[] array = new Color[4096];
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float f = Mathf.Abs(((float)j + 0.5f) / 64f * 2f - 1f);
					float f2 = Mathf.Abs(((float)i + 0.5f) / 64f * 2f - 1f);
					float num = Mathf.Pow(Mathf.Pow(f, 4f) + Mathf.Pow(f2, 4f), 0.25f);
					array[i * 64 + j] = new Color(0f, 0f, 0f, Mathf.Clamp01((1f - num) * 2.2f) * 0.62f);
				}
			}
			texture2D.SetPixels(array);
			texture2D.Apply();
			Material material = Resources.Load<Material>("FBMats/Unlit_Alpha");
			shadowMat = (material ? new Material(material) : FBShaders.Transparent(new Material(FBShaders.Unlit)));
			shadowMat.SetTexture("_BaseMap", texture2D);
			shadowMat.SetColor("_BaseColor", Color.white);
			shadowMat.renderQueue = 2990;
		}
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
		UnityEngine.Object.DestroyImmediate(gameObject.GetComponent<Collider>());
		gameObject.name = "sombra_contato";
		gameObject.transform.SetParent(grp, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(lb.center.x, lb.min.y + 0.03f, lb.center.z);
		gameObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
		gameObject.transform.localScale = new Vector3(lb.size.x, lb.size.z, 1f);
		MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
		component.sharedMaterial = shadowMat;
		component.shadowCastingMode = ShadowCastingMode.Off;
		component.receiveShadows = false;
	}

	public static bool AllowedIn(string mode)
	{
		if (!(mode == "squad") && !(mode == "4v4") && !(mode == "10x10"))
		{
			return !(mode == "ffa");
		}
		return false;
	}

	public static void SpawnParked(FBMatch G, float freeChance = 0f)
	{
		Clear();
		if (!Allowed)
		{
			Debug.Log("[FreeBala] veículos desativados no modo " + FBSession.mode);
			return;
		}
		List<object> list = Json.L(G.Level, "parked");
		if (list == null || FBRegistry.I == null)
		{
			return;
		}
		string text = Json.S(G.Level, "id") ?? Json.S(G.Level, "name") ?? "map";
		string text2 = string.Join("|", list.Select((object o) => string.Join(",", (Json.L(o, "p") ?? new List<object>()).Select((object v) => F(Json.Fv(v)))) + ":" + F(Json.F(o, "yaw")) + ":" + Json.S(o, "kind")));
		Func<float> allRng = Seeded(HashSeed(text + "|" + text2));
		List<(string id, float w)> ids = CIVIL.Where(((string id, float w) c) => (bool)FBRegistry.I.Vehicle(c.id) && GameData.Vehicles != null && GameData.Vehicles.ContainsKey(c.id)).ToList();
		float tot = ids.Sum(((string id, float w) c) => c.w);
		List<(Dictionary<string, object>, int, string, string, Func<float>, bool)> list2 = new List<(Dictionary<string, object>, int, string, string, Func<float>, bool)>();
		int num = 0;
		foreach (object item3 in list)
		{
			Dictionary<string, object> dictionary = item3 as Dictionary<string, object>;
			string text3 = Json.S(dictionary, "kind");
			Func<float> func = Seeded(HashSeed(string.Format("{0}:{1}:{2}:{3}", text, num, string.Join(",", (Json.L(dictionary, "p") ?? new List<object>()).Select((object v) => F(Json.Fv(v)))), F(Json.F(dictionary, "yaw")))));
			string text4 = ((text3 == "viatura") ? PickViatura(func) : null);
			list2.Add((dictionary, num, text4 ?? PickCivil(), null, func, false));
			num++;
		}
		List<(Dictionary<string, object>, int, string, string, Func<float>, bool)> list3 = list2.Where<(Dictionary<string, object>, int, string, string, Func<float>, bool)>(((Dictionary<string, object> s, int i, string id, string ie, Func<float> rng, bool skip) p) => Json.S(p.s, "kind") == "viatura").ToList();
		List<(Dictionary<string, object>, int, string, string, Func<float>, bool)> list4 = list2.Where<(Dictionary<string, object>, int, string, string, Func<float>, bool)>(((Dictionary<string, object> s, int i, string id, string ie, Func<float> rng, bool skip) p) => Json.S(p.s, "kind") != "viatura").ToList();
		for (int num2 = list4.Count - 1; num2 > 0; num2--)
		{
			int num3 = (int)(allRng() * (float)(num2 + 1));
			List<(Dictionary<string, object>, int, string, string, Func<float>, bool)> list5 = list4;
			int index = num2;
			List<(Dictionary<string, object>, int, string, string, Func<float>, bool)> list6 = list4;
			int index2 = num3;
			(Dictionary<string, object>, int, string, string, Func<float>, bool) value = list4[num3];
			(Dictionary<string, object>, int, string, string, Func<float>, bool) value2 = list4[num2];
			list5[index] = value;
			list6[index2] = value2;
		}
		if (list3.Count > 0 && GameData.IE != null && GameData.IE.ContainsKey("caveirao") && (bool)FBRegistry.I.Vehicle("caveirao"))
		{
			int index3 = list2.IndexOf(list3[Mathf.Min(list3.Count - 1, 3)]);
			(Dictionary<string, object>, int, string, string, Func<float>, bool) value3 = list2[index3];
			value3.Item4 = "caveirao";
			value3.Item3 = null;
			list2[index3] = value3;
		}
		if (freeChance > 0f)
		{
			for (int i = 0; i < list2.Count; i++)
			{
				(Dictionary<string, object>, int, string, string, Func<float>, bool) value4 = list2[i];
				if (Json.S(value4.Item1, "kind") != "viatura" && value4.Item4 == null && value4.Item5() < freeChance)
				{
					value4.Item6 = true;
					list2[i] = value4;
				}
			}
		}
		foreach (var item4 in list2)
		{
			if (item4.Item6)
			{
				continue;
			}
			Vector3 pos = Coords.FromWeb(Json.L(item4.Item1, "p"));
			float yawDeg = Coords.YawFromWeb(Json.F(item4.Item1, "yaw"));
			FBCar fBCar = ((item4.Item4 != null) ? MakeIE(item4.Item4, pos, yawDeg) : ((item4.Item3 != null) ? Make(item4.Item3, pos, yawDeg, Json.S(item4.Item1, "kind") != "viatura", item4.Item5) : null));
			if ((bool)fBCar)
			{
				fBCar.syncId = $"{text}:{item4.Item2}";
				string why;
				if (fBCar.kind == "moto")
				{
					fBCar.yaw += (item4.Item5() - 0.5f) * 0.6f;
					Rest(fBCar);
				}
				else if (!ParkValid(fBCar, out why))
				{
					ParkLog.Add(why);
					FBVehicles.list.Remove(fBCar);
					UnityEngine.Object.Destroy(fBCar.gameObject);
				}
			}
		}
		MotoProps();
		string PickCivil()
		{
			float num4 = allRng() * tot;
			foreach (var item5 in ids)
			{
				string item = item5.id;
				float item2 = item5.w;
				num4 -= item2;
				if (num4 <= 0f)
				{
					return item;
				}
			}
			if (ids.Count <= 0)
			{
				return null;
			}
			return ids[0].id;
		}
	}

	public static string PickViatura(Func<float> rng)
	{
		List<string> list = VIATURAS.Where((string v) => (bool)FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v)).ToList();
		if (list.Count > 0)
		{
			return list[Mathf.Min(list.Count - 1, (int)(rng() * (float)list.Count))];
		}
		if (!FBRegistry.I.Vehicle("ViaturaPM") || !GameData.Vehicles.ContainsKey("ViaturaPM"))
		{
			return null;
		}
		return "ViaturaPM";
	}

	private static void Rest(FBCar car)
	{
		car.transform.rotation = FBDrive.CarRot(0f, car.yaw, 0.1f);
	}

	private static bool ParkValid(FBCar car, out string why)
	{
		why = null;
		Transform transform = car.transform;
		if (!FBRoads.Ready)
		{
			FBRoads.Load(FBSession.map);
		}
		_ = car.lbox.extents;
		_ = car.scale;
		_ = car.lbox.extents;
		_ = car.scale;
		if (FBRoads.Ready)
		{
			float s;
			float lat;
			FBSeg fBSeg = FBRoads.NearestLane(transform.position, 6f, out s, out lat);
			if (fBSeg != null && Mathf.Abs(lat) < fBSeg.w / 2f * 0.55f)
			{
				FBSpot fBSpot = null;
				float num = 30f;
				foreach (FBSpot sp in FBRoads.spots)
				{
					if (!sp.car)
					{
						float num2 = Vector3.Distance(sp.p, transform.position);
						if (num2 < num && !list.Any((FBCar o) => (bool)o && o != car && Vector3.Distance(o.transform.position, sp.p) < 2.6f))
						{
							num = num2;
							fBSpot = sp;
						}
					}
				}
				if (fBSpot == null)
				{
					ParkDropped++;
					why = $"{car.id} no meio da faixa (lateral {lat:0.0} m de {fBSeg.w / 2f:0.0}) sem vaga perto: não nasce";
					return false;
				}
				transform.position = fBSpot.p;
				car.yaw = (0f - fBSpot.yaw) * ((float)Math.PI / 180f);
				transform.rotation = FBDrive.CarRot(0f, car.yaw, 0f);
				fBSpot.car = car;
				ParkMoved++;
				ParkLog.Add($"{car.id} estava no meio da faixa: foi para a vaga a {num:0} m");
			}
		}
		Vector3 right = transform.right;
		Vector3 position = transform.position;
		float[] array = new float[5] { 0f, 0.6f, -0.6f, 1.2f, -1.2f };
		foreach (float num3 in array)
		{
			transform.position = position + right * num3;
			Settle(car);
			if (!Blocked(car, out var what))
			{
				if (num3 != 0f)
				{
					ParkMoved++;
					ParkLog.Add($"{car.id} afastado {num3:+0.0;-0.0} m ({what})");
				}
				ParkOk++;
				return true;
			}
			why = car.id + " sobreposto a " + what;
		}
		ParkDropped++;
		return false;
	}

	private static bool Blocked(FBCar car, out string what)
	{
		what = null;
		Transform transform = car.transform;
		Bounds lbox = car.lbox;
		Vector3 vector = Vector3.Scale(lbox.extents, Vector3.one * car.scale);
		float num = 0.35f;
		float num2 = Mathf.Max(num + 0.3f, lbox.max.y * car.scale * 0.88f);
		Vector3 center = transform.TransformPoint(new Vector3(lbox.center.x, 0f, lbox.center.z)) + transform.up * ((num + num2) / 2f);
		Vector3 halfExtents = new Vector3(vector.x * 0.85f, (num2 - num) / 2f, vector.z * 0.88f);
		car.SetColliders(on: false);
		try
		{
			Collider[] array = Physics.OverlapBox(center, halfExtents, transform.rotation, FBWorld.SolidMask | 0x800, QueryTriggerInteraction.Ignore);
			foreach (Collider collider in array)
			{
				FBCar componentInParent = collider.GetComponentInParent<FBCar>();
				if (!(componentInParent == car))
				{
					if ((bool)componentInParent)
					{
						what = "veículo " + componentInParent.id;
						return true;
					}
					if (collider.gameObject.layer == 8 && !(collider is TerrainCollider) && collider.bounds.size.y > 0.5f)
					{
						what = collider.name;
						return true;
					}
				}
			}
		}
		finally
		{
			car.SetColliders(on: true);
		}
		return false;
	}

	public static void Settle(FBCar car)
	{
		Transform transform = car.transform;
		Bounds lbox = car.lbox;
		float scale = car.scale;
		float num = lbox.extents.x * scale * 0.8f;
		float num2 = Mathf.Max(0.8f, car.wheelbase * 0.5f);
		Vector3 vector = FBDrive.Fwd(car.yaw);
		Vector3 normalized = Vector3.Cross(Vector3.up, vector).normalized;
		Vector3 position = transform.position;
		float[] array = new float[4];
		int num3 = 0;
		car.SetColliders(on: false);
		try
		{
			Vector3[] array2 = new Vector3[4]
			{
				position + vector * num2 - normalized * num,
				position + vector * num2 + normalized * num,
				position - vector * num2 - normalized * num,
				position - vector * num2 + normalized * num
			};
			for (int i = 0; i < 4; i++)
			{
				float? num4 = FBWorld.GroundY(array2[i].x, position.y + 1.5f, array2[i].z, 0.3f);
				array[i] = num4 ?? position.y;
				if (num4.HasValue)
				{
					num3++;
				}
			}
		}
		finally
		{
			car.SetColliders(on: true);
		}
		if (num3 < 3)
		{
			return;
		}
		float num5 = (array[0] + array[1]) / 2f;
		float num6 = (array[2] + array[3]) / 2f;
		float num7 = (array[0] + array[2]) / 2f;
		float num8 = (array[1] + array[3]) / 2f;
		float num9 = Mathf.Clamp(Mathf.Atan2(num5 - num6, num2 * 2f), -0.25f, 0.25f);
		float num10 = Mathf.Clamp(Mathf.Atan2(num7 - num8, num * 2f), -0.15f, 0.15f);
		float num11 = (array[0] + array[1] + array[2] + array[3]) / 4f;
		float num12 = 1E+09f;
		float pitch = 0f;
		float roll = 0f;
		float[] array3 = new float[2] { 1f, -1f };
		foreach (float num13 in array3)
		{
			float[] array4 = new float[2] { 1f, -1f };
			foreach (float num14 in array4)
			{
				Quaternion quaternion = FBDrive.CarRot(num9 * num13, car.yaw, num10 * num14);
				float num15 = 0f;
				Vector3[] array5 = new Vector3[4]
				{
					new Vector3(0f - num, 0f, num2),
					new Vector3(num, 0f, num2),
					new Vector3(0f - num, 0f, 0f - num2),
					new Vector3(num, 0f, 0f - num2)
				};
				for (int l = 0; l < 4; l++)
				{
					float num16 = num11 + (quaternion * array5[l]).y;
					num15 += Mathf.Abs(num16 - array[l]);
				}
				if (num15 < num12)
				{
					num12 = num15;
					pitch = num9 * num13;
					roll = num10 * num14;
				}
			}
		}
		transform.SetPositionAndRotation(new Vector3(position.x, num11, position.z), FBDrive.CarRot(pitch, car.yaw, roll));
		car.pitch = pitch;
		car.roll = roll;
		SettleErr = Mathf.Max(SettleErr, num12 / 4f);
	}

	private static void MotoProps()
	{
		List<string> list = FBMxBike.IDS.Where(FBMxBike.Has).ToList();
		List<string> list2 = ((list.Count > 0) ? list : BIKES.Where((string b) => GameData.IE != null && GameData.IE.ContainsKey(b) && (bool)FBRegistry.I.Vehicle(b)).ToList());
		if (list2.Count == 0)
		{
			return;
		}
		Regex regex = new Regex("(^|_)moto_(bros|crf|pop|cbr)", RegexOptions.IgnoreCase);
		List<Transform> list3 = new List<Transform>();
		Transform[] array = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
		foreach (Transform transform in array)
		{
			if (transform.gameObject.layer == 8 && regex.IsMatch(transform.name) && (!transform.parent || !regex.IsMatch(transform.parent.name)))
			{
				list3.Add(transform);
			}
		}
		list3 = (from t in list3
			orderby t.GetSiblingIndex(), t.position.x
			select t).ToList();
		for (int j = 0; j < list3.Count; j++)
		{
			Transform transform2 = list3[j];
			Renderer[] componentsInChildren = transform2.GetComponentsInChildren<Renderer>();
			Bounds b2;
			if (componentsInChildren.Length != 0)
			{
				b2 = componentsInChildren[0].bounds;
				Renderer[] array2 = componentsInChildren;
				foreach (Renderer renderer in array2)
				{
					b2.Encapsulate(renderer.bounds);
				}
				Vector3 normalized = (transform2.rotation * Vector3.right).Flat().normalized;
				Vector3 normalized2 = (transform2.rotation * Vector3.forward).Flat().normalized;
				Vector3 size = b2.size;
				Vector3 vector = ((Mathf.Abs(size.x * normalized.x) + Mathf.Abs(size.z * normalized.z) >= Mathf.Abs(size.x * normalized2.x) + Mathf.Abs(size.z * normalized2.z)) ? normalized : normalized2);
				array2 = componentsInChildren;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].enabled = false;
				}
				Collider[] componentsInChildren2 = transform2.GetComponentsInChildren<Collider>();
				for (int i = 0; i < componentsInChildren2.Length; i++)
				{
					componentsInChildren2[i].enabled = false;
				}
				if (Room(-vector) > Room(vector) + 0.5f)
				{
					vector = -vector;
				}
				Vector3 pos = new Vector3(b2.center.x, b2.min.y, b2.center.z);
				float yawDeg = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
				FBCar fBCar = ((list.Count > 0) ? MakeMx(list2[j % list2.Count], pos, yawDeg) : MakeIE(list2[j % list2.Count], pos, yawDeg));
				if ((bool)fBCar)
				{
					fBCar.syncId = "motoprop:" + j;
					Rest(fBCar);
				}
			}
			float Room(Vector3 d)
			{
				float num = 9f;
				float[] array3 = new float[2] { 0.5f, 1f };
				foreach (float num2 in array3)
				{
					if (FBWorld.RayHit(new Vector3(b2.center.x, b2.min.y + num2, b2.center.z), d, 9f, out var hit))
					{
						num = Mathf.Min(num, hit.distance);
					}
				}
				return num;
			}
		}
	}

	public static void Clear()
	{
		foreach (FBCar item in list)
		{
			if ((bool)item)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}
		list.Clear();
		driving.Clear();
		FBParedao.Reset();
		FBCarFX.Reset();
		FBDrive.ResetState();
		FBSkyPlanes.Reset();
	}

	public static FBCar Spawn(string id, Vector3 p, float yawDeg)
	{
		if (id == null || !id.StartsWith("mx_"))
		{
			if (GameData.IE == null || !GameData.IE.ContainsKey(id))
			{
				return Make(id, p, yawDeg, civil: true, null);
			}
			return MakeIE(id, p, yawDeg);
		}
		return MakeMx(id.Substring(3), p, yawDeg);
	}

	public static FBCar MakeMx(string vid, Vector3 pos, float yawDeg)
	{
		GameObject gameObject = FBRegistry.I.Vehicle("mx_" + vid);
		Dictionary<string, object> dictionary = Json.O(FBMxBike.DB, vid);
		if (!gameObject || dictionary == null)
		{
			return null;
		}
		GameObject gameObject2 = new GameObject("MX_" + vid);
		GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, gameObject2.transform, worldPositionStays: false);
		gameObject3.name = "modelo";
		Collider[] componentsInChildren = gameObject3.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		FBCar car = gameObject2.AddComponent<FBCar>();
		car.id = "mx_" + vid;
		car.ie = true;
		car.kind = "moto";
		car.meta = dictionary;
		car.grau = true;
		FBMxBike fBMxBike = FBMxBike.Attach(car, vid, gameObject3.transform);
		car.mx = fBMxBike;
		fBMxBike.OnCrash += delegate(string why)
		{
			FBDrive.MxCrash(car, why);
		};
		FBMxBike.Pose ridingPose = fBMxBike.RidingPose;
		car.seat = ridingPose.hips - new Vector3(0f, 0.42f, -0.02f);
		if ((bool)fBMxBike.gripL && (bool)fBMxBike.gripR)
		{
			Vector3 vector = gameObject2.transform.InverseTransformPoint(fBMxBike.gripL.position);
			Vector3 vector2 = gameObject2.transform.InverseTransformPoint(fBMxBike.gripR.position);
			car.grip = (vector + vector2) * 0.5f;
			car.gripSide = Mathf.Abs(vector2.x - vector.x) * 0.5f;
		}
		car.feet = (ridingPose.lfoot + ridingPose.rfoot) * 0.5f;
		car.footSide = Mathf.Abs(ridingPose.rfoot.x - ridingPose.lfoot.x) * 0.5f;
		float num = 0f;
		float num2 = 0f;
		Transform[] componentsInChildren2 = gameObject3.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren2)
		{
			if (transform.name == "FPhysicsWheel")
			{
				num = gameObject2.transform.InverseTransformPoint(transform.position).z;
			}
			else if (transform.name == "RPhysicsWheel")
			{
				num2 = gameObject2.transform.InverseTransformPoint(transform.position).z;
			}
		}
		gameObject2.transform.localScale = Vector3.one * 1.2f;
		car.scale = 1.2f;
		car.wheelbase = Mathf.Max(1f, (num - num2) * 1.2f);
		car.track = 0.36f;
		float num3 = 3.4028235E+38f;
		float y = pos.y;
		bool flag = false;
		RaycastHit[] array = Physics.RaycastAll(new Vector3(pos.x, pos.y + 2.8f, pos.z), Vector3.down, 8.8f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if (raycastHit.normal.y > 0.6f && raycastHit.collider.transform.root != gameObject2.transform && Mathf.Abs(raycastHit.point.y - pos.y) < num3)
			{
				num3 = Mathf.Abs(raycastHit.point.y - pos.y);
				y = raycastHit.point.y;
				flag = true;
			}
		}
		if (flag)
		{
			pos.y = y;
		}
		gameObject2.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
		fBMxBike.Paint(HashSeed(vid + "|" + F(Mathf.Round(pos.x)) + "," + F(Mathf.Round(pos.z))));
		car.lbox = Bounds(gameObject2.transform, gameObject2.transform);
		car.box = car.lbox;
		ContactShadow(gameObject2.transform, car.lbox);
		componentsInChildren2 = gameObject2.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].gameObject.layer = 11;
		}
		car.yaw = (0f - yawDeg) * ((float)Math.PI / 180f);
		car.engineProf = null;
		LODGroup lODGroup = gameObject2.AddComponent<LODGroup>();
		lODGroup.fadeMode = LODFadeMode.CrossFade;
		lODGroup.animateCrossFading = true;
		lODGroup.SetLODs(new LOD[1]
		{
			new LOD(0.01f, (from r in gameObject2.GetComponentsInChildren<Renderer>(includeInactive: true)
				where r.name != "sombra_contato"
				select r).ToArray())
		});
		lODGroup.RecalculateBounds();
		list.Add(car);
		return car;
	}

	public static FBCar BySyncId(string id)
	{
		return list.FirstOrDefault((FBCar c) => (bool)c && c.syncId == id);
	}

	public static FBCar Nearest(Vector3 p, float r)
	{
		FBCar result = null;
		float num = r;
		foreach (FBCar item in list)
		{
			if ((bool)item && !item.driver && item.remoteDriver < 0)
			{
				Vector3 point = item.transform.InverseTransformPoint(p);
				Vector3 position = item.lbox.ClosestPoint(point);
				float num2 = Vector3.Distance(item.transform.TransformPoint(position), p);
				if (num2 < num)
				{
					num = num2;
					result = item;
				}
			}
		}
		return result;
	}

	public static void Toggle(FBCharacter ch)
	{
		if (Driving(ch))
		{
			FBDrive.Exit(ch, force: false);
		}
		else if (Allowed)
		{
			FBCar fBCar = Nearest(ch.Pos, 2.2f);
			if ((bool)fBCar)
			{
				FBDrive.Enter(ch, fBCar);
			}
		}
	}

	public static void Exit(FBCharacter ch, bool force)
	{
		FBDrive.Exit(ch, force);
	}

	public static void ExitIfDriving(FBCharacter ch)
	{
		if (Driving(ch))
		{
			FBDrive.Exit(ch, force: true);
		}
	}

	public static void PlayerInput(FBCharacter ch, FBInputState inp, float dt)
	{
		FBDrive.PlayerInput(ch, inp, dt);
	}

	public static void Tick(float dt)
	{
		FBDrive.Tick(dt);
		FBCarFX.Update(dt);
	}

	public static void Camera(Camera cam, float dt, float camYaw, float camPitch)
	{
		FBDrive.Camera(cam, dt, camYaw, camPitch);
	}
}
}
