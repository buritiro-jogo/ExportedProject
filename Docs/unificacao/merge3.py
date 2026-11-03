"""Merge de 3 vias do fb_data.json: base (r45) x nosso (r53) x parceiro.
Uso: python -I merge3.py base.json nosso.json parceiro.json saida.json relatorio.json [prefer=nosso|parceiro]
Regra por chave (recursivo em dicts; listas e escalares sao atomicos):
  so um lado mudou -> fica a mudanca; os dois mudaram igual -> ok; os dois mudaram diferente -> conflito (resolve por 'prefer', e registra).
"""
import json, sys

base_p, ours_p, theirs_p, out_p, rep_p = sys.argv[1:6]
prefer = sys.argv[6] if len(sys.argv) > 6 else "nosso"
load = lambda p: json.load(open(p, encoding="utf-8-sig"))
B, O, T = load(base_p), load(ours_p), load(theirs_p)
MISSING = object()
rep = {"so_parceiro": [], "so_nosso": 0, "conflitos": [], "removido_parceiro": [], "removido_nosso": []}

def short(v):
    s = json.dumps(v, ensure_ascii=False)
    return s if len(s) < 300 else s[:300] + "..."

def merge(b, o, t, path):
    if isinstance(o, dict) and isinstance(t, dict) and (isinstance(b, dict) or b is MISSING):
        bb = b if isinstance(b, dict) else {}
        res = {}
        for k in list(o.keys()) + [k for k in t.keys() if k not in o]:
            v = merge(bb.get(k, MISSING), o.get(k, MISSING), t.get(k, MISSING), path + [k])
            if v is not MISSING:
                res[k] = v
        return res
    if o == t:
        return o
    if t == b:          # so nos mudamos (ou o parceiro nao tem)
        if o is not MISSING:
            rep["so_nosso"] += 1
        return o
    if o == b:          # so o parceiro mudou
        if t is MISSING:
            rep["removido_parceiro"].append("/".join(map(str, path)))
            return o if prefer == "nosso" else MISSING  # nao apagamos o que e nosso por padrao
        rep["so_parceiro"].append("/".join(map(str, path)))
        return t
    # os dois mudaram
    rep["conflitos"].append({"caminho": "/".join(map(str, path)), "base": short(b) if b is not MISSING else None,
                             "nosso": short(o) if o is not MISSING else None, "parceiro": short(t) if t is not MISSING else None})
    if o is MISSING:
        return t
    if t is MISSING:
        return o
    return o if prefer == "nosso" else t

M = merge(B, O, T, [])
json.dump(M, open(out_p, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
json.dump(rep, open(rep_p, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("so_parceiro:", len(rep["so_parceiro"]), "| so_nosso:", rep["so_nosso"], "| conflitos:", len(rep["conflitos"]),
      "| removidos pelo parceiro:", len(rep["removido_parceiro"]))
