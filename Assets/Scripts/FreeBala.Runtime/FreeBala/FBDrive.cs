using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeBala
{
public static class FBDrive
{
	public struct K
	{
		public float vmax;

		public float acc;

		public float brake;

		public float rev;

		public float steer;

		public float steerDeg;

		public float grip;

		public Vector2 cam;
	}

	public class Rider
	{
		public float v;

		public float lean;

		public float sp;

		public float a;
	}

	public class VolSetup
	{
		public Vector3 axis;

		public Vector3 pivot;

		public Vector3 baseP;

		public Quaternion baseQ;
	}

	public struct PoseTargets
	{
		public bool hands;

		public bool feet;

		public Vector3 hL;

		public Vector3 hR;

		public Vector3 fL;

		public Vector3 fR;
	}

	public static readonly Dictionary<string, K> KIND = new Dictionary<string, K>
	{
		["car"] = new K
		{
			vmax = 33f,
			acc = 8.5f,
			brake = 20f,
			rev = 9f,
			steer = 0.62f,
			steerDeg = 40f,
			grip = 1f,
			cam = new Vector2(4.6f, 1.95f)
		},
		["heavy"] = new K
		{
			vmax = 25f,
			acc = 5.2f,
			brake = 14f,
			rev = 6f,
			steer = 0.5f,
			steerDeg = 34f,
			grip = 0.9f,
			cam = new Vector2(6.4f, 2.6f)
		},
		["moto"] = new K
		{
			vmax = 36f,
			acc = 11f,
			brake = 22f,
			rev = 3f,
			steer = 0.7f,
			steerDeg = 30f,
			grip = 1.1f,
			cam = new Vector2(3.4f, 1.65f)
		}
	};

	private static readonly Dictionary<string, float> TOP = new Dictionary<string, float>
	{
		["Fusca"] = 125f,
		["Kombi"] = 110f,
		["Chevett"] = 150f,
		["Kadett"] = 170f,
		["OpalaDiplomata85"] = 180f,
		["OmegaCD1997"] = 200f,
		["Santana2002"] = 175f,
		["ParatiSurf1994"] = 165f,
		["GolQuadrado"] = 160f,
		["GolG2"] = 170f,
		["GolG5"] = 175f,
		["Up"] = 160f,
		["Celta"] = 155f,
		["CorsaClassic"] = 160f,
		["Palio2010"] = 160f,
		["UnoMille"] = 150f,
		["UnoWay"] = 160f,
		["Clio2003"] = 165f,
		["HB20"] = 170f,
		["Fiorino"] = 140f,
		["CrossFox"] = 170f,
		["SaveiroQuadrada"] = 160f,
		["SaveiroG4"] = 165f,
		["SaveiroCross"] = 170f,
		["Montana"] = 165f,
		["Nivus2024"] = 190f,
		["Jetta2012"] = 210f,
		["Golf"] = 200f,
		["GolfGtiMk7"] = 245f,
		["i30"] = 200f,
		["Corolla2018"] = 195f,
		["CivicLXR_2014"] = 195f,
		["CivicG10"] = 205f,
		["Bmw320i"] = 230f,
		["AudioS8"] = 250f,
		["Camaro"] = 270f,
		["MustangBlackShadow"] = 260f,
		["Porsche911CarreraS"] = 300f,
		["PorscheBoxster"] = 280f,
		["McLarenArtura"] = 320f,
		["LamboHuracanEvo"] = 325f,
		["MacanGTS_2024"] = 260f,
		["Evoque2020"] = 215f,
		["RangerRover"] = 230f,
		["S10_2017"] = 180f,
		["Amarok"] = 190f,
		["Toro"] = 185f,
		["L200"] = 175f,
		["Hilux2023Conquest"] = 180f,
		["Sw4"] = 180f,
		["SW4_Diamond"] = 185f,
		["RAM3500"] = 170f,
		["F250"] = 160f,
		["ViaturaPM"] = 175f,
		["ViaturaBM"] = 175f,
		["ViaturaPMAzul"] = 175f
	};

	private const string REB_SIT = "FF_SYS_REB_Dirigir";

	private static readonly string[] SG_CAR = new string[3] { "FF_SYS_SG_Driver_Idle", "FF_SYS_SG_Driver_IdleL", "FF_SYS_SG_Driver_IdleR" };

	private static readonly string[] SG_MOTO = new string[3] { "FF_SYS_SG_Vehicle_MOTO_Driver_Idle", "FF_SYS_SG_Vehicle_MOTO_Driver_IdleL", "FF_SYS_SG_Vehicle_MOTO_Driver_IdleR" };

	private static FBCar cur;

	private static FBCharacter curActor;

	private static bool prevWeaponVisible;

	private static float lastCarYaw;

	public static bool hold;

	public static bool rev;

	private static float shake;

	public const string MOTO_RIDE = "X171_Idle_on_bike_driving";

	public const string MOTO_CRASH = "X171_colision_on_bike_driving";

	public const string MOTO_MOUNT = "X171_Go_to_bike_L";

	public const string MOTO_DISMOUNT = "X171_Bike_Go_to_idle_L__Renato";

	public const float MOUNT_SPEED = 1.5f;

	public static bool MotoAnims = Array.IndexOf(Environment.GetCommandLineArgs(), "-fbmotodireto") < 0;

	public const string CAR_IN = "X171_Entrar_Motorista_SK_Mannequin_Anim1";

	public const string CAR_OUT = "X171_Sair_Motorista_Anim2";

	public static bool CarAnims = Array.IndexOf(Environment.GetCommandLineArgs(), "-fbcarrodireto") < 0;

	public static string DoorDiag;

	private static readonly Dictionary<FBCar, (string clip, float t)> motoClip = new Dictionary<FBCar, (string, float)>();

	public static string MotoLeanPos = "X171_Pose_on_bike_R";

	public static string MotoLeanNeg = "X171_Pose_on_bike_L";

	private static readonly string[] RIDE_STY = new string[6] { null, "X171_Idle_on_bike_driving", "X171_Pose_on_bike_Fwd", "X171_Pose_on_bike_Fwd_adt", "X171_exported_root_idle", "X171_pose_moto_exported_root_pose_f_2" };

	private static readonly string[] IDLE_STY = new string[4] { null, "X171_14_AA_MOTO_MOTOIdle01_Loop", "X171_16_AA_MOTO_MOTOIdle02_Loop", "X171_13_AA_MOTO_Idle_MOTOIdle03" };

	private static readonly Dictionary<FBCharacter, float> hipStand = new Dictionary<FBCharacter, float>();

	public static int PathInits;

	public static int PathFallback;

	public static string PathDiag;

	private static float scrapeT;

	private static float squealT;

	private static float brkP;

	private static float surfT;

	private static float skidK;

	private static int surf;

	private static bool hardOn;

	private static bool mxStand;

	private static bool mxTrickA;

	private static bool mxTrickB;

	private static readonly Dictionary<string, (bool acc, float accT, float rv)> gbState = new Dictionary<string, (bool, float, float)>();

	private static readonly Dictionary<FBCharacter, float> runT = new Dictionary<FBCharacter, float>();

	public static readonly float[] ZOOMS = new float[6] { 0.34f, 0.48f, 0.64f, 0.82f, 1f, 1.25f };

	private static readonly string[] ZOOM_NAMES = new string[6] { "Câmera colada", "Câmera bem perto", "Câmera perto", "Câmera média", "Câmera padrão", "Câmera longe" };

	private static float camZoom = -1f;

	private static float camZoomS = 1f;

	public static int NetLightsApplied;

	public static FBCar Cur => cur;

	public static float CamZoom
	{
		get
		{
			if (camZoom < 0f)
			{
				float @float = PlayerPrefs.GetFloat("fb_vehcam_zoom", 0.82f);
				float min = ZOOMS[0];
				float[] zOOMS = ZOOMS;
				camZoom = Mathf.Clamp(@float, min, zOOMS[zOOMS.Length - 1]);
			}
			return camZoom;
		}
	}

	public static float TopKmh(FBCar c)
	{
		if (!c)
		{
			return 99f;
		}
		if (c.kind == "moto")
		{
			return KIND["moto"].vmax * 3f;
		}
		if (c.ie)
		{
			return c.heavy ? 115 : 150;
		}
		if (!TOP.TryGetValue(c.id ?? "", out var value))
		{
			string text = c.engineProf ?? FBCarSound.ProfileFor(c.id, c) ?? "";
			value = (Regex.IsMatch(text, "V12|V10|v10|v12") ? 300 : (Regex.IsMatch(text, "V8|v8|GTR|Rotary|Flat6") ? 250 : (FBCarSound.Sporty(text) ? 215 : ((text.Contains("diesel") || c.heavy) ? 170 : (text.Contains("boxer") ? 125 : 170)))));
		}
		return value * (c.turbo ? 1.07f : 1f);
	}

	public static float TopV(FBCar c)
	{
		return TopKmh(c) * 1.2f / 3.6f;
	}

	public static float PowerAcc(FBCar car, K Kd, float v, float accK)
	{
		float num = TopV(car);
		float num2 = Mathf.Min(Kd.vmax, num * 0.9f);
		float num3 = Kd.acc * accK;
		if (v <= num2)
		{
			return num3 * (1f - Mathf.Max(0f, v) / Kd.vmax * 0.55f);
		}
		float num4 = num3 * (1f - num2 / Kd.vmax * 0.55f);
		float num5 = Mathf.Lerp(1f, Mathf.Clamp(TopKmh(car) / 190f, 0.75f, 1.5f), Mathf.Clamp01((v - num2) / 8f));
		return num4 * num5 * Mathf.Pow(Mathf.Clamp01((num * 1.03f - v) / (num * 1.03f - num2)), 1.35f);
	}

	public static K KindOf(FBCar c)
	{
		return KIND[(c.kind == "moto") ? "moto" : (c.heavy ? "heavy" : "car")];
	}

	public static Quaternion CarRot(float pitch, float yaw, float roll)
	{
		return Quaternion.Euler(pitch * 57.29578f, (0f - yaw) * 57.29578f, (0f - roll) * 57.29578f);
	}

	public static Vector3 Fwd(float yaw)
	{
		return new Vector3(0f - Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
	}

	public static Vector3 Left(float yaw)
	{
		return new Vector3(0f - Mathf.Cos(yaw), 0f, 0f - Mathf.Sin(yaw));
	}

	private static void Shake(float k)
	{
		shake = Mathf.Max(shake, k);
	}

	public static void ResetState()
	{
		if ((bool)cur)
		{
			StopAudio(cur);
		}
		cur = null;
		curActor = null;
		FBGrauHud.Hide();
	}

	private static bool SideHit(FBCar car, Vector3 o, Vector3 dir, float far)
	{
		RaycastHit[] array = Physics.RaycastAll(o, dir, far, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			Collider collider = raycastHit.collider;
			if ((bool)collider && !(collider.GetComponentInParent<FBCar>() == car) && (!car.mx || !collider.transform.IsChildOf(car.mx.transform)) && !collider.GetComponentInParent<FBCharacter>())
			{
				DoorDiag = $"bloqueado por {collider.name} a {raycastHit.distance:0.00}";
				return true;
			}
		}
		return false;
	}

	private static bool DoorSideFree(FBCar car, float k)
	{
		Vector3 vector = car.L2W(car.seat);
		Vector3 vector2 = -car.transform.right;
		Vector3 o = vector + Vector3.up * 0.55f;
		DoorDiag = "livre";
		car.SetColliders(on: false);
		try
		{
			if (SideHit(car, o, vector2, 1.35f * k))
			{
				return false;
			}
			Vector3 vector3 = vector + vector2 * 1.25f * k;
			if (!FBWorld.GroundY(vector3.x, vector.y + 1f, vector3.z, 0.4f, 2.5f).HasValue)
			{
				DoorDiag = "sem chão do lado";
				return false;
			}
			return true;
		}
		finally
		{
			car.SetColliders(on: true);
		}
	}

	public static bool StartCarIn(FBCharacter p, FBCar car)
	{
		if (!CarAnims || !car || car.kind == "moto" || (bool)car.mx || !FBRegistry.I || !FBRegistry.I.Clip("X171_Entrar_Motorista_SK_Mannequin_Anim1"))
		{
			return false;
		}
		FBDriverPose component = p.GetComponent<FBDriverPose>();
		if (!component)
		{
			return false;
		}
		float scaleK = p.ScaleK;
		if (!DoorSideFree(car, scaleK))
		{
			return false;
		}
		Vector3 vector = car.L2W(car.seat);
		Vector3 position = vector - car.transform.right * 1.235f * scaleK - car.transform.forward * 0.218f * scaleK;
		car.SetColliders(on: false);
		position.y = FBWorld.GroundY(position.x, vector.y + 1f, position.z, 0.5f, 3f) ?? car.transform.position.y;
		car.SetColliders(on: true);
		AnimationClip animationClip = FBRegistry.I.Clip("X171_Entrar_Motorista_SK_Mannequin_Anim1");
		component.trans = "mount";
		component.carAnim = true;
		component.transT = 0f;
		component.transLen = animationClip.length;
		component.baseL = car.transform.InverseTransformPoint(position);
		PathInit(p, car, component, mount: true, "X171_Entrar_Motorista_SK_Mannequin_Anim1");
		p.sys = "X171_Entrar_Motorista_SK_Mannequin_Anim1";
		if (FBDoors.HasPart(car, 1))
		{
			FBDoors.SetMask(car, FBDoors.Mask(car) | 1, sound: true);
		}
		else
		{
			FBAudio.PlayAt("veh_door_open", car.transform.position, 0.6f, 5f);
		}
		return true;
	}

	public static bool StartCarOut(FBCharacter p, FBCar car, bool needFree = true)
	{
		if (!CarAnims || !car || car.kind == "moto" || (bool)car.mx || Mathf.Abs(car.v) > 1.5f || !FBRegistry.I || !FBRegistry.I.Clip("X171_Sair_Motorista_Anim2"))
		{
			return false;
		}
		FBDriverPose component = p.GetComponent<FBDriverPose>();
		if (!component || component.trans != null)
		{
			return false;
		}
		if (needFree && !DoorSideFree(car, p.ScaleK))
		{
			return false;
		}
		AnimationClip animationClip = FBRegistry.I.Clip("X171_Sair_Motorista_Anim2");
		component.trans = "dismount";
		component.carAnim = true;
		component.transT = 0f;
		component.transLen = animationClip.length;
		component.baseL = car.transform.InverseTransformPoint(p.transform.position);
		PathInit(p, car, component, mount: false, "X171_Sair_Motorista_Anim2");
		p.sys = "X171_Sair_Motorista_Anim2";
		if (FBDoors.HasPart(car, 1))
		{
			FBDoors.SetMask(car, FBDoors.Mask(car) | 1, sound: true);
		}
		else
		{
			FBAudio.PlayAt("veh_door_open", car.transform.position, 0.6f, 5f);
		}
		if (car == cur)
		{
			car.v = 0f;
			StopAudio(car);
			FBAudio.PlayAt("veh_stop", car.transform.position, 0.45f, 5f);
		}
		return true;
	}

	public static bool OutPoint(FBCharacter p, FBCar car, out Vector3 at, out float yaw)
	{
		at = p.transform.position;
		yaw = p.Yaw;
		Animator an = p.an;
		Transform transform = (an ? an.GetBoneTransform(HumanBodyBones.Hips) : null);
		if (!transform)
		{
			return false;
		}
		car.SetColliders(on: false);
		float? num = FBWorld.GroundY(transform.position.x, transform.position.y, transform.position.z, 0.3f, 2.5f);
		car.SetColliders(on: true);
		float y = num ?? DoorOut(car, p.ScaleK).y;
		at = new Vector3(transform.position.x, y, transform.position.z);
		Transform boneTransform = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		Transform boneTransform2 = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
		if ((bool)boneTransform && (bool)boneTransform2)
		{
			Vector3 forward = Vector3.Cross(boneTransform2.position - boneTransform.position, Vector3.up).Flat();
			if (forward.sqrMagnitude > 1E-06f)
			{
				yaw = Quaternion.LookRotation(forward).eulerAngles.y;
			}
		}
		return true;
	}

	public static string MotoClipOf(FBCar car)
	{
		if (!car || !motoClip.TryGetValue(car, out (string, float) value))
		{
			return null;
		}
		return value.Item1;
	}

	private static void MotoRiderClip(FBCar car, FBCharacter a, float vmax, float dt)
	{
		if ((bool)FBRegistry.I && (bool)FBRegistry.I.Clip("X171_Idle_on_bike_driving") && !(Time.time < car.riderHoldT))
		{
			float num = Mathf.Abs(car.v) / Mathf.Max(1f, vmax);
			string text = ((car.wh != null && car.wh.a > 0.15f) ? "X171_Moto_PuxandoGrau_Pose" : ((Mathf.Abs(car.v) < 0.6f) ? "X171_14_AA_MOTO_MOTOIdle01_Loop" : ((Mathf.Abs(car.lean) > 0.2f) ? ((car.lean > 0f) ? MotoLeanPos : MotoLeanNeg) : ((!(num > 0.62f)) ? "X171_Idle_on_bike_driving" : "X171_Idle_on_bike_driving_wind"))));
			if (!FBRegistry.I.Clip(text))
			{
				text = "X171_Idle_on_bike_driving";
			}
			motoClip.TryGetValue(car, out (string, float) value);
			value.Item2 += dt;
			if (value.Item1 != text && (value.Item1 == null || value.Item2 > 0.35f))
			{
				value = (text, 0f);
				a.sys = text;
			}
			motoClip[car] = value;
		}
	}

	public static string SitClip(FBCar u)
	{
		if ((bool)u && u.kind == "moto" && (bool)FBRegistry.I && (bool)FBRegistry.I.Clip("X171_Idle_on_bike_driving"))
		{
			return "X171_Idle_on_bike_driving";
		}
		if ((bool)u && u.rigHands != null && (bool)FBRegistry.I.Clip("FF_SYS_REB_Dirigir"))
		{
			return "FF_SYS_REB_Dirigir";
		}
		string text = (((bool)u && u.kind == "moto") ? SG_MOTO : SG_CAR)[0];
		if ((bool)FBRegistry.I.Clip(text))
		{
			return text;
		}
		if (!FBRegistry.I.Clip("FF_SYS_REB_Dirigir"))
		{
			return null;
		}
		return "FF_SYS_REB_Dirigir";
	}

	public static bool Enter(FBCharacter p, FBCar car)
	{
		if (!p || !car || (bool)car.driver || car.remoteDriver >= 0 || (bool)cur || !FBVehicles.Allowed)
		{
			return false;
		}
		p.StopDance(force: true);
		prevWeaponVisible = p.weapon != null;
		// Correção: o carro parado em rampa ou meio-fio guarda a inclinação ao entrar (antes nivelava de uma vez
		// e balançava de volta). Moto continua saindo do descanso em pé.
		Vector3 eulerAngles = car.transform.eulerAngles;
		bool flag = car.kind == "moto" || (bool)car.mx;
		car.yaw = (0f - eulerAngles.y) * ((float)Math.PI / 180f);
		car.pitch = (flag ? 0f : (Mathf.Clamp(Mathf.DeltaAngle(0f, eulerAngles.x), -28f, 28f) * ((float)Math.PI / 180f)));
		car.roll = (flag ? 0f : (Mathf.Clamp(0f - Mathf.DeltaAngle(0f, eulerAngles.z), -22f, 22f) * ((float)Math.PI / 180f)));
		car.lean = (car.v = (car.steer = (car.vy = (car.stIn = 0f))));
		car.air = false;
		car.gb = null;
		car.wh = FBMoto.NewWheelie();
		car.eng = FBMoto.NewEngine();
		car.shift = Vector3.zero;
		car.transform.rotation = CarRot(car.pitch, car.yaw, car.roll);
		car.driver = p;
		FBVehicles.SetDriving(p, car);
		cur = car;
		curActor = p;
		lastCarYaw = car.transform.eulerAngles.y;
		CharacterController component = p.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		p.vehiclePose = true;
		p.sys = SitClip(car);
		p.aiming = (p.shooting = (p.sprint = (p.crouch = false)));
		p.combat = false;
		p.SetWeaponVisible(v: false);
		p.velocity = Vector3.zero;
		p.gameObject.GetOrAdd<FBDriverPose>().Set(car);
		if ((bool)car.mx)
		{
			car.mx.Recover();
			car.mx.SetActive(on: true);
			car.mx.StartBlip();
			car.wh = FBMoto.NewWheelie();
			StartMount(p, car);
			Collider[] componentsInChildren = p.GetComponentsInChildren<Collider>();
			foreach (Collider collider in componentsInChildren)
			{
				foreach (Collider col in car.cols)
				{
					if ((bool)collider && (bool)col)
					{
						Physics.IgnoreCollision(collider, col);
					}
				}
			}
		}
		else if (car.kind == "moto")
		{
			FBAudio.PlayAt("ie_moto_start", car.transform.position, 0.55f, 6f);
			StartMount(p, car);
		}
		else if (StartCarIn(p, car))
		{
			float transLen = p.GetComponent<FBDriverPose>().transLen;
			car.StartCoroutineSafe(transLen + 0.25f, delegate
			{
				if (cur == car)
				{
					FBAudio.PlayAt("veh_start", car.transform.position, 0.5f, 6f);
				}
			});
		}
		else
		{
			FBAudio.PlayAt("veh_door_open", car.transform.position, 0.6f, 5f);
			FBAudio.PlayAt("veh_start", car.transform.position, 0.5f, 6f);
			car.StartCoroutineSafe(0.52f, delegate
			{
				if (cur == car)
				{
					FBAudio.PlayAt("veh_door_close", car.transform.position, 0.6f, 5f);
				}
			});
		}
		car.wallOn = FBParedao.State(car)?.on ?? false;
		if (p == FBMatch.Player)
		{
			FBMirrors.OnEnter(car);
		}
		if (p == FBMatch.Player && (bool)FBPlayer.I)
		{
			FBPlayer.I.camYaw = car.transform.eulerAngles.y;
			FBPlayer.I.camPitch = -7f;
		}
		return true;
	}

	public static void StartMount(FBCharacter p, FBCar car)
	{
		if (MotoAnims && (bool)FBRegistry.I && (bool)FBRegistry.I.Clip("X171_Go_to_bike_L"))
		{
			FBDriverPose component = p.GetComponent<FBDriverPose>();
			if ((bool)component)
			{
				Vector3 position = car.L2W(car.seat + new Vector3(0f, 0.42f, -0.02f)) - car.transform.right * 0.36f * p.ScaleK;
				car.SetColliders(on: false);
				position.y = FBWorld.GroundY(position.x, position.y, position.z, 0.5f, 3f) ?? car.transform.position.y;
				car.SetColliders(on: true);
				component.trans = "mount";
				component.carAnim = false;
				component.transT = 0f;
				component.transLen = FBRegistry.I.Clip("X171_Go_to_bike_L").length / 1.5f;
				component.baseL = car.transform.InverseTransformPoint(position);
				PathInit(p, car, component, mount: true, "X171_Go_to_bike_L");
				p.sys = "X171_Go_to_bike_L";
			}
		}
	}

	public static bool StartDismount(FBCharacter p, FBCar car)
	{
		if (!MotoAnims || !FBRegistry.I || !FBRegistry.I.Clip("X171_Bike_Go_to_idle_L__Renato") || Mathf.Abs(car.v) > 1.5f)
		{
			return false;
		}
		FBDriverPose component = p.GetComponent<FBDriverPose>();
		if (!component)
		{
			return false;
		}
		Vector3 vector = (car.mx ? SeatHips(car) : car.L2W(car.seat + new Vector3(0f, 0.42f, -0.02f)));
		Vector3 vector2 = vector - car.transform.right * 0.55f;
		DoorDiag = "livre";
		car.SetColliders(on: false);
		bool flag = SideHit(car, vector, -car.transform.right, 0.75f);
		if (!flag && !FBWorld.GroundY(vector2.x, vector.y, vector2.z, 0.3f, 2.5f).HasValue)
		{
			flag = true;
			DoorDiag = "sem chão do lado";
		}
		car.SetColliders(on: true);
		if (flag)
		{
			return false;
		}
		component.trans = "dismount";
		component.carAnim = false;
		component.transT = 0f;
		component.transLen = FBRegistry.I.Clip("X171_Bike_Go_to_idle_L__Renato").length / 1.5f;
		component.baseL = car.transform.InverseTransformPoint(p.transform.position);
		PathInit(p, car, component, mount: false, "X171_Bike_Go_to_idle_L__Renato");
		p.sys = "X171_Bike_Go_to_idle_L__Renato";
		car.rider = null;
		return true;
	}

	public static string RiderStyle(FBCharacter a, FBCar u, string want)
	{
		string[] array = ((want == "MX_Ride") ? RIDE_STY : ((want == "MX_Idle") ? IDLE_STY : null));
		if (array == null)
		{
			return want;
		}
		int num = ((a.skinId ?? a.name).GetHashCode() * 31 + (u.syncId ?? u.id ?? "").GetHashCode()) & 0x7FFFFFFF;
		string text = array[num % array.Length];
		if (text == null || !FBRegistry.I || !FBRegistry.I.Clip(text))
		{
			return want;
		}
		return text;
	}

	public static float HipStandH(FBCharacter p)
	{
		Animator an = p.an;
		float num = 0.92f * p.ScaleK;
		if ((bool)an && an.isHuman)
		{
			Transform boneTransform = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
			Transform boneTransform2 = an.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
			Transform boneTransform3 = an.GetBoneTransform(HumanBodyBones.LeftFoot);
			Transform boneTransform4 = an.GetBoneTransform(HumanBodyBones.Hips);
			if ((bool)boneTransform && (bool)boneTransform2 && (bool)boneTransform3 && (bool)boneTransform4)
			{
				num = (boneTransform.position - boneTransform2.position).magnitude + (boneTransform2.position - boneTransform3.position).magnitude + 0.075f * p.ScaleK + Mathf.Max(0f, Vector3.Dot(boneTransform4.position - boneTransform.position, p.transform.up));
			}
		}
		if (!hipStand.TryGetValue(p, out var value) || !(Mathf.Abs(value - num) < num * 0.2f))
		{
			return num;
		}
		return value;
	}

	public static Vector3 SeatHips(FBCar u)
	{
		if (!u.mx)
		{
			return u.L2W(u.seat + ((u.kind == "moto") ? new Vector3(0f, 0.42f, -0.02f) : Vector3.zero));
		}
		return u.mx.CurHipsW;
	}

	public static Vector3 DoorOut(FBCar u, float k)
	{
		Transform transform = u.transform;
		Vector3 vector = -transform.right;
		Vector3 vector2 = (u.mx ? SeatHips(u) : u.L2W(u.seat));
		Vector3 result;
		if (u.kind == "moto")
		{
			result = vector2 + vector * 0.62f * k;
		}
		else
		{
			float num = Mathf.Abs(Vector3.Dot(transform.TransformPoint(new Vector3(u.lbox.min.x, u.seat.y, u.seat.z)) - transform.TransformPoint(u.seat), transform.right));
			result = vector2 + vector * (num + 0.38f * k) - transform.forward * 0.17f * k;
		}
		u.SetColliders(on: false);
		float? num2 = FBWorld.GroundY(result.x, vector2.y + 0.5f, result.z, 0.4f, 3f);
		u.SetColliders(on: true);
		result.y = num2 ?? transform.position.y;
		return result;
	}

	private static void PathInit(FBCharacter p, FBCar car, FBDriverPose dp, bool mount, string clip)
	{
		dp.path = false;
		dp.pathClip = clip;
		Animator an = p.an;
		Transform transform = (an ? an.GetBoneTransform(HumanBodyBones.Hips) : null);
		if (!transform || !FBAnimPaths.Has(clip))
		{
			PathFallback++;
			return;
		}
		if (mount)
		{
			float num = transform.position.y - p.transform.position.y;
			if (num > 0.4f * p.ScaleK)
			{
				hipStand[p] = num;
			}
		}
		Vector3 vector = DoorOut(car, p.ScaleK) + Vector3.up * HipStandH(p);
		Transform transform2 = car.transform;
		dp.pathS = transform2.InverseTransformPoint(vector);
		dp.pathE = dp.pathS;
		dp.pathS0 = ((mount && (transform.position - vector).magnitude < 1.6f) ? transform2.InverseTransformPoint(transform.position) : dp.pathS);
		dp.yaw0 = Mathf.DeltaAngle(transform2.eulerAngles.y, p.transform.eulerAngles.y);
		dp.path = true;
		PathInits++;
		PathDiag = $"{clip} · em pé {HipStandH(p):0.00} · porta {(vector - SeatHips(car)).Flat().magnitude:0.00} m do banco · desnível {vector.y - SeatHips(car).y:0.00}";
	}

	public static void FinishDismount(FBCharacter p, FBCar car)
	{
		Animator an = p.an;
		Transform transform = (an ? an.GetBoneTransform(HumanBodyBones.Hips) : null);
		Vector3? at = null;
		float? atYaw = null;
		if ((bool)transform)
		{
			car.SetColliders(on: false);
			float? num = FBWorld.GroundY(transform.position.x, transform.position.y, transform.position.z, 0.3f, 2.5f);
			car.SetColliders(on: true);
			at = new Vector3(transform.position.x, num ?? DoorOut(car, p.ScaleK).y, transform.position.z);
			Transform boneTransform = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
			Transform boneTransform2 = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
			if ((bool)boneTransform && (bool)boneTransform2)
			{
				Vector3 forward = Vector3.Cross(boneTransform2.position - boneTransform.position, Vector3.up).Flat();
				if (forward.sqrMagnitude > 1E-06f)
				{
					atYaw = Quaternion.LookRotation(forward).eulerAngles.y;
				}
			}
		}
		Exit(p, force: true, at, atYaw);
	}

	public static void SeatTest(FBCharacter a, FBCar car)
	{
		a.StopDance(force: true);
		CharacterController component = a.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		a.vehiclePose = true;
		a.sys = SitClip(car);
		a.aiming = (a.shooting = (a.sprint = (a.crouch = false)));
		a.combat = false;
		a.SetWeaponVisible(v: false);
		a.velocity = Vector3.zero;
		car.driver = a;
		if (car.kind == "moto")
		{
			car.rider = new Rider();
		}
		a.gameObject.GetOrAdd<FBDriverPose>().Set(car);
	}

	public static bool Exit(FBCharacter p, bool force, Vector3? at = null, float? atYaw = null)
	{
		FBCar fBCar = FBVehicles.CarOf(p);
		if (!fBCar)
		{
			return false;
		}
		FBDriverPose component = p.GetComponent<FBDriverPose>();
		if (!force && (bool)component && component.trans != null)
		{
			return false;
		}
		if (!force && Mathf.Abs(fBCar.v) > 6f)
		{
			FBHUD.I?.Center("REDUZA PARA SAIR", 1f);
			return false;
		}
		if (!force && !at.HasValue && fBCar.kind == "moto" && StartDismount(p, fBCar))
		{
			return true;
		}
		if (!force && !at.HasValue && fBCar.kind != "moto" && StartCarOut(p, fBCar))
		{
			return true;
		}
		bool flag = (bool)component && component.carAnim;
		FpHead(p, on: false);
		float scale = fBCar.scale;
		float num = fBCar.lbox.extents.x * scale;
		List<Vector3> list = new List<Vector3>();
		Vector3? rigOut = fBCar.rigOut;
		if (rigOut.HasValue)
		{
			Vector3 valueOrDefault = rigOut.GetValueOrDefault();
			list.Add(valueOrDefault * scale);
		}
		list.Add(new Vector3(0f - (num + 0.75f), 0f, fBCar.seat.z * scale));
		list.Add(new Vector3(num + 0.75f, 0f, fBCar.seat.z * scale));
		list.Add(new Vector3(0f, 0f, fBCar.lbox.min.z * scale - 1.1f));
		Vector3? vector = at;
		Vector3 cp = fBCar.transform.position;
		fBCar.SetColliders(on: false);
		try
		{
			foreach (Vector3 item in at.HasValue ? new List<Vector3>() : list)
			{
				Vector3 vector2 = Quaternion.Euler(0f, (0f - fBCar.yaw) * 57.29578f, 0f) * item + cp;
				float? num2 = FBWorld.GroundY(vector2.x, cp.y + 1.5f, vector2.z, 0.5f);
				if (num2.HasValue)
				{
					float valueOrDefault2 = num2.GetValueOrDefault();
					Vector3 vector3 = (vector2 - cp).Flat();
					// fisica 31/10: rejeita ponto de saida mais de 0,6 m acima da base do carro (teto do carro vizinho, topo de muro)
					if ((valueOrDefault2 - cp.y < 0.6f && !FBWorld.RayHit(new Vector3(vector2.x, valueOrDefault2 + 0.3f, vector2.z), Vector3.up, 1.6f) && !FBWorld.RayHit(new Vector3(cp.x, valueOrDefault2 + 1f, cp.z), vector3.normalized, vector3.magnitude)) || force)
					{
						vector = new Vector3(vector2.x, valueOrDefault2, vector2.z);
						break;
					}
				}
			}
		}
		finally
		{
			fBCar.SetColliders(on: true);
		}
		if (!vector.HasValue)
		{
			if (!force)
			{
				FBHUD.I?.Center("SEM ESPAÇO PARA SAIR", 1f);
				return false;
			}
			vector = cp + Vector3.up * (fBCar.lbox.max.y * scale + 0.1f);
		}
		StopAudio(fBCar);
		FBGrauHud.Hide();
		if (p == FBMatch.Player)
		{
			FBMirrors.OnExit(fBCar);
		}
		if (fBCar.kind != "moto" && flag)
		{
			FBCar c2 = fBCar;
			fBCar.StartCoroutineSafe(0.25f, delegate
			{
				if ((bool)c2 && !c2.driver)
				{
					if (FBDoors.HasPart(c2, 1))
					{
						FBDoors.SetMask(c2, FBDoors.Mask(c2) & -2, sound: true);
					}
					else
					{
						FBAudio.PlayAt("veh_door_close", cp, 0.55f, 5f);
					}
				}
			});
		}
		else if (fBCar.kind != "moto")
		{
			FBAudio.PlayAt("veh_stop", cp, 0.45f, 5f);
			FBAudio.PlayAt("veh_door_open", cp, 0.6f, 5f);
			fBCar.StartCoroutineSafe(0.5f, delegate
			{
				FBAudio.PlayAt("veh_door_close", cp, 0.55f, 5f);
			});
		}
		else if ((bool)fBCar.mx && force && !at.HasValue && fBCar.mx.active && !fBCar.mx.crashed && fBCar.mx.vmag * 1.2f > 1.5f)
		{
			// fisica 31/10: saida forcada (piloto morreu) com a moto andando ou no ar: ela fica na fisica e cai sozinha,
			// como no caminho de queda, em vez de virar cinematica e parar na hora (ou ficar parada no ar)
			fBCar.mx.Abandon();
		}
		else if (!fBCar.mx || !fBCar.mx.crashed)
		{
			if ((bool)fBCar.mx)
			{
				fBCar.mx.SetActive(on: false);
			}
			fBCar.transform.rotation = CarRot(0f, fBCar.yaw, 0.1f);
		}
		fBCar.v = (fBCar.steer = 0f);
		FBCarPhys.Reset(fBCar);
		fBCar.driver = null;
		FBVehicles.SetDriving(p, null);
		if (cur == fBCar)
		{
			cur = null;
			curActor = null;
		}
		FBDriverPose component2 = p.GetComponent<FBDriverPose>();
		if ((bool)component2)
		{
			UnityEngine.Object.Destroy(component2);
		}
		float num3 = atYaw ?? ((0f - fBCar.yaw) * 57.29578f);
		p.transform.SetPositionAndRotation(vector.Value, Quaternion.Euler(0f, num3, 0f));
		p.Yaw = num3;
		CharacterController component3 = p.GetComponent<CharacterController>();
		if ((bool)component3)
		{
			component3.enabled = true;
		}
		p.sys = null;
		p.vehiclePose = false;
		p.velocity = Vector3.zero;
		p.SetWeaponVisible(prevWeaponVisible && p.weapon != null);
		fBCar.wallOn = FBParedao.State(fBCar)?.on ?? false;
		return true;
	}

	public static void ReleaseSeat(FBCharacter p)
	{
		FBCar fBCar = FBVehicles.CarOf(p);
		if ((bool)fBCar)
		{
			StopAudio(fBCar);
			FBGrauHud.Hide();
			if (p == FBMatch.Player)
			{
				FBMirrors.OnExit(fBCar);
			}
			fBCar.v = (fBCar.steer = 0f);
			FBCarPhys.Reset(fBCar);
			fBCar.driver = null;
			FBVehicles.SetDriving(p, null);
			if (cur == fBCar)
			{
				cur = null;
				curActor = null;
			}
			p.sys = null;
			p.vehiclePose = false;
			p.velocity = Vector3.zero;
		}
	}

	private static void StopAudio(FBCar car)
	{
		string[] array = new string[11]
		{
			"veh_engine", "veh_moto", "veh_skid", "veh_skid_dirt", "veh_horn", "veh_wind", "veh_brake", "veh_rev", "veh_gb_par", "veh_gb_ace",
			"veh_gb_cut"
		};
		for (int i = 0; i < array.Length; i++)
		{
			FBAudio.StopLoop(array[i]);
		}
		FBCarSound.StopEngine("veh_e_");
	}

	public static bool ToggleSoundWall(FBCar car)
	{
		if (!car || !FBParedao.Has(car))
		{
			return false;
		}
		bool result = FBParedao.Toggle(car);
		car.wallOn = FBParedao.State(car)?.on ?? false;
		return result;
	}

	public static void PlayerInput(FBCharacter ch, FBInputState inp, float dt)
	{
		FBCar fBCar = FBVehicles.CarOf(ch);
		if (!fBCar)
		{
			return;
		}
		if (FBMultimidia.Open)
		{
			if (inp.multimidia || inp.pause)
			{
				FBMultimidia.Toggle();
			}
			Step(fBCar, ch, 0f, 0f, space: false, wheelSpace: false, horn: false, dt);
			return;
		}
		if (FBGarage.Open_)
		{
			Step(fBCar, ch, 0f, 0f, space: false, wheelSpace: false, horn: false, dt);
			return;
		}
		if (inp.interact)
		{
			Exit(ch, force: false);
			return;
		}
		FBVehicles.fp = false;
		CamZoomInput(inp);
		if (inp.multimidia)
		{
			FBMultimidia.Toggle();
		}
		Keyboard current = Keyboard.current;
		if (current != null && current.nKey.wasPressedThisFrame)
		{
			ToggleSoundWall(fBCar);
		}
		bool horn = current?.hKey.isPressed ?? false;
		FBCarLights.PlayerControl(fBCar);
		FBSiren.PlayerControl(fBCar);
		FBDoors.Keys(fBCar);
		float thr = Mathf.Clamp(inp.move.y * (FBInput.Touch ? 1.3f : 1f), -1f, 1f);
		float st = Mathf.Clamp((0f - inp.move.x) * (FBInput.Touch ? 1.3f : 1f), -1f, 1f);
		mxStand = inp.sprintHeld;
		mxTrickA = inp.fire;
		mxTrickB = inp.aim;
		Step(fBCar, ch, thr, st, inp.jumpHeld, inp.jumpHeld, horn, dt);
	}

	private static string Fmt(float d)
	{
		return d.ToString("0.0").Replace('.', ',') + " m";
	}

	private static void MxStep(FBCar car, FBCharacter player, float thr, float st, bool wheelKey, bool horn, float dt)
	{
		FBMxBike mx = car.mx;
		FBDriverPose fBDriverPose = (player ? player.GetComponent<FBDriverPose>() : null);
		bool flag = (bool)fBDriverPose && fBDriverPose.trans != null;
		bool num = (bool)player && player.alive;
		if (!num || flag)
		{
			thr = ((mx.speedKmh > 1f) ? (-1) : 0);
			st = 0f;
			wheelKey = false;
		}
		bool flag2 = num && !flag && mxStand && Mathf.Abs(mx.speedKmh) < 3f && thr > 0f;
		mx.Input(0f - st, flag2 ? 0f : thr, wheelKey, flag2, mxStand && !flag2, mxTrickA, mxTrickB, dt);
		if (horn && Time.time > car.hornT)
		{
			car.Horn();
		}
		if (mx.crashed)
		{
			return;
		}
		FBMoto.Wheelie wheelie = car.wh ?? (car.wh = FBMoto.NewWheelie());
		float num2 = (mx.onWheelie ? Mathf.Max(0f, mx.wheelieAngle) : 0f);
		wheelie.a = num2 * ((float)Math.PI / 180f);
		wheelie.trick = ((mx.trickA || mx.trickB) ? 1 : 0);
		float num3 = Mathf.Abs(car.v);
		if (num2 > 17f)
		{
			wheelie.dist += num3 / 1.2f * dt;
			wheelie.t += dt;
			wheelie.peak = Mathf.Max(wheelie.peak, wheelie.a);
		}
		else if (num2 < 5f && (wheelie.dist > 0f || wheelie.t > 0f))
		{
			if (wheelie.dist > 4f)
			{
				wheelie.last = wheelie.dist;
				wheelie.best = Mathf.Max(wheelie.best, wheelie.dist);
				car.grauMsg = ("GRAU " + Fmt(wheelie.dist), (wheelie.dist >= wheelie.best) ? "NOVO RECORDE" : ("RECORDE " + Fmt(wheelie.best)), false, Time.time + 2.2f);
			}
			wheelie.dist = 0f;
			wheelie.t = 0f;
			wheelie.peak = 0f;
		}
		if (player == FBMatch.Player)
		{
			FBGrauHud.Show(car);
		}
		if (Mathf.Abs(car.v) > 3f)
		{
			RunOver(car, car.v, car.transform.forward);
		}
		if (mx.rabeta && Time.time > scrapeT)
		{
			scrapeT = Time.time + 0.25f;
			FBAudio.PlayAt("veh_scrape", car.transform.position - car.transform.forward * 1.1f, 0.5f, 5f);
		}
	}

	public static void MxCrash(FBCar car, string why)
	{
		FBCharacter driver = car.driver;
		Vector3 position = car.transform.position;
		Vector3 vector = car.transform.forward * car.v;
		FBAudio.PlayAt("reb_carimpact3", position, 0.8f, 6f);
		FBNpcRotina.Event("caimoto", position, 28f);
		if ((bool)driver)
		{
			if (driver == FBMatch.Player)
			{
				Shake(1f);
			}
			Vector3 position2 = car.transform.position;
			Quaternion rotation = car.transform.rotation;
			Vector3 value = position - car.transform.right * 0.9f + Vector3.up * 0.2f;
			Exit(driver, force: true, value, car.transform.eulerAngles.y);
			car.transform.SetPositionAndRotation(position2, rotation);
			if (!FBFall.FromBike(driver, car, why))
			{
				driver.velocity = vector * 0.55f + Vector3.up * 2.5f;
				driver.grounded = false;
			}
			FBCombat.DamageActor(driver, (why == "batida") ? Mathf.Clamp(Mathf.Abs(car.v) * 2.2f, 8f, 45f) : 6f, head: false, null, "fall");
			car.grauMsg = ("CAIU DA MOTO", (why == "loop") ? "PASSOU DO PONTO DE EQUILÍBRIO" : ((why == "batida") ? "BATIDA" : ""), true, Time.time + 2.2f);
		}
	}

	private static void Step(FBCar car, FBCharacter player, float thr, float st, bool space, bool wheelSpace, bool horn, float dt)
	{
		if ((bool)car.mx)
		{
			MxStep(car, player, thr, st, wheelSpace, horn, dt);
			return;
		}
		if ((bool)player)
		{
			FBDriverPose component = player.GetComponent<FBDriverPose>();
			if ((object)component != null && component.trans != null)
			{
				space = (wheelSpace = false);
				thr = ((car.v > 0.3f) ? (-1) : 0);
				st = 0f;
			}
		}
		K kd = KindOf(car);
		bool flag = car.kind == "moto";
		bool flag2 = (bool)player && player.alive;
		bool flag3 = !flag && space;
		bool flag4 = flag && wheelSpace && flag2;
		if (!flag2)
		{
			thr = 0f;
			st = 0f;
		}
		if (hold)
		{
			HoldStep(car, player, dt);
			return;
		}
		bool flag5 = FBCarPhys.On && !flag;
		if (!flag)
		{
			float num = FBCarDamage.Pull(car);
			if (num != 0f)
			{
				st = Mathf.Clamp(st + num * Mathf.Clamp01(Mathf.Abs(car.v) / 10f), -1f, 1f);
			}
			kd.grip *= FBCarDamage.GripK(car);
		}
		if (flag5)
		{
			car.stIn = FBCarPhys.SteerIn(car, st, Mathf.Abs(car.v) * 3.6f / 1.2f, dt);
			st = car.stIn;
		}
		else
		{
			float num2 = Mathf.Clamp01(Mathf.Abs(car.v) / 25f);
			float num3 = (flag ? 2.6f : 4.2f) - num2 * 1.2f;
			car.stIn = Mathf.Lerp(car.stIn, st, 1f - Mathf.Exp((0f - dt) * ((st != 0f && Mathf.Sign(st) == Mathf.Sign((car.stIn == 0f) ? st : car.stIn)) ? num3 : 7f)));
			st = car.stIn;
		}
		Vector3 pos = car.transform.position;
		if (flag && car.shift.sqrMagnitude > 0f)
		{
			pos -= car.shift;
		}
		car.burn = flag2 && !car.air && thr > 0.5f && flag3 && Mathf.Abs(car.v) < 5f;
		if (thr > 0.6f && !flag3 && Mathf.Abs(car.v) < 1f && !car.air && car.prevThr <= 0.6f)
		{
			car.launchT = 0.9f;
		}
		car.launchT = ((thr > 0.3f) ? Mathf.Max(0f, car.launchT - dt) : 0f);
		car.prevThr = thr;
		if (!flag)
		{
			float num4 = FBCarDamage.PowerK(car);
			thr = ((!(num4 <= 0f) || !(thr > 0f)) ? (thr * ((thr > 0f) ? num4 : 1f)) : 0f);
		}
		float v = car.v;
		float accK = (car.turbo ? 1.12f : 1f) * ((car.tracao == "4x4") ? 1.06f : ((car.tracao == "FWD") ? 0.97f : 1f));
		float num5 = (flag ? FBMoto.EngineStep(car.eng, Mathf.Max(0f, car.v), (thr > 0f) ? thr : 0f, dt, kd.vmax, kd.acc * 1.3f) : 0f);
		float num6 = ((!flag && car.gb != null && car.gb.shiftT > 0f) ? 0.3f : 1f);
		if (thr > 0f)
		{
			car.v += (flag ? (((car.v < -0.5f) ? (kd.brake * thr) : num5) * dt) : (((car.v < -0.5f) ? kd.brake : (PowerAcc(car, kd, car.v, accK) * num6)) * thr * dt));
		}
		else if (thr < 0f)
		{
			car.v += ((car.v > 0.5f) ? (0f - kd.brake) : ((0f - kd.acc) * 0.7f)) * (0f - thr) * dt;
		}
		else
		{
			car.v -= JSign(car.v) * Mathf.Min(Mathf.Abs(car.v), (1.2f + Mathf.Abs(car.v) * 0.05f) * dt);
		}
		if (flag3)
		{
			car.v -= JSign(car.v) * Mathf.Min(Mathf.Abs(car.v), (float)(flag5 ? 5 : 9) * dt);
		}
		if (car.air)
		{
			car.v = v;
		}
		car.v = Mathf.Clamp(car.v, 0f - kd.rev, flag ? kd.vmax : (TopV(car) * FBCarDamage.TopK(car)));
		float num7 = Mathf.Abs(car.v) * 3.6f / 1.2f;
		float num8 = (flag ? FBMoto.SteerAngle(car.v / 1.2f) : (Mathf.Lerp(kd.steerDeg, 5f, Mathf.Min(1f, num7 / 200f)) * ((float)Math.PI / 180f)));
		float ax = (car.v - v) / Mathf.Max(dt, 0.0001f);
		if (flag5)
		{
			FBCarPhys.Lateral(car, kd, flag3, ax, dt);
			num8 = Mathf.Max(car.lim, 0.05f);
			car.steerN = Mathf.Clamp(car.steer / num8, -1f, 1f);
		}
		else
		{
			car.steer = Mathf.Lerp(car.steer, st * num8, 1f - Mathf.Exp((0f - dt) * 7f));
			car.steerN = Mathf.Clamp(car.steer / Mathf.Max(0.05f, num8), -1f, 1f);
			float num9 = car.v / car.wheelbase * Mathf.Tan(car.steer) * ((flag3 && Mathf.Abs(car.v) > 6f) ? 1.55f : 1f);
			if (flag)
			{
				num9 = FBMoto.YawRate(car.v, car.steer, car.lean, car.wheelbase) * ((car.wh.a > 0.15f) ? 0.55f : 1f);
			}
			if (!flag && Mathf.Abs(car.v) > 1f)
			{
				float num10 = 14f * kd.grip * (flag3 ? 1.55f : 1f) / Mathf.Abs(car.v);
				num9 = Mathf.Clamp(num9, 0f - num10, num10);
			}
			if (!car.air)
			{
				car.yaw += num9 * dt;
			}
			car.vl = (car.yr = 0f);
		}
		car.SetColliders(on: false);
		try
		{
			Bounds lbox = car.lbox;
			float scale = car.scale;
			float num11 = ((JSign(car.v) == 0f) ? 1f : JSign(car.v));
			float num12 = Mathf.Abs(car.v) * dt;
			Vector3 f = Fwd(car.yaw);
			Vector3 r = Left(car.yaw);
			if (num12 > 0.0001f)
			{
				float num13 = ((num11 > 0f) ? lbox.max.z : lbox.min.z) * scale;
				float num14 = (0f - lbox.center.x) * scale;
				RaycastHit? raycastHit = null;
				Vector3 halfExtents = new Vector3(flag ? 0.3f : (lbox.extents.x * scale * 0.94f), 0.38f, 0.12f);
				RaycastHit[] array = Physics.BoxCastAll(pos + f * (num13 - num11 * 0.3f) + r * num14 + Vector3.up * 0.9f, halfExtents, f * num11, Quaternion.LookRotation(f), num12 + 0.32f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
				for (int i = 0; i < array.Length; i++)
				{
					RaycastHit value = array[i];
					if ((value.distance <= 0f && value.point == Vector3.zero) || (Mathf.Abs(car.v) > 2f && FBDestructible.TryHit(value.collider, 9999f, value.point, f * num11)))
					{
						continue;
					}
					float num15 = FBFisica.Knock(value.collider, f * num11, Mathf.Abs(car.v), value.point);
					if (num15 >= 0f)
					{
						car.v *= 1f - num15;
						FBCarDamage.OnImpact(car, value.point, -f * num11, Mathf.Abs(car.v) * num15 * 2f);
						Shake(0.25f);
						continue;
					}
					FBPole fBPole = FBStreetPoleFx.PoleOf(value.collider);
					if ((bool)fBPole && (fBPole.down || fBPole.falling))
					{
						continue;
					}
					if ((bool)fBPole && !flag && Mathf.Abs(car.v) > 3.5f && car.crashT <= 0f)
					{
						float num16 = Mathf.Abs(car.v);
						FBCarDamage.OnImpact(car, value.point, -f * num11, num16 * 0.8f);
						car.crashT = 0.35f;
						Shake(Mathf.Min(1f, num16 / 16f));
						if (fBPole.Hit(f * num11, num16, value.point))
						{
							car.v *= 0.55f;
							continue;
						}
					}
					if (!raycastHit.HasValue || value.distance < raycastHit.Value.distance)
					{
						raycastHit = value;
					}
				}
				if (raycastHit.HasValue)
				{
					Vector3 normal = raycastHit.Value.normal;
					normal.y = 0f;
					normal = ((normal.sqrMagnitude > 0.0001f) ? normal.normalized : (-f * num11));
					float num17 = Mathf.Abs(Vector3.Dot(f * num11, normal));
					float num18 = Mathf.Abs(car.v) * Mathf.Max(0.25f, num17);
					FBCar componentInParent = raycastHit.Value.collider.GetComponentInParent<FBCar>();
					if (num17 < 0.55f && !flag)
					{
						Vector3 normalized = (f * num11 - normal * Vector3.Dot(f * num11, normal)).normalized;
						float num19 = Vector3.SignedAngle(f * num11, normalized, Vector3.up) * ((float)Math.PI / 180f);
						car.yaw -= num19 * Mathf.Min(1f, dt * 7f);
						car.v *= 1f - num17 * 0.7f;
						pos += normal * 0.02f;
						if (Mathf.Abs(car.v) > 4f && Time.time - scrapeT > 1.2f)
						{
							scrapeT = Time.time;
							FBAudio.PlayAt("veh_scrape", raycastHit.Value.point, Mathf.Min(0.8f, 0.3f + Mathf.Abs(car.v) / 30f), 5f);
							FBFx.Sparks(raycastHit.Value.point, raycastHit.Value.normal);
						}
					}
					else
					{
						car.v = (0f - car.v) * 0.28f;
					}
					if (flag5)
					{
						FBCarPhys.OnHit(car, (num17 < 0.55f) ? 0.6f : 0.3f);
					}
					if (num18 > 3f && car.crashT <= 0f)
					{
						if (flag && (bool)car.driver && Time.time > car.riderHoldT && (bool)FBRegistry.I && (bool)FBRegistry.I.Clip("X171_colision_on_bike_driving"))
						{
							car.driver.sys = "X171_colision_on_bike_driving";
							car.riderHoldT = Time.time + 0.9f;
						}
						FBAudio.PlayAt("reb_carimpact" + (2 + Mathf.Min(3, Mathf.FloorToInt(num18 / 6f))), pos, Mathf.Min(1f, 0.35f + num18 / 20f), 6f);
						car.crashT = 0.4f;
						Shake(Mathf.Min(1f, num18 / 18f));
						FBFx.Sparks(raycastHit.Value.point, raycastHit.Value.normal);
						FBCarDamage.OnImpact(car, raycastHit.Value.point, normal, num18);
						if ((bool)componentInParent && componentInParent != car)
						{
							FBCarDamage.OnImpact(componentInParent, raycastHit.Value.point, -normal, num18 * 0.85f);
							FBCarDamage.Shove(componentInParent, f * num11 * num18 * 0.35f, UnityEngine.Random.Range(-1f, 1f) * num18 * 4f);
						}
					}
				}
				else
				{
					pos += f * (car.v * dt);
				}
			}
			if (flag5 && Mathf.Abs(car.vl) * dt > 0.0001f)
			{
				pos = SlideSide(car, pos, f, r, dt);
			}
			car.crashT -= dt;
			float num20 = car.wheelbase / 2f;
			float num21 = car.track / 2f;
			float? num22 = G(num20, 0f);
			float? num23 = G(0f - num20, 0f);
			float? num24 = (flag ? null : G(0f, num21));
			float? num25 = (flag ? null : G(0f, 0f - num21));
			List<float> list = (from y in new float?[4] { num22, num23, num24, num25 }
				where y.HasValue
				select y.Value).ToList();
			if (list.Count > 0)
			{
				float num26 = list.Average();
				// fisica 31/10: na descida o Lerp a 25/s atrasa ~inclinacao*v/25 do chao; com so 5 cm o carro caia e pousava com reb_bump + Shake
				// a cada ~0,2 s e em ladeira forte ficava "no ar". Ja apoiado (vy==0) a tolerancia cobre esse atraso com folga de 2,5x (max 0,45 m,
				// dentro da janela de 1,6 m do G()). A descida e medida pelo pitch que o carro ja segue (>0 = nariz para baixo; de re com pitch<0
				// da o mesmo sinal), nao pela velocidade: no plano, na crista de lombada e na borda de rampa/meio-fio (nariz ainda reto ou para
				// cima) fica 5 cm e o carro voa como antes. Atraso na forma discreta do Lerp, para nao depender do FPS. Pouso (vy!=0) mantem 5 cm.
				float desce = Mathf.Max(0f, car.v * Mathf.Tan(car.pitch));
				float atraso = desce * dt / Mathf.Max(0.0001f, 1f - Mathf.Exp((0f - dt) * 25f));
				if (num26 >= pos.y - ((car.vy == 0f) ? (0.05f + Mathf.Min(0.4f, atraso * 2.5f)) : 0.05f))
				{
					pos.y = Mathf.Lerp(pos.y, num26, 1f - Mathf.Exp((0f - dt) * 25f));
					car.vy = 0f;
					car.air = false;
				}
				else
				{
					car.vy -= 22f * dt;
					pos.y += car.vy * dt;
					if (pos.y <= num26)
					{
						if (flag5)
						{
							car.bHvV += Mathf.Max(-0.6f, car.vy * 0.03f);
						}
						if (car.vy < -4f)
						{
							FBAudio.PlayAt((car.vy < -9f) ? "reb_carimpact2" : "reb_bump", pos, Mathf.Min(0.8f, 0.3f - car.vy / 20f), 5f);
							Shake(Mathf.Min(0.6f, (0f - car.vy) / 18f));
						}
						pos.y = num26;
						car.vy = 0f;
					}
					car.air = pos.y > num26 + 0.25f;
				}
				float value2 = ((num22.HasValue && num23.HasValue) ? (0f - Mathf.Atan2(num22.Value - num23.Value, num20 * 2f)) : 0f);
				float value3 = ((num24.HasValue && num25.HasValue) ? Mathf.Atan2(num24.Value - num25.Value, num21 * 2f) : 0f);
				car.pitch = Mathf.Lerp(car.pitch, Mathf.Clamp(value2, -0.5f, 0.5f), 1f - Mathf.Exp((0f - dt) * 10f));
				car.roll = Mathf.Lerp(car.roll, Mathf.Clamp(value3, -0.4f, 0.4f), 1f - Mathf.Exp((0f - dt) * 10f));
			}
			else
			{
				car.vy -= 22f * dt;
				pos.y += car.vy * dt;
				car.air = true;
			}
			if (pos.y < -150f)
			{
				pos.y = 50f;
				car.v = 0f;
			}
			if (flag)
			{
				foreach (FBMoto.Ev item in FBMoto.StepWheelie(car.wh, flag4 && !car.air, Mathf.Max(0f, thr), (thr < 0f) ? 1 : 0, car.v / 1.2f, dt))
				{
					WheelieEvent(car, item);
				}
				car.lean = Mathf.Lerp(car.lean, 0f - FBMoto.LeanTarget(st, car.v / 1.2f, car.wh.a > 0.15f), 1f - Mathf.Exp((0f - dt) * 3.5f));
				car.trick = car.wh.trick;
				car.wheelie = car.wh.a;
				car.rider = new Rider
				{
					v = Mathf.Clamp01(Mathf.Abs(car.v) / kd.vmax),
					lean = car.lean,
					sp = car.sp,
					a = car.wh.a
				};
				if ((bool)car.driver && car.driver.GetComponent<FBDriverPose>()?.trans == null)
				{
					MotoRiderClip(car, car.driver, kd.vmax, dt);
				}
				if (car.wh.a > 0.3f && Mathf.Abs(car.v) > 4f)
				{
					FBNpcRotina.Grau(car);
				}
				float num27 = (car.v - v) / Mathf.Max(dt, 0.001f);
				car.sp = Mathf.Lerp(car.sp, Mathf.Clamp((0f - num27) * 0.0065f, -0.1f, 0.1f), 1f - Mathf.Exp((0f - dt) * 9f));
			}
			float num28 = (flag ? car.wh.a : 0f);
			car.shift = Vector3.zero;
			if (num28 > 0.0001f)
			{
				Vector3 vector = new Vector3(0f, 0f, (0f - car.wheelbase) / 2f);
				car.shift = CarRot(car.pitch, car.yaw, 0f) * vector - CarRot(car.pitch - num28, car.yaw, 0f) * vector;
				pos += car.shift;
			}
			car.transform.SetPositionAndRotation(pos, CarRot(car.pitch - num28 + (flag ? car.sp : 0f), car.yaw, car.roll + car.lean));
			if (flag5)
			{
				FBCarPhys.Body(car, ax, car.air ? 0f : (car.v * car.yr), dt);
			}
			if (flag)
			{
				FBGrauHud.Show(car);
			}
			car.spin += car.v * dt;
			car.rspin += (car.burn ? 26f : ((car.launchT > 0f) ? (9f * car.launchT) : 0f)) * dt;
			PoseWheels(car, car.steer);
			if (Mathf.Abs(car.v) > 3.5f)
			{
				RunOver(car, car.v, f);
			}
			Vector3 position = car.transform.position;
			float num29 = Mathf.Abs(car.v) / kd.vmax;
			int num30;
			if ((bool)player)
			{
				FBDriverPose component = player.GetComponent<FBDriverPose>();
				num30 = (((object)component != null && component.trans == "dismount" && component.carAnim) ? 1 : 0);
			}
			else
			{
				num30 = 0;
			}
			bool flag6 = (byte)num30 != 0;
			if (flag && Json.O(car.meta, "sound") != null)
			{
				GbMotoAudio(car, car.v, flag2 && thr > 0f, car.air, car.wh.a > 0.08f || (flag4 && thr > 0f && car.v > 3f), dt, car.eng.rpm, position, "veh_gb_");
			}
			else if (flag)
			{
				var (name, value4) = EngineSample(car, Mathf.Clamp01(0.18f + num29 * 0.78f + Mathf.Max(0f, thr) * 0.08f));
				FBAudio.SetLoop("veh_engine", name, 0.42f, position, value4, 3f, 0.75f);
			}
			else
			{
				FBCar fBCar = car;
				string text = fBCar.engineProf ?? (fBCar.engineProf = FBCarSound.ProfileFor(car.id, car));
				float num31 = (flag2 ? (Mathf.Max(0f, thr) + ((thr < 0f && car.v < -0.5f) ? (0f - thr) : 0f)) : 0f);
				fBCar = car;
				if (fBCar.gb == null)
				{
					fBCar.gb = new FBCarSound.Gearbox(TopV(car));
				}
				(float, int, int) tuple2 = car.gb.Step(dt, car.v, num31);
				if (tuple2.Item3 != 0)
				{
					FBAudio.PlayAt("reb_gearshifting" + (1 + UnityEngine.Random.Range(0, 5)), position, (tuple2.Item3 > 0) ? 0.45f : 0.28f);
				}
				if (car.gb.shiftT > 0f)
				{
					num31 *= 0.2f;
				}
				if (car.gas > 0.5f && num31 < 0.2f)
				{
					FBCarSound.TurboBlow(text, tuple2.Item1, position, car.turbo);
					if (FBCarSound.Sporty(text) && tuple2.Item1 > 4800f && UnityEngine.Random.value < 0.6f)
					{
						FBAudio.PlayAt("reb_exhaustfire" + (new int[4] { 1, 2, 4, 6 })[UnityEngine.Random.Range(0, 4)], position, 0.45f);
					}
				}
				car.gas = num31;
				car.gear = tuple2.Item2;
				car.rpm = tuple2.Item1 / 7000f;
				if (!flag6)
				{
					FBCarSound.EngineAudio("veh_e_", text, tuple2.Item1, num31, position, 0.42f, 4f, 0.8f, car.turbo);
				}
				FBAudio.SetLoop("veh_wind", "reb_wind", Mathf.Clamp((Mathf.Abs(car.v) - 10f) / 40f, 0f, 0.35f));
				FBAudio.SetLoop("veh_rev", "reb_reverse", (car.v < -0.5f) ? 0.22f : 0f, position, 0.8f + Mathf.Min(1f, (0f - car.v) / kd.rev) * 0.5f);
			}
			brkP = Mathf.MoveTowards(brkP, (!car.air && thr < 0f && car.v > 0.5f) ? (0f - thr) : 0f, dt / (FBInput.Touch ? 0.12f : 0.3f));
			float num32 = Mathf.Abs(car.v);
			float num33 = ((!car.air && brkP > 0.7f && car.v > 8f) ? (Mathf.Clamp((car.v - 8f) / 14f, 0.25f, 1f) * Mathf.InverseLerp(0.7f, 1f, brkP)) : 0f);
			float num34 = ((car.air || flag5) ? 0f : ((flag3 && num32 > 6f) ? Mathf.Clamp(num32 / 20f, 0.35f, 1f) : ((Mathf.Abs(car.steer) > num8 * 0.8f && num32 > 18f) ? Mathf.Clamp((num32 - 18f) / 10f, 0.3f, 0.9f) : 0f)));
			float num35 = (car.burn ? 1f : ((car.launchT > 0f) ? (0.5f + car.launchT * 0.5f) : 0f));
			Vector3 vel = f * car.v + (flag5 ? (r * car.vl) : Vector3.zero);
			foreach (FBWheel wheel in car.wheels)
			{
				bool flag7 = !wheel.front;
				float num36 = Mathf.Max(num33 * (flag7 ? 0.85f : 1f), flag7 ? Mathf.Max(num34, num35) : (num34 * 0.3f));
				if (flag)
				{
					num36 *= 0.6f;
				}
				if (flag5)
				{
					num36 = Mathf.Max(num36, flag7 ? car.slipR : car.slipF);
				}
				if ((bool)wheel.obj)
				{
					FBCarFX.Wheel(wheel, wheel.obj.position, wheel.r, num36, vel, r, dt, car.burn && flag7, flag && !car.burn && num33 <= 0f);
				}
			}
			float b = (flag5 ? Mathf.Max(car.slipF, car.slipR) : 0f);
			if (!flag)
			{
				TireAudio(car, position, num33, Mathf.Max(num34, b), num35, dt);
			}
			else
			{
				FBAudio.SetLoop("veh_skid", "veh_skid", 0f, position);
			}
			FBAudio.SetLoop("veh_horn", FBCarSound.HornFor(car), (horn && flag2) ? 0.5f : 0f);
			float? G(float fz, float lx)
			{
				Vector3 vector2 = pos + f * fz + r * lx;
				return FBWorld.GroundY(vector2.x, pos.y + 0.9f, vector2.z, 0.3f, 2.5f);
			}
		}
		finally
		{
			car.SetColliders(on: true);
		}
		if ((bool)player)
		{
			player.velocity = Fwd(car.yaw) * car.v + Left(car.yaw) * car.vl;
			player.grounded = true;
		}
	}

	private static void TireAudio(FBCar car, Vector3 cpos, float brake, float drift, float spin, float dt)
	{
		if ((surfT -= dt) <= 0f)
		{
			surfT = 0.2f;
			surf = FBWorld.SurfaceBelow(car.transform.position + Vector3.up * 0.3f);
		}
		bool flag = surf == 1 || surf == 2 || surf == 3;
		bool air = car.air;
		float num = Mathf.Abs(car.v);
		float num2 = (air ? 0f : Mathf.Max(brake, drift, car.burn ? 1f : (spin * 0.8f)));
		skidK = Mathf.MoveTowards(skidK, num2, dt * (float)((num2 > skidK) ? 8 : 4));
		float num3 = Mathf.Lerp(0.86f, 1.08f, Mathf.Clamp01(num / 30f)) * (car.burn ? 0.95f : 1f);
		float vol = ((!flag && skidK > 0.05f) ? (Mathf.Lerp(0.12f, 0.55f, skidK) * Mathf.Clamp01(0.35f + num / 25f)) : 0f);
		float vol2 = ((flag && skidK > 0.05f) ? (Mathf.Lerp(0.15f, 0.6f, skidK) * Mathf.Clamp01(0.3f + num / 18f)) : 0f);
		FBAudio.SetLoop("veh_skid", "veh_skid", vol, cpos, num3, 7f);
		FBAudio.SetLoop("veh_skid_dirt", "veh_skid_dirt", vol2, cpos, num3 * 0.95f, 7f);
		int num4;
		if (brake > 0.2f && !flag)
		{
			num4 = ((num > 11f) ? 1 : 0);
			if (num4 != 0 && !hardOn && Time.time - squealT > 1.2f)
			{
				squealT = Time.time;
				FBAudio.PlayAt("veh_brake_squeal", cpos, Mathf.Clamp(num / 28f, 0.35f, 0.8f), 7f, 0.05f);
			}
		}
		else
		{
			num4 = 0;
		}
		if (num4 == 0 && hardOn && !flag && num < 3.5f && brkP > 0.5f && Time.time - squealT > 0.5f)
		{
			squealT = Time.time;
			FBAudio.PlayAt("veh_brake_squeal", cpos, 0.3f, 6f, 0.1f);
		}
		if (num4 != 0)
		{
			hardOn = true;
		}
		else if (num < 3.5f || brkP < 0.3f)
		{
			hardOn = false;
		}
	}

	private static Vector3 SlideSide(FBCar car, Vector3 pos, Vector3 f, Vector3 r, float dt)
	{
		Bounds lbox = car.lbox;
		float scale = car.scale;
		float num = Mathf.Sign(car.vl);
		float num2 = Mathf.Abs(car.vl) * dt;
		float num3 = (0f - lbox.center.x) * scale;
		Vector3 vector = r * num;
		RaycastHit? raycastHit = null;
		RaycastHit[] array = Physics.BoxCastAll(halfExtents: new Vector3(0.1f, 0.38f, lbox.extents.z * scale * 0.88f), center: pos + f * (lbox.center.z * scale) + r * (num3 + num * (lbox.extents.x * scale - 0.3f)) + Vector3.up * 0.9f, direction: vector, orientation: Quaternion.LookRotation(f), maxDistance: num2 + 0.32f, layerMask: FBWorld.SolidMask, queryTriggerInteraction: QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit value = array[i];
			if ((!(value.distance <= 0f) || !(value.point == Vector3.zero)) && (!(Mathf.Abs(car.vl) > 2f) || !FBDestructible.TryHit(value.collider, 9999f, value.point, vector)))
			{
				FBPole fBPole = FBStreetPoleFx.PoleOf(value.collider);
				if ((!fBPole || (!fBPole.down && !fBPole.falling)) && (!raycastHit.HasValue || value.distance < raycastHit.Value.distance))
				{
					raycastHit = value;
				}
			}
		}
		if (!raycastHit.HasValue)
		{
			return pos + vector * num2;
		}
		float num4 = Mathf.Abs(car.vl);
		Vector3 normal = raycastHit.Value.normal;
		normal.y = 0f;
		normal = ((normal.sqrMagnitude > 0.0001f) ? normal.normalized : (-vector));
		if (num4 > 3f && car.crashT <= 0f)
		{
			FBAudio.PlayAt("reb_carimpact" + (2 + Mathf.Min(3, Mathf.FloorToInt(num4 / 6f))), raycastHit.Value.point, Mathf.Min(1f, 0.35f + num4 / 20f), 6f);
			car.crashT = 0.4f;
			Shake(Mathf.Min(1f, num4 / 18f));
			FBFx.Sparks(raycastHit.Value.point, raycastHit.Value.normal);
			FBCarDamage.OnImpact(car, raycastHit.Value.point, normal, num4);
			FBCar componentInParent = raycastHit.Value.collider.GetComponentInParent<FBCar>();
			if ((bool)componentInParent && componentInParent != car)
			{
				FBCarDamage.OnImpact(componentInParent, raycastHit.Value.point, -normal, num4 * 0.85f);
				FBCarDamage.Shove(componentInParent, vector * num4 * 0.35f, UnityEngine.Random.Range(-1f, 1f) * num4 * 4f);
			}
		}
		car.vl = (0f - car.vl) * 0.15f;
		car.yr *= 0.5f;
		return pos + normal * 0.01f;
	}

	public static void Simulate(FBCar car, FBCharacter p, float thr, float st, bool space, float dt)
	{
		Step(car, p, thr, st, space, space, horn: false, dt);
	}

	private static float JSign(float x)
	{
		return (x > 0f) ? 1 : ((x < 0f) ? (-1) : 0);
	}

	private static (string, float) EngineSample(FBCar u, float rpm)
	{
		float num = (u.heavy ? 0.82f : 1f);
		if (u.kind == "moto")
		{
			return ("ie_moto_idle", num * (0.75f + rpm * 1.1f));
		}
		if (u.ie)
		{
			if (rpm < 0.24f)
			{
				return ("veh_eng_idle", num * (0.9f + rpm * 0.3f));
			}
			return ((rpm < 0.72f) ? "ie_eng_low" : "ie_eng_high", num * (0.78f + rpm * 0.5f));
		}
		return ((rpm < 0.24f) ? "veh_eng_idle" : ((rpm < 0.48f) ? "veh_eng_low" : ((rpm < 0.74f) ? "veh_eng_med" : "veh_eng_high")), num * (0.78f + rpm * 0.5f));
	}

	private static void GbMotoAudio(FBCar u, float v, bool acc, bool air, bool cut, float dt, float? rpm, Vector3 pos, string keys)
	{
		Dictionary<string, object> S = Json.O(u.meta, "sound");
		float num = Mathf.Clamp01(Mathf.Abs(v) / KIND["moto"].vmax);
		bool flag = acc && !air;
		gbState.TryGetValue(keys, out (bool, float, float) value);
		if (value.Item1 && !flag && value.Item2 > 0.5f && Mathf.Abs(v) > 2f && !cut && C("reduzindo") != null)
		{
			FBAudio.PlayAt(C("reduzindo"), pos, 0.62f);
		}
		value.Item2 = (flag ? (value.Item2 + dt) : 0f);
		value.Item1 = flag;
		gbState[keys] = value;
		if (C("parado") != null)
		{
			FBAudio.SetLoop(keys + "par", C("parado"), (flag || cut) ? 0f : 0.34f, pos, 1f, 3.5f, 0.75f);
		}
		if (C("acelerando") != null)
		{
			FBAudio.SetLoop(keys + "ace", C("acelerando"), (flag && !cut) ? 0.56f : 0f, pos, rpm.HasValue ? (0.84f + rpm.Value * 0.5f) : (0.88f + num * 0.42f), 3.5f, 0.75f);
		}
		if (C("corte") != null)
		{
			FBAudio.SetLoop(keys + "cut", C("corte"), cut ? 0.62f : 0f, pos, 0.92f + num * 0.2f, 3.5f, 0.75f);
		}
		string C(string k)
		{
			return Json.S(Json.O(S, k), "clip");
		}
	}

	private static void WheelieEvent(FBCar car, FBMoto.Ev e)
	{
		Vector3 position = car.transform.position;
		if (e.kind == "start")
		{
			FBAudio.PlayAt("ie_moto_start", position, 0.5f);
		}
		else if (e.kind == "land")
		{
			FBAudio.PlayAt("reb_bump", position, Mathf.Clamp(0.25f + e.power * 0.12f, 0.25f, 0.7f), 5f);
			Shake(Mathf.Clamp(e.power * 0.08f, 0.1f, 0.5f));
		}
		else if (e.kind == "loop")
		{
			FBNpcRotina.Event("caimoto", position, 28f);
			FBAudio.PlayAt("reb_carimpact3", position, 0.7f, 5f);
			Shake(0.8f);
			car.v *= 0.2f;
			car.grauMsg = ("PERDEU O EQUILÍBRIO", (e.dist > 4f) ? ("GRAU ANULADO · " + Fmt(e.dist)) : "", true, Time.time + 2.2f);
		}
		else if (e.kind == "end")
		{
			car.grauMsg = ("GRAU " + Fmt(e.dist), ((e.dist >= car.wh.best - 1E-06f) ? "NOVO RECORDE · " : ("RECORDE " + Fmt(car.wh.best) + " · ")) + e.time.ToString("0.0").Replace('.', ',') + " s", false, Time.time + 3f);
		}
		static string Fmt(float d)
		{
			return d.ToString("0.0").Replace('.', ',') + " m";
		}
	}

	private static void HoldStep(FBCar car, FBCharacter p, float dt)
	{
		car.v = 0f;
		car.steer *= 0.9f;
		string profile = car.engineProf ?? (car.engineProf = FBCarSound.ProfileFor(car.id, car));
		float num = (rev ? 1 : 0);
		car.revRpm = Mathf.Min(7000f, Mathf.Max(900f, ((car.revRpm == 0f) ? 900f : car.revRpm) + (float)((num > 0f) ? 5200 : (-3800)) * dt * ((num > 0f && car.revRpm > 6600f) ? 0.2f : 1f)));
		if (num > 0f && car.revRpm > 6750f)
		{
			car.revRpm -= 380f * UnityEngine.Random.value;
		}
		if (car.gas > 0.5f && num == 0f)
		{
			FBCarSound.TurboBlow(profile, car.revRpm, car.transform.position);
		}
		car.gas = num;
		car.rpm = car.revRpm / 7000f;
		if (car.kind != "moto")
		{
			FBCarSound.EngineAudio("veh_e_", profile, car.revRpm, num, car.transform.position, 0.5f);
		}
		foreach (FBWheel wheel in car.wheels)
		{
			if ((bool)wheel.obj)
			{
				wheel.obj.localRotation = wheel.bas;
			}
		}
	}

	public static void PoseWheels(FBCar u, float steer)
	{
		foreach (FBWheel wheel in u.wheels)
		{
			if ((bool)wheel.obj)
			{
				float angle = (u.spin + (wheel.front ? 0f : u.rspin)) / ((wheel.r <= 0f) ? 0.4f : wheel.r) * (float)wheel.sgn * 57.29578f;
				float num = ((wheel.front && !wheel.noSteer) ? (steer * ((u.kind == "moto") ? 0.8f : 1f) * 57.29578f) : 0f);
				wheel.obj.localRotation = Quaternion.AngleAxis(0f - num, Vector3.up) * wheel.bas * Quaternion.AngleAxis(angle, Vector3.right);
			}
		}
		foreach (var (transform, quaternion) in u.steerNodes)
		{
			if ((bool)transform)
			{
				transform.localRotation = Quaternion.AngleAxis((0f - steer) * 0.8f * 57.29578f, u.steerAxis ?? Vector3.up) * quaternion;
			}
		}
		u.steerAng = steer * 0.8f;
		if ((bool)u.volante)
		{
			TurnVolante(u, u.steerN);
		}
	}

	private static void RunOver(FBCar car, float v, Vector3 f)
	{
		Bounds lbox = car.lbox;
		foreach (FBCharacter item in FBMatch.All.ToList())
		{
			if (!item || !item.alive || item == car.driver || item.isRemote || item.vehiclePose || (runT.TryGetValue(item, out var value) && Time.time - value < 0.7f))
			{
				continue;
			}
			Vector3 vector = car.transform.InverseTransformPoint(item.Pos + Vector3.up);
			if (!(vector.x < lbox.min.x - 0.35f) && !(vector.x > lbox.max.x + 0.35f) && !(vector.z < lbox.min.z - 0.35f) && !(vector.z > lbox.max.z + 0.35f) && !(vector.y < lbox.min.y - 1f) && !(vector.y > lbox.max.y + 1f))
			{
				runT[item] = Time.time;
				if ((bool)car.driver && item != car.driver && !FBMatch.FFA && item.team == car.driver.team && !item.civ && FBSession.mode != "role")
				{
					item.velocity += f * v * 0.25f;
					continue;
				}
				// fisica 31/10: de re (v<0) a vitima e lancada para tras do carro, nao para o capo contra a carroceria
				FBAtropelo.Hit(item, car, v, (v < 0f) ? (-f) : f, FBSession.mode != "role" || item.isPlayer);
				car.v *= (car.mx ? 0.9f : 0.82f);
			}
		}
	}

	private static Vector3 Eig3Min(double[] C)
	{
		double[,] a = new double[3, 3]
		{
			{
				C[0],
				C[1],
				C[2]
			},
			{
				C[1],
				C[3],
				C[4]
			},
			{
				C[2],
				C[4],
				C[5]
			}
		};
		double[,] array = new double[3, 3]
		{
			{ 1.0, 0.0, 0.0 },
			{ 0.0, 1.0, 0.0 },
			{ 0.0, 0.0, 1.0 }
		};
		for (int i = 0; i < 30; i++)
		{
			int num = 0;
			int num2 = 1;
			if (Math.Abs(a[0, 2]) > Math.Abs(a[num, num2]))
			{
				num = 0;
				num2 = 2;
			}
			if (Math.Abs(a[1, 2]) > Math.Abs(a[num, num2]))
			{
				num = 1;
				num2 = 2;
			}
			if (Math.Abs(a[num, num2]) < 1E-12)
			{
				break;
			}
			double num3 = 0.5 * Math.Atan2(2.0 * a[num, num2], a[num2, num2] - a[num, num]);
			double num4 = Math.Cos(num3);
			double num5 = Math.Sin(num3);
			for (int j = 0; j < 3; j++)
			{
				double num6 = a[j, num];
				double num7 = a[j, num2];
				a[j, num] = num4 * num6 - num5 * num7;
				a[j, num2] = num5 * num6 + num4 * num7;
			}
			for (int k = 0; k < 3; k++)
			{
				double num8 = a[num, k];
				double num9 = a[num2, k];
				a[num, k] = num4 * num8 - num5 * num9;
				a[num2, k] = num5 * num8 + num4 * num9;
			}
			for (int l = 0; l < 3; l++)
			{
				double num10 = array[l, num];
				double num11 = array[l, num2];
				array[l, num] = num4 * num10 - num5 * num11;
				array[l, num2] = num5 * num10 + num4 * num11;
			}
		}
		int num12 = new int[3] { 0, 1, 2 }.OrderBy((int x) => a[x, x]).First();
		return new Vector3((float)array[0, num12], (float)array[1, num12], (float)array[2, num12]).normalized;
	}

	private static VolSetup Vol(FBCar u)
	{
		if (u.volTried)
		{
			return u.vol;
		}
		u.volTried = true;
		Transform volante = u.volante;
		if (!volante)
		{
			return null;
		}
		Transform parent = volante.parent;
		Quaternion localRotation = volante.localRotation;
		volante.localRotation = u.volBase;
		Matrix4x4 worldToLocalMatrix = parent.worldToLocalMatrix;
		List<Vector3> list = new List<Vector3>();
		MeshFilter[] componentsInChildren = volante.GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			Mesh sharedMesh = meshFilter.sharedMesh;
			if ((bool)sharedMesh && sharedMesh.isReadable)
			{
				Matrix4x4 matrix4x = worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
				Vector3[] vertices = sharedMesh.vertices;
				int num = Mathf.Max(1, vertices.Length / 1500);
				for (int j = 0; j < vertices.Length; j += num)
				{
					list.Add(matrix4x.MultiplyPoint3x4(vertices[j]));
				}
			}
		}
		volante.localRotation = localRotation;
		if (list.Count < 8)
		{
			return null;
		}
		Vector3 zero = Vector3.zero;
		foreach (Vector3 item in list)
		{
			zero += item;
		}
		zero /= (float)list.Count;
		double[] array = new double[6];
		foreach (Vector3 item2 in list)
		{
			double num2 = item2.x - zero.x;
			double num3 = item2.y - zero.y;
			double num4 = item2.z - zero.z;
			array[0] += num2 * num2;
			array[1] += num2 * num3;
			array[2] += num2 * num4;
			array[3] += num3 * num3;
			array[4] += num3 * num4;
			array[5] += num4 * num4;
		}
		Vector3 vector = Eig3Min(array);
		Vector3 vector2 = parent.InverseTransformPoint(u.L2W(u.seat));
		if (Vector3.Dot(vector, vector2 - zero) < 0f)
		{
			vector = -vector;
		}
		return u.vol = new VolSetup
		{
			axis = vector,
			pivot = zero,
			baseP = volante.localPosition,
			baseQ = u.volBase
		};
	}

	private static void TurnVolante(FBCar u, float steerN)
	{
		Transform volante = u.volante;
		VolSetup volSetup = Vol(u);
		if (volSetup == null)
		{
			volante.localRotation = u.volBase * Quaternion.AngleAxis(steerN * 1.25f * 57.29578f, Vector3.forward);
			return;
		}
		Quaternion quaternion = Quaternion.AngleAxis((0f - steerN) * 1.25f * 57.29578f, volSetup.axis);
		volante.localRotation = quaternion * volSetup.baseQ;
		volante.localPosition = quaternion * (volSetup.baseP - volSetup.pivot) + volSetup.pivot;
	}

	public static PoseTargets PoseActor(FBCharacter a, FBCar u, float w = 1f, FBDriverPose dp = null)
	{
		PoseTargets result = default(PoseTargets);
		Animator an = a.an;
		if (!an || !an.isHuman)
		{
			return result;
		}
		Transform boneTransform = an.GetBoneTransform(HumanBodyBones.Hips);
		if (!boneTransform)
		{
			return result;
		}
		string text = (dp ? dp.trans : null);
		float num = (((bool)dp && dp.transLen > 0f) ? Mathf.Clamp01(dp.transT / dp.transLen) : 1f);
		float num2 = 1f;
		float num3 = 1f;
		switch (text)
		{
		case "mount":
			num2 = Mathf.SmoothStep(0f, 1f, (num - 0.35f) / 0.65f);
			num3 = Mathf.SmoothStep(0f, 1f, (num - 0.6f) / 0.4f);
			break;
		case "dismount":
			num2 = 1f - Mathf.SmoothStep(0f, 1f, (num - 0.12f) / 0.58f);
			num3 = 1f - Mathf.SmoothStep(0f, 1f, num / 0.3f);
			break;
		case "blend":
			num3 = Mathf.Lerp(0.6f, 1f, num);
			break;
		}
		w *= num3;
		Quaternion quaternion = ((text == "mount") ? (u.transform.rotation * Quaternion.Euler(0f, 90f, 0f)) : u.transform.rotation);
		bool flag = text == "mount" || text == "dismount";
		bool flag2 = false;
		float wh = 0f;
		float wv = 0f;
		float lat = 0f;
		float bump = 0f;
		if (flag && dp.path)
		{
			flag2 = FBAnimPaths.Eval(dp.pathClip, num, out wh, out wv, out lat, out bump);
		}
		if (flag2 && text == "mount")
		{
			quaternion = Quaternion.Slerp(u.transform.rotation * Quaternion.Euler(0f, dp.yaw0, 0f), quaternion, Mathf.SmoothStep(0f, 1f, num / 0.15f));
		}
		a.transform.rotation = quaternion;
		Vector3 vector = SeatHips(u);
		if (flag2)
		{
			Vector3 vector2 = vector - u.transform.up * ((a.fitCar == u) ? a.seatDrop : 0f);
			Transform transform = u.transform;
			Vector3 vector3;
			Vector3 vector4;
			if (text == "mount")
			{
				vector3 = Vector3.Lerp(transform.TransformPoint(dp.pathS0), transform.TransformPoint(dp.pathS), Mathf.SmoothStep(0f, 1f, num / 0.15f));
				vector4 = vector2;
			}
			else
			{
				vector3 = vector2;
				vector4 = transform.TransformPoint(dp.pathE);
			}
			Vector3 v = vector4 - vector3;
			Vector3 vector5 = v.Flat();
			Vector3 vector6 = ((vector5.sqrMagnitude > 1E-06f) ? Vector3.Cross(Vector3.up, vector5.normalized) : Vector3.zero);
			float num4 = an.humanScale * a.transform.lossyScale.y;
			a.transform.position += vector3 + vector5 * wh + Vector3.up * (v.y * wv + bump * num4) + vector6 * lat * num4 - boneTransform.position;
		}
		else if (flag)
		{
			a.transform.position = u.transform.TransformPoint(dp.baseL);
			a.transform.position += (vector - boneTransform.position) * num2;
		}
		else
		{
			a.transform.position += vector - boneTransform.position;
		}
		if (text == "blend")
		{
			Transform boneTransform2 = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
			Transform boneTransform3 = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
			if ((bool)boneTransform2 && (bool)boneTransform3)
			{
				Vector3 from = Vector3.Cross(boneTransform3.position - boneTransform2.position, Vector3.up).Flat();
				if (from.sqrMagnitude > 1E-06f)
				{
					float angle = Vector3.SignedAngle(from, u.transform.forward.Flat(), Vector3.up);
					a.transform.RotateAround(boneTransform.position, Vector3.up, angle);
				}
			}
			a.transform.position += vector - boneTransform.position;
		}
		Vector3 up = u.transform.up;
		if (a.fitCar != u)
		{
			a.fitCar = u;
			a.seatDrop = 0f;
			Transform boneTransform4 = an.GetBoneTransform(HumanBodyBones.Head);
			if ((bool)boneTransform4)
			{
				Vector3 origin = boneTransform4.position + up * 1.4f;
				int layerMask = 2048;
				float num5 = 9f;
				RaycastHit[] array = Physics.RaycastAll(origin, -up, 1.4f, layerMask, QueryTriggerInteraction.Ignore);
				for (int j = 0; j < array.Length; j++)
				{
					RaycastHit raycastHit = array[j];
					if (raycastHit.collider.GetComponentInParent<FBCar>() == u && raycastHit.distance < num5)
					{
						num5 = raycastHit.distance;
					}
				}
				if (num5 < 9f)
				{
					float num6 = 1.4f - num5 - 0.035f - 0.22f * (a.transform.lossyScale.y / 2.2f * 1.25f);
					a.seatDrop = Mathf.Min(0.35f, Mathf.Max(0f, 0.04f - num6));
				}
			}
		}
		if (a.seatDrop > 0f && !flag2)
		{
			a.transform.position -= up * a.seatDrop;
		}
		if ((bool)u.mx && text == null)
		{
			string text2 = u.mx.RiderClip(Time.deltaTime);
			if (!a.isPlayer)
			{
				text2 = RiderStyle(a, u, text2);
			}
			if (a.sys != text2 && (bool)FBRegistry.I && (bool)FBRegistry.I.Clip(text2))
			{
				a.sys = text2;
			}
			return u.mx.PoseRider(a, w);
		}
		if (u.kind == "moto" && u.rider != null)
		{
			RiderBody(a, u, u.rider, w);
		}
		Transform transform2 = B(HumanBodyBones.LeftUpperArm);
		Transform lF = B(HumanBodyBones.LeftLowerArm);
		Transform transform3 = B(HumanBodyBones.LeftHand);
		Transform transform4 = B(HumanBodyBones.RightUpperArm);
		Transform rF = B(HumanBodyBones.RightLowerArm);
		Transform transform5 = B(HumanBodyBones.RightHand);
		Vector3 vector7 = -u.transform.forward;
		if (u.rigHands == null)
		{
			Vector3? grip = u.grip;
			if (grip.HasValue)
			{
				Vector3 valueOrDefault = grip.GetValueOrDefault();
				if ((bool)transform2 && (bool)transform4)
				{
					float num7 = ((u.gripSide > 0f) ? u.gripSide : ((u.kind == "moto") ? 0.34f : 0.19f));
					float num8 = ((u.kind == "moto") ? u.trick : 0f);
					Vector3 vector8 = u.L2W(valueOrDefault + new Vector3(0f - (num7 + num8 * 0.1f), num8 * 0.5f, (0f - num8) * 0.22f));
					Vector3 vector9 = u.L2W(valueOrDefault + new Vector3(num7 + num8 * 0.1f, num8 * 0.5f, (0f - num8) * 0.22f));
					if (u.kind == "moto" && u.steerNodes.Count > 0 && u.steerAng != 0f)
					{
						Vector3 position = u.steerNodes[0].obj.position;
						Vector3 axis2 = u.transform.TransformDirection(u.steerAxis ?? Vector3.up);
						Quaternion quaternion2 = Quaternion.AngleAxis((0f - u.steerAng) * (1f - num8) * 57.29578f, axis2);
						vector8 = position + quaternion2 * (vector8 - position);
						vector9 = position + quaternion2 * (vector9 - position);
					}
					Reach(a, u, dp, transform2, lF, transform3, transform4, rF, transform5, vector8, vector9, w);
					FBIK.TwoBonePole(transform2, lF, transform3, vector8, lF.position + vector7 * 0.4f, w);
					FBIK.TwoBonePole(transform4, rF, transform5, vector9, rF.position + vector7 * 0.4f, w);
					result.hands = true;
					result.hL = vector8;
					result.hR = vector9;
				}
			}
		}
		float sn;
		Vector3 hub;
		Vector3 axis;
		if (u.rigHands != null && (bool)transform2 && (bool)transform4)
		{
			sn = u.steerN;
			VolSetup volSetup = Vol(u);
			if (volSetup != null)
			{
				Transform parent = u.volante.parent;
				hub = parent.TransformPoint(volSetup.pivot);
				axis = -parent.TransformDirection(volSetup.axis).normalized;
			}
			else
			{
				hub = u.L2W(u.rigWheel ?? Vector3.Lerp(u.rigHands[0], u.rigHands[1], 0.5f));
				axis = (hub - (u.L2W(u.seat) + up * 0.45f)).normalized;
			}
			result.hands = true;
			result.hL = Hand(u.rigHands[0]);
			result.hR = Hand(u.rigHands[1]);
			Reach(a, u, dp, transform2, lF, transform3, transform4, rF, transform5, result.hL, result.hR, w);
			FBIK.TwoBonePole(transform2, lF, transform3, result.hL, Pole(0), w);
			FBIK.TwoBonePole(transform4, rF, transform5, result.hR, Pole(1), w);
		}
		(Transform, Transform, Transform)[] array2 = new(Transform, Transform, Transform)[2]
		{
			(B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot)),
			(B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot))
		};
		for (int k = 0; k < 2; k++)
		{
			var (upper, transform6, transform7) = array2[k];
			if (!transform7)
			{
				continue;
			}
			Vector3 vector10;
			Vector3 pole;
			if (u.rigFeet != null)
			{
				vector10 = u.L2W(u.rigFeet[k]);
				pole = transform6.position + u.transform.forward * 0.6f + up * 0.35f;
			}
			else
			{
				Vector3? grip = u.feet;
				if (!grip.HasValue)
				{
					continue;
				}
				Vector3 valueOrDefault2 = grip.GetValueOrDefault();
				vector10 = u.L2W(valueOrDefault2 + new Vector3((k == 0) ? (0f - u.footSide) : u.footSide, 0f, 0f));
				pole = transform6.position + u.transform.forward * 0.5f + up * 0.2f;
			}
			Quaternion rotation = transform7.rotation;
			FBIK.TwoBonePole(upper, transform6, transform7, vector10, pole, w);
			transform7.rotation = rotation;
			result.feet = true;
			if (k == 0)
			{
				result.fL = vector10;
			}
			else
			{
				result.fR = vector10;
			}
		}
		return result;
		Transform B(HumanBodyBones b)
		{
			return an.GetBoneTransform(b);
		}
		Vector3 Hand(Vector3 p)
		{
			return hub + Quaternion.AngleAxis(sn * 1.25f * 57.29578f, axis) * (u.L2W(p) - hub);
		}
		Vector3 Pole(int i)
		{
			if (u.rigPoles == null)
			{
				return ((i == 1) ? rF : lF).position - Vector3.up * 0.3f + u.transform.right * ((i == 1) ? 0.25f : (-0.25f));
			}
			return u.L2W(u.rigPoles[i]);
		}
	}

	public static void Reach(FBCharacter a, FBCar u, FBDriverPose dp, Transform lA, Transform lF, Transform lH, Transform rA, Transform rF, Transform rH, Vector3 tL, Vector3 tR, float w)
	{
		Animator an = a.an;
		Transform boneTransform = an.GetBoneTransform(HumanBodyBones.Spine);
		Transform transform = an.GetBoneTransform(HumanBodyBones.Chest) ?? an.GetBoneTransform(HumanBodyBones.UpperChest);
		float num = Need();
		float num2 = 0f;
		if (num > 0f && (bool)boneTransform && (bool)lA)
		{
			float num3 = Mathf.Max(0.2f, (lA.position - boneTransform.position).magnitude);
			num2 = Mathf.Min(((bool)u && (bool)u.mx) ? 42 : 25, Mathf.Asin(Mathf.Clamp01(num / num3)) * 57.29578f * 1.15f);
		}
		float num4 = num2;
		if ((bool)dp)
		{
			dp.reachLean = Mathf.Lerp(dp.reachLean, num2, 1f - Mathf.Exp((0f - Time.deltaTime) * 6f));
			num4 = dp.reachLean;
		}
		num4 *= w;
		Vector3 right = u.transform.right;
		if (num4 > 0.05f)
		{
			if ((bool)boneTransform)
			{
				boneTransform.rotation = Quaternion.AngleAxis(num4 * (transform ? 0.55f : 1f), right) * boneTransform.rotation;
			}
			if ((bool)transform)
			{
				transform.rotation = Quaternion.AngleAxis(num4 * 0.45f, right) * transform.rotation;
			}
		}
		float num5 = Need();
		if (num5 > 0.002f)
		{
			a.transform.position += u.transform.forward * Mathf.Min(num5, ((bool)u && (bool)u.mx) ? 0.2f : 0.1f) * w;
		}
		float Need()
		{
			float num6 = 0f;
			(Transform, Transform, Transform, Vector3)[] array = new(Transform, Transform, Transform, Vector3)[2]
			{
				(lA, lF, lH, tL),
				(rA, rF, rH, tR)
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (transform2, transform3, transform4, vector) = array[i];
				if ((bool)transform2 && (bool)transform3 && (bool)transform4)
				{
					float num7 = (transform3.position - transform2.position).magnitude + (transform4.position - transform3.position).magnitude;
					num6 = Mathf.Max(num6, (vector - transform2.position).magnitude - num7 * 0.97f);
				}
			}
			return num6;
		}
	}

	private static void RiderBody(FBCharacter a, FBCar car, Rider r, float wt = 1f)
	{
		Animator an = a.an;
		Vector3 right = car.transform.right;
		Vector3 forward = car.transform.forward;
		float num = ((0.04f + r.v * 0.3f + Mathf.Clamp((0f - r.sp) * 3f, 0f, 0.2f)) * (1f - Mathf.Clamp01(r.a / 0.5f)) - r.a * 0.55f) * wt;
		float num2 = Mathf.Clamp(r.lean * 0.55f, -0.4f, 0.4f) * wt;
		float[] array = new float[3] { 0.3f, 0.4f, 0.3f };
		int num3 = 0;
		HumanBodyBones[] array2 = new HumanBodyBones[3]
		{
			HumanBodyBones.Spine,
			HumanBodyBones.Chest,
			HumanBodyBones.UpperChest
		};
		foreach (HumanBodyBones humanBoneId in array2)
		{
			Transform boneTransform = an.GetBoneTransform(humanBoneId);
			if ((bool)boneTransform)
			{
				float num4 = array[num3++ % 3];
				boneTransform.rotation = Quaternion.AngleAxis(num * num4 * 57.29578f, right) * Quaternion.AngleAxis((0f - num2) * num4 * 57.29578f, forward) * boneTransform.rotation;
			}
		}
		Transform transform = an.GetBoneTransform(HumanBodyBones.Neck) ?? an.GetBoneTransform(HumanBodyBones.Head);
		if ((bool)transform)
		{
			transform.rotation = Quaternion.AngleAxis((0f - num) * 0.75f * 57.29578f, right) * Quaternion.AngleAxis(num2 * 0.8f * 57.29578f, forward) * transform.rotation;
		}
	}

	public static void FpHead(FBCharacter a, bool on)
	{
		if ((bool)a && (bool)a.an)
		{
			Transform boneTransform = a.an.GetBoneTransform(HumanBodyBones.Head);
			if ((bool)boneTransform && a.fpHeadOn != on)
			{
				a.fpHeadOn = on;
				boneTransform.localScale = Vector3.one * (on ? 0.001f : 1f);
			}
		}
	}

	public static void SetZoom(float z, bool label)
	{
		float min = ZOOMS[0];
		float[] zOOMS = ZOOMS;
		camZoom = Mathf.Clamp(z, min, zOOMS[zOOMS.Length - 1]);
		PlayerPrefs.SetFloat("fb_vehcam_zoom", camZoom);
		if (!label)
		{
			return;
		}
		int num = 0;
		for (int i = 1; i < ZOOMS.Length; i++)
		{
			if (Mathf.Abs(ZOOMS[i] - camZoom) < Mathf.Abs(ZOOMS[num] - camZoom))
			{
				num = i;
			}
		}
		FBHUD.I?.Center(ZOOM_NAMES[num], 0.9f);
	}

	private static void CamZoomInput(FBInputState inp)
	{
		if (inp.toggleView)
		{
			int num = Array.FindIndex(ZOOMS, (float z) => z > CamZoom + 0.01f);
			SetZoom((num < 0) ? ZOOMS[0] : ZOOMS[num], label: true);
		}
		Mouse current = Mouse.current;
		if (current != null && FBInput.Locked)
		{
			float y = current.scroll.ReadValue().y;
			if (Mathf.Abs(y) > 0.1f)
			{
				SetZoom(CamZoom * ((y > 0f) ? 0.9f : 1.1111112f), label: false);
			}
		}
	}

	public static void Camera(Camera cam, float dt, float camYaw, float camPitch)
	{
		FBCharacter player = FBMatch.Player;
		FBCar fBCar = FBVehicles.CarOf(player);
		if (!fBCar)
		{
			return;
		}
		K k = KindOf(fBCar);
		float y = fBCar.transform.eulerAngles.y;
		float num = Mathf.DeltaAngle(lastCarYaw, y);
		lastCarYaw = y;
		if ((bool)FBPlayer.I)
		{
			FBPlayer.I.camYaw += num;
		}
		Vector3 vector = Quaternion.Euler(0f - camPitch, camYaw + num, 0f) * Vector3.forward;
		if (FBVehicles.fp)
		{
			FpHead(player, on: true);
			FBDriverPose component = player.GetComponent<FBDriverPose>();
			if ((bool)component)
			{
				component.PoseNow();
			}
			Transform transform = (player.an ? player.an.GetBoneTransform(HumanBodyBones.Head) : null);
			cam.transform.position = (transform ? (transform.position + fBCar.transform.rotation * new Vector3(0f, 0.09f, 0.11f)) : fBCar.L2W(fBCar.seat + new Vector3(0f, (fBCar.kind == "moto") ? 0.95f : 0.62f, (fBCar.kind == "moto") ? 0.05f : 0.1f)));
			cam.transform.rotation = Quaternion.Euler(0f - camPitch, camYaw + num, (0f - fBCar.transform.eulerAngles.z) * 0f);
			cam.nearClipPlane = 0.05f;
			return;
		}
		FpHead(player, on: false);
		cam.nearClipPlane = 0.1f;
		if ((bool)fBCar.mx)
		{
			fBCar.mx.Camera(cam, dt, camPitch);
			return;
		}
		Vector3 vector2;
		float num2;
		if (fBCar.kind == "moto")
		{
			vector2 = fBCar.transform.position + Vector3.up * k.cam.y;
			num2 = k.cam.x;
		}
		else
		{
			Bounds lbox = fBCar.lbox;
			float scale = fBCar.scale;
			float num3 = lbox.size.z * scale;
			float num4 = lbox.size.y * scale;
			Vector3 vector3 = fBCar.transform.TransformPoint(lbox.center);
			vector2 = new Vector3(vector3.x, vector3.y + num4 * 0.5f + 0.5f, vector3.z);
			num2 = Mathf.Clamp(num3 * 0.95f + num4 * 0.85f + 0.6f, 4.6f, 15f);
		}
		camZoomS = Mathf.Lerp(camZoomS, CamZoom, 1f - Mathf.Exp((0f - dt) * 8f));
		float num5 = camZoomS;
		num2 = Mathf.Max((fBCar.kind == "moto") ? 1.4f : Mathf.Max(1.7f, fBCar.lbox.size.z * fBCar.scale * 0.42f), num2 * num5);
		vector2.y -= Mathf.Clamp01((1f - num5) / 0.66f) * ((fBCar.kind == "moto") ? 0.25f : 0.45f);
		num2 += Mathf.Abs(fBCar.v) * 0.025f * Mathf.Min(1f, num5 + 0.2f);
		Vector3 vector4 = -vector;
		fBCar.SetColliders(on: false);
		RaycastHit hit;
		float num6 = (FBWorld.RayHit(vector2, vector4, num2 + 0.3f, out hit) ? Mathf.Max(0.6f, hit.distance - 0.3f) : num2);
		fBCar.SetColliders(on: true);
		cam.transform.position = vector2 + vector4 * num6;
		cam.transform.rotation = Quaternion.LookRotation(vector2 + vector * 8f - cam.transform.position);
		if (shake > 0.01f)
		{
			cam.transform.position += UnityEngine.Random.insideUnitSphere * shake * 0.12f;
			shake = Mathf.Max(0f, shake - dt * 2.5f);
		}
		cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 70f + Mathf.Min(12f, Mathf.Abs(fBCar.v) * 0.35f), 1f - Mathf.Exp((0f - dt) * 4f));
	}

	public static List<object> VehicleState()
	{
		FBCar fBCar = cur;
		if (!fBCar || fBCar.syncId == null)
		{
			return null;
		}
		float[] array = Coords.ToWeb(fBCar.transform.position);
		if ((bool)fBCar.mx)
		{
			Vector3 eulerAngles = fBCar.transform.eulerAngles;
			return new List<object>
			{
				fBCar.syncId,
				R(array[0]),
				R(array[1]),
				R(array[2]),
				R(0f - W(eulerAngles.y)),
				R(W(eulerAngles.x)),
				R(0f - W(eulerAngles.z)),
				R(fBCar.mx.in_steering * 0.55f),
				R(fBCar.mx.in_acceleration),
				R(fBCar.v),
				0,
				R(fBCar.wheelie),
				FBDoors.Mask(fBCar),
				LightBits(fBCar)
			};
		}
		return new List<object>
		{
			fBCar.syncId,
			R(array[0]),
			R(array[1]),
			R(array[2]),
			R(fBCar.yaw),
			R(fBCar.pitch),
			R(fBCar.roll + fBCar.lean),
			R(fBCar.steer),
			R(fBCar.spin),
			R(fBCar.v),
			fBCar.wallOn ? 1 : 0,
			R((fBCar.kind == "moto") ? fBCar.wh.a : 0f),
			FBDoors.Mask(fBCar),
			LightBits(fBCar)
		};
		static float R(float x)
		{
			return Mathf.Round(x * 100f) / 100f;
		}
		static float W(float d)
		{
			return ((d > 180f) ? (d - 360f) : d) * ((float)Math.PI / 180f);
		}
	}

	public static int LightBits(FBCar car)
	{
		if (!car)
		{
			return 0;
		}
		int num = Mathf.Clamp(FBSiren.Mode(car), 0, 3) | (FBSiren.Lights(car) ? 4 : 0) | ((FBCarLights.headMode & 3) << 3);
		num |= FBCarLights.PlayerBlinkNow switch
		{
			1 => 2, 
			-1 => 1, 
			2 => 3, 
			_ => 0, 
		} << 5;
		if (FBCarLights.PlayerBrakeNow)
		{
			num |= 0x80;
		}
		return num;
	}

	private static void NetLights(FBCar car, int b)
	{
		car.netLights = b;
		NetLightsApplied++;
		if (FBSiren.IsPolice(car))
		{
			int num = b & 3;
			bool flag = (b & 4) != 0;
			if (FBSiren.Mode(car) != num)
			{
				FBSiren.Set(car, num);
			}
			if (FBSiren.Lights(car) != flag)
			{
				FBSiren.SetLights(car, flag);
			}
		}
	}

	public static bool ApplyRemote(FBCharacter actor, List<object> state, float dt, bool npc = false)
	{
		if (state == null || state.Count < 12)
		{
			Release(actor);
			return false;
		}
		if (!npc && !FBVehicles.Allowed)
		{
			Release(actor);
			return false;
		}
		FBCar fBCar = FBVehicles.BySyncId((state[0] as string) ?? state[0]?.ToString());
		if (!fBCar)
		{
			Release(actor);
			return false;
		}
		if (state.Count > 12)
		{
			FBDoors.SetMask(fBCar, Mathf.RoundToInt(Json.Fv(state[12])), sound: true);
		}
		if (state.Count > 13)
		{
			NetLights(fBCar, Mathf.RoundToInt(Json.Fv(state[13])));
		}
		int remoteDriver = actor.GetComponent<FBRemote>()?.netId ?? (-2);
		FBCar fBCar2 = FBVehicles.CarOf(actor);
		if ((bool)fBCar2 && fBCar2 != fBCar)
		{
			Release(actor);
		}
		if ((bool)fBCar.driver && fBCar.driver != actor)
		{
			Release(actor);
			return false;
		}
		bool num = !fBCar2;
		fBCar.remoteDriver = remoteDriver;
		fBCar.driver = actor;
		FBVehicles.SetDriving(actor, fBCar);
		if (num)
		{
			actor.StopDance(force: true);
			actor.vehiclePose = true;
			actor.sys = SitClip(fBCar);
			actor.SetWeaponVisible(v: false);
			actor.GetComponent<FBDriverPose>()?.Set(fBCar);
			actor.gameObject.GetOrAdd<FBDriverPose>().Set(fBCar);
			FBAudio.PlayAt((fBCar.kind == "moto") ? "ie_moto_start" : "veh_start", fBCar.transform.position, 0.35f, 5f);
		}
		fBCar.transform.SetPositionAndRotation(Coords.FromWeb(new List<object>
		{
			state[1],
			state[2],
			state[3]
		}), CarRot(F(5), F(4), F(6)));
		fBCar.yaw = F(4);
		fBCar.pitch = F(5);
		fBCar.v = F(9);
		fBCar.spin = F(8);
		fBCar.steerN = Mathf.Clamp(F(7) / 0.55f, -1f, 1f);
		if ((bool)fBCar.mx)
		{
			fBCar.wheelie = F(11);
			fBCar.mx.RemoteState(fBCar.v, F(7) / 0.55f, F(8), fBCar.wheelie, (dt <= 0f) ? (1f / 60f) : dt);
			return true;
		}
		if (fBCar.kind == "moto")
		{
			fBCar.wheelie = F(11);
			fBCar.trick = Mathf.Clamp01((fBCar.wheelie * 57.29578f - FBMoto.TRICK[0]) / 15f);
			fBCar.rider = new Rider
			{
				v = Mathf.Clamp01(Mathf.Abs(fBCar.v) / KIND["moto"].vmax),
				lean = F(6),
				a = fBCar.wheelie
			};
		}
		PoseWheels(fBCar, F(7));
		string text = "veh_remote_" + fBCar.syncId + "_";
		float num2 = Mathf.Min(1f, Mathf.Abs(fBCar.v) / KindOf(fBCar).vmax);
		object obj;
		if (!(fBCar.kind == "moto"))
		{
			FBCar fBCar3 = fBCar;
			obj = fBCar3.engineProf ?? (fBCar3.engineProf = FBCarSound.ProfileFor(fBCar.id, fBCar));
		}
		else
		{
			obj = null;
		}
		string text2 = (string)obj;
		if (text2 != null)
		{
			float num3 = ((dt <= 0f) ? (1f / 60f) : dt);
			float num4 = (fBCar.v - fBCar.vPrev) / num3;
			fBCar.vPrev = fBCar.v;
			float b = ((Mathf.Abs(fBCar.v) < 0.5f) ? 0f : Mathf.Clamp01((num4 > 0.4f) ? (0.55f + num4 / 6f) : ((num4 < -0.8f) ? 0f : 0.35f)));
			fBCar.gasEst = Mathf.Lerp(fBCar.gasEst, b, 1f - Mathf.Exp((0f - num3) / 0.25f));
			FBCar fBCar3 = fBCar;
			if (fBCar3.gb == null)
			{
				fBCar3.gb = new FBCarSound.Gearbox(TopV(fBCar));
			}
			(float, int, int) tuple = fBCar.gb.Step(num3, fBCar.v, fBCar.gasEst);
			FBCarSound.EngineAudio(text + "e_", text2, tuple.Item1, (fBCar.gb.shiftT > 0f) ? 0.15f : fBCar.gasEst, fBCar.transform.position, 0.32f, 5f);
		}
		else if (Json.O(fBCar.meta, "sound") != null)
		{
			float revRpm = fBCar.revRpm;
			fBCar.revRpm = fBCar.v;
			GbMotoAudio(fBCar, fBCar.v, fBCar.v > revRpm + 0.001f || (num2 > 0.2f && Mathf.Abs(fBCar.v - revRpm) < 0.001f), air: false, fBCar.wheelie > 0.08f, (dt <= 0f) ? (1f / 60f) : dt, null, fBCar.transform.position, text + "gb_");
		}
		else
		{
			var (name, value) = EngineSample(fBCar, Mathf.Clamp01(0.18f + num2 * 0.78f));
			FBAudio.SetLoop(text + "engine", name, 0.36f, fBCar.transform.position, value, 4f, 0.8f);
		}
		return true;
		float F(int i)
		{
			return Json.Fv(state[i]);
		}
	}

	public static void Release(FBCharacter actor)
	{
		FBCar fBCar = FBVehicles.CarOf(actor);
		if ((bool)fBCar && !(actor == FBMatch.Player))
		{
			string text = "veh_remote_" + fBCar.syncId + "_";
			FBCarSound.StopEngine(text + "e_");
			string[] array = new string[4] { "engine", "gb_par", "gb_ace", "gb_cut" };
			foreach (string text2 in array)
			{
				FBAudio.StopLoop(text + text2);
			}
			if ((bool)fBCar.mx)
			{
				fBCar.mx.RemoteStop();
			}
			fBCar.remoteDriver = -1;
			fBCar.driver = null;
			fBCar.netLights = -1;
			FBVehicles.SetDriving(actor, null);
			FBDriverPose component = actor.GetComponent<FBDriverPose>();
			if ((bool)component)
			{
				UnityEngine.Object.Destroy(component);
			}
			actor.vehiclePose = false;
			actor.sys = null;
		}
	}

	public static void Tick(float dt)
	{
	}

	private static void StartCoroutineSafe(this MonoBehaviour m, float t, Action a)
	{
		if ((bool)m)
		{
			m.StartCoroutine(Wait(t, a));
		}
	}

	private static IEnumerator Wait(float t, Action a)
	{
		yield return new WaitForSeconds(t);
		a();
	}
}
}
