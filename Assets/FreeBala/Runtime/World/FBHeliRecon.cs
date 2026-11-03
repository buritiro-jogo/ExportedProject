using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    /// <summary>
    /// Sistema de Reconhecimento do Helicóptero (Modos 4v4 e Clássico):
    /// O helicóptero patrulha o mapa projetando uma área de varredura (radar/danger zone).
    /// Qualquer jogador ou inimigo que estiver ou passar por essa área é marcado e fica visível
    /// no minimapa e no mapa tático em tela cheia [M] para todos durante 60 segundos.
    /// </summary>
    public static class FBHeliRecon
    {
        public static float ScanRadius = 35f;
        public static Vector3 ScanPosition;
        public static bool IsScanning;
        public static float LastPingTime;

        static readonly Dictionary<FBCharacter, float> taggedUntil = new();

        public static void SetScanState(bool scanning, Vector3 groundPos)
        {
            IsScanning = scanning;
            ScanPosition = groundPos;
        }

        public static void Tag(FBCharacter c, float duration = 60f)
        {
            if (!c || !c.alive) return;
            float expire = Time.time + duration;
            if (!taggedUntil.ContainsKey(c) || taggedUntil[c] < expire)
            {
                taggedUntil[c] = expire;
                if (c.isPlayer)
                {
                    FBHUD.I?.Toast("⚠️ ALERTA", "RECONHECIMENTO AÉREO", "VOCÊ FOI REVELADO PELO HELICÓPTERO (60s)", new Color(1f, 0.4f, 0.1f));
                }
            }
        }

        public static bool IsTagged(FBCharacter c)
        {
            if (!c || !c.alive) return false;
            return taggedUntil.TryGetValue(c, out float expire) && Time.time < expire;
        }

        public static float RemainingSeconds(FBCharacter c)
        {
            if (c && taggedUntil.TryGetValue(c, out float expire))
            {
                return Mathf.Max(0, expire - Time.time);
            }
            return 0;
        }

        public static List<FBCharacter> GetActiveTagged()
        {
            var list = new List<FBCharacter>();
            var now = Time.time;
            foreach (var kv in taggedUntil)
            {
                if (kv.Key && kv.Key.alive && kv.Value > now)
                {
                    list.Add(kv.Key);
                }
            }
            return list;
        }

        public static void Clear()
        {
            taggedUntil.Clear();
            IsScanning = false;
        }
    }
}

