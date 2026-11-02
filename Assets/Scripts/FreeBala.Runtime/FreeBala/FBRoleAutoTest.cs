using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBRoleAutoTest : MonoBehaviour
{
	private string[] maps;

	private float secs = 40f;

	private bool night;

	private string dir;

	private Action restore;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbrole");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			GameObject obj = new GameObject("FB_RoleTeste");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBRoleAutoTest fBRoleAutoTest = obj.AddComponent<FBRoleAutoTest>();
			fBRoleAutoTest.maps = commandLineArgs[num + 1].Split(',');
			if (num + 2 < commandLineArgs.Length && float.TryParse(commandLineArgs[num + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				fBRoleAutoTest.secs = result;
			}
			fBRoleAutoTest.night = Array.IndexOf(commandLineArgs, "-fbnight") >= 0;
		}
	}

	private void OnApplicationQuit()
	{
		restore?.Invoke();
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][rolê-teste] " + s);
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		FBInput.TestMove = Vector2.zero;
		yield return new WaitForSeconds(3f);
		string[] array = maps;
		foreach (string map in array)
		{
			GameData.Load();
			FBSession.Load();
			string keepMap = FBSession.map;
			string keepMode = FBSession.mode;
			(string time, string weather) keepW = FBSession.WeatherFor(map);
			FBSession.map = map;
			FBSession.mode = "role";
			FBSession.online = false;
			FBSession.view = "tps";
			if (night)
			{
				FBSession.SetWeather(map, "noite", "limpo");
			}
			FBSession.Save();
			restore = delegate
			{
				FBSession.SetWeather(map, keepW.time, keepW.weather);
				FBSession.map = keepMap;
				FBSession.mode = keepMode;
				FBSession.Save();
			};
			Log("carregando " + map + (night ? " (noite)" : ""));
			AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + map);
			if (op == null)
			{
				Log("FALHA: cena não existe");
				continue;
			}
			while (!op.isDone)
			{
				yield return null;
			}
			for (float w = 0f; w < 5f; w += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			FBCharacter player = FBMatch.Player;
			FBCar c0 = (player ? FBVehicles.CarOf(player) : null);
			Log(string.Format("{0}: garagem aberta {1} · jogador já dentro do carro {2} · cursor {3} ({4})", map, FBGarage.Open_, c0 ? c0.id : "NÃO", Cursor.visible ? "visível" : "ESCONDIDO", Cursor.lockState));
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "r38_" + map + "_garagem.png"));
			yield return null;
			yield return null;
			if ((bool)c0)
			{
				Vector3 p0 = c0.transform.position;
				FBInput.TestMove = Vector2.up;
				for (float w = 0f; w < 1.5f; w += Time.unscaledDeltaTime)
				{
					yield return null;
				}
				FBInput.TestMove = Vector2.zero;
				Log($"{map}: com a garagem aberta, acelerando 1,5 s o carro andou {(c0 ? (c0.transform.position - p0).Flat().magnitude : (-1f)):0.00} m (esperado ~0)");
			}
			FBGarage.AutoPick();
			for (float w = 0f; w < 2f; w += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			FBCar fBCar = (FBMatch.Player ? FBVehicles.CarOf(FBMatch.Player) : null);
			Log(string.Format("{0}: depois de escolher: dentro do carro {1} · garagem aberta {2} · cursor {3}", map, fBCar ? fBCar.id : "NÃO", FBGarage.Open_, Cursor.visible ? "visível" : "escondido"));
			int num = 0;
			Transform[] array2 = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (Transform transform in array2)
			{
				if (transform.gameObject.layer == 8 && FBRoads.IsCoverContainer(transform))
				{
					num += ((!transform.gameObject.activeInHierarchy) ? 1 : 1000);
				}
			}
			Log(string.Format("{0}: malha {1} · contêineres de cobertura: {2} desligados, {3} ainda ligados", map, FBRoads.Ready ? (FBRoads.segs.Count + " segmentos, " + FBRoads.spots.Count + " vagas") : "AUSENTE", num % 1000, num / 1000));
			yield return Traffic(map);
			List<FBCharacter> list = FBRole.npcs.Where((FBCharacter n) => (bool)n && n.alive && !FBNav.InsideNear(n.Pos, 2f)).ToList();
			Log($"{map}: NPCs fora da área jogável (atrás do muro): {list.Count} de {FBRole.npcs.Count((FBCharacter n) => n)}");
			GameObject cg2 = new GameObject("ForaCam");
			Camera cam2 = cg2.AddComponent<Camera>();
			cam2.depth = 100f;
			cam2.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
			cam2.fieldOfView = 60f;
			cam2.farClipPlane = 800f;
			int fi = 0;
			foreach (FBCharacter item in list.Take(4))
			{
				FBRoleNpc component = item.GetComponent<FBRoleNpc>();
				NavMeshHit hit;
				bool flag = NavMesh.SamplePosition(item.Pos, out hit, 3f, -1);
				Log(string.Format("{0}:   fora #{1}: {2} em {3} · estado {4} · navmesh a {5} · motorista/pedestre do trânsito {6} · sentado {7}", map, fi, item.skinId, item.Pos, component?.st, flag ? ((hit.position - item.Pos).magnitude.ToString("0.00") + " m") : ">3 m", component == null || !component.enabled, component?.seat != null));
				cam2.transform.position = item.Pos + new Vector3(0f, 14f, -9f);
				cam2.transform.LookAt(item.Pos);
				for (int i = 0; i < 4; i++)
				{
					yield return null;
				}
				ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"r35_{map}_fora{fi++}.png"));
				yield return null;
				yield return null;
			}
			UnityEngine.Object.Destroy(cg2);
			yield return Shots(map);
			yield return Paredao(map);
			yield return Damage(map);
			if (map == maps[0])
			{
				yield return Round35(map);
			}
			restore?.Invoke();
			restore = null;
		}
		FBInput.TestMove = null;
		Log("fim");
		Application.Quit(0);
	}

	private IEnumerator Traffic(string map)
	{
		int samples = 0;
		int overlaps = 0;
		int offroad = 0;
		int offlane = 0;
		int pedRoad = 0;
		int pedTot = 0;
		int moving = 0;
		float vSum = 0f;
		Dictionary<object, float> stuck = new Dictionary<object, float>();
		Dictionary<string, int> seenModes = new Dictionary<string, int>();
		int lit = 0;
		int brakeStill = 0;
		int stillCount = 0;
		Dictionary<string, int> ovKinds = new Dictionary<string, int>();
		for (float t = 0f; t < secs; t += 0.25f)
		{
			yield return new WaitForSeconds(0.25f);
			samples++;
			List<FBTransito.Car> list = FBTransito.List.Where((FBTransito.Car c) => (bool)c.car && c.mode != "walk").ToList();
			foreach (FBTransito.Car item in list)
			{
				seenModes[item.mode] = ((!seenModes.TryGetValue(item.mode, out var value)) ? 1 : (value + 1));
				if (item.mode != "drive")
				{
					continue;
				}
				vSum += item.v;
				moving++;
				if (!FBRoads.IsRoad(item.pos))
				{
					offroad++;
				}
				if (item.ri < item.route.Count)
				{
					FBRoads.Project(FBRoads.segs[item.route[item.ri]], item.pos, out var _, out var dist);
					if (dist > 1.6f && Mathf.Abs(item.lat) < 0.3f)
					{
						offlane++;
					}
				}
				if (item.v < 0.3f)
				{
					stuck[item] = (stuck.TryGetValue(item, out var value2) ? value2 : 0f) + 0.25f;
					stillCount++;
					if (item.car.lBrake)
					{
						brakeStill++;
					}
				}
				else
				{
					stuck[item] = 0f;
				}
				if (night && item.car.aiLights)
				{
					lit++;
				}
			}
			foreach (FBTransito.Car item2 in list)
			{
				foreach (FBCar o in FBVehicles.list)
				{
					if ((bool)o && !(o == item2.car) && !((o.transform.position - item2.car.transform.position).sqrMagnitude > 64f) && Overlap(item2.car, o))
					{
						overlaps++;
						FBTransito.Car car = list.Find((FBTransito.Car x) => x.car == o);
						string key = item2.mode + "×" + ((car != null) ? car.mode : ((!o.driver) ? "parado" : (o.driver.isPlayer ? "jogador" : "motorista")));
						ovKinds[key] = ((!ovKinds.TryGetValue(key, out var value3)) ? 1 : (value3 + 1));
					}
				}
			}
			foreach (FBCharacter npc in FBRole.npcs)
			{
				if ((bool)npc && npc.alive)
				{
					pedTot++;
					if (FBRoads.IsRoad(npc.Pos) && npc.velocity.sqrMagnitude < 0.04f)
					{
						pedRoad++;
					}
				}
			}
		}
		int num = stuck.Values.Count((float v) => v > 20f);
		Log(string.Format("{0}: trânsito {1} carros · {2}· v média {3:0} km/h reais · ", map, FBTransito.List.Count, seenModes.Select((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value).Aggregate("", (string a, string b) => a + b + " "), (moving > 0) ? (vSum / (float)moving * 3.6f / 1.2f) : 0f) + string.Format("sobreposições {0}{1} · fora do asfalto {2}/{3} · longe da faixa {4} · travados >20 s {5} · parados com freio {6}/{7}", overlaps, (ovKinds.Count > 0) ? (" (" + string.Join(", ", ovKinds.Select((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value)) + ")") : "", offroad, moving, offlane, num, brakeStill, stillCount) + (night ? $" · com luz {lit}" : ""));
		Log($"{map}: pedestres amostras {pedTot} · parados no asfalto {pedRoad} ({((pedTot > 0) ? (100f * (float)pedRoad / (float)pedTot) : 0f):0.0} %)");
	}

	private static bool Overlap(FBCar a, FBCar b)
	{
		Vector2[] array = C(a);
		Vector2[] array2 = C(b);
		Vector2[][] array3 = new Vector2[2][] { array, array2 };
		foreach (Vector2[] array4 in array3)
		{
			for (int j = 0; j < 4; j++)
			{
				Vector2 vector = array4[(j + 1) % 4] - array4[j];
				Vector2 rhs = new Vector2(0f - vector.y, vector.x);
				float num = 3.4028235E+38f;
				float num2 = -3.4028235E+38f;
				float num3 = 3.4028235E+38f;
				float num4 = -3.4028235E+38f;
				Vector2[] array5 = array;
				for (int k = 0; k < array5.Length; k++)
				{
					float b2 = Vector2.Dot(array5[k], rhs);
					num = Mathf.Min(num, b2);
					num2 = Mathf.Max(num2, b2);
				}
				array5 = array2;
				for (int k = 0; k < array5.Length; k++)
				{
					float b3 = Vector2.Dot(array5[k], rhs);
					num3 = Mathf.Min(num3, b3);
					num4 = Mathf.Max(num4, b3);
				}
				if (num2 < num3 || num4 < num)
				{
					return false;
				}
			}
		}
		return true;
		static Vector2[] C(FBCar c)
		{
			Bounds lbox = c.lbox;
			Vector3 vector2 = lbox.extents * c.scale * 0.92f;
			Vector3 vector3 = c.transform.TransformPoint(lbox.center);
			Vector3 forward = c.transform.forward;
			forward.y = 0f;
			forward.Normalize();
			Vector3 vector4 = new Vector3(forward.z, 0f, 0f - forward.x);
			Vector2 vector5 = new Vector2(vector3.x, vector3.z);
			Vector2 vector6 = new Vector2(forward.x, forward.z) * vector2.z;
			Vector2 vector7 = new Vector2(vector4.x, vector4.z) * vector2.x;
			return new Vector2[4]
			{
				vector5 + vector6 + vector7,
				vector5 + vector6 - vector7,
				vector5 - vector6 - vector7,
				vector5 - vector6 + vector7
			};
		}
	}

	private IEnumerator Shots(string map)
	{
		GameObject cg = new GameObject("RoleCam");
		Camera cam = cg.AddComponent<Camera>();
		cam.depth = 100f;
		if ((bool)Camera.main)
		{
			cam.cullingMask = Camera.main.cullingMask;
		}
		cam.fieldOfView = 55f;
		cam.farClipPlane = 1500f;
		cg.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
		List<FBTransito.Car> drive = FBTransito.List.Where((FBTransito.Car c) => (bool)c.car && c.mode == "drive").ToList();
		for (int i = 0; i < Mathf.Min(3, drive.Count); i++)
		{
			FBTransito.Car c2 = drive[i * drive.Count / Mathf.Max(1, Mathf.Min(3, drive.Count))];
			if ((bool)c2.car)
			{
				Vector3 pos2 = c2.pos;
				yield return Shot(pos2 + Vector3.up * 38f - c2.fwd * 22f, pos2 + c2.fwd * 12f, "cima" + i);
				pos2 = c2.pos;
				yield return Shot(pos2 - c2.fwd * 9f + Vector3.up * 3.2f, pos2 + c2.fwd * 10f, "atras" + i);
				pos2 = c2.pos;
				yield return Shot(pos2 + c2.fwd * 8f + Vector3.up * 1.6f + new Vector3(c2.fwd.z, 0f, 0f - c2.fwd.x) * 2f, pos2, "frente" + i);
			}
		}
		List<FBCharacter> list = FBRole.npcs.Where((FBCharacter n) => (bool)n && n.alive).ToList();
		if (list.Count > 0)
		{
			Vector3 pos3 = list[list.Count / 2].Pos;
			yield return Shot(pos3 + new Vector3(14f, 22f, -14f), pos3, "pedestres");
		}
		UnityEngine.Object.Destroy(cg);
		IEnumerator Shot(Vector3 pos, Vector3 look, string name)
		{
			cam.transform.position = pos;
			cam.transform.LookAt(look);
			for (int j = 0; j < 4; j++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "role_" + map + (night ? "_noite" : "") + "_" + name + ".png"));
			yield return null;
			yield return null;
		}
	}

	private IEnumerator Damage(string map)
	{
		FBCar car = FBRole.roleCar;
		if (!car)
		{
			Log(map + ": dano: sem carro do rolê");
			yield break;
		}
		FBCharacter player = FBMatch.Player;
		if ((bool)player && (bool)FBVehicles.CarOf(player))
		{
			car.v = 0f;
			FBDrive.Exit(player, force: true);
		}
		car.v = 0f;
		for (float w3 = 0f; w3 < 1.2f; w3 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		GameObject cg = new GameObject("DanoCam");
		Camera cam = cg.AddComponent<Camera>();
		cam.depth = 100f;
		cam.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
		cam.fieldOfView = 50f;
		cam.farClipPlane = 800f;
		cg.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
		Transform t = car.transform;
		Bounds L = car.lbox;
		Vector3 ctr = t.TransformPoint(L.center);
		Vector3 side = t.right;
		bool has = FBDoors.Has(car);
		FBDoors.SetMask(car, 63, sound: true);
		for (float w3 = 0f; w3 < 1.6f; w3 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Shot(ctr + t.forward * 5.5f + side * 4f + Vector3.up * 2.2f, ctr, "portas");
		yield return Shot(ctr - t.forward * 6f - side * 3.5f + Vector3.up * 2.4f, ctr, "portas_tras");
		Log(map + ": portas (" + car.id + "): dados " + (has ? "sim" : "NÃO") + " · " + FBDoors.DebugInfo(car));
		FBDoors.SetMask(car, 0, sound: true);
		for (float w3 = 0f; w3 < 1.3f; w3 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		Vector3 front = t.TransformPoint(new Vector3(L.center.x - L.extents.x * 0.45f, L.center.y, L.max.z));
		FBCarDamage.OnImpact(car, front, -t.forward, 16f);
		yield return null;
		for (float w3 = 0f; w3 < 0.6f; w3 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Shot(t.TransformPoint(new Vector3(L.center.x - L.extents.x * 2.2f, L.max.y * 0.9f, L.max.z + 2.4f)), front, "dano_quina");
		Vector3 shotP = t.TransformPoint(new Vector3(L.center.x, L.max.y - 0.3f, L.center.z + L.extents.z * 0.3f));
		FBCarDamage.OnShot(car, shotP, -t.forward, 30f);
		yield return null;
		FBCarDamage.OnShot(car, shotP, -t.forward, 30f);
		for (float w3 = 0f; w3 < 1.8f; w3 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Shot(ctr + t.forward * 6.2f + side * 2.6f + Vector3.up * 1.5f, front, "dano_frente");
		Log($"{map}: dano: vida {FBCarDamage.Hp(car):0} · lâmpadas quebradas {FBCarLights.DeadCount(car)} · {FBCarDamage.DebugInfo(car)} · {FBDoors.DebugInfo(car)}");
		FBPole pole = (from p in FBPole.All
			where (bool)p && p.isActiveAndEnabled
			orderby (p.basePos - ctr).sqrMagnitude
			select p).FirstOrDefault();
		if ((bool)pole)
		{
			Vector3 d2 = (pole.basePos - ctr).Flat();
			d2 = ((d2.sqrMagnitude > 0.01f) ? d2.normalized : Vector3.forward);
			pole.Hit(d2, 10f, pole.basePos + Vector3.up * 0.8f);
			for (float w3 = 0f; w3 < 3f; w3 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			yield return Shot(pole.basePos - d2 * 10f + new Vector3(d2.z, 0f, 0f - d2.x) * 6f + Vector3.up * 4.5f, pole.basePos + d2 * 3f, "poste");
			Log($"{map}: poste {pole.name}: caiu {pole.down} · inclinação {pole.tilt:0}° · {FBPole.All.Count} postes móveis na cena");
		}
		else
		{
			Log(map + ": poste: nenhum FBPole na cena (FBPolePrep não rodou nesta cena?)");
		}
		int num = FBStreetLights.Nearest(ctr, 120f);
		if (num >= 0)
		{
			Vector3 p2 = FBStreetLights.Lamps[num].p;
			FBStreetPoleFx.Ray(p2 + Vector3.down * 3f, Vector3.up, 6f);
			Log($"{map}: luminária {num} ({p2}) apagada pelo tiro: {FBStreetLights.IsDead(num)}");
		}
		else
		{
			Log(map + ": luminária: nenhuma");
		}
		FBCarDamage.Repair(car);
		for (float w3 = 0f; w3 < 0.5f; w3 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		Log($"{map}: oficina: vida {FBCarDamage.Hp(car):0} · lâmpadas quebradas {FBCarLights.DeadCount(car)} · {FBCarDamage.DebugInfo(car)}");
		UnityEngine.Object.Destroy(cg);
		IEnumerator Shot(Vector3 pos, Vector3 look, string name)
		{
			cam.transform.position = pos;
			cam.transform.LookAt(look);
			for (int i = 0; i < 5; i++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "role_" + map + (night ? "_noite" : "") + "_" + name + ".png"));
			yield return null;
			yield return null;
		}
	}

	private static FBRoleNpc rn2(FBCharacter n)
	{
		return n.GetComponent<FBRoleNpc>();
	}

	private IEnumerator Round35(string map)
	{
		List<string> list = FBNpcAnims.Roster();
		Dictionary<string, int> source = (from n in FBRole.npcs
			where n
			group n by n.skinId).ToDictionary((IGrouping<string, FBCharacter> g) => g.Key, (IGrouping<string, FBCharacter> g) => g.Count());
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		Dictionary<string, string> clipSex = new Dictionary<string, string>();
		foreach (object item2 in Json.L(GameData.Npc2, "clips") ?? new List<object>())
		{
			clipSex[Json.S(item2, "id")] = Json.S(item2, "sex");
		}
		foreach (FBCharacter k in FBRole.npcs.Where((FBCharacter n) => (bool)n && n.npcSet != null))
		{
			List<string> source2 = (from x in k.npcSet.idle.Concat(k.npcSet.talk).Concat(k.npcSet.sit).Concat(k.npcSet.dance)
					.Append(k.npcSet.walk)
				where x != null
				select x).ToList();
			num += source2.Count((string x) => !FBRegistry.I.Clip(x));
			num2 += source2.Count((string x) => clipSex.TryGetValue(x, out var value) && value != k.npcSet.sex);
			num3 += source2.Count((string x) => !x.StartsWith("NPC2_"));
		}
		int num4 = FBRole.npcs.Count((FBCharacter n) => (bool)n && n.alive && !FBNav.InsideNear(n.Pos, 2f));
		Log($"{map}: NPCs fora da área jogável (atrás do muro de limite): {num4} de {FBRole.npcs.Count((FBCharacter n) => n)}");
		Log(string.Format("{0}: NPCs pacote novo: elenco {1} [{2}] · em cena {3} ({4}) · clipes faltando {5} · de outro sexo {6} · fora do pacote {7}", map, list.Count, string.Join(", ", list), FBRole.npcs.Count((FBCharacter n) => n), string.Join(", ", source.Select((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value)), num, num2, num3));
		foreach (string id3 in list)
		{
			FBCharacter fBCharacter = FBRole.npcs.FirstOrDefault((FBCharacter n) => (bool)n && n.skinId == id3);
			if ((bool)fBCharacter && fBCharacter.npcSet != null)
			{
				Log(string.Format("{0}: NPC {1} ({2}): pés no chão +{3:0.0} cm · andar {4} · parados {5} · conversa {6} · senta {7} · dança {8}", map, id3, fBCharacter.npcSet.sex, fBCharacter.GroundLift * 100f, fBCharacter.npcSet.walk, string.Join("/", fBCharacter.npcSet.idle), fBCharacter.npcSet.talk.Count, fBCharacter.npcSet.sit.Count, fBCharacter.npcSet.dance.Count));
			}
		}
		GameObject cg = new GameObject("R35Cam");
		Camera cam = cg.AddComponent<Camera>();
		cam.depth = 100f;
		cam.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
		cam.fieldOfView = 45f;
		cam.farClipPlane = 800f;
		cg.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
		foreach (string id2 in list.Take(10))
		{
			FBCharacter fBCharacter2 = FBRole.npcs.FirstOrDefault((FBCharacter n) => (bool)n && n.skinId == id2 && n.alive);
			if (!fBCharacter2)
			{
				continue;
			}
			Vector3 vector = fBCharacter2.Pos + Vector3.up * 1.1f;
			Vector3 vector2 = Quaternion.Euler(0f, fBCharacter2.Yaw, 0f) * Vector3.forward;
			int[] array = new int[6] { 0, 60, -60, 120, -120, 180 };
			foreach (int num5 in array)
			{
				Vector3 vector3 = Quaternion.Euler(0f, fBCharacter2.Yaw + (float)num5, 0f) * Vector3.forward;
				if (!Physics.Raycast(vector, (vector3 * 3.2f + Vector3.up * 0.5f).normalized, 3.4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
				{
					vector2 = vector3;
					break;
				}
			}
			yield return Shot(fBCharacter2.Pos + vector2 * 3.2f + Vector3.up * 1.6f, vector, "npc_" + id2);
		}
		foreach (FBCharacter j in FBRole.npcs.Where((FBCharacter n) => (bool)n && n.npcSet != null && n.npcSet.sit.Count > 0).Take(2))
		{
			FBRoleNpc component = j.GetComponent<FBRoleNpc>();
			FBSeat fBSeat = (from x in FBRole.seats
				where x.busy == null && x.kind == "banco"
				orderby (x.p - j.Pos).sqrMagnitude
				select x).FirstOrDefault() ?? FBRole.seats.FirstOrDefault((FBSeat x) => x.busy == null);
			if ((bool)component && fBSeat != null)
			{
				component.SeatNow(fBSeat, enter: false);
			}
		}
		for (float w6 = 0f; w6 < 6f; w6 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		foreach (FBCharacter item3 in FBRole.npcs.Where((FBCharacter n) => (bool)n && n.GetComponent<FBRoleNpc>()?.st == "sit"))
		{
			FBRoleNpc component2 = item3.GetComponent<FBRoleNpc>();
			Transform transform = (item3.an ? item3.an.GetBoneTransform(HumanBodyBones.Hips) : null);
			Log($"{map}: sentado {item3.skinId}: assento y {component2.seat.h:0.00} · raiz y {item3.transform.position.y:0.00} · quadril {(transform ? (transform.position.y - component2.seat.h) : 0f) * 100f:0} cm acima do assento · do ponto {(transform ? (transform.position - component2.seat.p).Flat().magnitude : 0f) * 100f:0} cm");
		}
		int ns = 0;
		foreach (FBCharacter item4 in FBRole.npcs.Where((FBCharacter n) => (bool)n && n.GetComponent<FBRoleNpc>()?.st == "sit").Take(3))
		{
			Vector3 vector4 = Quaternion.Euler(0f, item4.Yaw, 0f) * Vector3.forward;
			yield return Shot(rn2(item4).seat.p + vector4 * 2.6f + Vector3.up * 1.3f + Quaternion.Euler(0f, 90f, 0f) * vector4 * 1.4f, rn2(item4).seat.p + Vector3.up * 0.55f, "sentado" + ns++);
		}
		FBCharacter me = FBMatch.Player;
		FBCar rcar = FBRole.roleCar;
		Vector3 basePos = (rcar ? rcar.transform.position : me.Pos) + Vector3.up * 0.2f;
		Vector3 right = (rcar ? rcar.transform.right : Vector3.right);
		Vector3 fwd = (rcar ? rcar.transform.forward : Vector3.forward);
		(string, bool)[] array2 = new(string, bool)[7]
		{
			("GolG2", true),
			("Golf", true),
			("Chevett", true),
			("ViaturaPMAzul", false),
			("ViaturaBM", false),
			("Clio2003", false),
			("Celta", true)
		};
		int slot = 0;
		(string id, bool kit)[] array3 = array2;
		for (int m = 0; m < array3.Length; m++)
		{
			(string, bool) tuple = array3[m];
			string id4 = tuple.Item1;
			bool item = tuple.Item2;
			Vector3 pos2 = basePos + fwd * 9f + right * (slot++ - 3) * 3.4f;
			FBCar car = FBVehicles.Make(id4, pos2, Quaternion.LookRotation(fwd).eulerAngles.y, civil: false, null, true);
			if (!car)
			{
				Log(map + ": carro " + id4 + " não montou");
				continue;
			}
			FBTuneData fBTuneData = new FBTuneData();
			if (item)
			{
				for (int num6 = 0; num6 < FBReb.ZONAS.Length; num6++)
				{
					List<int> list2 = FBReb.ZoneVariants(car, FBReb.ZONAS[num6]);
					fBTuneData.bk[num6] = ((list2.Count > 0) ? list2[list2.Count - 1] : (-1));
				}
				bool bandL = true;
				fBTuneData.bandR = true;
				fBTuneData.bandL = bandL;
				fBTuneData.bandeira = slot % 4;
				fBTuneData.engate = true;
				fBTuneData.forroF = 1;
				fBTuneData.forroT = 1;
			}
			FBTune.Apply(car, fBTuneData);
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Transform transform2 = car.shell.Find("reb_parts");
			int num7 = (transform2 ? transform2.GetComponentsInChildren<Renderer>(includeInactive: false).Length : 0);
			Log(string.Format("{0}: carro {1}: shell y {2:+0.000;-0.000} · ronco {3} · buzina {4} · tração {5} · pneus {6} · bodykit {7} · peças ativas {8} · bandeira {9} · engate {10}", map, id4, car.shell.localPosition.y, car.engineProf, FBReb.Buzina(id4) ?? "(perfil)", car.tracao ?? "-", FBReb.Pneus(id4).Length, FBReb.HasBodykit(car) ? string.Join(",", FBReb.ZONAS.Where((string z) => FBReb.HasZone(car, z))) : "não", num7, FBReb.HasBandeira(car), FBReb.HasEngate(car)));
			Vector3 c2 = car.transform.TransformPoint(car.lbox.center);
			Transform t = car.transform;
			yield return Shot(c2 + t.forward * 5.2f + t.right * 3.6f + Vector3.up * 1.4f, c2, "carro_" + id4 + "_frente");
			yield return Shot(c2 - t.forward * 5.4f - t.right * 3.4f + Vector3.up * 1.4f, c2, "carro_" + id4 + "_tras");
			yield return Shot(c2 + t.right * 6.5f + Vector3.up * 0.55f, c2 + Vector3.down * 0.2f, "carro_" + id4 + "_lado");
			List<object> list3 = Json.L(FBReb.Own(id4), "espelhos");
			if (list3 != null && list3.Count > 0)
			{
				List<object> list4 = Json.L(list3[0], "min");
				List<object> list5 = Json.L(list3[0], "max");
				Vector3 vector5 = car.shell.TransformPoint(new Vector3((Json.Fv(list4[0]) + Json.Fv(list5[0])) / 2f, (Json.Fv(list4[1]) + Json.Fv(list5[1])) / 2f, (Json.Fv(list4[2]) + Json.Fv(list5[2])) / 2f));
				yield return Shot(vector5 - t.forward * 1.3f + Vector3.up * 0.25f, vector5, "carro_" + id4 + "_espelho");
			}
			yield return Shot(t.TransformPoint(new Vector3(car.lbox.center.x, car.lbox.min.y + 0.6f, car.lbox.min.z - 1.6f)), t.TransformPoint(new Vector3(car.lbox.center.x, car.lbox.min.y + 0.4f, car.lbox.min.z)), "carro_" + id4 + "_placa");
			if (FBDoors.Has(car))
			{
				FBDoors.SetMask(car, 15, sound: true);
				for (float w6 = 0f; w6 < 1.4f; w6 += Time.unscaledDeltaTime)
				{
					yield return null;
				}
				yield return Shot(c2 - t.right * 3.2f + t.forward * 1.2f + Vector3.up * 1.1f, c2 - t.right * 0.9f, "carro_" + id4 + "_porta");
				FBDoors.SetMask(car, 0, sound: true);
			}
		}
		cam.enabled = false;
		if ((bool)rcar && (bool)me)
		{
			if (!FBVehicles.CarOf(me))
			{
				FBDrive.Enter(me, rcar);
			}
			for (float w6 = 0f; w6 < 1.5f; w6 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			float[] zOOMS = FBDrive.ZOOMS;
			for (int m = 0; m < zOOMS.Length; m++)
			{
				float w6 = zOOMS[m];
				FBDrive.SetZoom(w6, label: false);
				for (float w2 = 0f; w2 < 1.2f; w2 += Time.unscaledDeltaTime)
				{
					yield return null;
				}
				ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"r35_{map}_zoom_{w6:0.00}.png"));
				yield return null;
				yield return null;
				Log($"{map}: zoom {w6:0.00}: câmera a {(Camera.main ? Vector3.Distance(Camera.main.transform.position, rcar.transform.position) : (-1f)):0.0} m do carro");
			}
			FBDrive.SetZoom(0.82f, label: false);
			FBDrive.Exit(me, force: true);
		}
		FBCar fBCar = FBVehicles.list.LastOrDefault((FBCar c) => (bool)c && c.id == "GolG2");
		if ((bool)fBCar && (bool)me)
		{
			FBOficina.Open(fBCar);
			(string, string)[] array4 = new(string, string)[5]
			{
				("inicio", ""),
				("carro", "bodykit"),
				("carro", "acess"),
				("roda", "rodas"),
				("som", "")
			};
			for (int m = 0; m < array4.Length; m++)
			{
				var (id4, sb) = array4[m];
				FBOficina.TestTab(id4, sb);
				for (float w6 = 0f; w6 < 1f; w6 += Time.unscaledDeltaTime)
				{
					yield return null;
				}
				ScreenCapture.CaptureScreenshot(Path.Combine(dir, "r35_" + map + "_oficina_" + id4 + sb + ".png"));
				yield return null;
				yield return null;
			}
			FBOficina.TestClose();
		}
		UnityEngine.Object.Destroy(cg);
		IEnumerator Shot(Vector3 pos, Vector3 look, string name)
		{
			cam.enabled = true;
			cam.transform.position = pos;
			cam.transform.LookAt(look);
			for (int k2 = 0; k2 < 5; k2++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "r35_" + map + "_" + name + ".png"));
			yield return null;
			yield return null;
		}
	}

	private IEnumerator Paredao(string map)
	{
		FBParedao.Wall wall = FBParedao.List().FirstOrDefault((FBParedao.Wall w) => (bool)w.car && w.on && w.playing);
		if (wall == null)
		{
			Log(map + ": nenhum paredão tocando (biblioteca vazia?)");
			yield break;
		}
		FBCar car = wall.car;
		List<AudioListener> others = (from l in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)
			where l.enabled
			select l).ToList();
		foreach (AudioListener item5 in others)
		{
			item5.enabled = false;
		}
		GameObject eg = new GameObject("OuvidoTeste");
		eg.AddComponent<AudioListener>();
		Transform ear = eg.transform;
		yield return null;
		Transform transform = (car.soundWallNode ? car.soundWallNode : car.transform);
		Vector3 face = transform.forward;
		face.y = 0f;
		face.Normalize();
		Vector3 vector = new Vector3(face.z, 0f, 0f - face.x);
		Vector3 src = transform.position + Vector3.up * 0.8f;
		float[] buf = new float[2048];
		(string, Vector3)[] array = new(string, Vector3)[7]
		{
			("frente 4 m", src + face * 4f),
			("frente 15 m", src + face * 15f),
			("frente 40 m", src + face * 40f),
			("frente 90 m", src + face * 90f),
			("frente 160 m", src + face * 160f),
			("atrás 15 m", src - face * 15f),
			("lado 15 m", src + vector * 15f)
		};
		for (int m = 0; m < array.Length; m++)
		{
			var (name, vector2) = array[m];
			if (!ear)
			{
				break;
			}
			ear.position = vector2 + Vector3.up * 0.9f;
			ear.rotation = Quaternion.LookRotation(((src - ear.position).Flat().sqrMagnitude > 0.01f) ? (src - ear.position).Flat() : Vector3.forward);
			float k = 0f;
			float r2 = 0f;
			for (float w3 = 0f; w3 < 1.6f; w3 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			for (int i = 0; i < 20; i++)
			{
				(float l, float r) tuple2 = Rms();
				float item = tuple2.l;
				float item2 = tuple2.r;
				k += item;
				r2 += item2;
				yield return null;
			}
			Log($"{map}: paredão {name}: saída E {k / 20f:0.0000} D {r2 / 20f:0.0000} · {FBParedao.Diag(car)}");
		}
		if ((bool)ear)
		{
			ear.position = src + face * 12f + Vector3.up * 0.9f;
			Vector3 normalized = (src - ear.position).Flat().normalized;
			ear.rotation = Quaternion.LookRotation(new Vector3(0f - normalized.z, 0f, normalized.x));
			float r2 = 0f;
			float k = 0f;
			for (float w3 = 0f; w3 < 1.2f; w3 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			for (int m = 0; m < 20; m++)
			{
				(float l, float r) tuple3 = Rms();
				float item3 = tuple3.l;
				float item4 = tuple3.r;
				r2 += item3;
				k += item4;
				yield return null;
			}
			Log($"{map}: paredão fonte à DIREITA da cabeça: saída E {r2 / 20f:0.0000} D {k / 20f:0.0000}");
		}
		UnityEngine.Object.Destroy(eg);
		foreach (AudioListener item6 in others)
		{
			if ((bool)item6)
			{
				item6.enabled = true;
			}
		}
		(float l, float r) Rms()
		{
			float num = 0f;
			float num2 = 0f;
			AudioListener.GetOutputData(buf, 0);
			float[] array2 = buf;
			foreach (float num3 in array2)
			{
				num += num3 * num3;
			}
			AudioListener.GetOutputData(buf, 1);
			array2 = buf;
			foreach (float num4 in array2)
			{
				num2 += num4 * num4;
			}
			return (l: Mathf.Sqrt(num / (float)buf.Length), r: Mathf.Sqrt(num2 / (float)buf.Length));
		}
	}
}
}
