// Auditoria do referencial das armas (rodada 42): a arma entra na mão com UM deslocamento fixo do Complexo (tpsWeaponProxyLocal*), o
// que só dá certo se todo modelo tiver o punho na origem e o cano no mesmo eixo. Mede, no espaço da raiz de cada arma: direção do cano
// (FBCharacter.GunAxes), boca, SOCKET_Grip e a caixa dos renderizadores; compara com a AK (que segura certo) -> unity/logs/armas_referencial.tsv
// Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBWeaponAudit.Batch
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBWeaponAudit
    {
        public static void Batch()
        {
            int code = 0;
            try { Run(); } catch (System.Exception e) { Debug.LogError("[FreeBala][armas] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        static string V(Vector3 v) => $"{v.x:0.000}\t{v.y:0.000}\t{v.z:0.000}";
        public static void Run()
        {
            GameData.Load(); var reg = FBRegistry.I;
            var sb = new StringBuilder("id\tclasse\tcano_x\tcano_y\tcano_z\tboca_x\tboca_y\tboca_z\tpunho_x\tpunho_y\tpunho_z\tcaixa_min_x\tcaixa_min_y\tcaixa_min_z\tcaixa_max_x\tcaixa_max_y\tcaixa_max_z\tsoquetes\n");
            foreach (var d in GameData.Weapons.Values.OrderBy(w => w.cls).ThenBy(w => w.id))
            {
                var pf = reg.Weapon(d.id); if (!pf) continue;
                var go = (GameObject)Object.Instantiate(pf); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
                FBWeaponModel.Apply(go, d, FBWeaponModel.Default(d));
                var r = go.transform; Vector3 bar = Vector3.forward, muz = Vector3.zero;
                if (FBCharacter.GunAxes(go, out var sp, out var b, out _)) { bar = r.InverseTransformDirection(sp.TransformDirection(b)).normalized; }
                Transform best = null; float bm = 0; foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("SOCKET_Muzzle") && t.localPosition.magnitude > bm) { bm = t.localPosition.magnitude; best = t; }
                if (best) muz = r.InverseTransformPoint(best.position);
                var grip = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("SOCKET_Grip"));
                var gp = grip ? r.InverseTransformPoint(grip.position) : new Vector3(float.NaN, float.NaN, float.NaN);
                var bb = new Bounds(); bool any = false;
                foreach (var rd in go.GetComponentsInChildren<Renderer>(false)) { if (!rd.enabled || !rd.gameObject.activeInHierarchy) continue; var b2 = rd.bounds; if (!any) { bb = b2; any = true; } else bb.Encapsulate(b2); }
                var socks = string.Join(",", go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("SOCKET_")).Select(t => t.name.Replace("SOCKET_", "")).Distinct());
                sb.Append($"{d.id}\t{d.cls}\t{V(bar)}\t{V(muz)}\t{V(gp)}\t{V(bb.min)}\t{V(bb.max)}\t{socks}\n");
                Object.DestroyImmediate(go);
            }
            var outp = System.IO.Path.GetFullPath("../logs/armas_referencial.tsv"); System.IO.File.WriteAllText(outp, sb.ToString());
            Debug.Log("[FreeBala][armas] " + outp);
        }
    }
}
