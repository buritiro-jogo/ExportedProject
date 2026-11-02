using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace FreeBala
{
public static class FBVestirRua
{
	public static bool On = Array.IndexOf(Environment.GetCommandLineArgs(), "-fbsem171props") < 0;

	public static readonly Dictionary<string, int> Count = new Dictionary<string, int>();

	public static int Total;

	public static GameObject Root;

	private static readonly Dictionary<string, GameObject> libs = new Dictionary<string, GameObject>();

	private static readonly string[] LIXEIRA = new string[6] { "SM_lixeiras1", "SM_lixeiras2", "SM_lixeiras3", "SM_lixeiras4", "SM_Lixeira7", "LixeiraPoste" };

	private static readonly string[] PLACA_ESQ = new string[9] { "PlacasRegulamentacao_Pare", "PlacasRegulamentacao_ProibidoEstacionar", "PlacasAdvertencia_Lombada", "PlacasAdvertencia_FaixaPedestre", "PlacasRegulamentacao_Limite40km", "PlacasRegulamentacao_ProibidoUltrapassar", "PlacasRegulamentacao_ProibidoRetornoEsquerda", "PlacasAdvertencia_Semaforo", "PlacasRegulamentacao_PodeEstacionar" };

	private static readonly string[] LIXO = new string[11]
	{
		"SM_CaixaPapelao1", "SM_CaixaPapelao2", "SM_CaixaPapelao3", "SM_CaixaPapelao5", "SM_CaixaPapelao8", "SM_CaixaPapelao10", "SM_Pallet_03", "SM_WoodenPallet", "SM_Barrel_01", "SM_Tire_02",
		"Papelao"
	};

	private static readonly string[] MESA = new string[2] { "SM_Mesa1", "SM_Table02_003" };

	private static readonly string[] CADEIRA = new string[2] { "SM_Cadeira1", "SM_CadeiraDeBar2" };

	private static readonly string[] NA_MESA = new string[7] { "Garrafa_de_Cerveja", "Garrafa_de_Cerveja", "Garrafa_de_Cachaca", "Garrafa3", "SM_CanecaChopp", "BandeijaCopos", "FardoLata" };

	private static readonly Dictionary<string, (string mat, float kg)> FIS = new Dictionary<string, (string, float)>
	{
		["SM_lixeiras1"] = ("plastico", 6f),
		["SM_lixeiras2"] = ("plastico", 6f),
		["SM_lixeiras3"] = ("plastico", 7f),
		["SM_lixeiras4"] = ("plastico", 7f),
		["SM_Lixeira7"] = ("metal", 12f),
		["SM_CaixaPapelao1"] = ("papelao", 1.5f),
		["SM_CaixaPapelao2"] = ("papelao", 1.2f),
		["SM_CaixaPapelao3"] = ("papelao", 2f),
		["SM_CaixaPapelao5"] = ("papelao", 1.8f),
		["SM_CaixaPapelao8"] = ("papelao", 1f),
		["SM_CaixaPapelao10"] = ("papelao", 2.4f),
		["Papelao"] = ("papelao", 0.6f),
		["SM_Pallet_03"] = ("madeira", 15f),
		["SM_WoodenPallet"] = ("madeira", 15f),
		["SM_Barrel_01"] = ("metal_pesado", 22f),
		["SM_Tire_02"] = ("plastico", 9f),
		["SM_ConedeTransito1"] = ("plastico", 2.2f),
		["SM_ConedeTransito2"] = ("plastico", 2.2f),
		["SM_BarreiradeTransito1"] = ("plastico", 9f),
		["Cavalete1"] = ("madeira", 6f),
		["Cavalete2"] = ("madeira", 6f),
		["SM_Mesa1"] = ("plastico", 4.5f),
		["SM_Table02_003"] = ("madeira", 14f),
		["SM_Cadeira1"] = ("plastico", 2.4f),
		["SM_CadeiraDeBar2"] = ("plastico", 2.6f),
		["Garrafa_de_Cerveja"] = ("vidro", 0.6f),
		["Garrafa_de_Cachaca"] = ("vidro", 0.9f),
		["Garrafa3"] = ("vidro", 0.6f),
		["SM_CanecaChopp"] = ("vidro", 0.4f),
		["BandeijaCopos"] = ("vidro", 0.8f),
		["FardoLata"] = ("lata", 4.8f)
	};

	public static int Floating;

	public static int Removed;

	private static readonly List<GameObject> placed = new List<GameObject>();

	private static GameObject Lib(string g)
	{
		if (!libs.TryGetValue(g, out var value))
		{
			value = (libs[g] = Resources.Load<GameObject>("X171/Urbano/" + g));
		}
		return value;
	}

	private static GameObject Make(string g, string name, Transform parent)
	{
		GameObject pf = Lib(g);
		if (!pf)
		{
			return null;
		}
		Transform transform = pf.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault((Transform t) => t.parent == pf.transform && t.name.Split('.')[0] == name);
		if (!transform)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(transform.gameObject, parent);
		gameObject.name = "x171_" + name;
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>();
		foreach (Renderer obj in componentsInChildren)
		{
			obj.shadowCastingMode = ShadowCastingMode.On;
			obj.receiveShadows = true;
		}
		Count[name] = (Count.TryGetValue(name, out var value) ? value : 0) + 1;
		Total++;
		return gameObject;
	}

	private static Bounds Bnd(GameObject g)
	{
		Renderer[] componentsInChildren = g.GetComponentsInChildren<Renderer>();
		Bounds result = ((componentsInChildren.Length != 0) ? componentsInChildren[0].bounds : new Bounds(g.transform.position, Vector3.one * 0.1f));
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			result.Encapsulate(renderer.bounds);
		}
		return result;
	}

	private static GameObject Put(string g, string name, Vector3 p, float yaw, bool fixedObj = false)
	{
		GameObject gameObject = Make(g, name, Root.transform);
		if (!gameObject)
		{
			return null;
		}
		gameObject.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * gameObject.transform.rotation;
		Bounds bounds = Bnd(gameObject);
		gameObject.transform.position += new Vector3(p.x - bounds.center.x, p.y - bounds.min.y + 0.005f, p.z - bounds.center.z);
		bounds = Bnd(gameObject);
		BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
		boxCollider.center = gameObject.transform.InverseTransformPoint(bounds.center);
		boxCollider.size = Vector3.Max(gameObject.transform.InverseTransformVector(bounds.size).Abs3(), Vector3.one * 0.02f);
		gameObject.layer = (fixedObj ? 8 : 0);
		foreach (Transform item in gameObject.transform)
		{
			item.gameObject.layer = gameObject.layer;
		}
		if (!fixedObj && FIS.TryGetValue(name, out (string, float) value))
		{
			FBFisica.Make(gameObject, value.Item1, value.Item2);
		}
		return gameObject;
	}

	private static bool Clear(Vector3 p, float r, float h = 1.4f)
	{
		return !Physics.CheckBox(p + Vector3.up * (h * 0.5f + 0.08f), new Vector3(r, h * 0.5f, r), Quaternion.identity, FBWorld.SolidMask | 1 | 0x400, QueryTriggerInteraction.Ignore);
	}

	private static bool TryBar(Vector3 edge, Vector3 r, float width, Func<float> R, Func<IList<string>, string> Pk)
	{
		Vector3 p = edge + r * Mathf.Min(width * 0.5f, 1.9f);
		if (!FBStreetEvents.Ground(ref p, edge.y, 0.5f) || !Clear(p, 1f))
		{
			return false;
		}
		GameObject gameObject = PutG("Bar", Pk(MESA), p, R() * 90f);
		if (!gameObject)
		{
			return false;
		}
		float y = Bnd(gameObject).max.y;
		int num = 2 + (int)(R() * 3f);
		for (int i = 0; i < num; i++)
		{
			float y2 = (float)i * (360f / (float)num) + R() * 25f;
			Vector3 vector = Quaternion.Euler(0f, y2, 0f) * Vector3.forward;
			Vector3 p2 = p + vector * 0.78f;
			if (FBStreetEvents.Ground(ref p2, p.y, 0.4f) && (bool)PutG("Bar", Pk(CADEIRA), p2, Mathf.Atan2(0f - vector.x, 0f - vector.z) * 57.29578f))
			{
				FBRole.seats.Add(new FBSeat
				{
					p = p2,
					yaw = Mathf.Atan2(0f - vector.x, 0f - vector.z) * 57.29578f,
					kind = "cadeira",
					grp = gameObject
				});
			}
		}
		int j = 0;
		for (int num2 = 2 + (int)(R() * 3f); j < num2; j++)
		{
			Vector3 p3 = p + new Vector3(R() - 0.5f, 0f, R() - 0.5f) * 0.45f;
			p3.y = y;
			Put("Bar", Pk(NA_MESA), p3, R() * 360f);
		}
		int k = 0;
		for (int num3 = 1 + (int)(R() * 3f); k < num3; k++)
		{
			Vector3 p4 = p + Quaternion.Euler(0f, R() * 360f, 0f) * Vector3.forward * (1f + R() * 0.6f);
			if (FBStreetEvents.Ground(ref p4, p.y, 0.4f))
			{
				PutG("Bar", (R() < 0.7f) ? "Garrafa_de_Cerveja" : "FardoLata", p4, R() * 360f);
			}
		}
		return true;
	}

	private static bool Grounded(GameObject go)
	{
		if (!go)
		{
			return false;
		}
		Bounds bounds = Bnd(go);
		float y = bounds.min.y;
		float num = 0f;
		int num2 = 0;
		Vector3[] array = new Vector3[4]
		{
			new Vector3(bounds.min.x, 0f, bounds.min.z),
			new Vector3(bounds.max.x, 0f, bounds.min.z),
			new Vector3(bounds.min.x, 0f, bounds.max.z),
			new Vector3(bounds.max.x, 0f, bounds.max.z)
		};
		Collider[] componentsInChildren = go.GetComponentsInChildren<Collider>();
		Collider[] array2 = componentsInChildren;
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].enabled = false;
		}
		try
		{
			Vector3[] array3 = array;
			foreach (Vector3 b in array3)
			{
				Vector3 vector = Vector3.Lerp(new Vector3(bounds.center.x, 0f, bounds.center.z), b, 0.85f);
				if (Physics.Raycast(new Vector3(vector.x, y + 0.5f, vector.z), Vector3.down, out var hitInfo, 1.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
				{
					num = Mathf.Max(num, y - hitInfo.point.y);
					num2++;
				}
				else
				{
					num = 9f;
				}
			}
		}
		finally
		{
			array2 = componentsInChildren;
			foreach (Collider collider in array2)
			{
				if ((bool)collider)
				{
					collider.enabled = true;
				}
			}
		}
		if (num2 >= 3)
		{
			return num < 0.08f;
		}
		return false;
	}

	private static GameObject PutG(string g, string name, Vector3 p, float yaw, bool fixedObj = false)
	{
		GameObject gameObject = Put(g, name, p, yaw, fixedObj);
		if (!gameObject)
		{
			return null;
		}
		if (!Grounded(gameObject))
		{
			Floating++;
			Removed++;
			Count[name]--;
			Total--;
			UnityEngine.Object.DestroyImmediate(gameObject);
			return null;
		}
		placed.Add(gameObject);
		return gameObject;
	}

	private static string Least(IList<string> l, Func<float> R)
	{
		int i = 2147483647;
		foreach (string item in l)
		{
			i = Mathf.Min(i, Count.TryGetValue(item, out var value) ? value : 0);
		}
		int value2;
		List<string> list = l.Where((string n) => (Count.TryGetValue(n, out value2) ? value2 : 0) == i).ToList();
		return list[Mathf.Min(list.Count - 1, (int)(R() * (float)list.Count))];
	}

	public static void Dress()
	{
		Count.Clear();
		Total = 0;
		Floating = (Removed = 0);
		placed.Clear();
		if ((bool)Root)
		{
			UnityEngine.Object.Destroy(Root);
		}
		if (!On || !FBRoads.Ready || !Lib("Rua"))
		{
			Debug.Log("[FreeBala][vestir] sem objetos do 171 (" + ((!On) ? "desligado" : ((!FBRoads.Ready) ? "sem malha de ruas" : "sem Resources/X171/Urbano")) + ")");
			return;
		}
		Root = new GameObject("FB_Rua171");
		Stopwatch stopwatch = Stopwatch.StartNew();
		string obj = FBSession.map ?? "map";
		uint num = 2166136261u;
		string text = obj;
		foreach (char c in text)
		{
			num = (num ^ c) * 16777619;
		}
		Func<float> rnd = FBVehicles.Seeded(num);
		int num2 = (FBQuality.Mobile ? 140 : 380);
		int num3 = 0;
		int num4 = (FBQuality.Mobile ? 5 : 12);
		int num5 = 0;
		int num6 = 0;
		Vector3 edge6 = default(Vector3);
		Vector3 r6 = default(Vector3);
		bool wall4 = default(bool);
		float width5 = default(float);
		Vector3 edge7 = default(Vector3);
		Vector3 r7 = default(Vector3);
		Vector3 tan7 = default(Vector3);
		bool wall5 = default(bool);
		RaycastHit wh5 = default(RaycastHit);
		float width6 = default(float);
		Vector3 edge8 = default(Vector3);
		Vector3 r8 = default(Vector3);
		Vector3 tan8 = default(Vector3);
		bool wall6 = default(bool);
		foreach (FBSeg item in FBRoads.segs.Where((FBSeg S) => S.kind == 0 && !S.ring && S.len > 12f).ToList())
		{
			if (Total >= num2)
			{
				break;
			}
			num6++;
			if (item.len > 20f && R() < 0.7f && Side(item, item.len - 4.5f, out var edge2, out var _, out var tan2, out var wall2, out var wh2, out var width2) && Clear(edge2, 0.25f, 2.2f))
			{
				PutG("Placas", Least(PLACA_ESQ, R), edge2, yawOf(-tan2), fixedObj: true);
			}
			for (float num7 = 18f + R() * 10f; num7 < item.len - 12f; num7 += 40f + R() * 10f)
			{
				if (Total >= num2)
				{
					break;
				}
				if (!Side(item, num7, out var edge3, out var r3, out var tan3, out var _, out var _, out var _) || !Clear(edge3, 0.35f))
				{
					continue;
				}
				string text2 = Least(LIXEIRA, R);
				if ((bool)PutG("Rua", text2, edge3, yawOf(-r3), text2 == "LixeiraPoste") && R() < 0.3f)
				{
					Vector3 p = edge3 + tan3 * 0.7f;
					FBStreetEvents.Ground(ref p, edge3.y, 0.4f);
					if (Clear(p, 0.3f))
					{
						PutG("Lixo", Pk<string>(new string[3] { "SM_CaixaPapelao1", "SM_CaixaPapelao3", "Papelao" }), p, R() * 360f);
					}
				}
			}
			if (item.len > 90f && R() < 0.55f && Side(item, item.len / 2f, out var edge4, out var r4, out var tan4, out wall2, out wh2, out var width4) && width4 > 2.6f)
			{
				Vector3 p2 = edge4 + r4 * 0.8f;
				if (Clear(p2, 1.3f, 2.4f) && PutG("Rua", (R() < 0.5f) ? "PontoOnibus" : "SM_PontoOnibus1", p2, yawOf(-r4), fixedObj: true) != null)
				{
					PutG("Placas", "PlacasAuxiliares_PontoOnibus", edge4 - tan4 * 2.2f, yawOf(-tan4), fixedObj: true);
				}
			}
			if (item.len > 30f && num6 % 4 == 0 && Side(item, 9f + R() * 6f, out var edge5, out var r5, out var tan5, out wall2, out wh2, out width2) && Clear(edge5, 0.5f, 2.2f) && (bool)PutG("Rua", (R() < 0.75f) ? Least(new string[3] { "SM_Orelhao1", "SM_Orelhao2", "SM_Orelhao3" }, R) : "SM_Caixadoscorreios1", edge5, yawOf(-r5), fixedObj: true))
			{
				num5++;
			}
			if (item.len > 24f && num3 < num4 && R() < 0.35f && Side(item, item.len - (4f + R() * 8f), out edge6, out r6, out var _, out wall4, out var _, out width5) && wall4 && width5 > 2.6f)
			{
				if (TryBar(edge6, r6, width5, R, Pk<string>))
				{
					num3++;
				}
			}
			else if (item.len > 24f && R() < 0.3f && Side(item, item.len - (6f + R() * 4f), out edge7, out r7, out tan7, out wall5, out wh5, out width6) && wall5 && width6 > 1.8f)
			{
				Vector3 p3 = wh5.point - r7 * 0.55f;
				p3.y = edge7.y;
				if (FBStreetEvents.Ground(ref p3, edge7.y, 0.6f) && Clear(p3, 0.5f))
				{
					int num8 = 2 + (int)(R() * 2f);
					HashSet<string> hashSet = new HashSet<string>();
					for (int j = 0; j < num8; j++)
					{
						string text3 = Least(LIXO, R);
						if (hashSet.Add(text3))
						{
							Vector3 p4 = p3 + tan7 * (((float)j - (float)num8 / 2f) * 0.65f);
							if (FBStreetEvents.Ground(ref p4, p3.y, 0.5f))
							{
								PutG("Lixo", text3, p4, R() * 360f);
							}
						}
					}
				}
			}
			if (!(R() < 0.45f && Side(item, item.len * (0.3f + R() * 0.4f), out edge8, out r8, out tan8, out wall6, out wh2, out width2) && wall6))
			{
				continue;
			}
			int k = 0;
			for (int num9 = 1 + (int)(R() * 3f); k < num9; k++)
			{
				float num10 = 2f + R() * 1.5f;
				if (Physics.Raycast(edge8 + tan8 * ((float)k * 2.4f) + Vector3.up * num10, r8, out var hitInfo, 7f, 256, QueryTriggerInteraction.Ignore) && !(Mathf.Abs(hitInfo.normal.y) > 0.3f))
				{
					GameObject gameObject = Make("Comercio", "ArCondicionado_Exterior", Root.transform);
					if ((bool)gameObject)
					{
						gameObject.transform.rotation = Quaternion.LookRotation(hitInfo.normal.Flat().normalized, Vector3.up) * gameObject.transform.rotation;
						Bounds bounds = Bnd(gameObject);
						Vector3 point = hitInfo.point;
						Vector3 normal = hitInfo.normal;
						tan5 = bounds.extents;
						Vector3 vector = point + normal * (tan5.magnitude * 0.25f);
						gameObject.transform.position += vector - bounds.center;
						gameObject.layer = 8;
					}
				}
			}
		}
		Physics.SyncTransforms();
		Debug.Log($"[FreeBala][vestir] {Total} objetos do 171 em {stopwatch.ElapsedMilliseconds} ms · botecos {num3} · orelhões/correio {num5} · tirados por flutuar {Floating} · " + string.Join(", ", from kv in (from kv in Count
				where kv.Value > 0
				orderby kv.Value descending
				select kv).Take(14)
			select kv.Key + " " + kv.Value));
		T Pk<T>(IList<T> l)
		{
			return l[Mathf.Min(l.Count - 1, (int)(R() * (float)l.Count))];
		}
		float R()
		{
			return rnd();
		}
		static bool Side(FBSeg S, float s, out Vector3 edge, out Vector3 r, out Vector3 tan, out bool wall, out RaycastHit wh, out float width)
		{
			Vector3 vector2 = FBRoads.At(S, s, out tan);
			tan = tan.Flat().normalized;
			r = new Vector3(tan.z, 0f, 0f - tan.x);
			edge = default(Vector3);
			wall = false;
			wh = default(RaycastHit);
			width = 0f;
			if (tan.sqrMagnitude < 0.01f)
			{
				return false;
			}
			float num11 = -1f;
			for (float num12 = 0.75f; num12 < 8f; num12 += 0.25f)
			{
				if (!FBRoads.IsRoad(vector2 + r * num12))
				{
					num11 = num12;
					break;
				}
			}
			if (num11 < 1f)
			{
				return false;
			}
			edge = vector2 + r * (num11 + 0.45f);
			if (!FBStreetEvents.Ground(ref edge, vector2.y, 0.6f) || FBRoads.IsRoad(edge) || !FBNav.InsideNear(edge, 3f))
			{
				return false;
			}
			wall = Physics.Raycast(edge + Vector3.up * 1.2f, r, out wh, 7f, 256, QueryTriggerInteraction.Ignore) && Mathf.Abs(wh.normal.y) < 0.3f;
			width = (wall ? wh.distance : 7f);
			return true;
		}
		static float yawOf(Vector3 v)
		{
			return Mathf.Atan2(v.x, v.z) * 57.29578f;
		}
	}
}
}
