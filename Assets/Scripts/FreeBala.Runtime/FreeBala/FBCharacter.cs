using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
[RequireComponent(typeof(Animator))]
public class FBCharacter : MonoBehaviour, IKnockable, FBFarinhaBuriti.IActor
{
	public class Dance
	{
		public string clip;

		public float t;

		public float len;

		public Queue<(string clip, bool hold)> chain;

		public string sitOut;

		public bool leaving;
	}

	public class NpcSet
	{
		public List<string> idle = new List<string>();

		public List<string> talk = new List<string>();

		public List<string> sit = new List<string>();

		public List<string> dance = new List<string>();

		public Dictionary<string, string> ovr = new Dictionary<string, string>();

		public string walk;

		public string sex = "m";

		public float walkRs = 1f;

		public bool activity;
	}

	private class Holstered
	{
		public GameObject go;

		public string slot;

		public Vector3 b;

		public Vector3 t;

		public Vector3 c;

		public float half;

		public float thick;

		public float wide;

		public float d;

		public bool dOk;

		public FBHolsterManualCalibrations.Entry manual;

		public FBHolsterManualCalibrations manualDatabase;

		public string manualSkin;

		public int manualRevision = -2147483648;

		public float manualClearance;

		public bool manualClearanceReady;
	}

	private class BodyPart
	{
		public HumanBodyBones hb;

		public HumanBodyBones endHb = HumanBodyBones.LastBone;

		public int kind;

		public Vector3 end;

		public Vector3 c;

		public float r0;

		public float r1;

		public Vector3 ax;

		public Vector3 ay;

		public Vector3 az;

		public float cell;

		public int nx;

		public int ny;

		public int nz;

		public int ox;

		public int oy;

		public int oz;

		public float[] fF;

		public float[] fB;

		public float[] sR;

		public float[] sL;
	}

	public string skinId;

	public string displayName;

	public int team;

	public bool isPlayer;

	public bool isBot;

	public bool isRemote;

	public bool civ;

	public float hp = 100f;

	public float maxHp = 100f;

	public float armor;

	[NonSerialized]
	public float protUntil;

	public bool alive = true;

	public bool helmet;

	public int kills;

	public int deaths;

	public int netId = -1;

	public int kits = 2;

	[NonSerialized]
	public Vector3 velocity;

	[NonSerialized]
	public bool grounded = true;

	[NonSerialized]
	public bool crouch;

	[NonSerialized]
	public bool sprint;

	[NonSerialized]
	public bool aiming;

	[NonSerialized]
	public bool shooting;

	[NonSerialized]
	public bool combat;

	[NonSerialized]
	public bool relaxed;

	[NonSerialized]
	public bool reloading;

	[NonSerialized]
	public bool prone;

	[NonSerialized]
	public bool special;

	[NonSerialized]
	public bool surrender;

	[NonSerialized]
	public bool talking;

	[NonSerialized]
	public bool fleeing;

	[NonSerialized]
	public Vector3 aimPoint;

	[NonSerialized]
	public bool hasAim;

	[NonSerialized]
	public float lastShot = 9f;

	[NonSerialized]
	public float lean;

	[NonSerialized]
	public float peekRoom = 0.25f;

	[NonSerialized]
	public float combatHold;

	[NonSerialized]
	public WeaponDef weapon;

	[NonSerialized]
	public GameObject weaponGO;

	public Action<FBCharacter, FBCharacter> onDeath;

	[NonSerialized]
	public FBHitInfo lastHit;

	[NonSerialized]
	public string lastWhy;

	[NonSerialized]
	public bool lastHitHead;

	[NonSerialized]
	public Vector3? lastHitFrom;

	[NonSerialized]
	public string deathClip;

	[NonSerialized]
	public FBBrCore.Armor vest;

	[NonSerialized]
	public FBBrCore.Armor helmetFF;

	[NonSerialized]
	public float poseT = -9f;

	[NonSerialized]
	public float tagT;

	[NonSerialized]
	public float getUpT;

	[NonSerialized]
	public string koFall;

	[NonSerialized]
	public bool knocked;

	[NonSerialized]
	public float knockInvT;

	[NonSerialized]
	public float bleedAcc;

	[NonSerialized]
	public float reviveP;

	[NonSerialized]
	public float reviveTick;

	[NonSerialized]
	public float koAt;

	[NonSerialized]
	public object revivedBy;

	[NonSerialized]
	public FBCharacter knockedBy;

	[NonSerialized]
	public string sys;

	private string waitPose;

	[NonSerialized]
	public Dance dance;

	private static readonly string[] DEATHS = new string[4] { "Rifle_Death_3", "Rifle_Death_L", "Rifle_Death_R", "Rifle_Death_L" };

	private readonly HashSet<string> pars = new HashSet<string>();

	private readonly Dictionary<string, string> pn = new Dictionary<string, string>();

	[NonSerialized]
	public Animator an;

	private AnimatorOverrideController ov;

	private readonly List<KeyValuePair<AnimationClip, AnimationClip>> ovList = new List<KeyValuePair<AnimationClip, AnimationClip>>();

	private readonly Dictionary<string, AnimationClip> origByName = new Dictionary<string, AnimationClip>();

	private readonly Dictionary<string, string> curOver = new Dictionary<string, string>();

	private int LU = -1;

	private int LF = -1;

	private int LE = -1;

	private int LEU = -1;

	private bool hasDeathState;

	private float upperW;

	private float emoW;

	private float danW;

	private float finW;

	private float finTarget;

	private float airT;

	private float vx;

	private float vz;

	private float stillT;

	private float hitCd;

	private float lastSpeed;

	private float kick;

	private float lastShotPrev = 9f;

	private float pwW;

	private float leanS;

	private bool wasAir;

	private bool wasCrouch;

	private bool wasSprint;

	private bool wasStill;

	private bool emoteB;

	private bool fullB;

	private string npcIdleLast;

	private Transform rHand;

	private Transform lHand;

	private Transform hips;

	private Transform chest;

	private Transform neck;

	private Transform rThigh;

	private Transform rKnee;

	private Transform rFore;

	private Transform rArm;

	private Transform lArm;

	private Transform lFoot;

	private Transform rFoot;

	private Transform muzzle;

	private Transform grip;

	private Transform mag;

	private Transform[] spine;

	[NonSerialized]
	public FBRig rig;

	[NonSerialized]
	public float walkSpeed = 1.4f;

	[NonSerialized]
	public NpcSet npcSet;

	private float scaleK = 1f;

	private bool crouchFast;

	private Quaternion handCorr = Quaternion.identity;

	private float palmK = 1f;

	[NonSerialized]
	public float HandDeg;

	private Quaternion proxyQ;

	private Vector3 proxyP;

	private (Transform node, Vector3 dir)? barrel;

	private readonly List<GameObject> ownWeapon = new List<GameObject>();

	private static FBHolsterManualCalibrations manualHolsterDatabase;

	private readonly Dictionary<string, Holstered> stowed = new Dictionary<string, Holstered>();

	private GameObject curModel;

	private Holstered cur;

	private bool stowCur;

	private static readonly Dictionary<string, List<BodyPart>> bodyCache = new Dictionary<string, List<BodyPart>>();

	private List<BodyPart> body;

	private Transform[] bodyBone;

	private Transform[] bodyEnd;

	private bool bodyTried;

	private Vector3 tUpL;

	private Vector3 tRtL;

	private bool tAxes;

	private Transform lThighB;

	public static double MeasureMs;

	public static double SolveMs;

	public static int MeasureN;

	public static int SolveN;

	private int holsterFrame;

	private float drawT = -1f;

	private float drawAt;

	private float drawLen;

	private float drawSpd = 1f;

	private bool drawing;

	private bool drawHid;

	private float rescueT = -1f;

	private float rescueEndT = -1f;

	private float emoteT = -1f;

	private float emoteLen;

	private float emoteSpd = 1f;

	private string fullClip;

	[NonSerialized]
	public bool vehiclePose;

	[NonSerialized]
	public bool fpHeadOn;

	[NonSerialized]
	public FBCar fitCar;

	[NonSerialized]
	public float seatDrop;

	private float specT = -1f;

	private float specLen;

	private Action specEnd;

	[NonSerialized]
	public bool keepFinal;

	[NonSerialized]
	public Vector2? animVel;

	[NonSerialized]
	public float LocoRate = 1f;

	[NonSerialized]
	public float VaultRate;

	private static readonly List<AnimatorClipInfo> clipInfo = new List<AnimatorClipInfo>();

	private static readonly Dictionary<AnimationClip, float> rsCache = new Dictionary<AnimationClip, float>();

	private static Dictionary<string, object> measuredRS;

	private Vector3 hipRest;

	private bool hipRestOk;

	private bool vaultTail;

	private float danceLift;

	private Transform lToe;

	private Transform rToe;

	private bool toesOk;

	[NonSerialized]
	public bool seated;

	[NonSerialized]
	public FBFootIK feet;

	public bool Unarmed => weapon == null;

	public bool Knocked
	{
		get
		{
			return knocked;
		}
		set
		{
			knocked = value;
		}
	}

	public float Hp
	{
		get
		{
			return hp;
		}
		set
		{
			hp = value;
		}
	}

	public float MaxHp => maxHp;

	public float KnockInvT
	{
		get
		{
			return knockInvT;
		}
		set
		{
			knockInvT = value;
		}
	}

	public float BleedAcc
	{
		get
		{
			return bleedAcc;
		}
		set
		{
			bleedAcc = value;
		}
	}

	public object RevivedBy
	{
		get
		{
			return revivedBy;
		}
		set
		{
			revivedBy = value;
		}
	}

	public float ReviveP
	{
		get
		{
			return reviveP;
		}
		set
		{
			reviveP = value;
		}
	}

	public bool Civ => civ;

	public bool IsFemale
	{
		get
		{
			SkinDef skinDef = GameData.Skins.Find((SkinDef s) => s.id == skinId);
			if (skinDef == null)
			{
				return FBCustomizeData.Find(skinId)?.fem ?? false;
			}
			return skinDef.sex == "f";
		}
	}

	public string Name
	{
		get
		{
			object obj;
			if (string.IsNullOrEmpty(displayName))
			{
				obj = GameData.Skins.Find((SkinDef s) => s.id == skinId)?.name;
				if (obj == null)
				{
					obj = FBCustomizeData.Find(skinId)?.name;
					if (obj == null)
					{
						return skinId;
					}
				}
			}
			else
			{
				obj = displayName;
			}
			return (string)obj;
		}
	}

	public Vector3 Pos => base.transform.position;

	public Vector3 Fwd => base.transform.forward;

	public int Team => team;

	public bool Alive => alive;

	public float Yaw
	{
		get
		{
			return base.transform.eulerAngles.y;
		}
		set
		{
			base.transform.rotation = Quaternion.Euler(0f, value, 0f);
		}
	}

	public bool Sitting
	{
		get
		{
			if (dance != null && dance.sitOut != null)
			{
				return !dance.leaving;
			}
			return false;
		}
	}

	public bool UnarmedLocomotion { get; private set; }

	public bool IsPistol
	{
		get
		{
			if (weapon != null)
			{
				return weapon.IsPistol;
			}
			return false;
		}
	}

	public bool CombatStance { get; private set; }

	public bool Hot { get; private set; }

	public bool HasTpsGrip
	{
		get
		{
			if (weapon != null)
			{
				return Json.O(GameData.TpsGrip, weapon.id) != null;
			}
			return false;
		}
	}

	public Transform Head { get; private set; }

	public Transform Muzzle
	{
		get
		{
			if (!muzzle)
			{
				return rHand;
			}
			return muzzle;
		}
	}

	public Transform RightHand => rHand;

	public Transform LeftHand => lHand;

	public Transform Hips => hips;

	public Transform Neck => neck;

	public Transform Chest => chest;

	public bool CrouchFast
	{
		get
		{
			return crouchFast;
		}
		set
		{
			if (crouchFast != value)
			{
				crouchFast = value;
				LocoOverrides();
			}
		}
	}

	public Vector3 ProxyLocalPos => proxyP;

	public Quaternion ProxyLocalRot => proxyQ;

	public float HolsterWorldScale
	{
		get
		{
			if (!weaponGO)
			{
				if (!rHand)
				{
					return 1.3585f * scaleK;
				}
				return rHand.lossyScale.x * 0.6175f;
			}
			return weaponGO.transform.lossyScale.x;
		}
	}

	public IEnumerable<GameObject> HolsterObjects
	{
		get
		{
			foreach (Holstered value in stowed.Values)
			{
				if ((bool)value.go)
				{
					yield return value.go;
				}
			}
			if (cur != null && (bool)cur.go)
			{
				yield return cur.go;
			}
		}
	}

	public bool Rescuing
	{
		get
		{
			if (!(rescueT >= 0f))
			{
				return rescueEndT >= 0f;
			}
			return true;
		}
	}

	public bool DrawBusy
	{
		get
		{
			if (!drawing)
			{
				if (drawT >= 0f)
				{
					return emoW > 0.25f;
				}
				return false;
			}
			if (drawT >= 0f)
			{
				return drawT < drawAt;
			}
			return false;
		}
	}

	public bool IsDrawing
	{
		get
		{
			if (drawing && drawT >= 0f)
			{
				return drawT < drawAt;
			}
			return false;
		}
	}

	public float GroundLift => danceLift;

	public float PeekShift { get; private set; }

	public Vector3 MuzzlePos
	{
		get
		{
			if (!Muzzle)
			{
				return base.transform.position + Vector3.up * 1.4f * scaleK;
			}
			return Muzzle.position;
		}
	}

