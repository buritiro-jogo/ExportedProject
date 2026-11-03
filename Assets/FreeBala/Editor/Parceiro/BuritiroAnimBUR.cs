// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/BuritiroAnimBUR.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// BURITIRO: liga as animações grátis BUR_ (pasta Assets/BURITIRO_Animacoes) na pegada do fuzil em terceira pessoa.
// Tudo estático e sem diálogo, para o lab runner ("metodo BuritiroEditor.BuritiroAnimBUR.<Metodo>"):
//   Inventario        só lê: camadas, estados e clipes do controlador do jogo, eventos dos clipes e os clipes BUR_
//                     importados (Humanoid ou não, duração, repetição). Não muda nada.
//   Aplicar           guarda cópia do controlador em Buritiro_Unity\_backup\bur_anim_<data>\, cria a máscara
//                     Assets/BuritiroMascaras/BUR_Tronco.mask (tronco, cabeça, braços, dedos, IK das mãos; sem
//                     pernas, raiz e IK dos pés) e a camada "BUR Tronco" logo depois de "Upper Body", com peso 0.
//                     Rodar de novo refaz a camada (não duplica). Se faltar clipe, não muda nada.
//   Desfazer          tira a camada "BUR Tronco" e apaga a máscara BUR_Tronco.mask. Os clipes ficam na pasta.
//   MedirVelocidades  (fase 3, opcional) mede a velocidade do pé apoiado nos clipes BUR_ de andar/correr/sprint
//                     (que vêm "no lugar") e grava _diag\bur_clipRS.json para o "clipRS" do fb_data. Não muda nada.
// Log em Buritiro_Unity\_diag\bur_anim.txt. Com a camada criada e peso 0 o jogo fica igual: quem sobe o peso é o
// FBAnimBur (runtime), que só liga com -fbburanim ou "burAnim": {"ligado": true} no fb_data.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FreeBala;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuritiroEditor
{
	public static class BuritiroAnimBUR
	{
		const string Camada = "BUR Tronco";
		const string DepoisDe = "Upper Body";
		const string PastaBur = "BURITIRO_Animacoes";
		const string PastaMascara = "Assets/BuritiroMascaras";
		const string Mascara = PastaMascara + "/BUR_Tronco.mask";
		// [unificação 02/11] no parceiro (AssetRipper) era Assets/AnimatorController/...; no nosso fonte o controlador fica em FreeBala/Anim/Controllers
		const string CtrlPadrao = "Assets/FreeBala/Anim/Controllers/EnemyAnimator_AAA_relaxed_20260910_014452.controller";

		// estado da camada -> clipe BUR_ (o FBAnimBur escolhe o estado pelo nome)
		static readonly string[,] Estados =
		{
			{ "Fuzil_Segurar", "BUR_Fuzil_Segurar" },
			{ "Fuzil_Mirar", "BUR_Fuzil_Mirar" },
			{ "Espingarda_Segurar", "BUR_Espingarda_Segurar" },
			{ "Espingarda_Mirar", "BUR_Espingarda_Mirar" },
		};

		static string Raiz => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
		static string ArqLog => Path.Combine(Raiz, "_diag", "bur_anim.txt");

		static void L(string s)
		{
			Debug.Log("[BURITIRO] bur_anim: " + s);
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ArqLog));
				File.AppendAllText(ArqLog, DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine);
			}
			catch { }
		}

		// ---------- achar controlador e clipes ----------

		static AnimatorController AcharControlador(out string caminho)
		{
			caminho = null;
			foreach (var guid in AssetDatabase.FindAssets("t:FBRegistry"))
			{
				var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>(AssetDatabase.GUIDToAssetPath(guid));
				if (!reg || !reg.mainController) continue;
				RuntimeAnimatorController rc = reg.mainController;
				if (rc is AnimatorOverrideController aoc) rc = aoc.runtimeAnimatorController;
				var ac = rc as AnimatorController;
				if (ac)
				{
					caminho = AssetDatabase.GetAssetPath(ac);
					L($"controlador do FBRegistry ({AssetDatabase.GUIDToAssetPath(guid)}): {caminho}");
					if (caminho != CtrlPadrao) L($"AVISO: não é o controlador esperado {CtrlPadrao}");
					return ac;
				}
			}
			var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPadrao);
			if (c)
			{
				caminho = CtrlPadrao;
				L("FBRegistry sem mainController; usando " + CtrlPadrao);
			}
			return c;
		}

		static Dictionary<string, AnimationClip> ClipesBur(out Dictionary<string, string> origem)
		{
			var d = new Dictionary<string, AnimationClip>();
			origem = new Dictionary<string, string>();
			foreach (var guid in AssetDatabase.FindAssets("t:Model"))
			{
				string p = AssetDatabase.GUIDToAssetPath(guid);
				if (!p.Replace('\\', '/').Contains("/" + PastaBur + "/")) continue;
				foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
				{
					if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
					{
						d[c.name] = c;
						origem[c.name] = p;
					}
				}
			}
			return d;
		}

		// ---------- Inventario (só leitura) ----------

		public static void Inventario()
		{
			L("===== Inventario =====");
			var ac = AcharControlador(out string caminho);
			if (!ac) { L("ERRO: controlador não encontrado"); return; }
			var sb = new StringBuilder();
			sb.Append("parâmetros: ");
			foreach (var p in ac.parameters) sb.Append($"'{p.name}' ({p.type}) ");
			L(sb.ToString());
			var eventos = new Dictionary<string, string>();
			var ls = ac.layers;
			for (int i = 0; i < ls.Length; i++)
			{
				var l = ls[i];
				string partes = "";
				if (l.avatarMask)
					for (int k = 0; k < (int)AvatarMaskBodyPart.LastBodyPart; k++)
						if (l.avatarMask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)k)) partes += (AvatarMaskBodyPart)k + " ";
				L($"camada {i} '{l.name}': peso {l.defaultWeight} · {l.blendingMode} · IK {l.iKPass} · máscara {(l.avatarMask ? AssetDatabase.GetAssetPath(l.avatarMask) + " [" + partes.Trim() + "]" : "nenhuma")}");
				if (l.stateMachine) Maquina(l.stateMachine, "  ", eventos);
			}
			L(eventos.Count == 0 ? "eventos de animação nos clipes do controlador: nenhum" : "eventos de animação: " + string.Join(" · ", eventos.Select(x => x.Key + " -> " + x.Value)));
			var bur = ClipesBur(out var origem);
			L($"clipes BUR_ importados: {bur.Count} (o pacote tem 82)");
			foreach (var kv in bur.OrderBy(x => x.Key))
			{
				var c = kv.Value;
				var mi = AssetImporter.GetAtPath(origem[kv.Key]) as ModelImporter;
				L($"  {c.name}: {(c.humanMotion ? "Humanoid" : "NÃO Humanoid")} · {c.length:0.00} s · repete {c.isLooping} · velocidade média {new Vector2(c.averageSpeed.x, c.averageSpeed.z).magnitude:0.00} · eventos {c.events.Length} · tipo de import {(mi ? mi.animationType.ToString() : "?")} · {origem[kv.Key]}");
			}
			foreach (string n in new[] { "BUR_Fuzil_Segurar", "BUR_Fuzil_Mirar", "BUR_Espingarda_Segurar", "BUR_Espingarda_Mirar", "BUR_Fuzil_Recarregar", "BUR_Fuzil_Atirar" })
				if (!bur.ContainsKey(n)) L("  FALTA " + n);
			foreach (var guid in AssetDatabase.FindAssets("t:FBRegistry"))
			{
				var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>(AssetDatabase.GUIDToAssetPath(guid));
				if (!reg) continue;
				L($"FBRegistry {AssetDatabase.GUIDToAssetPath(guid)}: {reg.clips.Count} clipes ({reg.clips.Count(x => x && x.name.StartsWith("BUR_"))} BUR_) · mainController {(reg.mainController ? reg.mainController.name : "nenhum")} · pedestrianController {(reg.pedestrianController ? reg.pedestrianController.name : "nenhum")}");
			}
			L("===== fim do inventário =====");
		}

		static void Maquina(AnimatorStateMachine sm, string pre, Dictionary<string, string> eventos)
		{
			foreach (var cs in sm.states)
			{
				var s = cs.state;
				L($"{pre}estado '{s.name}' · velocidade {s.speed}{(s.speedParameterActive ? " x " + s.speedParameter : "")} · WD {s.writeDefaultValues} · {Movimento(s.motion, eventos)}");
			}
			foreach (var sub in sm.stateMachines) Maquina(sub.stateMachine, pre + "  ", eventos);
		}

		static string Movimento(Motion m, Dictionary<string, string> eventos)
		{
			if (!m) return "sem clipe";
			if (m is AnimationClip c)
			{
				foreach (var e in c.events) eventos[c.name + "@" + e.time.ToString("0.00", CultureInfo.InvariantCulture)] = e.functionName;
				return $"clipe '{c.name}' ({(c.humanMotion ? "H" : "G")}, {c.length:0.00} s)";
			}
			if (m is BlendTree bt)
			{
				var sb = new StringBuilder($"blend {bt.blendType} ({bt.blendParameter}{(bt.blendType == BlendTreeType.Simple1D ? "" : "," + bt.blendParameterY)}): ");
				foreach (var ch in bt.children)
					sb.Append($"[{ch.position.x:0.00},{ch.position.y:0.00} t{ch.threshold:0.00}] {Movimento(ch.motion, eventos)}; ");
				return sb.ToString();
			}
			return m.GetType().Name + " " + m.name;
		}

		// ---------- Aplicar ----------

		// Aplicar: máscara com coluna (Body), cabeça e braços. AplicarSemColuna: só cabeça e braços (a coluna e o
		// quadril continuam vindo da locomoção). Rodar uma troca a outra (a camada velha sai antes).
		public static void Aplicar() { Fazer(true); }

		public static void AplicarSemColuna() { Fazer(false); }

		static void Fazer(bool coluna)
		{
			L("===== Aplicar (" + (coluna ? "com coluna" : "sem coluna") + ") =====");
			var ac = AcharControlador(out string caminho);
			if (!ac) { L("ERRO: controlador não encontrado; nada mudou"); return; }
			var bur = ClipesBur(out _);
			var clipes = new List<AnimationClip>();
			for (int i = 0; i < Estados.GetLength(0); i++)
			{
				if (!bur.TryGetValue(Estados[i, 1], out var c)) { L($"ERRO: falta o clipe {Estados[i, 1]} (copie BURITIRO_Animacoes para Assets e espere importar); nada mudou"); return; }
				if (!c.humanMotion) { L($"ERRO: {c.name} não é Humanoid (rode BURITIRO > Animacoes > Reimportar todas as animacoes); nada mudou"); return; }
				clipes.Add(c);
			}
			// cópia do controlador antes de mexer
			try
			{
				string bk = Path.Combine(Raiz, "_backup", "bur_anim_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
				Directory.CreateDirectory(bk);
				string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", caminho));
				File.Copy(abs, Path.Combine(bk, Path.GetFileName(abs)), true);
				if (File.Exists(abs + ".meta")) File.Copy(abs + ".meta", Path.Combine(bk, Path.GetFileName(abs) + ".meta"), true);
				L("cópia do controlador em " + bk);
			}
			catch (Exception e) { L("ERRO ao copiar o controlador (" + e.Message + "); nada mudou"); return; }

			// máscara própria (não usa a FB_UpperBody para não mudar junto se alguém mexer nela)
			if (!AssetDatabase.IsValidFolder(PastaMascara)) AssetDatabase.CreateFolder("Assets", "BuritiroMascaras");
			var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(Mascara);
			bool nova = m == null;
			if (nova) m = new AvatarMask();
			for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) m.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
			foreach (var p in new[] { AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK })
				m.SetHumanoidBodyPartActive(p, true);
			m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, coluna);
			m.transformCount = 0;
			if (nova) AssetDatabase.CreateAsset(m, Mascara); else EditorUtility.SetDirty(m);

			// tira a camada velha (rodar duas vezes não duplica)
			TirarCamada(ac);

			var sm = new AnimatorStateMachine { name = Camada, hideFlags = HideFlags.HideInHierarchy };
			AssetDatabase.AddObjectToAsset(sm, ac);
			var vazio = sm.AddState("Vazio", new Vector3(300, 0, 0));
			vazio.writeDefaultValues = false;
			sm.defaultState = vazio;
			for (int i = 0; i < Estados.GetLength(0); i++)
			{
				var s = sm.AddState(Estados[i, 0], new Vector3(300 + 260 * (i % 2), 100 + 80 * (i / 2), 0));
				s.motion = clipes[i];
				s.writeDefaultValues = false;
			}
			var nl = new AnimatorControllerLayer
			{
				name = Camada,
				stateMachine = sm,
				avatarMask = m,
				defaultWeight = 0f,
				blendingMode = AnimatorLayerBlendingMode.Override,
				iKPass = false,
				syncedLayerIndex = -1,
			};
			var ls = ac.layers.ToList();
			int idx = ls.FindIndex(x => x.name == DepoisDe);
			if (idx < 0) { L($"AVISO: camada '{DepoisDe}' não existe; a camada BUR vai no fim"); idx = ls.Count; }
			else idx++;
			ls.Insert(idx, nl);
			ac.layers = ls.ToArray();
			EditorUtility.SetDirty(sm);
			EditorUtility.SetDirty(ac);
			AssetDatabase.SaveAssets();
			var fim = ac.layers;
			for (int i = 0; i < fim.Length; i++)
				L($"camada {i} '{fim[i].name}' · peso padrão {fim[i].defaultWeight} · máscara {(fim[i].avatarMask ? fim[i].avatarMask.name : "nenhuma")}");
			L($"pronto: camada '{Camada}' no índice {idx}, peso 0, estados {string.Join(", ", sm.states.Select(x => x.state.name + (x.state.motion ? "=" + x.state.motion.name : "")))}");
		}

		static int TirarCamada(AnimatorController ac)
		{
			int n = 0;
			for (int i = ac.layers.Length - 1; i >= 0; i--)
			{
				if (ac.layers[i].name != Camada) continue;
				ac.RemoveLayer(i);
				n++;
			}
			if (n > 0) L($"camada '{Camada}' antiga tirada ({n})");
			return n;
		}

		// ---------- Desfazer ----------

		public static void Desfazer()
		{
			L("===== Desfazer =====");
			var ac = AcharControlador(out _);
			if (!ac) { L("ERRO: controlador não encontrado"); return; }
			int n = TirarCamada(ac);
			EditorUtility.SetDirty(ac);
			AssetDatabase.SaveAssets();
			if (AssetDatabase.LoadAssetAtPath<AvatarMask>(Mascara)) { AssetDatabase.DeleteAsset(Mascara); L("máscara apagada: " + Mascara); }
			L(n > 0 ? "pronto: controlador como antes (camadas: " + string.Join(", ", ac.layers.Select(x => x.name)) + ")" : "não havia camada BUR");
		}

		// ---------- MedirVelocidades (fase 3, só leitura) ----------

		public static void MedirVelocidades()
		{
			L("===== MedirVelocidades =====");
			var bur = ClipesBur(out var origem);
			var nomes = bur.Keys.Where(n => n.StartsWith("BUR_Andar_") || n.StartsWith("BUR_Correr_") || n.StartsWith("BUR_Sprint_") || n == "BUR_Agachar_Andar_F").OrderBy(n => n).ToList();
			var json = new StringBuilder("{");
			bool primeiro = true;
			AnimationMode.StartAnimationMode();
			try
			{
				foreach (string n in nomes)
				{
					var clip = bur[n];
					var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(origem[n]);
					if (!modelo) { L($"{n}: sem modelo no FBX"); continue; }
					// cena de prévia: não suja a cena aberta (o lab runner recusa "metodo" com cena suja)
					var cena = EditorSceneManager.NewPreviewScene();
					var go = PrefabUtility.InstantiatePrefab(modelo, cena) as GameObject;
					if (!go) { EditorSceneManager.ClosePreviewScene(cena); L($"{n}: não instanciou o modelo"); continue; }
					try
					{
						go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
						var an = go.GetComponent<Animator>();
						if (!an || !an.isHuman) { L($"{n}: modelo sem Animator Humanoid"); continue; }
						var pe = new[] { an.GetBoneTransform(HumanBodyBones.LeftFoot), an.GetBoneTransform(HumanBodyBones.RightFoot) };
						if (!pe[0] || !pe[1]) { L($"{n}: sem osso do pé"); continue; }
						int amostras = Mathf.Max(30, Mathf.CeilToInt(clip.length * 120f));
						float dt = clip.length / amostras;
						var pos = new Vector3[2, amostras + 1];
						for (int i = 0; i <= amostras; i++)
						{
							AnimationMode.BeginSampling();
							AnimationMode.SampleAnimationClip(go, clip, i * dt);
							AnimationMode.EndSampling();
							for (int k = 0; k < 2; k++) pos[k, i] = go.transform.InverseTransformPoint(pe[k].position);
						}
						// pé apoiado: o mais baixo, a menos de 1,5 cm (vezes a escala) da altura mínima dele no ciclo
						float esc = Mathf.Max(0.01f, an.humanScale);
						var minY = new float[2];
						for (int k = 0; k < 2; k++) { minY[k] = float.MaxValue; for (int i = 0; i <= amostras; i++) minY[k] = Mathf.Min(minY[k], pos[k, i].y); }
						var vels = new List<float>();
						Vector3 soma = Vector3.zero;
						for (int i = 1; i <= amostras; i++)
						{
							int k = pos[0, i].y <= pos[1, i].y ? 0 : 1;
							if (pos[k, i].y > minY[k] + 0.015f * esc || pos[k, i - 1].y > minY[k] + 0.015f * esc) continue;
							Vector3 d = pos[k, i] - pos[k, i - 1];
							d.y = 0f;
							vels.Add(d.magnitude / dt);
							soma += d;
						}
						if (vels.Count < 4) { L($"{n}: poucas amostras com pé apoiado ({vels.Count}); a amostragem pode ter falhado"); continue; }
						vels.Sort();
						float med = vels[vels.Count / 2];
						float rs = med / esc;
						Vector3 dir = -soma.normalized;
						float ang = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
						L($"{n}: pé apoiado anda {med:0.00} m/s no modelo (humanScale {esc:0.00}) -> clipRS {rs:0.00} · direção do corpo {ang:0}° (0 = frente, 90 = direita) · {vels.Count} amostras");
						json.Append((primeiro ? "" : ", ") + $"\"{n}\": {rs.ToString("0.00", CultureInfo.InvariantCulture)}");
						primeiro = false;
					}
					finally { UnityEngine.Object.DestroyImmediate(go); EditorSceneManager.ClosePreviewScene(cena); }
				}
			}
			finally { AnimationMode.StopAnimationMode(); }
			json.Append("}");
			string arq = Path.Combine(Raiz, "_diag", "bur_clipRS.json");
			try { File.WriteAllText(arq, json.ToString()); L("gravado " + arq); } catch (Exception e) { L("ERRO ao gravar " + arq + ": " + e.Message); }
		}
	}
}
#endif
