// r49: clipe de DEITAR (pedido de 30/10: "corrigir as animações de deitar, permanecer deitado e levantar; transições respeitando a duração").
// O jogo tinha só a pose deitada (FF_SYS_Prone) e o rastejar: deitar/levantar eram a camada de corpo inteiro subindo/descendo em 0,1–0,3 s
// (corpo "teletransportado" para o chão). O levantar do caído do FF (FF_SYS_KO_GetUp, 1,2 s) começa exatamente na pose deitada de bruços
// do FF_SYS_Prone (corpo a 0,12 × 0,10–0,11, RootQ.x 0,64 × 0,67) e termina em pé (0,89), sem andar (RootT.z ±0,13): é o levantar. O deitar é
// o mesmo clipe ao contrário (FF_SYS_Prone_Down), gerado aqui — chave a chave, tempo t → duração − t e tangentes trocadas/negadas.
// Chamado pelo FBBuild antes da Dev Build (idempotente: refaz só se faltar ou se a fonte mudou) e põe o clipe no registro.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBProneClips
    {
        const string Dir = "Assets/FreeBala/Anim/Clips", Src = Dir + "/FF_SYS_KO_GetUp.anim", Dst = Dir + "/FF_SYS_Prone_Down.anim";
        [MenuItem("FreeBala/Animação/Gerar clipe de deitar (r49)")]
        public static void Ensure()
        {
            var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(Src); if (!src) { Debug.LogWarning("[FreeBala][prone] sem " + Src); return; }
            bool fresh = File.Exists(Dst) && File.GetLastWriteTimeUtc(Dst) >= File.GetLastWriteTimeUtc(Src);
            var dst = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dst);
            if (!fresh || !dst)
            {
                float len = src.length; var clip = new AnimationClip { name = "FF_SYS_Prone_Down", frameRate = src.frameRate };
                foreach (var b in AnimationUtility.GetCurveBindings(src))
                {
                    var c = AnimationUtility.GetEditorCurve(src, b); if (c == null) continue;
                    var keys = c.keys.Select(k => new Keyframe(len - k.time, k.value, -k.outTangent, -k.inTangent, k.outWeight, k.inWeight) { weightedMode = Swap(k.weightedMode) }).OrderBy(k => k.time).ToArray();
                    AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(keys));
                }
                var st = AnimationUtility.GetAnimationClipSettings(src); st.loopTime = false; st.loopBlend = false; AnimationUtility.SetAnimationClipSettings(clip, st);
                if (dst) { EditorUtility.CopySerialized(clip, dst); EditorUtility.SetDirty(dst); } else { AssetDatabase.CreateAsset(clip, Dst); dst = clip; }
                AssetDatabase.SaveAssets(); Debug.Log($"[FreeBala][prone] {Dst} gerado ({len:0.00} s, {AnimationUtility.GetCurveBindings(dst).Length} curvas)");
            }
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            if (reg && !reg.clips.Contains(dst)) { reg.clips.Add(dst); EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets(); Debug.Log("[FreeBala][prone] FF_SYS_Prone_Down no registro"); }
        }
        static WeightedMode Swap(WeightedMode m) => m == WeightedMode.In ? WeightedMode.Out : m == WeightedMode.Out ? WeightedMode.In : m;
    }
}
