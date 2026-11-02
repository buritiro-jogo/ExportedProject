using System;
using System.Collections.Generic;
using FreeBala;
using UnityEngine;

namespace FreeBalaWeaponAdjustSafe
{
[DefaultExecutionOrder(30000)]
public sealed class FBWeaponAdjustSafeRunner : MonoBehaviour
{
	public struct BasePose
	{
		public Vector3 position;

		public Quaternion rotation;

		public Vector3 scale;

		public Vector3 appliedScale;

		public bool hasAppliedScale;

		public int frame;
	}

	private static FBWeaponAdjustSafeRunner instance;

	private const string DatabaseResource = "FBWeaponAdjustSafeDatabase";

	private FBWeaponAdjustSafeDatabase database;

	private readonly List<FBCharacter> characters = new List<FBCharacter>();

	private readonly Dictionary<Transform, BasePose> bases = new Dictionary<Transform, BasePose>();

	public static bool CalibrationEnabled { get; set; } = true;


	public static bool ApplyHolsterAdjust { get; set; } = false;


	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Bootstrap()
	{
		if (!instance)
		{
			GameObject obj = new GameObject("FBWeaponAdjustSafeRunner");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			instance = obj.AddComponent<FBWeaponAdjustSafeRunner>();
		}
	}

	private void Awake()
	{
		if ((bool)instance && instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
		else
		{
			instance = this;
		}
	}

	public static bool TryGetBase(Transform target, out BasePose pose)
	{
		pose = default(BasePose);
		if ((bool)instance && (bool)target)
		{
			return instance.bases.TryGetValue(target, out pose);
		}
		return false;
	}

	public static AdjustPose CurrentPose(FBCharacter character)
	{
		if (!character || !character.isActiveAndEnabled)
		{
			return AdjustPose.Hold;
		}
		float num = new Vector2(character.velocity.x, character.velocity.z).magnitude;
		if (character.animVel.HasValue)
		{
			num = Mathf.Max(num, character.animVel.Value.magnitude);
		}
		if (float.IsNaN(num) || float.IsInfinity(num))
		{
			num = 0f;
		}
		bool flag = num > 0.2f;
		bool crouch = character.crouch;
		if (character.reloading)
		{
			return AdjustPose.Reload;
		}
		if (character.shooting || character.lastShot < 0.16f)
		{
			if (crouch)
			{
				return AdjustPose.CrouchFire;
			}
			if (!flag)
			{
				return AdjustPose.Fire;
			}
			return AdjustPose.WalkFire;
		}
		if (character.aiming)
		{
			if (crouch)
			{
				return AdjustPose.CrouchAim;
			}
			if (!flag)
			{
				return AdjustPose.Aim;
			}
			return AdjustPose.WalkAim;
		}
		if (crouch)
		{
			if (!flag)
			{
				return AdjustPose.Crouch;
			}
			return AdjustPose.CrouchWalk;
		}
		if (flag && character.sprint)
		{
			return AdjustPose.Run;
		}
		if (!flag)
		{
			return AdjustPose.Hold;
		}
		return AdjustPose.Walk;
	}

	private void RefreshCharacters()
	{
		characters.Clear();
		characters.AddRange(UnityEngine.Object.FindObjectsByType<FBCharacter>(FindObjectsSortMode.None));
	}

	private void LateUpdate()
	{
		if (!CalibrationEnabled)
		{
			foreach (KeyValuePair<Transform, BasePose> basis in bases)
			{
				if ((bool)basis.Key && basis.Value.hasAppliedScale && NearlySameScale(basis.Key.localScale, basis.Value.appliedScale))
				{
					basis.Key.localScale = basis.Value.scale;
				}
			}
			bases.Clear();
			return;
		}
		if (!database)
		{
			database = Resources.Load<FBWeaponAdjustSafeDatabase>("FBWeaponAdjustSafeDatabase");
		}
		if (Time.frameCount % 25 == 0 || characters.Count == 0)
		{
			RefreshCharacters();
		}
		if (Time.frameCount % 600 == 0)
		{
			List<Transform> list = new List<Transform>();
			foreach (KeyValuePair<Transform, BasePose> basis2 in bases)
			{
				if (!basis2.Key)
				{
					list.Add(basis2.Key);
				}
			}
			foreach (Transform item in list)
			{
				bases.Remove(item);
			}
		}
		for (int i = 0; i < characters.Count; i++)
		{
			FBCharacter fBCharacter = characters[i];
			if (!fBCharacter || !fBCharacter.isActiveAndEnabled || !fBCharacter.gameObject.activeInHierarchy || !fBCharacter.an || !fBCharacter.an.isInitialized)
			{
				continue;
			}
			GameObject weaponGO = fBCharacter.weaponGO;
			if ((bool)weaponGO && weaponGO.activeInHierarchy && fBCharacter.weapon != null && fBCharacter.dance == null)
			{
				string id = fBCharacter.weapon.id;
				if (!string.IsNullOrEmpty(id))
				{
					Apply(fBCharacter, weaponGO.transform, id, AdjustLocation.InHand, CurrentPose(fBCharacter));
				}
			}
			IEnumerable<GameObject> holsterObjects = fBCharacter.HolsterObjects;
			if (holsterObjects == null)
			{
				continue;
			}
			foreach (GameObject item2 in holsterObjects)
			{
				if ((bool)item2 && item2.activeInHierarchy && item2.name.StartsWith("Coldre_", StringComparison.Ordinal))
				{
					string text = item2.name.Substring("Coldre_".Length);
					if (!string.IsNullOrEmpty(text))
					{
						Apply(fBCharacter, item2.transform, text, AdjustLocation.Holster, AdjustPose.Hold);
					}
				}
			}
		}
	}

	private static bool NearlySameScale(Vector3 a, Vector3 b)
	{
		if (Near(a.x, b.x) && Near(a.y, b.y))
		{
			return Near(a.z, b.z);
		}
		return false;
		static bool Near(float x, float y)
		{
			return Mathf.Abs(x - y) <= 1E-06f + 0.0001f * Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
		}
	}

	private void Apply(FBCharacter character, Transform target, string weaponId, AdjustLocation location, AdjustPose pose)
	{
		if ((bool)target && (bool)character)
		{
			Vector3 vector = target.localScale;
			if (bases.TryGetValue(target, out var value) && value.hasAppliedScale && NearlySameScale(vector, value.appliedScale))
			{
				vector = value.scale;
			}
			BasePose basePose = default(BasePose);
			basePose.position = target.localPosition;
			basePose.rotation = target.localRotation;
			basePose.scale = vector;
			basePose.appliedScale = vector;
			basePose.hasAppliedScale = false;
			basePose.frame = Time.frameCount;
			BasePose value2 = basePose;
			target.localScale = vector;
			if ((bool)database && (location != 0 || ApplyHolsterAdjust) && database.TryFind(character.skinId, weaponId, location, pose, out var found))
			{
				target.localPosition += found.localPositionOffset;
				target.localRotation *= found.localRotationOffset;
				Vector3 b = FBWeaponAdjustSafeDatabase.SafeMultiplier(found.localScaleMultiplier);
				value2.appliedScale = Vector3.Scale(vector, b);
				target.localScale = value2.appliedScale;
				value2.hasAppliedScale = true;
			}
			bases[target] = value2;
		}
	}
}
}
