// r47 (pedido 30/10: "remova os pequenos baldes amarelos e as plantas verdes em vasos laranjas, bem como outros objetos semelhantes
// que estejam com aparência artificial"): o nó VJ11_Becos da Vila Jardim (tools/vj_full/v11_rua.py) era todo de primitivas — baldes
// de plástico, vasos de terracota com "plantas" de cilindros verdes, botijões, caixas de papelão e lixeiras de cilindro colorido no
// poste. O editor marca o nó como EditorOnly na cena (FBR47.Prep / FBImport.Maps: sai do build) e o gerador não cria mais; isto cobre
// cena antiga aberta por laboratório ou reimportação sem o passo do editor. Objetos legítimos (biblioteca br_props, prédios) não entram.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public static class FBMapRetire
    {
        public static readonly string[] NODES = { "VJ11_Becos" };
        public static int Retired;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }
        static void OnScene(Scene s, LoadSceneMode m)
        {
            foreach (var root in s.GetRootGameObjects())
            {
                if (!root.name.StartsWith("Map_")) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (System.Array.IndexOf(NODES, t.name) >= 0 && t.gameObject.activeSelf) { t.gameObject.SetActive(false); Retired++; Debug.Log($"[FreeBala][mapa] {t.name} retirado (objetos artificiais) em {root.name}"); }
            }
        }
    }
}
