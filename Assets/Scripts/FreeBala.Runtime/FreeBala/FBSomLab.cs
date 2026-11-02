using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBSomLab : MonoBehaviour
{
	private string run = "vila_jardim:classico";

	private float cThr;

	private float cSt;

	private bool cHb;

	private static readonly float[] bufL = new float[4096];

	private static readonly float[] bufR = new float[4096];

	private static readonly float[] spec = new float[4096];

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbsomlab");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_SomLab");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBSomLab fBSomLab = obj.AddComponent<FBSomLab>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				fBSomLab.run = commandLineArgs[num + 1];
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][somlab] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private IEnumerator Measure(float secs, float[] res)
	{
		double sl = 0.0;
		double sr = 0.0;
		int i = 0;
		for (float t = 0f; t < secs; t += Time.unscaledDeltaTime)
		{
			AudioListener.GetOutputData(bufL, 0);
			AudioListener.GetOutputData(bufR, 1);
			double num = 0.0;
			double num2 = 0.0;
			for (int j = 0; j < bufL.Length; j++)
			{
				num += (double)(bufL[j] * bufL[j]);
				num2 += (double)(bufR[j] * bufR[j]);
			}
			sl += num / (double)bufL.Length;
			sr += num2 / (double)bufR.Length;
			i++;
			yield return null;
		}
		res[0] = (float)(sl / (double)Mathf.Max(1, i));
		res[1] = (float)(sr / (double)Mathf.Max(1, i));
	}

	private static float Db(float p)
	{
		return 10f * Mathf.Log10(Mathf.Max(1E-12f, p));
	}

	private static string Above(float[] m, float[] bg)
	{
		float num = m[0] + m[1] - bg[0] - bg[1];
		if (!(num <= 0f))
		{
			return $"{Db(num / 2f):0.0} dB (fundo {Db((bg[0] + bg[1]) / 2f):0.0})";
		}
		return "≈ fundo";
	}

	private IEnumerator Start()
	{
		yield return Wait(3f);
		string[] array = run.Split(':');
		GameData.Load();
		FBSession.Load();
		string keepMap = FBSession.map;
		string keepMode = FBSession.mode;
		FBSession.map = array[0];
		FBSession.mode = ((array.Length > 1) ? array[1] : "classico");
		FBSession.online = false;
		FBSession.view = "tps";
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + array[0]);
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		yield return Wait(5f);
		FBMatch.God = true;
		foreach (FBCharacter item in FBMatch.Bots.ToList())
		{
			if ((bool)item)
			{
				item.gameObject.SetActive(value: false);
			}
		}
		FBCharacter me = FBMatch.Player;
		if (!me)
		{
			Log("sem jogador");
			yield break;
		}
		Vector3 p2 = new Vector3(me.Pos.x, me.Pos.y + 300f, me.Pos.z);
		BoxCollider bc = new GameObject("FB_SomPad").AddComponent<BoxCollider>();
		bc.gameObject.layer = 8;
		bc.size = new Vector3(3000f, 2f, 3000f);
		bc.transform.position = p2 - Vector3.up;
		Physics.SyncTransforms();
		AudioListener[] array2 = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
		for (int m = 0; m < array2.Length; m++)
		{
			array2[m].enabled = false;
		}
		GameObject ear = new GameObject("SomLabEar");
		ear.AddComponent<AudioListener>();
		ear.AddComponent<Camera>().depth = 100f;
		CharacterController cc = me.GetComponent<CharacterController>();
		if ((bool)cc)
		{
			cc.enabled = false;
		}
		me.transform.position = p2 + new Vector3(0f, 0f, -600f);
		if ((bool)cc)
		{
			cc.enabled = true;
		}
		List<string> played = new List<string>();
		FBAudio.Played = delegate(string n, Vector3 p, float v)
		{
			played.Add(n);
		};
		float[] bg = new float[2];
		float[] l = new float[2];
		ear.transform.SetPositionAndRotation(p2 + new Vector3(0f, 1.6f, -40f), Quaternion.identity);
		yield return Wait(1f);
		yield return Measure(1f, bg);
		Log($"fundo (pista vazia): {Db((bg[0] + bg[1]) / 2f):0.0} dB");
		string vid = new string[3] { "ViaturaPMAzul", "ViaturaBM", "ViaturaPM" }.FirstOrDefault((string v) => (bool)FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v));
		FBCar pol = ((vid != null) ? FBVehicles.Spawn(vid, p2 + Vector3.up * 0.3f, 0f) : null);
		yield return Wait(1f);
		if (!pol)
		{
			Log("viatura não nasceu");
		}
		else
		{
			Log($"viatura {pol.id} · polícia {FBSiren.IsPolice(pol)} · buzina {FBCarSound.HornFor(pol)}");
			int[] array3 = new int[3] { 1, 2, 3 };
			foreach (int mode in array3)
			{
				FBSiren.Set(pol, mode);
				yield return Wait(0.5f);
				ear.transform.SetPositionAndRotation(p2 + new Vector3(-10f, 1.6f, 0f), Quaternion.identity);
				yield return Measure(2f, l);
				Log($"sirene modo {mode} ({FBSiren.MODES[mode]}) a 10 m: {Above(l, bg)} · {FBSiren.Diag(pol)}");
			}
			FBSiren.Set(pol, 1);
			yield return Wait(0.5f);
			float near3 = 0f;
			float[] array4 = new float[8] { 3f, 10f, 30f, 60f, 120f, 200f, 300f, 400f };
			for (int num = 0; num < array4.Length; num++)
			{
				float d2 = array4[num];
				ear.transform.SetPositionAndRotation(p2 + new Vector3(0f - d2, 1.6f, 0f), Quaternion.identity);
				yield return Wait(0.4f);
				yield return Measure(1.6f, l);
				float num2 = (l[0] + l[1] - bg[0] - bg[1]) / 2f;
				if (d2 == 3f)
				{
					near3 = num2;
				}
				Log($"sirene a {d2:0} m: {Above(l, bg)} · relativo a 3 m {((num2 > 0f && near3 > 0f) ? Db(num2 / near3) : (-99f)):0.0} dB · E/D {Db(l[0] / Mathf.Max(1E-12f, l[1])):+0.0;-0.0} dB (viatura à direita: E < D) · {FBSiren.Diag(pol)}");
			}
			ear.transform.SetPositionAndRotation(p2 + new Vector3(-40f, 1.6f, 0f), Quaternion.identity);
			yield return Wait(0.5f);
			yield return Measure(1.5f, l);
			float[] free = (float[])l.Clone();
			BoxCollider wall = new GameObject("FB_SomParede").AddComponent<BoxCollider>();
			wall.gameObject.layer = 8;
			wall.size = new Vector3(2f, 14f, 30f);
			wall.transform.position = p2 + new Vector3(-20f, 6f, 0f);
			Physics.SyncTransforms();
			yield return Wait(1.2f);
			yield return Measure(1.5f, l);
			Log("oclusão a 40 m: livre " + Above(free, bg) + " · com prédio no meio " + Above(l, bg) + " · " + FBSiren.Diag(pol));
			UnityEngine.Object.Destroy(wall.gameObject);
			yield return Wait(0.3f);
			ear.transform.SetPositionAndRotation(p2 + new Vector3(-10f, 1.6f, 0f), Quaternion.identity);
			yield return Wait(0.5f);
			FBSiren.Set(pol, 0);
			yield return Wait(0.2f);
			yield return Measure(0.6f, l);
			Log(string.Format("sirene desligada (0,2 s depois): {0} · {1} · fontes 'sirene' na cena {2}", Above(l, bg), FBSiren.Diag(pol), UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count((AudioSource s) => (bool)s && s.name == "sirene")));
			FBSiren.SetLights(pol, on: false);
			FBSiren.Set(pol, 2);
			ear.transform.SetPositionAndRotation(p2 + new Vector3(-8f, 1.6f, 0f), Quaternion.identity);
			Vector3 rb0 = pol.transform.position;
			float[] pitchLog = new float[3];
			int j = 0;
			for (float d2 = 0f; d2 < 4f; d2 += Time.deltaTime)
			{
				pol.transform.position = rb0 + new Vector3(0f, 0f, -50f + d2 * 25f);
				AudioSource audioSource = pol.GetComponentsInChildren<AudioSource>().FirstOrDefault((AudioSource s) => s.name == "sirene");
				if (j < 3 && d2 > (float)j * 1.6f)
				{
					pitchLog[j++] = (audioSource ? audioSource.dopplerLevel : (-1f));
				}
				yield return null;
			}
			Log($"passagem a 90 km/h: Doppler da fonte {pitchLog[0]:0.00} (0 = desligado, 1 = físico)");
			FBSiren.Set(pol, 0);
			pol.transform.position = rb0;
			yield return Wait(0.5f);
		}
		List<FBCar> cars = new List<FBCar>();
		if ((bool)pol)
		{
			cars.Add(pol);
		}
		for (int num3 = 1; num3 < 5; num3++)
		{
			if (vid == null)
			{
				break;
			}
			FBCar fBCar = FBVehicles.Spawn(vid, p2 + new Vector3(num3 * 8, 0.3f, 30f), 0f);
			if ((bool)fBCar)
			{
				cars.Add(fBCar);
			}
		}
		yield return Wait(0.5f);
		foreach (FBCar item2 in cars)
		{
			FBSiren.SetLights(item2, on: true);
		}
		int keepFps = Application.targetFrameRate;
		int keepV = QualitySettings.vSyncCount;
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 20;
		yield return Wait(0.5f);
		int[] redE = new int[cars.Count];
		int[] bluE = new int[cars.Count];
		bool[] lastR = new bool[cars.Count];
		bool[] lastB = new bool[cars.Count];
		int[] lights = new int[cars.Count];
		int frames = 0;
		for (float near3 = 0f; near3 < 9f; near3 += Time.unscaledDeltaTime)
		{
			frames++;
			for (int num4 = 0; num4 < cars.Count; num4++)
			{
				FBGiroflex.Bar bar = FBGiroflex.Find(cars[num4]);
				if (bar != null)
				{
					if ((bool)bar.red)
					{
						lights[num4] = 1;
					}
					bool rNow = bar.rNow;
					bool bNow = bar.bNow;
					if (rNow && !lastR[num4])
					{
						redE[num4]++;
					}
					if (bNow && !lastB[num4])
					{
						bluE[num4]++;
					}
					lastR[num4] = rNow;
					lastB[num4] = bNow;
				}
			}
			yield return null;
		}
		Application.targetFrameRate = keepFps;
		QualitySettings.vSyncCount = keepV;
		for (int num5 = 0; num5 < cars.Count; num5++)
		{
			Log(string.Format("giroflex viatura {0}: {1} lampejos vermelhos e {2} azuis em 9 s a {3:0} qps (esperado 20 + 20 — os dois de cada cor podem fundir num só a 20 qps: mín. 10) · luz pontual própria {4} · lentes próprias {5}", num5 + 1, redE[num5], bluE[num5], (float)frames / 9f, (lights[num5] == 1) ? "sim" : "não (só as lentes)", FBGiroflex.OwnCount(cars[num5])));
		}
		foreach (FBCar item3 in cars)
		{
			FBSiren.SetLights(item3, on: false);
		}
		yield return Wait(0.3f);
		foreach (FBCar item4 in cars.Skip(1))
		{
			if ((bool)item4)
			{
				UnityEngine.Object.Destroy(item4.gameObject);
			}
		}
		ear.transform.SetPositionAndRotation(p2 + new Vector3(-5f, 1.6f, 0f), Quaternion.identity);
		yield return Wait(0.5f);
		string[] array5 = new string[18]
		{
			"reb_horn_gol_saveiro", "reb_horn_palio", "horn_sedan", "horn_sedan_grave", "horn_palio_grave", "horn_gol_agudo", "reb_horn_chevette", "reb_horn_fusca", "reb_horn_caminhonetes", "reb_horn_caminhonetesv2",
			"horn_caminhonete_grave", "horn_navio", "horn_policia1", "horn_policia2", "horn_policia3", "horn_moto", "horn_moto_agudo", "horn_moto_grave"
		};
		string[] array6 = array5;
		foreach (string h2 in array6)
		{
			AudioClip clip = FBAudio.Clip(h2);
			if (!clip)
			{
				Log("buzina " + h2 + ": SEM CLIPE");
				continue;
			}
			FBAudio.SetLoop("lab_horn", h2, 0.6f, p2 + Vector3.up);
			yield return Wait(0.35f);
			float[] pitchLog = new float[spec.Length];
			int num = 0;
			for (float near3 = 0f; near3 < 0.5f; near3 += Time.unscaledDeltaTime)
			{
				AudioListener.GetSpectrumData(spec, 0, FFTWindow.BlackmanHarris);
				for (int num6 = 0; num6 < spec.Length; num6++)
				{
					pitchLog[num6] += spec[num6];
				}
				num++;
				yield return null;
			}
			FBAudio.StopLoop("lab_horn");
			float num7 = (float)AudioSettings.outputSampleRate / 2f / (float)spec.Length;
			float num8 = 0f;
			float num9 = 0f;
			int num10 = 0;
			for (int num11 = 2; num11 < pitchLog.Length; num11++)
			{
				num8 += pitchLog[num11];
				num9 += pitchLog[num11] * (float)num11 * num7;
				if (pitchLog[num11] > pitchLog[num10])
				{
					num10 = num11;
				}
			}
			Log($"buzina {h2} (clipe {clip.name}, {clip.length:0.00} s): pico {(float)num10 * num7:0} Hz · centróide {num9 / Mathf.Max(1E-09f, num8):0} Hz");
			yield return Wait(0.25f);
		}
		if ((bool)pol)
		{
			UnityEngine.Object.Destroy(pol.gameObject);
		}
		string cid = GameData.Vehicles.Keys.FirstOrDefault((string v) => !v.Contains("Viatura") && (bool)FBRegistry.I.Vehicle(v) && Json.S(GameData.Vehicles[v] as Dictionary<string, object>, "kind") != "moto" && new string[4] { "GolG5", "Up", "GolG2", "CivicG10" }.Contains(v)) ?? GameData.Vehicles.Keys.FirstOrDefault((string v) => !v.Contains("Viatura") && (bool)FBRegistry.I.Vehicle(v));
		array6 = new string[2] { "asfalto", "terra" };
		foreach (string h2 in array6)
		{
			bc.gameObject.name = ((h2 == "terra") ? "FB_SomPad_terra" : "FB_SomPad");
			Physics.SyncTransforms();
			FBCar car = FBVehicles.Spawn(cid, p2 + new Vector3(0f, 0.3f, -300f), 0f);
			yield return Wait(1f);
			if (!car)
			{
				Log("carro não nasceu " + cid);
				break;
			}
			if ((bool)cc)
			{
				cc.enabled = false;
			}
			me.transform.position = car.transform.position - car.transform.right * 1.5f;
			if ((bool)cc)
			{
				cc.enabled = true;
			}
			yield return Wait(0.3f);
			if (!FBDrive.Enter(me, car))
			{
				Log("não entrou");
				break;
			}
			yield return Wait(1.5f);
			ear.SetActive(value: false);
			_ = Camera.main;
			array2 = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
			foreach (AudioListener audioListener in array2)
			{
				if (audioListener.gameObject != ear)
				{
					audioListener.enabled = true;
				}
			}
			FBPlayer.TestInput = delegate(FBInputState inp)
			{
				inp.move = new Vector2(0f - cSt, cThr);
				inp.jumpHeld = cHb;
				inp.interact = false;
				inp.multimidia = false;
				return inp;
			};
			cThr = 1f;
			for (float t3 = 0f; t3 < 12f; t3 += Time.deltaTime)
			{
				if (!(car.v * 3f < 70f))
				{
					break;
				}
				yield return null;
			}
			cThr = 0f;
			yield return Wait(0.3f);
			played.Clear();
			cThr = -1f;
			yield return Wait(0.15f);
			cThr = 0f;
			float near3 = 0f;
			for (float t3 = 0f; t3 < 0.8f; t3 += Time.deltaTime)
			{
				near3 = Mathf.Max(near3, FBAudio.LoopLevel("veh_skid"), FBAudio.LoopLevel("veh_skid_dirt"));
				yield return null;
			}
			Log(string.Format("{0}: freada LEVE (toque 0,15 s) a {1:0} km/h -> sons {2} · atrito máx {3:0.00}", h2, car.v * 3f, (played.Count == 0) ? "nenhum" : string.Join(",", played.Distinct()), near3));
			cThr = 1f;
			for (float t3 = 0f; t3 < 8f; t3 += Time.deltaTime)
			{
				if (!(car.v * 3f < 70f))
				{
					break;
				}
				yield return null;
			}
			float d2 = car.v * 3f;
			played.Clear();
			cThr = -1f;
			float tb = 0f;
			float skA = 0f;
			float skD = 0f;
			string skC = null;
			while (car.v > 0.3f && tb < 8f)
			{
				tb += Time.deltaTime;
				skA = Mathf.Max(skA, FBAudio.LoopLevel("veh_skid"));
				skD = Mathf.Max(skD, FBAudio.LoopLevel("veh_skid_dirt"));
				if (skC == null)
				{
					skC = ((FBAudio.LoopLevel("veh_skid") > 0f) ? FBAudio.LoopClip("veh_skid") : ((FBAudio.LoopLevel("veh_skid_dirt") > 0f) ? FBAudio.LoopClip("veh_skid_dirt") : null));
				}
				yield return null;
			}
			yield return Wait(0.6f);
			Log(string.Format("{0}: freada BRUSCA de {1:0} km/h até parar em {2:0.0} s -> sons {3} · atrito asfalto {4:0.00} · atrito terra {5:0.00} · loop {6} · loop agudo antigo reb_brakes {7:0.00}", h2, d2, tb, (played.Count == 0) ? "nenhum" : string.Join(",", played), skA, skD, skC ?? "-", FBAudio.LoopLevel("veh_brake")));
			cThr = 0f;
			FBPlayer.TestInput = null;
			FBDrive.Exit(me, force: true);
			yield return Wait(1f);
			if ((bool)car)
			{
				UnityEngine.Object.Destroy(car.gameObject);
			}
			ear.SetActive(value: true);
			array2 = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
			foreach (AudioListener audioListener2 in array2)
			{
				if (audioListener2.gameObject != ear)
				{
					audioListener2.enabled = false;
				}
			}
		}
		FBAudio.Played = null;
		Log("fim");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		yield return Wait(1f);
		Application.Quit();
	}
}
}
