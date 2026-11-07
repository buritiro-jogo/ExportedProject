// Rodada 57 (05/11/2026) — prepara os personagens e os assets do rig de combate novo (KINEMATION FPS Animation Framework).
//
// Por que é uma ferramenta de Editor e não código de tempo de jogo: o KRigComponent guarda a lista de ossos SERIALIZADA, e
// RefreshHierarchy()/GetHierarchy() do pacote são #if UNITY_EDITOR. Então o componente, os ossos virtuais e a lista têm de
// entrar nos PREFABS aqui; em tempo de jogo o FBFpsaf só usa o que já está gravado.
//
// O que faz:
//  1) em cada prefab de personagem: KRigComponent na raiz + 5 ossos virtuais (IK das duas mãos, dica dos dois cotovelos e
//     WeaponBone), cada um com um KVirtualElement. O alvo deles é trocado em tempo de jogo pelo FBFpsaf quando a arma muda;
//     no prefab aponta para um osso de verdade, para nunca ficar nulo;
//  2) o asset KRig (Resources/FBFpsaf/Rig_TPS) com a hierarquia do personagem de referência e as cadeias que as camadas
//     procuram pelo NOME — índice fica -1 de propósito: nossos personagens não têm todos o mesmo número de ossos
//     (r_elaine tem 34, r_pm_almeida 52) e índice fixo pegaria o osso errado, calado, nos que têm menos dedo;
//  3) a configuração de entrada (Resources/FBFpsaf/Entrada_TPS), copiada da que vem no pacote;
//  4) o perfil (Resources/FBFpsaf/Perfil_TPS) com a pilha de camadas de terceira pessoa.
//
// Lote: Unity -batchmode -executeMethod FreeBala.EditorTools.FBFpsafSetup.Batch [-fbfpsafchars a,b,c]
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using KINEMATION.KAnimationCore.Runtime.Rig;
using KINEMATION.KAnimationCore.Runtime.Input;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.SwayLayer;

namespace FreeBala.EditorTools
{
    public static class FBFpsafSetup
    {
        const string Dir = "Assets/FreeBala/Resources/FBFpsaf";
        const string RigPath = Dir + "/Rig_TPS.asset", PerfilPath = Dir + "/Perfil_TPS.asset", EntradaPath = Dir + "/Entrada_TPS.asset";
        const string PacoteEntrada = "Assets/KINEMATION/FPSAnimationFramework/Assets/InputConfig_FPSAnimationFramework.asset";

        // nomes dos ossos virtuais: os mesmos que as camadas do pacote procuram (FPSANames)
        public static readonly (string nome, HumanBodyBones alvo)[] Virtuais =
        {
            ("IK LeftHand", HumanBodyBones.LeftHand), ("IK LeftElbow", HumanBodyBones.LeftLowerArm),
            ("IK RightHand", HumanBodyBones.RightHand), ("IK RightElbow", HumanBodyBones.RightLowerArm),
            ("WeaponBone", HumanBodyBones.RightHand),
            // "IK WeaponBone" (com espaço) é o nome que as camadas de ARMA procuram (FPSANames.IkWeaponBone) — é nele que
            // balanço e coice escrevem. O FBFpsaf aponta para a arma e, depois das camadas, copia o resultado de volta nela.
            ("IK WeaponBone", HumanBodyBones.RightHand),
        };

        public static void Batch()
        {
            int code = 0;
            try { Tudo(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r57] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("FreeBala/Rig novo (FPSAF): preparar personagens e assets", priority = 12)]
        public static void Tudo()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Directory.CreateDirectory(Dir);
            int n = Personagens(out var referencia, out var ruins);
            if (!referencia) throw new System.Exception("nenhum personagem preparado: sem referência para o KRig");
            var rig = Rig(referencia);
            var entrada = Entrada();
            Perfil(rig);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"[FreeBala][r57] rig novo: {n} personagens preparados{(ruins.Count > 0 ? " · sem Animator humanoide: " + string.Join(",", ruins) : "")}" +
                      $" · KRig {rig.rigHierarchy.Count} ossos / {rig.rigElementChains.Count} cadeias · entrada {(entrada ? "ok" : "FALTOU")} · {sw.Elapsed.TotalSeconds:0} s");
        }

