// Ferramenta de teste (pode apagar depois; fica só no Editor). Vigia as fontes de som do FBAudio durante os testes
// para achar quem destrói as fontes 3D (erro "AudioSource has been destroyed" em FBAudio.PlayAt).
// Escreve no log quando o FB_Audio some, quando fontes do grupo morrem e quando alguma fonte sai de dentro do FB_Audio.
// Fica fora da pasta Editor porque o Unity não deixa pôr componente de script de editor; o #if tira do jogo final.
// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Scripts/FBTeste/FBVigiaSom.cs (lá no Assembly-CSharp, fora de namespace);
// aqui fica no FreeBala.Runtime, dentro do namespace FreeBala.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBVigiaSom : MonoBehaviour
    {
        FieldInfo fRoot, fPool3, fPool2;
        int mortos3 = -1, total3 = -1, mortos2 = -1;
        string cena = "";
        string estadoRaiz = "";
        readonly Dictionary<int, string> pais = new Dictionary<int, string>();

        static void Log(string s) => Debug.Log("[FreeBala][vigia-som] " + s);

        static string Caminho(Transform t)
        {
            var partes = new List<string>();
            for (int i = 0; t && i < 6; i++, t = t.parent) partes.Insert(0, t.name);
            return partes.Count == 0 ? "(nenhum)" : string.Join("/", partes);
        }

        void Awake()
        {
            var t = typeof(FBAudio);
            const BindingFlags f = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            fRoot = t.GetField("root", f);
            fPool3 = t.GetField("pool3", f);
            fPool2 = t.GetField("pool2", f);
            if (fRoot == null || fPool3 == null) Log("campos do FBAudio não encontrados");
        }

        void LateUpdate()
        {
            if (fRoot == null || fPool3 == null) return;
            var cenaAgora = SceneManager.GetActiveScene().name;
            if (cenaAgora != cena) { cena = cenaAgora; Log($"t={Time.time:0.0} cena ativa {cena}"); }
            var root = fRoot.GetValue(null) as Object;
            var estado = (bool)root ? "vivo" : ReferenceEquals(root, null) ? "não criado" : "DESTRUÍDO";
            if (estado != estadoRaiz) { Log($"t={Time.time:0.0} quadro {Time.frameCount}: FB_Audio {estado}"); estadoRaiz = estado; }
            var pool3 = fPool3.GetValue(null) as List<AudioSource>;
            var pool2 = fPool2?.GetValue(null) as List<AudioSource>;
            if (pool3 == null) return;
            int m3 = pool3.Count(s => !s), m2 = pool2?.Count(s => !s) ?? 0;
            if (m3 != mortos3 || pool3.Count != total3 || m2 != mortos2)
            {
                Log($"t={Time.time:0.0} quadro {Time.frameCount}: fontes 3D {pool3.Count} ({m3} destruídas) · fontes 2D {pool2?.Count ?? 0} ({m2} destruídas) · cena {cena}");
                mortos3 = m3; total3 = pool3.Count; mortos2 = m2;
            }
            for (int i = 0; i < pool3.Count; i++)
            {
                var s = pool3[i];
                if (!s) continue;
                var pai = Caminho(s.transform.parent);
                if (pais.TryGetValue(i, out var velho) && velho != pai)
                    Log($"t={Time.time:0.0} fonte 3D nº{i} ({s.name}) mudou de pai: {velho} -> {pai}");
                pais[i] = pai;
            }
        }
    }
}
#endif
