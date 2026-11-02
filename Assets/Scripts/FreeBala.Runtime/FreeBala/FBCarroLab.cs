using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBCarroLab : MonoBehaviour
{
	private string run = "vila_jardim:role";

	private string dir;

	private float cThr;

	private float cSt;

	private Camera cam;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbcarrolab");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_CarroLab");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBCarroLab fBCarroLab = obj.AddComponent<FBCarroLab>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				fBCarroLab.run = commandLineArgs[num + 1];
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][carrolab] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private IEnumerator Shot(string n)
	{
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, n + ".png"));
		yield return null;
		yield return null;
	}

	private void Look(Vector3 from, Vector3 at, float fov = 50f)
	{
		cam.transform.position = from;
		cam.transform.LookAt(at);
		cam.fieldOfView = fov;
	}

	private IEnumerator Reta(FBCar dc, string quando)
	{
		GameObject gameObject = GameObject.Find("FB_Muro");
		if ((bool)gameObject)
		{
			gameObject.SetActive(value: false);
		}
		Transform T = dc.transform;
		dc.v = 0f;
		yield return Wait(0.5f);
		float yaw0 = T.eulerAngles.y;
		cThr = 1f;
		cSt = 0f;
		float vm = 0f;
		for (float t = 0f; t < 7f; t += Time.deltaTime)
		{
			vm = Mathf.Max(vm, dc.v * 3f);
			yield return null;
		}
		cThr = 0f;
		float num = Mathf.DeltaAngle(yaw0, T.eulerAngles.y);
		Log($"reta de 7 s sem volante {quando}: desvio {num:+0.0;-0.0}° (− = esquerda) · máxima {vm:0} km/h · {FBCarDamage.DebugInfo(dc)}");
		cThr = -1f;
		yield return Wait(2f);
		cThr = 0f;
		yield return Wait(1f);
		dc.v = 0f;
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste", "carro");
		Directory.CreateDirectory(dir);
		yield return Wait(3f);
		string[] array = run.Split(':');
		GameData.Load();
		FBSession.Load();
		(string map, string mode, string view) keep = (map: FBSession.map, mode: FBSession.mode, view: FBSession.view);
		FBSession.map = array[0];
		FBSession.mode = ((array.Length > 1) ? array[1] : "role");
		FBSession.online = false;
		FBSession.view = "tps";
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + array[0]);
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		yield return Wait(8f);
		FBCharacter me = FBMatch.Player;
		if (!me)
		{
			Log("sem jogador");
			yield break;
		}
		FBMatch.God = true;
		if (FBGarage.Open_)
		{
			FBGarage.AutoPick();
			yield return Wait(1.5f);
		}
		if ((bool)FBVehicles.CarOf(me))
		{
			FBDrive.Exit(me, force: true);
			yield return Wait(1f);
		}
		cam = new GameObject("CarroLabCam").AddComponent<Camera>();
		cam.depth = 100f;
		cam.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
		cam.enabled = false;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		List<FBCar> list = FBVehicles.list.Where((FBCar c) => (bool)c && !c.driver && c.kind != "moto" && FBTransito.List.All((FBTransito.Car t) => t.car != c)).ToList();
		float s2;
		for (int m = 0; m < list.Count; m++)
		{
			FBCar fBCar = list[m];
			num3++;
			float lat = 0f;
			FBSeg fBSeg = (FBRoads.Ready ? FBRoads.NearestLane(fBCar.transform.position, 6f, out s2, out lat) : null);
			if (fBSeg != null && Mathf.Abs(lat) < fBSeg.w / 2f * 0.55f)
			{
				num++;
			}
			for (int num4 = m + 1; num4 < list.Count; num4++)
			{
				FBCar fBCar2 = list[num4];
				if ((fBCar2.transform.position - fBCar.transform.position).sqrMagnitude < 64f)
				{
					Bounds lbox = fBCar.lbox;
					Vector3 vector = lbox.extents * fBCar.scale;
					if (FBTransito.BoxHit(fBCar.transform.TransformPoint(lbox.center), fBCar.transform.forward, vector.z * 0.95f, vector.x * 0.95f, fBCar2))
					{
						num2++;
					}
				}
			}
		}
		Log($"vagas: {FBVehicles.ParkOk} ok · {FBVehicles.ParkMoved} movidos · {FBVehicles.ParkDropped} sem lugar · erro do chão nas rodas até {FBVehicles.SettleErr:0.000} m · agora {num3} estacionados, {num} no meio da faixa, {num2} pares sobrepostos");
		foreach (string item2 in FBVehicles.ParkLog.Take(12))
		{
			Log("  vaga: " + item2);
		}
		Transform transform = (me.an ? me.an.GetBoneTransform(HumanBodyBones.Head) : null);
		float num5 = (transform ? (transform.position.y - me.Pos.y) : (-1f));
		float j = num5 / 1.62f;
		FBCar fBCar3 = FBVehicles.list.FirstOrDefault((FBCar c) => (bool)c && c.kind != "moto" && !c.heavy);
		float num6 = (fBCar3 ? (fBCar3.lbox.size.y * fBCar3.scale) : (-1f));
		Log(string.Format("escala: cabeça do jogador a {0:0.00} u do chão (real ~1,62 m -> {1:0.00} u/m) · carro {2} {3:0.00} u ({4:0.00} m; real ~1,45 m) · {5} · {6} · {7} · {8} · {9} · {10}", num5, j, fBCar3?.id, num6, num6 / j, H("SM_lixeiras", 1f), H("SM_Orelhao", 2.3f), H("SM_Mesa1", 0.72f), H("SM_Cadeira1", 0.8f), H("Placas", 2.6f), H("PontoOnibus", 2.7f)));
		Log($"semáforos: {FBSemaforo.Junctions} cruzamentos · {FBSemaforo.Heads} postes");
		List<Transform> list2 = (from t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
			where t.name.StartsWith("semaforo_")
			orderby (t.position - me.Pos).sqrMagnitude
			select t).Take(2).ToList();
		foreach (Transform h in list2)
		{
			_ = -h.right;
			cam.enabled = true;
			Look(h.position + Vector3.up * 3.5f - h.forward * 1f + h.right * 9f, h.position + Vector3.up * 3.6f + h.forward * 1.5f, 45f);
			for (int k = 0; k < 3; k++)
			{
				yield return Shot($"semaforo_{h.name}_{k}");
				yield return Wait(4f);
			}
			cam.enabled = false;
		}
		int in0;
		int out3;
		float t6;
		int van;
		if (FBTransito.Running)
		{
			FBTransito.TestParking();
			Time.timeScale = 2f;
			t6 = Time.unscaledTime;
			int k = 0;
			int wrong = 0;
			int offLane = 0;
			int vanishSeen = 0;
			int fastSeen = 0;
			Dictionary<FBCar, (Vector3 p, bool vis, float t, float v)> last = new Dictionary<FBCar, (Vector3, bool, float, float)>();
			int sig0 = FBTransito.SigStops;
			out3 = FBTransito.OutsDone;
			in0 = FBTransito.InsDone;
			van = FBTransito.Vanished;
			Camera mc = Camera.main;
			while (Time.unscaledTime - t6 < 60f)
			{
				yield return Wait(0.5f);
				k++;
				Plane[] array2 = (mc ? GeometryUtility.CalculateFrustumPlanes(mc) : null);
				foreach (FBTransito.Car item3 in FBTransito.List)
				{
					if (!item3.car)
					{
						continue;
					}
					Vector3 position = item3.car.transform.position;
					bool item = array2 != null && GeometryUtility.TestPlanesAABB(array2, new Bounds(position + Vector3.up, new Vector3(4f, 2f, 4f))) && (position - mc.transform.position).magnitude < 120f;
					if (last.TryGetValue(item3.car, out (Vector3, bool, float, float) value) && value.Item2 && (position - value.Item1).magnitude > 15f)
					{
						float num7 = Time.time - value.Item3;
						float magnitude = (position - value.Item1).magnitude;
						float num8 = Mathf.Max(item3.v, value.Item4);
						bool flag = magnitude < num8 * num7 * 1.3f + 4f;
						if (flag)
						{
							fastSeen++;
						}
						else
						{
							vanishSeen++;
						}
						Log(string.Format("  salto à vista: {0} {1} modo {2} · {3:0} u em {4:0.0} s de jogo · {5:0} km/h · {6}{7}", item3.car.mx ? "moto" : "carro", item3.car.id, item3.mode, magnitude, num7, num8 / 1.17f * 3.6f, flag ? "andou (rápido)" : "SUMIU/REAPARECEU", (item3.runLights || item3.fugitive != null || item3.chase != null) ? " · perseguição" : ""));
					}
					last[item3.car] = (position, item, Time.time, item3.v);
					if (item3.mode != "drive" || item3.route.Count == 0 || item3.ri >= item3.route.Count)
					{
						continue;
					}
					FBSeg fBSeg2 = FBRoads.segs[item3.route[item3.ri]];
					FBRoads.At(fBSeg2, item3.s, out var tan);
					tan.y = 0f;
					Vector3 forward = item3.car.transform.forward;
					forward.y = 0f;
					if (fBSeg2.kind == 0 && item3.overSide == 0 && Vector3.Dot(tan.normalized, forward.normalized) < 0f)
					{
						wrong++;
					}
					if (fBSeg2.kind == 0 && item3.overSide == 0)
					{
						FBRoads.Project(fBSeg2, position, out s2, out var dist);
						if (dist > 2.5f)
						{
							offLane++;
						}
					}
				}
			}
			Time.timeScale = 1f;
			Log($"trânsito 120 s de jogo: {FBTransito.List.Count} carros · amostras {k} · contramão {wrong} · fora da faixa {offLane} · sumiu À VISTA {vanishSeen} (rápidos à vista, não somem: {fastSeen}) · reaparecidos longe {FBTransito.Vanished - van} · esperando sair da vista {FBTransito.VanishWaits} · paradas no semáforo {FBTransito.SigStops - sig0} · desceram pelo clipe {FBTransito.OutsDone - out3} · entraram pelo clipe {FBTransito.InsDone - in0}");
		}
		else
		{
			Log("trânsito não está rodando neste modo");
		}
		FBWeather.ForceNight(on: true);
		yield return Wait(3f);
		van = FBCarLights.LensToggles;
		in0 = FBCarLights.BeamSwaps;
		t6 = Time.unscaledTime;
		out3 = 0;
		while (Time.unscaledTime - t6 < 20f)
		{
			out3++;
			yield return null;
		}
		int num9 = FBVehicles.list.Count((FBCar c) => (bool)c && ((bool)c.driver || c.aiLights));
		Log($"luzes à noite (20 s, {num9} carros com motor ligado): lentes trocando {(float)(FBCarLights.LensToggles - van) / 20f:0.0}/s no total ({(float)(FBCarLights.LensToggles - van) / 20f / (float)Mathf.Max(1, num9):0.00}/s por carro; freio/seta legítimos entram aqui) · fachos trocando de carro {(float)(FBCarLights.BeamSwaps - in0) / 20f:0.00}/s · fachos acesos {FBCarLights.BeamsOn}");
		FBTransito.Car nc = (from c in FBTransito.List
			where (bool)c.car && c.mode == "drive"
			orderby (c.pos - me.Pos).sqrMagnitude
			select c).FirstOrDefault();
		if (nc != null)
		{
			cam.enabled = true;
			for (int sig0 = 0; sig0 < 3; sig0++)
			{
				Transform h = nc.car.transform;
				Look(h.position - h.forward * 7f + h.right * 2.5f + Vector3.up * 2f, h.position + Vector3.up * 0.7f, 55f);
				yield return Shot($"luz_noite_tras_{sig0}");
				Look(h.position + h.forward * 9f + h.right * 2f + Vector3.up * 1.6f, h.position + Vector3.up * 0.7f, 55f);
				yield return Shot($"luz_noite_frente_{sig0}");
				yield return Wait(1.5f);
			}
			cam.enabled = false;
		}
		FBWeather.ForceNight(on: false);
		yield return Wait(1f);
		FBCar car = (from c in FBVehicles.list
			where (bool)c && !c.driver && c.kind != "moto" && !c.ie && FBTransito.List.All((FBTransito.Car t) => t.car != c)
			orderby (c.transform.position - me.Pos).sqrMagnitude
			select c).FirstOrDefault();
		if ((bool)car)
		{
			Vector3 p = car.L2W(car.seat) - car.transform.right * 1.6f;
			FBMatch.Teleport(me, p, car.transform.eulerAngles.y + 90f);
			yield return Wait(1f);
			if ((bool)FBVehicles.CarOf(me))
			{
				FBDrive.Exit(me, force: true);
				yield return Wait(0.5f);
			}
			bool ok = FBDrive.Enter(me, car);
			FBDriverPose dp4 = me.GetComponent<FBDriverPose>();
			cam.enabled = true;
			Log($"entrar em {car.id}: pedido {ok} · portas no modelo {FBDoors.HasPart(car, 1)} · caminho {dp4?.path} · {FBDrive.PathDiag}");
			Vector3 side3 = car.transform.position - car.transform.right * 5.5f + Vector3.up * 1.6f;
			float[] array3 = new float[5] { 0.3f, 0.8f, 1.4f, 2f, 2.6f };
			for (out3 = 0; out3 < array3.Length; out3++)
			{
				t6 = array3[out3];
				float t17 = Time.time;
				while (Time.time - t17 < ((t6 == 0.3f) ? 0.3f : 0.6f))
				{
					yield return null;
				}
				Look(side3, car.transform.position + Vector3.up * 0.9f);
				yield return Shot($"entrar_{t6:0.0}".Replace(',', '_'));
				Transform transform2 = (me.an ? me.an.GetBoneTransform(HumanBodyBones.Hips) : null);
				Log(string.Format("entrar t={0:0.0}s: pedido {1} · fase {2} · clipe {3} · porta {4} · quadril×banco {5:0.00} m", t6, ok, dp4?.trans ?? "-", me.sys, FBDoors.Mask(car), transform2 ? (transform2.position - car.L2W(car.seat)).magnitude : (-1f)));
			}
			yield return Wait(1f);
			bool ex2 = FBDrive.Exit(me, force: false);
			array3 = new float[5] { 0.4f, 1f, 1.6f, 2.4f, 3.2f };
			for (out3 = 0; out3 < array3.Length; out3++)
			{
				t6 = array3[out3];
				float t17 = Time.time;
				while (Time.time - t17 < ((t6 == 0.4f) ? 0.4f : (0.6f + ((t6 > 2f) ? 0.2f : 0f))))
				{
					yield return null;
				}
				Look(side3, car.transform.position + Vector3.up * 0.9f);
				yield return Shot($"sair_{t6:0.0}".Replace(',', '_'));
				Log(string.Format("sair t={0:0.0}s: pedido {1} · no carro {2} · fase {3} · clipe {4} · porta {5}", t6, ex2, FBVehicles.CarOf(me) != null, me.GetComponent<FBDriverPose>()?.trans ?? "-", me.sys ?? "-", FBDoors.Mask(car)));
			}
			Vector3 vector2 = car.transform.InverseTransformPoint(me.transform.position);
			Log($"depois de sair: x {vector2.x:+0.00;-0.00} (− = lado do motorista) z {vector2.z:+0.00;-0.00} · de pé no chão {me.GetComponent<CharacterController>()?.isGrounded}");
			cam.enabled = false;
		}
		Vector3 p0 = me.Pos + Vector3.up * 300f;
		BoxCollider boxCollider = new GameObject("FB_CarroPad").AddComponent<BoxCollider>();
		boxCollider.gameObject.layer = 8;
		boxCollider.size = new Vector3(3000f, 2f, 3000f);
		boxCollider.transform.position = p0 - Vector3.up;
		GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
		wall.name = "FB_Muro";
		wall.layer = 8;
		wall.transform.localScale = new Vector3(14f, 3f, 1f);
		Physics.SyncTransforms();
		string text = new string[5] { "GolG5", "Up", "GolG2", "CivicG10", "Corolla2018" }.FirstOrDefault((string v) => (bool)FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v));
		FBCar dc = ((text != null) ? FBVehicles.Spawn(text, p0 + Vector3.up * 0.3f, 0f) : null);
		yield return Wait(1f);
		string nome2;
		if ((bool)dc)
		{
			if ((bool)FBVehicles.CarOf(me))
			{
				FBDrive.Exit(me, force: true);
				yield return Wait(0.5f);
			}
			FBMatch.Teleport(me, dc.transform.position - dc.transform.right * 2f, 0f);
			yield return Wait(0.5f);
			FBDrive.CarAnims = false;
			bool ex2 = FBDrive.Enter(me, dc);
			yield return Wait(0.5f);
			Log($"pista de dano: {dc.id} · entrou {ex2} · dirigindo {FBDrive.Cur == dc}");
			FBPlayer.TestInput = delegate(FBInputState inp)
			{
				inp.move = new Vector2(0f - cSt, cThr);
				inp.jumpHeld = false;
				inp.interact = false;
				inp.multimidia = false;
				return inp;
			};
			(string, float, float)[] array4 = new(string, float, float)[3]
			{
				("leve", 20f, 0f),
				("moderada", 45f, -1.2f),
				("grave", 75f, -1.2f)
			};
			for (out3 = 0; out3 < array4.Length; out3++)
			{
				float t17;
				(nome2, t6, t17) = array4[out3];
				if (nome2 == "grave")
				{
					yield return Reta(dc, "depois da leve + moderada");
					FBCarDamage.Repair(dc);
					Log("oficina antes da grave (carro novo para medir a grave sozinha)");
				}
				wall.SetActive(value: true);
				Transform transform3 = dc.transform;
				wall.transform.SetPositionAndRotation(transform3.position + transform3.forward * 90f + transform3.right * t17 + Vector3.up * 1.5f, Quaternion.LookRotation(transform3.forward) * Quaternion.Euler(0f, (t17 != 0f) ? (-25) : 0, 0f));
				Physics.SyncTransforms();
				cThr = 1f;
				cSt = 0f;
				float vMax = 0f;
				float tt = 0f;
				while (tt < 15f && dc.v * 3f < t6)
				{
					tt += Time.deltaTime;
					yield return null;
				}
				float hitV4 = dc.v * 3f;
				bool ok = false;
				for (float w = 0f; w < 25f; w += Time.deltaTime)
				{
					if (ok)
					{
						break;
					}
					cThr = ((dc.v * 3f < t6) ? 0.7f : 0f);
					if (dc.crashT > 0f)
					{
						ok = true;
					}
					hitV4 = Mathf.Max(hitV4, dc.v * 3f);
					vMax = Mathf.Max(vMax, dc.v * 3f);
					yield return null;
				}
				cThr = 0f;
				yield return Wait(1.2f);
				cam.enabled = true;
				Look(dc.transform.position + dc.transform.forward * 4.5f - dc.transform.right * 3f + Vector3.up * 1.8f, dc.transform.position + Vector3.up * 0.6f);
				yield return Shot("dano_" + nome2);
				cam.enabled = false;
				Log($"batida {nome2}: a {hitV4:0} km/h · bateu {ok} · {FBCarDamage.DebugInfo(dc)} · peças do REB no modelo: capô {FBDoors.HasPart(dc, 32)} mala {FBDoors.HasPart(dc, 16)} portas {FBDoors.HasPart(dc, 15)} · soltas no total {FBDoors.Detached} · {FBDoors.DebugInfo(dc)}");
				if (FBDoors.GoneCount(dc) > 0)
				{
					List<Transform> list3 = (from t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
						where t.name.StartsWith("peca_solta_")
						select t).ToList();
					foreach (Transform item4 in list3)
					{
						Rigidbody component = item4.GetComponent<Rigidbody>();
						Log(string.Format("peça solta {0}: a {1:0.0} u do carro · {2} · altura {3:0.00} u", item4.name, (item4.position - dc.transform.position).magnitude, component ? (component.isKinematic ? "parada" : (component.linearVelocity.magnitude.ToString("0.0") + " u/s")) : "-", item4.position.y - p0.y));
					}
					if (list3.Count > 0)
					{
						cam.enabled = true;
						Look(list3[0].position + Vector3.up * 2.5f + dc.transform.right * 3f, list3[0].position);
						yield return Shot("dano_" + nome2 + "_peca_solta");
						cam.enabled = false;
					}
				}
				cThr = -1f;
				yield return Wait(2.2f);
				cThr = 0f;
				yield return Wait(1f);
				Transform transform4 = dc.transform;
				dc.v = 0f;
				transform4.position = new Vector3(transform4.position.x, p0.y, transform4.position.z) - transform4.forward * 20f;
				yield return Wait(0.5f);
			}
			FBCarDamage.Repair(dc);
			yield return Wait(0.3f);
			wall.SetActive(value: false);
			GameObject lib = Resources.Load<GameObject>("X171/Urbano/Placas");
			Transform transform5 = (lib ? lib.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault((Transform t) => t.parent == lib.transform && t.name.StartsWith("PlacasRegulamentacao_Pare")) : null);
			if ((bool)transform5)
			{
				Transform h = dc.transform;
				GameObject pl = UnityEngine.Object.Instantiate(transform5.gameObject);
				pl.name = "x171_PlacasRegulamentacao_Pare";
				pl.transform.position = h.position + h.forward * 40f + h.right * -0.4f;
				pl.layer = 8;
				Renderer[] componentsInChildren = pl.GetComponentsInChildren<Renderer>();
				Bounds bounds = componentsInChildren[0].bounds;
				Renderer[] array5 = componentsInChildren;
				foreach (Renderer renderer in array5)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				BoxCollider boxCollider2 = pl.AddComponent<BoxCollider>();
				boxCollider2.center = pl.transform.InverseTransformPoint(bounds.center);
				boxCollider2.size = new Vector3(Mathf.Max(0.15f, bounds.size.x), bounds.size.y, Mathf.Max(0.15f, bounds.size.z));
				Physics.SyncTransforms();
				out3 = FBFisica.Knocked;
				cThr = 1f;
				for (float tt = 0f; tt < 10f; tt += Time.deltaTime)
				{
					if (!(dc.v * 3f < 35f))
					{
						break;
					}
					yield return null;
				}
				float t17 = dc.v * 3f;
				cThr = 0.3f;
				for (float tt = 0f; tt < 4f; tt += Time.deltaTime)
				{
					if (FBFisica.Knocked != out3)
					{
						break;
					}
					yield return null;
				}
				t6 = dc.v * 3f;
				yield return Wait(1.2f);
				float hitV4 = Vector3.Angle(pl.transform.up, Vector3.up);
				cam.enabled = true;
				Look(pl.transform.position + Vector3.up * 2f + h.right * 4f, pl.transform.position);
				yield return Shot("placa_derrubada");
				cam.enabled = false;
				Log(string.Format("placa do 171 a {0:0} km/h: cedeu {1} · carro de {2:0} para {3:0} km/h · placa inclinada {4:0}° · física {5}", t17, FBFisica.Knocked > out3, t17, t6, hitV4, pl.GetComponent<Rigidbody>() ? "sim" : "não"));
				cThr = 0f;
				yield return Wait(1f);
				dc.v = 0f;
				h.position = new Vector3(h.position.x, p0.y, h.position.z) - h.forward * 30f;
				yield return Wait(0.5f);
			}
			FBCarDamage.Repair(dc);
			yield return Wait(0.3f);
			Log("oficina: " + FBCarDamage.DebugInfo(dc));
			FBPlayer.TestInput = null;
			FBDrive.Exit(me, force: true);
			FBDrive.CarAnims = true;
		}
		string text2 = FBVehicles.PickViatura(() => 0.1f);
		Vector3 vector3 = me.Pos + me.Fwd * 12f;
		float? num11 = FBWorld.GroundY(vector3.x, vector3.y + 3f, vector3.z, 0.4f, 8f);
		if (num11.HasValue)
		{
			float valueOrDefault = num11.GetValueOrDefault();
			vector3.y = valueOrDefault;
		}
		FBCar mine = ((text2 != null) ? FBVehicles.Spawn(text2, vector3 + Vector3.up * 0.3f, 0f) : null);
		FBCar rc = ((text2 != null) ? FBVehicles.Spawn(text2, vector3 + Vector3.right * 6f + Vector3.up * 0.3f, 0f) : null);
		string text3 = FBStreetEvents.FreeCivilian(allowRepeat: true);
		FBCharacter ra = ((text3 != null) ? FBStreetEvents.Spawn(text3, vector3 + Vector3.right * 9f, 0f, "netlab") : null);
		if ((bool)mine && (bool)rc && (bool)ra)
		{
			mine.syncId = "netlab:a";
			rc.syncId = "netlab:b";
			yield return Wait(1f);
			FBMatch.Teleport(me, mine.transform.position - mine.transform.right * 2f, 0f);
			yield return Wait(0.4f);
			FBDrive.CarAnims = false;
			FBDrive.Enter(me, mine);
			yield return Wait(0.6f);
			FBWeather.ForceNight(on: true);
			yield return Wait(1.5f);
			yield return Hold("antes (tudo desligado)", 1f);
			FBSiren.Set(mine, 2);
			yield return Hold("sirene rápida", 1.5f);
			cam.enabled = true;
			Transform transform6 = rc.transform;
			Look(transform6.position + transform6.forward * 8f + transform6.right * 3f + Vector3.up * 2.2f, transform6.position + Vector3.up * 1f, 55f);
			yield return Shot("rede_sirene_giroflex");
			cam.enabled = false;
			in0 = 0;
			van = 0;
			AudioSource src = rc.GetComponentsInChildren<AudioSource>().FirstOrDefault((AudioSource a) => a.isPlaying && (bool)a.clip && a.clip.name.ToLower().Contains("sirene"));
			float hitV4;
			for (hitV4 = 0f; hitV4 < 3f; hitV4 += Time.unscaledDeltaTime)
			{
				Step("x");
				van++;
				if (FBSiren.Lights(rc))
				{
					in0++;
				}
				yield return null;
			}
			Log(string.Format("rede · 3 s com a sirene: remoto ligado {0}/{1} quadros · fonte 3D no remoto {2} · {3}", in0, van, src ? (src.clip.name + " espacial " + src.spatialBlend) : "-", FBSiren.Diag(rc)));
			FBSiren.Set(mine, 3);
			yield return Hold("troca para wail", 1f);
			FBSiren.Set(mine, 0);
			FBSiren.SetLights(mine, on: true);
			yield return Hold("só giroflex (SHIFT+B)", 1f);
			FBSiren.SetLights(mine, on: false);
			hitV4 = Time.unscaledTime;
			t6 = -1f;
			for (float t17 = 0f; t17 < 2f; t17 += Time.unscaledDeltaTime)
			{
				Step("x");
				if (t6 < 0f && FBSiren.Mode(rc) == 0 && !FBSiren.Lights(rc))
				{
					t6 = Time.unscaledTime - hitV4;
				}
				yield return null;
			}
			Log($"rede · desligou no local -> remoto desligou em {t6:0.00} s");
			FBCarLights.TestBlink(2);
			van = 0;
			in0 = 0;
			int sig0 = 0;
			for (t6 = 0f; t6 < 3f; t6 += Time.unscaledDeltaTime)
			{
				Step("x");
				sig0++;
				if (FBCarLights.LensOn(rc, 3, 0) || FBCarLights.LensOn(rc, 3, 1))
				{
					van++;
				}
				if (FBCarLights.LensOn(rc, 4, 0) || FBCarLights.LensOn(rc, 4, 1))
				{
					in0++;
				}
				yield return null;
			}
			Log($"rede · pisca-alerta: lente seta esq acesa {van}/{sig0} quadros, dir {in0}/{sig0} (piscando ~50%) · bits {FBDrive.LightBits(mine)}");
			cam.enabled = true;
			Transform transform7 = rc.transform;
			Look(transform7.position - transform7.forward * 8f + transform7.right * 2.5f + Vector3.up * 2f, transform7.position + Vector3.up * 0.8f, 55f);
			for (sig0 = 0; sig0 < 2; sig0++)
			{
				Step("x");
				yield return Shot($"rede_pisca_alerta_{sig0}");
				yield return Wait(0.36f);
			}
			cam.enabled = false;
			FBCarLights.TestBlink(-1);
			sig0 = 0;
			in0 = 0;
			van = 0;
			for (t6 = 0f; t6 < 3f; t6 += Time.unscaledDeltaTime)
			{
				Step("x");
				van++;
				if (FBCarLights.LensOn(rc, 3, 0) || FBCarLights.LensOn(rc, 3, 1))
				{
					sig0++;
				}
				if (FBCarLights.LensOn(rc, 4, 0) || FBCarLights.LensOn(rc, 4, 1))
				{
					in0++;
				}
				yield return null;
			}
			Log($"rede · seta esquerda: esq {sig0}/{van}, dir {in0}/{van}");
			FBCarLights.TestBlink(0);
			out3 = FBCarLights.headMode;
			FBCarLights.headMode = 1;
			van = 0;
			in0 = 0;
			for (t6 = 0f; t6 < 1.5f; t6 += Time.unscaledDeltaTime)
			{
				Step("x");
				in0++;
				if (FBCarLights.LensOn(rc, 0, 0) || FBCarLights.LensOn(rc, 0, 1))
				{
					van++;
				}
				yield return null;
			}
			Log($"rede · faróis ligados: farol do remoto aceso {van}/{in0}");
			FBCarLights.headMode = 2;
			in0 = 0;
			van = 0;
			for (t6 = 0f; t6 < 1.5f; t6 += Time.unscaledDeltaTime)
			{
				Step("x");
				van++;
				if (FBCarLights.LensOn(rc, 0, 0) || FBCarLights.LensOn(rc, 0, 1))
				{
					in0++;
				}
				yield return null;
			}
			Log($"rede · faróis desligados (noite): farol do remoto aceso {in0}/{van}");
			FBCarLights.headMode = out3;
			FBWeather.ForceNight(on: false);
			FBDrive.Release(ra);
			FBDrive.Exit(me, force: true);
			FBDrive.CarAnims = true;
			Log($"rede · motorista remoto saiu: netLights {rc.netLights}");
		}
		else
		{
			Log("rede: não montou (viatura " + text2 + " · remoto " + text3 + ")");
		}
		nome2 = (from c in FBVehicles.list
			where (bool)c && c.kind != "moto" && !c.ie && !c.mx
			select c.id).FirstOrDefault((string v) => FBRegistry.I.Vehicle(v));
		string motoId = GameData.Vehicles.Keys.FirstOrDefault((string v) => (bool)FBRegistry.I.Vehicle(v) && Json.S(GameData.Vehicles[v], "kind") == "moto" && !v.StartsWith("mx_"));
		string mxId = (from i in FBMxBike.IDS.Where(FBMxBike.Has)
			select "mx_" + i).FirstOrDefault();
		if ((bool)FBVehicles.CarOf(me))
		{
			FBDrive.Exit(me, force: true);
		}
		FBMatch.Teleport(me, p0 + Vector3.right * 60f, 0f);
		yield return Wait(0.5f);
		Log(string.Format("7: jogador no centro da pista · chão {0} (pista {1:0.00})", FBWorld.GroundY(me.Pos.x, me.Pos.y + 1f, me.Pos.z, 0.5f, 3f)?.ToString("0.00") ?? "NENHUM", p0.y));
		(string, string)[] array6 = new(string, string)[3]
		{
			(nome2, "carro"),
			(motoId, "moto"),
			(mxId, "mx")
		};
		for (out3 = 0; out3 < array6.Length; out3++)
		{
			(string, string) tuple2 = array6[out3];
			string vid2 = tuple2.Item1;
			string tag2 = tuple2.Item2;
			if (vid2 == null)
			{
				Log("7 " + tag2 + ": sem veículo");
				continue;
			}
			FBCar vc5 = Put(vid2, me.Pos + me.Fwd * 9f, me.Yaw);
			if (!vc5)
			{
				Log("7 " + tag2 + ": " + vid2 + " não nasceu");
				continue;
			}
			vc5.syncId = "lab7:" + tag2;
			yield return Wait(1.5f);
			FBCharacter a3 = FBStreetEvents.Spawn(FBStreetEvents.FreeCivilian(allowRepeat: true), FBDrive.DoorOut(vc5, 1f), vc5.transform.eulerAngles.y + 90f, "lab7");
			if (!a3)
			{
				Kill(vc5);
				continue;
			}
			yield return Wait(1.2f);
			FBDrive.ApplyRemote(a3, State(vc5), 0f, npc: true);
			bool ex2 = vc5.kind == "moto";
			bool flag2;
			if (ex2)
			{
				FBDrive.StartMount(a3, vc5);
				flag2 = a3.GetComponent<FBDriverPose>()?.trans == "mount";
			}
			else
			{
				flag2 = FBDrive.StartCarIn(a3, vc5);
			}
			FBDriverPose dp4 = a3.GetComponent<FBDriverPose>();
			Log($"7 {tag2} ({vid2}) NPC sobe/entra: {flag2} · porta {FBDrive.DoorDiag} · caminho {dp4?.path} · {FBDrive.PathDiag}");
			if (flag2)
			{
				yield return Shots(vc5, "npc_" + tag2 + "_entra", dp4.transLen, a3);
			}
			yield return Wait(1.2f);
			Log("  " + tag2 + " sentado: " + Hip(a3, vc5));
			bool outDone = false;
			bool flag3 = (ex2 ? FBDrive.StartDismount(a3, vc5) : FBDrive.StartCarOut(a3, vc5, needFree: false));
			if ((bool)dp4)
			{
				dp4.onOut = delegate(FBCharacter ch, FBCar cr)
				{
					FBDrive.OutPoint(ch, cr, out var at2, out var yaw2);
					FBDrive.Release(ch);
					FBMatch.Teleport(ch, at2, yaw2);
					CharacterController component3 = ch.GetComponent<CharacterController>();
					if ((bool)component3)
					{
						component3.enabled = true;
					}
					outDone = true;
				};
			}
			Log($"7 {tag2} NPC desce/sai: {flag3} · porta {FBDrive.DoorDiag} · {FBDrive.PathDiag}");
			if (flag3)
			{
				yield return Shots(vc5, "npc_" + tag2 + "_sai", dp4.transLen, a3);
			}
			yield return Wait(0.8f);
			Vector3 vector4 = vc5.transform.InverseTransformPoint(a3.transform.position);
			Log($"  {tag2} depois de sair: terminou {outDone} · x {vector4.x:+0.00;-0.00} z {vector4.z:+0.00;-0.00} · no veículo {FBVehicles.CarOf(a3) != null}");
			cam.enabled = true;
			Transform transform8 = vc5.transform;
			Look(transform8.position - transform8.right * 4.2f + transform8.forward * 0.8f + Vector3.up * 1.4f, transform8.position + Vector3.up * 0.8f);
			yield return Shot("npc_" + tag2 + "_fora");
			FBStreetEvents.Despawn(a3);
			Kill(vc5);
			yield return Wait(0.5f);
		}
		if (nome2 != null)
		{
			FBCar vc5 = Put(nome2, me.Pos + me.Fwd * 9f, me.Yaw);
			yield return Wait(1f);
			float[] array3 = new float[2] { 35f, 12f };
			for (out3 = 0; out3 < array3.Length; out3++)
			{
				t6 = array3[out3];
				FBCarDamage.SetHpTest(vc5, t6);
				yield return Wait(2.5f);
				Transform transform9 = vc5.transform;
				cam.enabled = true;
				Look(transform9.position + transform9.forward * 5f - transform9.right * 3.5f + Vector3.up * 2f, transform9.position + transform9.forward * 0.8f + Vector3.up * 1.2f);
				yield return Shot($"fumaca_motor_{t6:0}");
				Log($"fumaça do motor: vida {FBCarDamage.Hp(vc5):0}");
			}
			Kill(vc5);
			yield return Wait(0.5f);
		}
		if (nome2 != null)
		{
			FBCar vc5 = Put(nome2, me.Pos + me.Fwd * 9f, me.Yaw);
			yield return Wait(1f);
			if ((bool)vc5 && FBDrive.Enter(me, vc5))
			{
				FBDriverPose dp4 = me.GetComponent<FBDriverPose>();
				for (float hitV4 = 0f; hitV4 < 4f; hitV4 += Time.deltaTime)
				{
					if (!dp4)
					{
						break;
					}
					if (dp4.trans == null)
					{
						break;
					}
					yield return null;
				}
				yield return Wait(0.6f);
				bool hbk = true;
				cThr = 0f;
				cSt = 0f;
				FBPlayer.TestInput = delegate(FBInputState inp)
				{
					inp.move = new Vector2(0f - cSt, cThr);
					inp.jumpHeld = hbk;
					inp.interact = false;
					inp.multimidia = false;
					return inp;
				};
				cThr = 1f;
				t6 = 0f;
				for (float hitV4 = 0f; hitV4 < 2.5f; hitV4 += Time.deltaTime)
				{
					if (vc5.burn)
					{
						t6 += Time.deltaTime;
					}
					yield return null;
				}
				Transform transform10 = vc5.transform;
				cam.enabled = true;
				Look(transform10.position - transform10.forward * 7.5f + transform10.right * 3.2f + Vector3.up * 1.6f, transform10.position - transform10.forward * 1.2f + Vector3.up * 0.7f, 55f);
				yield return Shot("fumaca_pneu_queimada");
				Log($"nuvem dos pneus · queimada parado: queimando {t6:0.0} de 2,5 s · {vc5.v * 3f:0} km/h");
				cThr = 0f;
				hbk = false;
				yield return Wait(1.5f);
				FBPlayer.TestInput = null;
			}
			else
			{
				Log("nuvem dos pneus: não entrou no carro");
			}
			if ((bool)FBVehicles.CarOf(me))
			{
				FBDrive.Exit(me, force: true);
			}
			Kill(vc5);
			yield return Wait(0.5f);
		}
		if (mxId != null)
		{
			FBCar vc5 = Put(mxId, me.Pos + me.Fwd * 6f, me.Yaw);
			yield return Wait(1.5f);
			if ((bool)vc5 && FBDrive.Enter(me, vc5))
			{
				FBDriverPose dp4 = me.GetComponent<FBDriverPose>();
				for (t6 = 0f; t6 < 4f; t6 += Time.deltaTime)
				{
					if (!dp4)
					{
						break;
					}
					if (dp4.trans == null)
					{
						break;
					}
					yield return null;
				}
				yield return Wait(0.5f);
				cThr = 0f;
				cSt = 0f;
				FBPlayer.TestInput = delegate(FBInputState inp)
				{
					inp.move = new Vector2(0f - cSt, cThr);
					inp.jumpHeld = false;
					inp.interact = false;
					inp.multimidia = false;
					return inp;
				};
				cThr = 1f;
				yield return Wait(0.9f);
				Transform transform11 = (vc5.mx ? vc5.mx.transform : vc5.transform);
				cam.enabled = true;
				Look(transform11.position - transform11.forward * 6.5f + transform11.right * 3f + Vector3.up * 1.5f, transform11.position - transform11.forward * 1f + Vector3.up * 0.6f, 55f);
				yield return Shot("fumaca_moto_arrancada");
				Log($"nuvem dos pneus · arrancada da MX: {(vc5.mx ? vc5.mx.speedKmh : 0f):0} km/h depois de 0,9 s");
				cThr = -1f;
				yield return Wait(1.8f);
				cThr = 0f;
				FBPlayer.TestInput = null;
			}
			else
			{
				Log("nuvem dos pneus: não montou na MX");
			}
			if ((bool)FBVehicles.CarOf(me))
			{
				FBDrive.Exit(me, force: true);
			}
			Kill(vc5);
			yield return Wait(0.5f);
		}
		if (mxId != null)
		{
			FBCar vc5 = Put(mxId, me.Pos + me.Fwd * 6f, me.Yaw);
			yield return Wait(1.5f);
			if ((bool)vc5 && FBDrive.Enter(me, vc5))
			{
				FBDriverPose component2 = me.GetComponent<FBDriverPose>();
				Log($"7 queda: montou {vc5.id} · caminho {component2?.path} · {FBDrive.PathDiag}");
				yield return Shots(vc5, "jog_mx_monta", component2 ? component2.transLen : 1f, me);
				yield return Wait(1.5f);
				vc5.mx.Launch(vc5.mx.transform.forward * 40f / 3.6f * 1.2f);
				yield return Wait(0.35f);
				Vector3 side3 = me.Pos;
				FBDrive.MxCrash(vc5, "batida");
				Log($"7 queda do jogador: {FBFall.Diag} · caindo {FBFall.Busy(me)}");
				float[] array3 = new float[6] { 0.25f, 0.7f, 1.3f, 2.1f, 3f, 4.2f };
				for (out3 = 0; out3 < array3.Length; out3++)
				{
					t6 = array3[out3];
					float hitV4 = Time.time;
					while (Time.time - hitV4 < ((t6 == 0.25f) ? 0.25f : 0.45f))
					{
						yield return null;
					}
					cam.enabled = true;
					Vector3 normalized = me.transform.forward.Flat().normalized;
					Vector3 vector5 = Vector3.Cross(Vector3.up, normalized);
					Look(me.Pos - normalized * 3.5f + vector5 * 3.2f + Vector3.up * 1.8f, me.Pos + Vector3.up * 0.6f, 55f);
					yield return Shot($"jog_queda_{t6:0.0}".Replace(',', '_'));
					Log(string.Format("  queda t={0:0.0}s: clipe {1} · caindo {2} · andou {3:0.0} m · {4}", t6, me.sys ?? "-", FBFall.Busy(me), (me.Pos - side3).Flat().magnitude, Hip(me, vc5)));
				}
				yield return Wait(1f);
				Log($"  queda fim: caindo {FBFall.Busy(me)} · no chão {me.GetComponent<CharacterController>()?.isGrounded} · quedas {FBFall.Started}/{FBFall.Finished}");
			}
			else
			{
				Log("7 queda: não montou na MX");
			}
			if ((bool)FBVehicles.CarOf(me))
			{
				FBDrive.Exit(me, force: true);
			}
			Kill(vc5);
			yield return Wait(0.5f);
			string[] array7 = new string[3] { "Left", "Right", "Back" };
			foreach (string tag2 in array7)
			{
				FBCharacter a3 = FBStreetEvents.Spawn(FBStreetEvents.FreeCivilian(allowRepeat: true), me.Pos + me.Fwd * 6f, me.Yaw, "lab7f");
				if ((bool)a3)
				{
					yield return Wait(1f);
					Vector3 side3 = a3.Pos;
					bool flag4 = FBFall.Begin(a3, a3.Pos, me.Yaw, a3.transform.forward * 7f, tag2);
					Log($"7 queda NPC {tag2}: {flag4} · {FBFall.Diag}");
					for (van = 0; van < 3; van++)
					{
						yield return Wait(0.9f);
						cam.enabled = true;
						Vector3 normalized2 = a3.transform.forward.Flat().normalized;
						Vector3 vector6 = Vector3.Cross(Vector3.up, normalized2);
						Look(a3.Pos - normalized2 * 4f + vector6 * 3f + Vector3.up * 1.8f, a3.Pos + Vector3.up * 0.6f, 55f);
						yield return Shot($"npc_queda_{tag2}_{van}");
						Log(string.Format("  NPC {0} {1}: clipe {2} · andou {3:0.0} m", tag2, van, a3.sys ?? "-", (a3.Pos - side3).Flat().magnitude));
					}
					yield return Wait(1.5f);
					Log($"  NPC {tag2} fim: caindo {FBFall.Busy(a3)}");
					FBStreetEvents.Despawn(a3);
				}
			}
		}
		cam.enabled = false;
		(FBSession.map, FBSession.mode, FBSession.view) = keep;
		FBSession.Save();
		Log("fim");
		yield return Wait(1f);
		Application.Quit();
		string H(string n, float real)
		{
			Transform transform13 = (FBVestirRua.Root ? FBVestirRua.Root.GetComponentsInChildren<Transform>().FirstOrDefault((Transform t) => t.parent == FBVestirRua.Root.transform && t.name.StartsWith("x171_" + n)) : null);
			if (!transform13)
			{
				return n + " -";
			}
			Renderer[] componentsInChildren2 = transform13.GetComponentsInChildren<Renderer>();
			Bounds bounds2 = componentsInChildren2[0].bounds;
			Renderer[] array9 = componentsInChildren2;
			foreach (Renderer renderer2 in array9)
			{
				bounds2.Encapsulate(renderer2.bounds);
			}
			return $"{n} {bounds2.size.y:0.00} u ({bounds2.size.y / j:0.00} m pela escala do corpo; real ~{real:0.00} m)";
		}
		static string Hip(FBCharacter a, FBCar c)
		{
			Transform transform12 = (a.an ? a.an.GetBoneTransform(HumanBodyBones.Hips) : null);
			if (!transform12)
			{
				return "-";
			}
			float? num12 = FBWorld.GroundY(transform12.position.x, transform12.position.y, transform12.position.z, 0.3f, 3f);
			object arg = (transform12.position - FBDrive.SeatHips(c)).magnitude;
			object arg2 = (transform12.position - FBDrive.DoorOut(c, a.ScaleK)).Flat().magnitude;
			float num13;
			if (num12.HasValue)
			{
				float valueOrDefault2 = num12.GetValueOrDefault();
				num13 = transform12.position.y - valueOrDefault2;
			}
			else
			{
				num13 = -1f;
			}
			return $"quadril {arg:0.00} m do banco, {arg2:0.00} m da porta, {num13:0.00} m do chão";
		}
		IEnumerator Hold(string what, float s)
		{
			string last2 = null;
			for (float w2 = 0f; w2 < s; w2 += Time.unscaledDeltaTime)
			{
				last2 = Step(what);
				yield return null;
			}
			Log("rede · " + last2);
		}
		static void Kill(FBCar c)
		{
			if ((bool)c)
			{
				if ((bool)c.mx)
				{
					c.mx.SetActive(on: false);
				}
				FBVehicles.list.Remove(c);
				UnityEngine.Object.Destroy(c.gameObject);
			}
		}
		static FBCar Put(string vid, Vector3 at, float yaw)
		{
			float? num14 = FBWorld.GroundY(at.x, at.y + 3f, at.z, 0.4f, 8f);
			if (num14.HasValue)
			{
				float valueOrDefault3 = num14.GetValueOrDefault();
				at.y = valueOrDefault3;
			}
			return FBVehicles.Spawn(vid, at + Vector3.up * 0.3f, yaw);
		}
		IEnumerator Shots(FBCar c, string n, float len, FBCharacter a)
		{
			Transform T = c.transform;
			Vector3 from = T.position - T.right * 4.2f + T.forward * 0.8f + Vector3.up * 1.4f;
			float t22 = Time.time;
			for (int i2 = 0; i2 < 5; i2++)
			{
				while (Time.time - t22 < len * ((float)i2 + 0.5f) / 5f)
				{
					yield return null;
				}
				cam.enabled = true;
				Look(from, T.position + Vector3.up * 0.8f);
				yield return Shot($"{n}_{i2}");
				Log(string.Format("  {0} {1}: fase {2} · clipe {3} · {4}", n, i2, a.GetComponent<FBDriverPose>()?.trans ?? "-", a.sys ?? "-", Hip(a, c)));
			}
		}
		static List<object> State(FBCar c)
		{
			float[] array8 = Coords.ToWeb(c.transform.position);
			return new List<object>
			{
				c.syncId,
				(double)array8[0],
				(double)array8[1],
				(double)array8[2],
				(double)c.yaw,
				0.0,
				0.0,
				0.0,
				0.0,
				0.0,
				0.0,
				0.0
			};
		}
		string Step(string what)
		{
			List<object> list4 = FBDrive.VehicleState();
			if (list4 == null)
			{
				return what + ": sem estado";
			}
			string text4 = FBNet.ToJson(list4);
			List<object> list5 = Json.Parse(text4) as List<object>;
			list5[0] = rc.syncId;
			FBDrive.ApplyRemote(ra, list5, Time.deltaTime);
			return $"{what}: bits {((list5.Count > 13) ? Json.Fv(list5[13]) : (-1f)):0} · remoto sirene {FBSiren.Mode(rc)} giroflex {FBSiren.Lights(rc)} netLights {rc.netLights} · local sirene {FBSiren.Mode(mine)} giroflex {FBSiren.Lights(mine)} · json {text4.Length} bytes";
		}
	}
}
}
