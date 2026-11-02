using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBMotoLab : MonoBehaviour
{
	private string run = "vila_jardim:classico";

	private string moto = "xj6";

	private string dir;

	private Camera cam;

	private float cThr;

	private float cSt;

	private bool cG;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbmotolab");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_MotoLab");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBMotoLab fBMotoLab = obj.AddComponent<FBMotoLab>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				fBMotoLab.run = commandLineArgs[num + 1];
			}
			if (num + 2 < commandLineArgs.Length && !commandLineArgs[num + 2].StartsWith("-"))
			{
				fBMotoLab.moto = commandLineArgs[num + 2];
			}
			else if (FBMxBike.Has("xj6"))
			{
				fBMotoLab.moto = "mx_xj6";
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][motolab] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private void Shot(string n)
	{
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "moto_" + n + ".png"));
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
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
		Vector3 p0 = new Vector3(me.Pos.x, me.Pos.y + 300f, me.Pos.z);
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
		gameObject.name = "FB_MotoPad";
		UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
		gameObject.layer = 8;
		gameObject.transform.position = p0;
		gameObject.transform.localScale = new Vector3(300f, 1f, 300f);
		BoxCollider boxCollider = new GameObject("FB_MotoPadCol").AddComponent<BoxCollider>();
		boxCollider.gameObject.layer = 8;
		boxCollider.size = new Vector3(3000f, 2f, 3000f);
		boxCollider.transform.position = p0 - Vector3.up;
		Texture2D texture2D = new Texture2D(64, 64)
		{
			wrapMode = TextureWrapMode.Repeat
		};
		for (int k = 0; k < 64; k++)
		{
			for (int l = 0; l < 64; l++)
			{
				texture2D.SetPixel(l, k, (l % 32 == 0 || k % 32 == 0) ? new Color(0.85f, 0.85f, 0.8f) : (((l / 32 + k / 32) % 2 == 0) ? new Color(0.33f, 0.34f, 0.35f) : new Color(0.27f, 0.28f, 0.29f)));
			}
		}
		texture2D.Apply();
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		Material material = new Material(shader ? shader : gameObject.GetComponent<Renderer>().sharedMaterial.shader);
		material.mainTexture = texture2D;
		if (material.HasProperty("_BaseMap"))
		{
			material.SetTexture("_BaseMap", texture2D);
			material.SetTextureScale("_BaseMap", new Vector2(375f, 375f));
		}
		gameObject.GetComponent<Renderer>().sharedMaterial = material;
		Physics.SyncTransforms();
		FBCar car = FBVehicles.Spawn(moto, p0 + Vector3.up * 0.3f, 0f);
		yield return Wait(1f);
		if (!car)
		{
			Log("moto não nasceu: " + moto);
			yield break;
		}
		CharacterController component = me.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		me.transform.position = car.transform.position - car.transform.right * 1.2f;
		if ((bool)component)
		{
			component.enabled = true;
		}
		yield return Wait(0.3f);
		cam = new GameObject("MotoCam").AddComponent<Camera>();
		cam.depth = 100f;
		cam.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
		cam.nearClipPlane = 0.05f;
		if (!FBDrive.Enter(me, car))
		{
			Log("não montou");
			yield break;
		}
		FBDriverPose dp = me.GetComponent<FBDriverPose>();
		float tm = 0f;
		float[] array2 = new float[5] { 0.2f, 0.55f, 0.9f, 1.3f, 2.2f };
		foreach (float at in array2)
		{
			while (tm < at)
			{
				tm += Time.deltaTime;
				yield return null;
			}
			SideShot($"monta_{at:0.0}".Replace(',', '_').Replace('.', '_'));
			Log(string.Format("subindo t={0:0.0}s fase {1} clipe {2} quadril-banco {3:0.00} m", at, dp?.trans ?? "-", me.sys, me.an ? (me.an.GetBoneTransform(HumanBodyBones.Hips).position - car.L2W(car.seat + new Vector3(0f, 0.42f, -0.02f))).magnitude : (-1f)));
			yield return null;
		}
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.move = new Vector2(0f - cSt, cThr);
			inp.jumpHeld = cG;
			inp.interact = false;
			inp.multimidia = false;
			return inp;
		};
		Log("moto " + car.id + " · clipe de pilotar " + FBDrive.SitClip(car) + " · lean+ = " + FBDrive.MotoLeanPos + " · lean− = " + FBDrive.MotoLeanNeg);
		if ((bool)car.mx)
		{
			cThr = 1f;
			cSt = 0f;
			cG = false;
			float at = -1f;
			float tt = 0f;
			float vmax = 0f;
			for (; tt < 12f; tt += Time.deltaTime)
			{
				float speedKmh = car.mx.speedKmh;
				vmax = Mathf.Max(vmax, speedKmh);
				if (at < 0f && speedKmh >= 60f)
				{
					at = tt;
				}
				yield return null;
			}
			Log(string.Format("arrancada: 0–60 em {0} · máxima em 12 s {1:0} km/h (APK: {2} km/h)", (at < 0f) ? "não chegou" : (at.ToString("0.0") + " s"), vmax, Json.F(Json.O(car.meta, "params"), "maxspeed")) + MxInfo());
			cThr = -1f;
			float tb = 0f;
			while (car.mx.speedKmh > 2f && tb < 10f)
			{
				tb += Time.deltaTime;
				yield return null;
			}
			Log($"frenagem de {vmax:0} km/h até parar: {tb:0.0} s" + MxInfo());
			cThr = 1f;
			for (float w0 = 0f; w0 < 3f; w0 += Time.deltaTime)
			{
				yield return null;
			}
			float gT = 0f;
			float gMax = 0f;
			float gOn2 = 0f;
			cG = true;
			for (; gT < 7f; gT += Time.deltaTime)
			{
				if (car.mx.crashed)
				{
					break;
				}
				float wheelieAngle = car.mx.wheelieAngle;
				gMax = Mathf.Max(gMax, wheelieAngle);
				if (car.mx.onWheelie)
				{
					gOn2 += Time.deltaTime;
				}
				cThr = ((wheelieAngle > 50f) ? (-0.7f) : ((wheelieAngle > 38f) ? 0f : ((wheelieAngle > 26f) ? 0.45f : 1f)));
				if (Mathf.Abs(gT - 3f) < 0.02f)
				{
					cam.transform.position = car.transform.position + car.transform.right * 3.2f + Vector3.up * 1.2f;
					cam.transform.LookAt(car.transform.position + Vector3.up * 0.9f);
					Shot("mx_grau_lado");
				}
				yield return null;
			}
			cG = false;
			Log($"grau segurado 7 s: no grau {gOn2:0.0} s · ângulo máx {gMax:0}° · caiu {car.mx.crashed}" + MxInfo());
			if (car.mx.crashed)
			{
				yield return Wait(5f);
				FBDrive.Enter(me, car);
				yield return Wait(3f);
			}
			cThr = 0f;
			for (float w0 = 0f; w0 < 4f; w0 += Time.deltaTime)
			{
				if (!car.mx.onWheelie)
				{
					break;
				}
				yield return null;
			}
			yield return Wait(0.5f);
			cThr = 0.8f;
			cSt = 1f;
			for (float w0 = 0f; w0 < 4f; w0 += Time.deltaTime)
			{
				yield return null;
			}
			Log("curva fechada esquerda 4 s" + MxInfo());
			cSt = 0f;
			cThr = 0f;
			for (float w0 = 0f; w0 < 3f; w0 += Time.deltaTime)
			{
				yield return null;
			}
			bool[] array3 = new bool[2] { false, true };
			foreach (bool assist in array3)
			{
				FBMxBike.GrauAssist = assist;
				if (car.mx.crashed)
				{
					yield return Wait(4f);
					if (!FBVehicles.CarOf(me))
					{
						FBDrive.Enter(me, car);
					}
					yield return Wait(3f);
				}
				cG = false;
				cThr = 1f;
				for (float w4 = 0f; w4 < 3f; w4 += Time.deltaTime)
				{
					yield return null;
				}
				float w0 = 0f;
				float jOn3 = 0f;
				float s2 = 0f;
				float s4 = 0f;
				int j = 0;
				cG = true;
				cThr = 1f;
				for (float w4 = 0f; w4 < 6f; w4 += Time.deltaTime)
				{
					if (car.mx.crashed)
					{
						break;
					}
					float wheelieAngle2 = car.mx.wheelieAngle;
					w0 = Mathf.Max(w0, wheelieAngle2);
					if (car.mx.onWheelie)
					{
						jOn3 += Time.deltaTime;
						if (w4 > 1.5f)
						{
							s2 += wheelieAngle2;
							s4 += wheelieAngle2 * wheelieAngle2;
							j++;
						}
					}
					yield return null;
				}
				float mean2 = ((j > 0) ? (s2 / (float)j) : 0f);
				float sd2 = ((j > 0) ? Mathf.Sqrt(Mathf.Max(0f, s4 / (float)j - mean2 * mean2)) : 0f);
				Transform transform = car.transform;
				cam.transform.position = transform.position + transform.right * 3.4f + Vector3.up * 1.2f;
				cam.transform.LookAt(transform.position + Vector3.up * 0.9f);
				cam.fieldOfView = 45f;
				yield return null;
				Shot("grau_jogador_" + (assist ? "assistido" : "apk"));
				Log(string.Format("grau do jogador ({0}): no grau {1:0.0}/6 s · máx {2:0}° · média {3:0}° ± {4:0}° · caiu {5} · motos levantadas {6}", assist ? "ASSISTIDO" : "APK puro", jOn3, w0, mean2, sd2, car.mx.crashed, FBMxBike.Recovered));
				cG = false;
				cThr = 0.5f;
				for (float w4 = 0f; w4 < 3f; w4 += Time.deltaTime)
				{
					yield return null;
				}
				cThr = 0.5f;
				for (float w4 = 0f; w4 < 1.5f; w4 += Time.deltaTime)
				{
					yield return null;
				}
				Log("  meia aceleração no grau: " + (assist ? "assistido" : "apk") + " · " + MxInfo());
			}
			FBMxBike.GrauAssist = true;
			if (car.mx.crashed)
			{
				yield return Wait(4f);
				if (!FBVehicles.CarOf(me))
				{
					FBDrive.Enter(me, car);
				}
				yield return Wait(3f);
			}
			cThr = 1f;
			for (float sd2 = 0f; sd2 < 4f; sd2 += Time.deltaTime)
			{
				yield return null;
			}
			Log("acelerando: " + car.mx.SoundDiag + " · " + car.mx.WheelDiag);
			array3 = new bool[2] { false, true };
			foreach (bool assist in array3)
			{
				FBMxBike.SomPiso = assist;
				cThr = 0f;
				for (float sd2 = 0f; sd2 < 1.2f; sd2 += Time.deltaTime)
				{
					yield return null;
				}
				Log(string.Format("soltou o acelerador ({0}): {1:0} km/h · ", assist ? "com piso" : "APK", car.mx.speedKmh) + car.mx.SoundDiag + " · " + car.mx.WheelDiag);
				cThr = 1f;
				for (float sd2 = 0f; sd2 < 2.5f; sd2 += Time.deltaTime)
				{
					yield return null;
				}
			}
			FBMxBike.SomPiso = true;
			cThr = -1f;
			for (float sd2 = 0f; sd2 < 4f; sd2 += Time.deltaTime)
			{
				yield return null;
			}
			cThr = 0f;
			yield return Wait(1.5f);
			Log("parado: " + car.mx.SoundDiag);
			if (FBRoads.Ready)
			{
				Vector3 tan3;
				FBSeg road = (from S in FBRoads.segs
					where S.kind == 0 && S.len > 90f && !S.ring
					orderby (FBRoads.At(S, 10f, out tan3) - p0 + Vector3.up * 300f).sqrMagnitude
					select S).FirstOrDefault();
				array3 = new bool[2] { false, true };
				foreach (bool assist in array3)
				{
					if (road == null)
					{
						break;
					}
					FBMxBike.AntiHop = assist;
					if (car.mx.crashed)
					{
						yield return Wait(4f);
						if (!FBVehicles.CarOf(me))
						{
							FBDrive.Enter(me, car);
						}
						yield return Wait(2f);
					}
					Vector3 tan;
					Vector3 vector = FBRoads.At(road, 8f, out tan);
					float? num = FBWorld.GroundY(vector.x, vector.y + 3f, vector.z, 0.4f, 8f);
					if (num.HasValue)
					{
						float valueOrDefault = num.GetValueOrDefault();
						vector.y = valueOrDefault;
					}
					car.mx.SetActive(on: false);
					car.transform.SetPositionAndRotation(vector + Vector3.up * 0.35f, Quaternion.LookRotation(tan.Flat().normalized));
					Physics.SyncTransforms();
					car.mx.SetActive(on: true);
					car.mx.hopVy = 0f;
					yield return Wait(0.6f);
					float sd2 = 0f;
					float mean2 = 0f;
					float s4 = 0f;
					float s2 = 0f;
					int j = 0;
					bool inAir = false;
					bool offRoad = false;
					for (float jOn3 = 0f; jOn3 < 9f; jOn3 += Time.deltaTime)
					{
						if (car.mx.crashed)
						{
							break;
						}
						Transform transform2 = car.transform;
						float lat;
						float dist;
						float num2 = FBRoads.Project(road, transform2.position, out lat, out dist);
						if (num2 > road.len - 12f)
						{
							break;
						}
						Vector3 tan2;
						Vector3 vector2 = FBRoads.At(road, num2 + 14f, out tan2);
						if ((FBRoads.At(road, num2, out tan2) - transform2.position).Flat().magnitude > 4.5f)
						{
							offRoad = true;
							break;
						}
						cThr = ((car.mx.speedKmh < 50f) ? 0.7f : 0f);
						cSt = 0f - Mathf.Clamp(Vector3.SignedAngle(transform2.forward.Flat(), (vector2 - transform2.position).Flat(), Vector3.up) / 15f, -1f, 1f);
						num = FBWorld.GroundY(transform2.position.x, transform2.position.y + 0.5f, transform2.position.z, 0.25f, 3f);
						float num3;
						if (num.HasValue)
						{
							float valueOrDefault2 = num.GetValueOrDefault();
							num3 = transform2.position.y - valueOrDefault2;
						}
						else
						{
							num3 = 3f;
						}
						float num4 = num3;
						s4 = Mathf.Max(s4, num4);
						Rigidbody component2 = car.GetComponent<Rigidbody>();
						float b2 = (component2 ? (component2.linearVelocity.y / 1.2f) : 0f);
						s2 = Mathf.Max(s2, b2);
						bool flag = num4 > 0.66f;
						if (flag)
						{
							sd2 += Time.deltaTime;
						}
						if (flag && !inAir)
						{
							j++;
						}
						inAir = flag;
						mean2 += Time.deltaTime;
						yield return null;
					}
					cThr = -1f;
					cSt = 0f;
					Transform transform3 = car.transform;
					cam.transform.position = transform3.position + transform3.right * 3.2f + Vector3.up * 1.2f;
					cam.transform.LookAt(transform3.position + Vector3.up * 0.9f);
					yield return null;
					Shot("rua_" + (assist ? "antiquique" : "apk"));
					Log(string.Format("rua real ({0}) faixa {1}: {2:0.0} s · no ar {3:0.00} s · pulos {4} · subida máx {5:0.0} m/s · altura máx {6:0.00} · quique segurado {7:0.0} m/s · caiu {8} · saiu da faixa {9} · ", assist ? "COM anti-quique" : "sem", road.id, mean2, sd2, j, s2, s4, car.mx.hopVy, car.mx.crashed, offRoad) + car.mx.WheelDiag);
					for (float jOn3 = 0f; jOn3 < 2f; jOn3 += Time.deltaTime)
					{
						yield return null;
					}
				}
				FBMxBike.AntiHop = true;
				car.mx.SetActive(on: false);
				car.transform.SetPositionAndRotation(p0 + Vector3.up * 0.3f, Quaternion.identity);
				Physics.SyncTransforms();
				car.mx.SetActive(on: true);
				cThr = 0f;
				cSt = 0f;
				yield return Wait(1f);
			}
			else
			{
				Log("rua real: sem malha de ruas neste mapa/modo");
			}
		}
		yield return Phase("parado", 1.5f, 0f, 0f, grau: false);
		yield return Phase("acelerando", 2.5f, 1f, 0f, grau: false);
		yield return Phase("curva_esq", 1.4f, 0.7f, 1f, grau: false);
		yield return Phase("curva_dir", 1.8f, 0.7f, -1f, grau: false);
		yield return Phase("rapido", 5f, 1f, 0f, grau: false);
		yield return Phase("grau", 1.2f, 1f, 0f, grau: true);
		yield return Phase("freando", 2.5f, -1f, 0f, grau: false);
		yield return Phase("parado_fim", 2f, 0f, 0f, grau: false);
		FBPlayer.TestInput = null;
		yield return Wait(0.5f);
		bool flag2 = FBDrive.Exit(me, force: false);
		Log("descer: pedido " + (flag2 ? "aceito" : "recusado") + " fase " + (dp?.trans ?? "-"));
		float td = 0f;
		array2 = new float[5] { 0.15f, 0.4f, 0.7f, 1f, 1.6f };
		foreach (float gOn2 in array2)
		{
			while (td < gOn2)
			{
				td += Time.deltaTime;
				yield return null;
			}
			SideShot($"desce_{gOn2:0.0}".Replace(',', '_').Replace('.', '_'));
			yield return null;
		}
		Vector3 vector3 = car.transform.InverseTransformPoint(me.transform.position);
		Log($"depois de descer: na moto {FBVehicles.CarOf(me) != null} · posição na moto x {vector3.x:+0.00;-0.00} z {vector3.z:+0.00;-0.00} · olhando {Mathf.DeltaAngle(car.transform.eulerAngles.y, me.Yaw):0}° da frente da moto");
		if ((bool)FBVehicles.CarOf(me))
		{
			FBDrive.Exit(me, force: true);
		}
		yield return Wait(1f);
		Log("fim");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		yield return Wait(1f);
		Application.Quit();
		string MxInfo()
		{
			FBMxBike mx = car.mx;
			if (!mx)
			{
				return "";
			}
			FBDrive.PoseTargets poseTargets = (dp ? dp.Last : default(FBDrive.PoseTargets));
			Animator an = me.an;
			return $" · MX marcha {mx.GearLabel} rpm {mx.rpmN:0.00} grau {mx.wheelieAngle:0}° lean {mx.leanAngle:0}° estado {mx.state} caiu {mx.crashed} · erro mão E {Er(HumanBodyBones.LeftHand, poseTargets.hL, poseTargets.hands)} D {Er(HumanBodyBones.RightHand, poseTargets.hR, poseTargets.hands)} pé E {Er(HumanBodyBones.LeftFoot, poseTargets.fL, poseTargets.feet)} D {Er(HumanBodyBones.RightFoot, poseTargets.fR, poseTargets.feet)} m";
			string Er(HumanBodyBones b, Vector3 t, bool on)
			{
				Transform transform4 = (an ? an.GetBoneTransform(b) : null);
				if (!on || !transform4)
				{
					return "-";
				}
				return (transform4.position - t).magnitude.ToString("0.00");
			}
		}
		IEnumerator Phase(string n, float secs, float thr, float st, bool grau)
		{
			cThr = thr;
			cSt = st;
			cG = grau;
			for (float t2 = 0f; t2 < secs; t2 += Time.deltaTime)
			{
				yield return null;
			}
			Transform T = car.transform;
			float leanDeg = car.lean * 57.29578f;
			cam.transform.position = T.position + T.right * 3.2f + Vector3.up * 1.2f;
			cam.transform.LookAt(T.position + Vector3.up * 0.9f);
			cam.fieldOfView = 45f;
			yield return null;
			Shot(n + "_lado");
			cam.transform.position = T.position - T.forward * 3.6f + Vector3.up * 1.5f;
			cam.transform.LookAt(T.position + Vector3.up * 0.9f);
			yield return null;
			Shot(n + "_tras");
			Log($"{n}: {car.v * 3.6f / 1.2f:0} km/h · inclinação {leanDeg:0}° · grau {car.wheelie:0.00} · clipe {FBDrive.MotoClipOf(car) ?? me.sys} (sys {me.sys})" + MxInfo());
		}
		void SideShot(string n2)
		{
			Transform transform5 = car.transform;
			cam.transform.position = transform5.position - transform5.right * 3.4f + transform5.forward * 0.4f + Vector3.up * 1.2f;
			cam.transform.LookAt(transform5.position + Vector3.up * 0.8f);
			cam.fieldOfView = 45f;
			Shot(n2);
		}
	}
}
}
