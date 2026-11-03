// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/BURITIRO_Animacoes/Editor/BuritiroAnimImporter.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// BURITIRO - configura sozinho a importação das animações desta pasta.
// Tudo o que estiver dentro de uma pasta chamada "BURITIRO_Animacoes" vira Humanoid,
// recebe o nome padronizado (BUR_...), Loop Time certo e "Bake Into Pose" ligado.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Buritiro.EditorTools
{
    public static class BuritiroAnimTabela
    {
        public const string Pasta = "BURITIRO_Animacoes";

        // Nome do take dentro do FBX da Quaternius -> (nome padronizado, loop)
        public static readonly Dictionary<string, KeyValuePair<string, bool>> Quaternius =
            new Dictionary<string, KeyValuePair<string, bool>>
        {
            // UAL1
            ["Crouch_Idle_Loop"] = P("BUR_Agachar_Idle", true),
            ["Crouch_Fwd_Loop"] = P("BUR_Agachar_Andar_F", true),
            ["Roll"] = P("BUR_Rolar", false),
            ["Hit_Chest"] = P("BUR_Dano_Peito", false),
            ["Hit_Head"] = P("BUR_Dano_Cabeca", false),
            ["Death01"] = P("BUR_Morte_04", false),
            ["Driving_Loop"] = P("BUR_Veiculo_Dirigir", true),
            ["Sitting_Enter"] = P("BUR_Veiculo_Entrar_Provisorio", false),
            ["Sitting_Exit"] = P("BUR_Veiculo_Sair_Provisorio", false),
            ["Sitting_Idle_Loop"] = P("BUR_Sentado_Idle", true),
            ["Pistol_Idle_Loop"] = P("BUR_Pistola_Idle", true),
            ["Pistol_Aim_Neutral"] = P("BUR_Pistola_Mira_Meio", true),
            ["Pistol_Aim_Up"] = P("BUR_Pistola_Mira_Cima", true),
            ["Pistol_Aim_Down"] = P("BUR_Pistola_Mira_Baixo", true),
            ["Pistol_Shoot"] = P("BUR_Pistola_Atirar_Q", false),
            ["Pistol_Reload"] = P("BUR_Pistola_Recarregar_Q", false),
            ["Swim_Idle_Loop"] = P("BUR_Nadar_Idle", true),
            ["Swim_Fwd_Loop"] = P("BUR_Nadar_F", true),
            ["Interact"] = P("BUR_Interagir", false),
            ["Punch_Jab"] = P("BUR_Soco_Jab", false),
            ["Punch_Cross"] = P("BUR_Soco_Direto", false),
            // UAL2
            ["LayToIdle"] = P("BUR_Levantar_do_Chao", false),
            ["Hit_Knockback"] = P("BUR_Dano_Empurrao", false),
            ["ClimbUp_1m"] = P("BUR_Escalar_1m", false),
            ["Slide_Start"] = P("BUR_Deslizar_Inicio", false),
            ["Slide_Loop"] = P("BUR_Deslizar_Loop", true),
            ["Slide_Exit"] = P("BUR_Deslizar_Fim", false),
            ["Idle_TalkingPhone_Loop"] = P("BUR_Celular_Idle", true),
        };

        static KeyValuePair<string, bool> P(string n, bool loop) { return new KeyValuePair<string, bool>(n, loop); }

        // Clipes de arquivo único (Kevin Iglesias) que repetem
        public static bool LoopPorNome(string nome)
        {
            return nome == "BUR_Idle" || nome == "BUR_Idle_Variacao" || nome == "BUR_Queda_Loop"
                || nome == "BUR_Armado_Idle" || nome == "BUR_Maos_Fechadas"
                || nome.StartsWith("BUR_Andar_") || nome.StartsWith("BUR_Correr_") || nome.StartsWith("BUR_Sprint_")
                || nome.EndsWith("_Mirar") || nome.EndsWith("_Mirar_UmaMao") || nome.EndsWith("_Segurar");
        }

        public static bool NaPasta(string caminho)
        {
            return caminho.Replace('\\', '/').Contains("/" + Pasta + "/");
        }
    }

    public class BuritiroAnimImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!BuritiroAnimTabela.NaPasta(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            if (assetPath.Contains("Quaternius")) mi.bakeAxisConversion = true;
        }

        void OnPreprocessAnimation()
        {
            if (!BuritiroAnimTabela.NaPasta(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            bool multi = assetPath.Contains("Quaternius");
            var origem = (!multi && mi.clipAnimations.Length > 0) ? mi.clipAnimations : mi.defaultClipAnimations;
            var saida = new List<ModelImporterClipAnimation>();
            foreach (var c in origem)
            {
                string nome;
                bool loop;
                if (multi)
                {
                    string take = c.takeName;
                    int barra = take.LastIndexOf('|');
                    if (barra >= 0) take = take.Substring(barra + 1);
                    KeyValuePair<string, bool> item;
                    if (!BuritiroAnimTabela.Quaternius.TryGetValue(take, out item)) continue;
                    nome = item.Key;
                    loop = item.Value;
                }
                else
                {
                    nome = Path.GetFileNameWithoutExtension(assetPath);
                    loop = BuritiroAnimTabela.LoopPorNome(nome);
                }
                c.name = nome;
                c.loopTime = loop;
                c.loopPose = false;
                // Bake Into Pose: o código do jogo move o personagem, a animação fica no lugar
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                // Based Upon = Original
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                c.heightFromFeet = false;
                saida.Add(c);
            }
            if (saida.Count > 0) mi.clipAnimations = saida.ToArray();
        }

        [MenuItem("BURITIRO/Animacoes/Reimportar todas as animacoes")]
        static void Reimportar()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (BuritiroAnimTabela.NaPasta(p))
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("[BURITIRO] Animacoes reimportadas.");
        }
    }
}
#endif
