// Monumento "EU AMO BOA VISTA" (Praça das Águas) — CONGELADO por pedido do usuário: não pode mudar em hipótese alguma.
// A geometria vem do map.glb (verificada por tools/pda_monumento_check.py). Os materiais, porém, eram recalculados a cada importação
// (FBMapLook, vegetação, índice das texturas extraídas): com o mapa novo a base verde perdeu a textura e o coração apontava para outra
// imagem. Aqui, depois de todo o processamento do mapa, as peças do letreiro (PA_Letra*, PA_Letreiro_*) recebem os materiais aprovados,
// copiados da versão que estava no jogo (git HEAD de praca_aguas_materiais.mat, 19/10/2026) para Art/Monumento/ — com as próprias
// texturas congeladas (heart_mosaic.jpg = bytes do glb; base_grama.png = chao_grama_v2). Nada aqui pode ser regerado automaticamente.
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBMonumento
    {
        const string Dir = "Assets/FreeBala/Art/Monumento";
        public static int Apply(string id, GameObject map)
        {
            if (id != "praca_aguas" || !map) return 0;
            int n = 0, miss = 0;
            foreach (var r in map.GetComponentsInChildren<MeshRenderer>(true).Where(r => IsPart(r.transform)))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue; var name = mats[i].name.Replace(" (Instance)", "");
                    var frozen = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/{name}.mat");
                    if (frozen) { mats[i] = frozen; n++; } else { miss++; Debug.LogWarning($"[FreeBala][monumento] sem material congelado para {name} em {r.name}"); }
                }
                r.sharedMaterials = mats;
            }
            Debug.Log($"[FreeBala][monumento] letreiro EU AMO BOA VISTA: {n} materiais congelados aplicados{(miss > 0 ? $", {miss} SEM correspondente" : "")}");
            return n;
        }
        static bool IsPart(Transform t)
        {
            for (var q = t; q; q = q.parent) if (q.name.StartsWith("PA_Letra") || q.name.StartsWith("PA_Letreiro")) return true;
            return false;
        }
    }
}
