// REFLEXO DO CÉU EM TEMPO REAL (rodada 64). A sonda de reflexo de cada mapa (Map_*_Reflexo.exr) foi assada com o céu do MEIO-DIA:
// no pôr do sol os carros, vidros e poças continuavam refletindo céu azul. Aqui uma ReflectionProbe em tempo real, do tamanho do mapa,
// renderiza SÓ o céu (cullingMask = 0: custo de 6 faces de 128 px do skybox) quando o céu muda; a assada some com o sol baixo
// (FBEnvState.bakedProbeK) e esta assume. Só nos níveis com reflexo em tempo real (FBQuality.RealtimeReflections / PC); no mobile e na web
// fica a assada com a intensidade reduzida à noite (como antes).
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public static class FBEnvProbe
    {
        static ReflectionProbe probe; static float lastEl = 999, lastNight = -1, lastT; static int pending = -1;
        public static bool Live => probe && probe.enabled;
        public static void Build(Transform parent)
        {
            probe = null; lastEl = 999; lastNight = -1; pending = -1;
            bool rt = !FBQuality.Mobile && Application.platform != RuntimePlatform.WebGLPlayer && FBQuality.Level >= FBQLevel.Alto;   // só o céu: barato (Alto e Ultra)
            if (!rt) return;
            var go = new GameObject("FB_ReflexoCeu"); go.transform.SetParent(parent, false);
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting; probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.cullingMask = 0; probe.clearFlags = ReflectionProbeClearFlags.Skybox; probe.resolution = 128; probe.hdr = true;
            probe.size = new Vector3(4000, 1200, 4000); probe.importance = 0; probe.boxProjection = false; probe.blendDistance = 0; probe.intensity = 1;
            probe.nearClipPlane = .3f; probe.farClipPlane = 1000; probe.shadowDistance = 0;
        }
        // chamado a cada aplicação do ambiente: re-renderiza quando o sol andou ≥ 0,4° ou a noite mudou (no ciclo de 7 min ≈ a cada 3 s)
        public static void Changed(in FBEnvState s)
        {
            if (!probe) return;
            if (Mathf.Abs(s.sunEl - lastEl) < .4f && Mathf.Abs(s.skyNight - lastNight) < .02f && Time.unscaledTime - lastT < 30) return;
            lastEl = s.sunEl; lastNight = s.skyNight; lastT = Time.unscaledTime;
            if (pending >= 0 && !probe.IsFinishedRendering(pending)) return;
            probe.intensity = 1; pending = probe.RenderProbe();
        }
    }
}
