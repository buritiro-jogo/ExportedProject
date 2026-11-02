using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeBala
{
public static class FBRole
{
	public static readonly List<FBCharacter> npcs = new List<FBCharacter>();

	public static readonly List<FBSeat> seats = new List<FBSeat>();

	private static readonly List<string> types = new List<string>();

	private static readonly List<FBCar> walls = new List<FBCar>();

	private static float wallT;

	private static FBMatch G;

	public static FBCar roleCar;

	public static (Vector3 p, float yaw)? spot;

	public const float WallGap = 260f;

	private static readonly HashSet<FBCar> wallMuted = new HashSet<FBCar>();

	public static int WallMutes;

	public static int WallTicks;

	public static string WallDiag;

	private static float recycleT;

	public static IReadOnlyList<FBCar> CityWalls => walls;

	public static void Setup(FBMatch g)
	{
		G = g;
		npcs.Clear();
		seats.Clear();
		walls.Clear();
		wallMuted.Clear();
		types.Clear();
		roleCar = null;
		types.AddRange(from _ in FBNpcAnims.Roster(FBSession.skin)
			orderby Random.value
			select _);
		if ((bool)FBMatch.Player)
		{
			FBNav.SetAnchor(FBMatch.Player.Pos);
		}
		int num = 0;
		Transform[] array = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
		foreach (Transform transform in array)
		{
			if (transform.gameObject.layer == 8 && FBRoads.IsCoverContainer(transform))
			{
				transform.gameObject.SetActive(value: false);
				num++;
			}
		}
		if (num > 0)
		{
			Physics.SyncTransforms();
			Debug.Log($"[FreeBala][rolê] {num} contêineres de cobertura fora do De Rolê");
		}
		FBTransito.BuildRoads();
		FBPedNav.Build(FBSession.map);
		FindSeats();
		int count = seats.Count;
		seats.RemoveAll((FBSeat S) => !FBNav.InsideNear(S.p, 2f));
		if (seats.Count < count)
		{
			Debug.Log($"[FreeBala][rolê] {count - seats.Count} assentos fora da área jogável ignorados");
		}
		FBVestirRua.Dress();
		int num2 = (FBQuality.Mobile ? 18 : ((FBQuality.Level >= FBQLevel.Alto) ? 46 : 32));
		if (types.Count > num2 + 4)
		{
			types.RemoveRange(num2 + 4, types.Count - num2 - 4);
		}
		int num3 = Mathf.Min(4, types.Count / 2);
		for (int j = 0; j < types.Count - num3; j++)
		{
			Vector3? vector = FBTransito.SidewalkPoint(null, 0f);
			if (vector.HasValue)
			{
				SpawnNpc(types[j], vector.Value, "n" + j);
			}
		}
		List<object> list = (from _ in (from s in seats
				where s.kind == "cadeira"
				select s.grp).Distinct()
			orderby Random.value
			select _).ToList();
		foreach (object gp in list.Take((list.Count + 2) / 3))
		{
			foreach (FBSeat item in seats.Where((FBSeat s) => s.grp == gp))
			{
				FBCharacter fBCharacter = npcs.FirstOrDefault(delegate(FBCharacter o)
				{
					if (o.GetComponent<FBRoleNpc>().st == "walk" && o.GetComponent<FBRoleNpc>().seat == null)
					{
						FBCharacter.NpcSet npcSet = o.npcSet;
						if (npcSet == null)
						{
							return false;
						}
						return npcSet.sit.Count > 0;
					}
					return false;
				});
				if ((bool)fBCharacter)
				{
					fBCharacter.GetComponent<FBRoleNpc>().SeatNow(item, enter: false);
				}
			}
		}
		FBRoleHeli.Spawn();
		FBCharacter me = FBMatch.Player;
		FBCar fBCar = (from c in FBVehicles.list
			where (bool)c && !c.ie
			orderby (!me) ? 0f : (c.transform.position - me.Pos).sqrMagnitude
			select c).FirstOrDefault();
		if ((bool)me && (bool)fBCar && Vector3.Distance(fBCar.transform.position, me.Pos) < 60f)
		{
			spot = (fBCar.transform.position, fBCar.transform.eulerAngles.y);
			FBVehicles.list.Remove(fBCar);
			Object.Destroy(fBCar.gameObject);
		}
		else if ((bool)me)
		{
			spot = (me.Pos + me.transform.forward * 5f, me.Yaw + 90f);
		}
		FBMediaLib.Refresh();
		List<FBTrack> list2 = FBMediaLib.lib.Where((FBTrack t) => t.s <= 31457280).ToList();
		if (list2.Count > 0)
		{
			foreach (FBCar car in from c in FBVehicles.list
				where (bool)c && (bool)c.soundWallNode
				select c into _
				orderby Random.value
				select _)
			{
				if (walls.Count >= 2)
				{
					break;
				}
				if (!walls.Any((FBCar w) => (w.transform.position - car.transform.position).magnitude < 260f))
				{
					walls.Add(car);
					FBParedao.Play(car, FBUtil.Pick(list2), Random.Range(0f, 40f));
				}
			}
		}
		FBTransito.Start(12);
		FBGarage.Open();
	}

	private static void WallSpacing(List<FBTrack> tracks)
	{
		WallTicks++;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < walls.Count; i++)
		{
			FBCar car = walls[i];
			if ((bool)car && FBParedao.CanControl(car))
			{
				Vector3 p = car.transform.position;
				bool flag = FBParedao.List().Any((FBParedao.Wall o) => (bool)o.car && o.car != car && o.on && (o.car.transform.position - p).magnitude < 260f && (!walls.Contains(o.car) || walls.IndexOf(o.car) < i));
				bool flag2 = FBParedao.State(car)?.on ?? false;
				stringBuilder.Append($"{car.id}[{i}] ligado {flag2} conflito {flag} · ");
				if (flag && flag2)
				{
					FBParedao.SetOn(car, on: false, quiet: true);
					wallMuted.Add(car);
					WallMutes++;
				}
				else if (!flag && !flag2 && wallMuted.Contains(car) && tracks.Count > 0)
				{
					wallMuted.Remove(car);
					FBParedao.Play(car, FBUtil.Pick(tracks), Random.Range(0f, 40f));
				}
			}
		}
		WallDiag = $"{walls.Count} da cidade · " + stringBuilder;
	}

	public static FBCharacter SpawnNpc(string id, Vector3 p, string seed)
	{
		FBCharacter fBCharacter = G.Spawn(id, null, 2, p, player: false, bot: false, civ: true);
		FBCivilian component = fBCharacter.GetComponent<FBCivilian>();
		if ((bool)component)
		{
			Object.Destroy(component);
		}
		fBCharacter.hp = (fBCharacter.maxHp = 60f);
		FBNpcAnims.Apply(fBCharacter, seed);
		fBCharacter.SetWeaponVisible(v: false);
		fBCharacter.combat = false;
		fBCharacter.Yaw = Random.Range(0f, 360f);
		fBCharacter.gameObject.AddComponent<FBRoleNpc>();
		npcs.Add(fBCharacter);
		return fBCharacter;
	}

	public static void Tick(float dt)
	{
		FBTransito.Update(dt);
		FBRoleHeli.Tick(dt);
		Keyboard current = Keyboard.current;
		if (current != null && current.oKey.wasPressedThisFrame && !FBGarage.Open_)
		{
			FBOficina.Toggle();
		}
		if ((wallT += dt) > 0.5f)
		{
			wallT = 0f;
			List<FBTrack> lib = FBMediaLib.lib;
			foreach (FBCar wall2 in walls)
			{
				FBParedao.Wall wall = FBParedao.State(wall2);
				if ((bool)wall2 && wall != null && wall.on && wall.ended && lib.Count > 0 && FBParedao.CanControl(wall2))
				{
					FBParedao.Play(wall2, FBUtil.Pick(lib));
				}
			}
			WallSpacing(lib);
		}
		npcs.RemoveAll((FBCharacter n) => !n);
		if ((recycleT -= dt) <= 0f)
		{
			recycleT = 1.2f;
			Recycle();
		}
	}

	private static void Recycle()
	{
		FBCharacter player = FBMatch.Player;
		Camera main = Camera.main;
		if (!player || !main || npcs.Count == 0)
		{
			return;
		}
		Vector3 pp = player.Pos;
		Vector3 normalized = main.transform.forward.Flat().normalized;
		int num = 0;
		foreach (FBCharacter item in (from o in npcs
			where (bool)o && o.alive && !o.vehiclePose
			orderby (o.Pos - pp).sqrMagnitude descending
			select o).Take(4))
		{
			FBRoleNpc component = item.GetComponent<FBRoleNpc>();
			if (!component || component.st != "walk" || (item.Pos - pp).magnitude < 130f)
			{
				continue;
			}
			for (int i = 0; i < 6; i++)
			{
				Vector3? vector = FBTransito.SidewalkPoint(pp, 110f);
				if (!vector.HasValue)
				{
					break;
				}
				Vector3 vector2 = (vector.Value - pp).Flat();
				float magnitude = vector2.magnitude;
				if (!(magnitude < 55f) && (!(Vector3.Dot(vector2 / magnitude, normalized) > 0.45f) || !(magnitude < 85f)))
				{
					component.Relocate(vector.Value);
					num++;
					break;
				}
			}
			if (num >= 2)
			{
				break;
			}
		}
	}

	private static void FindSeats()
	{
		Regex rxProp = new Regex("banco|mesas_plastico", RegexOptions.IgnoreCase);
		Regex rxNo = new Regex("sign|placa", RegexOptions.IgnoreCase);
		Transform[] array = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
		foreach (Transform transform in array)
		{
			if (transform.gameObject.layer != 8 || !IsProp(transform) || IsProp(transform.parent))
			{
				continue;
			}
			Renderer[] componentsInChildren = transform.GetComponentsInChildren<Renderer>();
			if (componentsInChildren.Length == 0)
			{
				continue;
			}
			Bounds bounds = componentsInChildren[0].bounds;
			Renderer[] array2 = componentsInChildren;
			foreach (Renderer renderer in array2)
			{
				bounds.Encapsulate(renderer.bounds);
			}
			Vector3 center = bounds.center;
			Vector3 sz = bounds.size;
			Vector3 normalized = (transform.rotation * Vector3.right).Flat().normalized;
			Vector3 normalized2 = (transform.rotation * Vector3.forward).Flat().normalized;
			if (transform.name.ToLowerInvariant().Contains("banco"))
			{
				Vector3 vector = ((Ext(normalized) >= Ext(normalized2)) ? normalized : normalized2);
				Vector3 vector2 = ((vector == normalized) ? normalized2 : normalized);
				float num = Mathf.Min(0.7f, Ext(vector2)) / 2f;
				int[] array3 = new int[2] { -1, 1 };
				foreach (int num2 in array3)
				{
					float[] array4 = new float[2] { -0.45f, 0.45f };
					foreach (float num3 in array4)
					{
						Vector3 vector3 = vector2 * num2;
						Vector3 p = center + vector * num3 + vector3 * (num * 0.35f);
						p.y = bounds.min.y;
						seats.Add(new FBSeat
						{
							p = p,
							yaw = Mathf.Atan2(vector3.x, vector3.z) * 57.29578f,
							kind = "banco",
							grp = transform
						});
					}
				}
			}
			else
			{
				FindChairs(transform);
			}
			float Ext(Vector3 d)
			{
				return Mathf.Abs(sz.x * d.x) + Mathf.Abs(sz.z * d.z);
			}
		}
		bool IsProp(Transform t)
		{
			if ((bool)t && rxProp.IsMatch(t.name))
			{
				return !rxNo.IsMatch(t.name);
			}
			return false;
		}
	}

	private static void FindChairs(Transform prop)
	{
		List<(Vector3, float, float)> list = new List<(Vector3, float, float)>();
		float num = 1E+09f;
		MeshFilter[] componentsInChildren = prop.GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			Mesh sharedMesh = meshFilter.sharedMesh;
			if (!sharedMesh || !sharedMesh.isReadable)
			{
				continue;
			}
			Matrix4x4 localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
			Vector3[] vertices = sharedMesh.vertices;
			int[] triangles = sharedMesh.triangles;
			for (int j = 0; j + 2 < triangles.Length; j += 3)
			{
				Vector3 vector = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[j]]);
				Vector3 vector2 = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[j + 1]]);
				Vector3 vector3 = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[j + 2]]);
				Vector3 vector4 = Vector3.Cross(vector2 - vector, vector3 - vector);
				float magnitude = vector4.magnitude;
				if (!(magnitude < 1E-07f))
				{
					vector4 /= magnitude;
					Vector3 item = (vector + vector2 + vector3) / 3f;
					list.Add((item, vector4.y, magnitude));
					num = Mathf.Min(num, item.y);
				}
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		Dictionary<(int, int), List<int>> dictionary = new Dictionary<(int, int), List<int>>();
		for (int k = 0; k < list.Count; k++)
		{
			float num2 = list[k].Item1.y - num;
			if (!(Mathf.Abs(list[k].Item2) < 0.85f) && !(num2 < 0.33f) && !(num2 > 0.6f))
			{
				(int, int) key = (Mathf.FloorToInt(list[k].Item1.x / 0.05f), Mathf.FloorToInt(list[k].Item1.z / 0.05f));
				if (!dictionary.TryGetValue(key, out var value))
				{
					value = (dictionary[key] = new List<int>());
				}
				value.Add(k);
			}
		}
		HashSet<(int, int)> hashSet = new HashSet<(int, int)>();
		List<Vector3> list3 = new List<Vector3>();
		foreach (var key3 in dictionary.Keys)
		{
			if (hashSet.Contains(key3))
			{
				continue;
			}
			Stack<(int, int)> stack = new Stack<(int, int)>();
			stack.Push(key3);
			hashSet.Add(key3);
			List<int> list4 = new List<int>();
			while (stack.Count > 0)
			{
				(int, int) key2 = stack.Pop();
				list4.AddRange(dictionary[key2]);
				for (int m = -1; m <= 1; m++)
				{
					for (int n = -1; n <= 1; n++)
					{
						(int, int) tuple = (key2.Item1 + m, key2.Item2 + n);
						if (dictionary.ContainsKey(tuple) && hashSet.Add(tuple))
						{
							stack.Push(tuple);
						}
					}
				}
			}
			float num3 = 1E+09f;
			float num4 = -1E+09f;
			float num5 = 1E+09f;
			float num6 = -1E+09f;
			float num7 = 0f;
			float num8 = 0f;
			foreach (int item3 in list4)
			{
				Vector3 item2 = list[item3].Item1;
				num3 = Mathf.Min(num3, item2.x);
				num4 = Mathf.Max(num4, item2.x);
				num5 = Mathf.Min(num5, item2.z);
				num6 = Mathf.Max(num6, item2.z);
				num7 += item2.y * list[item3].Item3;
				num8 += list[item3].Item3;
			}
			float num9 = num4 - num3;
			float num10 = num6 - num5;
			if (!(num9 < 0.25f) && !(num10 < 0.25f) && !(num9 > 0.7f) && !(num10 > 0.7f))
			{
				list3.Add(new Vector3((num3 + num4) / 2f, num7 / num8, (num5 + num6) / 2f));
			}
		}
		foreach (Vector3 S in list3)
		{
			if (list.Any<(Vector3, float, float)>(delegate((Vector3 c, float ny, float ar) l)
			{
				float num15 = l.c.y - S.y;
				return num15 > 0.08f && num15 < 0.4f && Mathf.Abs(l.ny) > 0.85f && new Vector2(l.c.x - S.x, l.c.z - S.z).magnitude < 0.15f;
			}))
			{
				continue;
			}
			float num11 = 0f;
			float num12 = 0f;
			float num13 = 0f;
			foreach (var item4 in list)
			{
				float num14 = item4.Item1.y - num;
				if (!(num14 < 0.76f) && !(num14 > 1f) && !(Mathf.Abs(item4.Item2) > 0.5f) && !(new Vector2(item4.Item1.x - S.x, item4.Item1.z - S.z).magnitude > 0.45f))
				{
					num11 += item4.Item1.x * item4.Item3;
					num12 += item4.Item1.z * item4.Item3;
					num13 += item4.Item3;
				}
			}
			if (!(num13 <= 0f))
			{
				Vector3 vector5 = new Vector3(S.x - num11 / num13, 0f, S.z - num12 / num13);
				if (!(vector5.sqrMagnitude < 0.0001f))
				{
					vector5.Normalize();
					seats.Add(new FBSeat
					{
						p = new Vector3(S.x, num, S.z),
						yaw = Mathf.Atan2(vector5.x, vector5.z) * 57.29578f,
						kind = "cadeira",
						grp = prop
					});
				}
			}
		}
	}

	public static Vector3 SeatApproach(FBSeat S)
	{
		Vector3 vector = Quaternion.Euler(0f, S.yaw, 0f) * Vector3.forward;
		if (S.kind != "cadeira")
		{
			return S.p + vector * 0.6f;
		}
		Vector3 r = new Vector3(vector.z, 0f, 0f - vector.x);
		List<FBSeat> others = seats.Where((FBSeat o) => o.grp == S.grp && o != S).ToList();
		return S.p + r * ((Clear(1) >= Clear(-1)) ? 0.62f : (-0.62f)) - vector * 0.12f;
		float Clear(int sd)
		{
			if (others.Count != 0)
			{
				return Mathf.Min(9f, others.Min((FBSeat o) => new Vector2(S.p.x + r.x * (float)sd * 0.62f - o.p.x, S.p.z + r.z * (float)sd * 0.62f - o.p.z).magnitude));
			}
			return 9f;
		}
	}

	public static IEnumerable<string> Dances()
	{
		List<string> list = (from n in FBEmotes.DanceIds()
			where (bool)FBRegistry.I.Clip(n) && !Regex.IsMatch(n, "cut|throat|kill|bow|apocalypse", RegexOptions.IgnoreCase)
			select n).ToList();
		if (list.Count <= 0)
		{
			return new List<string> { "FF_BR_Passinho" };
		}
		return list;
	}
}
}
