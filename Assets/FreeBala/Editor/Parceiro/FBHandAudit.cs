// Auditoria das mãos (09/11): para cada personagem do registro, diz se o Avatar tem os ossos de dedo que o FBRig usa para orientar
// a palma esquerda no guarda-mão (FBHand.Frame) e fechar os dedos (FBRig.Wrap). Sem eles a mão fica com a rotação do clipe.
// Em lote: Unity -batchmode -executeMethod FBHandAudit.Batch  →  ..\..\_diag\maos_avatar.txt
using System.IO;
using System.Linq;
using System.Text;
using FreeBala;
using UnityEditor;
using UnityEngine;

public static class FBHandAudit
{
	static readonly HumanBodyBones[] Esq =
	{
		HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
		HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
		HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal
	};
	static readonly HumanBodyBones[] Dir =
	{
		HumanBodyBones.RightHand, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal,
		HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal
	};

	[MenuItem("FreeBala/Diagnóstico/Auditar dedos dos avatares")]
	public static void Rodar()
	{
		var saida = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag/maos_avatar.txt"));
		GameData.Load();
		var reg = FBRegistry.I; var sb = new StringBuilder(); int ok = 0, ruim = 0;
		var extra = new System.Collections.Generic.Dictionary<string, string>();
		sb.AppendLine("personagem;humano;palma_esq_ok;palma_dir_ok;faltam_esq;faltam_dir;nome_mao_esq;filhos_da_mao_esq");
		foreach (var s in GameData.Skins.OrderBy(x => x.id))
		{
			if (!reg.HasChar(s.id)) continue;
			var pf = reg.Char(s.id); if (!pf) continue;
			var go = (GameObject)Object.Instantiate(pf); go.hideFlags = HideFlags.HideAndDontSave;
			try
			{
				var an = go.GetComponent<Animator>() ?? go.GetComponentInChildren<Animator>();
				bool hum = an && an.isHuman;
				bool le = FBHand.Frame(an, false, out var ql, out var pl), ri = FBHand.Frame(an, true, out var qr, out var pr);
				// referencial da palma no espaço do osso (frente dos dedos e normal da palma) e direção mão→indicador, para comparar rigs
				string V(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";
				string Q(bool okq, Quaternion q, float p, bool right)
				{
					var h = hum ? an.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand) : null;
					var ix = hum ? an.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal) : null;
					var th = hum ? an.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal) : null;
					string di = h && ix ? V(h.InverseTransformPoint(ix.position).normalized) : "-", dt = h && th ? V(h.InverseTransformPoint(th.position).normalized) : "-";
					return (okq ? $"f{V(q * Vector3.forward)} n{V(q * Vector3.up)} palma {p * 100:0.0}cm" : "sem referencial") + $" · indicador {di} · polegar {dt}";
				}
				extra[s.id] = "E: " + Q(le, ql, pl, false) + " | D: " + Q(ri, qr, pr, true);
				string F(HumanBodyBones[] l) => hum ? string.Join(",", l.Where(b => !an.GetBoneTransform(b)).Select(b => b.ToString().Replace("Left", "").Replace("Right", ""))) : "?";
				var lh = hum ? an.GetBoneTransform(HumanBodyBones.LeftHand) : null;
				string filhos = lh ? string.Join(",", Enumerable.Range(0, lh.childCount).Select(i => lh.GetChild(i).name)) : "";
				sb.AppendLine($"{s.id};{(hum ? 1 : 0)};{(le ? 1 : 0)};{(ri ? 1 : 0)};{F(Esq)};{F(Dir)};{(lh ? lh.name : "")};{filhos}");
				if (le && ri) ok++; else ruim++;
			}
			finally { Object.DestroyImmediate(go); }
		}
		sb.AppendLine("\n== referencial da palma por personagem (espaço do osso da mão) ==");
		foreach (var kv in extra.OrderBy(k => k.Key)) sb.AppendLine($"{kv.Key,-18} {kv.Value}");
		sb.Insert(0, $"avatares com as duas palmas ok: {ok} · com problema: {ruim}\n");
		File.WriteAllText(saida, sb.ToString());
		Debug.Log("[FreeBala][maos] gravado em " + saida);
	}

	public static void Batch() { try { Rodar(); EditorApplication.Exit(0); } catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); } }
}