        // ---- 1) prefabs
        static int Personagens(out GameObject referencia, out List<string> ruinsOut)
        {
            referencia = null; var ruins = new List<string>(); ruinsOut = ruins;
            var args = System.Environment.GetCommandLineArgs(); int ia = System.Array.IndexOf(args, "-fbfpsafchars");
            var filtro = ia >= 0 && ia + 1 < args.Length ? new HashSet<string>(args[ia + 1].Split(',')) : null;
            var paths = Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab").Select(p => p.Replace('\\', '/')).ToList();
            int n = 0;
            foreach (var p in paths)
            {
                var id = Path.GetFileNameWithoutExtension(p);
                if (filtro != null && !filtro.Contains(id)) continue;
                var raiz = PrefabUtility.LoadPrefabContents(p);
                try
                {
                    var an = raiz.GetComponentInChildren<Animator>();
                    if (!an || !an.isHuman) { ruins.Add(id); continue; }
                    Preparar(an);
                    PrefabUtility.SaveAsPrefabAsset(raiz, p);
                    n++;
                    if (!referencia && id == "r_tutu") { referencia = AssetDatabase.LoadAssetAtPath<GameObject>(p); }
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
            }
            referencia ??= AssetDatabase.LoadAssetAtPath<GameObject>(paths.FirstOrDefault(x => !ruins.Contains(Path.GetFileNameWithoutExtension(x))));
            return n;
        }

        /// <summary>KRigComponent + ossos virtuais num personagem já aberto (prefab ou cena)</summary>
        public static void Preparar(Animator an)
        {
            var raiz = an.transform;
            foreach (var (nome, alvo) in Virtuais)
            {
                var t = raiz.Find(nome);
                if (!t) { var go = new GameObject(nome); go.transform.SetParent(raiz, false); t = go.transform; }
                var ve = t.GetComponent<KVirtualElement>() ?? t.gameObject.AddComponent<KVirtualElement>();
                ve.targetBone = an.GetBoneTransform(alvo) ?? raiz;      // trocado em tempo de jogo pelo FBFpsaf quando a arma muda
                t.SetPositionAndRotation(ve.targetBone.position, ve.targetBone.rotation);
            }
            var rc = an.GetComponent<KRigComponent>() ?? an.gameObject.AddComponent<KRigComponent>();
            rc.RefreshHierarchy();
            EditorUtility.SetDirty(an.gameObject);
        }

        // ---- 2) KRig
        static KRig Rig(GameObject referencia)
        {
            var rig = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            if (!rig) { rig = ScriptableObject.CreateInstance<KRig>(); AssetDatabase.CreateAsset(rig, RigPath); }
            var an = referencia.GetComponentInChildren<Animator>();
            var rc = an.GetComponent<KRigComponent>();

            rig.rigHierarchy.Clear(); rig.rigElementChains.Clear();
            var virtuais = new HashSet<string>(Virtuais.Select(v => v.nome));
            foreach (var t in rc.GetHierarchy())
                rig.rigHierarchy.Add(new KRigElement(-1, t.name, virtuais.Contains(t.name)));   // índice -1: resolve por NOME (ver cabeçalho)

            string B(HumanBodyBones b) { var t = an.GetBoneTransform(b); return t ? t.name : null; }
            void Cadeia(string nome, params string[] ossos)
            {
                var e = ossos.Where(o => !string.IsNullOrEmpty(o)).Select(o => new KRigElement(-1, o, virtuais.Contains(o))).ToList();
                if (e.Count > 0) rig.rigElementChains.Add(new KRigElementChain { chainName = nome, elementChain = e });
            }
            Cadeia("Spine", B(HumanBodyBones.Spine), B(HumanBodyBones.Chest), B(HumanBodyBones.UpperChest));
            Cadeia("Head", B(HumanBodyBones.Neck), B(HumanBodyBones.Head));
            Cadeia("LeftArm", B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand));
            Cadeia("RightArm", B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand));
            Cadeia("WeaponBone", "WeaponBone");
            Cadeia("IkHands", "IK LeftHand", "IK RightHand");

