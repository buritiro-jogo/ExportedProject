// Importação da rodada 38 (22/10/2026), sem refazer mapas: pedido do usuário depois do teste da rodada 37.
//  · personagens removidos: TOMIZAWA, DANÇARINO, RUMBA, CIVVIE, PENNY ("IVVIEPENNY"), REALISTA, JESSICA, MARIAH, CAMINHANTE e KADEN — o pacote
//    inteiro de pedestres n2_* (prefabs, avatares, GLBs e os 176 clipes NPC2_ só deles). Os arquivos do jogo web foram para
//    assets/chars_retirados; o De Rolê usa os regionais como pedestres (FBRoleBR.Roster) até os NPCs novos entrarem.
//  · andar desarmado novo por sexo (assets/walk_regional.json → REG_Walk_M/F) e o anel de andar da RelaxedLocomotion na velocidade nova:
//    reaproveita o import de clipes/controllers da rodada 37 (FBImportR37.Run).
// Antes: node tools/unity/export_data.mjs. Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportR38.Batch
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR38
    {
        const string Imp = FBImport.Imp;
        public static void Batch()
        {
            int code = 0;
            try { Run(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r38] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar rodada 38 (personagens, andar)", priority = 8)]
        public static void Run()
        {
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            // ---- pedestres n2_* saem (prefab, avatar, glTF e clipes próprios)
            int nd = 0, nk = 0;
            foreach (var f in Directory.GetFiles(FBCharImport.PrefabDir, "n2_*.prefab")) if (AssetDatabase.DeleteAsset(f.Replace('\\', '/'))) nd++;
            foreach (var f in Directory.GetFiles(FBCharImport.AvatarDir, "n2_*.asset")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
            foreach (var f in Directory.GetFiles($"{Imp}/Chars", "n2_*.gltf")) { var b = Path.ChangeExtension(f, null); AssetDatabase.DeleteAsset(f.Replace('\\', '/')); AssetDatabase.DeleteAsset((b + ".bin").Replace('\\', '/')); AssetDatabase.DeleteAsset((b + FBGlbUnpack.TexSuffix).Replace('\\', '/')); }
            foreach (var f in Directory.GetFiles(FBAnimImport.ClipDir, "NPC2_*.anim")) if (AssetDatabase.DeleteAsset(f.Replace('\\', '/'))) nk++;
            AssetDatabase.SaveAssets();
            // ---- clipes (andar novo) + controllers + registro de clipes/veículos (rodada 37)
            FBImportR37.Run();
            // ---- registro de personagens
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            reg.charIds = Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab").Where(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList();
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            bool walk = reg.clips.Any(c => c && c.name == FBAnimProfile.WalkMale) && reg.clips.Any(c => c && c.name == FBAnimProfile.WalkFemale);
            Debug.Log($"[FreeBala][r38] pedestres n2_* removidos {nd} (clipes NPC2_ {nk}); andar novo {(walk ? "ok" : "FALTANDO")}; registro {reg.charIds.Count} personagens: {string.Join(",", reg.charIds)}");
        }
    }
}
