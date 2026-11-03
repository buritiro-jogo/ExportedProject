// Liga a água do mapa (FBWater.Setup) quando a partida tem o nível carregado e mantém o tique (luz noturna das fontes, som).
// Fica em arquivo próprio porque o FBWaterSetup salva este componente na cena do mapa (classe = nome do arquivo, senão o build não
// acha o script). Se o mapa foi importado antes do FBWaterSetup existir, um é criado sozinho ao carregar a cena.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBWaterRuntime : MonoBehaviour
    {
        public string mapId;
        FBWater.State st; bool done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); }
        static void OnLoaded(Scene sc, LoadSceneMode mode)
        {
            if (!sc.name.StartsWith("Map_") || FindAnyObjectByType<FBWaterRuntime>()) return;
            var g = new GameObject("FB_Agua"); SceneManager.MoveGameObjectToScene(g, sc); g.AddComponent<FBWaterRuntime>().mapId = sc.name.Substring(4);
        }

        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBWaterRuntime.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (!done)
            {
                var m = FBMatch.I; if (!m || m.Level == null) return;                 // espera a partida montar o nível
                done = true; st = FBWater.Setup(m.Level);
            }
            FBWater.Tick(st, Time.deltaTime);
        }
        void OnDestroy() { if (st != null) { if (st.drops) { var mf = st.drops.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) Destroy(mf.sharedMesh); Destroy(st.drops); } FBWater.Stop(); } }
    }
}
