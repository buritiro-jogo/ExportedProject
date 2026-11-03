"""Troca, nas pastas do parceiro copiadas para o nosso projeto, os GUIDs de assets extraidos pelo AssetRipper
(Assets/Resources/fbimported/...) pelos GUIDs dos assets equivalentes no nosso projeto (Assets/FreeBala/Resources/FBImported/...).
Uso: python -I remap_guids.py <projeto parceiro> <nosso projeto> <relatorio.json> [--aplicar] pasta1 pasta2 ...
Sem --aplicar so lista. Confere a imagem (tamanho e media de cor) quando o Pillow existe.
"""
import os, re, sys, json

par, our, rep_p = sys.argv[1:4]
aplicar = "--aplicar" in sys.argv
folders = [a for a in sys.argv[4:] if not a.startswith("--")]

def guid_index(root):
    g2p = {}
    for dp, dn, fn in os.walk(os.path.join(root, "Assets")):
        if "Library" in dp:
            continue
        for f in fn:
            if f.endswith(".meta"):
                p = os.path.join(dp, f)
                try:
                    with open(p, encoding="utf-8", errors="ignore") as fh:
                        for line in fh:
                            if line.startswith("guid:"):
                                g2p[line.split()[1]] = os.path.relpath(p[:-5], root).replace("\\", "/")
                                break
                except OSError:
                    pass
    return g2p

pg = guid_index(par)
og = guid_index(our)
op_lower = {p.lower(): g for g, p in og.items()}
op_noext = {}
for p, g in op_lower.items():
    op_noext.setdefault(os.path.splitext(p)[0], []).append((p, g))

try:
    from PIL import Image
except ImportError:
    Image = None

def sig(path):
    if Image is None or not os.path.exists(path):
        return None
    try:
        im = Image.open(path).convert("RGB")
        w, h = im.size
        sm = im.resize((8, 8))
        return (w, h, [sum(px) // 3 for px in sm.getdata()])
    except Exception:
        return None

def candidate(ppath):
    lp = ppath.lower()
    if lp.startswith("assets/resources/fbimported/"):
        lp2 = "assets/freebala/resources/fbimported/" + lp[len("assets/resources/fbimported/"):]
    elif lp.startswith("assets/resources/"):
        lp2 = "assets/freebala/resources/" + lp[len("assets/resources/"):]
    else:
        return None
    if lp2 in op_lower:
        return lp2, op_lower[lp2]
    alts = op_noext.get(os.path.splitext(lp2)[0])
    return alts[0] if alts else None

pat = re.compile(r"guid: ([0-9a-f]{32})")
rep = {"trocados": {}, "sem_equivalente": {}, "imagem_diferente": {}, "arquivos_alterados": 0}
for fold in folders:
    for dp, dn, fn in os.walk(os.path.join(our, fold)):
        for f in fn:
            if not f.endswith((".mat", ".prefab", ".asset", ".controller", ".unity")):
                continue
            p = os.path.join(dp, f)
            with open(p, "rb") as fh:
                head = fh.read(5)
            if head != b"%YAML":
                continue
            txt = open(p, encoding="utf-8", errors="surrogateescape").read()
            changed = [False]
            def sub(m):
                g = m.group(1)
                if g in og:            # ja existe no nosso projeto (pacote ou pasta copiada)
                    return m.group(0)
                pp = pg.get(g)
                if pp is None:
                    rep["sem_equivalente"].setdefault("<guid desconhecido " + g + ">", 0)
                    rep["sem_equivalente"]["<guid desconhecido " + g + ">"] += 1
                    return m.group(0)
                c = candidate(pp)
                if c is None:
                    rep["sem_equivalente"][pp] = rep["sem_equivalente"].get(pp, 0) + 1
                    return m.group(0)
                s1, s2 = sig(os.path.join(par, pp)), sig(os.path.join(our, og[c[1]]))
                if s1 and s2 and (s1[0:2] != s2[0:2] or sum(abs(a - b) for a, b in zip(s1[2], s2[2])) / 64 > 25):
                    rep["imagem_diferente"][pp] = og[c[1]]
                rep["trocados"][pp] = og[c[1]]
                changed[0] = True
                return "guid: " + c[1]
            new = pat.sub(sub, txt)
            if changed[0] and aplicar:
                open(p, "w", encoding="utf-8", errors="surrogateescape", newline="").write(new)
                rep["arquivos_alterados"] += 1
json.dump(rep, open(rep_p, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("trocados:", len(rep["trocados"]), "| sem equivalente:", len(rep["sem_equivalente"]), "| imagem diferente:", len(rep["imagem_diferente"]),
      "| arquivos alterados:", rep["arquivos_alterados"], "| Pillow:", Image is not None)
for k, v in list(rep["sem_equivalente"].items())[:10]:
    print("  sem:", v, k)
for k, v in list(rep["imagem_diferente"].items())[:10]:
    print("  difere:", k, "->", v)