	// Correção: depois que a mão pega a arma, a animação de sacar ainda traz a arma das costas (ou do coldre) para a frente.
	// A mira e a mão esquerda entravam logo nessa hora e giravam a arma para a frente com a mão ainda no ombro,
	// e o cano atravessava o peito. Agora elas só entram quando a animação de sacar está terminando.
	public static bool DrawFollowAnim = true;

	private bool DrawSettling
	{
		get
		{
			if (DrawFollowAnim && drawing && drawT >= drawAt)
			{
				return emoW > 0.35f;
			}
			return false;
		}
	}

	public bool HandsBusy
	{
		get
		{
			if (reloading || DrawBusy || DrawSettling)
			{
				return true;
			}
			if (LU < 0 || !an || !an.isActiveAndEnabled || !an.runtimeAnimatorController || LU >= an.layerCount)
			{
				return false;
			}
			AnimatorStateInfo currentAnimatorStateInfo = an.GetCurrentAnimatorStateInfo(LU);
			if (!currentAnimatorStateInfo.IsName("Hit Reaction") && !currentAnimatorStateInfo.IsName("Grenade Throw"))
			{
				return currentAnimatorStateInfo.IsName("Inspect Anim");
			}
			return true;
		}
	}

	public float UpperW => upperW;

	public float DanW => danW;

	public float StillT => stillT;

	public float ScaleK => scaleK;

	public bool IsRelaxedPose => UnarmedLocomotion;

	public bool Resting
	{
		get
		{
			if (!UnarmedLocomotion && !Hot)
			{
				return stillT > PUP("relaxedEnterDelay", 3f);
			}
			return false;
		}
	}

	public bool Special
	{
		get
		{
			if (!special)
			{
				return danW > 0.05f;
			}
			return true;
		}
	}

	private static float PUP(string k, float d)
	{
		return Json.F(GameData.Section("puppet"), k, d);
	}

