// PAREDÃO DE SOM (porte do paredao.js / multimidia.js / medialib.js) — sistema de som dos veículos como no Rebaixados Elite Brasil:
//  · cada carro com paredão tem estado próprio (ligado, dono, faixa, posição, volume, porta-malas, LED/VU), independente de quem dirige;
//  · cadeia de áudio igual ao MixerControllerSOM do REB: fonte -> passa-baixa "SomAbafado" (porta-malas fechado / ouvinte no carro)
//    -> distorção (volume no talo) -> grave de paredão (lowshelf) -> analisador (LED/VU) -> som 3D no módulo, com cone para trás,
//    absorção do ar, atraso pela velocidade do som, oclusão por prédios e reverb que cresce com a distância;
//  · rede (mesmo protocolo do web, server.mjs): 'som' (estado, a cada mudança e a cada 3 s), 'somreq'/'somchk' (pedaços de 11 KB em
//    base64, janelas de 16, só para quem pediu). A faixa recebida vai para a pasta "Recebidas" da biblioteca (hash SHA-1 igual ao web).
//  Biblioteca: as músicas do próprio jogador — pasta Musicas do jogo (persistentDataPath) e, no PC, a pasta Músicas do Windows.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBTrack { public string h, n, a, m, path, url, folder; public float d; public long s; public bool ns; }

    // ---- biblioteca de músicas (medialib.js): arquivos de áudio no disco, identificados pelo mesmo hash do web
    public static class FBMediaLib
    {
        public static string Dir => Path.Combine(Application.persistentDataPath, "Musicas");
        public static string Received => Path.Combine(Dir, "Recebidas");
        static readonly string[] EXT = { ".mp3", ".ogg", ".wav", ".m4a", ".aac", ".aif", ".aiff", ".flac", ".webm" };
        static readonly Dictionary<string, FBTrack> byPath = new();
        public static List<FBTrack> lib = new();
        public static IEnumerable<string> Roots()
        {
            yield return Dir;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            string mm = null; try { mm = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic); } catch { }
            if (!string.IsNullOrEmpty(mm)) yield return mm;
#endif
        }
        public static string Mime(string ext) => ext switch { ".mp3" => "audio/mpeg", ".ogg" => "audio/ogg", ".wav" => "audio/wav", ".m4a" => "audio/mp4", ".aac" => "audio/aac", ".flac" => "audio/flac", ".webm" => "audio/webm", _ => "audio/aiff" };
        public static string Ext(string mime) => mime switch { "audio/mpeg" => ".mp3", "audio/ogg" => ".ogg", "audio/wav" => ".wav", "audio/x-wav" => ".wav", "audio/mp4" => ".m4a", "audio/aac" => ".aac", "audio/flac" => ".flac", "audio/webm" => ".webm", "video/mp4" => ".mp4", "video/webm" => ".webm", _ => ".mp3" };
        public static void Refresh()
        {
            try { Directory.CreateDirectory(Dir); } catch { }
            var list = new List<FBTrack>(); var seen = new HashSet<string>();
            foreach (var root in Roots())
            {
                if (!Directory.Exists(root)) continue; IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories).Where(f => EXT.Contains(Path.GetExtension(f).ToLowerInvariant())).Take(3000).ToList(); } catch { continue; }
                foreach (var f in files)
                {
                    try
                    {
                        var fi = new FileInfo(f); string key = f + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
                        if (!byPath.TryGetValue(key, out var t)) { t = Read(f, fi); byPath[key] = t; }
                        if (t != null && seen.Add(t.h)) list.Add(t);
                    }
                    catch { }
                }
            }
            lib = list.OrderBy(t => t.folder == "Recebidas" ? 1 : 0).ThenBy(t => t.a).ThenBy(t => t.n).ToList();
        }
        static FBTrack Read(string f, FileInfo fi)
        {
            var ext = Path.GetExtension(f).ToLowerInvariant(); var t = new FBTrack { path = f, s = fi.Length, m = Mime(ext), h = TrackId(f, fi.Length) };
            var dir = Path.GetFileName(Path.GetDirectoryName(f)); t.folder = string.Equals(dir, "Recebidas", StringComparison.OrdinalIgnoreCase) ? "Recebidas" : dir;
            var nm = Path.GetFileNameWithoutExtension(f); var parts = nm.Split(new[] { " - " }, 2, StringSplitOptions.None);
            if (parts.Length == 2) { t.a = parts[0].Trim(); t.n = parts[1].Trim(); } else { t.n = nm; t.a = ""; }
            if (ext == ".mp3") try { Id3(f, t); } catch { }
            return t;
        }
        // trackId do medialib.js: SHA-1 de [256 KB do início + 64 KB do fim + tamanho (float64 big-endian)], 10 primeiros bytes em hex
        public static string TrackId(string f, long size)
        {
            using var fs = File.OpenRead(f); return TrackId(o => { fs.Position = o.off; var b = new byte[o.len]; int r = 0; while (r < o.len) { int k = fs.Read(b, r, o.len - r); if (k <= 0) break; r += k; } return b; }, size);
        }
        public static string TrackId(byte[] all) => TrackId(o => { var b = new byte[o.len]; Buffer.BlockCopy(all, (int)o.off, b, 0, o.len); return b; }, all.Length);
        static string TrackId(Func<(long off, int len), byte[]> read, long size)
        {
            var a = read((0, (int)Math.Min(262144, size))); long bo = Math.Max(0, size - 65536); var b = read((bo, (int)(size - bo)));
            var buf = new byte[a.Length + b.Length + 8]; Buffer.BlockCopy(a, 0, buf, 0, a.Length); Buffer.BlockCopy(b, 0, buf, a.Length, b.Length);
            var d = BitConverter.GetBytes((double)size); if (BitConverter.IsLittleEndian) Array.Reverse(d); Buffer.BlockCopy(d, 0, buf, a.Length + b.Length, 8);
            using var sha = System.Security.Cryptography.SHA1.Create(); var h = sha.ComputeHash(buf);
            var sb = new System.Text.StringBuilder(); for (int i = 0; i < 10; i++) sb.Append(h[i].ToString("x2")); return sb.ToString();
        }
        // ID3v2.3/2.4: título (TIT2) e artista (TPE1)
        static void Id3(string f, FBTrack t)
        {
            using var fs = File.OpenRead(f); var hd = new byte[10]; if (fs.Read(hd, 0, 10) < 10 || hd[0] != 'I' || hd[1] != 'D' || hd[2] != '3') return;
            int ver = hd[3], size = (hd[6] << 21) | (hd[7] << 14) | (hd[8] << 7) | hd[9]; if (size <= 0 || size > 4 << 20) size = Math.Min(size, 4 << 20);
            var tag = new byte[Math.Min(size, 262144)]; fs.Read(tag, 0, tag.Length); int p = 0;
            while (p + 10 <= tag.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(tag, p, 4); if (id[0] == 0) break;
                int fsz = ver >= 4 ? (tag[p + 4] << 21) | (tag[p + 5] << 14) | (tag[p + 6] << 7) | tag[p + 7] : (tag[p + 4] << 24) | (tag[p + 5] << 16) | (tag[p + 6] << 8) | tag[p + 7];
                if (fsz <= 0 || p + 10 + fsz > tag.Length) break;
                if (id == "TIT2" || id == "TPE1")
                {
                    int enc = tag[p + 10]; var raw = new byte[fsz - 1]; Buffer.BlockCopy(tag, p + 11, raw, 0, raw.Length);
                    string s = enc switch { 1 => System.Text.Encoding.Unicode.GetString(raw.Length >= 2 && raw[0] == 0xFE ? raw.Skip(2).ToArray() : raw.Skip(raw.Length >= 2 && raw[0] == 0xFF ? 2 : 0).ToArray()), 2 => System.Text.Encoding.BigEndianUnicode.GetString(raw), 3 => System.Text.Encoding.UTF8.GetString(raw), _ => System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(raw) };
                    s = s.Trim('\0', ' '); if (s.Length > 0) { if (id == "TIT2") t.n = s; else t.a = s; }
                }
                p += 10 + fsz;
            }
        }
        public static FBTrack Find(string h) => h == null ? null : lib.FirstOrDefault(t => t.h == h);
        public static FBTrack AddReceived(string h, byte[] data, FBTrack meta)
        {
            Directory.CreateDirectory(Received);
            string safe = string.Concat(((string.IsNullOrEmpty(meta.a) ? "" : meta.a + " - ") + (meta.n ?? h)).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var f = Path.Combine(Received, safe + Ext(meta.m)); File.WriteAllBytes(f, data);
            var t = new FBTrack { h = h, n = meta.n, a = meta.a, d = meta.d, m = meta.m, s = data.Length, path = f, folder = "Recebidas" }; byPath[f + "|" + data.Length + "|" + new FileInfo(f).LastWriteTimeUtc.Ticks] = t;
            lib.RemoveAll(x => x.h == h); lib.Add(t); return t;
        }
        public static AudioType TypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".mp3" => AudioType.MPEG, ".ogg" => AudioType.OGGVORBIS, ".wav" => AudioType.WAV, ".aif" or ".aiff" => AudioType.AIFF, ".m4a" or ".aac" or ".mp4" => AudioType.ACC, _ => AudioType.UNKNOWN };
    }

    public static class FBParedao
    {
        public const int CHUNK = 11264, WIN = 16; public const float HEAR = 380, HB = 3, SOUND_SPEED = 343; public const long MAX_SHARE = 30L << 20;
        public static readonly string[] LED_MODES = { "off", "cor", "rgb", "grad", "sinc" }, VU_MODES = { "off", "rgb", "sinc" };
        static readonly string[] SOM_MODS = { "Caixa_Grave_03_04", "Caixa_Grave_01_04", "Caixa_Grave_03_02" };
        public static readonly HashSet<string> PICKUPS = new() { "S10_2017", "SaveiroG4", "SaveiroQuadrada", "SaveiroCross", "Montana", "Toro", "Amarok", "Hilux2023Conquest", "L200", "F250", "RAM3500" };
        public static Action<FBCar> OnEnded, OnNeedTrack; public static event Action Changed;
        static bool OkMime(string m) => m != null && (m.StartsWith("audio/") || m == "video/mp4" || m == "video/webm" || m == "video/ogg");

        public class Wall
        {
            public FBCar car; public bool on, playing = false, trunk = true, ended; public int owner = -1; public FBTrack tr; public float refPos; public double refT;
            public float vol = .85f, hue = UnityEngine.Random.value, level, bass, hb, bassAvg, kick; public Vector3 nodePos; public bool nodePosOk; public string ledM = "sinc", vu = "sinc"; public Color ledC = new(1, .165f, .427f);
            public AudioSource src; public FBAcTap tap; public readonly FBAcVoice[] dv = new FBAcVoice[2], ev = new FBAcVoice[4]; public FBAcReverb rev; public bool full;
            public AudioLowPassFilter lp; public string loadedH, wantH; public bool ready, wait, loading; public float st, ot, et, occK; public bool occ;
            public Bounds box; public bool boxOk; public readonly Echo[] echo = new Echo[4]; public Vector3 echoFrom; public float echoT;
            public Stream stream; public string localPath;
            public AudioClip local; public bool loop;                                                   // som próprio de carro sem paredão (PlayLocal): só aqui, em loop
            public List<Material> leds = new(), vus = new(); public List<(Transform t, Vector3 s)> spk = new(); public bool fxDone;
        }
        public class Stream { public FBTrack meta; public byte[][] chunks; public int n, got, next, asked = -1, owner; public bool done, bad; public double last; }
        // eco numa fachada: ponto de reflexão visto do ouvinte, comprimento do caminho caixa->parede->ouvinte, ganho; troca de eco sem estalo
        public class Echo { public Vector3 q; public float len, g, gT, cur; public bool live; }
        static readonly Dictionary<FBCar, Wall> walls = new(); static readonly Dictionary<string, Stream> streams = new(); static readonly HashSet<string> serving = new();
        static double Now => Time.realtimeSinceStartupAsDouble;
        static int MyId => Online ? FBNet.I.myId : 0;
        static bool Online => FBNet.I && FBNet.I.open;
        static bool Mine(Wall W) => W.owner == MyId;
        static void Emit() { try { Changed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }
        public static Wall Get(FBCar car) { if (!walls.TryGetValue(car, out var W)) walls[car] = W = new Wall { car = car }; return W; }
        public static Wall State(FBCar car) => car && walls.TryGetValue(car, out var W) ? W : null;
        public static bool Has(FBCar car) => car && car.soundWallNode;
        public static IEnumerable<Wall> List() => walls.Values;
        static float Expected(Wall W) => W.refPos + (W.playing ? (float)(Now - W.refT) : 0);
        static Runner R { get { if (!runner) { var g = new GameObject("FBParedao"); UnityEngine.Object.DontDestroyOnLoad(g); runner = g.AddComponent<Runner>(); } return runner; } }
        static Runner runner;
        class Runner : MonoBehaviour
        {
            // [parceiro] v1 (thread de física): tempo de CPU do LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
            static readonly int __fbt_LateUpdate = FBTempo.Id("FBParedao.Runner.LateUpdate");
            void LateUpdate() { if (!FBTempo.On) { LateUpdate__fbt(); return; } long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0); }
            void LateUpdate__fbt() { var ear = FBAudio.Ear; if (!ear) return; var mc = FBVehicles.CarOf(FBMatch.Player); Tick(Time.deltaTime, ear.position, mc && FBDrive.Cur == mc && InCabin(mc, ear.position) ? mc : null); }   // "is FBCar" não enxerga carro destruído (NRE a cada quadro)
        }
        // ouvinte dentro da cabine do carro do paredão (câmera de 1ª pessoa ou muito perto): som abafado do porta-malas/cabine
        static bool InCabin(FBCar car, Vector3 ear) { var lp = car.transform.InverseTransformPoint(ear); var b = car.lbox; return b.Contains(lp); }

        // ---- montagem do módulo (vehicles.js addSom + somModule): em pé na caçamba, virado para trás; LED vira 4 fitas na face
        public static void Mount(FBCar car, string mod, Dictionary<string, object> d)
        {
            var prefab = FBRegistry.I ? FBRegistry.I.Som(mod) : null; var m = Json.O(GameData.Som, mod); if (!prefab || m == null) return;
            var som = Json.O(d, "som"); var mala = Json.L(Json.O(d, "doors"), "mala"); var bse = Json.L(som, "base"); if (mala == null && bse == null) return;
            var mn = Vec(Json.L(m, "min")); var mx = Vec(Json.L(m, "max"));     // espaço do web (x à direita)
            var shellT = car.transform.Find("body/shell"); var o = UnityEngine.Object.Instantiate(prefab, shellT ? shellT : car.transform); o.name = "sound_wall";   // no web o módulo vai no "shell" (altura da carroceria)
            foreach (var c in o.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
            Vector3 webPos; float k = 1; var bed = Json.O(som, "bed");
            if (bed != null && (mx.x - mn.x) <= Json.F(bed, "w") + .02f) webPos = new Vector3((mn.x + mx.x) / 2, Json.F(bed, "floor") + .01f - mn.y, Json.F(bed, "cab") - .04f + mn.z);
            else
            {
                float track = 0; foreach (var w in Json.L(d, "wheels") ?? new List<object>()) track = Mathf.Max(track, 2 * Mathf.Abs(Json.Fv(Json.L(w, "p")[0])));
                float wd = mx.x - mn.x; k = Mathf.Min(1, track * .8f / Mathf.Max(.01f, wd));
                float y = bse != null ? Json.Fv(bse[1]) : Json.Fv(mala[1]) + .12f;
                float z = mala != null ? Mathf.Max(Json.Fv(mala[2]) + .3f, (bse != null ? Json.Fv(bse[2]) : -1e9f) - k * (mx.z - mn.z) / 2) : Json.Fv(bse[2]);
                webPos = new Vector3(k * (mn.x + mx.x) / 2, y - k * mn.y, z + k * mx.z);
            }
            o.transform.localPosition = new Vector3(-webPos.x, webPos.y, webPos.z); o.transform.localRotation = Quaternion.Euler(0, 180, 0); o.transform.localScale = Vector3.one * k;
            var W = Get(car); W.leds.Clear(); W.vus.Clear(); W.spk.Clear(); Dress(o, W);
            car.soundWallNode = o.transform; car.soundMod = mod; W.fxDone = true;
        }
        // módulo pronto para montar (somModule do web): LED vira 4 fitas na face dos alto-falantes, VU com material próprio; registra no estado do paredão
        public static GameObject Module(string mod, Wall W)
        {
            var prefab = FBRegistry.I ? FBRegistry.I.Som(mod) : null; if (!prefab) return null; var o = UnityEngine.Object.Instantiate(prefab); o.name = "som_" + mod;
            foreach (var c in o.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c); Dress(o, W); return o;
        }
        static void Dress(GameObject o, Wall W)
        {
            var unlit = FBShaders.Unlit; var ledMat = new Material(unlit); ledMat.SetColor("_BaseColor", new Color(.13f, 0, .03f)); var vuMat = new Material(unlit); vuMat.SetColor("_BaseColor", new Color(.07f, .07f, .07f));
            W.leds.Add(ledMat); W.vus.Add(vuMat);
            var inv = o.transform.worldToLocalMatrix; var bb = new Bounds(); bool any = false;
            foreach (var r in o.GetComponentsInChildren<Renderer>(true))
            {
                string t = Tag(r.transform);
                if (t == "led") r.enabled = false;
                else if (t == "vu") r.sharedMaterial = vuMat;
                else if (!(r.transform.parent && System.Text.RegularExpressions.Regex.IsMatch(r.transform.parent.name, "^spk\\d")))
                {
                    var mf = r.GetComponent<MeshFilter>(); Bounds b; Matrix4x4 M;
                    if (mf && mf.sharedMesh) { b = mf.sharedMesh.bounds; M = inv * r.transform.localToWorldMatrix; } else { b = r.bounds; M = inv; }   // caixa LOCAL da malha (a de mundo inchava com o carro girado)
                    foreach (var cnr in Corners(b)) { var lp = M.MultiplyPoint3x4(cnr); if (!any) { bb = new Bounds(lp, Vector3.zero); any = true; } else bb.Encapsulate(lp); }
                }
            }
            foreach (var t in o.GetComponentsInChildren<Transform>(true)) if (System.Text.RegularExpressions.Regex.IsMatch(t.name, "^spk\\d+$")) W.spk.Add((t, t.localScale));
            if (!any) return;
            W.box = bb; W.boxOk = true;                                                       // colunas de som (esquerda/direita) na frente das caixas
            float w = .05f, z = bb.max.z + .015f, sx = bb.size.x, sy = bb.size.y, cx = bb.center.x, cy = bb.center.y;
            var led = new GameObject("led").transform; led.SetParent(o.transform, false);
            foreach (var (px, py, gx, gy) in new[] { (cx, bb.max.y - w / 2, sx, w), (cx, bb.min.y + w / 2, sx, w), (bb.min.x + w / 2, cy, w, sy), (bb.max.x - w / 2, cy, w, sy) })
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.Destroy(s.GetComponent<Collider>()); s.name = "led_fita";
                s.transform.SetParent(led, false); s.transform.localPosition = new Vector3(px, py, z); s.transform.localScale = new Vector3(gx, gy, .02f); s.GetComponent<Renderer>().sharedMaterial = ledMat;
            }
        }
        static string Tag(Transform t) { if (t.name == "led" || t.name == "vu") return t.name; if (t.parent && (t.parent.name == "led" || t.parent.name == "vu")) return t.parent.name; return null; }
        static IEnumerable<Vector3> Corners(Bounds b) { for (int i = 0; i < 8; i++) yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z); }
        static Vector3 Vec(List<object> l) => l == null ? Vector3.zero : new Vector3(Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2]));
        // módulo entre os 6 primeiros da lista dos que cabem nesta caçamba (vehicles.json som.fit); sem lista, um dos 3 graves
        public static string PickModule(Dictionary<string, object> d, Func<float> rng)
        {
            var fit = (Json.L(Json.O(d, "som"), "fit") ?? new List<object>()).Select(x => x as string).Where(x => FBRegistry.I.Som(x)).ToList();
            return fit.Count > 0 ? fit[Mathf.Min(fit.Count - 1, (int)(rng() * Mathf.Min(6, fit.Count)))] : SOM_MODS[Mathf.Min(2, (int)(rng() * SOM_MODS.Length))];
        }

        // ---- controle (API do Paredao do web)
        public static FBCar MineCar(Vector3 playerPos)
        {
            var d = FBVehicles.CarOf(FBMatch.Player); if (d && Has(d)) return d;
            FBCar best = null; float bd = 40; foreach (var W in walls.Values) if (W.on && Mine(W) && !W.local && W.car) { float dd = Vector3.Distance(W.car.transform.position, playerPos); if (dd < bd) { bd = dd; best = W.car; } }
            return best;
        }
        public static bool CanControl(FBCar car) { var W = State(car); return W == null || !W.on || W.owner < 0 || Mine(W); }
        public static string OwnerName(FBCar car) { var W = State(car); return W != null && W.owner >= 0 && !Mine(W) ? FBNet.I?.NameOf(W.owner) ?? "OUTRO JOGADOR" : null; }
        public static bool Toggle(FBCar car) => SetOn(car, !(State(car)?.on ?? false));
        public static bool SetOn(FBCar car, bool on, bool quiet = false)   // quiet: regra do rolê calando o paredão da cidade (sem aviso na tela)
        {
            if (!Has(car)) return false; var W = Get(car); _ = R;
            if (!CanControl(car)) { Toast($"PAREDÃO DE {OwnerName(car) ?? "OUTRO JOGADOR"}", 1.4f); return false; }
            W.on = on; W.owner = MyId;
            if (on && W.tr == null) { OnNeedTrack?.Invoke(car); if (W.tr == null) { Toast("PAREDÃO LIGADO · ESCOLHA UMA MÚSICA NA CENTRAL (K)", 2.2f); Broadcast(W); Emit(); return true; } }
            if (!on) { W.playing = false; StopPlayer(W); }
            if (!quiet) Toast(on ? "PAREDÃO LIGADO" : "PAREDÃO DESLIGADO", 1.2f); Broadcast(W); if (!on) W.owner = -1; Emit(); return true;
        }
        public static bool Play(FBCar car, FBTrack t, float at = 0)
        {
            if (!Has(car) || t == null || !CanControl(car)) return false; var W = Get(car); _ = R;
            W.on = true; W.owner = MyId; W.ended = false;
            W.tr = new FBTrack { h = t.h, n = t.n, a = t.a ?? "", d = t.d, m = t.m ?? "audio/mpeg", s = t.s, url = t.url, path = t.path };
            W.localPath = t.path;
            if (Online && (W.tr.s > MAX_SHARE || !OkMime(W.tr.m))) Toast($"FAIXA GRANDE DEMAIS PARA COMPARTILHAR (MÁX. {MAX_SHARE >> 20} MB) — SÓ VOCÊ OUVE", 3);
            W.playing = true; W.refPos = at; W.refT = Now;
            StopPlayer(W); Want(W); Broadcast(W); Emit(); return true;
        }
        public static void Pause(FBCar car, bool p = true) { var W = State(car); if (W?.tr == null || !Mine(W)) return; W.refPos = Expected(W); W.refT = Now; W.playing = !p; if (!p && W.ended) { W.ended = false; W.refPos = 0; } Sync(W, true); Broadcast(W); Emit(); }
        public static void Seek(FBCar car, float s) { var W = State(car); if (W?.tr == null || !Mine(W)) return; W.refPos = Mathf.Max(0, s); W.refT = Now; W.ended = false; Sync(W, true); Broadcast(W); Emit(); }
        public static void SetVol(FBCar car, float v) { if (!Has(car) || !CanControl(car)) return; var W = Get(car); W.vol = Mathf.Clamp01(v); Broadcast(W); Emit(); }
        public static void SetTrunk(FBCar car, bool open) { if (!Has(car) || !CanControl(car)) return; var W = Get(car); W.trunk = open; Broadcast(W); Emit(); }
        public static void SetLed(FBCar car, string m, Color? c = null) { if (!Has(car) || !CanControl(car)) return; var W = Get(car); W.ledM = m ?? W.ledM; if (c is Color cc) W.ledC = cc; Broadcast(W); Emit(); }
        public static void SetVu(FBCar car, string m) { if (!Has(car) || !CanControl(car)) return; var W = Get(car); W.vu = m; Broadcast(W); Emit(); }
        public static float Position(FBCar car) { var W = State(car); return W != null ? Mathf.Min(Expected(W), W.tr != null && W.tr.d > 0 ? W.tr.d : 1e9f) : 0; }
        public static float Duration(FBCar car) { var W = State(car); return W?.src && W.src.clip ? W.src.clip.length : W?.tr?.d ?? 0; }
        public static string Buffering(FBCar car)
        {
            var W = State(car); if (W == null) return ""; var S = W.stream;
            if (W.tr != null && W.tr.ns && !Mine(W)) return "FAIXA SÓ NO APARELHO DO DONO"; if (S != null && S.bad) return "FALHA AO RECEBER A FAIXA";
            return W.wait || W.loading ? "SINCRONIZANDO…" : S != null && !S.done ? $"RECEBENDO {Mathf.RoundToInt(100f * S.got / Mathf.Max(1, S.n))}%" : "";
        }
        // SOM PRÓPRIO de um carro sem paredão (carro do açaí, pedido 26/10: "tão alto quanto os paredões, com o mesmo reverb, eco,
        // distância"): mesma acústica do paredão (colunas, ecos nas fachadas, reverb da rua, atraso/Doppler pela distância, prédio na
        // frente), mas só neste aparelho (não vai pela rede nem aparece na central K), em loop, e o clipe é do jogo (não é destruído)
        public static Wall PlayLocal(FBCar car, AudioClip clip, float vol = .85f, float at = 0)
        {
            if (!car || !clip) return null; var W = Get(car); _ = R;
            W.local = clip; W.loop = true; W.on = true; W.owner = MyId; W.playing = true; W.ended = false; W.vol = vol; W.trunk = true;
            W.tr = new FBTrack { h = "local_" + clip.name, n = clip.name, d = clip.length, ns = true }; W.refPos = at; W.refT = Now;
            StopPlayer(W); Want(W); return W;
        }
        public static void StopLocal(FBCar car) { var W = State(car); if (W == null || !W.local) return; StopPlayer(W); walls.Remove(car); }
        public static void Rebuilt(FBCar car) { var W = State(car); if (W != null && W.src) { var s = W.src.transform; var host = car.soundWallNode ? car.soundWallNode : car.transform; s.SetParent(host, false); s.localPosition = new Vector3(0, .8f, 0); } }   // oficina remontou o som
        public static void Reset() { foreach (var W in walls.Values) StopPlayer(W); walls.Clear(); streams.Clear(); serving.Clear(); Emit(); }
        public static void PeerLeft(int id) { foreach (var W in walls.Values) if (W.owner == id) { W.on = false; W.owner = -1; StopPlayer(W); } Emit(); }
        static void Toast(string s, float t) => FBHUD.I?.Center(s, t);

        // ---- reprodutor
