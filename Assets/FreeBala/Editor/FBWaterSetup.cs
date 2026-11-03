// Água no import do mapa (chamado pelo FBImport): troca os materiais de água do glTF (lâmina, rio, brejo, jatos — pelo nome) pelos
// materiais-modelo de Resources/Water e põe o FBWaterRuntime na raiz, que monta as fontes/fluxo/som na partida.
// Tudo salvo na cena: o build já leva os shaders FreeBala/Water* e não troca material em runtime (o runtime só repete a troca em
// mapas importados antes deste passo existir).
using UnityEditor;
using UnityEngine;

namespace FreeBala
{
    public static class FBWaterSetup
    {
        const string Dir = "Assets/FreeBala/Resources/Water/";

        public static void Apply(string mapId, GameObject root)
        {
            if (!root) return;
            int n = FBWater.SwapMaterials(root, k => { var name = FBWater.MaterialName(k); return name != null ? AssetDatabase.LoadAssetAtPath<Material>(Dir + name + ".mat") : null; });
            if (GameData.Maps.Count == 0) GameData.Load();
            int jets = FBWater.ReadJets(GameData.Maps.TryGetValue(mapId, out var m) ? m.level : null).Count;
            var rt = root.GetComponentInChildren<FBWaterRuntime>(true) ?? Object.FindAnyObjectByType<FBWaterRuntime>(FindObjectsInactive.Include);   // um só por cena
            if (n > 0 || jets > 0) { if (!rt) rt = new GameObject("FB_Agua").AddComponent<FBWaterRuntime>(); rt.transform.SetParent(root.transform, false); rt.mapId = mapId; }
            Debug.Log($"[FreeBala] água {mapId}: {n} renderer(s) com material novo, {jets} jato(s) de fonte");
        }

        [MenuItem("FreeBala/Água: aplicar na cena aberta")]
        static void ApplyOpen()
        {
            GameData.Load(); var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); var id = sc.name.StartsWith("Map_") ? sc.name.Substring(4) : sc.name;
            foreach (var g in sc.GetRootGameObjects()) Apply(id, g);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        }
    }
}