	private string P(string n)
	{
		AnimatorControllerParameter[] parameters = an.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (animatorControllerParameter.name.Trim() == n)
			{
				return animatorControllerParameter.name;
			}
		}
		return null;
	}

	private string PN(string n)
	{
		if (!pn.TryGetValue(n, out var value))
		{
			value = (pn[n] = (pars.Contains(n) ? P(n) : null));
		}
		return value;
	}

	private void SF(string n, float v)
	{
		string text = PN(n);
		if (text != null)
		{
			an.SetFloat(text, v);
		}
	}

	private void SB(string n, bool v)
	{
		string text = PN(n);
		if (text != null)
		{
			an.SetBool(text, v);
		}
	}

	private void ST(string n)
	{
		string text = PN(n);
		if (text != null)
		{
			an.SetTrigger(text);
		}
	}

	private void Awake()
	{
		GameData.Load();
		an = GetComponent<Animator>();
		if (!an.runtimeAnimatorController && (bool)FBRegistry.I)
		{
			an.runtimeAnimatorController = FBRegistry.I.mainController;
		}
		if ((bool)an.runtimeAnimatorController)
		{
			ov = new AnimatorOverrideController(an.runtimeAnimatorController);
			an.runtimeAnimatorController = ov;
			ov.GetOverrides(ovList);
			foreach (KeyValuePair<AnimationClip, AnimationClip> ov in ovList)
			{
				if ((bool)ov.Key)
				{
					origByName[ov.Key.name] = ov.Key;
				}
			}
		}
		AnimatorControllerParameter[] parameters = an.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			pars.Add(animatorControllerParameter.name.Trim());
		}
		LU = an.GetLayerIndex("Upper Body");
		LF = an.GetLayerIndex("Finisher");
		LE = an.GetLayerIndex("Emote Full Body");
		LEU = an.GetLayerIndex("Emote Upper Body");
		hasDeathState = (bool)an.runtimeAnimatorController && an.HasState(0, Animator.StringToHash("Death Reaction"));
		if (an.isHuman)
		{
			B(out rHand, HumanBodyBones.RightHand);
			B(out lHand, HumanBodyBones.LeftHand);
			Head = an.GetBoneTransform(HumanBodyBones.Head);
			if ((bool)rHand)
			{
				HashSet<Transform> hashSet = new HashSet<Transform>();
				for (int j = 0; j < 55; j++)
				{
					Transform boneTransform = an.GetBoneTransform((HumanBodyBones)j);
					if ((bool)boneTransform)
					{
						hashSet.Add(boneTransform);
					}
				}
				foreach (Transform item in rHand)
				{
					if (!item.name.StartsWith("mixamorig") && !hashSet.Contains(item) && (bool)item.GetComponentInChildren<Renderer>(includeInactive: true))
					{
						ownWeapon.Add(item.gameObject);
					}
				}
			}
			B(out hips, HumanBodyBones.Hips);
			B(out neck, HumanBodyBones.Neck);
			B(out rFore, HumanBodyBones.RightLowerArm);
			B(out rArm, HumanBodyBones.RightUpperArm);
			B(out lArm, HumanBodyBones.LeftUpperArm);
			chest = an.GetBoneTransform(HumanBodyBones.UpperChest) ?? an.GetBoneTransform(HumanBodyBones.Chest) ?? an.GetBoneTransform(HumanBodyBones.Spine);
			B(out rThigh, HumanBodyBones.RightUpperLeg);
			B(out rKnee, HumanBodyBones.RightLowerLeg);
			B(out lFoot, HumanBodyBones.LeftFoot);
			B(out rFoot, HumanBodyBones.RightFoot);
			List<Transform> list = new List<Transform>();
			HumanBodyBones[] array = new HumanBodyBones[3]
			{
				HumanBodyBones.Spine,
				HumanBodyBones.Chest,
				HumanBodyBones.UpperChest
			};
			foreach (HumanBodyBones humanBoneId in array)
			{
				Transform boneTransform2 = an.GetBoneTransform(humanBoneId);
				if ((bool)boneTransform2)
				{
					list.Add(boneTransform2);
				}
			}
			spine = list.ToArray();
			InitTorsoAxes();
		}
		scaleK = base.transform.lossyScale.y / 2.2f;
		handCorr = FBHand.RightCorrection(an, out palmK, out HandDeg);
		rig = this.GetOrAdd<FBRig>();
		rig.Init(this);
		feet = this.GetOrAdd<FBFootIK>();
		feet.Init(this);
		SetUnarmedLocomotion(on: true, force: true);
	}

	private void B(out Transform t, HumanBodyBones b)
	{
		t = an.GetBoneTransform(b);
	}

	private void Start()
	{
		SetUnarmedLocomotion(UnarmedLocomotion, force: true);
	}

	public void SetOverrides(Dictionary<string, string> map)
	{
		if (ov == null)
		{
			return;
		}
		List<KeyValuePair<AnimationClip, AnimationClip>> list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
		foreach (KeyValuePair<string, string> item in map)
		{
			if (origByName.TryGetValue(FBRegistry.Safe(item.Key), out var value) || origByName.TryGetValue(item.Key, out value))
			{
				AnimationClip value2 = ((item.Value == item.Key) ? null : (FBRegistry.I ? FBRegistry.I.Clip(item.Value) : null));
				if (!curOver.TryGetValue(item.Key, out var value3) || !(value3 == item.Value))
				{
					curOver[item.Key] = item.Value;
					list.Add(new KeyValuePair<AnimationClip, AnimationClip>(value, value2));
				}
			}
		}
		if (list.Count > 0)
		{
			ov.ApplyOverrides(list);
		}
	}

	public void SetOverride(string orig, string clip)
	{
		SetOverrides(new Dictionary<string, string> { [orig] = clip });
	}

	public string OverrideOf(string orig)
	{
		if (!curOver.TryGetValue(orig, out var value))
		{
			return orig;
		}
		return value;
	}

	private static bool Has(string clip)
	{
		if ((bool)FBRegistry.I)
		{
			return FBRegistry.I.Clip(clip);
		}
		return false;
	}

	private void LocoOverrides()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (string key in FBAnimProfile.Unarmed.Keys)
		{
			dictionary[key] = key;
		}
		foreach (string key2 in FBAnimProfile.ArmedRun.Keys)
		{
			dictionary[key2] = key2;
		}
		if (UnarmedLocomotion)
		{
			foreach (KeyValuePair<string, string> item in FBAnimProfile.Unarmed)
			{
				if (Has(item.Value))
				{
					dictionary[item.Key] = item.Value;
				}
			}
			string text = (IsFemale ? "REG_Walk_F" : "REG_Walk_M");
			if (Has(text))
			{
				dictionary["WalkFwdLoop"] = text;
			}
			foreach (KeyValuePair<string, string> item2 in FBAnimProfile.UnarmedWalkNpc)
			{
				if (item2.Value == item2.Key || Has(item2.Value))
				{
					dictionary[item2.Key] = item2.Value;
				}
			}
			foreach (KeyValuePair<string, string> item3 in FBAnimProfile.UnarmedRight)
			{
				if (Has(item3.Value))
				{
					dictionary[item3.Key] = item3.Value;
				}
			}
			if (npcSet != null)
			{
				foreach (KeyValuePair<string, string> item4 in npcSet.ovr)
				{
					if (Has(item4.Value))
					{
						dictionary[item4.Key] = item4.Value;
					}
				}
			}
			if (!isPlayer && !isRemote)
			{
				string[] npcStrafe = FBAnimProfile.NpcStrafe;
				foreach (string text2 in npcStrafe)
				{
					dictionary[text2] = text2;
				}
				if (!IsFemale)
				{
					npcStrafe = FBAnimProfile.NpcMaleWalk;
					foreach (string text3 in npcStrafe)
					{
						if (!(text3 == "WalkFwdLoop") || npcSet == null || !npcSet.activity)
						{
							dictionary[text3] = text3;
						}
					}
				}
			}
		}
		else
		{
			foreach (KeyValuePair<string, string> item5 in FBAnimProfile.ArmedRun)
			{
				if (Has(item5.Value))
				{
					dictionary[item5.Key] = item5.Value;
				}
			}
			if (IsPistol && Has("IE_Pistol_Sprint_F"))
			{
				dictionary["Rifle_SprintLoop"] = "IE_Pistol_Sprint_F";
			}
		}
		CombatOverrides(dictionary);
		SetOverrides(dictionary);
	}

	public void SetUnarmedLocomotion(bool on, bool force = false)
	{
		if (force || UnarmedLocomotion != on)
		{
			UnarmedLocomotion = on;
			stillT = 0f;
			LocoOverrides();
			if (on)
			{
				aiming = false;
				shooting = false;
				reloading = false;
			}
		}
	}

	public void SetNpcSet(NpcSet s)
	{
		npcSet = ((s != null && s.idle.Exists(Has)) ? s : null);
		npcIdleLast = null;
		if (UnarmedLocomotion)
		{
			SetUnarmedLocomotion(on: true, force: true);
		}
	}

	public void SetFireMode(bool automatic)
	{
		string text = (automatic ? "Rifle_ShootLoop_Additive" : (FBAnimProfile.Fire(weapon) ?? "Rifle_ShootOnce"));
		SetOverride("Rifle_ShootOnce", Has(text) ? text : "Rifle_ShootOnce");
	}

	private void CombatOverrides(Dictionary<string, string> m)
	{
		bool flag = UnarmedLocomotion || weapon == null;
		foreach (KeyValuePair<string, string> item in FBAnimProfile.UnarmedCrouch)
		{
			O(item.Key, flag ? item.Value : null);
		}
		if (!flag)
		{
			foreach (KeyValuePair<string, string> item2 in FBAnimProfile.ArmedCrouchFast)
			{
				O(item2.Key, crouchFast ? item2.Value : null);
			}
		}
		O("Rifle_Reload_2", flag ? null : FBAnimProfile.Reload(weapon));
		void O(string orig, string clip)
		{
			if (origByName.ContainsKey(orig) || origByName.ContainsKey(FBRegistry.Safe(orig)))
			{
				m[orig] = ((clip != null && Has(clip)) ? clip : orig);
			}
		}
	}

	public void SetWeapon(WeaponDef def, GameObject model)
	{
		if ((bool)weaponGO)
		{
			UnityEngine.Object.Destroy(weaponGO);
		}
		foreach (GameObject item in ownWeapon)
		{
			if ((bool)item)
			{
				item.SetActive(value: false);
			}
		}
		weapon = def;
		weaponGO = null;
		muzzle = (grip = (mag = null));
		curModel = model;
		if (cur != null)
		{
			UnityEngine.Object.Destroy(cur.go);
			cur = null;
		}
		drawT = -1f;
		drawHid = false;
		if (def != null && (bool)model && (bool)rHand)
		{
			Dictionary<string, object> o = GameData.Section("puppet");
			Dictionary<string, object> dictionary = Json.O(o, "tpsWeaponProxyLocalPosition");
			Dictionary<string, object> dictionary2 = Json.O(o, "tpsWeaponProxyLocalEuler");
			Dictionary<string, object> dictionary3 = Json.O(o, "tpsWeaponProxyLocalScale");
			weaponGO = FBWeaponModel.Make(def, rHand);
			Vector3 vector = ((dictionary != null) ? new Vector3(Json.F(dictionary, "x"), Json.F(dictionary, "y"), Json.F(dictionary, "z")) : new Vector3(-0.0191f, 0.0931f, 0.0241f));
			Vector3 euler = ((dictionary2 != null) ? new Vector3(Json.F(dictionary2, "x"), Json.F(dictionary2, "y"), Json.F(dictionary2, "z")) : new Vector3(-92.148f, 168.481f, -91.717f));
			weaponGO.transform.localPosition = handCorr * vector * palmK;
			weaponGO.transform.localRotation = handCorr * Quaternion.Euler(euler);
			weaponGO.transform.localScale = Vector3.one * ((dictionary3 != null) ? Json.F(dictionary3, "x", 0.6175f) : 0.6175f);
			GripCorrect(def);
			proxyQ = weaponGO.transform.localRotation;
			proxyP = weaponGO.transform.localPosition;
			Transform[] componentsInChildren = weaponGO.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (transform.name.StartsWith("SOCKET_Muzzle"))
				{
					muzzle = transform;
				}
				else if (transform.name.StartsWith("SOCKET_Grip"))
				{
					grip = transform;
				}
				else if (transform.name.StartsWith("SOCKET_Magazine"))
				{
					mag = transform;
				}
			}
			Collider[] componentsInChildren2 = weaponGO.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				UnityEngine.Object.Destroy(componentsInChildren2[i]);
			}
			barrel = null;
			FBLaser.Bind(this, weaponGO, def, fp: false);
		}
		SetUnarmedLocomotion(def == null);
		if (def != null)
		{
			SetFireMode(def.auto);
		}
		LocoOverrides();
		rig?.OnWeapon(weaponGO, grip, mag, muzzle, IsPistol);
	}

	// Empunhadura em terceira pessoa: todas as armas nascem com a mesma pose na mão direita. Para as armas (ou classes)
	// listadas aqui, a arma é girada e movida para que a mão fique nela do mesmo jeito que fica na arma de referência,
	// usando a posição da mão direita de cada arma guardada em tpsGrip.<id>.r (pulso, nós dos dedos), em espaço da arma.
	// Referências conferidas com fotos antes x depois das 51 armas (revólveres e RPG eram os que ficavam fora da mão).
	public static Dictionary<string, string> GripRefs = new Dictionary<string, string>
	{
		{ "pistola", "desert_eagle" },
		{ "fuzil", "ak" },
		{ "submetralhadora", "ak" },
		{ "escopeta", "ak" },
		{ "sniper", "ak" },
		{ "explosivo", "ak" }
	};

	public static bool GripAuto = true;

	private static readonly Dictionary<string, (Vector3 wrist, Quaternion rot)?> gripFrames = new Dictionary<string, (Vector3, Quaternion)?>();

	private static Vector3 GripV(object o)
	{
		if (!(o is List<object> { Count: >=3 } list))
		{
			return Vector3.zero;
		}
		return new Vector3(0f - Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2]));
	}

	// pulso e orientação da mão direita no espaço da raiz do modelo da arma (lidos do prefab, iguais para toda instância)
	private static (Vector3 wrist, Quaternion rot)? GripFrame(string id)
	{
		if (gripFrames.TryGetValue(id, out var value))
		{
			return value;
		}
		(Vector3, Quaternion)? result = null;
		Dictionary<string, object> dictionary = Json.O(Json.O(GameData.TpsGrip, id), "r");
		List<object> list = Json.L(dictionary, "m");
		List<object> list2 = Json.L(dictionary, "i");
		List<object> list3 = Json.L(dictionary, "p");
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Weapon(id) : null);
		if (dictionary != null && dictionary.TryGetValue("w", out var w) && list != null && list.Count > 0 && list2 != null && list2.Count > 0 && list3 != null && list3.Count > 0 && (bool)gameObject)
		{
			Transform transform = gameObject.transform;
			Transform transform2 = ((transform.childCount != 1 || (bool)transform.GetComponent<Renderer>()) ? transform : transform.GetChild(0));
			Vector3 vector = transform.InverseTransformPoint(transform2.TransformPoint(GripV(w)));
			Vector3 vector2 = transform.InverseTransformPoint(transform2.TransformPoint(GripV(list[0]))) - vector;
			Vector3 rhs = transform.InverseTransformPoint(transform2.TransformPoint(GripV(list2[0]))) - transform.InverseTransformPoint(transform2.TransformPoint(GripV(list3[0])));
			Vector3 upwards = Vector3.Cross(vector2, rhs);
			if (vector2.sqrMagnitude > 1E-08f && upwards.sqrMagnitude > 1E-10f)
			{
				result = (vector, Quaternion.LookRotation(vector2, upwards));
			}
		}
		if ((bool)FBRegistry.I)
		{
			gripFrames[id] = result;
		}
		return result;
	}

	public static string GripRefOf(WeaponDef def)
	{
		if (def == null)
		{
			return null;
		}
		if (GripRefs.TryGetValue(def.id, out var value) || (def.cls != null && GripRefs.TryGetValue(def.cls, out value)))
		{
			return value;
		}
		return null;
	}

	private void GripCorrect(WeaponDef def)
	{
		string text = (GripAuto ? GripRefOf(def) : null);
		if (!weaponGO || string.IsNullOrEmpty(text) || text == def.id)
		{
			return;
		}
		(Vector3, Quaternion)? tuple = GripFrame(def.id);
		(Vector3, Quaternion)? tuple2 = GripFrame(text);
		if (tuple.HasValue && tuple2.HasValue)
		{
			Transform transform = weaponGO.transform;
			Quaternion localRotation = transform.localRotation;
			Quaternion quaternion = localRotation * tuple2.Value.Item2 * Quaternion.Inverse(tuple.Value.Item2);
			transform.localPosition += transform.localScale.x * (localRotation * tuple2.Value.Item1 - quaternion * tuple.Value.Item1);
			transform.localRotation = quaternion;
		}
	}

	public void SetWeaponVisible(bool v)
	{
		if ((bool)weaponGO)
		{
			weaponGO.SetActive(v && !drawHid);
			return;
		}
		foreach (GameObject item in ownWeapon)
		{
			if ((bool)item)
			{
				item.SetActive(v && !vehiclePose);
			}
		}
	}

	private (Transform, Vector3)? BarrelAxis()
	{
		if ((bool)weaponGO && GunAxes(weaponGO, out var space, out var item, out var _))
		{
			return (space, item);
		}
		if (!weaponGO)
		{
			return null;
		}
		return (weaponGO.transform, Vector3.forward);
	}

	public static bool GunAxes(GameObject w, out Transform space, out Vector3 barrel, out Vector3 top)
	{
		space = null;
		barrel = Vector3.forward;
		top = Vector3.up;
		if (!w)
		{
			return false;
		}
		Transform transform = null;
		float num = 0.001f;
		Transform[] componentsInChildren = w.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform2 in componentsInChildren)
		{
			if (transform2.name.StartsWith("SOCKET_Muzzle") && (bool)transform2.parent && transform2.localPosition.magnitude > num)
			{
				num = transform2.localPosition.magnitude;
				transform = transform2;
			}
		}
		if (!transform)
		{
			return false;
		}
		Vector3 localPosition = transform.localPosition;
		int num2 = ((!(Mathf.Abs(localPosition.x) >= Mathf.Abs(localPosition.y)) || !(Mathf.Abs(localPosition.x) >= Mathf.Abs(localPosition.z))) ? ((Mathf.Abs(localPosition.y) >= Mathf.Abs(localPosition.z)) ? 1 : 2) : 0);
		Vector3 zero = Vector3.zero;
		zero[num2] = Mathf.Sign(localPosition[num2]);
		Vector3 vector = localPosition - zero * localPosition[num2];
		Vector3 vector2;
		if (vector.magnitude > 0.001f)
		{
			int index = ((!(Mathf.Abs(vector.x) >= Mathf.Abs(vector.y)) || !(Mathf.Abs(vector.x) >= Mathf.Abs(vector.z))) ? ((Mathf.Abs(vector.y) >= Mathf.Abs(vector.z)) ? 1 : 2) : 0);
			vector2 = Vector3.zero;
			vector2[index] = Mathf.Sign(vector[index]);
		}
		else
		{
			vector2 = ((num2 == 1) ? Vector3.forward : Vector3.up);
		}
		space = transform.parent;
		barrel = zero;
		top = vector2;
		return true;
	}

	public Vector3 BarrelDir()
	{
		(Transform, Vector3)? tuple = barrel;
		if (!tuple.HasValue)
		{
			barrel = BarrelAxis();
		}
		tuple = barrel;
		if (tuple.HasValue)
		{
			(Transform, Vector3) valueOrDefault = tuple.GetValueOrDefault();
			var (transform, _) = valueOrDefault;
			if ((object)transform != null)
			{
				Vector3 item = valueOrDefault.Item2;
				if ((bool)transform)
				{
					return transform.TransformDirection(item).normalized;
				}
			}
		}
		return base.transform.forward;
	}

	public static string HolsterSlot(string cls)
	{
		if (!(cls == "pistola"))
		{
			if (!(cls == "submetralhadora"))
			{
				return "back";
			}
			return "sling";
		}
		return "thigh";
	}

	private bool TryManualHolster(Holstered h, out FBHolsterManualCalibrations.Entry entry)
	{
		if (!manualHolsterDatabase)
		{
			manualHolsterDatabase = Resources.Load<FBHolsterManualCalibrations>("FBHolsterManualCalibrations");
		}
		FBHolsterManualCalibrations fBHolsterManualCalibrations = manualHolsterDatabase;
		entry = null;
		if (!fBHolsterManualCalibrations || !h.go || !h.go.name.StartsWith("Coldre_"))
		{
			return false;
		}
		string text = skinId ?? string.Empty;
		if (h.manualDatabase != fBHolsterManualCalibrations || h.manualRevision != fBHolsterManualCalibrations.Revision || h.manualSkin != text)
		{
			fBHolsterManualCalibrations.TryFind(text, h.go.name.Substring("Coldre_".Length), out h.manual);
			h.manualDatabase = fBHolsterManualCalibrations;
			h.manualRevision = fBHolsterManualCalibrations.Revision;
			h.manualSkin = text;
			h.manualClearanceReady = false;
		}
		entry = h.manual;
		return entry != null;
	}

	internal static bool LocalBoundsOf(Renderer r, Matrix4x4 inv, out Bounds b)
	{
		b = default(Bounds);
		Mesh mesh = null;
		if (r is SkinnedMeshRenderer skinnedMeshRenderer)
		{
			mesh = skinnedMeshRenderer.sharedMesh;
		}
		else
		{
			MeshFilter component = r.GetComponent<MeshFilter>();
			if ((bool)component)
			{
				mesh = component.sharedMesh;
			}
		}
		Bounds bounds = (mesh ? mesh.bounds : r.bounds);
		Matrix4x4 matrix4x = (mesh ? (inv * r.transform.localToWorldMatrix) : inv);
		if (bounds.size.sqrMagnitude <= 0f)
		{
			return false;
		}
		for (int i = 0; i < 8; i++)
		{
			Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((((uint)i & (true ? 1u : 0u)) != 0) ? 1 : (-1), (((uint)i & 2u) != 0) ? 1 : (-1), (((uint)i & 4u) != 0) ? 1 : (-1)));
			Vector3 vector = matrix4x.MultiplyPoint3x4(point);
			if (i == 0)
			{
				b = new Bounds(vector, Vector3.zero);
			}
			else
			{
				b.Encapsulate(vector);
			}
		}
		return true;
	}

	private Holstered MakeHolster(WeaponDef def, GameObject model)
	{
		Holstered holstered = new Holstered
		{
			slot = HolsterSlot(def.cls),
			go = FBWeaponModel.Make(def, base.transform)
		};
		holstered.go.name = "Coldre_" + def.id;
		Collider[] componentsInChildren = holstered.go.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		holstered.go.transform.localScale = Vector3.one;
		holstered.go.transform.localRotation = Quaternion.identity;
		holstered.go.transform.localPosition = Vector3.zero;
		Transform transform = null;
		Transform transform2 = null;
		Transform[] componentsInChildren2 = holstered.go.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform3 in componentsInChildren2)
		{
			if (transform3.name.StartsWith("SOCKET_Muzzle"))
			{
				transform = transform3;
			}
			else if (transform3.name.StartsWith("SOCKET_Magazine"))
			{
				transform2 = transform3;
			}
		}
		Matrix4x4 worldToLocalMatrix = holstered.go.transform.worldToLocalMatrix;
		Bounds bb = default(Bounds);
		bool flag = true;
		Renderer[] componentsInChildren3 = holstered.go.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren3)
		{
			if (!Regex.IsMatch(renderer.name, "laser|beam|flash", RegexOptions.IgnoreCase) && LocalBoundsOf(renderer, worldToLocalMatrix, out var b))
			{
				if (flag)
				{
					bb = b;
					flag = false;
				}
				else
				{
					bb.Encapsulate(b);
				}
			}
		}
		holstered.c = bb.center;
		if (GunAxes(holstered.go, out var space, out var direction, out var top))
		{
			holstered.b = worldToLocalMatrix.MultiplyVector(space.TransformDirection(direction)).normalized;
			holstered.t = Vector3.ProjectOnPlane(worldToLocalMatrix.MultiplyVector(space.TransformDirection(top)), holstered.b).normalized;
		}
		else
		{
			holstered.b = (transform ? (worldToLocalMatrix.MultiplyPoint3x4(transform.position) - holstered.c).normalized : Vector3.forward);
			Vector3 vector = (transform2 ? (-(worldToLocalMatrix.MultiplyPoint3x4(transform2.position) - holstered.c)) : Vector3.up);
			holstered.t = Vector3.ProjectOnPlane(vector, holstered.b).normalized;
		}
		if (holstered.t.sqrMagnitude < 0.5f)
		{
			holstered.t = ((Mathf.Abs(Vector3.Dot(holstered.b, Vector3.up)) < 0.9f) ? Vector3.ProjectOnPlane(Vector3.up, holstered.b).normalized : Vector3.forward);
		}
		holstered.half = Sup(holstered.b);
		holstered.thick = Sup(Vector3.Cross(holstered.b, holstered.t).normalized);
		holstered.wide = Sup(holstered.t);
		return holstered;
		float Sup(Vector3 d)
		{
			return Mathf.Abs(bb.extents.x * d.x) + Mathf.Abs(bb.extents.y * d.y) + Mathf.Abs(bb.extents.z * d.z);
		}
	}

	public void SetHolsters(IEnumerable<(WeaponDef def, GameObject model)> list)
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (var (weaponDef, gameObject) in list)
		{
			if (weaponDef != null && (bool)gameObject)
			{
				hashSet.Add(weaponDef.id);
				if (!stowed.ContainsKey(weaponDef.id))
				{
					stowed[weaponDef.id] = MakeHolster(weaponDef, gameObject);
				}
			}
		}
		foreach (string item in new List<string>(stowed.Keys))
		{
			if (!hashSet.Contains(item))
			{
				UnityEngine.Object.Destroy(stowed[item].go);
				stowed.Remove(item);
			}
		}
	}

	public void StowCurrent(bool on)
	{
		stowCur = on;
	}

	private string BodyKey()
	{
		StringBuilder stringBuilder = new StringBuilder(64);
		stringBuilder.Append(Mathf.RoundToInt(base.transform.lossyScale.y * 1000f));
		SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
		{
			if ((bool)skinnedMeshRenderer && skinnedMeshRenderer.enabled && (bool)skinnedMeshRenderer.sharedMesh && !h_isWeapon(skinnedMeshRenderer.transform))
			{
				stringBuilder.Append('|').Append(skinnedMeshRenderer.sharedMesh.GetHashCode());
			}
		}
		return stringBuilder.ToString();
	}

	private static HumanBodyBones PartOf(HumanBodyBones b, HumanBodyBones chestHb)
	{
		switch (b)
		{
		case HumanBodyBones.Neck:
		case HumanBodyBones.LeftShoulder:
		case HumanBodyBones.RightShoulder:
			return chestHb;
		case HumanBodyBones.LeftEye:
		case HumanBodyBones.RightEye:
		case HumanBodyBones.Jaw:
			return HumanBodyBones.Head;
		case HumanBodyBones.LeftThumbProximal:
		case HumanBodyBones.LeftThumbIntermediate:
		case HumanBodyBones.LeftThumbDistal:
		case HumanBodyBones.LeftIndexProximal:
		case HumanBodyBones.LeftIndexIntermediate:
		case HumanBodyBones.LeftIndexDistal:
		case HumanBodyBones.LeftMiddleProximal:
		case HumanBodyBones.LeftMiddleIntermediate:
		case HumanBodyBones.LeftMiddleDistal:
		case HumanBodyBones.LeftRingProximal:
		case HumanBodyBones.LeftRingIntermediate:
		case HumanBodyBones.LeftRingDistal:
		case HumanBodyBones.LeftLittleProximal:
		case HumanBodyBones.LeftLittleIntermediate:
		case HumanBodyBones.LeftLittleDistal:
			return HumanBodyBones.LeftHand;
		default:
			if (b >= HumanBodyBones.RightThumbProximal && b <= HumanBodyBones.RightLittleDistal)
			{
				return HumanBodyBones.RightHand;
			}
			return b switch
			{
				HumanBodyBones.LeftToes => HumanBodyBones.LeftFoot, 
				HumanBodyBones.RightToes => HumanBodyBones.RightFoot, 
				_ => b, 
			};
		}
	}

	private static float Pct(List<float> l, float q)
	{
		if (l.Count == 0)
		{
			return 0f;
		}
		l.Sort();
		return l[Mathf.Clamp(Mathf.RoundToInt(q * (float)(l.Count - 1)), 0, l.Count - 1)];
	}

	private List<BodyPart> MeasureBind()
	{
		if (!an || !an.isHuman || !chest)
		{
			return null;
		}
		Dictionary<Transform, HumanBodyBones> dictionary = new Dictionary<Transform, HumanBodyBones>();
		for (int i = 0; i < 55; i++)
		{
			Transform boneTransform = an.GetBoneTransform((HumanBodyBones)i);
			if ((bool)boneTransform && !dictionary.ContainsKey(boneTransform))
			{
				dictionary[boneTransform] = (HumanBodyBones)i;
			}
		}
		if (!dictionary.TryGetValue(chest, out var value))
		{
			return null;
		}
		Dictionary<HumanBodyBones, List<Vector3>> dictionary2 = new Dictionary<HumanBodyBones, List<Vector3>>();
		Vector3? vector = null;
		Vector3? vector2 = null;
		Matrix4x4[] bpUse = null;
		Dictionary<Transform, int> idxUse = null;
		SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
		{
			if (!skinnedMeshRenderer || !skinnedMeshRenderer.enabled || !skinnedMeshRenderer.sharedMesh || !skinnedMeshRenderer.sharedMesh.isReadable || h_isWeapon(skinnedMeshRenderer.transform))
			{
				continue;
			}
			Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
			Transform[] bones = skinnedMeshRenderer.bones;
			Matrix4x4[] bp = sharedMesh.bindposes;
			BoneWeight[] boneWeights = sharedMesh.boneWeights;
			Vector3[] vertices = sharedMesh.vertices;
			if (boneWeights.Length != vertices.Length || bp.Length != bones.Length)
			{
				continue;
			}
			Dictionary<Transform, int> idx = new Dictionary<Transform, int>();
			for (int k = 0; k < bones.Length; k++)
			{
				if ((bool)bones[k] && !idx.ContainsKey(bones[k]))
				{
					idx[bones[k]] = k;
				}
			}
			int[] array = new int[bones.Length];
			for (int l = 0; l < bones.Length; l++)
			{
				Transform transform = bones[l];
				while ((bool)transform && transform != base.transform && !dictionary.ContainsKey(transform))
				{
					transform = transform.parent;
				}
				array[l] = (int)(((bool)transform && dictionary.TryGetValue(transform, out var value2)) ? PartOf(value2, value) : ((HumanBodyBones)(-1)));
			}
			if (!vector.HasValue && idx.ContainsKey(chest) && (bool)hips && (bool)neck && (bool)rArm && (bool)lArm && idx.ContainsKey(hips) && idx.ContainsKey(neck) && idx.ContainsKey(rArm) && idx.ContainsKey(lArm))
			{
				Vector3 normalized = (MP(neck) - MP(hips)).normalized;
				Vector3 normalized2 = Vector3.ProjectOnPlane(MP(rArm) - MP(lArm), normalized).normalized;
				if (normalized2.sqrMagnitude > 0.5f)
				{
					vector = normalized;
					vector2 = normalized2;
					bpUse = bp;
					idxUse = idx;
				}
			}
			int[] array2 = new int[bones.Length];
			for (int m = 0; m < bones.Length; m++)
			{
				array2[m] = -1;
				if (array[m] >= 0)
				{
					Transform boneTransform2 = an.GetBoneTransform((HumanBodyBones)array[m]);
					if ((bool)boneTransform2 && idx.TryGetValue(boneTransform2, out var value3))
					{
						array2[m] = value3;
					}
				}
			}
			for (int num = 0; num < vertices.Length; num++)
			{
				BoneWeight boneWeight = boneWeights[num];
				int num2 = boneWeight.boneIndex0;
				float num3 = boneWeight.weight0;
				if (boneWeight.weight1 > num3)
				{
					num2 = boneWeight.boneIndex1;
					num3 = boneWeight.weight1;
				}
				if (boneWeight.weight2 > num3)
				{
					num2 = boneWeight.boneIndex2;
					num3 = boneWeight.weight2;
				}
				if (boneWeight.weight3 > num3)
				{
					num2 = boneWeight.boneIndex3;
				}
				if (num2 >= 0 && num2 < bones.Length && array2[num2] >= 0)
				{
					HumanBodyBones key = (HumanBodyBones)array[num2];
					if (!dictionary2.TryGetValue(key, out var value4))
					{
						value4 = (dictionary2[key] = new List<Vector3>());
					}
					value4.Add(bp[array2[num2]].MultiplyPoint3x4(vertices[num]));
				}
			}
			Vector3 MP(Transform t)
			{
				return bp[idx[t]].inverse.GetColumn(3);
			}
		}
		if (!vector.HasValue || dictionary2.Count == 0)
		{
			return null;
		}
		List<BodyPart> list2 = new List<BodyPart>();
		float num4 = base.transform.lossyScale.y / 2.2f;
		tUpL = Local(value, vector.Value);
		tRtL = Local(value, vector2.Value);
		tAxes = tUpL.sqrMagnitude > 0.5f && tRtL.sqrMagnitude > 0.5f;
		Vector3 meshDir2 = Vector3.Cross(vector2.Value, vector.Value);
		Vector3 az = Local(HumanBodyBones.Head, meshDir2);
		Vector3 ay = Local(HumanBodyBones.Head, vector.Value);
		if (az.sqrMagnitude > 0.5f && ay.sqrMagnitude > 0.5f)
		{
			list2.Add(new BodyPart
			{
				hb = HumanBodyBones.Head,
				kind = 3,
				az = az,
				ay = ay
			});
		}
		foreach (KeyValuePair<HumanBodyBones, List<Vector3>> item in dictionary2)
		{
			HumanBodyBones key2 = item.Key;
			List<Vector3> value5 = item.Value;
			Transform boneTransform3 = an.GetBoneTransform(key2);
			if (!boneTransform3 || value5.Count < 12)
			{
				continue;
			}
			BodyPart bodyPart = new BodyPart
			{
				hb = key2
			};
			int NY;
			if (key2 == HumanBodyBones.Hips || key2 == HumanBodyBones.Spine || key2 == HumanBodyBones.Chest || key2 == HumanBodyBones.UpperChest)
			{
				bodyPart.kind = 0;
				bodyPart.ax = Local(key2, vector2.Value);
				bodyPart.ay = Local(key2, vector.Value);
				bodyPart.az = Local(key2, meshDir2);
				if (bodyPart.ax.sqrMagnitude < 0.5f)
				{
					continue;
				}
				float num5 = (bodyPart.cell = 0.03f * num4 / Mathf.Max(1E-06f, boneTransform3.lossyScale.x));
				float num6 = 1E+09f;
				float num7 = -1E+09f;
				float num8 = 1E+09f;
				float num9 = -1E+09f;
				float num10 = 1E+09f;
				float num11 = -1E+09f;
				foreach (Vector3 item2 in value5)
				{
					float b = Vector3.Dot(item2, bodyPart.ax);
					float b2 = Vector3.Dot(item2, bodyPart.ay);
					float b3 = Vector3.Dot(item2, bodyPart.az);
					num6 = Mathf.Min(num6, b);
					num7 = Mathf.Max(num7, b);
					num8 = Mathf.Min(num8, b2);
					num9 = Mathf.Max(num9, b2);
					num10 = Mathf.Min(num10, b3);
					num11 = Mathf.Max(num11, b3);
				}
				bodyPart.ox = 2 - Mathf.FloorToInt(num6 / num5);
				bodyPart.nx = Mathf.FloorToInt(num7 / num5) + bodyPart.ox + 3;
				bodyPart.oy = 2 - Mathf.FloorToInt(num8 / num5);
				bodyPart.ny = Mathf.FloorToInt(num9 / num5) + bodyPart.oy + 3;
				bodyPart.oz = 2 - Mathf.FloorToInt(num10 / num5);
				bodyPart.nz = Mathf.FloorToInt(num11 / num5) + bodyPart.oz + 3;
				int nx = bodyPart.nx;
				NY = bodyPart.ny;
				int nz = bodyPart.nz;
				if (nx * NY > 40000 || nz * NY > 40000)
				{
					continue;
				}
				float[] array3 = new float[nx * NY];
				float[] array4 = new float[nx * NY];
				float[] array5 = new float[nz * NY];
				float[] array6 = new float[nz * NY];
				foreach (Vector3 item3 in value5)
				{
					float num12 = Vector3.Dot(item3, bodyPart.ax);
					float num13 = Vector3.Dot(item3, bodyPart.ay);
					float num14 = Vector3.Dot(item3, bodyPart.az);
					int num15 = Mathf.FloorToInt(num12 / num5) + bodyPart.ox;
					int num16 = Mathf.FloorToInt(num13 / num5) + bodyPart.oy;
					int num17 = Mathf.FloorToInt(num14 / num5) + bodyPart.oz;
					if (num14 > 0f)
					{
						array3[num15 * NY + num16] = Mathf.Max(array3[num15 * NY + num16], num14);
					}
					else
					{
						array4[num15 * NY + num16] = Mathf.Max(array4[num15 * NY + num16], 0f - num14);
					}
					if (num12 > 0f)
					{
						array5[num17 * NY + num16] = Mathf.Max(array5[num17 * NY + num16], num12);
					}
					else
					{
						array6[num17 * NY + num16] = Mathf.Max(array6[num17 * NY + num16], 0f - num12);
					}
				}
				bodyPart.fF = Dil(array3, nx);
				bodyPart.fB = Dil(array4, nx);
				bodyPart.sR = Dil(array5, nz);
				bodyPart.sL = Dil(array6, nz);
				list2.Add(bodyPart);
				continue;
			}
			HumanBodyBones humanBodyBones;
			switch (key2)
			{
			case HumanBodyBones.Head:
			{
				Vector3 zero = Vector3.zero;
				foreach (Vector3 item4 in value5)
				{
					zero += item4;
				}
				zero /= (float)value5.Count;
				List<float> list3 = new List<float>(value5.Count);
				foreach (Vector3 item5 in value5)
				{
					list3.Add((item5 - zero).magnitude);
				}
				bodyPart.kind = 2;
				bodyPart.c = zero;
				bodyPart.r0 = Pct(list3, 0.97f);
				list2.Add(bodyPart);
				continue;
			}
			case HumanBodyBones.LeftUpperArm:
				humanBodyBones = HumanBodyBones.LeftLowerArm;
				break;
			case HumanBodyBones.RightUpperArm:
				humanBodyBones = HumanBodyBones.RightLowerArm;
				break;
			case HumanBodyBones.LeftLowerArm:
				humanBodyBones = HumanBodyBones.LeftHand;
				break;
			case HumanBodyBones.RightLowerArm:
				humanBodyBones = HumanBodyBones.RightHand;
				break;
			case HumanBodyBones.LeftUpperLeg:
				humanBodyBones = HumanBodyBones.LeftLowerLeg;
				break;
			case HumanBodyBones.RightUpperLeg:
				humanBodyBones = HumanBodyBones.RightLowerLeg;
				break;
			case HumanBodyBones.LeftLowerLeg:
				humanBodyBones = HumanBodyBones.LeftFoot;
				break;
			case HumanBodyBones.RightLowerLeg:
				humanBodyBones = HumanBodyBones.RightFoot;
				break;
			default:
				humanBodyBones = HumanBodyBones.LastBone;
				break;
			}
			HumanBodyBones humanBodyBones2 = humanBodyBones;
			Transform boneTransform4 = an.GetBoneTransform(key2);
			Transform transform2 = ((humanBodyBones2 != HumanBodyBones.LastBone) ? an.GetBoneTransform(humanBodyBones2) : null);
			Vector3 vector3;
			if ((bool)transform2 && idxUse.TryGetValue(boneTransform4, out var value6) && idxUse.TryGetValue(transform2, out var value7))
			{
				vector3 = bpUse[value6].MultiplyPoint3x4(bpUse[value7].inverse.GetColumn(3));
			}
			else
			{
				if (key2 != HumanBodyBones.LeftHand && key2 != HumanBodyBones.RightHand && key2 != HumanBodyBones.LeftFoot && key2 != HumanBodyBones.RightFoot)
				{
					continue;
				}
				Vector3 zero2 = Vector3.zero;
				foreach (Vector3 item6 in value5)
				{
					zero2 += item6;
				}
				Vector3 normalized3 = (zero2 / value5.Count).normalized;
				if (normalized3.sqrMagnitude < 0.5f)
				{
					continue;
				}
				List<float> list4 = new List<float>(value5.Count);
				foreach (Vector3 item7 in value5)
				{
					list4.Add(Vector3.Dot(item7, normalized3));
				}
				vector3 = normalized3 * Pct(list4, 0.97f);
				humanBodyBones2 = HumanBodyBones.LastBone;
			}
			if (vector3.sqrMagnitude < 1E-08f)
			{
				continue;
			}
			List<float> list5 = new List<float>();
			List<float> list6 = new List<float>();
			float sqrMagnitude = vector3.sqrMagnitude;
			foreach (Vector3 item8 in value5)
			{
				float num18 = Mathf.Clamp01(Vector3.Dot(item8, vector3) / sqrMagnitude);
				float magnitude = (item8 - vector3 * num18).magnitude;
				((num18 < 0.5f) ? list5 : list6).Add(magnitude);
			}
			bodyPart.kind = 1;
			bodyPart.endHb = humanBodyBones2;
			bodyPart.end = vector3;
			bodyPart.r0 = Pct((list5.Count > 0) ? list5 : list6, 0.97f);
			bodyPart.r1 = Pct((list6.Count > 0) ? list6 : list5, 0.97f);
			list2.Add(bodyPart);
			float[] Dil(float[] g, int n0)
			{
				float[] array7 = new float[g.Length];
				for (int num19 = 0; num19 < n0; num19++)
				{
					for (int num20 = 0; num20 < NY; num20++)
					{
						float num21 = 0f;
						for (int num22 = -1; num22 <= 1; num22++)
						{
							for (int num23 = -1; num23 <= 1; num23++)
							{
								int num24 = num19 + num22;
								int num25 = num20 + num23;
								if (num24 >= 0 && num24 < n0 && num25 >= 0 && num25 < NY)
								{
									num21 = Mathf.Max(num21, g[num24 * NY + num25]);
								}
							}
						}
						array7[num19 * NY + num20] = num21;
					}
				}
				return array7;
			}
		}
		return list2;
		Vector3 Local(HumanBodyBones hb, Vector3 meshDir)
		{
			Transform boneTransform5 = an.GetBoneTransform(hb);
			if (!boneTransform5 || !idxUse.TryGetValue(boneTransform5, out var value8))
			{
				return Vector3.zero;
			}
			return bpUse[value8].MultiplyVector(meshDir).normalized;
		}
	}

	private bool EnsureBody()
	{
		if (body != null)
		{
			return true;
		}
		if (bodyTried || !an || !an.isHuman)
		{
			return false;
		}
		bodyTried = true;
		Stopwatch stopwatch = Stopwatch.StartNew();
		string key = BodyKey();
		if (!bodyCache.TryGetValue(key, out var value))
		{
			value = MeasureBind();
			bodyCache[key] = value;
			MeasureN++;
		}
		else if (value != null)
		{
			InitTorsoAxesFromCache(value);
		}
		MeasureMs += stopwatch.Elapsed.TotalMilliseconds;
		if (value == null)
		{
			return false;
		}
		body = value;
		bodyBone = new Transform[value.Count];
		bodyEnd = new Transform[value.Count];
		for (int i = 0; i < value.Count; i++)
		{
			bodyBone[i] = an.GetBoneTransform(value[i].hb);
			bodyEnd[i] = ((value[i].endHb != HumanBodyBones.LastBone) ? an.GetBoneTransform(value[i].endHb) : null);
		}
		return true;
	}

	private void InitTorsoAxesFromCache(List<BodyPart> parts)
	{
		if (tAxes || !an || !chest)
		{
			return;
		}
		foreach (BodyPart part in parts)
		{
			if (part.kind == 0 && an.GetBoneTransform(part.hb) == chest)
			{
				tUpL = part.ay;
				tRtL = part.ax;
				tAxes = true;
				break;
			}
		}
	}

	private void InitTorsoAxes()
	{
		if (!tAxes && (bool)chest && (bool)hips && (bool)neck && (!EnsureBody() || !tAxes))
		{
			Vector3 normalized = (neck.position - hips.position).normalized;
			Vector3 vector = (((bool)rArm && (bool)lArm) ? Vector3.ProjectOnPlane(rArm.position - lArm.position, normalized) : Vector3.ProjectOnPlane(base.transform.right, normalized));
			if (vector.sqrMagnitude < 1E-06f)
			{
				vector = Vector3.ProjectOnPlane(base.transform.right, normalized);
			}
			tUpL = chest.InverseTransformDirection(normalized).normalized;
			tRtL = chest.InverseTransformDirection(vector.normalized).normalized;
			tAxes = true;
		}
	}

	public void TorsoAxes(out Vector3 up, out Vector3 fw, out Vector3 right)
	{
		InitTorsoAxes();
		if (!tAxes)
		{
			up = base.transform.up;
			fw = base.transform.forward;
			right = base.transform.right;
		}
		else
		{
			up = chest.TransformDirection(tUpL).normalized;
			right = Vector3.ProjectOnPlane(chest.TransformDirection(tRtL), up).normalized;
			fw = Vector3.Cross(right, up);
		}
	}

	public bool ThighAxes(out Vector3 dn, out Vector3 side, out Vector3 fwd)
	{
		dn = -base.transform.up;
		side = base.transform.right;
		fwd = base.transform.forward;
		if (!rThigh || !rKnee)
		{
			return false;
		}
		if (!lThighB && (bool)an && an.isHuman)
		{
			lThighB = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		}
		dn = (rKnee.position - rThigh.position).normalized;
		Vector3 vector = (lThighB ? (rThigh.position - lThighB.position) : base.transform.right);
		side = Vector3.ProjectOnPlane(vector, dn).normalized;
		if (side.sqrMagnitude < 0.5f)
		{
			side = Vector3.ProjectOnPlane(base.transform.right, dn).normalized;
		}
		fwd = Vector3.Cross(dn, side);
		return true;
	}

	private float Solve(Vector3 C0, Vector3 Bd, Vector3 Td, Vector3 n, float L, float W, float clear, int mode = 0)
	{
		float num = 0f;
		float num2 = Mathf.Sqrt(L * L + W * W);
		for (int i = 0; i < body.Count; i++)
		{
			BodyPart bodyPart = body[i];
			Transform transform = bodyBone[i];
			if (!transform || bodyPart.kind == 3)
			{
				continue;
			}
			float x = transform.lossyScale.x;
			if ((mode == 1 && bodyPart.kind != 0) || (mode == 2 && bodyPart.kind != 0 && bodyPart.hb != HumanBodyBones.RightUpperLeg))
			{
				continue;
			}
			if (bodyPart.kind == 0)
			{
				Matrix4x4 worldToLocalMatrix = transform.worldToLocalMatrix;
				Vector3 vector = worldToLocalMatrix.MultiplyPoint3x4(C0);
				Vector3 vector2 = worldToLocalMatrix.MultiplyVector(Bd) * L;
				Vector3 vector3 = worldToLocalMatrix.MultiplyVector(Td) * W;
				Vector3 lhs = worldToLocalMatrix.MultiplyVector(n);
				float magnitude = lhs.magnitude;
				if (magnitude < 1E-09f)
				{
					continue;
				}
				lhs /= magnitude;
				float num3 = Vector3.Dot(lhs, bodyPart.az);
				float num4 = Vector3.Dot(lhs, bodyPart.ax);
				bool flag = Mathf.Abs(num3) >= Mathf.Abs(num4);
				float num5 = (flag ? num3 : num4);
				if (Mathf.Abs(num5) < 0.3f)
				{
					continue;
				}
				float[] array = ((!flag) ? ((num5 > 0f) ? bodyPart.sR : bodyPart.sL) : ((num5 > 0f) ? bodyPart.fF : bodyPart.fB));
				float num6 = clear * magnitude;
				float num7 = Mathf.Abs(num5);
				float cell = bodyPart.cell;
				int ny = bodyPart.ny;
				int num8 = (flag ? bodyPart.nx : bodyPart.nz);
				for (int j = -7; j <= 7; j++)
				{
					for (int k = -2; k <= 2; k++)
					{
						Vector3 lhs2 = vector + vector2 * ((float)j / 7f) + vector3 * ((float)k / 2f);
						float num9 = Vector3.Dot(lhs2, bodyPart.ax);
						float num10 = Vector3.Dot(lhs2, bodyPart.ay);
						float num11 = Vector3.Dot(lhs2, bodyPart.az);
						int num12 = Mathf.FloorToInt(num10 / cell) + bodyPart.oy;
						if (num12 < 0 || num12 >= ny)
						{
							continue;
						}
						int num13 = (flag ? (Mathf.FloorToInt(num9 / cell) + bodyPart.ox) : (Mathf.FloorToInt(num11 / cell) + bodyPart.oz));
						if (num13 < 0 || num13 >= num8)
						{
							continue;
						}
						float num14 = array[num13 * ny + num12];
						if (!(num14 <= 0f))
						{
							float num15 = ((!flag) ? ((num5 > 0f) ? num9 : (0f - num9)) : ((num5 > 0f) ? num11 : (0f - num11)));
							float num16 = (num14 + num6 - num15) / num7;
							if (num16 > 0f)
							{
								num = Mathf.Max(num, num16 / magnitude);
							}
						}
					}
				}
				continue;
			}
			Vector3 vector5;
			Vector3 vector4;
			float a;
			float b;
			if (bodyPart.kind == 2)
			{
				vector5 = (vector4 = transform.TransformPoint(bodyPart.c));
				a = (b = bodyPart.r0 * x);
			}
			else
			{
				vector5 = transform.position;
				vector4 = (bodyEnd[i] ? bodyEnd[i].position : transform.TransformPoint(bodyPart.end));
				a = bodyPart.r0 * x;
				b = bodyPart.r1 * x;
			}
			Vector3 vector6 = vector4 - vector5;
			float num17 = Mathf.Max(1E-08f, vector6.sqrMagnitude);
			float num18 = Mathf.Clamp01(Vector3.Dot(C0 - vector5, vector6) / num17);
			if ((C0 - (vector5 + vector6 * num18)).magnitude > num2 + Mathf.Max(a, b) + clear + 0.6f * scaleK)
			{
				continue;
			}
			for (int l = -7; l <= 7; l++)
			{
				for (int m = -2; m <= 2; m++)
				{
					Vector3 vector7 = C0 + Bd * (L * (float)l / 7f) + Td * (W * (float)m / 2f);
					float num19 = Mathf.Clamp01(Vector3.Dot(vector7 - vector5, vector6) / num17);
					float num20 = Mathf.Lerp(a, b, num19) + clear;
					Vector3 lhs3 = vector7 - (vector5 + vector6 * num19);
					float num21 = Vector3.Dot(lhs3, n);
					float num22 = lhs3.sqrMagnitude - num21 * num21;
					if (!(num22 >= num20 * num20))
					{
						float num23 = 0f - num21 + Mathf.Sqrt(num20 * num20 - num22);
						if (num23 > 0f)
						{
							num = Mathf.Max(num, num23);
						}
					}
				}
			}
		}
		return num;
	}

	public float FrontPenetration(Vector3 p, float pad)
	{
		if (!EnsureBody())
		{
			return 0f;
		}
		float num = 0f;
		for (int i = 0; i < body.Count; i++)
		{
			BodyPart bodyPart = body[i];
			Transform transform = bodyBone[i];
			if (bodyPart.kind != 0 || !transform)
			{
				continue;
			}
			float x = transform.lossyScale.x;
			Vector3 lhs = transform.InverseTransformPoint(p);
			int num2 = Mathf.FloorToInt(Vector3.Dot(lhs, bodyPart.ax) / bodyPart.cell) + bodyPart.ox;
			int num3 = Mathf.FloorToInt(Vector3.Dot(lhs, bodyPart.ay) / bodyPart.cell) + bodyPart.oy;
			if (num2 >= 0 && num2 < bodyPart.nx && num3 >= 0 && num3 < bodyPart.ny)
			{
				float num4 = bodyPart.fF[num2 * bodyPart.ny + num3];
				if (!(num4 <= 0f))
				{
					num = Mathf.Max(num, (num4 + pad / x - Vector3.Dot(lhs, bodyPart.az)) * x);
				}
			}
		}
		return Mathf.Max(0f, num);
	}

	// Quanto um ponto está dentro do corpo, pelo mesmo modelo do corpo medido da malha que o FrontPenetration usa:
	// tronco (frente, costas e lados de cada osso), cabeça e, com limbs, braços e coxas/canelas (antebraços e mãos ficam
	// de fora porque seguram a arma). Devolve a profundidade em metros (0 = fora), a direção para sair pelo lado mais
	// perto (no tronco: frente, direita ou esquerda, nunca pelas costas) e a parte (0 tronco, 1 braço/perna, 2 cabeça).
	// osso da parte mais funda achada pelo último BodyDepth (para o relatório do teste)
	[NonSerialized]
	public HumanBodyBones DepthBone;

	// Cópia das matrizes do tronco e da cabeça para medir muitos pontos de uma vez (FBRig.KeepOutOfTorso) sem chamar
	// a Unity a cada ponto: BodyDepth com 150 pontos x 7 passadas custava ~1 ms por personagem (20 bonecos = 44 FPS).
	// Vale enquanto tronco e cabeça não se mexem (o braço pode mexer: não é filho deles na medida).
	private struct FastPart
	{
		public BodyPart p;

		public Matrix4x4 w2l;

		public Matrix4x4 l2w;

		public float scale;

		public Vector3 c;

		public float r;
	}

	private FastPart[] fast;

	private int fastN;

	public bool BodyFastBegin()
	{
		fastN = 0;
		if (!EnsureBody())
		{
			return false;
		}
		if (fast == null || fast.Length < body.Count)
		{
			fast = new FastPart[body.Count];
		}
		for (int i = 0; i < body.Count; i++)
		{
			BodyPart bodyPart = body[i];
			Transform transform = bodyBone[i];
			if (!transform || (bodyPart.kind != 0 && bodyPart.kind != 2))
			{
				continue;
			}
			FastPart fastPart = default(FastPart);
			fastPart.p = bodyPart;
			fastPart.scale = transform.lossyScale.x;
			if (bodyPart.kind == 0)
			{
				fastPart.w2l = transform.worldToLocalMatrix;
				fastPart.l2w = transform.localToWorldMatrix;
			}
			else
			{
				fastPart.c = transform.TransformPoint(bodyPart.c);
				fastPart.r = bodyPart.r0 * fastPart.scale;
			}
			fast[fastN++] = fastPart;
		}
		return fastN > 0;
	}

	// igual ao BodyDepth sem braços e pernas, usando a cópia feita no BodyFastBegin
	public float BodyDepthFast(Vector3 p, out Vector3 outDir)
	{
		outDir = Vector3.zero;
		float best = 0f;
		for (int i = 0; i < fastN; i++)
		{
			BodyPart bodyPart = fast[i].p;
			if (bodyPart.kind == 0)
			{
				Vector3 lhs = fast[i].w2l.MultiplyPoint3x4(p);
				float lx = Vector3.Dot(lhs, bodyPart.ax);
				float ly = Vector3.Dot(lhs, bodyPart.ay);
				float lz = Vector3.Dot(lhs, bodyPart.az);
				int ix = Mathf.FloorToInt(lx / bodyPart.cell) + bodyPart.ox;
				int iy = Mathf.FloorToInt(ly / bodyPart.cell) + bodyPart.oy;
				int iz = Mathf.FloorToInt(lz / bodyPart.cell) + bodyPart.oz;
				if (ix < 0 || ix >= bodyPart.nx || iy < 0 || iy >= bodyPart.ny || iz < 0 || iz >= bodyPart.nz)
				{
					continue;
				}
				float f = bodyPart.fF[ix * bodyPart.ny + iy];
				float b = bodyPart.fB[ix * bodyPart.ny + iy];
				float r = bodyPart.sR[iz * bodyPart.ny + iy];
				float l = bodyPart.sL[iz * bodyPart.ny + iy];
				if (f <= 0f || b <= 0f || r <= 0f || l <= 0f || lz >= f || lz <= 0f - b || lx >= r || lx <= 0f - l)
				{
					continue;
				}
				float d = f - lz;
				Vector3 dir = bodyPart.az;
				if (r - lx < d)
				{
					d = r - lx;
					dir = bodyPart.ax;
				}
				if (lx + l < d)
				{
					d = lx + l;
					dir = -bodyPart.ax;
				}
				d *= fast[i].scale;
				if (d > best)
				{
					best = d;
					outDir = fast[i].l2w.MultiplyVector(dir).normalized;
					DepthBone = bodyPart.hb;
				}
			}
			else
			{
				Vector3 v = p - fast[i].c;
				float d2 = fast[i].r - v.magnitude;
				if (d2 > best)
				{
					best = d2;
					outDir = ((v.sqrMagnitude > 1E-08f) ? v.normalized : base.transform.right);
					DepthBone = bodyPart.hb;
				}
			}
		}
		return best;
	}

	public float BodyDepth(Vector3 p, bool limbs, out Vector3 outDir, out int part)
	{
		outDir = Vector3.zero;
		part = -1;
		if (!EnsureBody())
		{
			return 0f;
		}
		float best = 0f;
		for (int i = 0; i < body.Count; i++)
		{
			BodyPart bodyPart = body[i];
			Transform transform = bodyBone[i];
			if (!transform)
			{
				continue;
			}
			float x = transform.lossyScale.x;
			if (bodyPart.kind == 0)
			{
				Vector3 lhs = transform.InverseTransformPoint(p);
				float lx = Vector3.Dot(lhs, bodyPart.ax);
				float ly = Vector3.Dot(lhs, bodyPart.ay);
				float lz = Vector3.Dot(lhs, bodyPart.az);
				int ix = Mathf.FloorToInt(lx / bodyPart.cell) + bodyPart.ox;
				int iy = Mathf.FloorToInt(ly / bodyPart.cell) + bodyPart.oy;
				int iz = Mathf.FloorToInt(lz / bodyPart.cell) + bodyPart.oz;
				if (ix < 0 || ix >= bodyPart.nx || iy < 0 || iy >= bodyPart.ny || iz < 0 || iz >= bodyPart.nz)
				{
					continue;
				}
				float f = bodyPart.fF[ix * bodyPart.ny + iy];
				float b = bodyPart.fB[ix * bodyPart.ny + iy];
				float r = bodyPart.sR[iz * bodyPart.ny + iy];
				float l = bodyPart.sL[iz * bodyPart.ny + iy];
				if (f <= 0f || b <= 0f || r <= 0f || l <= 0f || lz >= f || lz <= 0f - b || lx >= r || lx <= 0f - l)
				{
					continue;
				}
				float d = f - lz;
				Vector3 dir = bodyPart.az;
				if (r - lx < d)
				{
					d = r - lx;
					dir = bodyPart.ax;
				}
				if (lx + l < d)
				{
					d = lx + l;
					dir = -bodyPart.ax;
				}
				d *= x;
				if (d > best)
				{
					best = d;
					outDir = transform.TransformDirection(dir).normalized;
					part = 0;
					DepthBone = bodyPart.hb;
				}
				continue;
			}
			Vector3 a;
			Vector3 e;
			float r0;
			float r1;
			if (bodyPart.kind == 2)
			{
				a = (e = transform.TransformPoint(bodyPart.c));
				r0 = (r1 = bodyPart.r0 * x);
			}
			else
			{
				if (bodyPart.kind != 1 || !limbs || bodyPart.hb == HumanBodyBones.LeftLowerArm || bodyPart.hb == HumanBodyBones.RightLowerArm || bodyPart.hb == HumanBodyBones.LeftHand || bodyPart.hb == HumanBodyBones.RightHand)
				{
					continue;
				}
				a = transform.position;
				e = (bodyEnd[i] ? bodyEnd[i].position : transform.TransformPoint(bodyPart.end));
				r0 = bodyPart.r0 * x;
				r1 = bodyPart.r1 * x;
			}
			Vector3 seg = e - a;
			float t = Mathf.Clamp01(Vector3.Dot(p - a, seg) / Mathf.Max(1E-08f, seg.sqrMagnitude));
			Vector3 v = p - (a + seg * t);
			float d2 = Mathf.Lerp(r0, r1, t) - v.magnitude;
			if (d2 > best)
			{
				best = d2;
				outDir = ((v.sqrMagnitude > 1E-08f) ? v.normalized : base.transform.right);
				part = ((bodyPart.kind == 2) ? 2 : 1);
				DepthBone = bodyPart.hb;
			}
		}
		return best;
	}

	public bool HeadAxes(out Vector3 fwL, out Vector3 upL)
	{
		fwL = (upL = Vector3.zero);
		if (!EnsureBody())
		{
			return false;
		}
		foreach (BodyPart item in body)
		{
			if (item.kind == 3)
			{
				fwL = item.az;
				upL = item.ay;
				return true;
			}
		}
		return false;
	}

	private bool h_isWeapon(Transform t)
	{
		while ((bool)t && t != base.transform)
		{
			if (((bool)weaponGO && t == weaponGO.transform) || t.name.StartsWith("Coldre_"))
			{
				return true;
			}
			t = t.parent;
		}
		return false;
	}

	public void HolsterRemeasure()
	{
		body = null;
		bodyTried = false;
	}

	private void PlaceHolsters()
	{
		bool flag = (drawHid || stowCur) && weapon != null && (bool)curModel;
		if (flag && cur == null)
		{
			cur = MakeHolster(weapon, curModel);
		}
		if (cur != null)
		{
			cur.go.SetActive(flag && alive && !Sitting);
		}
		if ((stowed.Count == 0 && cur == null) || !chest || !hips || !neck)
		{
			return;
		}
		float num = base.transform.lossyScale.y / 2.2f;
		float holsterWorldScale = HolsterWorldScale;
		bool flag2 = EnsureBody();
		TorsoAxes(out var up, out var fw, out var right);
		Vector3 dn;
		Vector3 side;
		Vector3 fwd;
		bool flag3 = ThighAxes(out dn, out side, out fwd);
		float value = (((bool)rArm && (bool)lArm) ? Vector3.Distance(rArm.position, lArm.position) : (0.4f * num));
		float value2 = (((bool)rThigh && (bool)lThighB) ? Vector3.Distance(rThigh.position, lThighB.position) : (0.2f * num));
		value = Mathf.Clamp(value, 0.2f * num, 0.9f * num);
		value2 = Mathf.Clamp(value2, 0.1f * num, 0.45f * num);
		List<Holstered> list = new List<Holstered>(stowed.Values);
		if (cur != null && flag)
		{
			list.Add(cur);
		}
		float num2 = 0.02f * num;
		float deltaTime = Time.deltaTime;
		Camera main = Camera.main;
		bool flag4 = isPlayer || !main || !((main.transform.position - base.transform.position).sqrMagnitude > 625f) || ((holsterFrame++ + GetHashCode()) & 3) == 0;
		Stopwatch stopwatch = Stopwatch.StartNew();
		foreach (Holstered item in list)
		{
			bool flag5 = sys == "IE_Downed_Treated" || sys == "IE_Downed_GetUp";
			bool flag6 = sys == "FF_SYS_KO_Revive";
			bool flag7 = alive && !Sitting && !vehiclePose && !flag5 && ((!flag6 && !knocked) || !(item.slot == "sling")) && dance == null;
			if (item != cur)
			{
				item.go.SetActive(flag7);
			}
			else
			{
				item.go.SetActive(flag7 && item.go.activeSelf);
			}
			if (!flag7)
			{
				item.dOk = false;
				continue;
			}
			float l = item.half * holsterWorldScale;
			float num3 = item.thick * holsterWorldScale;
			float w = item.wide * holsterWorldScale;
			Vector3 vector;
			Transform transform;
			Vector3 vector3;
			Vector3 vector4;
			Vector3 vector2;
			float num4;
			if (item.slot == "thigh" && flag3)
			{
				vector = dn;
				vector2 = fwd;
				transform = rThigh;
				vector3 = side;
				vector4 = rThigh.position + dn * 0.2f * num - fwd * 0.02f * num;
				num4 = 0.135f * num;
			}
			else if (item.slot == "sling")
			{
				vector = (-up * 0.55f - right * 0.84f).normalized;
				vector2 = Vector3.Cross(vector, fw);
				transform = chest;
				vector3 = fw;
				vector4 = chest.position - up * 0.2f * num - right * 0.02f * num;
				num4 = 0.2f * num;
			}
			else
			{
				vector = (-up * 0.72f - right * 0.7f).normalized;
				vector2 = -Vector3.Cross(vector, fw);
				transform = chest;
				vector3 = -fw;
				vector4 = chest.position - up * 0.04f * num;
				num4 = 0.2f * num;
			}
			vector2 = Vector3.ProjectOnPlane(vector2, vector).normalized;
			bool flag8 = item.slot == "thigh" && flag3;
			float max = (flag8 ? (0.6f * value2) : (0.5f * value)) + num3 + num2;
			num4 = (flag8 ? (0.45f * value2) : (0.3f * value)) + num3 + num2;
			if (TryManualHolster(item, out var entry))
			{
				if (item.go.transform.parent != transform)
				{
					item.go.transform.SetParent(transform, worldPositionStays: false);
				}
				item.go.transform.localPosition = entry.localPosition;
				item.go.transform.localRotation = entry.localRotation;
				item.go.transform.localScale = entry.localScale;
				item.dOk = false;
				if (entry.preventClipping && flag2)
				{
					if (flag4 || !item.manualClearanceReady)
					{
						float num5 = Mathf.Abs(item.go.transform.lossyScale.x);
						Vector3 c = item.go.transform.TransformPoint(item.c);
						Vector3 normalized = item.go.transform.TransformDirection(item.b).normalized;
						Vector3 normalized2 = item.go.transform.TransformDirection(item.t).normalized;
						item.manualClearance = Solve(c, normalized, normalized2, vector3, item.half * num5, item.wide * num5, item.thick * num5 + num2);
						item.manualClearanceReady = true;
						SolveN++;
					}
					if (item.manualClearance > 0f)
					{
						item.go.transform.position += vector3 * item.manualClearance;
					}
				}
				else
				{
					item.manualClearanceReady = false;
				}
			}
			else
			{
				if (flag2 && (flag4 || !item.dOk))
				{
					float num6 = Solve(vector4, vector, vector2, vector3, l, w, num3 + num2, (!flag8) ? 1 : 2);
					num6 = ((float.IsNaN(num6) || float.IsInfinity(num6)) ? num4 : Mathf.Clamp(num6, num3 + num2, max));
					SolveN++;
					item.d = ((!item.dOk || num6 > item.d) ? num6 : Mathf.Lerp(item.d, num6, 1f - Mathf.Exp((0f - deltaTime) * 4f)));
					item.dOk = true;
				}
				else if (!flag2)
				{
					item.d = num4;
				}
				Vector3 vector5 = vector4 + vector3 * item.d;
				if (item.go.transform.parent != transform)
				{
					item.go.transform.SetParent(transform, worldPositionStays: true);
				}
				Quaternion quaternion = Quaternion.LookRotation(vector, vector2) * Quaternion.Inverse(Quaternion.LookRotation(item.b, item.t));
				item.go.transform.rotation = quaternion;
				item.go.transform.localScale = Vector3.one * holsterWorldScale / Mathf.Max(1E-05f, transform.lossyScale.x);
				item.go.transform.position = vector5 - quaternion * (item.c * holsterWorldScale);
			}
		}
		SolveMs += stopwatch.Elapsed.TotalMilliseconds;
	}

	public void DrawAnim()
	{
		if (weapon == null || !FBAnimProfile.Holsters.TryGetValue(HolsterSlot(weapon.cls), out var value))
		{
			return;
		}
		drawSpd = ((value.drawSpeed > 0.1f) ? value.drawSpeed : 1f);
		if (PlayEmote(value.draw, drawSpd))
		{
			drawing = true;
			drawT = 0f;
			drawAt = value.grab;
			drawLen = FBRegistry.I.Clip(value.draw).length;
			drawHid = true;
			if ((bool)weaponGO)
			{
				weaponGO.SetActive(value: false);
			}
		}
	}

	public float HolsterAnim()
	{
		if (weapon == null || !alive || !FBAnimProfile.Holsters.TryGetValue(HolsterSlot(weapon.cls), out var value))
		{
			return 0f;
		}
		drawSpd = ((value.putSpeed > 0.1f) ? value.putSpeed : 1f);
		if (!PlayEmote(value.put, drawSpd))
		{
			return 0f;
		}
		drawing = false;
		drawT = 0f;
		drawAt = value.rel;
		drawLen = FBRegistry.I.Clip(value.put).length;
		return value.rel / drawSpd + 0.02f;
	}

	public string RescuePose(bool on, float dt)
	{
		if (on)
		{
			rescueEndT = -1f;
			if (rescueT < 0f)
			{
				rescueT = 0f;
			}
			rescueT += dt;
			if (!Has("IE_Revive_Start") || !(rescueT < Len("IE_Revive_Start")))
			{
				if (!Has("IE_Revive_Loop"))
				{
					return null;
				}
				return "IE_Revive_Loop";
			}
			return "IE_Revive_Start";
		}
		if (rescueT >= 0f)
		{
			rescueT = -1f;
			rescueEndT = 0f;
		}
		if (rescueEndT >= 0f)
		{
			rescueEndT += dt;
			if (rescueEndT < Len("IE_Revive_End") - 0.05f && Has("IE_Revive_End"))
			{
				return "IE_Revive_End";
			}
			rescueEndT = -1f;
		}
		return null;
		static float Len(string c)
		{
			AnimationClip animationClip = (FBRegistry.I ? FBRegistry.I.Clip(c) : null);
			if (!animationClip)
			{
				return 0f;
			}
			return animationClip.length;
		}
	}

	public bool PlayEmote(string clip, float speed = 1f)
	{
		if (LEU < 0 || !Has(clip))
		{
			return false;
		}
		emoteSpd = ((speed > 0.01f) ? speed : 1f);
		SF("EmoteSpeed", emoteSpd);
		emoteB = !emoteB;
		SetOverride(emoteB ? "EMOTE_UPPER_PLACEHOLDER_B" : "EMOTE_UPPER_PLACEHOLDER", clip);
		an.CrossFadeInFixedTime(emoteB ? "EmoteB" : "Emote", (emoteT >= 0f) ? 0.12f : 0f, LEU, 0f);
		emoteT = 0f;
		emoteLen = FBRegistry.I.Clip(clip).length;
		return true;
	}

	private void PlayFull(string clip, float fade)
	{
		if (!(fullClip == clip))
		{
			fullClip = clip;
			if (clip != null && LE >= 0)
			{
				fullB = !fullB;
				SetOverride(fullB ? "EMOTE_FULL_PLACEHOLDER_B" : "EMOTE_FULL_PLACEHOLDER", clip);
				an.CrossFadeInFixedTime(fullB ? "EmoteB" : "Emote", (danW > 0.05f) ? fade : 0f, LE, 0f);
			}
		}
	}

	public bool PlayDance(string clip, string sitEnter = null, string sitOut = null)
	{
		if (LE < 0 || !Has(clip) || !alive)
		{
			return false;
		}
		StopWaitPose();
		AnimationClip animationClip = FBRegistry.I.Clip(clip);
		bool num = !clip.StartsWith("FF_SYS_") && !clip.StartsWith("REB_");
		string text = clip;
		float len = (num ? Mathf.Max(animationClip.length * 2f, 6f) : (animationClip.isLooping ? 20f : animationClip.length));
		Queue<(string, bool)> queue = new Queue<(string, bool)>();
		if (sitEnter != null && Has(sitEnter))
		{
			text = sitEnter;
			len = FBRegistry.I.Clip(text).length;
			queue.Enqueue((clip, true));
		}
		else if (sitEnter != null)
		{
			len = 1E+09f;
		}
		fullClip = null;
		PlayFull(text, 0.25f);
		dance = new Dance
		{
			clip = text,
			len = len,
			chain = queue,
			sitOut = ((sitOut != null && Has(sitOut)) ? sitOut : null)
		};
		emoteT = -1f;
		SetWeaponVisible(v: false);
		return true;
	}

	private bool ChainDance(string clip, bool hold)
	{
		if (!Has(clip) || dance == null)
		{
			return false;
		}
		fullClip = null;
		PlayFull(clip, 0.25f);
		dance.clip = clip;
		dance.t = 0f;
		dance.len = (hold ? 1E+09f : FBRegistry.I.Clip(clip).length);
		return true;
	}

	public void StopDance(bool force = false)
	{
		Dance dance = this.dance;
		if (dance == null)
		{
			return;
		}
		if (!force && alive && dance.sitOut != null)
		{
			if (dance.leaving)
			{
				return;
			}
			if (dance.chain.Count == 0)
			{
				dance.leaving = true;
				if (ChainDance(dance.sitOut, hold: false))
				{
					return;
				}
			}
		}
		this.dance = null;
		SetWeaponVisible(!UnarmedLocomotion);
	}

	public void CutDance()
	{
		dance = null;
		danW = 0f;
		if (LE >= 0 && (bool)an)
		{
			an.SetLayerWeight(LE, 0f);
		}
		SetWeaponVisible(!UnarmedLocomotion);
	}

	public bool PlayWaitPose(string clip)
	{
		if (LE < 0 || !Has(clip) || !alive || special || dance != null)
		{
			return false;
		}
		waitPose = clip;
		return true;
	}

	public void StopWaitPose()
	{
		waitPose = null;
	}

	public bool PlaySpecial(string clip, float dur = -1f, Action onEnd = null)
	{
		if (LF < 0 || !Has(clip))
		{
			return false;
		}
		SetOverride("FINISHER_PLACEHOLDER", clip);
		an.Play("Finisher", LF, 0f);
		specT = 0f;
		specLen = ((dur > 0f) ? dur : FBRegistry.I.Clip(clip).length);
		specEnd = onEnd;
		finTarget = 1f;
		special = true;
		return true;
	}

	public void Trigger(string n, int idx = -1)
	{
		switch (n)
		{
		case "reload":
			ST("Reload");
			break;
		case "inspect":
			ST("Inspect");
			break;
		case "grenade":
			ST("GrenadeThrow");
			break;
		case "hit":
			if (!(hitCd > 0f) && !(UnityEngine.Random.value > PUP("hitReactionChance", 0.8f)))
			{
				hitCd = PUP("hitReactionCooldown", 0.35f);
				SF("HitIndex", (idx >= 0) ? idx : UnityEngine.Random.Range(0, (int)PUP("numberOfHitAnimations", 4f)));
				ST("Hit");
			}
			break;
		}
	}

	public void Damage(float dmg, FBCharacter by, bool head)
	{
		FBCombat.DamageActor(this, dmg, head, by);
	}

	public void Die(FBCharacter by)
	{
		if (!alive)
		{
			return;
		}
		alive = false;
		hp = 0f;
		deaths++;
		Collider[] componentsInChildren = GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			// fisica 31/10: nao desliga colliders de um ragdoll ja simulando (vitima morta deitada atravessaria o chao)
			if (!(collider is CharacterController) && collider.gameObject.layer != FBRagdoll.Layer)
			{
				collider.enabled = false;
			}
		}
		int num = UnityEngine.Random.Range(0, (int)PUP("numberOfDeathAnimations", 4f));
		deathClip = null;
		if (keepFinal)
		{
			finTarget = 1f;
			special = false;
		}
		else if (LF >= 0 && !vehiclePose && (deathClip = FBDeath.Begin(this)) != null)
		{
			SetOverride("FINISHER_PLACEHOLDER", deathClip);
			an.Play("Finisher", LF, 0f);
			finW = 0f;
			finTarget = 1f;
		}
		else
		{
			FBRagdoll fBRagdoll = FBRagdoll.Of(this);
			if ((object)fBRagdoll != null && (fBRagdoll.Simulating || fBRagdoll.enabled))
			{
				finTarget = 0f;
			}
			else if (hasDeathState)
			{
				SF("DeathIndex", num);
				ST("Death");
			}
			else if (LF >= 0 && Has(DEATHS[num]))
			{
				SetOverride("FINISHER_PLACEHOLDER", DEATHS[num]);
				an.Play("Finisher", LF, 0f);
				finW = 0f;
				finTarget = 1f;
			}
		}
		StopDance(force: true);
		sys = null;
		onDeath?.Invoke(this, by);
	}

	public void Revive(float h = 100f)
	{
		FBWoundFx.ClearCharacter(this);
		FBRagdoll component = GetComponent<FBRagdoll>();
		if ((bool)component)
		{
			component.Clear();
		}
		lastHit = default(FBHitInfo);
		lastWhy = null;
		lastHitHead = false;
		lastHitFrom = null;
		hp = h;
		alive = true;
		knocked = false;
		prone = false;
		sys = null;
		keepFinal = false;
		special = false;
		specT = -1f;
		an.Rebind();
		curOver.Clear();
		SetUnarmedLocomotion(UnarmedLocomotion, force: true);
		if (weapon != null)
		{
			SetFireMode(weapon.auto);
		}
		finW = (finTarget = 0f);
		fullClip = null;
		dance = null;
		Collider[] componentsInChildren = GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = true;
		}
		SetWeaponVisible(!UnarmedLocomotion);
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBCharacter.Update");

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
		if (deltaTime <= 0f || !an || !an.runtimeAnimatorController)
		{
			return;
		}
		float num = (alive ? new Vector2(velocity.x, velocity.z).magnitude : 0f);
		Vector3 vector = base.transform.InverseTransformDirection(velocity);
		vector /= Mathf.Max(0.01f, scaleK);
		float num2 = animVel?.x ?? vector.x;
		float num3 = animVel?.y ?? vector.z;
		float num4 = 1f - Mathf.Exp((0f - deltaTime) / 0.1f);
		vx += ((alive ? num2 : 0f) - vx) * num4;
		vz += ((alive ? num3 : 0f) - vz) * num4;
		SF("VelX", vx);
		SF("VelZ", vz);
		SF("Speed", new Vector2(vx, vz).magnitude);
		airT = (grounded ? 0f : (airT + deltaTime));
		bool flag = alive && !grounded && airT > 0.2f;
		SB("InAir", flag);
		SB("IsGrounded", !flag);
		SF("VerticalVelocity", velocity.y);
		if (flag && !wasAir && velocity.y > PUP("jumpVelocityThreshold", 2f))
		{
			ST("JumpStart");
		}
		if (!flag && wasAir)
		{
			ST("JumpLand");
		}
		wasAir = flag;
		bool flag2 = crouch && alive;
		SB("IsCrouching", flag2);
		SF("CrouchAlpha", flag2 ? 1 : 0);
		if (flag2 && !wasCrouch)
		{
			ST("CrouchTrigger");
		}
		wasCrouch = flag2;
		bool unarmedLocomotion = UnarmedLocomotion;
		bool flag3 = !unarmedLocomotion && (shooting || aiming || reloading);
		stillT = ((num < PUP("relaxedMoveStillThreshold", 0.1f) && !flag3 && !flag2) ? (stillT + deltaTime) : 0f);
		bool flag4 = unarmedLocomotion;
		SB("IsRelaxed", flag4 && alive);
		Hot = aiming || shooting || reloading || lastShot < PUP("combatFlagDuration", 3f) || combatHold > 0f;
		combatHold -= deltaTime;
		bool flag5 = num > 0.6f;
		CombatStance = !unarmedLocomotion && combat && !sprint && alive && !vehiclePose && sys == null && (Hot || !flag5);
		SB("InCombat", CombatStance);
		SB("IsShooting", alive && !unarmedLocomotion && shooting && !IsPistol);
		if (lastShot < lastShotPrev - 0.0001f)
		{
			kick = Mathf.Min(1f, kick + (IsPistol ? 0.85f : 0.35f));
		}
		lastShotPrev = lastShot;
		kick = Mathf.Max(0f, kick - deltaTime * (float)(IsPistol ? 9 : 12));
		SB("IsRunning", sprint && !reloading);
		SB("IsAiming", !unarmedLocomotion && aiming);
		SB("Tactical Sprint", v: false);
		float num5 = PUP("brakeSpeedThreshold", 1.5f);
		if (lastSpeed > num5 * 2f && num < num5 && !sprint && wasSprint)
		{
			ST("StopSprint");
			ST("Brake");
		}
		wasSprint = sprint;
		lastSpeed = num;
		hitCd = Mathf.Max(0f, hitCd - deltaTime);
		lastShot += deltaTime;
		if (LU >= 0)
		{
			AnimatorStateInfo currentAnimatorStateInfo = an.GetCurrentAnimatorStateInfo(LU);
			reloading = currentAnimatorStateInfo.IsName("Reload Anim");
			bool flag6 = currentAnimatorStateInfo.IsName("Idle") || currentAnimatorStateInfo.IsName("Tactical Sprint") || (unarmedLocomotion && currentAnimatorStateInfo.IsName("Relaxed Upper"));
			bool flag7 = (!unarmedLocomotion && (aiming || (shooting && !IsPistol) || reloading)) || (flag && !unarmedLocomotion) || !flag6;
			upperW = Mathf.Clamp01(upperW + (flag7 ? PUP("upperBodyWeightUpSpeed", 20f) : (0f - PUP("upperBodyWeightDownSpeed", 8f))) * deltaTime);
			an.SetLayerWeight(LU, upperW);
		}
		if (specT >= 0f)
		{
			specT += deltaTime;
			if (specT >= specLen)
			{
				specT = -1f;
				Action action = specEnd;
				specEnd = null;
				special = false;
				finTarget = ((!alive) ? 1 : 0);
				action?.Invoke();
			}
		}
		finW = Mathf.Lerp(finW, finTarget, 1f - Mathf.Exp((0f - deltaTime) * 12f));
		if (LF >= 0)
		{
			an.SetLayerWeight(LF, finW);
		}
		if (emoteT >= 0f)
		{
			emoteT += deltaTime * emoteSpd;
			if (emoteT > emoteLen - 0.2f || !alive || (shooting && (!drawing || drawT >= drawAt)))
			{
				emoteT = -1f;
			}
		}
		emoW = Mathf.Lerp(emoW, (emoteT >= 0f) ? 1 : 0, 1f - Mathf.Exp((0f - deltaTime) * (float)((emoteSpd > 1.2f) ? 12 : 10)));
		if (LEU >= 0)
		{
			an.SetLayerWeight(LEU, emoW);
		}
		if (dance != null)
		{
			dance.t += deltaTime;
			if (!alive || special || shooting)
			{
				StopDance(force: true);
			}
			else if (dance.t > dance.len - 0.2f)
			{
				if (dance.chain.Count > 0)
				{
					var (clip, hold) = dance.chain.Dequeue();
					ChainDance(clip, hold);
				}
				else
				{
					StopDance(force: true);
				}
			}
		}
		if (waitPose != null && (!alive || special || shooting || (!vehiclePose && num > 0.22f)))
		{
			StopWaitPose();
		}
		string text = ((alive && sys != null && Has(sys)) ? sys : ((dance != null) ? dance.clip : waitPose));
		bool flag8 = text == "FF_SYS_Vault";
		if (text != null)
		{
			PlayFull(text, flag8 ? 0.05f : 0.3f);
		}
		else
		{
			fullClip = null;
		}
		danW = Mathf.Lerp(danW, (text != null) ? 1 : 0, 1f - Mathf.Exp((0f - deltaTime) * (float)(flag8 ? 40 : (vaultTail ? 25 : ((text == null && num > 0.6f) ? 18 : ((sys != null) ? 10 : 8))))));
		if (LE >= 0)
		{
			an.SetLayerWeight(LE, danW);
		}
		if (drawT >= 0f)
		{
			drawT += deltaTime * drawSpd;
			bool flag9 = (drawHid = (drawing ? (drawT < drawAt) : (drawT >= drawAt)));
			if ((bool)weaponGO && dance == null && !vehiclePose)
			{
				weaponGO.SetActive(!flag9);
			}
			bool flag10 = drawing && drawT >= drawAt && (shooting || (aiming && emoW < 0.5f));
			if (drawT > drawLen || !alive || flag10)
			{
				drawT = -1f;
				drawHid = false;
				if (drawing && (bool)weaponGO && !vehiclePose)
				{
					weaponGO.SetActive(value: true);
				}
				if (flag10)
				{
					emoteT = -1f;
				}
			}
		}
		if (civ)
		{
			SB("Surrender", surrender && alive);
			SB("Talk", talking && alive);
			SB("Flee", fleeing);
		}
		bool flag11 = num < 0.12f && grounded;
		if (unarmedLocomotion && npcSet != null && flag11 && !wasStill && dance == null)
		{
			List<string> list = npcSet.idle.FindAll((string n) => Has(n) && n != npcIdleLast);
			string text2 = ((list.Count > 0) ? list[UnityEngine.Random.Range(0, list.Count)] : npcSet.idle.Find(Has));
			if (text2 != null)
			{
				SetOverride("Idle", text2);
				npcIdleLast = text2;
			}
		}
		wasStill = flag11;
		float num6 = BaseRootSpeed() * an.humanScale * base.transform.lossyScale.y;
		LocoRate = ((num > 0.25f && num6 > 0.35f && danW < 0.05f && finW < 0.05f) ? Mathf.Clamp(num / num6, 0.7f, 1.45f) : 1f);
		if (PN("FB_LocoSpeed") != null)
		{
			SF("FB_LocoSpeed", LocoRate);
			an.speed = 1f;
		}
		else
		{
			an.speed = LocoRate;
		}
		if (VaultRate > 0f && sys == "FF_SYS_Vault")
		{
			an.speed = VaultRate;
		}
	}

	public static float RootSpeedOf(AnimationClip c)
	{
		if (!c)
		{
			return 0f;
		}
		if (rsCache.TryGetValue(c, out var value))
		{
			return value;
		}
		if (measuredRS == null)
		{
			TextAsset textAsset = Resources.Load<TextAsset>("FBClipRS");
			measuredRS = (textAsset ? ((Json.Parse(textAsset.text) as Dictionary<string, object>) ?? new Dictionary<string, object>()) : new Dictionary<string, object>());
		}
		if (!GameData.ClipRS.TryGetValue(c.name, out value))
		{
			if (measuredRS.TryGetValue(c.name, out var value2))
			{
				value = Json.Fv(value2);
			}
			else
			{
				Vector3 averageSpeed = c.averageSpeed;
				value = (c.isLooping ? new Vector2(averageSpeed.x, averageSpeed.z).magnitude : 0f);
			}
		}
		return rsCache[c] = value;
	}

	private float BaseRootSpeed()
	{
		float sum = 0f;
		float ws = 0f;
		float num = (an.IsInTransition(0) ? an.GetAnimatorTransitionInfo(0).normalizedTime : 0f);
		an.GetCurrentAnimatorClipInfo(0, clipInfo);
		Acc(1f - num);
		if (num > 0f)
		{
			an.GetNextAnimatorClipInfo(0, clipInfo);
			Acc(num);
		}
		if (!(ws > 0.0001f))
		{
			return 0f;
		}
		return sum / ws;
		void Acc(float k)
		{
			if (k <= 0f)
			{
				return;
			}
			foreach (AnimatorClipInfo item in clipInfo)
			{
				sum += RootSpeedOf(item.clip) * item.weight * k;
				ws += item.weight * k;
			}
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBCharacter.LateUpdate");

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
		bool flag = special || !alive || danW > 0.05f || vehiclePose;
		// fisica 31/10: so o FBRagdoll desliga o Animator do personagem; nao escreve rig/maos/lean nos ossos fisicos de um ragdoll vivo
		if ((bool)an && !an.enabled)
		{
			return;
		}
		bool handsBusy = HandsBusy;
		if ((bool)weaponGO && dance == null)
		{
			weaponGO.transform.localRotation = proxyQ;
			weaponGO.transform.localPosition = proxyP;
		}
		rig?.Aim(deltaTime);
		leanS = Mathf.Lerp(leanS, (alive && !flag) ? lean : 0f, 1f - Mathf.Exp((0f - deltaTime) / 0.12f * 2.3f));
		if (Mathf.Abs(leanS) > 0.005f && spine != null && spine.Length >= 2)
		{
			float num = leanS * 40f;
			float[] array = new float[2] { 0.6f, 0.4f };
			for (int i = 0; i < 2; i++)
			{
				spine[i].Rotate(base.transform.forward, (0f - num) * array[i], Space.World);
				if (i == 1 && leanS < -0.05f)
				{
					spine[i].Rotate(Vector3.up, leanS * 0.22f * 57.29578f, Space.World);
				}
			}
			float num2 = Mathf.Sign(leanS) * Mathf.Min(0.25f * Mathf.Abs(leanS), Mathf.Max(0f, peekRoom));
			if ((bool)hips)
			{
				hips.position += base.transform.right * num2;
			}
			PeekShift = num2;
		}
		else
		{
			PeekShift = 0f;
		}
		if (kick > 0.01f && (bool)rFore && alive && !flag)
		{
			rFore.Rotate(base.transform.right, (0f - kick) * kick * (IsPistol ? 0.22f : 0.06f) * 57.29578f, Space.World);
		}
		if (danW > 0.05f && prone && (bool)weaponGO && weaponGO.activeSelf)
		{
			Quaternion quaternion = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(BarrelDir(), (base.transform.forward + Vector3.down * 0.06f).normalized), danW);
			weaponGO.transform.rotation = quaternion * weaponGO.transform.rotation;
		}
		rig?.Hands(deltaTime);
		pwW = Mathf.Lerp(pwW, (IsPistol && sprint && (bool)weaponGO && weaponGO.activeSelf && alive && !flag && !handsBusy) ? (1f - upperW) : 0f, 1f - Mathf.Exp((0f - deltaTime) * 10f));
		if (pwW > 0.01f && (bool)rHand)
		{
			Quaternion quaternion2 = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(BarrelDir(), (base.transform.forward + Vector3.up * (sprint ? (-0.75f) : (-0.3f))).normalized), pwW);
			rHand.rotation = quaternion2 * rHand.rotation;
		}
		if ((bool)feet)
		{
			feet.Tick(deltaTime);
		}
		DanceGround(deltaTime);
		VaultInPlace();
		PlaceHolsters();
	}

	private void VaultInPlace()
	{
		if (!hips)
		{
			return;
		}
		bool num = sys == "FF_SYS_Vault";
		if (num)
		{
			vaultTail = true;
		}
		else if (danW < 0.03f || sys != null || dance != null || !alive)
		{
			vaultTail = false;
		}
		if ((!num && !vaultTail) || !alive)
		{
			if (danW < 0.05f && !vehiclePose)
			{
				hipRest = base.transform.InverseTransformPoint(hips.position);
				hipRestOk = true;
			}
		}
		else if (hipRestOk)
		{
			Vector3 vector = base.transform.InverseTransformPoint(hips.position);
			hips.position = base.transform.TransformPoint(new Vector3(hipRest.x, Mathf.Min(vector.y, hipRest.y + 0.15f), hipRest.z));
		}
	}

	private void DanceGround(float dt)
	{
		bool flag = npcSet != null && !isPlayer;
		RaycastHit hitInfo;
		if ((danW < 0.3f && !flag) || sys == "FF_SYS_Vault" || vaultTail || seated || !alive || vehiclePose || !hips || !lFoot || !rFoot)
		{
			danceLift = (seated ? 0f : Mathf.MoveTowards(danceLift, 0f, dt * ((vaultTail || sys == "FF_SYS_Vault") ? 4f : 0.5f)));
		}
		else if (Physics.Raycast(base.transform.position + Vector3.up * 1.2f, Vector3.down, out hitInfo, 3f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
		{
			if (!toesOk)
			{
				toesOk = true;
				lToe = (an ? an.GetBoneTransform(HumanBodyBones.LeftToes) : null);
				rToe = (an ? an.GetBoneTransform(HumanBodyBones.RightToes) : null);
			}
			float num = ScaleK;
			float num2 = Mathf.Min(lFoot.position.y, rFoot.position.y) - (flag ? 0.075f : 0.035f) * num;
			if (flag && (bool)lToe && (bool)rToe)
			{
				num2 = Mathf.Min(num2, Mathf.Min(lToe.position.y, rToe.position.y) - 0.022f * num);
			}
			float num3 = Mathf.Max(0f, hitInfo.point.y - num2);
			danceLift = ((num3 > danceLift) ? Mathf.Lerp(danceLift, num3, 1f - Mathf.Exp((0f - dt) * 14f)) : Mathf.MoveTowards(danceLift, num3, dt * (flag ? 0.05f : 0.5f)));
			if (danceLift > 0.002f)
			{
				hips.position += Vector3.up * danceLift;
			}
		}
	}
}
}
