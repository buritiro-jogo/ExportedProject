// GLB -> glTF + .bin + imagens soltas. O glTFast importa as imagens EMBUTIDAS num .glb como texturas RGBA32 sem compressão (o build de
// Windows deu 8,5 GB: 6,1 GB só de textura). Com as imagens como arquivos do projeto, o glTFast usa os Texture2D do AssetDatabase, que passam
// pelo TextureImporter: compressão por plataforma (BC/DXT no PC, ASTC no Android/iOS, DXT+ASTC no WebGL), tamanho máximo e mipmaps.
// O uso de cada imagem sai dos materiais: normalTexture -> normal map; metallicRoughness/occlusion -> linear; o resto -> cor (sRGB).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using FreeBala;

namespace FreeBala.EditorTools
{
    public static class FBGlbUnpack
    {
        public const string TexSuffix = "_tex";
        // true = escreveu (mudou); destino: <dir>/<nome>.gltf + <nome>.bin + <nome>_tex/<i>_<uso>.<ext>
        public static bool Unpack(string glb, string gltfPath)
        {
            var stamp = Path.Combine("Library/FBUnpack", gltfPath.Replace('/', '_').Replace(Path.DirectorySeparatorChar, '_') + ".src"); Directory.CreateDirectory("Library/FBUnpack"); var sig = new FileInfo(glb).Length + "|" + File.GetLastWriteTimeUtc(glb).Ticks;
            if (File.Exists(gltfPath) && File.Exists(stamp) && File.ReadAllText(stamp) == sig) return false;
            var b = File.ReadAllBytes(glb); if (b.Length < 20 || BitConverter.ToUInt32(b, 0) != 0x46546C67) throw new Exception("não é GLB: " + glb);
            int p = 12; byte[] json = null, bin = null;
            while (p + 8 <= b.Length) { int len = BitConverter.ToInt32(b, p), type = BitConverter.ToInt32(b, p + 4); var chunk = new byte[len]; Buffer.BlockCopy(b, p + 8, chunk, 0, len); if (type == 0x4E4F534A) json = chunk; else if (type == 0x004E4942) bin = chunk; p += 8 + len; }
            var g = Json.Parse(Encoding.UTF8.GetString(json).TrimEnd('\0', ' ')) as Dictionary<string, object>;
            var dir = Path.GetDirectoryName(gltfPath); var name = Path.GetFileNameWithoutExtension(gltfPath); var texDir = Path.Combine(dir, name + TexSuffix);
            Directory.CreateDirectory(dir); if (Directory.Exists(texDir)) foreach (var f in Directory.GetFiles(texDir)) if (!f.EndsWith(".meta")) File.Delete(f); Directory.CreateDirectory(texDir);
            // uso das imagens pelos materiais
            var use = new Dictionary<int, string>(); var textures = Json.L(g, "textures") ?? new List<object>();
            int Src(object texInfo) { if (texInfo == null) return -1; int ti = Json.I(texInfo, "index", -1); if (ti < 0 || ti >= textures.Count) return -1; var t = textures[ti]; int s = Json.I(t, "source", -1); if (s < 0) { var ex = Json.O(t, "extensions"); if (ex != null) foreach (var kv in ex) { s = Json.I(kv.Value, "source", -1); if (s >= 0) break; } } return s; }
            void Mark(object ti, string u) { int s = Src(ti); if (s < 0) return; if (!use.TryGetValue(s, out var o) || o == "c") use[s] = u; }
            foreach (var m in Json.L(g, "materials") ?? new List<object>())
            {
                Mark(Json.O(m, "normalTexture"), "n"); Mark(Json.O(m, "occlusionTexture"), "l"); var pbr = Json.O(m, "pbrMetallicRoughness"); Mark(Json.O(pbr, "metallicRoughnessTexture"), "l");
                Mark(Json.O(pbr, "baseColorTexture"), "c"); Mark(Json.O(m, "emissiveTexture"), "c");
                var ex = Json.O(m, "extensions"); var cc = Json.O(ex, "KHR_materials_clearcoat"); if (cc != null) { Mark(Json.O(cc, "clearcoatNormalTexture"), "n"); Mark(Json.O(cc, "clearcoatRoughnessTexture"), "l"); Mark(Json.O(cc, "clearcoatTexture"), "l"); }
                var sp = Json.O(ex, "KHR_materials_specular"); if (sp != null) { Mark(Json.O(sp, "specularTexture"), "l"); Mark(Json.O(sp, "specularColorTexture"), "c"); }
            }
            var views = Json.L(g, "bufferViews") ?? new List<object>(); var images = Json.L(g, "images") ?? new List<object>();
            for (int i = 0; i < images.Count; i++)
            {
                var im = images[i] as Dictionary<string, object>; if (im == null || !im.ContainsKey("bufferView")) continue;
                var bv = views[Json.I(im, "bufferView")] as Dictionary<string, object>; int off = Json.I(bv, "byteOffset"), len = Json.I(bv, "byteLength");
                var mime = Json.S(im, "mimeType") ?? "image/png"; var ext = mime.Contains("jpeg") ? ".jpg" : mime.Contains("webp") ? ".webp" : mime.Contains("ktx2") ? ".ktx2" : ".png";
                var file = $"{i}_{(use.TryGetValue(i, out var u) ? u : "c")}{ext}"; var data = new byte[len]; Buffer.BlockCopy(bin, off, data, 0, len); File.WriteAllBytes(Path.Combine(texDir, file), data);
                im.Remove("bufferView"); im.Remove("mimeType"); im["uri"] = name + TexSuffix + "/" + file;
            }
            var bufs = Json.L(g, "buffers"); if (bufs != null && bufs.Count > 0) (bufs[0] as Dictionary<string, object>)["uri"] = name + ".bin";
            File.WriteAllBytes(Path.Combine(dir, name + ".bin"), bin ?? new byte[0]);
            var sb = new StringBuilder(json.Length + 1024); Write(sb, g); File.WriteAllText(gltfPath, sb.ToString(), new UTF8Encoding(false));
            File.WriteAllText(stamp, sig); return true;
        }
        static void Write(StringBuilder sb, object o)
        {
            switch (o)
            {
                case null: sb.Append("null"); break;
                case string s:
                    sb.Append('"'); foreach (var ch in s) { switch (ch) { case '"': sb.Append("\\\""); break; case '\\': sb.Append("\\\\"); break; case '\n': sb.Append("\\n"); break; case '\r': sb.Append("\\r"); break; case '\t': sb.Append("\\t"); break; default: if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4")); else sb.Append(ch); break; } }
                    sb.Append('"'); break;
                case bool bo: sb.Append(bo ? "true" : "false"); break;
                case double d: sb.Append(d == Math.Floor(d) && Math.Abs(d) < 1e15 ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString("R", CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case List<object> list: sb.Append('['); for (int i = 0; i < list.Count; i++) { if (i > 0) sb.Append(','); Write(sb, list[i]); } sb.Append(']'); break;
                case Dictionary<string, object> dict: { sb.Append('{'); bool first = true; foreach (var kv in dict) { if (!first) sb.Append(','); first = false; Write(sb, kv.Key); sb.Append(':'); Write(sb, kv.Value); } sb.Append('}'); break; }
                default: Write(sb, Convert.ToString(o, CultureInfo.InvariantCulture)); break;
            }
        }
    }

    // configurações das texturas soltas dos glTF (Imported/**/<nome>_tex/<i>_<uso>.<ext>)
    public class FBGltfTexturePost : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (p.Contains("/Resources/UI/"))                                                   // interface (lobby): sem mipmap, compressão de alta qualidade
            { var ui = (TextureImporter)assetImporter; ui.mipmapEnabled = false; ui.alphaIsTransparency = true; ui.wrapMode = TextureWrapMode.Clamp; ui.textureCompression = TextureImporterCompression.CompressedHQ; ui.npotScale = TextureImporterNPOTScale.None; return; }
            if (p.EndsWith("Resources/Graffiti/atlas.png"))                                   // atlas dos grafites: grande, sem perder os traços finos
            {
                var ga = (TextureImporter)assetImporter; ga.maxTextureSize = 4096; ga.alphaIsTransparency = true; ga.mipmapEnabled = true; ga.textureCompression = TextureImporterCompression.CompressedHQ;
                foreach (var plat in new[] { "Android", "iPhone", "WebGL" }) ga.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = plat, overridden = true, maxTextureSize = 2048, format = plat == "WebGL" ? TextureImporterFormat.Automatic : TextureImporterFormat.ASTC_6x6, textureCompression = TextureImporterCompression.Compressed });
                return;
            }
            if (!p.StartsWith(FBImport.Imp + "/") || !p.Contains(FBGlbUnpack.TexSuffix + "/")) return;
            var ti = (TextureImporter)assetImporter; var file = Path.GetFileNameWithoutExtension(p); char u = file.Length > 2 ? file[file.LastIndexOf('_') + 1] : 'c';
            bool map = p.Contains("/Maps/"), weapon = p.Contains("/Weapons/") || p.Contains("/FP/"), ch = p.Contains("/Chars/");
            ti.textureShape = TextureImporterShape.Texture2D; ti.mipmapEnabled = true; ti.isReadable = false; ti.streamingMipmaps = true;
            ti.textureType = u == 'n' ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = u == 'c';
            ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = map ? 4 : 2;
            int pc = weapon ? 2048 : map ? 2048 : ch ? 2048 : 1024, mob = weapon ? 1024 : map ? 1024 : 512;
            ti.maxTextureSize = pc; ti.textureCompression = TextureImporterCompression.Compressed; ti.crunchedCompression = false;
            foreach (var plat in new[] { "Android", "iPhone" })
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = plat, overridden = true, maxTextureSize = mob, format = TextureImporterFormat.ASTC_6x6, textureCompression = TextureImporterCompression.Compressed });
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "WebGL", overridden = true, maxTextureSize = mob, format = TextureImporterFormat.Automatic, textureCompression = TextureImporterCompression.Compressed });
        }
    }
}
