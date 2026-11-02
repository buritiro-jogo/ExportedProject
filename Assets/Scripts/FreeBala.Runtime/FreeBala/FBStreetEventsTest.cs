using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBStreetEventsTest : MonoBehaviour
{
	private string[] runs;

	private bool night;

	private string dir;

	private Action restore;

	private Camera cam;

	private int shot;

	private bool shotRoda;

	private Vector3 viewCache;

	private Vector3 viewFor = new Vector3(9999f, 0f, 0f);

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbruas");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			GameObject obj = new GameObject("FB_RuasTeste");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBStreetEventsTest fBStreetEventsTest = obj.AddComponent<FBStreetEventsTest>();
			fBStreetEventsTest.runs = commandLineArgs[num + 1].Split(',');
			fBStreetEventsTest.night = Array.IndexOf(commandLineArgs, "-fbnight") >= 0;
		}
	}

	private void OnApplicationQuit()
	{
		restore?.Invoke();
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][ruas-teste] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private void Shot(string name)
	{
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "ruas_" + name + ".png"));
		shot++;
	}

	private void Look(Vector3 from, Vector3 at, float fov = 55f)
	{
		if ((bool)cam)
		{
			cam.transform.position = from;
			cam.transform.rotation = Quaternion.LookRotation(at - from);
			cam.fieldOfView = fov;
		}
	}

	private static IEnumerator BackToStart()
	{
		FBCar roleCar = FBRole.roleCar;
		if ((bool)roleCar)
		{
			Transform transform = roleCar.transform;
			Vector3 p = transform.position - transform.right * 5f;
			float? num = FBWorld.GroundY(p.x, p.y + 3f, p.z, 0.4f, 8f);
			if (num.HasValue)
			{
				float valueOrDefault = num.GetValueOrDefault();
				p.y = valueOrDefault;
			}
			PlayerTo(p, transform.eulerAngles.y);
			yield return new WaitForSeconds(1.5f);
		}
	}

	private static void PlayerTo(Vector3 p, float yaw)
	{
		FBCharacter player = FBMatch.Player;
		if ((bool)player)
		{
			if ((bool)FBVehicles.CarOf(player))
			{
				FBDrive.Exit(player, force: true, p, yaw);
			}
			else
			{
				FBMatch.Teleport(player, p, yaw);
			}
		}
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		FBInput.TestMove = Vector2.zero;
		yield return Wait(3f);
		string[] array = runs;
		foreach (string obj in array)
		{
			string[] array2 = obj.Split(':');
			string map = array2[0];
			string mode = ((array2.Length > 1) ? array2[1] : "role");
			GameData.Load();
			FBSession.Load();
			string keepMap = FBSession.map;
			string keepMode = FBSession.mode;
			(string time, string weather) keepW = FBSession.WeatherFor(map);
			FBSession.map = map;
			FBSession.mode = mode;
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
			Log("carregando " + map + ":" + mode);
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
			yield return Wait(5f);
			if (mode == "role")
			{
				FBGarage.AutoPick();
				yield return Wait(2f);
			}
			FBMatch.God = true;
			if ((bool)FBMatch.Player)
			{
				FBMatch.Player.hp = 100f;
			}
			FBCharacter player = FBMatch.Player;
			FBCharacter fBCharacter = FBMatch.All.FirstOrDefault((FBCharacter a) => (bool)a && !a.isPlayer && !a.isBot && !a.IsFemale && a.Unarmed);
			Log(map + ":" + mode + ": strafe jogador " + player?.OverrideOf("StrafeRightLoop") + " / " + player?.OverrideOf("RunRtLoop") + " · NPC " + (fBCharacter ? fBCharacter.skinId : "-") + " andar " + fBCharacter?.OverrideOf("WalkFwdLoop") + " ré " + fBCharacter?.OverrideOf("WalkBwdLoop") + " lado " + fBCharacter?.OverrideOf("StrafeRightLoop") + " diag " + fBCharacter?.OverrideOf("StrafeRight45Loop"));
			WeaponDef weaponDef = (player ? player.weapon : null);
			Log(map + ":" + mode + ": sons 171 · porta " + FBAudio.Clip("veh_door_open")?.name + " · impacto concreto " + FBAudio.Clip("impact_concrete")?.name + " · arma " + weaponDef?.id + " tiro " + ((weaponDef != null) ? FBWeaponSound.Fire(weaponDef, silenced: false) : "-") + " longe " + FBWeaponSound.Far(weaponDef) + " recarga " + FBWeaponSound.Reload(weaponDef));
			if (!FBStreetEvents.I)
			{
				Log(map + ":" + mode + ": SEM diretor de eventos (mapa sem malha de ruas?)");
				restore();
				restore = null;
				continue;
			}
			float fps = 0f;
			int i = 0;
			for (float w = 0f; w < 3f; w += Time.unscaledDeltaTime)
			{
				fps += 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
				i++;
				yield return null;
			}
			fps /= (float)Mathf.Max(1, i);
			GameObject cg = new GameObject("RuasCam");
			cam = cg.AddComponent<Camera>();
			cam.depth = 100f;
			cam.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
			cam.farClipPlane = 900f;
			cam.nearClipPlane = 0.1f;
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			int num = Array.IndexOf(commandLineArgs, "-fbsoeventos");
			string[] only = ((num >= 0 && num + 1 < commandLineArgs.Length) ? commandLineArgs[num + 1].Split(',') : null);
			if (Do("eletricista"))
			{
				yield return Eletricista(map + "_" + mode);
			}
			if (Do("obra"))
			{
				yield return Obra(map + "_" + mode);
			}
			if (Do("acai"))
			{
				yield return Acai(map + "_" + mode, fps);
			}
			if (Do("baculejo"))
			{
				yield return Baculejo(map + "_" + mode, fps);
			}
			if (Do("veiculos"))
			{
				yield return Veiculos(map + "_" + mode);
			}
			if (Do("objetos"))
			{
				yield return Objetos(map + "_" + mode);
			}
			if (Do("fumaca"))
			{
				yield return Fumaca(map + "_" + mode);
			}
			if (Do("racha"))
			{
				yield return Racha(map + "_" + mode);
			}
			if (Do("motos"))
			{
				yield return MotosTransito(map + "_" + mode);
			}
			if (Do("perseguicao"))
			{
				yield return Perseguicao(map + "_" + mode);
			}
			if (Do("paredoes"))
			{
				yield return Paredoes(map + "_" + mode);
			}
			if (Do("camera"))
			{
				yield return Camera1P(map + "_" + mode);
			}
			UnityEngine.Object.Destroy(cg);
			FBMatch.God = false;
			restore();
			restore = null;
			bool Do(string k)
			{
				if (only != null)
				{
					return Array.IndexOf(only, k) >= 0;
				}
				return true;
			}
		}
		Log("fim");
		Application.Quit();
	}

	private IEnumerator Eletricista(string tag)
	{
		FBEletricista.ForceVariant = "escada";
		FBStreetEvents.ForceNear = 35f;
		FBStreetEvents.ForceEletricista = true;
		yield return Wait(1.2f);
		FBStreetEvents.ForceNear = -1f;
		if (FBEletricista.Active)
		{
			Vector3 b2 = FBEletricista.Base;
			string last = null;
			float t0 = 0f;
			List<string> seen = new List<string>();
			for (; t0 < 140f; t0 += Time.unscaledDeltaTime)
			{
				if (!FBEletricista.Active)
				{
					break;
				}
				if (!(FBEletricista.State != "pausa"))
				{
					break;
				}
				string st = FBEletricista.State;
				if (st != last)
				{
					last = st;
					seen.Add(st);
					FBCharacter w3 = FBEletricista.Worker;
					GameObject lad2 = FBEletricista.Ladder;
					Look(b2 + new Vector3(7f, 4f, 7f), (w3 ? w3.Pos : b2) + Vector3.up * 1.5f);
					yield return null;
					Shot(tag + "_escada_" + st);
					Log(string.Format("{0}: escada · etapa {1} aos {2:0.0} s · eletricista a {3} m · escada (topo) {4:0.0} m · luminárias cortadas {5}", tag, st, t0, w3 ? (w3.Pos.y - b2.y).ToString("0.0") : "-", lad2 ? (lad2.GetComponentInChildren<Renderer>().bounds.max.y - b2.y) : 0f, FBEletricista.LampsCut));
				}
				if (st == "trabalhando" && (bool)FBEletricista.Worker)
				{
					FBEletricista.Worker.GetComponent<FBLadderIK>();
				}
				if (st == "trabalhando" && t0 > 0f && Time.frameCount % 600 == 0)
				{
					Log($"{tag}: escada trabalhando · faíscas {FBEletricista.Sparks}");
				}
				yield return null;
			}
			Log(string.Format("{0}: escada · etapas: {1} · {2:0} s · faíscas {3}", tag, string.Join(" > ", seen), t0, FBEletricista.Sparks));
			FBEletricista.End(now: true);
			yield return Wait(1f);
		}
		else
		{
			Log(tag + ": escada NÃO montou (" + FBEletricista.Why + ")");
		}
		FBEletricista.ForceVariant = "energia";
		FBStreetEvents.ForceNear = 35f;
		FBStreetEvents.ForceEletricista = true;
		yield return Wait(1.2f);
		FBStreetEvents.ForceNear = -1f;
		Log(string.Format("{0}: energia · {1} · luminárias cortadas {2} · ajudante {3}", tag, FBEletricista.State, FBEletricista.LampsCut, FBEletricista.Helper ? FBEletricista.Helper.skinId : "-"));
		if (!FBEletricista.Active)
		{
			Log(tag + ": eletricista NÃO montou (" + FBEletricista.Why + " · " + FBStreetEvents.Log + ")");
			yield break;
		}
		Vector3 b = FBEletricista.Base;
		FBCharacter w2 = FBEletricista.Worker;
		GameObject lad = FBEletricista.Ladder;
		GameObject van = FBEletricista.Van;
		Log(tag + ": eletricista · " + FBEletricista.State + " · escada " + (lad ? lad.name : "-") + " · carro " + (van ? (van.transform.childCount + " peças") : "-") + " · personagem " + (w2 ? (w2.skinId + " a " + (w2.Pos.y - b.y).ToString("0.0") + " m") : "-") + " · clipe " + (((bool)w2 && w2.dance != null) ? w2.dance.clip : "-"));
		Vector3 side = (w2 ? Vector3.Cross(Vector3.up, (w2.Pos - b).Flat().normalized) : Vector3.right);
		if (side.sqrMagnitude < 0.1f)
		{
			side = Vector3.right;
		}
		Look(b + side * 9f + Vector3.up * 3.5f + (b - (w2 ? w2.Pos : b)).Flat().normalized * 4f, b + Vector3.up * 2.5f);
		yield return Wait(0.6f);
		Shot(tag + "_ele_cena");
		if ((bool)w2)
		{
			Look(w2.Pos + side * 2.2f + Vector3.up * 0.2f - w2.Fwd * 1.2f, w2.Pos + Vector3.up * 1.3f, 50f);
			yield return Wait(0.4f);
			Shot(tag + "_ele_trabalhando");
		}
		if ((bool)van)
		{
			Renderer[] componentsInChildren = van.GetComponentsInChildren<Renderer>();
			if (componentsInChildren.Length != 0)
			{
				Bounds bounds = componentsInChildren[0].bounds;
				Renderer[] array = componentsInChildren;
				foreach (Renderer renderer in array)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				Look(bounds.center + side * 6f + Vector3.up * 2.5f, bounds.center, 50f);
				yield return Wait(0.4f);
				Shot(tag + "_ele_carro");
			}
		}
		FBCharacter P = FBMatch.Player;
		if ((bool)P)
		{
			PlayerTo(b - side * 3.5f, 0f);
		}
		for (float t0 = 0f; t0 < 9f; t0 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		Log($"{tag}: eletricista com o jogador a {(P ? (P.Pos - b).Flat().magnitude : (-1f)):0.0} m · estado {FBEletricista.State}");
		FBEletricista.Fall(side, "teste");
		yield return Wait(0.7f);
		Look(b + side * 7f + Vector3.up * 3f, b + Vector3.up);
		Shot(tag + "_ele_queda");
		yield return Wait(6.5f);
		w2 = FBEletricista.Worker;
		if ((bool)w2)
		{
			Look(w2.Pos + side * 5f + Vector3.up * 2.5f, w2.Pos + Vector3.up);
			yield return Wait(0.3f);
		}
		Shot(tag + "_ele_depois");
		Log(tag + ": eletricista depois da queda: " + FBEletricista.State + " · escada no chão " + (lad ? (lad.transform.position.y - b.y).ToString("0.0") : "-") + " m · personagem de pé " + ((!w2) ? "-" : (w2.Hips ? (w2.Hips.position.y - w2.Pos.y).ToString("0.00") : "?")));
		yield return Wait(4f);
		FBEletricista.End(now: true);
	}

	private IEnumerator Objetos(string tag)
	{
		Log($"{tag}: objetos do 171: {FBVestirRua.Total} · " + string.Join(", ", from kv in FBVestirRua.Count.OrderByDescending((KeyValuePair<string, int> kv) => kv.Value).Take(10)
			select kv.Key + " " + kv.Value) + $" · corpos físicos {FBFisica.All.Count} · assentos {FBRole.seats.Count}");
		FBCharacter P = FBMatch.Player;
		if ((bool)P)
		{
			Transform transform = (FBVestirRua.Root ? FBVestirRua.Root.GetComponentsInChildren<Transform>().FirstOrDefault((Transform t) => t.name.StartsWith("x171_SM_Mesa")) : null);
			if ((bool)transform)
			{
				Look(transform.position + new Vector3(3f, 2f, 3f), transform.position);
				yield return Wait(0.5f);
				Shot(tag + "_obj_boteco");
			}
			FBPropBody lix = FBFisica.All.FirstOrDefault((FBPropBody b) => (bool)b && b.name.Contains("lixeira"));
			if ((bool)lix)
			{
				Vector3 p0 = lix.transform.position;
				Look(p0 + new Vector3(3f, 1.6f, 3f), p0);
				FBFisica.Shot(lix.GetComponentInChildren<Collider>(), p0 + Vector3.up * 0.4f, (p0 - P.Pos).Flat().normalized);
				yield return Wait(2f);
				Shot(tag + "_obj_lixeira_tiro");
				Log($"{tag}: lixeira levou tiro: andou {(lix.transform.position - p0).magnitude:0.00} m · tombou {Vector3.Angle(lix.transform.up, Vector3.up):0}° · impactos {FBFisica.Impacts}");
			}
			FBCharacter npc = FBMatch.All.FirstOrDefault((FBCharacter a) => (bool)a && a.alive && !a.isPlayer && !a.vehiclePose && (a.Pos - P.Pos).magnitude < 120f);
			FBCar fBCar = FBVehicles.list.FirstOrDefault((FBCar c) => (bool)c && !c.driver && c.kind != "moto");
			if ((bool)npc && (bool)fBCar)
			{
				int g0 = FBAtropelo.GotUp;
				Vector3 fwd = npc.Fwd;
				fBCar.transform.position = npc.Pos - fwd * 4f + Vector3.up * 0.05f;
				fBCar.transform.rotation = Quaternion.LookRotation(fwd);
				float hp0 = npc.hp;
				FBAtropelo.Hit(npc, fBCar, 11f, fwd, FBSession.mode != "role");
				Look(npc.Pos + Vector3.Cross(Vector3.up, fwd) * 5f + Vector3.up * 2f, npc.Pos);
				yield return Wait(0.6f);
				Shot(tag + "_atropelo_1");
				yield return Wait(1.5f);
				Shot(tag + "_atropelo_2");
				yield return Wait(10f);
				Shot(tag + "_atropelo_levantou");
				Log(string.Format("{0}: atropelamento {1} · vida {2:0} -> {3:0} · vivo {4} · levantou {5} · quadril {6} m", tag, FBAtropelo.Last, hp0, npc.hp, npc.alive, FBAtropelo.GotUp - g0, npc.Hips ? (npc.Hips.position.y - npc.Pos.y).ToString("0.00") : "-"));
			}
		}
	}

	private IEnumerator Obra(string tag)
	{
		FBStreetEvents.ForceNear = 30f;
		FBStreetEvents.ForceObra = true;
		yield return Wait(1.5f);
		FBStreetEvents.ForceNear = -1f;
		if (!FBObra.Active)
		{
			Log(tag + ": obra NÃO montou (" + FBObra.Why + " · " + FBStreetEvents.Log + ")");
			yield break;
		}
		Vector3 c = FBObra.Center;
		Log(string.Format("{0}: obra · {1} pedreiros · clipes {2}", tag, FBObra.Workers.Count, string.Join(",", from w in FBObra.Workers
			where w
			select (w.dance == null) ? "-" : w.dance.clip)));
		Look(c + new Vector3(9f, 6f, 9f), c + Vector3.up * 0.5f);
		yield return Wait(0.6f);
		Shot(tag + "_obra_geral");
		Look(c + new Vector3(-4f, 2.2f, 5f), c + Vector3.up * 0.8f);
		yield return Wait(0.4f);
		Shot(tag + "_obra_perto");
		FBCharacter fBCharacter = FBObra.Workers.FirstOrDefault((FBCharacter w) => (bool)w && w.dance != null);
		if ((bool)fBCharacter)
		{
			Look(fBCharacter.Pos + fBCharacter.Fwd * 2.4f + fBCharacter.transform.right * 1.2f + Vector3.up * 1.4f, fBCharacter.Pos + Vector3.up * 1f, 50f);
			yield return Wait(0.4f);
			Shot(tag + "_obra_pedreiro");
		}
		FBBotBrain.Noise(c + Vector3.up + new Vector3(10f, 0f, 0f));
		yield return Wait(3f);
		Look(c + new Vector3(9f, 6f, 9f), c + Vector3.up * 0.5f, 60f);
		Shot(tag + "_obra_tiro");
		Log($"{tag}: obra depois do tiro · correndo {FBObra.Workers.Count((FBCharacter w) => (bool)w && w.dance == null)}/{FBObra.Workers.Count}");
		FBObra.End(now: true);
	}

	private IEnumerator Racha(string tag)
	{
		int c0 = FBRacha.Crashes;
		int r0 = FBRacha.Races;
		int f0 = FBRacha.Finishes;
		int fails = 0;
		for (int i = 0; i < 3; i++)
		{
			int rr = FBRacha.Races;
			yield return RachaOne((i == 0) ? tag : (tag + "_" + i), i == 0);
			if (FBRacha.Races == rr)
			{
				fails++;
			}
			yield return Wait(1f);
		}
		Log($"{tag}: RACHAS no mapa: {FBRacha.Races - r0} corridas · chegadas {FBRacha.Finishes - f0} pilotos · quedas {FBRacha.Crashes - c0} · não montou/não terminou {fails}");
	}

	private IEnumerator RachaOne(string tag, bool diag)
	{
		FBStreetEvents.ForceNear = 120f;
		FBStreetEvents.ForceRacha = true;
		yield return Wait(1.5f);
		FBStreetEvents.ForceNear = -1f;
		if (!FBRacha.Active)
		{
			Log(tag + ": racha NÃO montou (" + FBRacha.Why + ")");
			yield break;
		}
		List<FBCar> bikes = FBVehicles.list.Where((FBCar c) => (bool)c && (bool)c.mx && (bool)c.driver && !c.driver.isPlayer && FBTransito.List.All((FBTransito.Car t) => t.car != c)).ToList();
		FBCar fBCar = bikes.FirstOrDefault();
		FBCharacter player = FBMatch.Player;
		if ((bool)fBCar && (bool)player)
		{
			Vector3 p = fBCar.transform.position - fBCar.transform.right * 9f;
			float? num = FBWorld.GroundY(p.x, p.y + 3f, p.z, 0.3f, 6f);
			if (num.HasValue)
			{
				float valueOrDefault = num.GetValueOrDefault();
				p.y = valueOrDefault;
			}
			PlayerTo(p, fBCar.transform.eulerAngles.y + 90f);
			yield return Wait(0.5f);
		}
		FBCar fBCar2 = FBTransito.List.Select((FBTransito.Car c) => c.car).FirstOrDefault((FBCar c) => (bool)c && (bool)c.mx);
		if ((bool)fBCar2)
		{
			bikes.Add(fBCar2);
		}
		FBCar fBCar3 = bikes.FirstOrDefault();
		if ((bool)fBCar3)
		{
			Transform transform = fBCar3.transform;
			Look(transform.position + transform.right * 5f + Vector3.up * 1.6f, transform.position + Vector3.up * 0.8f, 50f);
		}
		yield return null;
		yield return null;
		yield return null;
		if (diag)
		{
			foreach (FBCar b0 in bikes)
			{
				LODGroup component = b0.GetComponent<LODGroup>();
				Renderer[] componentsInChildren = b0.GetComponentsInChildren<Renderer>(includeInactive: true);
				Log(string.Format("{0}: racha diag2 {1} · Camera.main {2} · jogador {3:0} u · visíveis {4}/{5} · LODGroup ligado {6} lods {7} · shaders {8} · forceOff {9} · sombra {10} · camadas {11} · trânsito {12}", tag, b0.id, Camera.main ? (Vector3.Distance(Camera.main.transform.position, b0.transform.position).ToString("0") + " u") : "NULA", Vector3.Distance(FBMatch.Player.Pos, b0.transform.position), componentsInChildren.Count((Renderer r) => r.isVisible), componentsInChildren.Length, (bool)component && component.enabled, component ? component.lodCount : 0, string.Join(",", from r in componentsInChildren.Take(3)
					select (!r.sharedMaterial) ? "-" : r.sharedMaterial.shader.name), componentsInChildren.Count((Renderer r) => r.forceRenderingOff), componentsInChildren.Count((Renderer r) => r.shadowCastingMode != ShadowCastingMode.Off), string.Join(",", componentsInChildren.Select((Renderer r) => r.gameObject.layer).Distinct()), FBTransito.List.Any((FBTransito.Car c) => c.car == b0)));
			}
		}
		if (bikes.Count > 2)
		{
			bikes.RemoveAt(bikes.Count - 1);
		}
		if (diag)
		{
			foreach (FBCar item in bikes)
			{
				Renderer[] componentsInChildren2 = item.GetComponentsInChildren<Renderer>(includeInactive: true);
				Transform transform2 = (((bool)item.driver && (bool)item.driver.an) ? item.driver.an.GetBoneTransform(HumanBodyBones.Hips) : null);
				Bounds bounds = ((componentsInChildren2.Length != 0) ? componentsInChildren2[0].bounds : default(Bounds));
				Renderer[] array = componentsInChildren2;
				foreach (Renderer renderer in array)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				Log(string.Format("{0}: racha diag {1} · renderers {2} ligados {3} · malhas vazias {4} · caixa {5} tam {6} · grupo {7} escala {8:0.00} · quadril do piloto {9} · LODGroup {10:0.00} · ativo {11}", tag, item.id, componentsInChildren2.Length, componentsInChildren2.Count((Renderer r) => r.enabled && r.gameObject.activeInHierarchy), item.GetComponentsInChildren<MeshFilter>(includeInactive: true).Count((MeshFilter m) => !m.sharedMesh || m.sharedMesh.vertexCount == 0), bounds.center, bounds.size, item.transform.position, item.transform.lossyScale.x, transform2 ? transform2.position.ToString() : "-", item.GetComponent<LODGroup>()?.size ?? (-1f), item.gameObject.activeInHierarchy));
			}
		}
		string lastSt = null;
		int shots = 0;
		float t2 = 0f;
		float maxKmh = 0f;
		while (FBRacha.Active && t2 < 45f)
		{
			t2 += Time.deltaTime;
			FBCar fBCar4 = (from x in bikes
				where x
				orderby x.mx.speedKmh descending
				select x).FirstOrDefault();
			if (!fBCar4)
			{
				break;
			}
			maxKmh = Mathf.Max(maxKmh, fBCar4.mx.speedKmh);
			Transform transform3 = fBCar4.transform;
			Look(transform3.position + transform3.right * 7f + transform3.forward * 2f + Vector3.up * 2.2f, transform3.position + Vector3.up * 0.9f + transform3.forward * 1.5f, 50f);
			if (FBRacha.State != lastSt || (FBRacha.State == "largada" && shots < (diag ? 6 : 2) && t2 > 1f && Mathf.Repeat(t2, 1.6f) < Time.deltaTime))
			{
				lastSt = FBRacha.State;
				Shot($"{tag}_racha_{shots++}_{FBRacha.State}");
				Log(tag + ": racha " + FBRacha.State + " · " + string.Join(" · ", from x in bikes
					where x
					select $"{x.id} {x.mx.speedKmh:0} km/h grau {x.mx.wheelieAngle:0}° marcha {x.mx.GearLabel} caiu {x.mx.crashed} clipe {x.driver?.sys}"));
			}
			if (FBRacha.State == "volta")
			{
				break;
			}
			yield return null;
		}
		Log($"{tag}: racha fim · freadas por obstáculo {FBRacha.Brakes} · percorrido {FBRacha.Progress:0} m · máxima {maxKmh:0} km/h · grau máx {FBRacha.MaxWheelie:0}° · estado {FBRacha.State}");
		FBRacha.End(now: true);
	}

	private IEnumerator MotosTransito(string tag)
	{
		float t = 0f;
		float maxLean = 0f;
		float maxWh = 0f;
		float physLean = 0f;
		float tpT = 0f;
		int j = 0;
		int physSeen = 0;
		FBTransito.Car fol = null;
		float share0 = FBTransito.MotoShare;
		FBTransito.MotoShare = 0.6f;
		float gs0 = FBTransito.GarupaShare;
		FBTransito.GarupaShare = 1f;
		yield return BackToStart();
		int num = FBTransito.List.Count((FBTransito.Car c) => (bool)c.car && (bool)c.car.mx && c.mode == "drive");
		if (num < 3)
		{
			Log($"{tag}: {num} moto(s) no trânsito agora · pus {FBTransito.TestMotos(3 - num)} (o sorteio do começo pode vir sem moto)");
		}
		int ups0 = FBTransito.MotoPhysUps;
		int cr0 = FBTransito.MotoCrashes;
		int wh0 = FBTransito.MotoPhysWh;
		FBTransito.MotoPhysLagMax = (FBTransito.MotoPhysLatMax = (FBTransito.MotoPhysFrames = (FBTransito.MotoPhysWhMax = 0f)));
		while (t < 70f)
		{
			t += Time.deltaTime;
			List<FBTransito.Car> list = FBTransito.List.Where((FBTransito.Car c) => (bool)c.car && (bool)c.car.mx && c.mode == "drive").ToList();
			j = Mathf.Max(j, list.Count);
			physSeen = Mathf.Max(physSeen, list.Count((FBTransito.Car c) => c.phys));
			foreach (FBTransito.Car item in list)
			{
				maxLean = Mathf.Max(maxLean, Mathf.Abs(item.roll) * 57.29578f);
				maxWh = Mathf.Max(maxWh, item.whA * 57.29578f);
				if (item.phys)
				{
					physLean = Mathf.Max(physLean, Mathf.Abs(item.car.mx.leanAngle));
				}
			}
			if (fol == null || !fol.car || fol.mode != "drive")
			{
				fol = list.OrderBy((FBTransito.Car c) => (c.pos - FBStreetEvents.PlayerPos).sqrMagnitude).FirstOrDefault();
			}
			if (fol != null && (bool)fol.car)
			{
				Transform transform = fol.car.transform;
				float num2;
				tpT = (num2 = tpT - Time.deltaTime);
				if (num2 <= 0f && (transform.position - FBStreetEvents.PlayerPos).magnitude > 16f)
				{
					tpT = 1.2f;
					Vector3 p = transform.position - transform.forward * 6f + transform.right * 10f;
					float? num3 = FBWorld.GroundY(p.x, p.y + 3f, p.z, 0.4f, 8f);
					if (num3.HasValue)
					{
						float valueOrDefault = num3.GetValueOrDefault();
						p.y = valueOrDefault;
					}
					PlayerTo(p, transform.eulerAngles.y);
				}
				Look(transform.position + transform.right * 6f + Vector3.up * 2f, transform.position + Vector3.up * 0.9f, 50f);
				if (Mathf.Repeat(t, 7f) < Time.deltaTime)
				{
					Shot($"{tag}_moto_transito_{(int)(t / 7f)}");
					Log(string.Format("{0}: moto {1} · física {2} · {3:0} km/h · incl {4:0}° · grau {5:0}°", tag, fol.car.id, fol.phys ? "MX" : "cinemática", fol.phys ? fol.car.mx.speedKmh : (fol.v * 3.6f / 1.2f), fol.phys ? fol.car.mx.leanAngle : (fol.roll * 57.29578f), fol.phys ? fol.car.mx.wheelieAngle : (fol.whA * 57.29578f)) + ((fol.phys ? (fol.car.mx.speedKmh < 1f) : (fol.v < 0.3f)) ? (" · PARADA: " + FBTransito.Chain(fol)) : ""));
				}
			}
			yield return null;
		}
		List<string> values = (from c in FBTransito.List
			where (bool)c.car && (bool)c.car.mx && (bool)c.actor
			select c.actor.sys).Distinct().ToList();
		Log(string.Format("{0}: estilos dos pilotos MX agora: {1} · garupas criadas {2} · desceram com o piloto {3}", tag, string.Join(", ", values), FBTransito.GarupaSpawned, FBGarupa.Dismounted));
		for (float gw = 0f; gw < 45f; gw += 0.5f)
		{
			if (FBTransito.List.Any((FBTransito.Car c) => (bool)c.garupa && (bool)c.car && c.mode == "drive"))
			{
				break;
			}
			yield return Wait(0.5f);
		}
		if (!FBTransito.List.Any((FBTransito.Car c) => (bool)c.garupa && (bool)c.car && c.mode == "drive"))
		{
			FBTransito.Car car = (from c in FBTransito.List
				where (bool)c.car && (bool)c.car.mx && c.mode == "drive" && !c.garupa && (bool)c.actor
				orderby (c.pos - FBStreetEvents.PlayerPos).sqrMagnitude
				select c).FirstOrDefault();
			Log(string.Format("{0}: garupa: nenhuma moto com garupa andando em 45 s · sem passageira livre {1}x · pondo na {2}: {3}", tag, FBTransito.GarupaSemSkin, car?.car?.id, (car != null && FBTransito.ForceGarupa(car)) ? "ok" : "falhou"));
		}
		FBTransito.Car gm = (from c in FBTransito.List
			where (bool)c.garupa && (bool)c.car && c.mode == "drive"
			orderby (c.pos - FBStreetEvents.PlayerPos).sqrMagnitude
			select c).FirstOrDefault();
		if (gm != null)
		{
			for (int i = 0; i < 3; i++)
			{
				Transform transform2 = gm.car.transform;
				Vector3 p2 = transform2.position + transform2.right * 9f;
				float? num3 = FBWorld.GroundY(p2.x, p2.y + 3f, p2.z, 0.4f, 8f);
				if (num3.HasValue)
				{
					float valueOrDefault2 = num3.GetValueOrDefault();
					p2.y = valueOrDefault2;
				}
				PlayerTo(p2, transform2.eulerAngles.y);
				yield return Wait(0.6f);
				if (gm.car == null || !gm.garupa)
				{
					break;
				}
				transform2 = gm.car.transform;
				Look(transform2.position - transform2.right * 3.6f + Vector3.up * 1.4f, transform2.position + Vector3.up * 0.9f, 45f);
				yield return null;
				Shot($"{tag}_garupa_{i}");
				Transform transform3 = (gm.garupa.an ? gm.garupa.an.GetBoneTransform(HumanBodyBones.Hips) : null);
				Transform transform4 = (gm.garupa.an ? gm.garupa.an.GetBoneTransform(HumanBodyBones.LeftHand) : null);
				Transform transform5 = (((bool)gm.actor && (bool)gm.actor.an) ? gm.actor.an.GetBoneTransform(HumanBodyBones.Hips) : null);
				Log(string.Format("{0}: garupa {1} na {2} ({3}) · clipe {4} · quadril {5:0.00} m do piloto · mão E {6:0.00} m da cintura · piloto {7}", tag, gm.garupa.skinId, gm.car.id, gm.phys ? "física MX" : "cinemática", gm.garupa.sys, ((bool)transform3 && (bool)transform5) ? (transform3.position - transform5.position).magnitude : (-1f), ((bool)transform4 && (bool)transform5) ? (transform4.position - transform5.position).magnitude : (-1f), gm.actor?.sys));
				FBCharacter actor = gm.actor;
				FBDriverPose fBDriverPose = (actor ? actor.GetComponent<FBDriverPose>() : null);
				FBCar car2 = gm.car;
				Log(string.Format("{0}: garupa diag · piloto {1} pose {2} carro da pose {3} · CarOf {4} · piloto→carro {5:0.0} u · carro→MX {6:0.0} u · garupa→carro {7:0.0} u · animator {8} · vehiclePose {9} · modo {10} · ativo {11}", tag, actor?.skinId, (!fBDriverPose) ? "SEM" : (fBDriverPose.enabled ? "ligada" : "DESLIGADA"), ((bool)fBDriverPose && fBDriverPose.Car == car2) ? "o mesmo" : (fBDriverPose?.Car?.id ?? "-"), (FBVehicles.CarOf(actor) == car2) ? "o mesmo" : (FBVehicles.CarOf(actor)?.id ?? "nenhum"), actor ? (actor.transform.position - car2.transform.position).magnitude : (-1f), car2.mx ? (car2.transform.position - car2.mx.transform.position).magnitude : (-1f), (gm.garupa.transform.position - car2.transform.position).magnitude, (bool)actor && (bool)actor.an && actor.an.enabled, actor?.vehiclePose, gm.mode, (bool)actor && actor.gameObject.activeInHierarchy));
				yield return Wait(1.5f);
			}
		}
		else
		{
			Log(tag + ": nenhuma moto com garupa andando agora");
		}
		FBTransito.MotoShare = share0;
		FBTransito.GarupaShare = gs0;
		Log($"{tag}: motos no trânsito {j} · inclinação máx {maxLean:0}° · grau máx {maxWh:0}° · seguindo {fol?.car?.id} clipe {fol?.actor?.sys}");
		Log($"{tag}: FÍSICA MX nas motos NPC: ligou {FBTransito.MotoPhysUps - ups0}x · até {physSeen} ao mesmo tempo · {FBTransito.MotoPhysFrames:0} s com física · atraso máx do fantasma {FBTransito.MotoPhysLagMax:0.0} u · desvio lateral máx {FBTransito.MotoPhysLatMax:0.0} u · inclinação física máx {physLean:0}° · graus puxados {FBTransito.MotoPhysWh - wh0} (máx {FBTransito.MotoPhysWhMax:0}°) · quedas {FBTransito.MotoCrashes - cr0}");
	}

	private IEnumerator Fumaca(string tag)
	{
		FBCharacter player = FBMatch.Player;
		FBCar car = FBRole.roleCar;
		if (!player || !car)
		{
			Log(tag + ": fumaça: sem carro do rolê");
			yield break;
		}
		if (FBVehicles.CarOf(player) != car)
		{
			Log(tag + ": fumaça: jogador fora do carro do rolê");
			yield break;
		}
		yield return Wait(1f);
		bool hb = true;
		float thr = 0f;
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.move = new Vector2(0f, thr);
			inp.jumpHeld = hb;
			inp.interact = false;
			inp.multimidia = false;
			return inp;
		};
		thr = 1f;
		float burnT = 0f;
		for (float w = 0f; w < 2.5f; w += Time.deltaTime)
		{
			if (car.burn)
			{
				burnT += Time.deltaTime;
			}
			yield return null;
		}
		Transform transform = car.transform;
		Look(transform.position - transform.forward * 7.5f + transform.right * 3.2f + Vector3.up * 1.6f, transform.position - transform.forward * 1.2f + Vector3.up * 0.7f);
		FBCarFX.SmokeTex(light: false);
		yield return null;
		Shot(tag + "_fumaca_pneu_antes");
		yield return null;
		FBCarFX.SmokeTex(light: true);
		yield return null;
		Shot(tag + "_fumaca_pneu_depois");
		yield return null;
		Log($"{tag}: nuvem dos pneus na rua · {car.id} queimando {burnT:0.0} de 2,5 s · piso {FBCarFX.LastSurf} (0 asfalto, 4 concreto, 1–3 terra) · fotos com a textura antiga e a clareada no mesmo quadro");
		thr = 0f;
		hb = false;
		yield return Wait(1.5f);
		FBPlayer.TestInput = null;
	}

	private IEnumerator Camera1P(string tag)
	{
		FBCharacter me = FBMatch.Player;
		if (!me || !FBPlayer.I)
		{
			Log(tag + ": câmera: sem jogador");
			yield break;
		}
		FBCar car = FBVehicles.CarOf(me);
		if ((bool)car)
		{
			Transform transform = car.transform;
			Vector3 p = transform.position - transform.right * 6f;
			float? num = FBWorld.GroundY(p.x, p.y + 3f, p.z, 0.4f, 8f);
			if (num.HasValue)
			{
				float valueOrDefault = num.GetValueOrDefault();
				p.y = valueOrDefault;
			}
			PlayerTo(p, transform.eulerAngles.y);
		}
		yield return Wait(1.5f);
		Camera gc = Camera.main;
		Transform head = (me.an ? me.an.GetBoneTransform(HumanBodyBones.Head) : null);
		bool fp0 = FBPlayer.I.FirstPerson;
		float d0 = D();
		bool tv = false;
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.toggleView = tv;
			tv = false;
			return inp;
		};
		for (int i = 0; i < 3; i++)
		{
			tv = true;
			yield return Wait(0.8f);
			Log($"{tag}: câmera · apertou V {i + 1}x · 1ª pessoa {FBPlayer.I.FirstPerson} · câmera do jogo a {D():0.00} m da cabeça · view {FBSession.view}");
		}
		FBPlayer.TestInput = null;
		if ((bool)cam)
		{
			cam.enabled = false;
		}
		yield return null;
		Shot(tag + "_camera_depois_do_V");
		yield return null;
		if ((bool)cam)
		{
			cam.enabled = true;
		}
		Log(string.Format("{0}: CÂMERA 1ª PESSOA: antes {1} pessoa a {2:0.00} m da cabeça · depois de 3× V: {3} a {4:0.00} m · laboratório {5} · no carro {6}", tag, fp0 ? "1ª" : "3ª", d0, FBPlayer.I.FirstPerson ? "1ª PESSOA (ERRO)" : "3ª pessoa", D(), FBSession.Lab, car ? car.id : "não"));
		float D()
		{
			if (!gc || !head)
			{
				return -1f;
			}
			return Vector3.Distance(gc.transform.position, head.position);
		}
	}

	private IEnumerator Paredoes(string tag)
	{
		yield return Wait(3f);
		Log($"{tag}: paredões agora: {Pares()} · faixas na biblioteca {FBMediaLib.lib.Count}");
		Log(string.Format("{0}: paredões · {1} da cidade ({2}) · {3}", tag, FBRole.CityWalls.Count, string.Join(", ", FBRole.CityWalls.Where((FBCar c) => c).Select(delegate(FBCar c)
		{
			string id = c.id;
			FBParedao.Wall wall3 = FBParedao.State(c);
			return id + ((wall3 != null && wall3.on) ? " tocando" : " calado");
		})), Regra()));
		FBParedao.Wall city = FBRole.CityWalls.Where((FBCar c) => (bool)c && (FBParedao.State(c)?.on ?? false)).Select(FBParedao.State).FirstOrDefault() ?? FBParedao.List().FirstOrDefault((FBParedao.Wall w) => (bool)w.car && w.on && w.owner >= 0 && !w.local);
		if (city == null)
		{
			foreach (FBCar cw in FBRole.CityWalls.Where((FBCar c) => c))
			{
				FBParedao.Wall wall = (from o in FBParedao.List()
					where (bool)o.car && o.car != cw && o.@on
					orderby (o.car.transform.position - cw.transform.position).sqrMagnitude
					select o).FirstOrDefault();
				Log(tag + ": paredões · " + cw.id + " da cidade calado: som mais perto " + ((wall != null) ? string.Format("{0}{1} a {2:0} u", wall.car.id, wall.local ? " (som do açaí)" : "", (wall.car.transform.position - cw.transform.position).magnitude) : "nenhum") + " · " + FBRole.WallDiag);
			}
			yield break;
		}
		Vector3 cp = city.car.transform.position;
		FBCar other = (from c in FBVehicles.list
			where (bool)c && c != city.car && FBParedao.Has(c) && FBVehicles.CarOf(FBMatch.Player) != c && (c.transform.position - cp).magnitude < 150f
			orderby (c.transform.position - cp).sqrMagnitude
			select c).FirstOrDefault();
		if (!other || FBMediaLib.lib.Count == 0)
		{
			Log(tag + ": paredões · sem outro carro com som perto (" + (other ? "sem faixas" : "nenhum carro") + ")");
			yield break;
		}
		int m0 = FBRole.WallMutes;
		FBParedao.Play(other, FBMediaLib.lib[0]);
		Log($"{tag}: paredões · liguei o som do {other.id} a {(other.transform.position - cp).magnitude:0} u do paredão da cidade ({city.car.id})");
		yield return Wait(5f);
		Log($"{tag}: paredões · da cidade ligado {FBParedao.State(city.car)?.on} · {Regra()} · calou {FBRole.WallMutes > m0} · {Pares()} · regra rodou {FBRole.WallTicks}x · {FBRole.WallDiag}");
		Transform T = other.transform;
		Vector3 p = T.position - T.right * 12f;
		float? num = FBWorld.GroundY(p.x, p.y + 3f, p.z, 0.4f, 8f);
		if (num.HasValue)
		{
			float valueOrDefault = num.GetValueOrDefault();
			p.y = valueOrDefault;
		}
		PlayerTo(p, T.eulerAngles.y);
		yield return Wait(0.8f);
		Look(T.position - T.right * 7f + Vector3.up * 3f, T.position + Vector3.up);
		yield return null;
		Shot(tag + "_paredoes");
		FBParedao.SetOn(other, on: false);
		yield return Wait(6f);
		Log($"{tag}: paredões · desliguei o {other.id}: da cidade voltou {FBParedao.State(city.car)?.on} · {Pares()} · {FBRole.WallDiag}");
		static string Pares()
		{
			List<FBParedao.Wall> list = (from w in FBParedao.List()
				where (bool)w.car && w.@on
				select w).ToList();
			int num3 = 0;
			float a = 1E+09f;
			for (int i = 0; i < list.Count; i++)
			{
				for (int j = i + 1; j < list.Count; j++)
				{
					float magnitude = (list[i].car.transform.position - list[j].car.transform.position).magnitude;
					a = Mathf.Min(a, magnitude);
					if (magnitude < 260f)
					{
						num3++;
					}
				}
			}
			return string.Format("{0} ligados · pares a menos de {1:0} u: {2} · menor distância {3} u · calados pela regra {4}", list.Count, 260f, num3, (list.Count > 1) ? a.ToString("0") : "-", FBRole.WallMutes);
		}
		static string Regra()
		{
			int num2 = 0;
			foreach (FBCar cw2 in FBRole.CityWalls)
			{
				FBParedao.Wall wall2 = FBParedao.State(cw2);
				if ((bool)cw2 && wall2 != null && wall2.on && FBParedao.List().Any((FBParedao.Wall o) => (bool)o.car && o.car != cw2 && o.on && (o.car.transform.position - cw2.transform.position).magnitude < 260f))
				{
					num2++;
				}
			}
			return $"paredões da cidade tocando colados em outro som: {num2}";
		}
	}

	private IEnumerator Perseguicao(string tag)
	{
		yield return BackToStart();
		for (int run = 0; run < 3; run++)
		{
			if (run > 0)
			{
				yield return BackToStart();
			}
			bool ok = false;
			string why = null;
			for (int i = 0; i < 25; i++)
			{
				if (ok)
				{
					break;
				}
				ok = FBTransito.StartChase(force: true);
				why = FBTransito.ChaseWhy;
				if (!ok)
				{
					yield return Wait(2f);
				}
			}
			if (!ok)
			{
				Log(tag + ": perseguição NÃO começou (" + why + ")");
				break;
			}
			FBTransito.Car cop = FBTransito.ChaseCop;
			FBTransito.Car f = cop?.fugitive;
			if (cop == null || f == null)
			{
				Log(tag + ": perseguição sem viatura");
				break;
			}
			string fk = ((f.car.kind == "moto") ? "moto" : "carro");
			int e0 = FBTransito.ChaseEnds;
			int c0 = FBTransito.ChaseCaught;
			int k0 = FBTransito.ChaseKTurns;
			float gMin = 1E+09f;
			float gMax = 0f;
			float vMax = 0f;
			float vCop = 0f;
			float tp = 0f;
			int sirOn = 0;
			int j = 0;
			int sig = FBTransito.SigStops;
			int y0 = FBTransito.Car.Yields;
			float copStop = 0f;
			for (float t = 0f; t < 80f; t += Time.deltaTime)
			{
				if (FBTransito.ChaseEnds != e0)
				{
					break;
				}
				if (!cop.car)
				{
					break;
				}
				if (f.car == null)
				{
					break;
				}
				Transform transform = cop.car.transform;
				float magnitude = (cop.pos - f.pos).magnitude;
				gMin = Mathf.Min(gMin, magnitude);
				gMax = Mathf.Max(gMax, magnitude);
				vMax = Mathf.Max(vMax, f.v);
				vCop = Mathf.Max(vCop, cop.v);
				j++;
				if (FBSiren.Mode(cop.car) > 0 && FBSiren.Lights(cop.car))
				{
					sirOn++;
				}
				if (cop.v < 0.5f && !f.caught)
				{
					copStop += Time.deltaTime;
				}
				float num;
				tp = (num = tp - Time.deltaTime);
				if (num <= 0f)
				{
					tp = 1.5f;
					Vector3 p = transform.position - transform.forward * 22f + transform.right * 10f;
					float? num2 = FBWorld.GroundY(p.x, p.y + 4f, p.z, 0.4f, 10f);
					if (num2.HasValue)
					{
						float valueOrDefault = num2.GetValueOrDefault();
						p.y = valueOrDefault;
					}
					PlayerTo(p, transform.eulerAngles.y);
				}
				Vector3 vector = (((f.pos - transform.position).magnitude < 45f) ? Vector3.Lerp(transform.position, f.pos, 0.5f) : (transform.position + transform.forward * 9f));
				Look(transform.position - transform.forward * 9f + transform.right * 2.5f + Vector3.up * 3.5f, vector + Vector3.up);
				if (Mathf.Repeat(t, 5f) < Time.deltaTime && (f.v < 0.6f || cop.v < 0.5f))
				{
					Log($"{tag}: perseguição {run} t={t:0}s PARADO · fugitivo: {FBTransito.Chain(f)} · viatura: {FBTransito.Chain(cop)}");
				}
				if (Mathf.Repeat(t, 5f) < Time.deltaTime)
				{
					Shot($"{tag}_perseguicao{run}_{(int)(t / 5f)}");
					Log($"{tag}: perseguição {run} t={t:0}s · viatura {cop.car.id} {cop.v * 3.6f / 1.17f:0} km/h · {fk} {f.car.id} {f.v * 3.6f / 1.17f:0} km/h · distância {magnitude:0} m · sirene {FBSiren.Mode(cop.car)} giroflex {FBSiren.Lights(cop.car)} · pegou {f.caught} · frente da viatura: {cop.leadWho} · rota comum {cop.route.Intersect(f.route).Count()}");
				}
				yield return null;
			}
			Log($"{tag}: PERSEGUIÇÃO {run} ({fk}): fim '{FBTransito.ChaseWhy}' · meias-voltas nesta perseguição {FBTransito.ChaseKTurns - k0} · viatura parada {copStop:0} s · carros que abriram caminho {FBTransito.Car.Yields - y0} · empurrões em parados {FBTransito.Car.Pushes} · objetos da rua empurrados {FBTransito.PropPushes} · passou rente por quem encostou {FBTransito.SirenPasses} · pegou {FBTransito.ChaseCaught - c0} · distância {gMin:0}–{gMax:0} m · máx fugitivo {vMax * 3.6f / 1.17f:0} km/h, viatura {vCop * 3.6f / 1.17f:0} km/h · sirene+giroflex {sirOn}/{j} quadros · vermelhos respeitados por outros {FBTransito.SigStops - sig}");
			yield return Wait(2f);
		}
	}

	private IEnumerator Acai(string tag, float fps0)
	{
		FBStreetEvents.ForceNear = 42f;
		FBStreetEvents.ForceAcai = true;
		yield return Wait(1.5f);
		FBStreetEvents.ForceNear = -1f;
		if (!FBAcai.Active)
		{
			Log(tag + ": açaí NÃO entrou (" + FBStreetEvents.Log + ")");
			yield break;
		}
		FBCar car = FBAcai.Car;
		AudioSource src = FBAcai.Source;
		FBTransito.Car tc = FBAcai.Traffic;
		object[] obj = new object[8]
		{
			tag,
			car.id,
			ColorUtility.ToHtmlStringRGB(car.paint),
			car.wid,
			Json.S(car.def, "stockWheel"),
			car.soundWallNode ? "SIM (erro)" : "não",
			tc?.vCap,
			null
		};
		object obj2;
		float s2;
		if (!src)
		{
			obj2 = "AUSENTE";
		}
		else
		{
			string[] obj3 = new string[5]
			{
				src.clip.name,
				" loop=",
				src.loop.ToString(),
				" 3D=",
				null
			};
			s2 = src.spatialBlend;
			obj3[4] = s2.ToString();
			obj2 = string.Concat(obj3);
		}
		obj[7] = obj2;
		Log(string.Format("{0}: açaí {1} · cor {2} · roda {3} (fábrica {4}) · paredão {5} · teto {6:0.0} m/s · som {7}", obj));
		int samples = 0;
		int offRoad = 0;
		int overlap = 0;
		int restarts = 0;
		int srcCount = 0;
		float vSum = 0f;
		float vMax = 0f;
		float latMax = 0f;
		float stuck = 0f;
		float lastT = (src ? src.time : 0f);
		float fpsSum = 0f;
		int fpsN = 0;
		float t3;
		for (t3 = 0f; t3 < 40f; t3 += Time.unscaledDeltaTime)
		{
			if (!car)
			{
				Log($"{tag}: açaí sumiu aos {t3:0} s");
				break;
			}
			fpsSum += 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
			fpsN++;
			Vector3 position = car.transform.position;
			Vector3 forward = car.transform.forward;
			Look(position - forward * 9f + car.transform.right * 4f + Vector3.up * 3.2f, position + Vector3.up * 0.8f, 50f);
			if ((bool)src)
			{
				if (src.time + 1f < lastT && lastT < src.clip.length - 1.5f)
				{
					restarts++;
				}
				lastT = src.time;
			}
			if ((int)(t3 * 2f) != (int)((t3 - Time.unscaledDeltaTime) * 2f))
			{
				samples++;
				if (!FBRoads.IsRoad(position))
				{
					offRoad++;
				}
				float lat;
				FBSeg fBSeg = FBRoads.NearestLane(position, 8f, out s2, out lat, forward);
				latMax = Mathf.Max(latMax, (fBSeg != null) ? Mathf.Abs(lat) : 9f);
				float num = tc?.v ?? car.v;
				vSum += num;
				vMax = Mathf.Max(vMax, num);
				stuck = ((!(num < 0.2f)) ? 0f : (stuck + 0.5f));
				foreach (FBCar item in FBVehicles.list)
				{
					if ((bool)item && item != car && FBTransito.BoxHit(position, forward.Flat().normalized, car.lbox.extents.z * car.scale * 0.95f, car.lbox.extents.x * car.scale * 0.95f, item))
					{
						overlap++;
						break;
					}
				}
				srcCount = car.GetComponentsInChildren<AudioSource>().Count((AudioSource s) => (bool)s.clip && s.clip.name == "acai_carro");
			}
			if (Mathf.Abs(t3 - 3f) < 0.02f || (t3 > 3f && t3 - Time.unscaledDeltaTime < 3f))
			{
				Shot(tag + "_acai_a");
			}
			if (t3 > 14f && t3 - Time.unscaledDeltaTime <= 14f)
			{
				Shot(tag + "_acai_b");
			}
			yield return null;
		}
		Log($"{tag}: açaí 40 s · fora do asfalto {offRoad}/{samples} · longe da faixa máx {latMax:0.00} m · vel média {vSum / (float)Mathf.Max(1, samples):0.0} m/s (máx {vMax:0.0}) · sobreposição {overlap} · parado no fim {stuck:0} s · fontes do jingle {srcCount} · reinícios {restarts} · FPS {fps0:0} -> {fpsSum / (float)Mathf.Max(1, fpsN):0}");
		float[] buf;
		float off2;
		int j;
		int pass2;
		if ((bool)car && (bool)src)
		{
			List<AudioListener> others = (from l in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)
				where l.enabled
				select l).ToList();
			foreach (AudioListener item2 in others)
			{
				item2.enabled = false;
			}
			GameObject eg = new GameObject("OuvidoTeste");
			eg.AddComponent<AudioListener>();
			Transform ear = eg.transform;
			buf = new float[2048];
			yield return null;
			List<string> res = new List<string>();
			float[] array = new float[5] { 5f, 15f, 40f, 90f, 160f };
			for (j = 0; j < array.Length; j++)
			{
				t3 = array[j];
				float on2 = 0f;
				off2 = 0f;
				for (pass2 = 0; pass2 < 2; pass2++)
				{
					if (!car)
					{
						break;
					}
					if (FBAcai.Wall != null)
					{
						FBAcai.Mute = pass2 == 1;
					}
					for (float w = 0f; w < 0.8f; w += Time.unscaledDeltaTime)
					{
						if ((bool)car)
						{
							ear.position = car.transform.position + car.transform.right * t3 + Vector3.up * 1.6f;
						}
						yield return null;
					}
					float acc = 0f;
					for (int i = 0; i < 24; i++)
					{
						if (!car)
						{
							break;
						}
						ear.position = car.transform.position + car.transform.right * t3 + Vector3.up * 1.6f;
						ear.rotation = Quaternion.LookRotation(-car.transform.right);
						acc += Rms();
						yield return null;
					}
					if (pass2 == 0)
					{
						on2 = acc / 24f;
					}
					else
					{
						off2 = acc / 24f;
					}
				}
				FBAcai.Mute = false;
				res.Add($"{t3:0} m: {on2:0.0000} (sem {off2:0.0000})");
			}
			Log(tag + ": açaí nível na saída — " + string.Join(" · ", res) + $" · volume do paredão {FBAcai.Wall?.vol:0.00} · prédio na frente {FBAcai.Wall?.occK:0.00}");
			UnityEngine.Object.Destroy(eg);
			foreach (AudioListener item3 in others)
			{
				if ((bool)item3)
				{
					item3.enabled = true;
				}
			}
		}
		float tl = -1f;
		float tg = -1f;
		for (t3 = 0f; t3 < 150f; t3 += Time.unscaledDeltaTime)
		{
			if (!FBAcai.Active)
			{
				break;
			}
			if (tl < 0f && FBAcai.Leaving)
			{
				tl = t3;
			}
			if ((bool)FBAcai.Car)
			{
				Transform transform = FBAcai.Car.transform;
				Look(transform.position - transform.forward * 9f + Vector3.up * 3.2f, transform.position + Vector3.up, 50f);
			}
			yield return null;
		}
		if (!FBAcai.Active)
		{
			tg = 1f;
		}
		Log(tag + ": açaí indo embora " + ((tl >= 0f) ? $"aos {tl:0} s depois da medição" : "NÃO") + " · saiu de cena " + ((tg > 0f) ? "sim" : "NÃO (ainda rodando após 150 s)"));
		if (!FBAcai.Active)
		{
			FBStreetEvents.ForceNear = 42f;
			FBStreetEvents.ForceAcai = true;
			yield return Wait(1.5f);
			FBStreetEvents.ForceNear = -1f;
			yield return Wait(4f);
		}
		if (!FBAcai.Active || !FBAcai.Car || FBAcai.Traffic == null)
		{
			Log(tag + ": açaí sob tiro · carro NÃO entrou para o teste (" + FBStreetEvents.Log + ")");
		}
		if (!FBAcai.Active || !FBAcai.Car || FBAcai.Traffic == null)
		{
			yield break;
		}
		FBCar car2 = FBAcai.Car;
		t3 = FBAcai.Traffic.v;
		off2 = t3;
		j = 0;
		pass2 = 0;
		FBBotBrain.Noise(car2.transform.position + car2.transform.right * 8f + Vector3.up);
		for (float on2 = 0f; on2 < 8f; on2 += Time.unscaledDeltaTime)
		{
			if (!FBAcai.Car)
			{
				break;
			}
			if (FBAcai.Traffic != null)
			{
				off2 = Mathf.Max(off2, FBAcai.Traffic.v);
			}
			pass2++;
			if (!FBRoads.IsRoad(FBAcai.Car.transform.position))
			{
				j++;
			}
			Transform transform2 = FBAcai.Car.transform;
			Look(transform2.position - transform2.forward * 9f + Vector3.up * 3.5f, transform2.position);
			if (on2 > 3f && on2 - Time.unscaledDeltaTime <= 3f)
			{
				Shot(tag + "_acai_fuga");
			}
			yield return null;
		}
		Log($"{tag}: açaí sob tiro · fugindo {FBAcai.Fleeing} · velocidade {t3:0.0} -> máx {off2:0.0} m/s em 8 s · fora do asfalto {j}/{pass2}");
		float Rms()
		{
			float num2 = 0f;
			AudioListener.GetOutputData(buf, 0);
			float[] array2 = buf;
			foreach (float num3 in array2)
			{
				num2 += num3 * num3;
			}
			AudioListener.GetOutputData(buf, 1);
			array2 = buf;
			foreach (float num4 in array2)
			{
				num2 += num4 * num4;
			}
			return Mathf.Sqrt(num2 / (float)(2 * buf.Length));
		}
	}

	private IEnumerator Baculejo(string tag, float fps0)
	{
		shotRoda = false;
		List<FBCharacter> botsOff = FBMatch.Bots.Where((FBCharacter b) => (bool)b && b.gameObject.activeSelf).ToList();
		foreach (FBCharacter item in botsOff)
		{
			item.gameObject.SetActive(value: false);
		}
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbbacdesfecho");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			FBBaculejo.ForceOutcome = commandLineArgs[num + 1];
		}
		FBStreetEvents.ForceNear = 30f;
		FBStreetEvents.ForceBaculejo = true;
		yield return Wait(1.2f);
		FBStreetEvents.ForceNear = -1f;
		if (!FBBaculejo.Active)
		{
			Log(tag + ": baculejo NÃO montou (" + FBStreetEvents.Log + ")");
			yield break;
		}
		List<FBBaculejo.Act> sus = FBBaculejo.Suspects.ToList();
		Log($"{tag}: baculejo r42 · desfecho {FBBaculejo.Outcome} · {sus.Count} na roda");
		Vector3 c0 = ((sus.Count > 0 && (bool)sus[0].c) ? sus[0].c.Pos : FBStreetEvents.PlayerPos);
		for (float w2 = 0f; w2 < 30f; w2 += Time.unscaledDeltaTime)
		{
			if (!FBBaculejo.Active)
			{
				break;
			}
			if (!(FBBaculejo.State == "roda"))
			{
				break;
			}
			if (w2 > 2f && !shotRoda)
			{
				shotRoda = true;
				Look(c0 + new Vector3(3.5f, 2.2f, 3.5f), c0 + Vector3.up, 50f);
				yield return null;
				Shot(tag + "_bac_roda");
			}
			yield return null;
		}
		FBCar car = FBBaculejo.Car;
		FBBaculejo.Act fr = FBBaculejo.Frisker;
		FBBaculejo.Act cv = FBBaculejo.Cover;
		if (!car)
		{
			Log(tag + ": baculejo sem viatura depois da roda (estado " + FBBaculejo.State + ")");
			yield break;
		}
		Log(string.Format("{0}: baculejo · viatura {1} no asfalto {2} · {3} abordado(s) ({4}) · PMs {5}/{6} · arma da cobertura {7}", tag, car.id, FBRoads.IsRoad(car.transform.position), sus.Count, string.Join(",", sus.Select((FBBaculejo.Act s) => (!s.c) ? "?" : (s.c.skinId + (s.borrowed ? "[da rua]" : "[novo]")))), fr.c?.skinId, cv.c?.skinId, cv.c?.weapon?.id ?? "NENHUMA"));
		string last = null;
		float fpsSum = 0f;
		int fpsN = 0;
		int bad = 0;
		HashSet<string> shots = new HashSet<string>();
		float minGap = 9f;
		float maxGap = 0f;
		for (float w2 = 0f; w2 < 200f; w2 += Time.unscaledDeltaTime)
		{
			if (!FBBaculejo.Active)
			{
				break;
			}
			fpsSum += 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
			fpsN++;
			string st = FBBaculejo.State;
			if (st != last)
			{
				Log($"{tag}: baculejo estado {st} aos {w2:0.0} s");
				last = st;
			}
			Vector3 vector = (from s in sus
				where s.c
				select s.c.Pos).DefaultIfEmpty(c0).Aggregate(Vector3.zero, (Vector3 a, Vector3 b) => a + b) / Mathf.Max(1, sus.Count((FBBaculejo.Act s) => s.c));
			if (st == "partiu" && (bool)FBBaculejo.Car)
			{
				Transform transform = FBBaculejo.Car.transform;
				Look(transform.position - transform.forward * 10f + transform.right * 3f + Vector3.up * 3.5f, transform.position + Vector3.up, 50f);
			}
			else if ((st == "chegada" || st == "encosta") && (bool)FBBaculejo.Car)
			{
				Transform transform2 = FBBaculejo.Car.transform;
				Look(vector + (transform2.position - vector).Flat().normalized * -4f + Vector3.up * 3f, transform2.position + Vector3.up);
			}
			else if (st == "perseguicao" || st == "confronto")
			{
				List<Vector3> list = (from s in sus
					where s.c
					select s.c.Pos).Concat(from x in new FBCharacter[2]
					{
						fr?.c,
						cv?.c
					}
					where x
					select x.Pos).ToList();
				Vector3 vector2 = list.Aggregate(Vector3.zero, (Vector3 a, Vector3 b) => a + b) / Mathf.Max(1, list.Count);
				Look(vector2 + new Vector3(0f, 9f, -7f), vector2, 60f);
			}
			else if ((bool)car)
			{
				Look(ViewPoint(vector, car.transform.position), vector + Vector3.up * 1f, 50f);
			}
			if ((int)(w2 * 2f) != (int)((w2 - Time.unscaledDeltaTime) * 2f))
			{
				foreach (FBBaculejo.Act item2 in new FBBaculejo.Act[2] { fr, cv }.Concat(sus))
				{
					if ((bool)item2?.c && item2.c.alive && !item2.c.vehiclePose && !FBStreetEvents.Free(item2.c.Pos, 0.2f))
					{
						bad++;
						if (bad <= 6)
						{
							Log($"{tag}: {item2.c.skinId} com a cápsula dentro de algo em {item2.c.Pos} ({st})");
						}
					}
				}
				if (st == "revista" && (bool)fr?.c && fr.c.dance != null)
				{
					foreach (FBBaculejo.Act item3 in sus)
					{
						if ((bool)item3.c && !item3.fled)
						{
							float magnitude = (item3.c.Pos - fr.c.Pos).Flat().magnitude;
							if (magnitude < 1f)
							{
								minGap = Mathf.Min(minGap, magnitude);
								maxGap = Mathf.Max(maxGap, magnitude);
							}
						}
					}
				}
			}
			string[] array = new string[14]
			{
				"chegada", "encosta", "desembarque", "abordagem", "enquadro", "revista", "anota", "fala", "libera", "volta",
				"partiu", "perseguicao", "confronto", "tumulto"
			};
			foreach (string text in array)
			{
				if (st == text && !shots.Contains(text))
				{
					shots.Add(text);
					StartCoroutine(Later(0.9f, tag + "_bac_" + text));
				}
			}
			if (st == "revista" && w2 > 6f && !shots.Contains("revista2") && (bool)car)
			{
				shots.Add("revista2");
				Vector3 normalized = (vector - car.transform.position).Flat().normalized;
				Vector3 vector3 = new Vector3(normalized.z, 0f, 0f - normalized.x);
				Look(vector + vector3 * 2.6f - normalized * 0.6f + Vector3.up * 1.5f, vector + Vector3.up * 1.05f);
				yield return null;
				Shot(tag + "_bac_revista_perto");
				yield return null;
				yield return null;
			}
			if (st == "anota" && !shots.Contains("giroflex") && (bool)car && FBGiroflex.Lit(car))
			{
				shots.Add("giroflex");
				Transform transform3 = car.transform;
				Look(transform3.position + transform3.forward * 6.5f + transform3.right * 2.5f + transform3.up * 3.2f, transform3.position + transform3.up * 1.9f, 42f);
				yield return null;
				for (int w5 = 0; w5 < 60; w5++)
				{
					if (FBGiroflex.Lit(car))
					{
						break;
					}
					yield return null;
				}
				Shot(tag + "_bac_giroflex");
				yield return null;
				yield return null;
			}
			if (!shots.Contains("dentro") && (bool)FBBaculejo.Car && (bool)fr?.c && (bool)cv?.c && fr.c.vehiclePose && cv.c.vehiclePose)
			{
				shots.Add("dentro");
				yield return Wait(0.6f);
				FBCar u = FBBaculejo.Car;
				Transform T = u.transform;
				Vector3 vector4 = u.L2W(u.seat) + T.up * 0.35f;
				Vector3 vector5 = u.L2W(new Vector3(0f - u.seat.x, u.seat.y, u.seat.z)) + T.up * 0.35f;
				float num2 = u.lbox.extents.x * u.scale + 1.1f;
				Animator an = cv.c.an;
				int num3 = (an ? an.GetLayerIndex("Upper Body") : (-1));
				Log($"{tag}: sentados · motorista {fr.c.skinId} clipe {fr.c.sys} · carona {cv.c.skinId} clipe {cv.c.sys} Upper Body {((num3 >= 0) ? an.GetLayerWeight(num3) : (-1f)):0.00} · giroflex próprio {FBGiroflex.OwnCount(u)}");
				(string, Vector3, Vector3)[] array2 = new(string, Vector3, Vector3)[4]
				{
					("janela_motorista", vector4 + T.right * Mathf.Sign(u.seat.x) * num2 + T.up * 0.25f, vector4),
					("janela_carona", vector5 - T.right * Mathf.Sign(u.seat.x) * num2 + T.up * 0.25f, vector5),
					("parabrisa", Vector3.Lerp(vector4, vector5, 0.5f) + T.forward * 3.2f + T.up * 0.5f, Vector3.Lerp(vector4, vector5, 0.5f)),
					("teto", T.position + T.forward * 7f + T.right * 3f + T.up * 3.5f, T.position + T.up * 1.8f)
				};
				for (int w5 = 0; w5 < array2.Length; w5++)
				{
					var (i, from, at) = array2[w5];
					Look(from, at, (i == "teto") ? 45 : 60);
					yield return null;
					if (i == "teto")
					{
						for (int w = 0; w < 90; w++)
						{
							if (FBGiroflex.Lit(u))
							{
								break;
							}
							Look(T.position + T.forward * 7f + T.right * 3f + T.up * 3.5f, T.position + T.up * 1.8f, 45f);
							yield return null;
						}
					}
					Shot(tag + "_bac_" + i);
					yield return null;
					yield return null;
				}
			}
			yield return null;
		}
		Log($"{tag}: baculejo fim · PM atrás do abordado {minGap:0.00}–{maxGap:0.00} m · cápsulas presas {bad} · FPS {fps0:0} -> {fpsSum / (float)Mathf.Max(1, fpsN):0} · ativo ainda {FBBaculejo.Active}");
		foreach (FBCharacter item4 in botsOff)
		{
			if ((bool)item4)
			{
				item4.gameObject.SetActive(value: true);
			}
		}
	}

	private IEnumerator Veiculos(string tag)
	{
		List<FBCar> source = (from c in FBVehicles.list
			where (bool)c && c.kind == "car" && !c.driver
			group c by c.id into g
			select g.First()).ToList();
		foreach (FBCar item in source.Take(14))
		{
			FBDrive.K kd = FBDrive.KindOf(item);
			FBCarSound.Gearbox gearbox = new FBCarSound.Gearbox(FBDrive.TopV(item));
			float num = 0f;
			float num2 = -1f;
			float num3 = 0f;
			int num4 = 0;
			int num5 = 0;
			List<string> list = new List<string>();
			for (; num3 < 60f; num3 += 1f / 60f)
			{
				float num6 = ((gearbox.shiftT > 0f) ? 0.3f : 1f);
				num += FBDrive.PowerAcc(item, kd, num, 1f) * num6 / 60f;
				num = Mathf.Min(num, FBDrive.TopV(item));
				(float, int, int) tuple = gearbox.Step(1f / 60f, num, 1f);
				if (tuple.Item3 > 0)
				{
					num4++;
					list.Add($"{tuple.Item2}a@{num * 3f:0}");
				}
				else if (tuple.Item3 < 0)
				{
					num5++;
				}
				if (num2 < 0f && num * 3f >= 100f)
				{
					num2 = num3;
				}
			}
			Log(string.Format("{0}: {1} · máxima {2:0} km/h · alcançou {3:0} em 60 s · 0–100 {4} · trocas {5} · reduções {6} · rpm final {7:0} · buzina {8}", tag, item.id, FBDrive.TopKmh(item), num * 3f, (num2 < 0f) ? "não chega" : (num2.ToString("0.0") + " s"), string.Join(" ", list), num5, gearbox.rpm, FBCarSound.HornFor(item)));
		}
		Log(string.Format("{0}: buzinas diferentes em {1} veículos: {2} ({3})", tag, FBVehicles.list.Count((FBCar c) => c), FBVehicles.list.Where((FBCar c) => c).Select(FBCarSound.HornFor).Distinct()
			.Count(), string.Join(",", FBVehicles.list.Where((FBCar c) => c).Select(FBCarSound.HornFor).Distinct())));
		FBCharacter me = FBMatch.Player;
		FBCar car = source.OrderByDescending(delegate(FBCar c)
		{
			c.SetColliders(on: false);
			RaycastHit hit2;
			float result2 = (FBWorld.RayHit(c.transform.position + Vector3.up, c.transform.forward, 60f, out hit2) ? hit2.distance : 60f);
			c.SetColliders(on: true);
			return result2;
		}).FirstOrDefault();
		bool entered = false;
		foreach (FBCar cand in source.OrderByDescending(delegate(FBCar c)
		{
			c.SetColliders(on: false);
			RaycastHit hit;
			float result = (FBWorld.RayHit(c.transform.position + Vector3.up, c.transform.forward, 60f, out hit) ? hit.distance : 60f);
			c.SetColliders(on: true);
			return result;
		}).Take(5))
		{
			if (!me)
			{
				break;
			}
			CharacterController component = me.GetComponent<CharacterController>();
			if ((bool)component)
			{
				component.enabled = false;
			}
			me.transform.position = cand.transform.position + cand.transform.right * (0f - (cand.lbox.extents.x * cand.scale + 0.8f));
			if ((bool)component)
			{
				component.enabled = true;
			}
			yield return null;
			if (FBDrive.Enter(me, cand))
			{
				car = cand;
				entered = true;
				break;
			}
		}
		if ((bool)me && (bool)car)
		{
			if (entered)
			{
				int steps = 0;
				FBFootsteps.trace = delegate(FBFootsteps.Trace r)
				{
					if (r.ch == me)
					{
						steps++;
					}
				};
				float vmax = 0f;
				for (float t2 = 0f; t2 < 4.5f; t2 += Time.deltaTime)
				{
					FBDrive.Simulate(car, me, 1f, 0f, space: false, Time.deltaTime);
					vmax = Mathf.Max(vmax, car.v);
					Transform transform = car.transform;
					Look(transform.position - transform.forward * 7f + Vector3.up * 2.6f, transform.position + transform.forward * 4f);
					yield return null;
				}
				Shot(tag + "_velocimetro");
				for (float t2 = 0f; t2 < 3f; t2 += Time.deltaTime)
				{
					if (!(Mathf.Abs(car.v) > 0.2f))
					{
						break;
					}
					FBDrive.Simulate(car, me, -1f, 0f, space: false, Time.deltaTime);
					yield return null;
				}
				FBFootsteps.trace = null;
				Log($"{tag}: dirigindo {car.id} · {vmax * 3f:0} km/h em 4,5 s · marcha {car.gear} · passos do jogador dentro do carro {steps}");
				FBDrive.Exit(me, force: true);
				yield return Wait(0.5f);
			}
			else
			{
				Log(tag + ": não entrou no " + car.id);
			}
		}
		if ((bool)me)
		{
			Transform transform2 = me.transform;
			Look(transform2.position - transform2.forward * 4f + Vector3.up * 2.2f, transform2.position + Vector3.up * 1.2f);
		}
		if ((bool)Camera.main && (bool)cam)
		{
			Camera.main.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
		}
		FBScreenshot.CaptureReel();
		yield return Wait(4f);
		string lastReel = FBScreenshot.LastReel;
		if (lastReel != null && File.Exists(lastReel))
		{
			FileInfo fileInfo = new FileInfo(lastReel);
			Texture2D texture2D = new Texture2D(2, 2);
			texture2D.LoadImage(File.ReadAllBytes(lastReel));
			Log($"{tag}: reels 9:16 {texture2D.width}×{texture2D.height} · {(float)fileInfo.Length / 1048576f:0.0} MB · {lastReel}");
			UnityEngine.Object.Destroy(texture2D);
		}
		else
		{
			Log(tag + ": reels 9:16 NÃO salvou");
		}
		if ((bool)me)
		{
			GameObject bo = FBSkyPlanes.Boeing();
			if ((bool)bo)
			{
				Vector3 vector = me.Pos + me.transform.forward * 70f + Vector3.up * 30f;
				bo.transform.position = vector;
				bo.transform.rotation = Quaternion.LookRotation(me.transform.right);
				Look(vector - me.transform.forward * 60f + me.transform.right * 40f + Vector3.up * 15f, vector);
				yield return null;
				yield return null;
				Shot(tag + "_boeing_perto");
				yield return null;
				UnityEngine.Object.Destroy(bo);
			}
			Log(tag + ": boeing787 " + (bo ? "carregou" : "NÃO carregou (sem Acc/boeing787)"));
			FBSkyPlanes.Flyover(me.Pos, me.Pos.y);
			yield return Wait(9f);
			Transform pl = FBSkyPlanes.Current;
			Camera main = Camera.main;
			if ((bool)pl && (bool)main)
			{
				Look(main.transform.position, pl.position, 30f);
				yield return null;
				Shot(tag + "_aviao_ceu");
				Log($"{tag}: avião no céu {pl.name} escala {pl.localScale.x:0.00}");
			}
			else
			{
				Log(tag + ": avião do céu não apareceu");
			}
		}
	}

	private IEnumerator Later(float s, string name)
	{
		yield return Wait(s);
		Shot(name);
	}

	private Vector3 ViewPoint(Vector3 ctr, Vector3 carP)
	{
		if ((ctr - viewFor).sqrMagnitude < 1f && viewCache != Vector3.zero)
		{
			return viewCache;
		}
		Vector3 vector = ctr + Vector3.up * 1.1f;
		Vector3 normalized = (ctr - carP).Flat().normalized;
		Vector3 result = ctr - normalized * 7f + Vector3.up * 2.4f;
		for (int i = 0; i < 24; i++)
		{
			float y = (float)i * 15f;
			float num = 5 + i % 3;
			Vector3 vector2 = Quaternion.Euler(0f, y, 0f) * normalized;
			Vector3 vector3 = ctr + vector2 * num + Vector3.up * 2.2f;
			Vector3 vector4 = vector - vector3;
			if (!Physics.Raycast(vector3, vector4.normalized, vector4.magnitude - 0.4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && !Physics.CheckSphere(vector3, 0.3f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
			{
				result = vector3;
				break;
			}
		}
		viewFor = ctr;
		viewCache = result;
		return result;
	}
}
}
