// Teste do carregamento por setor do BV City (FBBVCitySetores): um alvo percorre uma rota a velocidade de estrada e o
// teste anota, a cada meio segundo, quantos setores estao vivos, quantos deviam estar e ainda nao chegaram, quantos
// ja deviam ter saido, e o pior quadro. Montado pelo Editor (FBBVCityPlanta.BatchStream) numa cena so com a planta.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FreeBala
{
    public class FBBVCityStreamTeste : MonoBehaviour
    {
        public FBBVCitySetores st;
        public float velocidade = 80f;          // m/s (288 km/h): mais que qualquer veiculo do jogo
        public string saida;
        public float limiteSeg = 300f;

        Vector3[] rota; int seg; float tIni, prox; int quadros;
        readonly StringBuilder sb = new StringBuilder();
        readonly Dictionary<int, float> faltaDesde = new Dictionary<int, float>();
        float piorQuadro, piorAtraso, percorrido; int maxVivos, maxFaltando, maxSobrando, amostras;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        void Start()
        {
            Application.targetFrameRate = 60; Application.runInBackground = true;
            // rota em coordenadas do corredor (x, y) -> Unity (-x, 2, y)
            var c = new[] { new Vector2(0, 0), new Vector2(2500, 0), new Vector2(2500, 1500), new Vector2(-1500, 1500), new Vector2(-1500, -1200), new Vector2(0, 0) };
            rota = new Vector3[c.Length];
            for (int i = 0; i < c.Length; i++) rota[i] = new Vector3(-c[i].x, 2f, c[i].y);
            transform.position = rota[0]; seg = 1; tIni = Time.realtimeSinceStartup;
            if (st) st.alvo = transform;
            sb.AppendLine("# t(s) x_corredor y_corredor vivos pedindo faltando sobrando");
        }

        void Update()
        {
            if (!st || rota == null) return;
            quadros++;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (quadros > 20) piorQuadro = Mathf.Max(piorQuadro, Time.unscaledDeltaTime);
            float anda = velocidade * dt;
            while (anda > 0f && seg < rota.Length)
            {
                var d = rota[seg] - transform.position; float L = d.magnitude;
                if (L <= anda) { transform.position = rota[seg]; anda -= L; percorrido += L; seg++; }
                else { transform.position += d / L * anda; percorrido += anda; anda = 0f; }
            }
            float t = Time.realtimeSinceStartup - tIni;
            if (t >= prox)
            {
                prox = t + 0.5f; amostras++;
                var p = transform.position; int faltando = 0, sobrando = 0;
                for (int i = 0; i < st.setores.Length; i++)
                {
                    float dist = FBBVCitySetores.Dist(st.setores[i], p); bool vivo = st.Vivo(i);
                    if (dist < st.raioEntra - 150f && !vivo)
                    {
                        faltando++;
                        if (!faltaDesde.ContainsKey(i)) faltaDesde[i] = t;
                        piorAtraso = Mathf.Max(piorAtraso, t - faltaDesde[i]);
                    }
                    else faltaDesde.Remove(i);
                    if (dist > st.raioSai + 1f && vivo) sobrando++;
                }
                maxVivos = Mathf.Max(maxVivos, st.Vivos); maxFaltando = Mathf.Max(maxFaltando, faltando); maxSobrando = Mathf.Max(maxSobrando, sobrando);
                sb.AppendLine(string.Format(IC, "{0:0.0} {1:0} {2:0} {3} {4} {5} {6}", t, -p.x, p.z, st.Vivos, st.Pedindo, faltando, sobrando));
            }
            if (seg >= rota.Length || t > limiteSeg) Fim(t);
        }

        void Fim(float t)
        {
            var r = new StringBuilder();
            r.AppendLine(string.Format(IC, "rota percorrida: {0:0} m em {1:0.0} s a {2:0} m/s ({3})", percorrido, t, velocidade, seg >= rota.Length ? "completa" : "INTERROMPIDA pelo limite de tempo"));
            r.AppendLine($"setores: {st.setores.Length}; entra a {st.raioEntra} m, sai a {st.raioSai} m");
            r.AppendLine($"cargas {st.Cargas}; descargas {st.Descargas}; maximo de setores vivos ao mesmo tempo {maxVivos}; vivos no fim {st.Vivos}");
            r.AppendLine(string.Format(IC, "setor a menos de {0:0} m ainda nao carregado: maximo {1} ao mesmo tempo; pior espera {2:0.0} s", st.raioEntra - 150f, maxFaltando, piorAtraso));
            r.AppendLine($"setor vivo alem do raio de saida: maximo {maxSobrando}");
            r.AppendLine(string.Format(IC, "pior quadro {0:0} ms; quadros {1}; amostras {2}", piorQuadro * 1000f, quadros, amostras));
            r.Append(sb);
            if (!string.IsNullOrEmpty(saida)) File.WriteAllText(saida, r.ToString());
            Debug.Log("[BVCity] teste de carregamento por setor terminou");
            enabled = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }
    }
}
