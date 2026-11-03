// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/BuritiroMascaraCorpo.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// Máscara de tronco para as camadas "Upper Body" e "Emote Upper Body" do Animator dos personagens.
// Na exportação do jogo (AssetRipper) as camadas perderam a máscara (m_Mask vazio). Sem máscara, a camada
// de cima (mira, tiro, recarga) manda no corpo inteiro e trava as pernas na pose de mira: o bot desliza
// quando mira e anda. Esta ferramenta cria Assets/BuritiroMascaras/FB_UpperBody.mask (coluna, cabeça,
// braços, dedos e IK das mãos; sem pernas, sem raiz, sem IK dos pés) e liga nessas duas camadas.
// Uso: menu BURITIRO > Animação, ou lab runner "metodo BuritiroMascaraCorpo.Aplicar".
// Log em Buritiro_Unity\_diag\mascara_corpo.txt.
// [unificação 02/11] ATENÇÃO: no nosso fonte original as camadas "Upper Body"/"Emote Upper Body" já têm máscara própria
// (EnemyAnimator_AAA_relaxed_..._Upper Body.mask); o problema era só da exportação do AssetRipper. Aplicar aqui TROCA a nossa
// máscara pela FB_UpperBody: só rode de propósito ("Desfazer" deixa as camadas SEM máscara, não volta a nossa).
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BuritiroMascaraCorpo
{
	// [unificação 02/11] no parceiro (AssetRipper) era Assets/AnimatorController/...; no nosso fonte fica em FreeBala/Anim/Controllers
	const string Ctrl = "Assets/FreeBala/Anim/Controllers/EnemyAnimator_AAA_relaxed_20260910_014452.controller";
	const string Pasta = "Assets/BuritiroMascaras";
	const string Mascara = Pasta + "/FB_UpperBody.mask";
	static readonly string[] Camadas = { "Upper Body", "Emote Upper Body" };

	static string ArqLog => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag/mascara_corpo.txt"));

	static void L(string s)
	{
		string l = DateTime.Now.ToString("HH:mm:ss") + "  " + s;
		Debug.Log("[FreeBala] mascara: " + s);
		try { File.AppendAllText(ArqLog, l + Environment.NewLine); } catch { }
	}

	[MenuItem("BURITIRO/Animação/Máscara de tronco nas camadas de cima (com coluna)")]
	public static void Aplicar() { Fazer(true); }

	[MenuItem("BURITIRO/Animação/Máscara de tronco nas camadas de cima (só braços e cabeça)")]
	public static void AplicarSemColuna() { Fazer(false); }

	[MenuItem("BURITIRO/Animação/Tirar a máscara das camadas de cima (como era)")]
	public static void Desfazer()
	{
		var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(Ctrl);
		if (!c) { L("controlador não encontrado: " + Ctrl); return; }
		var ls = c.layers;
		foreach (var l in ls) if (Array.IndexOf(Camadas, l.name) >= 0) l.avatarMask = null;
		c.layers = ls;
		EditorUtility.SetDirty(c);
		AssetDatabase.SaveAssets();
		L("máscara tirada das camadas " + string.Join(", ", Camadas));
	}

	static void Fazer(bool coluna)
	{
		var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(Ctrl);
		if (!c) { L("controlador não encontrado: " + Ctrl); return; }
		if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets", "BuritiroMascaras");
		var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(Mascara);
		bool nova = m == null;
		if (nova) m = new AvatarMask();
		for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) m.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
		foreach (var p in new[] { AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK })
			m.SetHumanoidBodyPartActive(p, true);
		m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, coluna);
		m.transformCount = 0;
		if (nova) AssetDatabase.CreateAsset(m, Mascara);
		else EditorUtility.SetDirty(m);
		var ls = c.layers;
		int ligadas = 0;
		foreach (var l in ls)
		{
			L($"camada {l.name}: máscara antes {(l.avatarMask ? l.avatarMask.name : "nenhuma")} · peso padrão {l.defaultWeight} · {l.blendingMode} · IK {l.iKPass}");
			if (Array.IndexOf(Camadas, l.name) >= 0) { l.avatarMask = m; ligadas++; }
		}
		c.layers = ls;
		EditorUtility.SetDirty(c);
		AssetDatabase.SaveAssets();
		var s = "";
		for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) s += $"{(AvatarMaskBodyPart)i}={(m.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i) ? 1 : 0)} ";
		foreach (var l in c.layers) L($"camada {l.name}: máscara agora {(l.avatarMask ? l.avatarMask.name : "nenhuma")}");
		L($"pronto: {ligadas} camadas com {Mascara} ({(coluna ? "com coluna" : "só braços e cabeça")}) · partes {s}");
	}
}