#if UNITY_WEBGL && !UNITY_EDITOR
        const bool SIMPLE = true;                                                                // WebGL: sem OnAudioFilterRead — AudioSource 3D simples
#else
        const bool SIMPLE = false;
#endif
        public const float LEAD = .12f;                                                          // a fonte toca adiantada; cada voz atrasa LEAD + distância/343
        static void MakePlayer(Wall W)
        {
            var host = W.car.soundWallNode ? W.car.soundWallNode : W.car.transform;
            var g = new GameObject("paredao_audio"); g.transform.SetParent(host, false); g.transform.localPosition = new Vector3(0, .8f, 0);
            var s = g.AddComponent<AudioSource>(); s.playOnAwake = false; s.dopplerLevel = 0; s.priority = 1; s.outputAudioMixerGroup = FBAudio.Group(false);
            if (SIMPLE)
            {
                s.spatialBlend = 1; s.spread = 60; s.rolloffMode = AudioRolloffMode.Custom; s.maxDistance = HEAR;
                var curve = new AnimationCurve(); for (int i = 0; i <= 40; i++) { float d = HEAR * i * i / 1600f, v = d <= 12 ? 1 : 12 / (12 + .8f * (d - 12)); curve.AddKey(d / HEAR, v); }
                s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
            }
            else
            {   // fonte única 2D (silenciada pelo tap) + vozes: 2 colunas, 4 ecos e o reverb da rua
                s.spatialBlend = 0; s.volume = 1; s.bypassListenerEffects = true; s.bypassReverbZones = true;
                W.tap = g.AddComponent<FBAcTap>(); var root = R.transform;
                W.dv[0] = FBAcVoice.Make(root, W.tap, 0, "paredao_coluna_E"); W.dv[1] = FBAcVoice.Make(root, W.tap, 1, "paredao_coluna_D");
                for (int i = 0; i < W.ev.Length; i++) { W.ev[i] = FBAcVoice.Make(root, W.tap, 2, "paredao_eco" + i); W.echo[i] = new Echo(); }
                W.rev = FBAcReverb.Make(root, W.tap);
            }
            W.src = s; W.loadedH = null; W.ready = false; W.echoT = 0;
        }
        static void StopPlayer(Wall W)
        {
            W.wantH = null; W.ready = false; W.loadedH = null; W.loading = false;
            if (W.src) { var c = W.src.clip; W.src.Stop(); UnityEngine.Object.Destroy(W.src.gameObject); if (c && c != W.local) UnityEngine.Object.Destroy(c); }
            foreach (var v in W.dv) if (v) UnityEngine.Object.Destroy(v.gameObject); foreach (var v in W.ev) if (v) UnityEngine.Object.Destroy(v.gameObject);
            if (W.rev) UnityEngine.Object.Destroy(W.rev.gameObject);
            W.src = null; W.tap = null; W.rev = null; for (int i = 0; i < 2; i++) W.dv[i] = null; for (int i = 0; i < W.ev.Length; i++) W.ev[i] = null;
        }
        static void Feed(Wall W, string path)
        {
            if (!W.src) MakePlayer(W); if (W.loadedH == W.tr.h || W.loading) return;
            W.loading = true; string h = W.tr.h; R.StartCoroutine(Load(W, path, h));
        }
        static IEnumerator Load(Wall W, string path, string h)
        {
            var type = FBMediaLib.TypeOf(path);
            using var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            if (req.downloadHandler is DownloadHandlerAudioClip dh) { dh.streamAudio = false; dh.compressed = true; }
            yield return req.SendWebRequest();
            W.loading = false;
            if (W.tr == null || W.tr.h != h || !W.src) yield break;
            if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning("[paredão] " + req.error + " " + path); Toast("NÃO DEU PARA TOCAR ESTE FORMATO NESTA PLATAFORMA", 2); yield break; }
            var clip = DownloadHandlerAudioClip.GetContent(req); if (!clip) yield break; clip.name = W.tr.n ?? h;
            if (W.src.clip) UnityEngine.Object.Destroy(W.src.clip);
            W.src.clip = clip; W.loadedH = h; W.ready = true; if (W.tr.d <= 0) W.tr.d = clip.length;
            Sync(W, true);
        }
        static void Sync(Wall W, bool hard = false, float delay = 0)
        {
            var s = W.src; if (!s || !W.ready || !s.clip) return;
            float want = Expected(W) - delay + (SIMPLE ? 0 : LEAD), len = s.clip.length;
            if (W.loop) want = Mathf.Repeat(want, len);                                          // loop: posição dentro da volta
            if (!W.playing) { if (s.isPlaying) s.Pause(); if (want >= 0 && want < len && Mathf.Abs(s.time - want) > .3f) s.time = want; return; }
            if (!W.loop && want >= len - .05f) { if (s.isPlaying) s.Pause(); return; }
            if (want < 0) { if (s.isPlaying) s.Pause(); W.wait = true; return; }    // som ainda "viajando" até o ouvinte
            float d = s.time - want; if (W.loop) d = Mathf.Repeat(d + len / 2, len) - len / 2;   // na virada da volta não conta como atraso
            if (hard || !s.isPlaying || Mathf.Abs(d) > .35f) { s.time = Mathf.Clamp(want, 0, len - .01f); s.pitch = 1; }
            else s.pitch = 1 - Mathf.Clamp(d * .25f, -.04f, .04f);
            if (!s.isPlaying) s.Play();
            W.wait = false;
        }

        // ---- rede: pedaços da faixa
        static void Want(Wall W)
        {
            var t = W.tr; if (t == null) return;
            if (W.local) { if (!W.src) MakePlayer(W); W.src.clip = W.local; W.src.loop = W.loop; W.loadedH = t.h; W.ready = true; Sync(W, true); return; }
            if (!string.IsNullOrEmpty(W.localPath) && Mine(W) && File.Exists(W.localPath)) { Feed(W, W.localPath); return; }
            var lt = FBMediaLib.Find(t.h); if (lt != null && File.Exists(lt.path)) { W.stream = null; Feed(W, lt.path); return; }
            if (t.url != null && t.url.StartsWith("http")) { Feed(W, t.url); return; }
            if (Mine(W)) return; Request(W, t);
        }
        static void Request(Wall W, FBTrack t)
        {
            if (t.ns || !(t.s > 0) || t.s > MAX_SHARE || !OkMime(t.m) || t.h == null || !System.Text.RegularExpressions.Regex.IsMatch(t.h, "^[0-9a-f]{20}$")) return;
            if (!streams.TryGetValue(t.h, out var S)) { S = new Stream { meta = t, n = (int)((t.s + CHUNK - 1) / CHUNK), owner = W.owner, last = Now }; S.chunks = new byte[S.n][]; streams[t.h] = S; }
            S.owner = W.owner; W.stream = S; Ask(S);
        }
        static void Ask(Stream S)
        {
            if (S.done || S.asked == S.next) return; S.asked = S.next; S.last = Now;
            FBNet.I?.SendEv("somreq", new Dictionary<string, object> { ["to"] = S.owner, ["h"] = S.meta.h, ["i"] = S.next });
        }
        static void GotChunk(Dictionary<string, object> m)
        {
            var h = Json.S(m, "h"); if (h == null || !streams.TryGetValue(h, out var S)) return; int i = Json.I(m, "i"); var d = Json.S(m, "d");
            if (!(i >= 0 && i < S.n) || S.chunks[i] != null || Json.I(m, "id") != S.owner || d == null || d.Length > CHUNK * 1.4 + 8) return;
            try { S.chunks[i] = Convert.FromBase64String(d); } catch { return; }
            S.got++; S.last = Now; while (S.next < S.n && S.chunks[S.next] != null) S.next++;
            if (S.next >= S.n)
            {
                S.done = true; var all = new byte[S.chunks.Sum(c => (long)c.Length)]; int o = 0; foreach (var c in S.chunks) { Buffer.BlockCopy(c, 0, all, o, c.Length); o += c.Length; }
                if (all.Length != S.meta.s) { Debug.LogWarning($"[paredão] tamanho diferente {all.Length} {S.meta.s}"); S.bad = true; return; }
                if (FBMediaLib.TrackId(all) != h) { Debug.LogWarning("[paredão] hash não confere — não vai para a biblioteca"); S.bad = true; return; }
                var t = FBMediaLib.AddReceived(h, all, S.meta);
                foreach (var W in walls.Values) if (W.tr?.h == h) { W.stream = null; W.loadedH = null; Feed(W, t.path); }
                Emit();
            }
            else if (S.next >= Mathf.Max(0, S.asked) + WIN) Ask(S);
        }
        static IEnumerator Serve(int to, string h, int from)
        {
            string key = to + "|" + h; if (!serving.Add(key)) yield break;
            try
            {
                var W = walls.Values.FirstOrDefault(x => x.on && Mine(x) && x.tr?.h == h); if (W == null || from < 0) yield break;   // só a faixa do meu paredão ligado
                var path = File.Exists(W.localPath ?? "") ? W.localPath : FBMediaLib.Find(h)?.path; if (path == null || !File.Exists(path)) yield break;
                long size = new FileInfo(path).Length; if (size > MAX_SHARE) yield break; int n = (int)((size + CHUNK - 1) / CHUNK);
                for (int i = from; i < Mathf.Min(n, from + WIN); i++)
                {
                    byte[] b; using (var fs = File.OpenRead(path)) { fs.Position = (long)i * CHUNK; int len = (int)Math.Min(CHUNK, size - fs.Position); b = new byte[len]; int r = 0; while (r < len) { int k = fs.Read(b, r, len - r); if (k <= 0) break; r += k; } }
                    FBNet.I?.SendEv("somchk", new Dictionary<string, object> { ["to"] = to, ["h"] = h, ["i"] = i, ["d"] = Convert.ToBase64String(b) });
                    if ((i & 3) == 3) yield return null;
                }
            }
            finally { serving.Remove(key); }
        }
        static Dictionary<string, object> Pack(Wall W)
        {
            var t = W.tr; Dictionary<string, object> tr = null;
            if (t != null) { tr = new() { ["h"] = t.h, ["n"] = t.n, ["a"] = t.a ?? "", ["d"] = Mathf.RoundToInt(t.d), ["m"] = t.m, ["s"] = t.s }; if (t.url != null) tr["url"] = t.url; if (t.s > MAX_SHARE || !OkMime(t.m)) tr["ns"] = 1; }
            return new() { ["car"] = W.car.syncId, ["on"] = W.on ? 1 : 0, ["tr"] = tr, ["at"] = Mathf.Round(Expected(W) * 1000) / 1000f, ["pl"] = W.playing ? 1 : 0, ["vol"] = W.vol, ["tk"] = W.trunk ? 1 : 0,
                ["led"] = new Dictionary<string, object> { ["m"] = W.ledM, ["c"] = "#" + ColorUtility.ToHtmlStringRGB(W.ledC).ToLowerInvariant() }, ["vu"] = W.vu };
        }
        static void Broadcast(Wall W) { if (Online && Mine(W) && !W.local && W.car.syncId != null) FBNet.I.SendEv("som", Pack(W)); W.hb = 0; }
        public static void OnNet(Dictionary<string, object> m)
        {
            var e = Json.S(m, "e"); int me = MyId;
            if (e == "somreq") { if (Json.I(m, "to") == me) R.StartCoroutine(Serve(Json.I(m, "id"), Json.S(m, "h"), Json.I(m, "i"))); return; }
            if (e == "somchk") { if (Json.I(m, "to") == me) GotChunk(m); return; }
            var sid = Json.S(m, "car") ?? (m.TryGetValue("car", out var cv) ? cv?.ToString() : null); var car = FBVehicles.list.FirstOrDefault(c => c && c.syncId == sid); if (!car || !Has(car)) return;
            var W = Get(car); _ = R; if (Mine(W) && W.on) return;                   // conflito: quem já é dono continua no controle
            float lat = Mathf.Min(.5f, (FBNet.I ? FBNet.I.ping : 80) / 1000f); var was = W.tr?.h;
            bool on = Json.I(m, "on") != 0; W.on = on; W.owner = on ? Json.I(m, "id") : -1; W.vol = Json.F(m, "vol", W.vol); W.trunk = Json.I(m, "tk") != 0;
            var led = Json.O(m, "led"); if (led != null) { W.ledM = Json.S(led, "m") ?? W.ledM; if (ColorUtility.TryParseHtmlString(Json.S(led, "c") ?? "", out var c)) W.ledC = c; }
            W.vu = Json.S(m, "vu") ?? W.vu;
            var tr = Json.O(m, "tr"); W.tr = tr == null ? null : new FBTrack { h = Json.S(tr, "h"), n = Json.S(tr, "n"), a = Json.S(tr, "a"), d = Json.F(tr, "d"), m = Json.S(tr, "m"), s = (long)Json.F(tr, "s"), url = Json.S(tr, "url"), ns = Json.I(tr, "ns") != 0 };
            W.localPath = null; W.playing = Json.I(m, "pl") != 0; W.refPos = Json.F(m, "at") + (W.playing ? lat : 0); W.refT = Now; W.ended = false;
            if (!W.on || W.tr == null) StopPlayer(W); else if (was != W.tr.h) { StopPlayer(W); W.stream = null; }
            Emit();
        }

        // ---- por quadro: alcance, filtros, sincronia, LEDs
        static void Tick(float dt, Vector3 camPos, FBCar insideCar)
        {
            int rank = 0;
            foreach (var W in walls.Values.OrderBy(w => w.car ? (w.car.transform.position - camPos).sqrMagnitude : 1e12f).ToList())
            {
                var car = W.car; if (!car) { StopPlayer(W); walls.Remove(car); continue; }
                if (W.on && Mine(W) && (W.hb += dt) > HB) Broadcast(W);
                if (W.on && Mine(W) && W.playing && !W.loop && W.tr != null && W.tr.d > 0 && !W.ended && Expected(W) > W.tr.d + .25f) { W.ended = true; W.playing = false; OnEnded?.Invoke(car); Emit(); }   // fim pelo relógio
                var node = car.soundWallNode; var pos = node ? node.position : car.transform.position; float d = Vector3.Distance(pos, camPos);
                bool hear = W.on && W.tr != null && d < HEAR;
                if (!hear) { if (W.src) StopPlayer(W); }
                else
                {
                    if ((!W.src || W.loadedH != W.tr.h) && W.wantH != W.tr.h) { W.wantH = W.tr.h; Want(W); }
                    if (W.src)
                    {
                        if ((W.ot += dt) > .25f) { W.ot = 0; W.occ = !(insideCar == car) && d > 4 && Occluded(camPos, pos + Vector3.up * .8f); }
                        W.occK = Mathf.MoveTowards(W.occK, W.occ ? 1 : 0, dt * 3);
                        if (SIMPLE) SimpleAudio(W, car, node, pos, camPos, insideCar == car, dt);
                        else Acoustics(W, car, node, pos, camPos, insideCar == car, dt, rank++ < 2);
                        float delay = SIMPLE && insideCar != car ? Mathf.Min(1.9f, d / SOUND_SPEED) : 0;
                        if ((W.st += dt) > .5f || W.wait) { W.st = 0; Sync(W, false, delay); }
                        if (W.stream != null && !W.stream.done && Now - W.stream.last > 4) { W.stream.asked = -1; Ask(W.stream); }   // pacote perdido: pede de novo
                    }
                }
                // ---- nível -> LED / VU / alto-falantes
                float lvl = 0, bass = 0; if (W.playing && W.src && W.src.isPlaying) { if (W.tap) { bass = W.tap.bass; lvl = W.tap.level; } else if (SIMPLE) { bass = .35f + .25f * Mathf.Sin(Time.time * 12); lvl = .5f; } }
                W.bass += (bass - W.bass) * Mathf.Min(1, dt * (bass > W.bass ? 30 : 8)); W.level += (lvl - W.level) * Mathf.Min(1, dt * 12);
                if (d > 140) continue;
                bool on = W.on; W.hue = (W.hue + dt * (.12f + W.bass * .8f)) % 1;
                string ledM = on ? W.ledM : "off";
                foreach (var m in W.leds)
                {
                    Color c = ledM switch
                    {
                        "off" => new Color(.1f, .04f, .04f),
                        "cor" => W.ledC * (.55f + W.bass * .9f),
                        "rgb" => Color.HSVToRGB(W.hue, 1, 1),
                        "grad" => Hsl((Hue(W.ledC) + Mathf.Sin(W.hue * Mathf.PI * 2) * .12f + 1) % 1, 1, .3f + W.bass * .35f),
                        _ => Hsl((W.hue + W.bass * .4f) % 1, 1, .12f + W.bass * .55f)
                    };
                    m.SetColor("_BaseColor", c);
                }
                string vuM = on ? W.vu : "off";
                foreach (var m in W.vus) m.SetColor("_BaseColor", vuM == "off" ? new Color(.07f, .07f, .07f) : vuM == "rgb" ? Hsl((W.hue + .5f) % 1, 1, .45f) : Hsl(.33f - Mathf.Min(.33f, W.level * .9f), 1, .15f + W.level * .6f));
                // batida: grave instantâneo acima da média lenta = bumbo -> pulso que decai em ~140 ms; o cone empurra para fora e o módulo treme
                W.bassAvg += (W.bass - W.bassAvg) * Mathf.Min(1, dt * 1.6f);
                float beat = on && W.playing ? Mathf.Clamp01((W.bass - W.bassAvg * 1.08f) / Mathf.Max(.04f, W.bassAvg * .7f)) : 0;
                W.kick = Mathf.Max(beat, W.kick - dt * 7);
                float kk = W.kick * Mathf.Clamp01(W.vol * 1.2f), hold = on && W.playing ? W.bass * .25f : 0;   // um pouco de "ar" constante no grave + o soco da batida
                foreach (var (t, s) in W.spk) if (t) t.localScale = new Vector3(s.x * (1 + kk * .07f), s.y * (1 + kk * .07f), s.z * (1 + hold * .6f + kk * 1.6f));
                if (node) { if (!W.nodePosOk) { W.nodePos = node.localPosition; W.nodePosOk = true; } node.localPosition = W.nodePos + new Vector3(0, kk * .006f, -kk * .004f); }
            }
        }
        // ---- acústica (FBAcoustics): parâmetros de cada voz a cada quadro
        static float Dist(float d, float refD = 10, float roll = .8f) => d <= refD ? 1 : refD / (refD + roll * (d - refD));   // 1/r suave (10 m de referência)
        static float Air(float d) => Mathf.Max(2400, 20000 * Mathf.Exp(-d / 170));                                               // ar come os agudos de longe
        static float Fade(float d) { float far = Mathf.Clamp01((d - 120) / (HEAR - 130)); return (1 - far) * (1 - far); }
        static void Acoustics(Wall W, FBCar car, Transform node, Vector3 pos, Vector3 camPos, bool inCar, float dt, bool full)
        {
            var ear = FBAudio.Ear; if (!ear || !W.tap) return; var right = ear.right; var fwd = ear.forward;
            var som = Json.O(Json.O(GameData.Vehicles, car.id), "som"); float trunkCut = !W.trunk && Json.F(som, "abafa", 1) != 0 ? 650 : 20000;
            float vol = W.vol * W.vol * .95f * FBAudio.VehVol; W.tap.drive = Mathf.Max(0, W.vol - .8f) * 5; float occ = W.occK;
            // colunas na frente das caixas: quem olha de frente para o paredão tem o canal esquerdo à esquerda
            Vector3 face = node ? node.forward : W.local ? car.transform.forward : -car.transform.forward, eL, eR;   // corneta do vendedor: no teto, para a frente
            if (node && W.boxOk) { var b = W.box; float zf = b.max.z; eL = node.TransformPoint(new Vector3(b.center.x + b.size.x * .27f, b.center.y, zf)); eR = node.TransformPoint(new Vector3(b.center.x - b.size.x * .27f, b.center.y, zf)); }
            else { var c0 = pos + Vector3.up * .8f; var rt = Vector3.Cross(Vector3.up, face).normalized; eL = c0 + rt * .45f; eR = c0 - rt * .45f; }
            var mid = (eL + eR) / 2; float dm = Mathf.Max(.3f, Vector3.Distance(mid, camPos));
            for (int k = 0; k < 2; k++)
            {
                var v = W.dv[k]; if (!v) continue; var e = k == 0 ? eL : eR;
                if (!full) { e = mid; v.chan = 2; } else v.chan = k;
                var to = e - camPos; float d = Mathf.Max(.3f, to.magnitude); var dir = to / d;
                float ang = Vector3.Angle(face, -dir);                                                        // 0° = de frente para as caixas
                float coneG = Mathf.Lerp(1, .55f, Mathf.InverseLerp(60, 165, ang)), coneC = Mathf.Lerp(20000, 1500, Mathf.InverseLerp(50, 170, ang));   // grave vai para todo lado, agudo só para a frente
                float cut = Mathf.Min(Mathf.Min(Air(d), coneC), Mathf.Min(trunkCut, Mathf.Lerp(20000, 650, occ)));
                float g = vol * Dist(d) * Fade(d) * coneG * Mathf.Lerp(1, .32f, occ);
                if (inCar) { cut = Mathf.Min(trunkCut, 2600); g = vol * .85f; }
                v.gain = !full && k == 1 ? 0 : g * (full ? 1 : 1.35f); v.cutoff = cut; v.delay = LEAD + (inCar ? 0 : d / SOUND_SPEED);
                float side = Vector3.Dot(dir, right), front = Vector3.Dot(dir, fwd);
                v.pan = side * Mathf.Clamp01(d / 2.5f); v.behind = inCar ? 0 : Mathf.Clamp01(-front) * .8f; v.width = inCar ? .3f : .78f;
            }
            // ecos nas fachadas (só nos 2 paredões mais perto)
            int walls = -1;
            if (full && !inCar)
            {
                if ((W.echoT -= dt) <= 0 || (W.echoFrom - mid).sqrMagnitude > 1) { W.echoT = .3f; W.echoFrom = mid; walls = FindEchoes(W, mid + face * .4f + Vector3.up * .5f, face, camPos, dm, vol); }
                for (int i = 0; i < W.ev.Length; i++)
                {
                    var v = W.ev[i]; var E = W.echo[i]; if (!v || E == null) continue;
                    float tgt = E.live ? E.gT : 0; if (E.live && Mathf.Abs(E.len - E.cur) > 6) tgt = 0;              // caminho mudou muito: apaga, troca o atraso, acende
                    E.g = Mathf.MoveTowards(E.g, tgt, dt * 4 * Mathf.Max(.05f, Mathf.Max(tgt, E.g)));
                    if (E.g < .002f && E.live) E.cur = E.len;
                    var to = E.q - camPos; float dq = Mathf.Max(.5f, to.magnitude); var dir = to / dq;
                    v.gain = E.g * Mathf.Lerp(1, .6f, occ); v.delay = LEAD + E.cur / SOUND_SPEED; v.cutoff = Mathf.Min(Air(E.cur) * .75f, 9000);
                    v.pan = Vector3.Dot(dir, right); v.behind = Mathf.Clamp01(-Vector3.Dot(dir, fwd)) * .8f; v.width = .85f;
                }
            }
            else foreach (var v in W.ev) if (v) v.gain = 0;
            // reverb da rua: de perto quase seco, de longe o reverberado domina; prédio na frente: o direto cai, o reverb quase não
            if (W.rev)
            {
                W.rev.delay = LEAD + dm / SOUND_SPEED + .02f; W.rev.cutoff = Mathf.Min(Air(dm) * .6f, 5200);
                W.rev.send = (full ? 1 : .6f) * vol * .36f * Mathf.Sqrt(12 / (12 + dm)) * Fade(dm) * (inCar ? .2f : 1) * Mathf.Lerp(1, .8f, occ);
                if (walls >= 0) W.rev.rt60 = Mathf.Lerp(1.0f, 2.1f, Mathf.Clamp01(walls / 6f));               // rua cercada de prédio reverbera mais
                else if (!full) W.rev.rt60 = 1.4f;
            }
        }
        // fontes-imagem: 16 raios em volta da caixa; parede de pé até 110 m -> reflexo especular até o ouvinte (visível dos dois lados)
        static int FindEchoes(Wall W, Vector3 S, Vector3 face, Vector3 L, float dDirect, float vol)
        {
            int mask = 1 << FBWorld.LMap, nWalls = 0; var cand = new List<(Vector3 q, float len, float g)>();
            for (int i = 0; i < 16; i++)
            {
                var dir = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                if (!Physics.Raycast(S, dir, out var h, 110, mask, QueryTriggerInteraction.Ignore) || Mathf.Abs(h.normal.y) > .35f) continue;
                nWalls++; var n = h.normal; n.y = 0; n.Normalize(); var I = S - 2 * Vector3.Dot(S - h.point, n) * n;
                float den = Vector3.Dot(L - I, n); if (Mathf.Abs(den) < 1e-3f) continue; float t = Vector3.Dot(h.point - I, n) / den; if (t <= .02f || t >= .98f) continue;
                var Q = I + (L - I) * t; var sq = Q - S; float dS = sq.magnitude; if (dS < .5f) continue;
                if (Physics.Raycast(S, sq / dS, out var h2, dS + .8f, mask, QueryTriggerInteraction.Ignore) && (h2.point - Q).sqrMagnitude > 2.5f * 2.5f) continue;   // reflexo fora da parede
                var lq = Q - L; float dL = lq.magnitude; if (dL < .5f || Physics.Raycast(L, lq / dL, dL - .7f, mask, QueryTriggerInteraction.Ignore)) continue;        // ouvinte não vê o ponto
                float len = dS + dL; if (len - dDirect < 5 || len > 320) continue;
                float dirS = Mathf.Lerp(1, .5f, Mathf.InverseLerp(60, 160, Vector3.Angle(face, sq)));
                cand.Add((Q, len, vol * .45f * Dist(len) * Fade(len) * dirS));
            }
            cand.Sort((a, b) => b.g.CompareTo(a.g));
            foreach (var E in W.echo) if (E != null) E.live = false;
            foreach (var (q, len, g) in cand.Take(W.echo.Length))
            {   // mantém o eco no mesmo canal se for a mesma parede (sem trocar o atraso à toa)
                Echo best = null; float bd = 16;
                foreach (var E in W.echo) if (E != null && !E.live && (E.q - q).sqrMagnitude < bd) { bd = (E.q - q).sqrMagnitude; best = E; }
                best ??= W.echo.FirstOrDefault(E => E != null && !E.live); if (best == null) break;
                if (best.g < .002f) best.cur = len; best.q = q; best.len = len; best.gT = g; best.live = true;
            }
            return nWalls;
        }
        // WebGL: AudioSource 3D com a curva e o volume (sem filtros)
        static void SimpleAudio(Wall W, FBCar car, Transform node, Vector3 pos, Vector3 camPos, bool inCar, float dt)
        {
            float d = Vector3.Distance(pos, camPos); var face = node ? node.forward : -car.transform.forward;
            float ang = Vector3.Angle(face, camPos - pos), cone = Mathf.Lerp(1, .5f, Mathf.InverseLerp(60, 160, ang));
            W.src.volume = Mathf.Min(1, W.vol * W.vol * 1.4f * Fade(d) * (inCar ? 1 : cone) * Mathf.Lerp(1, .5f, W.occK) * FBAudio.VehVol);
        }
        public static string Diag(FBCar car)
        {
            var W = State(car); if (W == null) return "sem estado"; var ic = System.Globalization.CultureInfo.InvariantCulture;
            string V(FBAcVoice v) => v ? $"g{v.gain.ToString("0.000", ic)} d{(v.delay - LEAD).ToString("0.000", ic)}s c{v.cutoff:0} p{v.pan.ToString("0.00", ic)}" : "-";
            return $"tocando {W.playing && W.src && W.src.isPlaying} · tap {(W.tap ? W.tap.level.ToString("0.00", ic) : "-")} · colunas [{V(W.dv[0])}] [{V(W.dv[1])}] · ecos {string.Join(" ", W.ev.Select((v, i) => W.echo[i] != null && W.echo[i].live ? $"[{V(v)} L{W.echo[i].len:0}m]" : "[-]"))} · reverb {(W.rev ? $"s{W.rev.send.ToString("0.000", ic)} rt{W.rev.rt60.ToString("0.0", ic)}" : "-")} · prédio {W.occK:0.0}";
        }
        public static bool Occluded(Vector3 from, Vector3 to)
        {
            var dir = to - from; float L = dir.magnitude - 1.8f; if (L <= 0) return false;
            return Physics.Raycast(from, dir.normalized, L, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore);
        }
        static float Hue(Color c) { Color.RGBToHSV(c, out var h, out _, out _); return h; }
        public static Color Hsl(float h, float s, float l) { float v = l + s * Mathf.Min(l, 1 - l), sv = v == 0 ? 0 : 2 * (1 - l / v); return Color.HSVToRGB(h, sv, v); }
    }

    // ---- CENTRAL MULTIMÍDIA (tecla K): tocando agora + fila / biblioteca / paredão (multimidia.js)
    public static class FBMultimidia
    {
        public static bool Open; static GameObject go; static Transform body; static string tab = "fila"; static FBCar car; static Text nowT, artT, timeT, stT; static Image prog; static Slider volS;
        static readonly List<string> q = new(); static int qi; static bool shuf; static string rep = "all"; static float vol = .85f; static int page;
        static bool inited;
        static void Init()
        {
            if (inited) return; inited = true; Load();
            FBParedao.OnEnded = c => { if (c == Car()) Next(true); };
            FBParedao.OnNeedTrack = c => { var t = FBMediaLib.Find(q.Count > 0 ? q[Mathf.Clamp(qi, 0, q.Count - 1)] : null) ?? FBMediaLib.lib.FirstOrDefault(x => x.folder != "Recebidas") ?? FBMediaLib.lib.FirstOrDefault(); if (t != null) PlayTrack(t, c); };
            FBParedao.Changed += () => { if (Open) Render(); };
            FBMediaLib.Refresh();
        }
        static void Load()
        {
            q.Clear(); var s = PlayerPrefs.GetString("fb.midia.v1", ""); if (string.IsNullOrEmpty(s)) return;
            var o = Json.Parse(s) as Dictionary<string, object>; foreach (var x in Json.L(o, "q") ?? new List<object>()) q.Add(x as string);
            qi = Json.I(o, "i"); shuf = Json.B(o, "shuf"); rep = Json.S(o, "rep") ?? "all"; vol = Json.F(o, "vol", .85f); tab = Json.S(o, "tab") ?? "fila";
        }
        static void Save() { PlayerPrefs.SetString("fb.midia.v1", "{\"q\":[" + string.Join(",", q.Select(x => "\"" + x + "\"")) + $"],\"i\":{qi},\"shuf\":{(shuf ? "true" : "false")},\"rep\":\"{rep}\",\"vol\":{vol.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"tab\":\"{tab}\"" + "}"); }
        static FBCar Car() { var me = FBMatch.Player; return me ? FBParedao.MineCar(me.Pos) ?? FBVehicles.CarOf(me) : null; }
        public static bool PlayTrack(FBTrack t, FBCar c = null)
        {
            c ??= Car(); if (!c || !FBParedao.Has(c)) { FBHUD.I?.Center("ENTRE NUM CARRO COM PAREDÃO", 1.6f); return false; }
            if (!FBParedao.CanControl(c)) { FBHUD.I?.Center("ESTE PAREDÃO É DE " + (FBParedao.OwnerName(c) ?? "OUTRO JOGADOR"), 1.6f); return false; }
            FBParedao.SetVol(c, vol); return FBParedao.Play(c, t);
        }
        static void PlayQueue(int i) { if (q.Count == 0) return; qi = ((i % q.Count) + q.Count) % q.Count; Save(); var t = FBMediaLib.Find(q[qi]); if (t != null) PlayTrack(t); Render(); }
        static void Next(bool auto)
        {
            if (q.Count == 0) { var W = FBParedao.State(Car()); if (auto && W?.tr != null && rep != "off") { var t = FBMediaLib.Find(W.tr.h); if (t != null) PlayTrack(t); } return; }
            if (auto && rep == "one") { PlayQueue(qi); return; }
            if (shuf && q.Count > 1) { int j; do j = UnityEngine.Random.Range(0, q.Count); while (j == qi); PlayQueue(j); return; }
            if (auto && rep == "off" && qi == q.Count - 1) { Render(); return; }
            PlayQueue(qi + 1);
        }
        static void Prev() { var c = Car(); if (FBParedao.Position(c) > 4) { FBParedao.Seek(c, 0); return; } PlayQueue(qi - 1); }
        public static void Toggle()
        {
            Init();
            if (Open) { UnityEngine.Object.Destroy(go); Open = false; if (!FBInput.Touch) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } return; }
            var me = FBMatch.Player; car = Car(); if (!car) { var n = FBVehicles.Nearest(me ? me.Pos : Vector3.zero, 6); if (n && FBParedao.Has(n)) car = n; }
            if (!car || !FBParedao.Has(car)) { FBHUD.I?.Center("ENTRE NUM CARRO COM PAREDÃO (OU CHEGUE PERTO DO SEU)", 2); return; }
            FBMediaLib.Refresh(); q.RemoveAll(id => FBMediaLib.Find(id) == null); if (qi >= q.Count) qi = 0;
            var cv = FBUI.Canvas("Multimidia", 35); go = cv.gameObject; Open = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            go.AddComponent<Ticker>(); Render();
        }
        class Ticker : MonoBehaviour
        {
            float t;
            // [parceiro] v1 (thread de física): tempo de CPU do Update medido pelo FBTempo (desligado no jogo: só um teste de bool)
            static readonly int __fbt_Update = FBTempo.Id("FBMultimidia.Ticker.Update");
            void Update() { if (!FBTempo.On) { Update__fbt(); return; } long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0); }
            void Update__fbt() { if ((t += Time.unscaledDeltaTime) > .25f) { t = 0; Tick(); } }
        }
        static string Mmss(float s) { int k = Mathf.Max(0, Mathf.FloorToInt(s)); return $"{k / 60}:{k % 60:00}"; }
        static void Tick()
        {
            if (!Open || !car) return; var W = FBParedao.State(car); float pos = FBParedao.Position(car), dur = FBParedao.Duration(car);
            if (timeT) timeT.text = $"{Mmss(pos)} / {Mmss(dur)}"; if (prog) prog.rectTransform.anchorMax = new Vector2(dur > 0 ? Mathf.Clamp01(pos / dur) : 0, 1);
            if (stT) { var b = FBParedao.Buffering(car); stT.text = b != "" ? b : W == null || !W.on ? "DESLIGADO" : W.playing ? "TOCANDO NO PAREDÃO" : "PAUSADO"; }
        }
        static void Render()
        {
            if (!Open || !go) return; var cv = go.transform; foreach (Transform c in cv) UnityEngine.Object.Destroy(c.gameObject);
            var W = FBParedao.State(car); var p = FBUI.Panel(cv, "central", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(940, 520), new Color(.07f, .08f, .1f, .95f)).transform;
            FBUI.Panel(p, "topo", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), new Vector2(0, 2), FBUI.Gold);
            FBUI.Label(p, "CENTRAL MULTIMÍDIA", 20, new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, -30), new Vector2(320, 30), TextAnchor.MiddleLeft, true);
            string owner = FBParedao.OwnerName(car);
            FBUI.Label(p, (car.id + (owner != null ? " · PAREDÃO DE " + owner : "")).ToUpper(), 12, new Vector2(0, 1), new Vector2(0, 1), new Vector2(520, -30), new Vector2(420, 24), TextAnchor.MiddleLeft, false, FBUI.Mut);
            FBUI.Button(p, "✕", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(40, 36), Toggle);
            // ---- tocando agora (esquerda)
            var t = W?.tr; float x0 = -235;
            var cov = FBUI.Panel(p, "capa", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 - 110, 120), new Vector2(150, 150), Color.HSVToRGB(Hue(t?.h) / 360f, .7f, .35f));
            FBUI.Label(cov.transform, "♪", 64, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 150), TextAnchor.MiddleCenter, false, new Color(1, 1, 1, .7f));
            FBUI.Label(p, "TOCANDO AGORA", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 + 85, 180), new Vector2(240, 18), TextAnchor.MiddleLeft, false, FBUI.Gold);
            nowT = FBUI.Label(p, t?.n ?? "Nenhuma faixa", 20, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 + 85, 145), new Vector2(240, 50), TextAnchor.UpperLeft, true);
            artT = FBUI.Label(p, t?.a ?? "", 14, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 + 85, 105), new Vector2(240, 22), TextAnchor.MiddleLeft, false, FBUI.Mut);
            stT = FBUI.Label(p, "", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 + 85, 75), new Vector2(240, 18), TextAnchor.MiddleLeft, false, new Color(.37f, .88f, .54f));
            var bar = FBUI.Panel(p, "barra", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0, 10), new Vector2(390, 6), new Color(1, 1, 1, .12f));
            prog = FBUI.Panel(bar.transform, "fi", Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, FBUI.Gold); prog.rectTransform.offsetMin = prog.rectTransform.offsetMax = Vector2.zero;
            var seek = bar.gameObject.AddComponent<Button>(); seek.onClick.AddListener(() => { var rt = bar.rectTransform; RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, UnityEngine.InputSystem.Pointer.current?.position.ReadValue() ?? Vector2.zero, null, out var lp); FBParedao.Seek(car, Mathf.Clamp01(lp.x / rt.rect.width + .5f) * FBParedao.Duration(car)); });
            timeT = FBUI.Label(p, "", 12, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0, -8), new Vector2(390, 18), TextAnchor.MiddleCenter, false, FBUI.Mut);
            float cy = -55;
            FBUI.Button(p, shuf ? "ALEAT ●" : "ALEAT", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 - 160, cy), new Vector2(70, 40), () => { shuf = !shuf; Save(); Render(); });
            FBUI.Button(p, "⏮", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 - 80, cy), new Vector2(56, 44), Prev);
            FBUI.Button(p, W != null && W.playing ? "❚❚" : "▶", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0, cy), new Vector2(72, 60), () =>
            {
                var w = FBParedao.State(car); if (w?.tr == null) { if (q.Count > 0) PlayQueue(qi); else FBParedao.OnNeedTrack?.Invoke(car); return; }
                if (!w.on) FBParedao.SetOn(car, true); FBParedao.Pause(car, w.playing);
            }, true);
            FBUI.Button(p, "⏭", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 + 80, cy), new Vector2(56, 44), () => Next(false));
            FBUI.Button(p, rep == "off" ? "REPETIR" : rep == "one" ? "REP 1" : "REP ●", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 + 160, cy), new Vector2(70, 40), () => { rep = rep == "all" ? "one" : rep == "one" ? "off" : "all"; Save(); Render(); });
            FBUI.Label(p, "VOLUME", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x0 - 150, -115), new Vector2(80, 20), TextAnchor.MiddleLeft, false, FBUI.Mut);
            volS = Slider(p, new Vector2(x0 + 30, -115), 280, W?.vol ?? vol, v => { vol = v; Save(); FBParedao.SetVol(car, v); });
            // ---- abas (direita)
            float xr = 230; int ti = 0;
            foreach (var (k, nm) in new[] { ("fila", "FILA"), ("lib", "BIBLIOTECA"), ("par", "PAREDÃO") })
            { var kk = k; FBUI.Button(p, nm, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(xr - 145 + ti++ * 145, 200), new Vector2(138, 38), () => { tab = kk; page = 0; Save(); Render(); }, tab == k); }
            body = FBUI.Rect(p, "corpo", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(xr, -30), new Vector2(440, 410));
            if (tab == "par") RenderWall(W); else RenderList(tab == "fila" ? q.Select(FBMediaLib.Find).Where(x => x != null).ToList() : FBMediaLib.lib, tab == "fila");
            Tick();
        }
        static void RenderList(List<FBTrack> list, bool fila)
        {
            const int PER = 7; int pages = Mathf.Max(1, (list.Count + PER - 1) / PER); page = Mathf.Clamp(page, 0, pages - 1);
            if (list.Count == 0)
            {
                FBUI.Label(body, fila ? "FILA VAZIA — ESCOLHA FAIXAS NA BIBLIOTECA" : "NENHUMA MÚSICA NA BIBLIOTECA\nCOLOQUE MP3/OGG/WAV EM:\n" + FBMediaLib.Dir, 13, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(420, 140), TextAnchor.MiddleCenter, false, FBUI.Mut);
                if (!fila) FBUI.Button(body, "ABRIR PASTA DE MÚSICAS", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -60), new Vector2(260, 42), OpenFolder, true);
                return;
            }
            var cur = FBParedao.State(car)?.tr?.h;
            for (int i = page * PER, r = 0; i < Mathf.Min(list.Count, (page + 1) * PER); i++, r++)
            {
                var t = list[i]; int idx = i; float y = 170 - r * 50;
                var row = FBUI.Panel(body, "faixa", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(436, 46), t.h == cur ? new Color(.96f, .67f, .09f, .16f) : new Color(1, 1, 1, .035f));
                FBUI.Label(row.transform, t.n ?? "?", 13, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(150, 8), new Vector2(280, 20), TextAnchor.MiddleLeft, false, t.h == cur ? FBUI.Gold : Color.white);
                FBUI.Label(row.transform, (t.a ?? "") + (t.folder == "Recebidas" ? " · RECEBIDA" : ""), 11, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(150, -11), new Vector2(280, 18), TextAnchor.MiddleLeft, false, FBUI.Mut);
                var b = row.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => { if (fila) PlayQueue(idx); else { if (!q.Contains(t.h)) q.Add(t.h); qi = q.IndexOf(t.h); Save(); PlayTrack(t); Render(); } });
                if (fila) FBUI.Button(row.transform, "✕", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-24, 0), new Vector2(36, 32), () => { q.RemoveAt(idx); if (qi >= q.Count) qi = 0; Save(); Render(); });
                else FBUI.Button(row.transform, "+", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-24, 0), new Vector2(36, 32), () => { if (!q.Contains(t.h)) q.Add(t.h); Save(); FBHUD.I?.Center("NA FILA", 1); });
            }
            if (pages > 1)
            {
                FBUI.Button(body, "◀", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-120, -190), new Vector2(60, 34), () => { page--; Render(); });
                FBUI.Label(body, $"{page + 1}/{pages}", 13, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -190), new Vector2(120, 30), TextAnchor.MiddleCenter, false, FBUI.Mut);
                FBUI.Button(body, "▶", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(120, -190), new Vector2(60, 34), () => { page++; Render(); });
            }
            if (fila && q.Count > 0) FBUI.Button(body, "LIMPAR", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(180, -190), new Vector2(80, 30), () => { q.Clear(); qi = 0; Save(); Render(); });
            if (!fila) FBUI.Button(body, "TUDO NA FILA", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-175, -190), new Vector2(110, 30), () => { q.Clear(); q.AddRange(list.Select(x => x.h)); qi = 0; Save(); PlayQueue(0); });
        }
        static void RenderWall(FBParedao.Wall W)
        {
            bool on = W != null && W.on;
            FBUI.Button(body, on ? "PAREDÃO LIGADO" : "PAREDÃO DESLIGADO", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 170), new Vector2(420, 50), () => FBParedao.Toggle(car), on);
            FBUI.Label(body, "PORTA-MALAS / CAÇAMBA", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-110, 120), new Vector2(200, 18), TextAnchor.MiddleLeft, false, FBUI.Mut);
            bool tk = W?.trunk ?? true;
            FBUI.Button(body, "ABERTO", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-150, 88), new Vector2(120, 34), () => FBParedao.SetTrunk(car, true), tk);
            FBUI.Button(body, "FECHADO", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-25, 88), new Vector2(120, 34), () => FBParedao.SetTrunk(car, false), !tk);
            FBUI.Label(body, "LED", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-110, 50), new Vector2(200, 18), TextAnchor.MiddleLeft, false, FBUI.Mut);
            string[] ln = { "DESLIG.", "COR", "RGB", "DEGRADÊ", "SINC" }; var lm = W?.ledM ?? "sinc";
            for (int i = 0; i < 5; i++) { var m = FBParedao.LED_MODES[i]; FBUI.Button(body, ln[i], new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-172 + i * 86, 18), new Vector2(82, 34), () => FBParedao.SetLed(car, m), lm == m); }
            Color[] cols = { new(1, .165f, .427f), new(.96f, .67f, .09f), new(.2f, .8f, 1), new(.3f, 1, .4f), new(.6f, .3f, 1), Color.white };
            for (int i = 0; i < cols.Length; i++) { var c = cols[i]; var b = FBUI.Button(body, "", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-175 + i * 46, -24), new Vector2(38, 30), () => FBParedao.SetLed(car, null, c)); b.GetComponent<Image>().color = c; }
            FBUI.Label(body, "VU", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-110, -62), new Vector2(200, 18), TextAnchor.MiddleLeft, false, FBUI.Mut);
            string[] vn = { "DESLIG.", "RGB", "SINC" }; var vm = W?.vu ?? "sinc";
            for (int i = 0; i < 3; i++) { var m = FBParedao.VU_MODES[i]; FBUI.Button(body, vn[i], new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-145 + i * 100, -94), new Vector2(94, 34), () => FBParedao.SetVu(car, m), vm == m); }
            FBUI.Label(body, "O som continua tocando quando você sai do carro. Online, quem estiver perto recebe a faixa do seu aparelho (até 30 MB).", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -150), new Vector2(420, 50), TextAnchor.MiddleCenter, false, FBUI.Mut);
        }
        static void OpenFolder() { try { System.IO.Directory.CreateDirectory(FBMediaLib.Dir); Application.OpenURL(new Uri(FBMediaLib.Dir).AbsoluteUri); } catch { } }
        static int Hue(string h) { int v = 0; foreach (var c in h ?? "x") v = (v * 31 + c) % 360; return v; }
        static Slider Slider(Transform p, Vector2 pos, float w, float val, Action<float> on)
        {
            var root = FBUI.Rect(p, "slider", new Vector2(.5f, .5f), new Vector2(.5f, .5f), pos, new Vector2(w, 20));
            var bg = FBUI.Panel(root, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 1, 1, .14f)); bg.rectTransform.offsetMin = new Vector2(0, 7); bg.rectTransform.offsetMax = new Vector2(0, -7);
            var fa = FBUI.Rect(root, "fa", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); fa.offsetMin = new Vector2(0, 7); fa.offsetMax = new Vector2(-10, -7);
            var fill = FBUI.Panel(fa, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FBUI.Gold); fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var ha = FBUI.Rect(root, "ha", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); ha.offsetMin = new Vector2(10, 0); ha.offsetMax = new Vector2(-10, 0);
            var hd = FBUI.Panel(ha, "kn", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(18, 18), Color.white); hd.sprite = FBUI.CircleSprite;
            var s = root.gameObject.AddComponent<Slider>(); s.fillRect = fill.rectTransform; s.handleRect = hd.rectTransform; s.targetGraphic = hd; s.minValue = 0; s.maxValue = 1; s.value = val;
            s.onValueChanged.AddListener(v => on(v)); return s;
        }
    }
}
