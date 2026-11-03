// r51 — mira óptica no estilo do MW (pedido: "transição suave da terceira para a primeira pessoa" com mira holográfica/ponto vermelho/
// lunetas; lunetas 2x/4x sem "tela praticamente toda preta": imagem ampliada SÓ dentro da lente, entorno visível, corpo da arma e da
// luneta aparecendo, retículo no ponto de impacto). Sem mira óptica (mira de ferro) continua a mira em 3ª pessoa.
//
// Lente com câmera (PiP): uma câmera secundária renderiza numa RenderTexture com o FOV da ampliação e a imagem entra num disco no plano da
// lente da própria luneta. Tabela das lentes medida nos GLBs (tools: glb_scopes/lens_table): as miras do LPSP são as MESMAS malhas em
// todas as armas (51–53 armas, 1 variante), coordenadas locais da malha (glTF: −Y = cano, +Z = cima, olho do lado +Y).
// Mapeamento da imagem: o CENTRO da RenderTexture (eixo da câmera = direção do tiro) cai no CENTRO do disco, esteja o disco onde estiver
// na tela — com o balanço/coice da arma a imagem anda junto com a luneta e o retículo (no centro da lente) continua marcando o impacto.
// FOV da câmera da lente: a borda da lente fica a p = tan(θL)/tan(FOVarma/2) da meia-altura da tela (a arma é desenhada pela câmera de
// 1ª pessoa, FOV próprio); para ampliar z vezes em relação à visão normal (FOV base): tan(φ/2) = p·tan(FOVbase/2)/z.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBOptic
    {
        public static bool On = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbmiraantiga") < 0;   // -fbmiraantiga = luneta 2D preta da r45
        public static bool PipOn = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbsempip") < 0;      // -fbsempip = lente escura (aparelho fraco)
        public const float InTime = .18f, OutTime = .14f;     // entrar/sair da mira óptica (s)

        public class Lens { public string mesh; public float y, z, r; public int[] hide; public bool reticle; }
        // y = plano onde a imagem entra · z = altura do eixo óptico · r = raio útil · hide = submalhas da lente opaca do modelo (somem) ·
        // reticle = o modelo não tem retículo (desenha o procedural)
        static readonly Lens[] T =
        {
            new Lens { mesh = "SM_ATT_Scope_04", y = -.0200f, z = .0415f, r = .0135f },                    // 2x prismático: retículos k2–k4 (y −0,019…−0,007) ficam na frente da imagem
            new Lens { mesh = "SM_ATT_Scope_05", y = .0333f, z = .0397f, r = .0165f, hide = new[] { 4 } },   // 4x: k4 = lente branca opaca ("Render Camera" do LPSP); retículo k3 (y .0344) fica
            new Lens { mesh = "SM_ATT_Scope_06", y = .0338f, z = .0401f, r = .0196f, hide = new[] { 4 } },   // 6x
            new Lens { mesh = "SM_ATT_Scope_07", y = .0345f, z = .0397f, r = .0165f, hide = new[] { 3, 4 } },// 4x: duas lentes opacas, retículo k2 (y .0357)
            new Lens { mesh = "SM_ATT_Scope_08", y = .0338f, z = .0397f, r = .0174f, hide = new[] { 4 } },   // 1,3x com lente de câmera
            // r51g: saiu a "luneta de fábrica" da Barret (SM_Sniper_02_Scope_Default). Ela é ALÇA E MASSA DE MIRA, não luneta: 1622 vértices em dois
            // grupos — massa em y −0,72…−0,63 (±6 mm) e alça com fenda em y −0,085…−0,028 (±14 mm, z até 0,141), nada no meio. O jogo original
            // mostrava uma luneta 2D por cima; aqui a lente PiP era desenhada atrás da alça (foto do exe: bloco preto com fenda tampando a lente).
            // Fora da tabela, FBWeaponModel.Default dá a ela a 6x do próprio modelo (P_LPSP_WEP_ATT_Scope_06), como já fazia com a sniper_hunting
        };
        public static Lens Find(string meshName) { foreach (var l in T) if (l.mesh == meshName) return l; return null; }

        // arma com mira óptica = luneta/holográfico/ponto vermelho escolhido no menu, ou a luneta de fábrica da sniper (Barret)
        public static bool Has(WeaponDef w)
        {
            if (!On || w == null) return false;
            if (FBWeaponModel.Of(w).scope >= 0) return true;
            return w.IsSniper && Find(Json.S(Json.O(Json.O(GameData.WeaponsRaw, w.id), "att"), "scopeDefault")) != null;
        }
        // aumento da visão normal (fora da lente) mirando: ponto vermelho/holográfico = o próprio aumento; luneta ≥ 2x = leve (o aumento
        // de verdade fica dentro da lente — entorno visível, como no MW)
        public static float MainZoom(float z) => z < 2 ? z : 1.2f + .05f * z;

        // ---------------------------------------------------------------- malhas e materiais
        static Mesh disc; static Material hidden; static Texture2D vigTex, retTex, impTex; static Sprite impSpr;
        public static Mesh Disc()
        {
            if (disc) return disc; const int N = 64; var v = new Vector3[N + 1]; var uv = new Vector2[N + 1]; var t = new int[N * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(.5f, .5f);
            for (int i = 0; i < N; i++) { float a = i * Mathf.PI * 2 / N; var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); v[i + 1] = p; uv[i + 1] = new Vector2(.5f, .5f) + p * .5f; t[i * 3] = 0; t[i * 3 + 1] = (i + 1) % N + 1; t[i * 3 + 2] = i + 1; }
            disc = new Mesh { name = "lente_disco", vertices = v, uv = uv, triangles = t }; disc.RecalculateBounds(); disc.bounds = new Bounds(Vector3.zero, Vector3.one * 2); return disc;
        }
        public static Material Hidden()
        {
            if (hidden) return hidden; var m = new Material(FBShaders.Unlit) { name = "lente_oculta" }; m.SetColor("_BaseColor", new Color(0, 0, 0, 0));
            hidden = FBShaders.Transparent(m); hidden.SetColor("_BaseColor", new Color(0, 0, 0, 0)); return hidden;
        }
        public static Material Overlay(Texture2D tex, int queue)
        {
            var m = new Material(FBShaders.Unlit) { name = "lente_" + tex.name }; m.SetTexture("_BaseMap", tex); m.mainTexture = tex; m.SetColor("_BaseColor", Color.white);
            var t = FBShaders.Transparent(m); t.SetTexture("_BaseMap", tex); t.mainTexture = tex; if (t.HasProperty("_Cull")) t.SetFloat("_Cull", 0); t.renderQueue = queue; return t;
        }
        // sombra da ocular: miolo limpo com leve tom de vidro, borda escurecendo (anel do tubo) — deslocada quando o olho sai do eixo
        public static Texture2D Vignette()
        {
            if (vigTex) return vigTex; const int S = 256; vigTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "sombra_ocular", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float u = (x + .5f) / S * 2 - 1, w = (y + .5f) / S * 2 - 1, r = Mathf.Sqrt(u * u + w * w);
                    float a = Mathf.Max(.05f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.80f, 1.0f, r))); if (r >= .985f) a = 1;
                    px[y * S + x] = new Color32(4, 6, 8, (byte)(a * 255));
                }
            vigTex.SetPixels32(px); vigTex.Apply(false, true); return vigTex;
        }
        // r52 INDICADOR DE IMPACTO: X pequeno vermelho com borda branca (lê no céu claro e na sombra). Braços até 0,68 do raio: cabe no disco
        // da lente (FBOptic.Disc, UV = círculo inscrito) sem cortar as pontas; mesma textura no HUD (sprite)
        public static Texture2D ImpactX()
        {
            if (impTex) return impTex; const int S = 64; impTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "indicador_impacto", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float u = (x + .5f) / S * 2 - 1, w = (y + .5f) / S * 2 - 1;
                    float along = Mathf.Max(Mathf.Abs(u), Mathf.Abs(w)), d = Mathf.Min(Mathf.Abs(u - w), Mathf.Abs(u + w)) * .7071f;   // distância ao traço diagonal mais perto
                    float cut = Mathf.Clamp01((.68f - along) * S * .5f);                                     // ponta do braço (suave)
                    float edge = Mathf.Clamp01((.155f - d) * S * .5f) * cut, core = Mathf.Clamp01((.075f - d) * S * .5f) * Mathf.Clamp01((.6f - along) * S * .5f);
                    var c = Color.Lerp(Color.white, new Color(1, .13f, .1f), core); c.a = Mathf.Max(edge, core);
                    px[y * S + x] = c;
                }
            impTex.SetPixels32(px); impTex.Apply(true, true); return impTex;
        }
        public static Sprite ImpactSprite => impSpr ? impSpr : impSpr = Sprite.Create(ImpactX(), new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        // retículo de luneta de precisão (Barret: o modelo não tem): cruz fina com postes grossos nas pontas, marcas de milésimo e ponto central
        public static Texture2D SniperReticle()
        {
            if (retTex) return retTex; const int S = 512; retTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "reticulo_precisao", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S]; float c = (S - 1) * .5f;
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Abs(x - c), dy = Mathf.Abs(y - c), r = Mathf.Sqrt(dx * dx + dy * dy); float a = 0;
                    float thin = 1.1f, thick = 4.5f, post = S * .30f;                                      // a cruz fina vai até 30 % do raio; depois o poste grosso
                    if (dx <= thin && dy > 6) a = dy < post ? 1 : 0; if (dy <= thin && dx > 6) a = dx < post ? 1 : 0;
                    if (dx <= thick && dy >= post) a = 1; if (dy <= thick && dx >= post) a = 1;
                    for (int k = 1; k <= 4; k++) { float m = k * S * .062f; if (dx <= 2.6f && Mathf.Abs(dy - m) <= 2.6f && Mathf.Abs(dy - m) * Mathf.Abs(dy - m) + dx * dx <= 7) a = 1; if (dy <= 2.6f && Mathf.Abs(dx - m) <= 2.6f && Mathf.Abs(dx - m) * Mathf.Abs(dx - m) + dy * dy <= 7) a = 1; }
                    var col = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 235));
                    if (r <= 2.4f) col = new Color32(255, 40, 30, 255);                                    // ponto de impacto aceso
                    px[y * S + x] = col;
                }
            retTex.SetPixels32(px); retTex.Apply(true, true); return retTex;
        }
    }
}
