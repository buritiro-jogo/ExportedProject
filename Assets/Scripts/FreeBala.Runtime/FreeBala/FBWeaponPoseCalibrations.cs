using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
[CreateAssetMenu(fileName = "FBWeaponPoseCalibrations", menuName = "FreeBala/Armas na mao/Calibracoes")]
public sealed class FBWeaponPoseCalibrations : ScriptableObject
{
	public enum Pose
	{
		Hold,
		Aim,
		Fire,
		Reload
	}

	[Serializable]
	public sealed class Entry
	{
		public string skinId = "*";

		public string weaponId;

		public Pose pose;

		public Vector3 positionOffset = Vector3.zero;

		public Quaternion rotationOffset = Quaternion.identity;
	}

	[SerializeField]
	private List<Entry> entries = new List<Entry>();

	[SerializeField]
	[HideInInspector]
	private int revision;

	public IReadOnlyList<Entry> Entries => entries;

	public int Revision => revision;

	public bool TryFind(string skinId, string weaponId, Pose pose, out Entry value)
	{
		value = null;
		if (string.IsNullOrEmpty(weaponId))
		{
			return false;
		}
		string text = (string.IsNullOrEmpty(skinId) ? "*" : skinId);
		foreach (Entry entry in entries)
		{
			if (entry != null && entry.skinId == text && entry.weaponId == weaponId && entry.pose == pose)
			{
				value = entry;
				return true;
			}
		}
		foreach (Entry entry2 in entries)
		{
			if (entry2 != null && entry2.skinId == "*" && entry2.weaponId == weaponId && entry2.pose == pose)
			{
				value = entry2;
				return true;
			}
		}
		if (pose != 0)
		{
			foreach (Entry entry3 in entries)
			{
				if (entry3 != null && entry3.skinId == text && entry3.weaponId == weaponId && entry3.pose == Pose.Hold)
				{
					value = entry3;
					return true;
				}
			}
			foreach (Entry entry4 in entries)
			{
				if (entry4 != null && entry4.skinId == "*" && entry4.weaponId == weaponId && entry4.pose == Pose.Hold)
				{
					value = entry4;
					return true;
				}
			}
		}
		return false;
	}
}
}
