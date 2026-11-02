using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBalaWeaponAdjustSafe
{
[CreateAssetMenu(fileName = "FBWeaponAdjustSafeDatabase", menuName = "FreeBala/Calibracao segura de armas")]
public sealed class FBWeaponAdjustSafeDatabase : ScriptableObject
{
	[Serializable]
	public sealed class Entry
	{
		public string skinId = "*";

		public string weaponId = "";

		public AdjustLocation location;

		public AdjustPose pose;

		public Vector3 localPositionOffset;

		public Quaternion localRotationOffset = Quaternion.identity;

		public Vector3 localScaleMultiplier = Vector3.one;
	}

	[SerializeField]
	private List<Entry> entries = new List<Entry>();

	public IReadOnlyList<Entry> Entries => entries;

	private bool TryFindExact(string skinId, string weaponId, AdjustLocation location, AdjustPose pose, out Entry found)
	{
		found = null;
		string text = (string.IsNullOrEmpty(skinId) ? "*" : skinId);
		for (int i = 0; i < entries.Count; i++)
		{
			Entry entry = entries[i];
			if (entry != null && entry.skinId == text && entry.weaponId == weaponId && entry.location == location && entry.pose == pose)
			{
				found = entry;
				return true;
			}
		}
		if (text != "*")
		{
			for (int j = 0; j < entries.Count; j++)
			{
				Entry entry2 = entries[j];
				if (entry2 != null && entry2.skinId == "*" && entry2.weaponId == weaponId && entry2.location == location && entry2.pose == pose)
				{
					found = entry2;
					return true;
				}
			}
		}
		return false;
	}

	public bool TryFind(string skinId, string weaponId, AdjustLocation location, AdjustPose pose, out Entry found)
	{
		found = null;
		if (string.IsNullOrEmpty(weaponId))
		{
			return false;
		}
		if (TryFindExact(skinId, weaponId, location, pose, out found))
		{
			return true;
		}
		if (location == AdjustLocation.InHand)
		{
			switch (pose)
			{
			case AdjustPose.Hold:
				break;
			case AdjustPose.WalkAim:
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Aim, out found))
				{
					return true;
				}
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Walk, out found))
				{
					return true;
				}
				goto default;
			case AdjustPose.WalkFire:
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Fire, out found))
				{
					return true;
				}
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Walk, out found))
				{
					return true;
				}
				goto default;
			case AdjustPose.CrouchAim:
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Aim, out found))
				{
					return true;
				}
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Crouch, out found))
				{
					return true;
				}
				goto default;
			case AdjustPose.CrouchFire:
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Fire, out found))
				{
					return true;
				}
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Crouch, out found))
				{
					return true;
				}
				goto default;
			case AdjustPose.CrouchWalk:
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Crouch, out found))
				{
					return true;
				}
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Walk, out found))
				{
					return true;
				}
				goto default;
			case AdjustPose.Run:
				if (TryFindExact(skinId, weaponId, location, AdjustPose.Walk, out found))
				{
					return true;
				}
				goto default;
			default:
				return TryFindExact(skinId, weaponId, location, AdjustPose.Hold, out found);
			}
		}
		return false;
	}

	public void Set(string skinId, string weaponId, AdjustLocation location, AdjustPose pose, Vector3 pos, Quaternion rot)
	{
		Vector3 scaleMultiplier = Vector3.one;
		string text = (string.IsNullOrEmpty(skinId) ? "*" : skinId);
		foreach (Entry entry in entries)
		{
			if (entry != null && entry.skinId == text && entry.weaponId == weaponId && entry.location == location && entry.pose == pose)
			{
				scaleMultiplier = SafeMultiplier(entry.localScaleMultiplier);
				break;
			}
		}
		Set(skinId, weaponId, location, pose, pos, rot, scaleMultiplier);
	}

	public void Set(string skinId, string weaponId, AdjustLocation location, AdjustPose pose, Vector3 pos, Quaternion rot, Vector3 scaleMultiplier)
	{
		string text = (string.IsNullOrEmpty(skinId) ? "*" : skinId);
		Entry entry = null;
		foreach (Entry entry2 in entries)
		{
			if (entry2 != null && entry2.skinId == text && entry2.weaponId == weaponId && entry2.location == location && entry2.pose == pose)
			{
				entry = entry2;
				break;
			}
		}
		if (entry == null)
		{
			entry = new Entry
			{
				skinId = text,
				weaponId = weaponId,
				location = location,
				pose = pose
			};
			entries.Add(entry);
		}
		entry.localPositionOffset = pos;
		entry.localRotationOffset = Quaternion.Normalize(rot);
		entry.localScaleMultiplier = SafeMultiplier(scaleMultiplier);
	}

	public static Vector3 SafeMultiplier(Vector3 v)
	{
		if (v == Vector3.zero)
		{
			return Vector3.one;
		}
		return new Vector3(S(v.x), S(v.y), S(v.z));
		static float S(float x)
		{
			if (!float.IsNaN(x) && !float.IsInfinity(x) && !(x <= 0.0001f))
			{
				return Mathf.Clamp(x, 0.01f, 100f);
			}
			return 1f;
		}
	}

	public bool Remove(string skinId, string weaponId, AdjustLocation location, AdjustPose pose)
	{
		string skin = (string.IsNullOrEmpty(skinId) ? "*" : skinId);
		return entries.RemoveAll((Entry e) => e != null && e.skinId == skin && e.weaponId == weaponId && e.location == location && e.pose == pose) > 0;
	}
}
}
