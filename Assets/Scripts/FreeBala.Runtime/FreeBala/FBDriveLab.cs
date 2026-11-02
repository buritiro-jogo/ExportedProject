using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBDriveLab : MonoBehaviour
{
	private struct S
	{
		public float t;

		public float kmh;

		public float st;

		public float thr;

		public float yr;

		public float ay;

		public float beta;

		public float pit;

		public float rol;

		public float sF;

		public float sR;

		public float x;

		public float z;

		public float yaw;

		public bool hb;
	}

	[StructLayout(3)]
	[CompilerGenerated]
	private struct _003C_003Ec__DisplayClass33_1
	{
		public string md;
	}

	private string run = "vila_jardim:classico";

	private string carId = "GolG2";

	private string which = "ambos";

	private string dir;

	private string model;

	private Camera cam;

	private FBCar car;

	private FBCharacter me;

	private Vector3 pad0;

	private float cThr;

	private float cSt;

	private bool cHb;

	private const float G = 11.76f;

	private readonly List<S> ser = new List<S>();

	private Vector3 lastP;

	private Vector3 lastV;

	private float lastYaw;

	private float t0;

	private float ayF;

	private float SpeedNow
	{
		get
		{
			if (ser.Count <= 0)
			{
				return 0f;
			}
			List<S> list = ser;
			return Mathf.Abs(list[list.Count - 1].kmh);
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] a = Environment.GetCommandLineArgs();
		int i = Array.IndexOf(a, "-fbdrivelab");
		if (i >= 0)
		{
			GameObject obj = new GameObject("FB_DriveLab");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBDriveLab fBDriveLab = obj.AddComponent<FBDriveLab>();
			fBDriveLab.run = Arg(1) ?? fBDriveLab.run;
			fBDriveLab.carId = Arg(2) ?? fBDriveLab.carId;
			fBDriveLab.which = Arg(3) ?? fBDriveLab.which;
		}
		string Arg(int k)
		{
			if (i + k >= a.Length || a[i + k].StartsWith("-"))
			{
				return null;
			}
			return a[i + k];
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][drivelab] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.deltaTime)
		{
			yield return null;
		}
	}

	private static float Kmh(float v)
	{
		return v * 3.6f / 1.2f;
	}

	private void Sample()
	{
		float num = Mathf.Max(Time.deltaTime, 0.0001f);
		Vector3 position = car.transform.position;
		Vector3 vector = (position - lastP) / num;
		vector.y = 0f;
		Vector3 rhs = FBDrive.Fwd(car.yaw);
		Vector3 rhs2 = FBDrive.Left(car.yaw);
		float num2 = Mathf.DeltaAngle(lastYaw * 57.29578f, car.yaw * 57.29578f) / num;
		float magnitude = vector.magnitude;
		float num3 = car.wheelbase / 2f;
		float beta = ((magnitude > 1f) ? (Mathf.Atan2(Vector3.Dot(vector, rhs2) - num3 * num2 * ((float)Math.PI / 180f), Vector3.Dot(vector, rhs)) * 57.29578f) : 0f);
		ayF = Mathf.Lerp(ayF, Vector3.Dot((vector - lastV) / num, rhs2) / 11.76f, 0.25f);
		lastV = vector;
		ser.Add(new S
		{
			t = Time.time - t0,
			kmh = Kmh(magnitude) * Mathf.Sign(Vector3.Dot(vector, rhs) + 0.001f),
			st = cSt,
			thr = cThr,
			hb = cHb,
			yr = num2,
			ay = ayF,
			beta = beta,
			pit = car.bPit * 57.29578f,
			rol = car.bRol * 57.29578f,
			sF = car.slipF,
			sR = car.slipR,
			x = position.x - pad0.x,
			z = position.z - pad0.z,
			yaw = car.yaw * 57.29578f
		});
		lastP = position;
		lastYaw = car.yaw;
	}

	private IEnumerator Drive(float secs, Func<float, bool> stop = null)
	{
		for (float t = 0f; t < secs; t += Time.deltaTime)
		{
			yield return null;
			Sample();
			if (stop != null && stop(t))
			{
				break;
			}
		}
	}

	private IEnumerator Reach(float kmh)
	{
		cSt = 0f;
		cHb = false;
		cThr = 1f;
		yield return Drive(25f, (float _) => SpeedNow >= kmh);
		for (float t = 0f; t < 1f; t += Time.deltaTime)
		{
			cThr = Mathf.Clamp((kmh - SpeedNow) * 0.15f, -0.3f, 1f);
			yield return null;
			Sample();
		}
	}

	private void Place()
	{
		car.transform.SetPositionAndRotation(pad0 + Vector3.up * 0.05f, FBDrive.CarRot(0f, 0f, 0f));
		car.yaw = 0f;
		car.v = 0f;
		car.vy = 0f;
		car.pitch = (car.roll = 0f);
		car.stIn = (car.steer = 0f);
		car.gb = null;
		FBCarPhys.Reset(car);
		car.crashT = 0f;
		lastP = car.transform.position;
		lastV = Vector3.zero;
		ayF = 0f;
		lastYaw = 0f;
		ser.Clear();
		t0 = Time.time;
	}

	private void Shot(string n, Vector3 camLocal, float fov = 50f)
	{
		Transform transform = car.transform;
		cam.transform.position = transform.TransformPoint(camLocal);
		cam.transform.LookAt(transform.position + Vector3.up * 0.7f);
		cam.fieldOfView = fov;
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "drive_" + model + "_" + n + ".png"));
	}

	private void Csv(string n)
	{
		StringBuilder stringBuilder = new StringBuilder("t,kmh,st,thr,hb,yr,ay,beta,pitch,roll,slipF,slipR,x,z,yaw\n");
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		foreach (S item in ser)
		{
			stringBuilder.Append(string.Format(invariantCulture, "{0:0.000},{1:0.0},{2:0.00},{3:0.00},{4},{5:0.0},{6:0.000},{7:0.0},{8:0.00},{9:0.00},{10:0.00},{11:0.00},{12:0.0},{13:0.0},{14:0.0}\n", item.t, item.kmh, item.st, item.thr, item.hb ? 1 : 0, item.yr, item.ay, item.beta, item.pit, item.rol, item.sF, item.sR, item.x, item.z, item.yaw));
		}
		File.WriteAllText(Path.Combine(dir, "drive_" + model + "_" + n + ".csv"), stringBuilder.ToString());
	}

	private IEnumerable<S> From(float t)
	{
		return ser.Where((S s) => s.t >= t);
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		yield return new WaitForSecondsRealtime(3f);
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
		yield return new WaitForSecondsRealtime(5f);
		FBMatch.God = true;
		foreach (FBCharacter item2 in FBMatch.Bots.ToList())
		{
			if ((bool)item2)
			{
				item2.gameObject.SetActive(value: false);
			}
		}
		me = FBMatch.Player;
		if (!me)
		{
			Log("sem jogador");
			yield break;
		}
		pad0 = new Vector3(me.Pos.x, me.Pos.y + 300f, me.Pos.z);
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
		gameObject.name = "FB_DrivePad";
		UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
		gameObject.layer = 8;
		gameObject.transform.position = pad0;
		gameObject.transform.localScale = new Vector3(300f, 1f, 300f);
		BoxCollider boxCollider = new GameObject("FB_DrivePadCol").AddComponent<BoxCollider>();
		boxCollider.gameObject.layer = 8;
		boxCollider.size = new Vector3(3000f, 2f, 3000f);
		boxCollider.transform.position = pad0 - Vector3.up;
		Texture2D texture2D = new Texture2D(64, 64)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Repeat
		};
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				bool flag = (j / 32 + i / 32) % 2 == 0;
				bool flag2 = j % 32 == 0 || i % 32 == 0;
				texture2D.SetPixel(j, i, flag2 ? new Color(0.85f, 0.85f, 0.8f) : (flag ? new Color(0.33f, 0.34f, 0.35f) : new Color(0.27f, 0.28f, 0.29f)));
			}
		}
		texture2D.Apply();
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		Material material = new Material(shader ? shader : gameObject.GetComponent<Renderer>().sharedMaterial.shader);
		material.mainTexture = texture2D;
		if (material.HasProperty("_BaseMap"))
		{
			material.SetTexture("_BaseMap", texture2D);
		}
		material.mainTextureScale = new Vector2(375f, 375f);
		if (material.HasProperty("_BaseMap"))
		{
			material.SetTextureScale("_BaseMap", new Vector2(375f, 375f));
		}
		gameObject.GetComponent<Renderer>().sharedMaterial = material;
		Physics.SyncTransforms();
		car = FBVehicles.Spawn(carId, pad0 + Vector3.up * 0.3f, 0f);
		yield return null;
		if (!car)
		{
			Log("carro não nasceu: " + carId);
			yield break;
		}
		CharacterController component = me.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		me.transform.position = pad0 + Vector3.right * 2f;
		if ((bool)component)
		{
			component.enabled = true;
		}
		yield return Wait(0.3f);
		if (!FBDrive.Enter(me, car))
		{
			Log("não entrou no carro");
			yield break;
		}
		yield return Wait(2.5f);
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.move = new Vector2(0f - cSt, cThr);
			inp.jumpHeld = cHb;
			inp.interact = false;
			inp.multimidia = false;
			return inp;
		};
		cam = new GameObject("DriveCam").AddComponent<Camera>();
		cam.depth = 100f;
		cam.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
		cam.nearClipPlane = 0.05f;
		Log(string.Format("carro {0} · tração {1} · entre-eixos {2:0.00} m · máxima {3:0} km/h", car.id, car.tracao ?? "-", car.wheelbase / 1.2f, FBDrive.TopKmh(car)));
		string[] array2 = ((!(which == "ambos")) ? new string[1] { which } : new string[2] { "antigo", "novo" });
		List<string> res = new List<string>();
		string[] array3 = array2;
		_003C_003Ec__DisplayClass33_1 CS_0024_003C_003E8__locals1 = default(_003C_003Ec__DisplayClass33_1);
		for (int k = 0; k < array3.Length; k++)
		{
			CS_0024_003C_003E8__locals1.md = array3[k];
			model = CS_0024_003C_003E8__locals1.md;
			FBCarPhys.On = CS_0024_003C_003E8__locals1.md == "novo";
			Place();
			cThr = 1f;
			yield return Drive(14f, (float _) => SpeedNow >= 100f);
			float t6 = ser.FirstOrDefault((S s) => s.kmh >= 60f).t;
			float t7 = ser.FirstOrDefault((S s) => s.kmh >= 100f).t;
			Csv("arrancada");
			R("arrancada", $"0-60 {t6:0.0} s · 0-100 {t7:0.0} s · arfagem máx {ser.Min((S s) => s.pit):0.0}°");
			Place();
			yield return Reach(100f);
			Vector3 pB = car.transform.position;
			float tb = Time.time - t0;
			cThr = -1f;
			bool shotB = false;
			yield return Drive(10f, delegate(float t)
			{
				if (!shotB && t > 0.35f)
				{
					shotB = true;
					Shot("frenagem", new Vector3(4.5f, 1.2f, 1f));
				}
				return SpeedNow < 1f;
			});
			float num = (car.transform.position - pB).magnitude / 1.2f;
			Csv("frenagem");
			R("frenagem 100-0", $"{num:0.0} m em {Time.time - t0 - tb:0.0} s · nariz desce {From(tb).Max((S s) => s.pit):0.0}°");
			Place();
			yield return Reach(60f);
			float tc = Time.time - t0;
			cSt = 1f;
			bool shotC = false;
			for (float t5 = 0f; t5 < 6f; t5 += Time.deltaTime)
			{
				cThr = Mathf.Clamp((60f - SpeedNow) * 0.15f, -0.3f, 1f);
				yield return null;
				Sample();
				if (!shotC && t5 > 4f)
				{
					shotC = true;
					Shot("curva60_tras", new Vector3(0f, 1.6f, -6.5f));
				}
			}
			List<S> source = From(tc + 4f).ToList();
			float num2 = source.Average((S s) => s.ay);
			float num3 = source.Average((S s) => s.kmh);
			float num4 = Sq(num3 / 3.6f) / Mathf.Max(0.01f, num2 * 9.8f);
			Csv("curva60");
			R("curva 60 (volante todo)", $"{num3:0} km/h · lateral {num2:0.00} g · raio {num4:0} m · giro {source.Average((S s) => s.yr):0}°/s · β {source.Average((S s) => s.beta):+0.0;-0.0}° · rolagem {source.Average((S s) => s.rol):+0.0;-0.0}° · deriva T {source.Average((S s) => s.sR):0.00}");
			Place();
			yield return Reach(80f);
			float ts = Time.time - t0;
			cSt = -1f;
			cThr = 0f;
			for (float t5 = 0f; t5 < 2.5f; t5 += Time.deltaTime)
			{
				yield return null;
				Sample();
			}
			List<S> source2 = From(ts).ToList();
			float fin = source2.Where((S s) => s.t > ts + 2f).Average((S s) => 0f - s.yr);
			float num5 = source2.Max((S s) => 0f - s.yr);
			float num6 = source2.FirstOrDefault((S s) => 0f - s.yr >= 0.9f * fin).t - ts;
			Csv("degrau80");
			R("degrau de volante a 80", $"giro final {fin:0}°/s · pico {num5:0}°/s (passa {Mathf.Max(0f, num5 / Mathf.Max(1f, fin) - 1f) * 100f:0} %) · 90 % em {num6:0.00} s · lateral {source2.Where((S s) => s.t > ts + 2f).Average((S s) => 0f - s.ay):0.00} g");
			Place();
			yield return Reach(90f);
			float tl = Time.time - t0;
			(float, float)[] array4 = new(float, float)[4]
			{
				(1f, 0.45f),
				(-1f, 0.8f),
				(1f, 0.4f),
				(0f, 2.5f)
			};
			for (int l = 0; l < array4.Length; l++)
			{
				(float, float) tuple = array4[l];
				float item = tuple.Item1;
				float t5 = tuple.Item2;
				cSt = item;
				for (float t2 = 0f; t2 < t5; t2 += Time.deltaTime)
				{
					cThr = Mathf.Clamp((90f - SpeedNow) * 0.15f, -0.3f, 1f);
					yield return null;
					Sample();
				}
			}
			List<S> ls = From(tl).ToList();
			List<S> source3 = ls.Where(delegate(S s)
			{
				float t8 = s.t;
				List<S> list = ls;
				return t8 > list[list.Count - 1].t - 1f;
			}).ToList();
			Csv("trocafaixa");
			R("troca de faixa a 90", string.Format("β máx {0:0.0}° · lateral máx {1:0.00} g · balanço no fim {2:0.0}°/s · {3}", ls.Max((S s) => Mathf.Abs(s.beta)), ls.Max((S s) => Mathf.Abs(s.ay)), source3.Max((S s) => Mathf.Abs(s.yr)), ls.Any((S s) => Mathf.Abs(s.beta) > 60f) ? "RODOU" : "estável"));
			Place();
			yield return Reach(60f);
			float th = Time.time - t0;
			float yaw0 = car.yaw;
			cSt = 1f;
			cHb = true;
			cThr = 0f;
			bool shotH = false;
			yield return Drive(0.9f, delegate(float t)
			{
				if (!shotH && t > 0.75f)
				{
					shotH = true;
					Shot("freiodemao", new Vector3(-3f, 9f, -4f), 60f);
				}
				return false;
			});
			cHb = false;
			cSt = 0f;
			cThr = 0.4f;
			yield return Drive(3f);
			List<S> source4 = From(th).ToList();
			Csv("freiodemao");
			float num7 = Mathf.Abs(Mathf.DeltaAngle(yaw0 * 57.29578f, source4.FirstOrDefault((S s) => s.t >= th + 1.5f).yaw));
			R("freio de mão a 60", string.Format("β máx {0:0}° · virou {1:0}° em 1,5 s · deriva T máx {2:0.00} · {3} · {4:0} km/h no fim", source4.Max((S s) => Mathf.Abs(s.beta)), num7, source4.Max((S s) => s.sR), source4.Any((S s) => Mathf.Abs(s.beta) > 100f) ? "RODOU" : "segurou", SpeedNow));
			Place();
			yield return Reach(80f);
			float tf = Time.time - t0;
			cSt = 0.6f;
			yield return Drive(1f);
			cThr = -1f;
			yield return Drive(3f, (float _) => SpeedNow < 3f);
			List<S> source5 = From(tf).ToList();
			Csv("freadacurva");
			R("freada na curva a 80", string.Format("β máx {0:0.0}° · {1} · rolagem máx {2:0.0}° · nariz {3:0.0}°", source5.Max((S s) => Mathf.Abs(s.beta)), source5.Any((S s) => Mathf.Abs(s.beta) > 60f) ? "RODOU" : "estável", source5.Max((S s) => Mathf.Abs(s.rol)), source5.Max((S s) => s.pit)));
			cThr = 0f;
			cSt = 0f;
			cHb = false;
			CS_0024_003C_003E8__locals1 = default(_003C_003Ec__DisplayClass33_1);
		}
		File.WriteAllLines(Path.Combine(dir, "drive_resumo.txt"), res);
		FBPlayer.TestInput = null;
		FBCarPhys.On = Array.IndexOf(Environment.GetCommandLineArgs(), "-fbfisicaantiga") < 0;
		FBDrive.Exit(me, force: true);
		yield return Wait(1f);
		Log("fim");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		yield return new WaitForSecondsRealtime(1f);
		Application.Quit();
		string R(string n, string s)
		{
			string text = CS_0024_003C_003E8__locals1.md + " · " + n + ": " + s;
			res.Add(text);
			Log(text);
			return text;
		}
	}

	private static float Sq(float x)
	{
		return x * x;
	}
}
}
