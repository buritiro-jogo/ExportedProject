// [mirante 03/11/2026] Opção C do Mirante (decidida pelo Ruan): varanda a 25 m jogável, acesso pelo elevador panorâmico.
// Monta na cena Map_orla, dentro de "MIR_Jogavel" (recriado a cada rodada, nada mais da cena é mexido):
//  · elevador (FBElevador) no poço de vidro da face do elevador, paradas no térreo (piso 0,2 m) e na varanda 2 (24,95 m);
//  · patamar dentro do núcleo a 25 m, ligando a porta do elevador à porta da varanda; piso e guarda-corpo da varanda com colisão;
//  · recorte do bloqueio da torre: o COL_Bloqueio_Torre_Mirante vira caixas em volta do hall, do poço e do patamar (fica desligado,
//    não é apagado);
//  · cobertura: 2 pontos (hall e varanda) no grupo "Pontos/cobertura_mirante".
// As medidas vêm do GLB da arena v7 (coordenadas glTF, ver Docs/mirante/ETAPA0_INVENTARIO_E_PLANO.md 6.3.1); a posição no Unity
// é achada pelo próprio COL_Bloqueio_Torre_Mirante (glTF x 16,55–42,55 / z 55,84–81,84), então vale qualquer que seja a conversão.
// Rodar: menu FreeBala/Mapas/Mirante: montar varanda e elevador, ou lab: "metodo FBMiranteJogavel.Montar" (com Map_orla aberta).
using System;
using System.Linq;
using FreeBala;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FBMiranteJogavel
{
    const string Cena = "Assets/FreeBala/Scenes/Map_orla.unity", Cabine = "Assets/BURITIRO_Mapas/mirante/modelos/MIR_Elevador_Cabine.fbx";
    const int LMapa = 8, LGatilho = 13;
    // caixa do bloqueio no glTF (min/max) e peças medidas
    static readonly Vector3 BloqMin = new(16.55f, 3f, 55.84f), BloqMax = new(42.55f, 116f, 81.84f);
    // núcleo: paredes em x 26,3 / 32,8 e z 62,9 / 73,6 (face do poço); varanda: grade em x 25,2 / 33,9 (1,1 m), z 62,9–73,1;
    // porta da varanda (A1_porta_aco_cinza) em z 68,4–69,3 nas duas paredes; vidro do poço em x 27,8–31,3, z 73,6
    const float VarY = 24.95f, ElevFaceZ = 73.6f, ElevX0 = 27.8f, ElevX1 = 31.3f, NucX0 = 26.3f, NucX1 = 32.8f, NucZ0 = 62.9f, NucZ1 = 73.1f,
                VarX0 = 25.2f, VarX1 = 33.9f, PortaZ0 = 68.4f, PortaZ1 = 69.3f, PocoZ0 = 71.4f, TerreoY = .2f, Teto = 27.5f;

    static Vector3 sx; static Vector3 off;   // conversão glTF → mundo: mundo = (sx.x*gx, gy, sx.z*gz) + off
    static Vector3 W(float gx, float gy, float gz) => new Vector3(sx.x * gx, gy, sx.z * gz) + off;

    [MenuItem("FreeBala/Mapas/Mirante: montar varanda e elevador")]
    public static void Montar()
    {
        var sc = EditorSceneManager.GetActiveScene();
        if (sc.path != Cena) sc = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        var bloq = sc.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).FirstOrDefault(c => c.name.StartsWith("COL_Bloqueio_Torre_Mirante"));
        if (!bloq) throw new Exception("COL_Bloqueio_Torre_Mirante não encontrado na Map_orla");
        bool on = bloq.enabled; bloq.enabled = true; var b = bloq.bounds; bloq.enabled = on;
        // descobre o espelhamento: o centro glTF (29,55; 68,84) cai no centro do bounds
        Vector3 gc = (BloqMin + BloqMax) * .5f;
        sx = new Vector3(Mathf.Sign(b.center.x) == Mathf.Sign(gc.x) ? 1 : -1, 1, Mathf.Sign(b.center.z) == Mathf.Sign(gc.z) ? 1 : -1);
        off = b.center - new Vector3(sx.x * gc.x, gc.y, sx.z * gc.z); off.y = b.min.y - BloqMin.y;
        Debug.Log($"[FreeBala][mirante] bloqueio {b.min}–{b.max} · espelho x {sx.x} z {sx.z} · deslocamento {off}");

        var velho = sc.GetRootGameObjects().FirstOrDefault(g => g.name == "MIR_Jogavel"); if (velho) UnityEngine.Object.DestroyImmediate(velho);
        var raiz = new GameObject("MIR_Jogavel"); SceneManager_Move(raiz, sc);

        // ---- bloqueio: desliga o inteiro (não apaga) e fecha tudo menos o hall (abaixo de 3 m), o poço, o patamar e a varanda 2
        bloq.enabled = false;
        var bl = new GameObject("Bloqueio_Recortado").transform; bl.SetParent(raiz.transform, false);
        Caixa(bl, "acima_patamar", BloqMin.x, Teto, BloqMin.z, BloqMax.x, BloqMax.y, BloqMax.z, LMapa);
        Caixa(bl, "oeste", BloqMin.x, BloqMin.y, BloqMin.z, VarX0, Teto, BloqMax.z, LMapa);
        Caixa(bl, "leste", VarX1, BloqMin.y, BloqMin.z, BloqMax.x, Teto, BloqMax.z, LMapa);
        Caixa(bl, "norte", VarX0, BloqMin.y, BloqMin.z, VarX1, Teto, NucZ0, LMapa);
        Caixa(bl, "sul", VarX0, BloqMin.y, ElevFaceZ + .1f, VarX1, Teto, BloqMax.z, LMapa);
        Caixa(bl, "sob_varanda_o", VarX0, BloqMin.y, NucZ0, NucX0, VarY - .25f, ElevFaceZ + .1f, LMapa);
        Caixa(bl, "sob_varanda_l", NucX1, BloqMin.y, NucZ0, VarX1, VarY - .25f, ElevFaceZ + .1f, LMapa);
        Caixa(bl, "nucleo_baixo", NucX0, BloqMin.y, NucZ0, NucX1, VarY - .3f, PocoZ0, LMapa);
        Caixa(bl, "poco_oeste", NucX0, BloqMin.y, PocoZ0, ElevX0 - .25f, Teto, ElevFaceZ + .1f, LMapa);
        Caixa(bl, "poco_leste", ElevX1 + .25f, BloqMin.y, PocoZ0, NucX1, Teto, ElevFaceZ + .1f, LMapa);
        Caixa(bl, "fim_varanda_s_o", VarX0, VarY, NucZ1, NucX0, Teto, ElevFaceZ + .1f, LMapa);
        Caixa(bl, "fim_varanda_s_l", NucX1, VarY, NucZ1, VarX1, Teto, ElevFaceZ + .1f, LMapa);

        // ---- varanda 2 (1,1 m em volta do núcleo, lados oeste e leste) com guarda-corpo de 1,10 m
        var va = new GameObject("Varanda_25m").transform; va.SetParent(raiz.transform, false);
        var mParede = Mat("MIR_concreto_branco", new Color(.86f, .85f, .82f), 0, .75f); var mPiso = Mat("MIR_porcelanato_cinza", new Color(.55f, .55f, .53f), 0, .35f);
        foreach (var (lado, x0, x1, xr) in new[] { ("oeste", VarX0, NucX0, VarX0), ("leste", NucX1, VarX1, VarX1 - .05f) })
        {
            Caixa(va, "piso_" + lado, x0, VarY - .25f, NucZ0, x1, VarY, NucZ1, LMapa);
            Caixa(va, "guarda_corpo_" + lado, xr, VarY, NucZ0, xr + .05f, VarY + 1.10f, NucZ1, LGatilho);   // grade: segura o corpo, bala passa
            Caixa(va, "ponta_n_" + lado, x0, VarY, NucZ0 - .05f, x1, VarY + 1.10f, NucZ0, LGatilho);
            // parede do núcleo com o vão da porta (0,9 m)
            float xw0 = lado == "oeste" ? NucX0 : NucX1 - .2f, xw1 = xw0 + .2f;
            Caixa(va, "parede_" + lado + "_n", xw0, VarY, NucZ0, xw1, Teto, PortaZ0, LMapa, mParede);
            Caixa(va, "parede_" + lado + "_s", xw0, VarY, PortaZ1, xw1, Teto, PocoZ0, LMapa, mParede);
            Caixa(va, "verga_" + lado, xw0, VarY + 2.1f, PortaZ0, xw1, Teto, PortaZ1, LMapa, mParede);
        }
        // patamar dentro do núcleo: piso, teto, parede norte por dentro e luz
        Caixa(va, "patamar_piso", NucX0, VarY - .3f, NucZ0, NucX1, VarY, PocoZ0 + 1.2f, LMapa, mPiso);
        Caixa(va, "patamar_teto", NucX0, VarY + 2.7f, NucZ0, NucX1, Teto, PocoZ0, LMapa, mParede);
        Caixa(va, "patamar_parede_n", NucX0, VarY, NucZ0, NucX1, Teto, NucZ0 + .2f, LMapa, mParede);
        Caixa(va, "patamar_parede_poco_o", NucX0, VarY, PocoZ0 - .2f, ElevX0 - .25f, Teto, PocoZ0, LMapa, mParede);
        Caixa(va, "patamar_parede_poco_l", ElevX1 + .25f, VarY, PocoZ0 - .2f, NucX1, Teto, PocoZ0, LMapa, mParede);
        var luz = new GameObject("patamar_luz").AddComponent<Light>(); luz.transform.SetParent(va, false); luz.transform.position = W(29.55f, VarY + 2.5f, (NucZ0 + PocoZ0) * .5f);
        luz.type = LightType.Point; luz.range = 7; luz.intensity = 2.5f; luz.color = new Color(1, .95f, .86f); luz.shadows = LightShadows.Soft;

        // ---- elevador: cabine na face do elevador, frente de vidro para fora
        var el = new GameObject("Elevador").transform; el.SetParent(raiz.transform, false);
        el.position = W((ElevX0 + ElevX1) * .5f, 0, ElevFaceZ - 1.05f);
        var fora = W(29.55f, 0, ElevFaceZ + 5) - W(29.55f, 0, ElevFaceZ); fora.y = 0; el.rotation = Quaternion.LookRotation(-fora.normalized, Vector3.up);   // +z local = lado do núcleo (porta)
        var fe = el.gameObject.AddComponent<FBElevador>(); fe.paradas = new[] { TerreoY, VarY };
        var cab = new GameObject("Cabine").transform; cab.SetParent(el, false); fe.cabine = cab;
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Cabine);
        if (fbx)
        {
            var m = (GameObject)PrefabUtility.InstantiatePrefab(fbx); m.transform.SetParent(cab, false); m.transform.localRotation = Quaternion.Euler(0, 180, 0);
            fe.portaE = m.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MIR_Elevador_PortaE");
            fe.portaD = m.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MIR_Elevador_PortaD");
        }
        else Debug.LogWarning("[FreeBala][mirante] " + Cabine + " não encontrado: cabine só com colisão");
        // colisão da cabine (local: x largura 2,4 · z profundidade 1,8 · porta em +z)
        CaixaLocal(cab, "col_piso", new Vector3(0, .05f, 0), new Vector3(2.4f, .1f, 1.8f), LMapa);
        CaixaLocal(cab, "col_teto", new Vector3(0, 2.45f, 0), new Vector3(2.4f, .1f, 1.8f), LMapa);
        CaixaLocal(cab, "col_vidro_frente", new Vector3(0, 1.25f, -.88f), new Vector3(2.4f, 2.5f, .04f), LGatilho);
        CaixaLocal(cab, "col_vidro_e", new Vector3(-1.18f, 1.25f, 0), new Vector3(.04f, 2.5f, 1.8f), LGatilho);
        CaixaLocal(cab, "col_vidro_d", new Vector3(1.18f, 1.25f, 0), new Vector3(.04f, 2.5f, 1.8f), LGatilho);

        // ---- cobertura (mesmo padrão dos pontos do mapa)
        var pontos = sc.GetRootGameObjects().FirstOrDefault(g => g.name == "Pontos");
        if (pontos)
        {
            var cv = pontos.transform.Find("cobertura_mirante"); if (cv) UnityEngine.Object.DestroyImmediate(cv.gameObject);
            cv = new GameObject("cobertura_mirante").transform; cv.SetParent(pontos.transform, false);
            new GameObject("hall").transform.SetParent(cv, false); cv.GetChild(0).position = W(29.55f, TerreoY, ElevFaceZ + 3f);
            new GameObject("varanda").transform.SetParent(cv, false); cv.GetChild(1).position = W((VarX0 + NucX0) * .5f, VarY, (NucZ0 + NucZ1) * .5f);
        }
        EditorSceneManager.MarkSceneDirty(sc);
        Debug.Log("[FreeBala][mirante] varanda a 25 m e elevador montados (salve a cena e refaça o NavMesh da Orla)");
    }

    static void SceneManager_Move(GameObject g, UnityEngine.SceneManagement.Scene sc) { UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, sc); }
    // caixa entre dois cantos glTF (sempre alinhada ao mundo; o espelhamento não gira nada)
    static void Caixa(Transform pai, string nome, float gx0, float gy0, float gz0, float gx1, float gy1, float gz1, int layer, Material vis = null)
    {
        Vector3 a = W(gx0, gy0, gz0), c = W(gx1, gy1, gz1); var mn = Vector3.Min(a, c); var mx = Vector3.Max(a, c);
        GameObject g;
        if (vis)
        {
            g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = nome; g.layer = layer; g.transform.SetParent(pai, false);
            g.transform.position = (mn + mx) * .5f; g.transform.localScale = mx - mn; g.GetComponent<MeshRenderer>().sharedMaterial = vis;
        }
        else { g = new GameObject(nome) { layer = layer }; g.transform.SetParent(pai, false); g.transform.position = (mn + mx) * .5f; g.AddComponent<BoxCollider>().size = mx - mn; }
        g.isStatic = true;
    }
    const string PastaMat = "Assets/BURITIRO_Mapas/mirante/materiais";
    static Material Mat(string nome, Color c, float metal, float rough)
    {
        if (!AssetDatabase.IsValidFolder(PastaMat)) { if (!AssetDatabase.IsValidFolder("Assets/BURITIRO_Mapas/mirante")) AssetDatabase.CreateFolder("Assets/BURITIRO_Mapas", "mirante"); AssetDatabase.CreateFolder("Assets/BURITIRO_Mapas/mirante", "materiais"); }
        var path = $"{PastaMat}/{nome}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", 1 - rough); EditorUtility.SetDirty(m); return m;
    }
    static void CaixaLocal(Transform pai, string nome, Vector3 c, Vector3 s, int layer)
    {
        var g = new GameObject(nome) { layer = layer }; g.transform.SetParent(pai, false); g.transform.localPosition = c;
        g.AddComponent<BoxCollider>().size = s;
    }
}
