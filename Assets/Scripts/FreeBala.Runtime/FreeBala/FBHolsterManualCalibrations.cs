using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
[CreateAssetMenu(fileName = "FBHolsterManualCalibrations", menuName = "FreeBala/Coldres/Calibracoes manuais")]
public sealed class FBHolsterManualCalibrations : ScriptableObject
{
	[Serializable]
	public sealed class Entry
	{
		[Tooltip("Identificador da skin, ou * para um preset compartilhado")]
		public string skinId = "*";

		public string weaponId;

		public Vector3 localPosition;

		public Quaternion localRotation = Quaternion.identity;

		public Vector3 localScale = Vector3.one;

		[Tooltip("Opcional: afasta a arma se detectar colisao com o corpo. Pode modificar a posicao calibrada.")]
		public bool preventClipping;
	}

	[SerializeField]
	private List<Entry> presets = new List<Entry>();

	[SerializeField]
	[HideInInspector]
	private int revision;

	public IReadOnlyList<Entry> Presets => presets;

	public int Revision => revision;

	public bool TryFind(string skinId, string weaponId, out Entry found)
	{
		found = null;
		Entry entry = null;
		if (string.IsNullOrEmpty(weaponId))
		{
			return false;
		}
		foreach (Entry preset in presets)
		{
			if (preset != null && !(preset.weaponId != weaponId))
			{
				if (!string.IsNullOrEmpty(skinId) && preset.skinId == skinId)
				{
					found = preset;
					return true;
				}
				if (preset.skinId == "*")
				{
					entry = preset;
				}
			}
		}
		found = entry;
		return found != null;
	}
}
}
