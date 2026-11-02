// BURITIRO - monta o Animator Controller de exemplo com as animações BUR_.
// Menu: BURITIRO > Animacoes > Criar Animator Controller de exemplo
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Buritiro.EditorTools
{
    public static class BuritiroAnimControllerBuilder
    {
        static Dictionary<string, AnimationClip> clipes;
        static readonly List<string> faltando = new List<string>();

        [MenuItem("BURITIRO/Animacoes/Criar Animator Controller de exemplo")]
        public static void Criar()
        {
            string pasta = AcharPasta();
            if (pasta == null)
            {
                EditorUtility.DisplayDialog("BURITIRO", "Nao achei a pasta BURITIRO_Animacoes dentro de Assets.", "OK");
                return;
            }
            CarregarClipes(pasta);
            faltando.Clear();

            string caminho = pasta + "/BUR_Personagem.controller";
            string caminhoMascara = pasta + "/BUR_Mascara_TroncoSuperior.mask";
            AssetDatabase.DeleteAsset(caminho);
            AssetDatabase.DeleteAsset(caminhoMascara);

            var mascara = CriarMascaraTronco();
            AssetDatabase.CreateAsset(mascara, caminhoMascara);

            var ac = AnimatorController.CreateAnimatorControllerAtPath(caminho);
            Parametros(ac);
            CamadaBase(ac);
            CamadaArmas(ac, mascara);
            CamadaDano(ac, mascara);

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            Selection.activeObject = ac;
            if (faltando.Count > 0)
                Debug.LogWarning("[BURITIRO] Clipes nao encontrados (reimporte a pasta): " + string.Join(", ", faltando));
            Debug.Log("[BURITIRO] Animator Controller criado em " + caminho);
        }

        // ---------- utilidades ----------

        static string AcharPasta()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                int i = p.IndexOf("/" + BuritiroAnimTabela.Pasta + "/");
                if (i >= 0) return p.Substring(0, i + BuritiroAnimTabela.Pasta.Length + 1);
            }
            return null;
        }

        static void CarregarClipes(string pasta)
        {
            clipes = new Dictionary<string, AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { pasta }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    var c = o as AnimationClip;
                    if (c != null && !c.name.StartsWith("__preview__")) clipes[c.name] = c;
                }
            }
        }

        static AnimationClip C(string nome)
        {
            AnimationClip c;
            if (clipes.TryGetValue(nome, out c)) return c;
            if (!faltando.Contains(nome)) faltando.Add(nome);
            return null;
        }

        static AnimatorState Estado(AnimatorStateMachine sm, string nome, string clipe, Vector3 pos)
        {
            var s = sm.AddState(nome, pos);
            s.motion = C(clipe);
            return s;
        }

        static AnimatorStateTransition Ir(AnimatorState de, AnimatorState para, float duracao = 0.15f)
        {
            var t = de.AddTransition(para);
            t.hasExitTime = false;
            t.duration = duracao;
            return t;
        }

        static AnimatorStateTransition IrAoTerminar(AnimatorState de, AnimatorState para, float exit = 0.9f, float duracao = 0.15f)
        {
            var t = de.AddTransition(para);
            t.hasExitTime = true;
            t.exitTime = exit;
            t.duration = duracao;
            return t;
        }

        static AnimatorStateTransition DeQualquer(AnimatorStateMachine sm, AnimatorState para, float duracao = 0.1f)
        {
            var t = sm.AddAnyStateTransition(para);
            t.hasExitTime = false;
            t.duration = duracao;
            t.canTransitionToSelf = false;
            return t;
        }

        static AvatarMask CriarMascaraTronco()
        {
            var m = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var parte = (AvatarMaskBodyPart)i;
                bool cima = parte == AvatarMaskBodyPart.Body || parte == AvatarMaskBodyPart.Head
                    || parte == AvatarMaskBodyPart.LeftArm || parte == AvatarMaskBodyPart.RightArm
                    || parte == AvatarMaskBodyPart.LeftFingers || parte == AvatarMaskBodyPart.RightFingers
                    || parte == AvatarMaskBodyPart.LeftHandIK || parte == AvatarMaskBodyPart.RightHandIK;
                m.SetHumanoidBodyPartActive(parte, cima);
            }
            return m;
        }

        // ---------- parâmetros ----------

        static void Parametros(AnimatorController ac)
        {
            ac.AddParameter("MoveX", AnimatorControllerParameterType.Float);       // -3..3 (1 andar, 2 correr, 3 sprint)
            ac.AddParameter("MoveY", AnimatorControllerParameterType.Float);       // -3..3
            ac.AddParameter("Velocidade", AnimatorControllerParameterType.Float);  // 0..1 (agachado)
            ac.AddParameter("Agachado", AnimatorControllerParameterType.Bool);
            ac.AddParameter("NoChao", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Pular", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Arma", AnimatorControllerParameterType.Int);          // 0 nada, 1 pistola, 2 fuzil, 3 espingarda
            ac.AddParameter("Mirando", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Atirar", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Recarregar", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Dano", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("IndiceDano", AnimatorControllerParameterType.Int);    // 0..3
            ac.AddParameter("Morrer", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("IndiceMorte", AnimatorControllerParameterType.Int);   // 0..3
            ac.AddParameter("Levantar", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Veiculo", AnimatorControllerParameterType.Int);       // 1 carro, 2 moto
            ac.AddParameter("EntrarVeiculo", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("SairVeiculo", AnimatorControllerParameterType.Trigger);

            var ps = ac.parameters;
            foreach (var p in ps) if (p.name == "NoChao") p.defaultBool = true;
            ac.parameters = ps;
        }

        // ---------- camada base (corpo inteiro) ----------

        static void CamadaBase(AnimatorController ac)
        {
            var sm = ac.layers[0].stateMachine;

            // Locomoção: blend tree 2D (idle, andar r=1, correr r=2, sprint r=3)
            BlendTree bt;
            var loco = ac.CreateBlendTreeInController("Locomocao", out bt, 0);
            bt.blendType = BlendTreeType.FreeformDirectional2D;
            bt.blendParameter = "MoveX";
            bt.blendParameterY = "MoveY";
            bt.AddChild(C("BUR_Idle"), Vector2.zero);
            string[] dirs = { "F", "FD", "D", "TD", "T", "TE", "E", "FE" };
            float d = 0.7071f;
            Vector2[] vet = { new Vector2(0, 1), new Vector2(d, d), new Vector2(1, 0), new Vector2(d, -d),
                              new Vector2(0, -1), new Vector2(-d, -d), new Vector2(-1, 0), new Vector2(-d, d) };
            for (int i = 0; i < dirs.Length; i++) bt.AddChild(C("BUR_Andar_" + dirs[i]), vet[i] * 1f);
            for (int i = 0; i < dirs.Length; i++) bt.AddChild(C("BUR_Correr_" + dirs[i]), vet[i] * 2f);
            foreach (int i in new[] { 0, 1, 2, 6, 7 }) bt.AddChild(C("BUR_Sprint_" + dirs[i]), vet[i] * 3f);
            sm.defaultState = loco;

            // Agachado: blend 1D por Velocidade
            BlendTree btAg;
            var agachado = ac.CreateBlendTreeInController("Agachado", out btAg, 0);
            btAg.blendType = BlendTreeType.Simple1D;
            btAg.blendParameter = "Velocidade";
            btAg.AddChild(C("BUR_Agachar_Idle"), 0f);
            btAg.AddChild(C("BUR_Agachar_Andar_F"), 1f);
            Ir(loco, agachado, 0.2f).AddCondition(AnimatorConditionMode.If, 0, "Agachado");
            Ir(agachado, loco, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Agachado");

            // Pulo e queda
            var puloIni = Estado(sm, "Pulo_Inicio", "BUR_Pulo_Inicio", new Vector3(300, 250));
            var queda = Estado(sm, "Queda_Loop", "BUR_Queda_Loop", new Vector3(550, 250));
            var aterr = Estado(sm, "Pulo_Aterrissar", "BUR_Pulo_Aterrissar", new Vector3(800, 250));
            Ir(loco, puloIni, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Pular");
            Ir(agachado, puloIni, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Pular");
            IrAoTerminar(puloIni, queda, 0.85f, 0.15f);
            Ir(loco, queda, 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0, "NoChao");
            var tAt = Ir(queda, aterr, 0.08f); tAt.AddCondition(AnimatorConditionMode.If, 0, "NoChao");
            IrAoTerminar(aterr, loco, 0.6f, 0.2f);

            // Morte (4 variações) e levantar do chão
            var levantar = Estado(sm, "Levantar_do_Chao", "BUR_Levantar_do_Chao", new Vector3(550, 500));
            IrAoTerminar(levantar, loco, 0.9f, 0.25f);
            for (int i = 0; i < 4; i++)
            {
                var morte = Estado(sm, "Morte_0" + (i + 1), "BUR_Morte_0" + (i + 1), new Vector3(300, 400 + i * 60));
                var t = DeQualquer(sm, morte, 0.1f);
                t.AddCondition(AnimatorConditionMode.If, 0, "Morrer");
                t.AddCondition(AnimatorConditionMode.Equals, i, "IndiceMorte");
                Ir(morte, levantar, 0.2f).AddCondition(AnimatorConditionMode.If, 0, "Levantar");
            }

            // Veículos (carro = 1, moto = 2). Entrar/sair ainda usam clipes provisórios (sentar/levantar).
            Veiculo(sm, loco, 1, "Carro", new Vector3(1050, 0));
            Veiculo(sm, loco, 2, "Moto", new Vector3(1050, 250));
        }

        static void Veiculo(AnimatorStateMachine sm, AnimatorState loco, int tipo, string nome, Vector3 pos)
        {
            var entrar = Estado(sm, nome + "_Entrar", "BUR_Veiculo_Entrar_Provisorio", pos);
            var dirigir = Estado(sm, nome + "_Dirigir", "BUR_Veiculo_Dirigir", pos + new Vector3(250, 0));
            var sair = Estado(sm, nome + "_Sair", "BUR_Veiculo_Sair_Provisorio", pos + new Vector3(500, 0));
            var t = DeQualquer(sm, entrar, 0.15f);
            t.AddCondition(AnimatorConditionMode.If, 0, "EntrarVeiculo");
            t.AddCondition(AnimatorConditionMode.Equals, tipo, "Veiculo");
            IrAoTerminar(entrar, dirigir, 0.9f, 0.15f);
            Ir(dirigir, sair, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "SairVeiculo");
            IrAoTerminar(sair, loco, 0.9f, 0.2f);
        }

        // ---------- camada de armas (tronco superior) ----------

        static AnimatorStateMachine NovaCamada(AnimatorController ac, string nome, AvatarMask mascara)
        {
            ac.AddLayer(nome);
            var camadas = ac.layers;
            int i = camadas.Length - 1;
            camadas[i].defaultWeight = 1f;
            camadas[i].avatarMask = mascara;
            camadas[i].blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = camadas;
            return camadas[i].stateMachine;
        }

        static void CamadaArmas(AnimatorController ac, AvatarMask mascara)
        {
            var sm = NovaCamada(ac, "Armas", mascara);
            var vazio = sm.AddState("Sem_Arma", new Vector3(250, 0));
            vazio.writeDefaultValues = false;
            sm.defaultState = vazio;
            Arma(sm, vazio, 1, "Pistola", "BUR_Pistola_Idle", new Vector3(0, 150));
            Arma(sm, vazio, 2, "Fuzil", "BUR_Fuzil_Segurar", new Vector3(0, 350));
            Arma(sm, vazio, 3, "Espingarda", "BUR_Espingarda_Segurar", new Vector3(0, 550));
        }

        static void Arma(AnimatorStateMachine sm, AnimatorState vazio, int indice, string nome, string clipeSegurar, Vector3 pos)
        {
            var segurar = Estado(sm, nome + "_Segurar", clipeSegurar, pos);
            var mirar = Estado(sm, nome + "_Mirar", "BUR_" + nome + "_Mirar", pos + new Vector3(250, 0));
            var atirar = Estado(sm, nome + "_Atirar", "BUR_" + nome + "_Atirar", pos + new Vector3(500, 0));
            var recarregar = Estado(sm, nome + "_Recarregar", "BUR_" + nome + "_Recarregar", pos + new Vector3(250, 80));

            Ir(vazio, segurar, 0.2f).AddCondition(AnimatorConditionMode.Equals, indice, "Arma");
            Ir(segurar, vazio, 0.2f).AddCondition(AnimatorConditionMode.NotEqual, indice, "Arma");
            Ir(mirar, vazio, 0.2f).AddCondition(AnimatorConditionMode.NotEqual, indice, "Arma");

            Ir(segurar, mirar, 0.12f).AddCondition(AnimatorConditionMode.If, 0, "Mirando");
            Ir(mirar, segurar, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0, "Mirando");

            Ir(mirar, atirar, 0.03f).AddCondition(AnimatorConditionMode.If, 0, "Atirar");
            Ir(segurar, atirar, 0.05f).AddCondition(AnimatorConditionMode.If, 0, "Atirar");
            Ir(atirar, atirar, 0.02f).AddCondition(AnimatorConditionMode.If, 0, "Atirar"); // rajada
            IrAoTerminar(atirar, mirar, 0.8f, 0.1f);

            Ir(segurar, recarregar, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "Recarregar");
            Ir(mirar, recarregar, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "Recarregar");
            IrAoTerminar(recarregar, segurar, 0.92f, 0.2f);
        }

        // ---------- camada de dano (tronco superior) ----------

        static void CamadaDano(AnimatorController ac, AvatarMask mascara)
        {
            var sm = NovaCamada(ac, "Dano", mascara);
            var vazio = sm.AddState("Sem_Dano", new Vector3(250, 0));
            vazio.writeDefaultValues = false;
            sm.defaultState = vazio;
            string[] danos = { "BUR_Dano_01", "BUR_Dano_02", "BUR_Dano_Peito", "BUR_Dano_Cabeca" };
            for (int i = 0; i < danos.Length; i++)
            {
                var s = Estado(sm, danos[i].Replace("BUR_", ""), danos[i], new Vector3(0, 120 + i * 70));
                var t = DeQualquer(sm, s, 0.05f);
                t.AddCondition(AnimatorConditionMode.If, 0, "Dano");
                t.AddCondition(AnimatorConditionMode.Equals, i, "IndiceDano");
                IrAoTerminar(s, vazio, 0.85f, 0.15f);
            }
        }
    }
}
#endif
