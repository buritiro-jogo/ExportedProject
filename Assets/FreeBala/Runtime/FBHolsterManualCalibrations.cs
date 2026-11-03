using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    // Perfis persistentes de coldres. Guarde o asset em:
    // Assets/FreeBala/Resources/FBHolsterManualCalibrations.asset
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

        [SerializeField] List<Entry> presets = new();
        [SerializeField, HideInInspector] int revision;
        public IReadOnlyList<Entry> Presets => presets;
        public int Revision => revision;

        public bool TryFind(string skinId, string weaponId, out Entry found)
        {
            found = null;
            Entry shared = null;
            if (string.IsNullOrEmpty(weaponId)) return false;
            foreach (var e in presets)
            {
                if (e == null || e.weaponId != weaponId) continue;
                if (!string.IsNullOrEmpty(skinId) && e.skinId == skinId)
                {
                    found = e;
                    return true;
                }
                if (e.skinId == "*") shared = e;
            }
            found = shared;
            return found != null;
        }

#if UNITY_EDITOR
        // A ferramenta de Editor chama este metodo durante o Play Mode PAUSADO.
        public void Save(string skinId, string weaponId, Vector3 localPos, Quaternion localRot, Vector3 localScl)
        {
            var key = string.IsNullOrEmpty(skinId) ? "*" : skinId;
            var e = presets.Find(x => x != null && x.skinId == key && x.weaponId == weaponId);
            if (e == null)
            {
                e = new Entry { skinId = key, weaponId = weaponId };
                presets.Add(e);
            }
            e.localPosition = localPos;
            e.localRotation = localRot;
            e.localScale = localScl;
            revision++; // invalida caches de personagens presentes no Play Mode
        }
#endif
    }
}
