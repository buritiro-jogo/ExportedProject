using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
[RequireComponent(typeof(CharacterController))]
public class FBPlayer : MonoBehaviour
{
	private static FBPlayer inst;

	[NonSerialized]
	public FBCharacter ch;

	[NonSerialized]
	public FBWeaponUser wu;

	[NonSerialized]
	public Camera cam;

	public bool firstPerson;

	private CharacterController cc;

	public float camYaw;

	public float camPitch;

	private float camYawS;

	private float camPitchS;

	private float vy;

	private float airT;

	private float fallTop;

	private float lastDamage = 99f;

	private float jumpQ = -9f;

	private float regen;

	private bool crouch;

	private bool prone;

	private bool autoRun;

	private bool jumpedAir;

	private bool inAir;

	private bool wasDriving;

	private FBCar lastCar;

	private FBCar holsterCar;

	private Vector3 hv;

	private float lean;

	private float tpsKick;

	private float tpsPunch;

	private float tpsShake;

	private float camShake;

	private float facing;

	private float tpsArm = 1.5f;

	private float tpsLat = 0.8f;

	private float pivY = 1.6f;

	private float lastMoveT;

	private (float t, float dur, Vector3 a, Vector3 b, float h, int snd, float yaw, float wall)? vault;

	private FBCharacter reviving;

	private FBCharacter reviveCand;

	private GameObject vm;

	private Transform vmMuzzle;

	private float vmKick;

	private float swayX;

	private float swayY;

	private float vmBob;

	private FBFpRig fp;

	private Camera vmCam;

	private bool wasInWater;

	private bool jumpedFlag;

	private bool onGround = true;

	private float slideV;

	private float stepLocal = -1f;

	private FBMotor.Ground ground;

	private bool flOn;

	private bool flLatch;

	private float flYaw;

	private float flPitch;

	private float flYawS;

	private float flPitchS;

	public static Func<FBInputState, FBInputState> TestInput;

	[NonSerialized]
	public Vector3 LastShotAim;

	[NonSerialized]
	public Vector3 LastShotHit;

	[NonSerialized]
	public int ShotCount;

	private float crossSpd = 36f;

	private float blkT;

	private float collD = -1f;

	private readonly float[] latClr = new float[2] { -1f, -1f };

	private float healEnd;

	private float fpHideT2;

	private bool fpHidden;

	private FBCharacter assistTgt;

	private float assistCool;

	private float assistPull;

	private float assistFric = 1f;

	private float ffFireT;

	public static int HealCancels;

	public static string LastHealCancel;

	private bool bodyVis = true;

	private float fpHideT;

	private float camOff;

	private float camOffV;

	private float camSlope;

	private Vector3 camPp;

	private bool camPpOk;

	public static FBPlayer I
	{
		get
		{
			if (!inst)
			{
				return null;
			}
			return inst;
		}
		set
		{
			inst = value;
		}
	}

	public static float CamYawRad
	{
		get
		{
			if (!I)
			{
				return 0f;
			}
			return I.camYaw * ((float)Math.PI / 180f);
		}
	}

	public bool OnGround => onGround;

	public FBMotor.Ground GroundInfo => ground;

	public bool FreeLooking => flOn;

	public float ViewYaw => camYawS + flYawS;

	private bool FlBlend
	{
		get
		{
			if (!(Mathf.Abs(flYawS) > 0.5f))
			{
				return Mathf.Abs(flPitchS) > 0.5f;
			}
			return true;
		}
	}

	private Vector3 AnchorFwd => Quaternion.Euler(0f - camPitchS, camYawS, 0f) * Vector3.forward;

	public bool AimBlocked { get; private set; }

	public float CrossGapPx => Mathf.Max(6f, crossSpd / 3.2f);

	public bool FirstPerson => firstPerson;

	public float FovBase => 55f + FBSession.fov * 40f;

	public bool Driving => FBVehicles.Driving(ch);

	private float WalkSp
	{
		get
		{
			if (!ch || !ch.IsPistol)
			{
				if (!ch || !ch.Hot)
				{
					return 4.2f;
				}
				return 3.4f;
			}
			if (!ch.Hot)
			{
				return 4.4f;
			}
			return 3.7f;
		}
	}

	private float M(string k, float d)
	{
		return GameData.Move(k, d);
	}

	private static float T(string k, float d)
	{
		return Json.F(GameData.Section("tpsCamera"), k, d);
	}

	private void Awake()
	{
		I = this;
		cc = GetComponent<CharacterController>();
		ch = GetComponent<FBCharacter>();
		wu = this.GetOrAdd<FBWeaponUser>();
		ch.isPlayer = true;
		ch.displayName = ((FBSession.nameChosen || FBSession.online) ? FBSession.name : "Você");
		cam = Camera.main;
		if (!cam)
		{
			cam = new GameObject("Main Camera").AddComponent<Camera>();
			cam.tag = "MainCamera";
		}
		if (!cam.GetComponent<AudioListener>() && !UnityEngine.Object.FindAnyObjectByType<AudioListener>())
		{
			cam.gameObject.AddComponent<AudioListener>();
		}
		cam.nearClipPlane = ((FBGfx.Tier <= FBGfxTier.Web) ? 0.1f : 0.05f);
		cam.farClipPlane = 1400f;
		cam.fieldOfView = FovBase;
		cam.useOcclusionCulling = false;
		FBQuality.SmoothLods(SceneManager.GetActiveScene());
		UniversalAdditionalCameraData universalAdditionalCameraData = cam.GetComponent<UniversalAdditionalCameraData>();
		if (!universalAdditionalCameraData)
		{
			universalAdditionalCameraData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
		}
		universalAdditionalCameraData.renderPostProcessing = true;
		universalAdditionalCameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
		firstPerson = false;
		// v18 (thread de física): uma exceção aqui (ex.: falta de memória ao ler as animações de primeira pessoa) parava o
		// Awake no meio e o jogador ficava sem controle. Agora o jogo segue em terceira pessoa.
		try
		{
			fp = FBFpRig.Create(null);
		}
		catch (Exception ex)
		{
			fp = null;
			Debug.LogWarning("[FreeBala][fp] braços de primeira pessoa não carregaram (" + ex.GetType().Name + ": " + ex.Message + "); seguindo em terceira pessoa");
		}
		if ((bool)fp)
		{
			fp.transform.SetParent(null);
			FBCharArms.Attach(fp, ch);
		}
		GameObject gameObject = new GameObject("CameraArma");
		gameObject.transform.SetParent(cam.transform, worldPositionStays: false);
		vmCam = gameObject.AddComponent<Camera>();
		vmCam.cullingMask = 16384;
		vmCam.nearClipPlane = 0.01f;
		vmCam.farClipPlane = 10f;
		vmCam.fieldOfView = Json.F(GameData.Section("character"), "fieldOfViewWeapon", 65f);
		gameObject.AddComponent<UniversalAdditionalCameraData>().renderType = CameraRenderType.Overlay;
		universalAdditionalCameraData.cameraStack.Add(vmCam);
		cam.cullingMask &= -16385;
		wu.onEquip = delegate
		{
			if ((bool)fp && wu.Cur != null)
			{
				fp.SetWeapon(wu.Cur);
			}
			if (firstPerson && !fp)
			{
				BuildViewModel();
			}
		};
		if (!FBInput.Touch && !FBGarage.Open_)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
		base.gameObject.AddComponent<FBTouch>();
	}

	public void TestJump()
	{
		jumpQ = Time.time;
	}

	public void OnRespawn(float yawDeg)
	{
		camYaw = (camYawS = yawDeg);
		camPitch = (camPitchS = 0f);
		prone = (crouch = false);
		vault = null;
		vy = 0f;
		hv = Vector3.zero;
		// fisica 31/10: teleporte de respawn/rodada nao e queda. Sem isto, quem estava no ar (pulo/queda) pousava na base
		// com o fallTop/airT do lugar antigo: baque ie_land e dano de queda na fase de compra.
		inAir = false;
		airT = 0f;
		jumpedAir = false;
		jumpQ = -9f;
		lastDamage = 99f;
		autoRun = false;
	}

	// Correção: zera o movimento a pé ao entrar e ao sair de veículo. Antes, a velocidade, o pulo, o vault
	// e o agachado de antes de entrar voltavam depois de sair (personagem deslizando, dano de queda, vault velho).
	public void ResetMotion()
	{
		vault = null;
		ch.VaultRate = 0f;
		vy = 0f;
		hv = Vector3.zero;
		slideV = 0f;
		inAir = false;
		airT = 0f;
		onGround = true;
		jumpedAir = false;
		jumpQ = -9f;
		prone = (crouch = false);
		if (autoRun)
		{
			autoRun = false;
			FBHUD.I?.SetRun(on: false);
		}
	}

	// Correção: ao sair do veículo a arma aparecia na mão já atravessando a lataria (porta aberta, tanque da moto).
	// Ela fica no coldre até o jogador se afastar do veículo e então é sacada; mirar ou atirar saca na hora.
	private bool HolsterNearCar(WeaponDef cur, FBInputState inp)
	{
		if (!holsterCar)
		{
			holsterCar = null;
			return false;
		}
		bool flag = inp.aim || inp.fire || inp.firePressed;
		if (cur == null || !ch.alive || flag || FarFrom(holsterCar, cur))
		{
			holsterCar = null;
			if (cur != null && ch.alive && !flag)
			{
				ch.DrawAnim();
			}
			return false;
		}
		return true;
	}

	private bool FarFrom(FBCar car, WeaponDef w)
	{
		Vector3 vector = ch.transform.position + Vector3.up * (1.2f * ch.ScaleK);
		Transform transform = car.transform;
		Vector3 vector2 = transform.TransformPoint(car.lbox.ClosestPoint(transform.InverseTransformPoint(vector)));
		return (vector2 - vector).Flat().magnitude > (w.IsPistol ? 0.55f : 1.1f) * ch.ScaleK;
	}

