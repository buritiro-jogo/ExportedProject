using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    // Configuracoes de arma empunhada em terceira pessoa, independentes dos coldres.
    // O banco e carregado por Resources pelo FBCharacter em runtime.
    [CreateAssetMenu(fileName = "FBWeaponPoseCalibrations", menuName = "FreeBala/Armas na mao/Calibracoes")]
    public sealed class FBWeaponPoseCalibrations : ScriptableObject
    {
        public enum Pose { Hold, Aim, Fire, Reload }

        [Serializable]
        public sealed class Entry
        {
            public string skinId = "*";
            public string weaponId;
            public Pose pose;
            public Vector3 positionOffset = Vector3.zero;
            public Quaternion rotationOffset = Quaternion.identity;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();
        [SerializeField, HideInInspector] int revision;
        public IReadOnlyList<Entry> Entries => entries;
        public int Revision => revision;

        // Ordem de prioridade: skin+pose, global+pose, skin+Hold, global+Hold.
        public bool TryFind(string skinId, string weaponId, Pose pose, out Entry value)
        {
            value = null;
            if (string.IsNullOrEmpty(weaponId)) return false;
            string sk = string.IsNullOrEmpty(skinId) ? "*" : skinId;
            foreach (var e in entries)
                if (e != null && e.skinId == sk && e.weaponId == weaponId && e.pose == pose) { value = e; return true; }
            foreach (var e in entries)
                if (e != null && e.skinId == "*" && e.weaponId == weaponId && e.pose == pose) { value = e; return true; }
            if (pose != Pose.Hold)
            {
                foreach (var e in entries)
                    if (e != null && e.skinId == sk && e.weaponId == weaponId && e.pose == Pose.Hold) { value = e; return true; }
                foreach (var e in entries)
                    if (e != null && e.skinId == "*" && e.weaponId == weaponId && e.pose == Pose.Hold) { value = e; return true; }
            }
            return false;
        }

#if UNITY_EDITOR
        public void Save(string skinId, string weaponId, Pose pose, Vector3 pos, Quaternion rot)
        {
            var sk = string.IsNullOrEmpty(skinId) ? "*" : skinId;
            var entry = entries.Find(e => e != null && e.skinId == sk && e.weaponId == weaponId && e.pose == pose);
            if (entry == null)
            {
                entry = new Entry { skinId = sk, weaponId = weaponId, pose = pose };
                entries.Add(entry);
            }
            entry.positionOffset = pos;
            entry.rotationOffset = rot.normalized;
            revision++;
        }
#endif
    }
}
