// Autoteste de ÁUDIO de dano (validação sem ouvir): FreeBala.exe -fbauto vila_jardim:tatico 90 -fbaudio
//   ou sozinho: FreeBala.exe -fbaudio [mapa:modo]   (carrega a partida, roda o roteiro e fecha)
// Numa partida carregada, congela os bots, põe dois bonecos (masculino c_felipe, feminino c_mia) na frente da câmera e aplica dano pelo
// FBCombat REAL (barramento → FBDamageVoice → diretor → reprodução), no roteiro obrigatório:
//   tiro masc/fem (leve, grave, cabeça, membro) · rajada automática · dano simultâneo (bala+explosão+queda) · queda · explosão ·
//   gás masc+fem com entrada e saída da zona · morte no gás · morte por tiro · passos (andar/correr em asfalto, grama e metal + pouso).
// Saídas em <pasta do executável>/autoteste/ (no editor: pasta do projeto/autoteste):
//   audio_log.csv    — uma linha por evento de dano e por decisão do diretor (tocou / interrompeu / roubou vaga / suprimida + motivo),
//                      com tempo, seção, personagem, sexo, tipo de dano, banco, clipe, volume, altura, intensidade, distância, vozes ativas;
//   audio_roteiro.wav — o áudio mixado que chega ao AudioListener (OnAudioFilterRead), 16 bits.
// O ambiente/música é parado durante o roteiro para o WAV ficar limpo (as vozes ainda saem com spatialização real da câmera).
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBVoiceAutoTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbaudio"); if (i < 0) return;
            var g = new GameObject("FB_AutoTesteAudio"); DontDestroyOnLoad(g); var t = g.AddComponent<FBVoiceAutoTest>();
            t.standalone = System.Array.IndexOf(a, "-fbauto") < 0;
            if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.job = a[i + 1];
        }
        bool standalone; string job = "vila_jardim:tatico";
        string dir, section = "inicio"; StreamWriter csv; WavRec rec; float t0;
        readonly Dictionary<string, int> played = new(), suppressed = new();
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        IEnumerator Start()
        {
            if (standalone)
            {
                var kv = job.Split(':'); GameData.Load(); FBSession.Load(); FBSession.map = kv[0]; FBSession.mode = kv.Length > 1 ? kv[1] : "tatico"; FBSession.online = false; FBSession.Save();
                var op = SceneManager.LoadSceneAsync("Map_" + FBSession.map); if (op == null) { Debug.Log("[FreeBala][audio] FALHA: cena Map_" + FBSession.map + " não existe"); Application.Quit(1); yield break; }
                while (!op.isDone) yield return null;
            }
            for (float w = 0; w < 120 && !(FBMatch.I && FBMatch.Player); w += Time.unscaledDeltaTime) yield return null;   // espera a partida montar (o -fbauto carrega)
            if (!(FBMatch.I && FBMatch.Player)) { Debug.Log("[FreeBala][audio] FALHA: partida não montou"); yield break; }
            for (float w = 0; w < 3; w += Time.unscaledDeltaTime) yield return null;
            yield return Roteiro();
            if (standalone) { Debug.Log("[FreeBala][audio] fim"); Application.Quit(0); }
        }

        const string MASC = "r_tutu", FEM = "r_elaine";
        IEnumerator Roteiro()
        {
            dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste"); Directory.CreateDirectory(dir);
            csv = new StreamWriter(Path.Combine(dir, "audio_log.csv"), false, new UTF8Encoding(true));
            csv.WriteLine("tempo_s,secao,linha,personagem,sexo,dano_tipo,dano,vida,banco,decisao,clipe,volume,pitch,intensidade,distancia_m,vozes_ativas");
            t0 = Time.time; FBVoice.trace = Row;
            var M = FBMatch.I; var P = FBMatch.Player; bool freeze = M.freeze, god = FBMatch.God; M.freeze = true; FBMatch.God = true;
            FBAudio.StopAllLoops(); FBAmbience.Stop();
            var lis = FindAnyObjectByType<AudioListener>(); if (lis) rec = lis.gameObject.AddComponent<WavRec>();
            Debug.Log($"[FreeBala][audio] roteiro iniciado · ouvinte {(lis ? lis.name : "NENHUM")} · {AudioSettings.outputSampleRate} Hz");

            // bonecos parados na frente da câmera (masc à esquerda, fem à direita, ~4 m)
            var cam = Camera.main ? Camera.main.transform : P.transform; var fw = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized; if (fw.sqrMagnitude < .1f) fw = P.transform.forward;
            var rt = Vector3.Cross(Vector3.up, fw); var basePos = P.Pos + fw * 4;
            var m = M.Spawn(MASC, "", P.team, Ground(basePos - rt * 1.4f), false); var f = M.Spawn(FEM, "", P.team, Ground(basePos + rt * 1.4f), false);   // (r41) c_felipe/c_mia saíram com os regionais
            if (!m || !f) { Debug.Log($"[FreeBala][audio] FALHA: bonecos {MASC}/{FEM} não nasceram"); yield break; }
            foreach (var c in new[] { m, f }) { c.transform.rotation = Quaternion.LookRotation(-fw); c.displayName = c == m ? "TESTE_MASC" : "TESTE_FEM"; }
            yield return Wait(1.5f);
            Debug.Log($"[FreeBala][audio] sexo dos bonecos: {MASC}={FBVoice.SexOf(m)} {FEM}={FBVoice.SexOf(f)}");

            foreach (var (c, tag) in new[] { (m, "masc"), (f, "fem") })
            {
                Sec("tiro_leve_" + tag); Ready(c); Shot(c, 8, "body"); yield return Wait(1.4f);
                Sec("tiro_grave_" + tag); Shot(c, 40, "body"); yield return Wait(1.6f);
                Sec("tiro_cabeca_" + tag); Ready(c); Shot(c, 22, "head", true); yield return Wait(1.4f);
                Sec("tiro_membro_" + tag); Ready(c); Shot(c, 6, "leg"); yield return Wait(1.4f);
                Sec("pouca_vida_" + tag); Ready(c, 30); Shot(c, 6, "body"); yield return Wait(1.8f);
            }
            Sec("rajada_auto_masc"); Ready(m);
            for (int k = 0; k < 12; k++) { if (m.hp > 10) Shot(m, 7, k % 4 == 3 ? "arm" : "body"); yield return Wait(.1f); }
            yield return Wait(1.5f);
            Sec("simultaneo_fem"); Ready(f);
            Shot(f, 10, "body"); FBCombat.DamageActor(f, 30, false, null, "explosion"); FBCombat.DamageActor(f, 5, false, null, "fall"); yield return Wait(1.8f);
            Sec("queda_masc"); Ready(m); FBCombat.DamageActor(m, 12, false, null, "fall"); yield return Wait(1.6f);
            Sec("queda_grave_masc"); FBCombat.DamageActor(m, 35, false, null, "fall"); yield return Wait(1.8f);
            Sec("explosao_fem"); Ready(f); FBCombat.DamageActor(f, 45, false, null, "explosion"); yield return Wait(2f);
            Sec("explosao_masc"); Ready(m); FBCombat.DamageActor(m, 45, false, null, "explosion"); yield return Wait(2f);

            // gás: os dois dentro da zona (tick de 1 s, como o FBZone), depois saem e a respiração deve decair e parar
            Sec("gas_entrada_masc_fem"); Ready(m); Ready(f);
            for (int k = 0; k < 15; k++) { foreach (var c in new[] { m, f }) FBCombat.DamageActor(c, 3, false, null, "zone"); yield return Wait(1); }
            Sec("gas_saida_masc_fem"); yield return Wait(11);
            Sec("morte_no_gas_fem"); Ready(f, 9);
            for (int k = 0; k < 12 && f.alive; k++) { FBCombat.DamageActor(f, 3, false, null, "zone"); yield return Wait(1); }
            yield return Wait(4);
            Sec("morte_tiro_masc"); Ready(m, 20); Shot(m, 30, "body"); yield return Wait(.5f); if (m.alive) Shot(m, 30, "body"); yield return Wait(4);   // modos com caído: 1º derruba, 2º mata

            var walker = M.Spawn(MASC, "", P.team, Ground(basePos + fw * 3), false);   // (r41) o "m" acabou de morrer no roteiro: boneco novo para os passos
            if (walker) { yield return Wait(1f); yield return Passos(walker, P, fw, rt); FBMatch.All.Remove(walker); Destroy(walker.gameObject); }
            Sec("fim"); FBVoice.trace = null; FBFootsteps.trace = null; M.freeze = freeze; FBMatch.God = god;
            foreach (var c in new[] { m, f }) if (c) { FBMatch.All.Remove(c); Destroy(c.gameObject); }
            string wav = "sem ouvinte";
            if (rec) { wav = rec.Save(Path.Combine(dir, "audio_roteiro.wav")); Destroy(rec); }
            csv.Close();
            string Sum(Dictionary<string, int> d) => string.Join(" ", d.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
            Debug.Log($"[FreeBala][audio] roteiro ok ({Time.time - t0:0} s) · tocadas: {Sum(played)} · suprimidas: {Sum(suppressed)} · wav: {wav} · csv: {Path.Combine(dir, "audio_log.csv")}");
        }

        // passos: três lajes temporárias (asfalto, grama, metal) na frente da câmera; o boneco anda (1,6 m/s) e corre (4,6 m/s) em cada uma.
        // A animação recebe a velocidade pelo FBCharacter.velocity; se os ossos não pisarem, o CSV mostra modo "passada" (por distância).
        IEnumerator Passos(FBCharacter c, FBCharacter P, Vector3 fw, Vector3 rt)
        {
            FBFootsteps.trace = r => csv.WriteLine(string.Join(",", T(), section, "passo", r.ch ? r.ch.skinId : "", "", r.mode, "", "", r.surf, r.foot == 0 ? "pe_esq" : "pe_dir", r.clip,
                r.vol.ToString("0.000", IC), "", r.speed.ToString("0.00", IC), r.dist.ToString("0.00", IC), ""));
            Ready(c); var slabs = new List<GameObject>();
            var pisos = new[] { ("Asfalto_rua_teste", 0), ("Grama_teste", 1), ("Chapa_teste", 4) };
            var o = Ground(P.Pos + fw * 7 - rt * 4); float y = o.y + .06f;
            for (int i = 0; i < pisos.Length; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = pisos[i].Item1; g.layer = FBWorld.LMap; slabs.Add(g);
                g.transform.position = new Vector3(o.x, y - .05f, o.z) + fw * (i * 2.4f) + rt * 4; g.transform.rotation = Quaternion.LookRotation(rt); g.transform.localScale = new Vector3(2, .1f, 17);   // comprida ao longo de rt g.AddComponent<FBSurface>().kind = pisos[i].Item2;
            }
            Physics.SyncTransforms();
            for (int i = 0; i < pisos.Length; i++)
                foreach (var (spd, tag) in new[] { (1.6f, "andar"), (4.6f, "correr") })
                {
                    Sec($"passos_{tag}_{pisos[i].Item1.Split('_')[0].ToLowerInvariant()}");
                    var a = new Vector3(o.x, y, o.z) + fw * (i * 2.4f); var b = a + rt * 16; c.transform.position = a; c.transform.rotation = Quaternion.LookRotation(rt);
                    yield return null; yield return null;
                    for (float d = 0; d < 16; d += spd * Time.deltaTime)
                    {
                        c.sprint = spd > 4; c.grounded = true; c.velocity = rt * spd; c.transform.position = Vector3.Lerp(a, b, d / 16); yield return null;
                    }
                    c.velocity = Vector3.zero; c.sprint = false; yield return Wait(.6f);
                }
            Sec("passos_pouso"); c.transform.position = new Vector3(o.x, y + 1.5f, o.z); c.grounded = false; yield return Wait(.6f);
            c.transform.position = new Vector3(o.x, y, o.z); c.grounded = true; yield return Wait(1);
            foreach (var g in slabs) Destroy(g);
        }

        void Sec(string s) { section = s; csv.WriteLine($"{T()},{s},secao,,,,,,,,,,,,,"); csv.Flush(); }
        string T() => (Time.time - t0).ToString("0.000", IC);
        static IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.deltaTime) yield return null; }
        static Vector3 Ground(Vector3 p) => Physics.Raycast(p + Vector3.up * 3, Vector3.down, out var h, 10, FBWorld.SolidMask) ? h.point : p;
        static void Ready(FBCharacter c, float hp = 100) { if (!c.alive || c.knocked) c.Revive(hp); c.hp = hp; c.armor = 0; c.helmet = false; c.protUntil = 0; c.knockInvT = 0; }
        static void Shot(FBCharacter c, float dmg, string part, bool head = false)
        {
            var pt = c.Pos + Vector3.up * (head ? 1.65f : part == "leg" ? .5f : 1.2f) * c.ScaleK;
            FBCombat.DamageActor(c, dmg, head, null, null, new FBHitInfo { valid = true, point = pt, normal = c.transform.forward, dir = -c.transform.forward, part = part, weaponId = "autoteste", dist = 4 });
        }
        void Row(FBVoice.Trace r)
        {
            string F(float v, string fmt = "0.00") => v.ToString(fmt, IC); string Q(string s) => string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "'") + "\"";
            bool voz = r.kind == "voz", tocou = voz && r.decision.StartsWith("tocou");
            csv.WriteLine(string.Join(",", T(), section, r.kind, r.ch ? r.ch.skinId : "", r.sex, Q(r.type), voz ? "" : F(r.amount, "0.0"), F(r.hp, "0"), r.bank, Q(r.decision), r.clip,
                tocou ? F(r.vol, "0.000") : "", tocou ? F(r.pitch, "0.000") : "", tocou ? F(r.intensity) : "", F(r.dist), r.voices.ToString()));
            if (voz) { var d = tocou ? played : suppressed; d.TryGetValue(r.bank, out var n); d[r.bank] = n + 1; }
        }

        // grava o que chega ao AudioListener (thread de áudio)
        class WavRec : MonoBehaviour
        {
            readonly List<float> buf = new(); readonly object lk = new(); int chans = 2; volatile bool on = true;
            void OnAudioFilterRead(float[] data, int channels) { if (!on) return; lock (lk) { chans = channels; if (buf.Count < 48000 * 2 * 240) buf.AddRange(data); } }
            public string Save(string path)
            {
                on = false; float[] d; int ch; lock (lk) { d = buf.ToArray(); ch = chans; }
                int sr = AudioSettings.outputSampleRate; float pk = 0; foreach (var x in d) pk = Mathf.Max(pk, Mathf.Abs(x));
                using (var w = new BinaryWriter(File.Create(path)))
                {
                    int bytes = d.Length * 2; w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch);
                    w.Write(sr); w.Write(sr * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16); w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                    foreach (var x in d) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(x * 32767), -32768, 32767));
                }
                return $"{path} ({d.Length / Mathf.Max(1, ch) / (float)sr:0.0} s, {ch} canais, pico {pk:0.00})";
            }
        }
    }
}
