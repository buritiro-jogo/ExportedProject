// Shaders criados em tempo de execução (materiais de FX, LEDs, zona, props). Shader.Find só acha no build o que foi incluído:
// o FBProjectSetup grava Resources/FBShaderRefs.asset com referências diretas (o build leva os shaders e variantes).
// Transparente no URP precisa das palavras-chave, não só de _Surface — Transparent() faz o que o inspetor do URP faz.
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public static class FBShaders
    {
        static FBShaderRefs refs; static FBShaderRefs R => refs ? refs : refs = Resources.Load<FBShaderRefs>("FBShaderRefs");
        static Shader Pick(Shader s, string name, string fb = null) => s ? s : Shader.Find(name) ?? (fb != null ? Shader.Find(fb) : null);
        public static Shader Lit => Pick(R ? R.lit : null, "Universal Render Pipeline/Lit", "Standard");
        public static Shader Unlit => Pick(R ? R.unlit : null, "Universal Render Pipeline/Unlit", "Unlit/Color");
        public static Shader ParticlesUnlit => Pick(R ? R.particlesUnlit : null, "Universal Render Pipeline/Particles/Unlit", "Sprites/Default");
        public static Shader Sky => Pick(R ? R.sky : null, "FreeBala/Sky");
        public static Shader Foliage => Pick(R ? R.foliage : null, "FreeBala/Foliage", "Universal Render Pipeline/Lit");
        public static Shader HeightFog => Pick(R ? R.heightFog : null, "FreeBala/HeightFog");
        public static Shader Contrail => Pick(R ? R.contrail : null, "FreeBala/Contrail");
        public static Shader Sprites => Pick(R ? R.sprites : null, "Sprites/Default");
        // material transparente SEMPRE a partir do material-modelo (Resources/FBMats/<Lit|Unlit|Particles>_<Alpha|Add|AlphaClip>): é ele que garante
        // a variante de shader no build. Criar um material do zero e ligar palavras-chave em tempo de execução funcionava no editor mas no
        // build caía na variante opaca (grafite/nuvens/fumaça com fundo colorido). As propriedades (textura, cor, cull…) vêm do material passado.
        static readonly string[] RS = { "_Surface", "_Blend", "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha", "_ZWrite", "_AlphaClip", "_BlendOp", "_AlphaToMask" };
        public static Material Transparent(Material m, bool additive = false, bool clip = false)
        {
            var sh = m.shader; string fam = sh == ParticlesUnlit ? "Particles" : sh == Unlit ? "Unlit" : sh == Lit ? "Lit" : null;
            var tpl = fam != null ? Resources.Load<Material>($"FBMats/{fam}_{(additive ? "Add" : clip ? "AlphaClip" : "Alpha")}") : null;
            if (tpl)
            {
                var t = new Material(tpl) { name = m.name }; t.CopyMatchingPropertiesFromMaterial(m);
                // a cópia traz o estado de mistura do material CRU (opaco: One/Zero, ZWrite 1, _Surface 0) — devolve o do modelo,
                // senão nuvens/grafites/PNGs saem sólidos com fundo
                foreach (var k in RS) if (tpl.HasProperty(k)) t.SetFloat(k, tpl.GetFloat(k));
                t.shaderKeywords = tpl.shaderKeywords; t.renderQueue = tpl.renderQueue;               // palavras-chave/fila do modelo (não as do material cru)
                t.SetFloat("_Surface", 1); return t;
            }
            return Legacy(m, additive);
        }
        /// <summary>Torna o PRÓPRIO material transparente (materiais salvos como asset no editor: mapas, props). Transparent() devolve
        /// um material NOVO — quem ignora o retorno fica com o original opaco (vidro/água/PNG dos mapas saíam sólidos).</summary>
        public static Material TransparentInPlace(Material m, bool additive = false) => Legacy(m, additive);
        static Material Legacy(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0); m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }
    }
}
