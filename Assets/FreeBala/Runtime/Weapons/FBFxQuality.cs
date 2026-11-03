// Limites e LOD dos efeitos de combate (sangue, decals, impactos, fumaça, cartuchos). Tudo aqui é só visual: nunca muda dano.
// Três níveis — 0 baixo (WebGL/celular fraco), 1 médio (celular), 2 alto (PC) — escolhidos pela plataforma; o perfil gráfico do
// jogo (FBQuality, agente de desempenho) chama Apply(nível) quando o jogador troca a qualidade. Os pools são criados UMA vez no
// carregamento com a capacidade do nível alto ou do nível atual (o que for menor por plataforma) e nunca crescem na partida.
using UnityEngine;

namespace FreeBala
{
    public static class FBFxQuality
    {
        public static int Level { get; private set; } = -1;
        public static float ParticleMul = 1;     // multiplica a contagem de partículas emitidas por efeito
        public static int MaxDecals = 120;       // decals de cenário (sangue + buracos + queimado) ativos ao mesmo tempo
        public static float DecalLife = 90;      // segundos até começar a sumir (os mais antigos são reciclados antes, se faltar vaga)
        public static int MaxWounds = 64;        // ferimentos presos nos ossos (todos os personagens)
        public static int WoundsPerChar = 6;
        public static bool Shells = true;        // cartuchos físicos saltando (colisão de partícula)
        public static bool UseProjector = true;  // URP Decal Projector (só se o renderer tiver o DecalRendererFeature)
        public static bool Drips = true;         // gotas no chão sob o ferido
        public static int FrameBudget = 420;     // partículas emitidas por quadro (soma de todos os efeitos)
        public static float FarCull = 90;        // além disso só decal (sem partícula)

        // capacidade física dos pools (fixa por plataforma; Apply só reduz o uso dentro dela)
        public static int PoolDecals => Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer ? 64 : 160;
        public static int PoolWounds => Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer ? 32 : 80;

        public static int AutoLevel()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3000 ? 0 : 1;
            if (Application.isMobilePlatform) return SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3500 ? 0 : 1;
            return 2;
        }
        public static void Ensure() { if (Level < 0) Apply(AutoLevel()); }

        public static void Apply(int level)
        {
            Level = Mathf.Clamp(level, 0, 2);
            switch (Level)
            {
                case 0: ParticleMul = .45f; MaxDecals = 36; DecalLife = 35; MaxWounds = 16; WoundsPerChar = 3; Shells = false; UseProjector = false; Drips = false; FrameBudget = 120; FarCull = 45; break;
                case 1: ParticleMul = .7f; MaxDecals = 64; DecalLife = 60; MaxWounds = 32; WoundsPerChar = 4; Shells = true; UseProjector = false; Drips = true; FrameBudget = 220; FarCull = 65; break;
                default: ParticleMul = 1; MaxDecals = 160; DecalLife = 120; MaxWounds = 80; WoundsPerChar = 6; Shells = true; UseProjector = true; Drips = true; FrameBudget = 420; FarCull = 90; break;
            }
            MaxDecals = Mathf.Min(MaxDecals, PoolDecals); MaxWounds = Mathf.Min(MaxWounds, PoolWounds);
            FBWater.ApplyQuality(Level);                                          // água: variante leve / profundidade
        }

        // ---- LOD por distância/visão: 1 perto, cai até ~0,3 no meio, 0 longe ou atrás da câmera (não vale emitir)
        static Camera cam; static float camT;
        public static Camera Cam
        {
            get
            {
                if (!cam || !cam.isActiveAndEnabled || Time.unscaledTime > camT) { camT = Time.unscaledTime + 1; var p = FBPlayer.I; cam = p && p.cam && p.cam.isActiveAndEnabled ? p.cam : Camera.main; }
                return cam;
            }
        }
        public static float Lod(Vector3 p)
        {
            var c = Cam; if (!c) return 1;
            var d = p - c.transform.position; float dist = d.magnitude;
            if (dist > FarCull) return 0;
            if (dist > 6 && Vector3.Dot(d, c.transform.forward) < -.2f * dist) return 0;            // atrás da câmera
            float near = Level >= 2 ? 22 : 14;
            return dist <= near ? 1 : Mathf.Lerp(1, .3f, (dist - near) / Mathf.Max(1, FarCull - near));
        }

        // ---- orçamento de partículas por quadro (todos os efeitos somados)
        static int budgetFrame = -1, budgetLeft;
        public static int Take(int want)
        {
            if (budgetFrame != Time.frameCount) { budgetFrame = Time.frameCount; budgetLeft = FrameBudget; }
            int n = Mathf.Min(want, budgetLeft); budgetLeft -= n; return Mathf.Max(0, n);
        }
        public static int Count(float baseCount, float lod)
        {
            float f = Mathf.Max(0, baseCount * ParticleMul * lod); int n = (int)f; if (Random.value < f - n) n++;   // arredonda no sorteio: 0,4 gota = 40% de chance
            return Take(n);
        }
    }
}
