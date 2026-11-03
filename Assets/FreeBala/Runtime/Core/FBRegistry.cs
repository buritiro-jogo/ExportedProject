// Índice dos assets importados (preenchido pelo FreeBala > Importar tudo). Personagens, armas, veículos, módulos de som, aros, interiores e
// acessórios ficam em Resources e são carregados SOB DEMANDA pelo nome (com cache): o registro guarda só os ids — carregar tudo de uma vez
// (170 personagens, 58 armas, 63 veículos) estourava a memória do celular/WebGL. Os clipes (686) e os controladores ficam referenciados direto.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    [CreateAssetMenu(menuName = "FreeBala/Registro")]
    public class FBRegistry : ScriptableObject
    {
        public const string RChars = "FBPrefabs/Chars/", RImp = "FBImported/";
        public List<string> charIds = new(), weaponIds = new(), vehicleIds = new(), somIds = new(), wheelIds = new(), intIds = new(), accIds = new(), partIds = new();
        public List<AnimationClip> clips = new();
        public List<Mesh> lodSrc = new(), lodDst = new();                                   // r44: malhas de veículo (motos MX) -> cópia com Mesh LOD (FBLod3.Veiculos)
        Dictionary<Mesh, Mesh> lodMap;
        public Mesh LodOf(Mesh m) { if (!m) return m; if (lodMap == null) { lodMap = new(); for (int i = 0; i < lodSrc.Count && i < lodDst.Count; i++) if (lodSrc[i] && lodDst[i]) lodMap[lodSrc[i]] = lodDst[i]; } return lodMap.TryGetValue(m, out var d) ? d : m; }
        public RuntimeAnimatorController mainController, pedestrianController; public GameObject fpArms; public AnimationClip birdClip;
        static FBRegistry inst; Dictionary<string, AnimationClip> k; readonly Dictionary<string, GameObject> cache = new(); readonly Dictionary<List<string>, HashSet<string>> sets = new();
        public static FBRegistry I { get { if (!inst) inst = Resources.Load<FBRegistry>("FBRegistry"); return inst; } }
        bool Has(List<string> l, string id) { if (id == null) return false; if (!sets.TryGetValue(l, out var s)) sets[l] = s = new HashSet<string>(l); return s.Contains(id); }
        GameObject Load(List<string> l, string dir, string id)
        {
            if (!Has(l, id)) return null; var path = dir + id;
            if (!cache.TryGetValue(path, out var g) || !g) { g = Resources.Load<GameObject>(path); cache[path] = g; if (!g) Debug.LogWarning("[FreeBala] não carregou " + path); }
            return g;
        }
        public GameObject Char(string id) => Load(charIds, RChars, id);
        public GameObject Weapon(string id) => Load(weaponIds, RImp + "Weapons/", id);
        public GameObject Vehicle(string id) => Load(vehicleIds, RImp + "Vehicles/", id);
        public GameObject Som(string id) => Load(somIds, RImp + "Som/", id);                    // módulos de som do REB (paredão)
        public GameObject Wheel(string id) => Load(wheelIds, RImp + "Wheels/", id);             // aros (wheels.json)
        public GameObject Interior(string id) => Load(intIds, RImp + "Int/", id);
        public GameObject Acc(string id) => Load(accIds, RImp + "Acc/", id);
        public GameObject Parts(string id) => Load(partIds, RImp + "Parts/", id);                // peças da oficina do REB por carro (bodykit, forros, engate, bandeiras)
        public bool HasChar(string id) => Has(charIds, id);
        public AnimationClip Clip(string id) { if (id == null) return null; if (k == null) { k = new Dictionary<string, AnimationClip>(); foreach (var o in clips) if (o && !k.ContainsKey(o.name)) k[o.name] = o; } if (k.TryGetValue(id, out var a) || k.TryGetValue(Safe(id), out a)) return a; return null; }
        public static string Safe(string n) { foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) n = n.Replace(ch, '_'); return n.Replace('|', '_').Replace(':', '_'); }   // mesmo nome de arquivo do importador
    }
}