	public void OnKnocked()
	{
		prone = (crouch = false);
		vault = null;
		autoRun = false;
		FBMed.Cancel();
	}

	public void Trauma(float k)
	{
		if (!firstPerson)
		{
			tpsShake = Mathf.Min(1f, tpsShake + T("damageTraumaScale", 0.6f) * k);
		}
		else
		{
			camShake = Mathf.Max(camShake, Mathf.Min(1.2f, k * 2.2f));
		}
		lastDamage = 0f;
	}

	public void ForceThirdPerson()
	{
		firstPerson = false;
		ApplyView();
	}

	public void ForceFirstPerson()
	{
		firstPerson = true;
		ApplyView();
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBPlayer.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		float deltaTime = Time.deltaTime;
		if (deltaTime <= 0f || !FBMatch.I)
		{
			return;
		}
		FBInputState inp = FBInput.Read();
		if (TestInput != null)
		{
			inp = TestInput(inp);
		}
		if (FBMatch.Paused)
		{
			return;
		}
		flOn = false;
		FBMatch i = FBMatch.I;
		bool flag = i.state == "play";
		if (Driving)
		{
			wasDriving = true;
			lastCar = FBVehicles.CarOf(ch);
			if ((bool)fp && fp.gameObject.activeSelf)
			{
				fp.gameObject.SetActive(value: false);
			}
			FBVehicles.PlayerInput(ch, inp, deltaTime);
			Look(inp, deltaTime);
			return;
		}
		if (wasDriving)
		{
			wasDriving = false;
			ResetMotion();
			holsterCar = lastCar;
			lastCar = null;
		}
		if (FBBattleRoyale.Active(ch) && ch.alive && FBBattleRoyale.StepPlayer(ch, deltaTime, inp, camYaw))
		{
			Look(inp, deltaTime);
			ch.SetWeaponVisible(v: false);
			return;
		}
		WeaponDef cur = wu.Cur;
		bool holster = HolsterNearCar(cur, inp);
		bool flag2 = ch.alive && flag && !i.freeze && !ch.special && !FBFall.Busy(ch);
		if (FBKillstreak2.Tick(ch, deltaTime, Keyboard.current != null && Keyboard.current.gKey.isPressed))
		{
			Look(inp, deltaTime);
			Vector2 vector = wu.recoil.Step(deltaTime);
			camPitch += vector.x;
			camYaw += vector.y;
			ch.Yaw = camYaw;
			ch.aiming = inp.aim;
			if (cur != null && (cur.auto ? inp.fire : inp.firePressed) && wu.cool <= 0f)
			{
				Fire(inp.aim);
			}
			if (inp.reload)
			{
				StartReload();
			}
			return;
		}
		if (ch.getUpT > 0f)
		{
			ch.getUpT -= deltaTime;
		}
		ReviveStep(deltaTime, flag2, inp);
		Vector2 vector2 = inp.move;
		float num = vector2.magnitude;
		if (num > 1f)
		{
			vector2 /= num;
			num = 1f;
		}
		if (ch.dance != null && flag && (num > 0.1f || inp.fire || inp.aim || inp.jump))
		{
			ch.StopDance(num > 0.1f || inp.jump);
			FBNet.I?.SendEvent("stopdance");
		}
		bool flag3 = cur != null && inp.aim && ch.alive && wu.switching <= 0f && !ch.special && !ch.knocked && !reviving;
		if (FBSession.freeLookToggle && !FBInput.Touch)
		{
			if (inp.freeLookPressed)
			{
				flLatch = !flLatch;
			}
		}
		else
		{
			flLatch = false;
		}
		bool flag4 = (inp.freeLook && (!FBSession.freeLookToggle || FBInput.Touch)) || flLatch;
		if (flag3 || inp.fire || inp.firePressed || inp.aimPressed || inp.reload || !ch.alive || ch.knocked || ch.special || FBMed.Busy || !flag)
		{
			flag4 = false;
			flLatch = false;
		}
		flOn = flag4;
		if (!flOn)
		{
			flYaw = 0f;
			flPitch = 0f;
		}
		Look(inp, deltaTime);
		if (ch.alive && flag)
		{
			Vector2 vector3 = wu.recoil.Step(deltaTime);
			camPitch = Mathf.Clamp(camPitch + vector3.x, firstPerson ? (-86) : (-34), firstPerson ? 86 : 54);
			camYaw += vector3.y;
			AimAssist(deltaTime, flag3, inp);
			BlockedCheck(deltaTime);
		}
		else
		{
			wu.recoil.Reset();
			AimBlocked = false;
		}
		if (FBMed.Busy)
		{
			HealGuard(in inp);
		}
		if (flag && ch.alive)
		{
			if (inp.sprintToggle)
			{
				autoRun = !autoRun;
				FBHUD.I?.SetRun(autoRun);
			}
			if (inp.crouchToggle)
			{
				// fisica 31/10: testa o teto so quando a capsula vai crescer (agachado -> em pe, ou deitado -> agachado); descer continua livre
				float alvoH = (crouch ? Json.F(GameData.Section("controller"), "m_Height", 1.8f) : M("crouchHeight", 1.4f));
				if ((crouch || prone) && SemEspaco((alvoH + 0.05f) * ch.ScaleK))
				{
					FBHUD.I?.Center("SEM ESPAÇO PARA LEVANTAR", 1f);
				}
				else
				{
					crouch = !crouch;
					prone = false;
					ch.poseT = Time.time;
					FBAudio.Play2D("ie_crouch", 0.3f);
				}
			}
			if (inp.prone)
			{
				SetProne(!prone);
			}
			if (inp.reload)
			{
				StartReload();
			}
			if (inp.heal)
			{
				TryHeal();
			}
			if (inp.pickup)
			{
				FBLoot.Pickup(ch);
			}
			if (inp.slot >= 0)
			{
				if (inp.slot == 4)
				{
					i.ks.Consume(Time.time);
				}
				else
				{
					wu.SwitchTo(inp.slot);
				}
			}
			if (inp.cycle != 0)
			{
				wu.Cycle(inp.cycle);
			}
			if (inp.inspect && cur != null)
			{
				ch.Trigger("inspect");
				if ((bool)fp && firstPerson && wu.reloadT <= 0f)
				{
					fp.Inspect();
				}
			}
			if (inp.toggleView && !ch.special && FBSession.Lab)
			{
				firstPerson = !firstPerson;
				ApplyView();
			}
			if (inp.interact && !reviveCand && !ch.knocked)
			{
				FBVehicles.Toggle(ch);
				if (Driving)
				{
					// Correção: entrou no veículo; o resto do Update a pé apagava o clipe de entrar (ch.sys)
					// e podia religar o colisor do jogador sentado.
					ResetMotion();
					wasDriving = true;
					return;
				}
			}
			if (inp.buy)
			{
				if (FBEconomy.buying)
				{
					FBShop.Show(!FBShop.Open);
				}
				else
				{
					FBEmoteWheel.Open();
				}
			}
			if (inp.buyReleased)
			{
				FBEmoteWheel.Close(apply: true);
			}
			if (inp.arsenal && i.mode != null && i.mode.arsenal)
			{
				FBEconomy.buying = true;
				FBShop.Show(!FBShop.Open);
			}
			if (inp.bag)
			{
				FBBag.Toggle();
			}
			if (inp.mark)
			{
				FBMarks.Mark(cam);
			}
			if (inp.multimidia)
			{
				FBMultimidia.Toggle();
			}
			if (inp.gps)
			{
				if (i.mode != null && i.mode.role)
				{
					FBGps.ToggleBig();
				}
				else
				{
					FBKillstreak2.Call();
				}
			}
		}
		if (inp.jump)
		{
			jumpQ = Time.time;
		}
		if (autoRun && (!flag2 || flag3 || inp.fire || crouch || inp.move.y < 0.3f || Mathf.Abs(inp.move.x) > 0.6f) && (!FBInput.Touch || !(inp.move.y > 0.3f)))
		{
			autoRun = false;
			FBHUD.I?.SetRun(on: false);
		}
		if (autoRun && !FBInput.Touch && inp.move.sqrMagnitude < 0.01f)
		{
			vector2 = new Vector2(0f, 1f);
		}
		bool flag5 = (autoRun || (FBSession.holdRun && inp.sprintHeld)) && vector2.y > 0f && !flag3 && !crouch && !inp.fire && !prone && !ch.knocked && (cur == null || !(wu.reloadT > 0f) || wu.Ammo >= cur.mag);
		float num2 = ((!flag5) ? 1f : (cur?.move ?? 1.15f));
		bool flag6 = crouch && !prone && inp.sprintHeld && vector2.y > 0.3f && !flag3 && !inp.fire && !ch.knocked;
		ch.CrouchFast = flag6;
		float num3 = (ch.knocked ? 1.5f : (((!crouch) ? (flag3 ? M("speedAiming", 3f) : (flag5 ? M("speedRunning", 7.4f) : (ch.UnarmedLocomotion ? 2.2f : WalkSp))) : (flag6 ? 4.4f : M("speedCrouching", 3f))) * num2 * (FBMed.Busy ? 0.6f : 1f) * (prone ? (1.1f / Mathf.Max(1f, M("speedWalking", 5f))) : 1f)));
		if (cur != null)
		{
			num3 *= FBWeaponModel.ModsOf(cur).move;
		}
		float num4 = M("walkingMultiplierSideways", 0.9f);
		float num5 = ((vector2.y >= 0f) ? M("walkingMultiplierForward", 1f) : M("walkingMultiplierBackwards", 0.9f));
		Quaternion quaternion = Quaternion.Euler(0f, camYaw, 0f);
		Vector3 b = ((flag2 && !reviving) ? (quaternion * new Vector3(vector2.x * num3 * num4, 0f, vector2.y * num3 * num5)) : Vector3.zero);
		b *= ch.ScaleK;
		float? num6 = FBWorld.WaterAt(base.transform.position.x, base.transform.position.z);
		bool flag7 = num6.HasValue && base.transform.position.y < num6 + 0.15f;
		if (flag7)
		{
			b *= 0.62f;
		}
		if (flag7 && !wasInWater && !vault.HasValue && vy < -2.5f && airT > 0.3f)
		{
			FBAudio.PlayAt("pulo_agua", base.transform.position, 0.8f, 8f);
			FBFx.Splash(new Vector3(base.transform.position.x, num6.Value, base.transform.position.z), 1.6f);
		}
		wasInWater = flag7;
		float num7 = ((!onGround) ? M("accelerationInAir", 1.7f) : ((b.magnitude > hv.magnitude) ? M("acceleration", 12f) : M("deceleration", 11f)));
		hv = Vector3.Lerp(hv, b, 1f - Mathf.Exp((0f - deltaTime) * num7));
		bool flag8 = false;
		if (inp.jump && flag2 && prone)
		{
			SetProne(on: false);
		}
		else if (inp.jump && flag2 && !vault.HasValue && TryVault())
		{
			jumpQ = -9f;
		}
		if (onGround)
		{
			jumpedAir = false;
		}
		bool num8 = Time.time - jumpQ <= 0.12f;
		bool flag9 = !onGround && !jumpedAir && airT <= 0.12f && vy <= 0f;
		if (num8 && (onGround || flag9) && !crouch && !prone && flag2 && !vault.HasValue && !ch.knocked && !reviving)
		{
			vy = Mathf.Sqrt(2f * M("jumpGravity", 35f) * M("jumpForce", 1.5f)) * Mathf.Sqrt(ch.ScaleK);
			jumpedAir = true;
			jumpQ = -9f;
			flag8 = true;
			FBAudio.Play2D("ie_jump", 0.4f, 0.1f);
			FBAudio.PlayAt(((FBVoice.SexOf(ch) == "f") ? "f" : "m") + "_pulo", ch.Pos + Vector3.up * 1.6f, 0.55f, 6f);
		}
		float magnitude = hv.magnitude;
		ch.sprint = flag5 && magnitude > Mathf.Max(0.2f, num3 * 0.22f);
		ch.crouch = (crouch || prone || ch.knocked || (bool)reviving) && ch.alive;
		ch.prone = (prone || ch.knocked) && ch.alive;
		if (i.mode != null && i.mode.econ && cur == null && ch.alive && onGround && magnitude < 0.18f && !flag3 && !inp.fire && !crouch && !prone)
		{
			ch.PlayWaitPose("FF_Emote_supercar_Male_Entrance_Idle01");
		}
		else
		{
			ch.StopWaitPose();
		}
		string text = ch.RescuePose((bool)reviving && ch.alive && !ch.knocked, deltaTime);
		ch.sys = ((!ch.alive) ? null : (ch.knocked ? FBCombat.KoPose(ch, magnitude > 0.4f) : ((ch.getUpT > 0f) ? FBCombat.GetUpClip : (HealPose() ?? text ?? (vault.HasValue ? Has("FF_SYS_Vault") : ((!prone) ? null : ((magnitude > 0.4f && Has("FF_SYS_Crawl") != null) ? "FF_SYS_Crawl" : "FF_SYS_Prone")))))));
		ch.shooting = ch.lastShot < Json.F(GameData.Section("puppet"), "shootingFlagDuration", 0.15f);
		ch.aiming = flag3;
		ch.combat = cur != null && !ch.knocked;
		lean = Mathf.Lerp(lean, (flag2 && !flag5 && !prone && !ch.knocked) ? ((inp.leanR ? 1 : 0) - (inp.leanL ? 1 : 0)) : 0, 1f - Mathf.Exp((0f - deltaTime) * 10f));
		float num9 = (vault.HasValue ? 0.95f : ((prone || ch.knocked) ? 0.7f : (crouch ? M("crouchHeight", 1.4f) : Json.F(GameData.Section("controller"), "m_Height", 1.8f))));
		float num10 = 1f / base.transform.lossyScale.y;
		cc.height = Mathf.Lerp(cc.height, num9 * num10, 1f - Mathf.Exp((0f - deltaTime) * M("crouchTransitionSpeed", 9f)));
		cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
		if (vault.HasValue)
		{
			VaultStep(deltaTime);
		}
		else
		{
			vy = ((onGround && vy < 0f) ? (-2f) : (vy - M("gravity", 35f) * Mathf.Sqrt(ch.ScaleK) * deltaTime));
			if (stepLocal < 0f)
			{
				stepLocal = cc.stepOffset;
			}
			if ((FBMotor.Move(cc, (hv + Vector3.up * vy) * deltaTime, onGround, vy > 0f, stepLocal, 0.45f * ch.ScaleK, ref slideV, out onGround, out ground) & CollisionFlags.Above) != 0 && vy > 0f)
			{
				vy = 0f;
			}
			if (ground.ok && ground.steep && !onGround)
			{
				vy = Mathf.Max(vy, -6f);
			}
		}
		if (!onGround)
		{
			if (!inAir)
			{
				inAir = true;
				fallTop = base.transform.position.y;
			}
			else if (base.transform.position.y > fallTop)
			{
				fallTop = base.transform.position.y;
			}
		}
		else if (inAir)
		{
			inAir = false;
			float dist = (fallTop - base.transform.position.y) / ch.ScaleK;
			bool hasValue = FBWorld.WaterAt(base.transform.position.x, base.transform.position.z).HasValue;
			float num11 = ((ch.alive && !hasValue && flag) ? FBMechanics.FallDamage(dist, airT) : 0f);
			if (num11 > 0f)
			{
				FBCombat.DamageActor(ch, num11, head: false, null, "fall");
				FBAudio.Play2D("ie_bodyhit", 0.7f);
				camShake = Mathf.Max(camShake, Mathf.Min(1f, num11 / 60f));
			}
			if (airT > 0.32f && ch.alive)
			{
				FBAudio.Play2D("ie_land", Mathf.Max(Mathf.Min(0.55f, 0.18f + airT * 0.35f), FBMechanics.LandVolume(dist) * 0.8f), 0.1f);
			}
			if (airT > 0.5f)
			{
				tpsShake = Mathf.Min(1f, tpsShake + T("landTraumaAmount", 0.45f) * Mathf.InverseLerp(T("landTraumaMinSpeed", 5f), T("landTraumaMaxSpeed", 16f), Mathf.Abs(vy)));
			}
		}
		airT = (onGround ? 0f : (airT + deltaTime));
		if (flag7 && magnitude > 0.6f)
		{
			regen -= deltaTime;
			if (regen <= 0f)
			{
				FBFx.Splash(new Vector3(base.transform.position.x, num6.Value, base.transform.position.z), 0.55f);
				regen = 0.26f;
			}
		}
		ch.velocity = new Vector3(hv.x, vy, hv.z);
		ch.grounded = onGround;
		if (ch.alive && !ch.special && ch.dance == null)
		{
			if (firstPerson || ch.knocked)
			{
				ch.Yaw = camYaw;
			}
			else
			{
				if (i.mode == null || !i.mode.role || cur != null)
				{
					facing = camYaw;
				}
				else if (magnitude > 0.75f)
				{
					facing = Mathf.Atan2(hv.x, hv.z) * 57.29578f;
				}
				float num12 = ((flag3 || inp.fire || ch.lastShot < T("shootingFacingHold", 0.35f)) ? T("aimFacingSmoothTime", 0.04f) : T("freeFacingSmoothTime", 0.14f));
				ch.Yaw = Mathf.LerpAngle(ch.Yaw, facing, 1f - Mathf.Exp((0f - deltaTime) / num12 * 2.3f));
			}
			ch.hasAim = true;
			ch.aimPoint = (firstPerson ? (cam.transform.position + cam.transform.forward * 50f) : RigAimPoint());
		}
		Vector3 vector4 = Quaternion.Inverse(Quaternion.Euler(0f, ch.Yaw, 0f)) * hv / Mathf.Max(0.01f, ch.ScaleK);
		float num13 = RemapSpeed(magnitude / Mathf.Max(0.01f, ch.ScaleK));
		float magnitude2 = new Vector2(vector4.x, vector4.z).magnitude;
		ch.animVel = ((magnitude2 > 0.001f) ? new Vector2(vector4.x / magnitude2 * num13, vector4.z / magnitude2 * num13) : Vector2.zero);
		ch.lean = lean;
		if (Mathf.Abs(lean) > 0.02f)
		{
			Vector3 dir = base.transform.right * Mathf.Sign(lean);
			float num14 = 0.25f;
			float[] array = new float[3] { 0.5f, 1.2f, 1.7f };
			foreach (float num15 in array)
			{
				if (FBWorld.RayHit(base.transform.position + Vector3.up * num15 * ch.ScaleK, dir, 0.55f, out var hit))
				{
					num14 = Mathf.Min(num14, Mathf.Max(0f, hit.distance - 0.32f));
				}
			}
			ch.peekRoom = num14;
		}
		else
		{
			ch.peekRoom = 0.25f;
		}
		if (FBMed.Busy)
		{
			string text2 = ((!flag2) ? "" : (ch.sprint ? "CORREU" : ((inp.fire || inp.firePressed) ? "ATIROU" : (flag8 ? "PULOU" : (FBShop.Open ? "LOJA" : ((hv.Flat().magnitude > 0.45f * ch.ScaleK) ? "SE MOVEU" : null))))));
			if (text2 != null)
			{
				CancelHeal(text2);
			}
		}
		if (cur != null)
		{
			if (wu.Ammo == 0 && wu.Reserve > 0 && wu.reloadT <= 0f && wu.switching <= 0f)
			{
				regen += deltaTime;
				if (regen > 0.25f)
				{
					regen = 0f;
					StartReload();
				}
			}
			bool flag10 = (cur.auto ? inp.fire : inp.firePressed);
			if (flag2 && flag10 && wu.cool <= 0f && !ch.sprint && !vault.HasValue && !ch.knocked && !reviving && !FBMed.Busy)
			{
				Fire(flag3);
			}
		}
		lastDamage += deltaTime;
		crossSpd = 36f + magnitude / Mathf.Max(0.01f, ch.ScaleK) * 7f + (float)((cur != null && cur.pellets > 1) ? 30 : 0) + (float)((!onGround) ? 30 : 0) + (float)((ch.lastShot < 0.15f) ? 10 : 0) + wu.recoil.bloom * 14f;
		float num16 = ((FBSession.mode == "treino") ? 100f : 40f);
		float num17 = Json.F(GameData.Section("target"), "regenDelaySeconds", 5f);
		float num18 = Json.F(GameData.Section("target"), "regenHpPerSecond", 5f);
		if (onGround && hv.Flat().magnitude < 0.3f * ch.ScaleK && !ch.shooting && !ch.vehiclePose && lastDamage > num17 && ch.alive && !ch.knocked && !FBEconomy.EconMode(FBSession.mode) && (i.mode == null || !i.mode.br) && ch.hp < num16)
		{
			ch.hp = Mathf.Min(num16, ch.hp + deltaTime * num18);
		}
		if (FBMed.Update(deltaTime, delegate(float v)
		{
			if (ch.alive)
			{
				ch.hp = Mathf.Min(ch.maxHp, ch.hp + v);
			}
		}) == "done")
		{
			FBAudio.Synth("heal");
			healEnd = 0.35f;
			if (cur != null && ch.alive)
			{
				wu.Equip(wu.cur);
			}
		}
		if (healEnd > 0f)
		{
			healEnd -= deltaTime;
		}
		bool flag11 = FBMed.Busy || healEnd > 0f || ch.Rescuing;
		ch.SetWeaponVisible(!flag11 && !holster && ch.dance == null && !ch.knocked && !ch.UnarmedLocomotion && !ch.vehiclePose);
		ch.StowCurrent((flag11 || holster) && cur != null && ch.alive);
		FBHUD.I?.SetMed(FBMed.kits, FBMed.inf, FBMed.Progress, FBMed.Healing);
		FBLoot.Update(deltaTime, ch);
		bool flag12 = firstPerson && ch.alive && !ch.special && !ch.vehiclePose && (bool)fp;
		if ((bool)fp)
		{
			fp.gameObject.SetActive(flag12 && cur != null && !FBMed.Busy && healEnd <= 0f && !ch.knocked && !reviving);
		}
		if (flag12 && fp.gameObject.activeSelf)
		{
			fp.transform.SetPositionAndRotation(base.transform.position, Quaternion.Euler(0f, ch.Yaw, 0f));
			fp.transform.localScale = Vector3.one * ch.ScaleK;
			float num19 = ((crouch || prone) ? M("speedCrouching", 3f) : (flag3 ? M("speedAiming", 3f) : (flag5 ? M("speedRunning", 7.4f) : (ch.UnarmedLocomotion ? 2.2f : WalkSp))));
			fp.Tick(deltaTime, new FBFpRig.St
			{
				aiming = flag3,
				axisX = vector2.x,
				axisY = vector2.y,
				moving = (magnitude > 0.5f),
				lookX = inp.look.x * 3f,
				running = ch.sprint,
				crouching = (crouch || prone),
				lean = lean,
				pitch = camPitchS,
				height = cc.height * base.transform.lossyScale.y / ch.ScaleK,
				jumped = flag8,
				inAir = (!onGround && airT > 0.1f),
				movementRate = Mathf.Clamp(num3 / Mathf.Max(0.01f, num19 * ch.ScaleK) * (flag7 ? 0.62f : 1f), 0.4f, 1.18f)
			});
		}
		bool flag13 = (bool)fp && fp.gameObject.activeSelf && (FBScopes.Scoped || Mathf.Abs(flYawS) >= 12f);
		if ((bool)fp && (flag13 != fpHidden || (flag13 && (fpHideT2 -= deltaTime) <= 0f)))
		{
			fpHidden = flag13;
			fpHideT2 = 0.25f;
			Renderer[] componentsInChildren = fp.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				componentsInChildren[j].forceRenderingOff = flag13;
			}
		}
	}

	private string HealPose()
	{
		if (FBMed.Busy)
		{
			if (!crouch || prone)
			{
				if (!(FBMed.t < 0.35f))
				{
					return Has("FF_SYS_Heal_Loop");
				}
				return Has("FF_SYS_Heal_Start");
			}
			return Has("FF_SYS_KO_Revive") ?? Has("FF_SYS_Heal_Loop");
		}
		if (healEnd > 0f && !crouch)
		{
			return Has("FF_SYS_Heal_End");
		}
		return null;
	}

	private static string Has(string c)
	{
		if (!FBRegistry.I || !FBRegistry.I.Clip(c))
		{
			return null;
		}
		return c;
	}

	private float RemapSpeed(float s)
	{
		Dictionary<string, object> o = GameData.Section("puppet");
		float num = Json.F(o, "playerWalkSpeed", 4f);
		float num2 = Json.F(o, "playerRunSpeed", 6.8f);
		float num3 = Json.F(o, "aiWalkAnimSpeed", 2f);
		float num4 = Json.F(o, "aiRunAnimSpeed", 5f);
		if (!(s <= num))
		{
			return num3 + (s - num) / (num2 - num) * (num4 - num3);
		}
		return s / num * num3;
	}

	private void Look(FBInputState inp, float dt)
	{
		WeaponDef cur = wu.Cur;
		float num = ((inp.aim && cur != null) ? (Mathf.Min(Json.F(FBWeaponModel.Scope(cur), "multiplierMouseSensitivity", 0.8f), 1.15f / FBScopes.Zoom(cur)) * (0.4f + FBInput.AdsSens * 1.2f)) : 1f);
		float num2 = inp.look.x * num * assistFric;
		float num3 = inp.look.y * num * assistFric;
		if (flOn)
		{
			float num4 = (firstPerson ? 105 : 178);
			float min = (float)(firstPerson ? (-86) : (-34)) - camPitch;
			float max = (float)(firstPerson ? 86 : 54) - camPitch;
			flYaw = Mathf.Clamp(flYaw + num2, 0f - num4, num4);
			flPitch = Mathf.Clamp(flPitch + num3, min, max);
		}
		else
		{
			camYaw += num2;
			camPitch = Mathf.Clamp(camPitch + num3, firstPerson ? (-86) : (-34), firstPerson ? 86 : 54);
			assistPull += num2;
			wu.recoil.Look(num2, num3);
			swayX = Mathf.Lerp(swayX, inp.look.x, 1f - Mathf.Exp((0f - dt) * 8f));
			swayY = Mathf.Lerp(swayY, inp.look.y, 1f - Mathf.Exp((0f - dt) * 8f));
		}
	}

	private void Fire(bool aiming)
	{
		WeaponDef cur = wu.Cur;
		FBWeaponModel.Mods mods = FBWeaponModel.ModsOf(cur, null, crouch || prone);
		FBRecoil.State state = default(FBRecoil.State);
		state.aiming = aiming;
		state.crouch = crouch;
		state.moving = hv.magnitude > 0.5f;
		state.air = !onGround;
		state.zoom = (aiming ? FBScopes.Zoom(cur) : 1f);
		state.grip = 1f;
		state.recV = mods.recV;
		state.recH = mods.recH;
		state.spreadHip = mods.spreadHip;
		state.spreadAds = mods.spreadAds;
		FBRecoil.State st = state;
		Vector3 position = cam.transform.position;
		Vector3 vector = (FlBlend ? AnchorFwd : cam.transform.forward);
		bool flag;
		if (firstPerson)
		{
			flag = wu.Fire(position, vector, st, out var hitPoint, 0f, ((bool)fp && fp.gameObject.activeSelf) ? new Vector3?(position + vector * (aiming ? 1.2f : 0.9f) + cam.transform.right * (aiming ? 0f : 0.12f) - Vector3.up * (aiming ? 0.03f : 0.1f)) : (vmMuzzle ? new Vector3?(vmMuzzle.position) : null));
			if (flag)
			{
				LastShotAim = position + vector * Vector3.Distance(position, hitPoint);
				LastShotHit = hitPoint;
			}
			if (flag && (bool)fp)
			{
				fp.Fire(empty: false, cur.auto);
			}
		}
		else
		{
			float near = Near(position);
			Vector3 value = ShotOrigin();
			Vector3? aimP = null;
			flag = wu.Fire(position, vector, st, out var hitPoint2, 0f, null, near, delegate(Vector3 oo, Vector3 dd)
			{
				Vector3 vector2 = AimPoint(oo, dd, near, 300f);
				Vector3 valueOrDefault = aimP.GetValueOrDefault();
				if (!aimP.HasValue)
				{
					valueOrDefault = vector2;
					aimP = valueOrDefault;
				}
				return vector2;
			}, value);
			if (flag)
			{
				LastShotAim = aimP ?? (position + vector * 300f);
				LastShotHit = hitPoint2;
			}
		}
		if (flag)
		{
			ShotCount++;
			bool flag2 = cur.cls == "escopeta" || cur.cls == "sniper";
			camShake = Mathf.Min(1.5f, camShake + (flag2 ? 0.45f : 0.1f));
			tpsKick = Mathf.Min(tpsKick + (0.7f + wu.recoil.lastKick * 0.9f) * (FBScopes.Scoped ? 0.5f : 1f), 7f);
			if (!firstPerson)
			{
				tpsPunch = Mathf.Min(0.3f, tpsPunch + (flag2 ? 0.16f : 0.05f));
			}
			vmKick = Mathf.Min(1f, vmKick + (cur.IsPistol ? 0.7f : 0.35f));
			if (FBScopes.Scoped)
			{
				FBHUD.I?.ScopeKick();
			}
			FBNet.I?.SendShot(ch);
		}
	}

	private Vector3 AimPoint(Vector3 o, Vector3 d, float near, float far)
	{
		RaycastHit[] array = Physics.RaycastAll(o, d, far, FBWorld.ShotMask, QueryTriggerInteraction.Collide);
		float num = far;
		RaycastHit[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			RaycastHit raycastHit = array2[i];
			if (raycastHit.distance >= num)
			{
				continue;
			}
			FBHitbox component = raycastHit.collider.GetComponent<FBHitbox>();
			if ((bool)component)
			{
				if (!(component.owner == ch) && component.owner.alive && (component.owner.civ || FBMatch.Hostile(component.owner, ch)))
				{
					num = raycastHit.distance;
				}
			}
			else if ((bool)raycastHit.collider.GetComponentInParent<FBTarget>())
			{
				num = raycastHit.distance;
			}
			else if (!raycastHit.collider.isTrigger && raycastHit.collider.gameObject.layer != 12 && !(raycastHit.collider.GetComponentInParent<FBCharacter>() == ch) && raycastHit.distance >= near)
			{
				num = raycastHit.distance;
			}
		}
		return o + d * num;
	}

	private float Near(Vector3 camPos)
	{
		return Mathf.Max(0f, (camPos - ch.Pos).magnitude - 1.2f * ch.ScaleK);
	}

	private Vector3 ShotOrigin()
	{
		Vector3 muzzlePos = ch.MuzzlePos;
		Vector3 vector = (ch.Head ? ch.Head.position : (ch.Pos + Vector3.up * 1.6f * ch.ScaleK));
		Vector3 vector2 = muzzlePos - vector;
		float magnitude = vector2.magnitude;
		if (!(magnitude > 0.01f) || !FBWorld.RayHit(vector, vector2 / magnitude, magnitude))
		{
			return muzzlePos;
		}
		return vector;
	}

	private Vector3 RigAimPoint()
	{
		Vector3 position = cam.transform.position;
		Vector3 vector = ((flOn || FlBlend) ? AnchorFwd : cam.transform.forward);
		if (!FBWorld.RayHit(position, vector, 250f, out var hit) || !(hit.distance >= 3.5f))
		{
			return position + vector * 120f;
		}
		return hit.point;
	}

	private void BlockedCheck(float dt)
	{
		if (firstPerson || wu.Cur == null || !ch.alive)
		{
			AimBlocked = false;
			return;
		}
		blkT -= dt;
		if (!(blkT > 0f))
		{
			blkT = 0.05f;
			Vector3 position = cam.transform.position;
			Vector3 vector = AimPoint(position, FlBlend ? AnchorFwd : cam.transform.forward, Near(position), 300f);
			Vector3 vector2 = ShotOrigin();
			Vector3 vector3 = vector - vector2;
			float magnitude = vector3.magnitude;
			AimBlocked = magnitude > 1.5f && FBWorld.RayHit(vector2, vector3 / magnitude, magnitude - 0.25f);
		}
	}

	public static float ZoomFov(float fov, float z)
	{
		if (!(z <= 1f))
		{
			return 2f * Mathf.Atan(Mathf.Tan(fov * 0.5f * ((float)Math.PI / 180f)) / z) * 57.29578f;
		}
		return fov;
	}

	private void AimAssist(float dt, bool aiming, FBInputState inp)
	{
		assistFric = 1f;
		float num = assistPull;
		assistPull = 0f;
		assistCool = Mathf.Max(0f, assistCool - dt);
		if (FBSession.aimAssist == "manual" || wu.Cur == null || wu.Cur.IsSniper || (!aiming && !inp.fire) || !ch.alive || ch.special || FBMatch.I.freeze)
		{
			assistTgt = null;
			return;
		}
		if (FBSession.aimAssist == "ff")
		{
			FFAssist(dt, aiming, inp);
			return;
		}
		Vector3 position = cam.transform.position;
		(float, float, float, FBCharacter)? tuple = (assistTgt ? Probe(assistTgt, position, aiming, 1.6f) : null);
		if ((bool)assistTgt && (!tuple.HasValue || num * Mathf.Sign(tuple.Value.Item1) < -0.68754935f))
		{
			assistTgt = null;
			tuple = null;
			assistCool = 0.6f;
		}
		if (!tuple.HasValue)
		{
			if (assistCool > 0f)
			{
				return;
			}
			float num2 = 1E+09f;
			foreach (FBCharacter item in FBMatch.Actors())
			{
				(float, float, float, FBCharacter)? tuple2 = Probe(item, position, aiming, 1f);
				if (tuple2.HasValue && tuple2.Value.Item3 < num2)
				{
					num2 = tuple2.Value.Item3;
					tuple = tuple2;
				}
			}
			if (!tuple.HasValue)
			{
				return;
			}
			assistTgt = tuple.Value.Item4;
		}
		(float, float, float, FBCharacter) value = tuple.Value;
		float num3 = (float)(aiming ? 22 : 12) * dt;
		float num4 = Mathf.Min(1f, dt * (float)(aiming ? 6 : 4));
		camYaw += Mathf.Clamp(value.Item1 * num4 * 57.29578f, 0f - num3, num3);
		if (value.Item2 > 0f)
		{
			camPitch += Mathf.Clamp(value.Item2 * num4 * 57.29578f, 0f - num3, num3);
		}
		else
		{
			Vector3 vector = cam.transform.InverseTransformPoint(value.Item4.Head.position + Vector3.up * 0.12f);
			float num5 = (float)((Mathf.Atan2(vector.y, new Vector2(vector.x, vector.z).magnitude) < 0f) ? 9 : 5) * dt;
			camPitch += Mathf.Max(value.Item2 * num4 * 0.35f * 57.29578f, 0f - num5);
		}
		assistFric = 0.8f;
	}

	private (float ex, float ey, float d, FBCharacter a)? Probe(FBCharacter a, Vector3 co, bool aiming, float coneK)
	{
		if (!a || a == ch || !a.alive || !FBMatch.Hostile(a, ch) || !a.Head)
		{
			return null;
		}
		Vector3 vector = Vector3.Lerp(a.Pos, a.Head.position, 0.72f);
		Vector3 vector2 = cam.transform.InverseTransformPoint(vector);
		if (vector2.z < 1f)
		{
			return null;
		}
		float z = vector2.z;
		if (z > 90f)
		{
			return null;
		}
		float num = Mathf.Atan2(vector2.x, z);
		float num2 = Mathf.Atan2(vector2.y, new Vector2(vector2.x, z).magnitude);
		float num3 = ((aiming ? 2.2f : 1.4f) * ((float)Math.PI / 180f) + Mathf.Atan(0.3f / z)) * coneK;
		float magnitude = new Vector2(num, num2).magnitude;
		if (magnitude > num3)
		{
			return null;
		}
		Vector3 vector3 = vector - co;
		if (FBWorld.RayHit(co, vector3.normalized, vector3.magnitude - 0.4f))
		{
			return null;
		}
		return (num, num2, magnitude, a);
	}

	private void FFAssist(float dt, bool aiming, FBInputState inp)
	{
		Vector3 position = cam.transform.position;
		FBCharacter fBCharacter = null;
		float num = -1f;
		foreach (FBCharacter item in FBMatch.Actors())
		{
			if (!item || item == ch || !item.alive || !FBMatch.Hostile(item, ch))
			{
				continue;
			}
			Vector3 vector = Vector3.Lerp(item.Pos, item.Head.position, 0.55f);
			Vector3 vector2 = cam.WorldToViewportPoint(vector);
			if (!(vector2.z < 0.6f) && !(vector2.z > 80f) && !(Mathf.Abs(vector2.x - 0.5f) > 0.12f) && !(Mathf.Abs(vector2.y - 0.5f) > 0.16f) && !FBWorld.RayHit(position, (vector - position).normalized, (vector - position).magnitude - 0.4f))
			{
				float num2 = 1f / (1f + vector2.z * 0.02f) - Vector3.Angle(cam.transform.forward, vector - position) * 0.01f;
				if (num2 > num)
				{
					num = num2;
					fBCharacter = item;
				}
			}
		}
		if (fBCharacter != assistTgt)
		{
			ffFireT = 0f;
		}
		assistTgt = fBCharacter;
		if (!fBCharacter)
		{
			ffFireT = 0f;
			return;
		}
		if (aiming)
		{
			assistFric = 0.65f;
		}
		if (!inp.fire)
		{
			ffFireT = 0f;
			return;
		}
		float num3 = Mathf.Clamp01(1f - ffFireT / 1.5f);
		ffFireT += dt;
		if (!(num3 <= 0f))
		{
			float num4 = 1f - Mathf.Exp((0f - dt) * 10f * num3);
			Vector3 v = Vector3.Lerp(fBCharacter.Pos, fBCharacter.Head.position, 0.55f) - position;
			float target = Mathf.Atan2(v.x, v.z) * 57.29578f;
			float num5 = Mathf.Atan2(v.y, v.Flat().magnitude) * 57.29578f;
			camYaw += Mathf.DeltaAngle(camYaw, target) * num4;
			camPitch += (num5 - camPitch) * num4;
		}
	}

	private void StartReload()
	{
		if (FBMed.Busy)
		{
			return;
		}
		bool empty = wu.Ammo == 0;
		if (wu.Reload() && (bool)fp && firstPerson && wu.Cur != null)
		{
			if (wu.Cur.cycled)
			{
				fp.ReloadCycled(Mathf.Min(wu.Cur.mag - wu.Ammo, wu.Reserve), null);
			}
			else
			{
				fp.Reload(empty);
			}
		}
	}

	private void TryHeal()
	{
		if (FBMed.Busy)
		{
			CancelHeal("");
		}
		else
		{
			if (ch.knocked)
			{
				return;
			}
			string text = FBMed.Why(ch);
			if (text == null && (hv.Flat().magnitude > 0.6f * ch.ScaleK || !onGround || ch.vehiclePose))
			{
				text = "PARE PARA SE CURAR";
			}
			if (text != null)
			{
				if (text != "morto")
				{
					FBHUD.I?.Center(text, 1.2f);
					FBAudio.Synth("deny");
				}
				return;
			}
			if (autoRun)
			{
				autoRun = false;
				FBHUD.I?.SetRun(on: false);
			}
			if (wu.reloadT > 0f)
			{
				wu.reloadT = 0f;
			}
			if (!(wu.switching > 0f))
			{
				FBMed.Begin();
				FBAudio.Synth("medopen");
			}
		}
	}

	private void CancelHeal(string why, bool redraw = true)
	{
		bool spent = FBMed.spent;
		if (FBMed.Cancel())
		{
			FBAudio.Synth("medcancel");
			healEnd = 0f;
			HealCancels++;
			if (!string.IsNullOrEmpty(why))
			{
				FBHUD.I?.Center(why + (spent ? " · CURA INTERROMPIDA" : " · KIT NÃO FOI GASTO"), 1.4f);
			}
		}
	}

	private void HealGuard(in FBInputState inp)
	{
		string text = ((!ch.alive || ch.knocked) ? "" : ((inp.move.sqrMagnitude > 0.04f || autoRun) ? "ANDOU" : (inp.jump ? "PULOU" : ((inp.sprintHeld && inp.move.y > 0.1f) ? "CORREU" : ((inp.fire || inp.firePressed) ? "ATIROU" : ((inp.aimPressed || inp.aim) ? "MIROU" : (inp.reload ? "RECARREGOU" : ((inp.slot >= 0 || inp.cycle != 0) ? "TROCOU DE ARMA" : ((inp.crouchToggle || inp.prone) ? "MUDOU DE POSIÇÃO" : (inp.interact ? "INTERAGIU" : ((inp.buy || inp.bag || inp.arsenal) ? "ABRIU O MENU" : (inp.inspect ? "INSPECIONOU" : ((!onGround) ? "SAIU DO CHÃO" : (ch.vehiclePose ? "ENTROU NO VEÍCULO" : null))))))))))))));
		if (text != null)
		{
			LastHealCancel = ((text == "") ? "caiu/morreu" : text);
			CancelHeal(text);
		}
	}

	// fisica 31/10: tem teto entre os pes e a altura topo (m, ja escalada)? Antes era um raio fino no centro da capsula: agachado com o corpo
	// meio debaixo de balcao/banco/toldo/retrovisor o raio passava livre, o cc.height crescia para dentro da beirada e o CC expulsava o
	// jogador de lado/para baixo (tranco) ou prendia. Agora SphereCast com 0,9 do raio da capsula e o mesmo topo. Mureta encostada nao
	// bloqueia: a varredura e vertical, fica ~3 cm dentro da pele, e o SphereCast ignora o que ja sobrepoe na largada.
	private bool SemEspaco(float topo)
	{
		float rr = cc.radius * Mathf.Max(base.transform.lossyScale.x, base.transform.lossyScale.z) * 0.9f;
		float o0 = 0.4f * ch.ScaleK;
		return FBWorld.RayHit(base.transform.position + Vector3.up * o0, Vector3.up, Mathf.Max(0.05f, topo - o0 - rr), rr);
	}

	private void SetProne(bool on)
	{
		if (!ch.alive || vault.HasValue || (on && !onGround))
		{
			return;
		}
		if (!on && prone && SemEspaco((Json.F(GameData.Section("controller"), "m_Height", 1.8f) + 0.2f) * ch.ScaleK))
		{
			FBHUD.I?.Center("SEM ESPAÇO PARA LEVANTAR", 1f);
			return;
		}
		if (on != prone)
		{
			FBAudio.Play2D(on ? "ie_slide" : "ie_crouch", on ? 0.5f : 0.35f);
		}
		prone = on;
		if (prone)
		{
			crouch = false;
			autoRun = false;
			FBHUD.I?.SetRun(on: false);
		}
		ch.poseT = Time.time;
	}

	private bool TryVault()
	{
		if (!onGround || prone || ch.special || FBMed.Busy)
		{
			return false;
		}
		float scaleK = ch.ScaleK;
		Vector3 dir = Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
		Vector3 position = base.transform.position;
		if (!FBWorld.RayHit(position + Vector3.up * 0.5f * scaleK, dir, 1.2f * scaleK, out var hit))
		{
			return false;
		}
		float num = hit.distance + 0.07f;
		Vector3 o = new Vector3(position.x + dir.x * num, position.y + 1.95f * scaleK, position.z + dir.z * num);
		if (!FBWorld.RayHit(o, Vector3.down, 1.95f * scaleK, out var hit2))
		{
			return false;
		}
		float num2 = o.y - hit2.distance - position.y;
		if (num2 < 0.45f * scaleK || num2 > 1.75f * scaleK)
		{
			return false;
		}
		if (FBWorld.RayHit(position + Vector3.up * (num2 + 0.3f * scaleK), dir, num + 1.2f * scaleK))
		{
			return false;
		}
		Vector3 vector = new Vector3(dir.z, 0f, 0f - dir.x);
		float[] array = new float[2] { 0.35f, 0.75f };
		foreach (float num3 in array)
		{
			for (float num4 = -0.22f; num4 <= 0.2201f; num4 += 0.04f)
			{
				if (FBWorld.RayHit(position + vector * num4 * scaleK + Vector3.up * (num2 + num3 * scaleK), dir, num + 0.35f * scaleK))
				{
					return false;
				}
			}
		}
		Vector3 vector2 = new Vector3(position.x + dir.x * (num + 1.1f * scaleK), position.y + num2, position.z + dir.z * (num + 1.1f * scaleK));
		if (!FBWorld.RayHit(vector2 + Vector3.up * 0.5f * scaleK, Vector3.down, num2 + 0.5f * scaleK + 1.3f * scaleK, out var hit3))
		{
			return false;
		}
		if (hit3.point.y < position.y - 1.2f * scaleK || hit3.point.y > position.y + num2 + 0.2f * scaleK)
		{
			return false;
		}
		vector2.y = hit3.point.y;
		vault = (0f, 0.567f * (1f + Mathf.Max(0f, num2 / scaleK - 1f) * 0.35f), position, vector2, num2 + 0.3f * scaleK, 0, camYaw, num);
		autoRun = false;
		FBHUD.I?.SetRun(on: false);
		hv = Vector3.zero;
		return true;
	}

	private void VaultStep(float dt)
	{
		(float, float, Vector3, Vector3, float, int, float, float) value = vault.Value;
		value.Item1 += dt;
		float num = Mathf.Min(1f, value.Item1 / value.Item2);
		float num2 = num * num * (3f - 2f * num);
		ch.Yaw = value.Item7;
		AnimationClip animationClip = (FBRegistry.I ? FBRegistry.I.Clip("FF_SYS_Vault") : null);
		ch.VaultRate = (animationClip ? Mathf.Clamp(animationClip.length * 0.92f / Mathf.Max(0.2f, value.Item2), 1f, 3f) : 0f);
		if (value.Item6 == 0 && value.Item1 >= 0.068f * value.Item2 / 0.567f)
		{
			value.Item6 = 1;
			FBAudio.Play2D("sg_getover", 0.7f, 0.04f);
		}
		if (value.Item6 == 1 && value.Item1 >= 0.578f * value.Item2 / 0.567f - 0.02f)
		{
			value.Item6 = 2;
			FBFootsteps.LandNow(ch, 0.55f);
			FBAudio.Play2D("ie_land", 0.32f);
		}
		Vector3 vector = new Vector3(value.Item3.x + (value.Item4.x - value.Item3.x) * num2, VaultY(value, num2), value.Item3.z + (value.Item4.z - value.Item3.z) * num2);
		Collider[] array = Physics.OverlapSphere(vector + Vector3.up * 0.7f, 0.5f, 4096);
		foreach (Collider obj in array)
		{
			FBGlass component = obj.GetComponent<FBGlass>();
			MeshFilter component2 = obj.GetComponent<MeshFilter>();
			if ((bool)component && (!component2 || !component2.sharedMesh || component2.sharedMesh.subMeshCount <= 1))
			{
				component.Break(vector, (value.Item4 - value.Item3).Flat().normalized);
			}
		}
		cc.enabled = false;
		base.transform.position = vector;
		cc.enabled = true;
		vy = 0f;
		if (num >= 1f)
		{
			vault = null;
			ch.VaultRate = 0f;
			hv = (value.Item4 - value.Item3).Flat() * 1.5f;
			vy = 0f;
			float? num3 = FBWorld.WaterAt(vector.x, vector.z);
			wasInWater = num3.HasValue && vector.y < num3 + 0.15f;
		}
		else
		{
			vault = value;
		}
	}

	private float VaultY((float t, float dur, Vector3 a, Vector3 b, float h, int snd, float yaw, float wall) v, float e)
	{
		float scaleK = ch.ScaleK;
		float num = Mathf.Max(0.01f, (v.b - v.a).Flat().magnitude);
		float num2 = Mathf.Clamp01(v.Rest.Item1 / num);
		float num3 = Mathf.Clamp(num2 * 0.8f, 0.08f, 0.6f);
		float num4 = Mathf.Clamp(num2 + 0.45f * scaleK / num, 0.4f, 0.85f);
		float num5 = Mathf.SmoothStep(0f, 1f, e / num3);
		float num6 = 1f - Mathf.SmoothStep(0f, 1f, (e - num4) / Mathf.Max(0.05f, 1f - num4));
		float num7 = v.h - 0.3f * scaleK;
		float num8 = Mathf.Lerp(v.a.y, v.b.y, num2);
		float num9 = Mathf.Max(0.05f * scaleK, v.a.y + num7 + 0.18f * scaleK - num8);
		return Mathf.Lerp(v.a.y, v.b.y, e) + num9 * num5 * num6;
	}

	private void ReviveStep(float dt, bool canMove, FBInputState inp)
	{
		FBCharacter fBCharacter = null;
		float time = Time.time;
		if (FBMatch.KnockOn && canMove && ch.alive && !ch.knocked)
		{
			float num = 9f;
			foreach (FBCharacter item in FBMatch.TeamOf(ch))
			{
				if (item.alive && item.knocked && (item.revivedBy == null || item.revivedBy == ch || !(time - item.reviveTick < 0.3f)))
				{
					Vector3 v = item.Pos - ch.Pos;
					float magnitude = v.Flat().magnitude;
					if (magnitude < num && FBMechanics.CanReviveTarget(v.x, v.z, magnitude / ch.ScaleK, camYaw * ((float)Math.PI / 180f)))
					{
						num = magnitude;
						fBCharacter = item;
					}
				}
			}
		}
		reviveCand = fBCharacter;
		if ((bool)fBCharacter && inp.interactHeld)
		{
			reviving = fBCharacter;
			fBCharacter.revivedBy = ch;
			fBCharacter.reviveTick = time;
			if (FBMechanics.ReviveStep(fBCharacter, dt, FBSession.mode))
			{
				FBCombat.CompleteRevive(fBCharacter, ch);
				reviving = null;
				fBCharacter = null;
			}
		}
		else
		{
			reviving = null;
		}
		FBHUD.I?.Revive(fBCharacter ? ((reviving ? "REANIMANDO " : "SEGURE F · REANIMAR ") + fBCharacter.Name) : null, fBCharacter ? fBCharacter.reviveP : 0f);
	}

	private void ApplyView()
	{
		if ((bool)fp && wu.Cur != null)
		{
			fp.SetWeapon(wu.Cur);
		}
		Renderer[] componentsInChildren = ch.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].shadowCastingMode = ((!firstPerson) ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly);
		}
		if (firstPerson)
		{
			BuildViewModel();
		}
		else if ((bool)vm)
		{
			UnityEngine.Object.Destroy(vm);
		}
	}

	private void BuildViewModel()
	{
		if ((bool)vm)
		{
			UnityEngine.Object.Destroy(vm);
		}
		vmMuzzle = null;
		WeaponDef cur = wu.Cur;
		if (!((cur != null && (bool)FBRegistry.I) ? FBRegistry.I.Weapon(cur.id) : null) || !firstPerson)
		{
			return;
		}
		vm = FBWeaponModel.Make(cur, cam.transform);
		vm.name = "ArmaVisao";
		Collider[] componentsInChildren = vm.GetComponentsInChildren<Collider>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		Renderer[] componentsInChildren2 = vm.GetComponentsInChildren<Renderer>();
		foreach (Renderer obj in componentsInChildren2)
		{
			obj.shadowCastingMode = ShadowCastingMode.Off;
			obj.gameObject.layer = 0;
		}
		Transform[] componentsInChildren3 = vm.GetComponentsInChildren<Transform>();
		foreach (Transform transform in componentsInChildren3)
		{
			if (transform.name.StartsWith("SOCKET_Muzzle"))
			{
				vmMuzzle = transform;
			}
		}
		FBFpArms.Attach(vm, ch, cur);
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBPlayer.LateUpdate");

	private void LateUpdate()
	{
		if (!FBTempo.On)
		{
			LateUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		LateUpdate__fbt();
		FBTempo.Soma(__fbt_LateUpdate, t0);
	}

	private void LateUpdate__fbt()
	{
		float deltaTime = Time.deltaTime;
		if (deltaTime <= 0f)
		{
			return;
		}
		StepSmooth(deltaTime);
		float t = 1f - Mathf.Exp((0f - deltaTime) * Json.F(GameData.Section("cameraLook"), "interpolationSpeed", 45f));
		camYawS = Mathf.LerpAngle(camYawS, camYaw, t);
		camPitchS = Mathf.Lerp(camPitchS, camPitch, t);
		float t2 = 1f - Mathf.Exp((0f - deltaTime) * (float)(flOn ? 30 : 9));
		flYawS = Mathf.Lerp(flYawS, flYaw, t2);
		flPitchS = Mathf.Lerp(flPitchS, flPitch, t2);
		if (!flOn && !FlBlend)
		{
			flYawS = (flPitchS = 0f);
		}
		tpsKick *= Mathf.Exp((0f - deltaTime) * 9f);
		tpsPunch *= Mathf.Exp((0f - deltaTime) * 11f);
		vmKick = Mathf.Max(0f, vmKick - deltaTime * 6f);
		if (FBOficina.Camera(cam, deltaTime) || FBGarage.Camera(cam, deltaTime))
		{
			return;
		}
		if (Driving && ch.alive)
		{
			if ((bool)vm)
			{
				vm.SetActive(value: false);
			}
			SetBodyVisible(v: true);
			FBVehicles.Camera(cam, deltaTime, camYawS, camPitchS);
		}
		else
		{
			if (FBBattleRoyale.Camera(cam, deltaTime, camYawS, camPitchS, ch))
			{
				return;
			}
			WeaponDef cur = wu.Cur;
			bool flag = ch.aiming && ch.alive;
			FBCharacter fBCharacter = (((bool)FBMatch.I && FBMatch.I.state == "spect" && (bool)FBMatch.I.Spectating && FBMatch.I.Spectating.alive) ? FBMatch.I.Spectating : ch);
			float fovBase = FovBase;
			Quaternion quaternion = Quaternion.Euler(0f - (camPitchS + flPitchS) - tpsKick, camYawS + flYawS, 0f);
			FBScopes.Update(cam, cur, flag && fBCharacter == ch && !ch.special);
			if (firstPerson && ch.alive && !ch.special && fBCharacter == ch)
			{
				if ((fpHideT -= deltaTime) <= 0f)
				{
					fpHideT = 0.2f;
					Renderer[] componentsInChildren = ch.GetComponentsInChildren<Renderer>(includeInactive: false);
					foreach (Renderer renderer in componentsInChildren)
					{
						if ((!vm || !renderer.transform.IsChildOf(vm.transform)) && (!fp || !renderer.transform.IsChildOf(fp.transform)) && renderer.gameObject.layer != 14)
						{
							renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
						}
					}
				}
				Vector3 vector = (ch.Head ? ch.Head.position : (base.transform.position + Vector3.up * 1.7f * ch.ScaleK));
				vector += Vector3.up * (camOff - (ch.feet ? ch.feet.HipsOffset : 0f));
				if ((bool)fp && fp.gameObject.activeSelf)
				{
					fp.transform.position += Vector3.up * camOff;
					fp.CameraPose(out var pos, out var rot);
					Quaternion quaternion2 = rot * Quaternion.Euler(0f, 0f, fp.camLeanRot.z);
					Vector3 vector2 = pos + rot * new Vector3(fp.camLeanLoc.x, fp.camLeanLoc.y, 0f);
					if (Mathf.Abs(lean) > 0.01f)
					{
						Vector3 vector3 = quaternion2 * Vector3.right;
						float num = lean * 0.35f;
						if (FBWorld.RayHit(vector2, vector3 * Mathf.Sign(num), Mathf.Abs(num) + 0.15f, out var hit, 0.05f))
						{
							num = Mathf.Sign(num) * Mathf.Max(0f, hit.distance - 0.15f);
						}
						vector2 += vector3 * num;
						quaternion2 *= Quaternion.Euler(0f, 0f, (0f - lean) * 8f);
					}
					Quaternion quaternion3 = quaternion2 * Quaternion.Inverse(rot);
					fp.transform.SetPositionAndRotation(vector2 + quaternion3 * (fp.transform.position - pos), quaternion3 * fp.transform.rotation);
					cam.transform.SetPositionAndRotation(vector2, quaternion2 * Quaternion.Euler(0f - tpsKick, 0f, 0f));
					if (FlBlend)
					{
						cam.transform.rotation = Quaternion.AngleAxis(flYawS, Vector3.up) * cam.transform.rotation;
						cam.transform.Rotate(0f - flPitchS, 0f, 0f, Space.Self);
					}
				}
				else
				{
					cam.transform.SetPositionAndRotation(vector + quaternion * new Vector3(0f, 0.07f, 0.08f) * ch.ScaleK, quaternion);
					if (Mathf.Abs(lean) > 0.01f)
					{
						float num2 = lean * 0.35f;
						if (FBWorld.RayHit(cam.transform.position, cam.transform.right * Mathf.Sign(num2), Mathf.Abs(num2) + 0.15f, out var hit2, 0.05f))
						{
							num2 = Mathf.Sign(num2) * Mathf.Max(0f, hit2.distance - 0.15f);
						}
						cam.transform.position += cam.transform.right * num2;
						cam.transform.Rotate(0f, 0f, (0f - lean) * 8f, Space.Self);
					}
				}
				if (camShake > 0f)
				{
					camShake = Mathf.Max(0f, camShake - deltaTime * 2f);
					float num3 = ((FBScopes.Scoped && cur != null) ? (1f / FBScopes.Zoom(cur)) : 1f);
					cam.transform.Rotate(UnityEngine.Random.Range(-1f, 1f) * camShake * 0.7f * num3, UnityEngine.Random.Range(-1f, 1f) * camShake * 0.7f * num3, 0f, Space.Self);
				}
				float b = ZoomFov(fovBase * (ch.sprint ? Json.F(GameData.Section("character"), "fieldOfViewRunningMultiplier", 1.1f) : 1f), (flag && cur != null) ? FBScopes.AdsZoom(cur) : 1f);
				cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, b, 1f - Mathf.Exp((0f - deltaTime) * 10f));
				if ((bool)vm && (bool)fp)
				{
					UnityEngine.Object.Destroy(vm);
				}
				if ((bool)vm && cur != null)
				{
					vm.SetActive(!FBMed.Busy && healEnd <= 0f && !ch.knocked && !reviving && !FBScopes.Scoped && Mathf.Abs(flYawS) < 12f);
					Vector3 vector4 = new Vector3(0.16f, -0.19f, 0.38f);
					Vector3 vector5 = FBScopes.AdsOffset(vm, cam);
					Vector3 vector6 = (flag ? vector5 : vector4);
					vmBob += deltaTime * new Vector2(hv.x, hv.z).magnitude * 1.6f;
					Vector3 vector7 = ((!flag) ? new Vector3(Mathf.Sin(vmBob) * 0.008f, Mathf.Abs(Mathf.Cos(vmBob)) * 0.01f, 0f) : Vector3.zero);
					Vector3 vector8 = new Vector3((0f - swayX) * 0.0015f, (0f - swayY) * 0.0015f, 0f) * (flag ? 0.3f : 1f);
					vm.transform.localPosition = Vector3.Lerp(vm.transform.localPosition, vector6 + vector7 + vector8 + new Vector3(0f, 0f, (0f - vmKick) * 0.035f) + (ch.sprint ? new Vector3(0.04f, -0.04f, -0.03f) : Vector3.zero), 1f - Mathf.Exp((0f - deltaTime) * 18f));
					vm.transform.localRotation = Quaternion.Slerp(vm.transform.localRotation, Quaternion.Euler((0f - vmKick) * 4f + (float)(ch.sprint ? 10 : 0), 180 + (ch.sprint ? (-25) : 0), 0f) * Quaternion.Euler(0f, 0f, swayX * 0.4f), 1f - Mathf.Exp((0f - deltaTime) * 16f));
					vm.transform.localScale = Vector3.one * 0.6175f;
				}
				else if (cur != null && !vm && !fp)
				{
					BuildViewModel();
				}
				if ((bool)vm && cur == null)
				{
					UnityEngine.Object.Destroy(vm);
				}
				SetBodyVisible(v: false);
			}
			else
			{
				if ((bool)vm)
				{
					vm.SetActive(value: false);
				}
				SetBodyVisible(v: true);
				bool flag2 = flag && fBCharacter == ch;
				bool flag3 = fBCharacter.velocity.Flat().magnitude > 0.5f;
				lastMoveT = (flag3 ? 0f : (lastMoveT + deltaTime));
				float scaleK = fBCharacter.ScaleK;
				float num4 = ((!fBCharacter.alive) ? 4f : ((fBCharacter.dance != null) ? 3.4f : (flag2 ? Mathf.Max(T("aimArmLength", 0.5f), 1.2f) : (T("armLength", 1.5f) - ((lastMoveT > 1f) ? T("idleArmPull", 0.3f) : 0f) + (fBCharacter.sprint ? T("runArmLengthBoost", 0.4f) : 0f) + ((fBCharacter != ch) ? 1.2f : 0f)))));
				float num5 = (flag2 ? Mathf.Max(T("aimLateralOffset", 0.5f), 0.7f) : (flag3 ? T("lateralOffset", 0.8f) : T("idleLateralOffset", 0.45f)));
				float num6 = ((lean < -0.05f) ? (0f - num5 + (lean + 1f) * 0.3f - Mathf.Abs(ch.PeekShift) * 0.8f) : ((lean > 0.05f) ? (num5 + Mathf.Abs(ch.PeekShift) * 1.1f) : num5));
				float num7 = ((Mathf.Abs(lean) > 0.05f) ? T("leanShiftSmoothTime", 0.12f) : (flag2 ? (1f / T("aimTransitionSpeed", 12f)) : T("lateralSmoothTime", 0.45f)));
				tpsArm = Mathf.Lerp(tpsArm, num4 * scaleK, 1f - Mathf.Exp((0f - deltaTime) / (flag2 ? (1f / T("aimTransitionSpeed", 12f)) : T("distanceSmoothTime", 0.4f)) * 2.3f));
				tpsLat = Mathf.Lerp(tpsLat, num6 * scaleK, 1f - Mathf.Exp((0f - deltaTime) / num7 * 2.3f));
				float b2 = ZoomFov(fovBase * T("tpsAimFovMultiplier", 1f), (flag2 && cur != null) ? FBScopes.AdsZoom(cur) : 1f) + ((fBCharacter.sprint && T("enableRunFov", 1f) > 0f) ? T("runFovBoost", 5f) : 0f);
				cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, b2, 1f - Mathf.Exp((0f - deltaTime) * 8f));
				Vector3 vector9 = quaternion * Vector3.forward;
				Vector3 vector10 = quaternion * Vector3.right;
				vector10.y = 0f;
				vector10.Normalize();
				pivY = Mathf.Lerp(pivY, ((fBCharacter.prone ? 0.45f : (fBCharacter.crouch ? T("crouchPivotHeight", 0.95f) : T("pivotHeight", 1.6f))) + (flag2 ? T("aimPivotHeightOffset", 0.12f) : 0f)) * scaleK, 1f - Mathf.Exp((0f - deltaTime) / T("verticalPivotSmoothTime", 0.08f) * 2.3f));
				Vector3 vector11 = fBCharacter.Pos + Vector3.up * (pivY + ((fBCharacter == ch) ? camOff : 0f));
				float r = T("collisionRadius", 0.25f);
				float num8 = Mathf.Sign(tpsLat);
				if (num8 == 0f)
				{
					num8 = 1f;
				}
				int num9 = ((num8 > 0f) ? 1 : 0);
				latClr[num9] = Recover(latClr[num9], Probe(vector11, vector10 * num8, 2f * scaleK, r), deltaTime);
				latClr[1 - num9] = -1f;
				vector11 += vector10 * num8 * Mathf.Min(latClr[num9], Mathf.Abs(tpsLat));
				Vector3 vector12 = -vector9;
				float a = tpsArm;
				collD = Recover(collD, Probe(vector11, vector12, 6f * scaleK, r), deltaTime);
				float num10 = Mathf.Min(a, collD);
				cam.transform.position = vector11 + vector12 * (num10 + ((fBCharacter == ch) ? (tpsPunch * Mathf.Min(1f, num10)) : 0f));
				cam.transform.rotation = Quaternion.LookRotation(vector9, Vector3.up);
				cam.transform.Rotate(0f, 0f, (0f - lean) * T("leanCameraRoll", 5f), Space.Self);
				if (fBCharacter == ch && ch.alive)
				{
					float magnitude = (cam.transform.position - ch.Pos).Flat().magnitude;
					float num11 = cam.transform.position.y - ch.Pos.y;
					float num12 = cc.radius * base.transform.lossyScale.x + 0.12f;
					float num13 = cc.height * base.transform.lossyScale.y;
					SetBodyVisible(ch.special || (!FBScopes.Scoped && (!(magnitude < num12) || !(num11 > -0.2f) || !(num11 < num13 + 0.2f))));
				}
				float num14 = ((FBScopes.Scoped && cur != null) ? (1f / Mathf.Max(1f, FBScopes.Zoom(cur))) : 1f);
				if (camShake > 0f && fBCharacter == ch)
				{
					camShake = Mathf.Max(0f, camShake - deltaTime * 2f);
					cam.transform.Rotate(UnityEngine.Random.Range(-1f, 1f) * camShake * 0.7f * num14, UnityEngine.Random.Range(-1f, 1f) * camShake * 0.7f * num14, 0f, Space.Self);
				}
				if (tpsShake > 0f)
				{
					tpsShake = Mathf.Max(0f, tpsShake - deltaTime * T("shakeDecaySpeed", 1.6f));
					float num15 = tpsShake * tpsShake;
					float num16 = Time.time * T("shakeFrequency", 18f);
					Dictionary<string, object> o = Json.O(GameData.Section("tpsCamera"), "shakeMaxAngles");
					cam.transform.Rotate(Mathf.Sin(num16 * 1.1f) * num15 * Json.F(o, "x", 1.2f), Mathf.Sin(num16 * 1.3f + 1f) * num15 * Json.F(o, "y", 1f), 0f, Space.Self);
				}
			}
			if ((bool)vmCam)
			{
				vmCam.fieldOfView = Json.F(GameData.Section("character"), "fieldOfViewWeapon", 65f) * ((flag && cur != null) ? Json.F(FBWeaponModel.Scope(cur), "fieldOfViewMultiplierAimWeapon", 1f) : 1f);
				vmCam.enabled = firstPerson && !ch.vehiclePose;
			}
		}
	}

	private static float Probe(Vector3 o, Vector3 dir, float L, float r)
	{
		float num = L;
		if (FBWorld.RayHit(o, dir, L + r, out var hit))
		{
			num = Mathf.Min(num, hit.distance - r);
		}
		if (FBWorld.RayHit(o, dir, L, out var hit2, r * 0.5f))
		{
			num = Mathf.Min(num, hit2.distance);
		}
		return Mathf.Max(0f, num);
	}

	private static float Recover(float cur, float free, float dt)
	{
		if (!(cur < 0f) && !(free <= cur))
		{
			return Mathf.Lerp(cur, free, 1f - Mathf.Exp((0f - dt) * T("collisionRecoverSpeed", 10f)));
		}
		return free;
	}

	private void StepSmooth(float dt)
	{
		float scaleK = ch.ScaleK;
		Vector3 position = base.transform.position;
		float num = position.y - camPp.y;
		float magnitude = new Vector2(position.x - camPp.x, position.z - camPp.z).magnitude;
		bool num2 = camPpOk && onGround && !vault.HasValue && ch.alive && !ch.vehiclePose && Mathf.Abs(num) < 0.7f * scaleK && magnitude < 2.5f * scaleK;
		camPp = position;
		camPpOk = true;
		if (num2)
		{
			if (magnitude > 0.001f)
			{
				camSlope = Mathf.Lerp(camSlope, Mathf.Clamp(num / magnitude, -1.5f, 1.5f), 1f - Mathf.Exp((0f - dt) * 3f));
			}
			camOff = Mathf.Clamp(camOff - (num - camSlope * magnitude), -0.6f * scaleK, 0.6f * scaleK);
		}
		else if (!onGround)
		{
			camSlope = Mathf.Lerp(camSlope, 0f, 1f - Mathf.Exp((0f - dt) * 6f));
		}
		camOff = Mathf.SmoothDamp(camOff, 0f, ref camOffV, 0.1f, 1f / 0f, dt);
	}

	private void SetBodyVisible(bool v)
	{
		if (bodyVis == v)
		{
			return;
		}
		bodyVis = v;
		Renderer[] componentsInChildren = ch.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if (!vm || !renderer.transform.IsChildOf(vm.transform))
			{
				renderer.shadowCastingMode = (v ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly);
			}
		}
	}
}
}
