using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBCamLab
{
	public static bool aim;

	public static bool fire;

	public static bool leanL;

	public static bool leanR;

	public static Vector2 move;

	private static bool firePulse;

	private static bool runPulse;

	private static bool crouchPulse;

	public static readonly (string cls, string[] pref)[] CLASSES = new(string, string[])[5]
	{
		("pistola", new string[2] { "g18", "38" }),
		("submetralhadora", new string[2] { "mp5", "p90" }),
		("fuzil", new string[3] { "ak", "ar15_new", "rpk" }),
		("sniper", new string[2] { "barret_50", "sniper_hunting" }),
		("escopeta", new string[2] { "remington_870", "m31" })
	};

	private static Material matA;

	private static Material matB;

	private static Material mark;

	public static void Install()
	{
		aim = (fire = (leanL = (leanR = (firePulse = (runPulse = (crouchPulse = false))))));
		move = Vector2.zero;
		FBPlayer.TestInput = delegate
		{
			FBInputState fBInputState = default(FBInputState);
			fBInputState.slot = -1;
			fBInputState.aim = aim;
			fBInputState.aimPressed = false;
			fBInputState.fire = fire || firePulse;
			fBInputState.firePressed = firePulse;
			fBInputState.leanL = leanL;
			fBInputState.leanR = leanR;
			fBInputState.move = move;
			fBInputState.sprintToggle = runPulse;
			fBInputState.crouchToggle = crouchPulse;
			FBInputState result = fBInputState;
			firePulse = (runPulse = (crouchPulse = false));
			return result;
		};
	}

	public static void Remove()
	{
		FBPlayer.TestInput = null;
		FBRecoil.TestNoSpread = false;
	}

	public static void Shoot()
	{
		firePulse = true;
	}

	public static void ToggleRun()
	{
		runPulse = true;
	}

	public static void ToggleCrouch()
	{
		crouchPulse = true;
	}

	public static WeaponDef Pick(string cls, string[] pref)
	{
		foreach (string key in pref)
		{
			if (GameData.Weapons.TryGetValue(key, out var value) && Ok(value))
			{
				return value;
			}
		}
		return (from d in GameData.Weapons.Values.Where(Ok)
			orderby d.id
			select d).FirstOrDefault();
		bool Ok(WeaponDef d)
		{
			if (d != null && d.cls == cls && (bool)FBRegistry.I)
			{
				return FBRegistry.I.Weapon(d.id);
			}
			return false;
		}
	}

	public static void Teleport(FBCharacter ch, Vector3 p, float yaw)
	{
		CharacterController component = ch.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		ch.transform.position = p;
		ch.Yaw = yaw;
		if ((bool)component)
		{
			component.enabled = true;
		}
		if ((bool)FBPlayer.I)
		{
			FBPlayer.I.OnRespawn(yaw);
		}
	}

	public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, bool alt = false, int layer = 8)
	{
		if ((object)matA == null)
		{
			matA = MakeMat(new Color(0.55f, 0.56f, 0.58f));
		}
		if ((object)matB == null)
		{
			matB = MakeMat(new Color(0.78f, 0.62f, 0.34f));
		}
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		gameObject.name = name;
		gameObject.layer = layer;
		if ((bool)parent)
		{
			gameObject.transform.SetParent(parent, worldPositionStays: true);
		}
		gameObject.transform.position = center;
		gameObject.transform.localScale = size;
		gameObject.GetComponent<Renderer>().sharedMaterial = (alt ? matB : matA);
		return gameObject;
	}

	private static Material MakeMat(Color c)
	{
		Material material = new Material(FBShaders.Lit);
		material.SetColor("_BaseColor", c);
		material.color = c;
		return material;
	}

	public static GameObject Mark(Vector3 p, float distToCam, Color? c = null)
	{
		if ((object)mark == null)
		{
			mark = new Material(FBShaders.Unlit);
		}
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
		gameObject.name = "MarcaImpacto";
		Material material = new Material(mark);
		material.SetColor("_BaseColor", c ?? Color.red);
		material.color = c ?? Color.red;
		gameObject.GetComponent<Renderer>().sharedMaterial = material;
		gameObject.transform.position = p;
		gameObject.transform.localScale = Vector3.one * (0.05f + distToCam * 0.004f);
		return gameObject;
	}

	public static float HipsDrift(FBCharacter ch)
	{
		Transform transform = (((bool)ch && (bool)ch.an) ? ch.an.GetBoneTransform(HumanBodyBones.Hips) : null);
		if (!transform)
		{
			return 0f;
		}
		return Vector3.ProjectOnPlane(transform.position - ch.Pos, Vector3.up).magnitude;
	}

	public static IEnumerator Move(FBCharacter ch, Camera cam, Vector2 mv, bool run, bool aimOn, bool fireOn, float secs, Action<float, float> done, Func<float, IEnumerator> mid = null, float midAt = -1f)
	{
		move = mv;
		if (run)
		{
			ToggleRun();
		}
		float worst = 0f;
		float dmin = 1E+09f;
		float dmax = 0f;
		bool midDone = false;
		for (float t = 0f; t < secs; t += Time.deltaTime)
		{
			if (t > 0.35f)
			{
				aim = aimOn;
				fire = fireOn;
			}
			yield return null;
			if (t > 0.6f)
			{
				worst = Mathf.Max(worst, HipsDrift(ch));
				Transform transform = (ch.an ? ch.an.GetBoneTransform(HumanBodyBones.Hips) : ch.transform);
				float b = Vector3.Distance(cam.transform.position, transform.position);
				dmin = Mathf.Min(dmin, b);
				dmax = Mathf.Max(dmax, b);
			}
			if (mid != null && !midDone && midAt >= 0f && t >= midAt)
			{
				midDone = true;
				yield return mid(t);
			}
		}
		fire = false;
		aim = false;
		move = Vector2.zero;
		if (run && (bool)FBPlayer.I && ch.sprint)
		{
			ToggleRun();
		}
		done?.Invoke(worst, dmax - dmin);
	}

	public static bool CrosshairHit(Camera cam, out RaycastHit h)
	{
		return Physics.Raycast(cam.transform.position, cam.transform.forward, out h, 400f, FBWorld.ShotMask, QueryTriggerInteraction.Ignore);
	}
}
}