            EditorUtility.SetDirty(rig);
            return rig;
        }

        // ---- 3) entrada (copiada do pacote: já tem IsAiming, AimingWeight, MoveInput, MouseInput...)
        static UserInputConfig Entrada()
        {
            var e = AssetDatabase.LoadAssetAtPath<UserInputConfig>(EntradaPath);
            if (e) return e;
            if (!AssetDatabase.CopyAsset(PacoteEntrada, EntradaPath)) { Debug.LogError("[FreeBala][r57] não copiou " + PacoteEntrada); return null; }
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<UserInputConfig>(EntradaPath);
        }

        // ---- 4) perfil: por enquanto só a camada de IK das mãos (a que o FBRig NÃO faz bem com a arma trocando)
        static void Perfil(KRig rig)
        {
            var perfil = AssetDatabase.LoadAssetAtPath<FPSAnimatorProfile>(PerfilPath);
            if (!perfil) { perfil = ScriptableObject.CreateInstance<FPSAnimatorProfile>(); AssetDatabase.CreateAsset(perfil, PerfilPath); }
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(PerfilPath).OfType<FPSAnimatorLayerSettings>().ToList())
                AssetDatabase.RemoveObjectFromAsset(s);
            perfil.settings.Clear();
            perfil.rigAsset = rig; perfil.blendInTime = .15f; perfil.blendOutTime = .15f;

            var an = AssetDatabase.LoadAssetAtPath<GameObject>(FBCharImport.PrefabDir + "/r_tutu.prefab")?.GetComponentInChildren<Animator>();
            string N(HumanBodyBones b) => an ? an.GetBoneTransform(b)?.name : null;

            // ---- balanço da arma: o peso dela ao virar a câmera e ao andar. Entra ANTES do IK das mãos, porque o IK
            // tem de resolver o braço depois que a arma já se mexeu. Escalas conservadoras: ~2° e ~1 cm no giro rápido.
            var sway = ScriptableObject.CreateInstance<SwayLayerSettings>();
            sway.name = "Balanco_Arma"; sway.rigAsset = rig; sway.alpha = 1f; sway.isStandalone = false;
            sway.weaponIkBone = new KRigElement(-1, "IK WeaponBone", true);
            sway.rightHandElbow = new KRigElement(-1, "IK RightElbow", true);
            sway.leftHandElbow = new KRigElement(-1, "IK LeftElbow", true);
            sway.headBone = new KRigElement(-1, N(HumanBodyBones.Head) ?? "mixamorig:Head");
            sway.hintTargetWeight = 0f;                      // cotovelos continuam com o jogo; só a arma balança
            sway.freeAimClamp = 0f; sway.freeAimInterpSpeed = 0f; sway.freeAimInputScale = 0f;
            VectorSpring Mola(float speed, float stiff, float damp, Vector3 escala) => new VectorSpring
            { speed = Vector3.one * speed, stiffness = Vector3.one * stiff, damping = Vector3.one * damp, scale = escala };
            // Escalas medidas, não chutadas: com 1,2° de escala o medidor (-fbrigdbg) acusou 0,00 cm e 0,3° de efeito,
            // ou seja, nada. A mola do pacote (KSpringMath) converge devagar: precisa de escala ~10x maior para dar um
            // atraso de arma visível (meta: 3 a 5° e 2 a 3 cm no giro rápido).
            sway.aimSwayPositionSpring = Mola(16, 1f, 1f, new Vector3(.06f, .06f, 0));
            sway.aimSwayRotationSpring = Mola(16, 1f, 1f, new Vector3(12f, 12f, 6f));
            sway.aimSwayTargetDamping = 4f;
            sway.moveSwayPositionSpring = Mola(12, .8f, 1f, new Vector3(.04f, .04f, .02f));
            sway.moveSwayRotationSpring = Mola(12, .8f, 1f, new Vector3(8f, 8f, 4f));
            sway.moveSwayTargetDamping = 3f;
            AssetDatabase.AddObjectToAsset(sway, perfil);
            perfil.settings.Add(sway);

            var ik = ScriptableObject.CreateInstance<IkLayerSettings>();
            ik.name = "Ik_Maos"; ik.rigAsset = rig; ik.alpha = 1f;
            ik.isStandalone = false;   // trava o OnValidate: ele chamaria OnRigUpdated e gravaria ÍNDICE de osso, que é o que
                                       // não pode (personagens com menos dedo pegariam o osso errado). Fica tudo por nome.
            ik.leftHand = new KRigElement(-1, N(HumanBodyBones.LeftHand) ?? "mixamorig:LeftHand");
            ik.rightHand = new KRigElement(-1, N(HumanBodyBones.RightHand) ?? "mixamorig:RightHand");
            ik.leftHandIk = new KRigElement(-1, "IK LeftHand", true);
            ik.rightHandIk = new KRigElement(-1, "IK RightHand", true);
            ik.leftHandHint = new KRigElement(-1, "IK LeftElbow", true);
            ik.rightHandHint = new KRigElement(-1, "IK RightElbow", true);
            // Pés: a camada resolve TODOS os elementos no InitializeState e o KRigComponent estoura se o nome não existir
            // (o padrão é "None"). Alvo = o próprio pé, então o IK já chega resolvido e não muda nada — quem cuida dos pés
            // continua sendo o FBFootIK.
            ik.leftFoot = ik.leftFootIk = new KRigElement(-1, N(HumanBodyBones.LeftFoot) ?? "mixamorig:LeftFoot");
            ik.rightFoot = ik.rightFootIk = new KRigElement(-1, N(HumanBodyBones.RightFoot) ?? "mixamorig:RightFoot");
            ik.leftFootHint = new KRigElement(-1, N(HumanBodyBones.LeftLowerLeg) ?? "mixamorig:LeftLeg");
            ik.rightFootHint = new KRigElement(-1, N(HumanBodyBones.RightLowerLeg) ?? "mixamorig:RightLeg");
            AssetDatabase.AddObjectToAsset(ik, perfil);
            perfil.settings.Add(ik);

            EditorUtility.SetDirty(perfil);
        }
    }
}
