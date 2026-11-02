using System.Collections.Generic;

namespace FreeBala
{
public static class FBAnimProfile
{
	public class Holster
	{
		public string draw;

		public string put;

		public float grab;

		public float rel;

		public float drawSpeed = 1.45f;

		public float putSpeed = 1.55f;
	}

	public static readonly Dictionary<string, string> Unarmed = new Dictionary<string, string>
	{
		["Idle"] = "IE_Unarmed_Idle01",
		["WalkFwdLoop"] = "IE_walk_hands_free",
		["RunFwdLoop"] = "IE_Unarmed_Sprint_F",
		["SprintFwdLoop"] = "IE_Unarmed_Sprint_F",
		["RunStrafeRight45Loop"] = "IE_Unarmed_Run_FR",
		["RunRtLoop"] = "IE_Unarmed_Run_R",
		["RunStrafeRight135Loop"] = "IE_Unarmed_Run_BR",
		["RunBwdLoop"] = "IE_Unarmed_Run_B",
		["RunStrafeLeft135Loop"] = "IE_Unarmed_Run_BL",
		["RunLtLoop"] = "IE_Unarmed_Run_L",
		["RunStrafeLeft45Loop"] = "IE_Unarmed_Run_FL",
		["StrafeRight45Loop"] = "IE_Unarmed_Run_FR",
		["StrafeLeft45Loop"] = "IE_Unarmed_Run_FL",
		["StrafeRight135Loop"] = "IE_Unarmed_Run_BR",
		["StrafeLeft135Loop"] = "IE_Unarmed_Run_BL",
		["StrafeRightLoop"] = "IE_Unarmed_Run_R",
		["StrafeLeftLoop"] = "IE_Unarmed_Run_L",
		["WalkBwdLoop"] = "IE_Unarmed_Run_B"
	};

	public const string WalkMale = "REG_Walk_M";

	public const string WalkFemale = "REG_Walk_F";

	public const float UnarmedWalkSpeed = 2.2f;

	public static readonly Dictionary<string, string> UnarmedWalkNpc = new Dictionary<string, string>
	{
		["StrafeRightLoop"] = "StrafeRightLoop",
		["StrafeLeftLoop"] = "StrafeLeftLoop",
		["WalkBwdLoop"] = "WalkBwdLoop"
	};

	public static readonly Dictionary<string, string> UnarmedRight = new Dictionary<string, string>
	{
		["StrafeRightLoop"] = "Right Strafe Walking",
		["RunRtLoop"] = "Right Strafe"
	};

	public static readonly string[] NpcStrafe = new string[6] { "StrafeLeft45Loop", "StrafeLeft135Loop", "StrafeLeftLoop", "StrafeRight45Loop", "StrafeRight135Loop", "StrafeRightLoop" };

	public static readonly string[] NpcMaleWalk = new string[2] { "WalkFwdLoop", "WalkBwdLoop" };

	public static readonly Dictionary<string, string> ArmedRun = new Dictionary<string, string>
	{
		["Walk With Rifle"] = "IE_Rifle_Run_F",
		["StrafeRightLoop"] = "IE_Rifle_Run_R",
		["StrafeLeftLoop"] = "IE_Rifle_Run_L",
		["WalkBwdLoop"] = "IE_Rifle_Run_B",
		["RunFwdLoop"] = "IE_Rifle_Run_F",
		["Run Forward Right"] = "IE_Rifle_Run_FR",
		["Run Forward Left"] = "IE_Rifle_Run_FL",
		["Rifle_SprintLoop"] = "IE_Rifle_Sprint_F",
		["Rifle_RunFwdLoop"] = "IE_Rifle_Run_F",
		["Rifle_RunBwdLoop"] = "IE_Rifle_Run_B",
		// 31/10/2026: o CombatLocomotion ganhou correr de lado e na diagonal de verdade
		// (antes o "de lado" tocava a corrida na diagonal para frente e o pé escorregava).
		// Estes clipes base só aparecem no RelaxedLocomotion, que só roda desarmado.
		["RunRtLoop"] = "IE_Rifle_Run_R",
		["RunLtLoop"] = "IE_Rifle_Run_L",
		["RunStrafeRight135Loop"] = "IE_Rifle_Run_BR",
		["RunStrafeLeft135Loop"] = "IE_Rifle_Run_BL"
	};

	public const string PistolSprint = "IE_Pistol_Sprint_F";

	public static readonly Dictionary<string, string> UnarmedCrouch = new Dictionary<string, string>
	{
		["Rifle_CrouchLoop"] = "IE_Unarmed_Crouch_Idle",
		["Walk Crouching Forward"] = "IE_Unarmed_Crouch_Run_F",
		["Crouch Walk Strafe Right"] = "IE_Unarmed_Crouch_Run_R",
		["Walk Crouching Backward 1"] = "IE_Unarmed_Crouch_Run_B",
		["Crouch Walk Strafe Left"] = "IE_Unarmed_Crouch_Run_L"
	};

	public static readonly Dictionary<string, string> ArmedCrouchFast = new Dictionary<string, string>
	{
		["Walk Crouching Forward"] = "IE_Rifle_Crouch_Run_F",
		["Crouch Walk Strafe Right"] = "IE_Rifle_Crouch_Run_R",
		["Walk Crouching Backward 1"] = "IE_Rifle_Crouch_Run_B",
		["Crouch Walk Strafe Left"] = "IE_Rifle_Crouch_Run_L"
	};

	public const float CrouchFastSpeed = 4.4f;

	public static readonly Dictionary<string, Holster> Holsters = new Dictionary<string, Holster>
	{
		["back"] = new Holster
		{
			draw = "IE_Rifle_Draw_Back",
			grab = 0.52f,
			put = "IE_Rifle_Unequip_R",
			rel = 0.4f,
			drawSpeed = 1.45f,
			putSpeed = 1.55f
		},
		["sling"] = new Holster
		{
			draw = "IE_Rifle_Equip",
			grab = 0.18f,
			put = "IE_Rifle_Unequip",
			rel = 0.32f,
			drawSpeed = 1.35f,
			putSpeed = 1.5f
		},
		["thigh"] = new Holster
		{
			draw = "IE_Pistol_Equip",
			grab = 0.24f,
			put = "IE_Pistol_Unequip",
			rel = 0.3f,
			drawSpeed = 1.5f,
			putSpeed = 1.5f
		}
	};

	public const string HealStart = "FF_SYS_Heal_Start";

	public const string HealLoop = "FF_SYS_Heal_Loop";

	public const string HealEnd = "FF_SYS_Heal_End";

	public const string HealCrouch = "FF_SYS_KO_Revive";

	public const string ReviveStart = "IE_Revive_Start";

	public const string ReviveLoop = "IE_Revive_Loop";

	public const string ReviveEnd = "IE_Revive_End";

	public const string DownedTreated = "IE_Downed_Treated";

	public const string DownedGetUp = "IE_Downed_GetUp";

	public static string Reload(WeaponDef w)
	{
		if (w != null)
		{
			return "Rifle_Reload_2";
		}
		return null;
	}

	public static string Fire(WeaponDef w)
	{
		if (w != null)
		{
			if (!w.auto)
			{
				return "Rifle_ShootOnce";
			}
			return "Rifle_ShootLoop_Additive";
		}
		return null;
	}
}
}
