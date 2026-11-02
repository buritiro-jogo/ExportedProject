using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBCuraLab : MonoBehaviour
{
	private string run = "vila_jardim:tdm";

	private string dir;

	private FBInputState press;

	private bool once;

	private Vector2 hold;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbcuralab");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_CuraLab");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBCuraLab fBCuraLab = obj.AddComponent<FBCuraLab>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				fBCuraLab.run = commandLineArgs[num + 1];
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][curalab] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.deltaTime)
		{
			yield return null;
		}
	}

	private void Shot(string n)
	{
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cura_" + n + ".png"));
	}

	private IEnumerator Press(Action<FBInputState> _ = null)
	{
		yield return null;
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		yield return Wait(3f);
		string[] array = run.Split(':');
		GameData.Load();
		FBSession.Load();
		string keepMap = FBSession.map;
		string keepMode = FBSession.mode;
		string keepView = FBSession.view;
		FBSession.map = array[0];
		FBSession.mode = ((array.Length > 1) ? array[1] : "tdm");
		FBSession.online = false;
		FBSession.view = "tps";
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + array[0]);
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		yield return Wait(6f);
		FBCharacter me = FBMatch.Player;
		if (!me)
		{
			Log("sem jogador");
			yield break;
		}
		List<FBCharacter> bots = FBMatch.Bots.Where((FBCharacter b) => b).ToList();
		foreach (FBCharacter item3 in bots)
		{
			item3.gameObject.SetActive(value: false);
		}
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.move = hold;
			if (once)
			{
				once = false;
				FBInputState result = press;
				result.move = hold;
				result.slot = press.slot;
				return result;
			}
			inp.slot = -1;
			inp.cycle = 0;
			return inp;
		};
		me.protUntil = 0f;
		yield return Wait(1f);
		string[] array2 = new string[2] { "tps", "fp" };
		foreach (string view2 in array2)
		{
			if (view2 == "fp" != FBPlayer.I.FirstPerson)
			{
				Do(delegate(FBInputState s)
				{
					s.toggleView = true;
					return s;
				});
				yield return Wait(1f);
			}
			Log(string.Format("--- {0} pessoa (FirstPerson {1})", (view2 == "fp") ? "1ª" : "3ª", FBPlayer.I.FirstPerson));
			FBMed.ResetPlayer(4, infinite: false);
			me.hp = 20f;
			hold = Vector2.zero;
			yield return Wait(0.6f);
			Do(delegate(FBInputState s)
			{
				s.heal = true;
				return s;
			});
			yield return null;
			yield return null;
			string row = "";
			float t2 = Time.time;
			string clips = "";
			while (Time.time - t2 < 3.4f)
			{
				row += $"{Time.time - t2:0.00}s:{me.hp:0} ";
				if (me.sys != null && !clips.Contains(me.sys))
				{
					clips = clips + me.sys + " ";
				}
				yield return Wait(0.25f);
			}
			Log($"{view2} cura parado: {row}· kits {FBMed.kits} (antes 4) · clipes {clips}· Busy {FBMed.Busy}");
			if (view2 == "tps")
			{
				Shot("parado_fim_" + view2);
			}
			(string, Func<FBInputState, FBInputState>)[] array3 = new(string, Func<FBInputState, FBInputState>)[7]
			{
				("andar", (FBInputState s) => s),
				("pular", delegate(FBInputState s)
				{
					s.jump = true;
					return s;
				}),
				("correr", delegate(FBInputState s)
				{
					s.sprintHeld = true;
					s.move = new Vector2(0f, 1f);
					return s;
				}),
				("atirar", delegate(FBInputState s)
				{
					s.fire = (s.firePressed = true);
					return s;
				}),
				("mirar", delegate(FBInputState s)
				{
					s.aim = (s.aimPressed = true);
					return s;
				}),
				("recarregar", delegate(FBInputState s)
				{
					s.reload = true;
					return s;
				}),
				("agachar", delegate(FBInputState s)
				{
					s.crouchToggle = true;
					return s;
				})
			};
			for (int j = 0; j < array3.Length; j++)
			{
				var (nome, f2) = array3[j];
				FBMed.ResetPlayer(4, infinite: false);
				me.hp = 20f;
				hold = Vector2.zero;
				yield return Wait(0.8f);
				Do(delegate(FBInputState s)
				{
					s.heal = true;
					return s;
				});
				yield return Wait(1.2f);
				if (nome == "agachar" && view2 == "tps")
				{
					Shot("curando_" + view2);
				}
				float hpAt = me.hp;
				int c0 = FBPlayer.HealCancels;
				string sysAt = me.sys;
				if (nome == "andar")
				{
					hold = new Vector2(0f, 1f);
				}
				else if (nome == "correr")
				{
					hold = new Vector2(0f, 1f);
				}
				Do(f2);
				yield return null;
				yield return null;
				float hpNext = me.hp;
				hold = Vector2.zero;
				yield return Wait(0.6f);
				Log(string.Format("{0} cancelar por {1}: vida na hora {2:0.0} -> 2 quadros {3:0.0} -> +0,6 s {4:0.0} · cancelou {5} (motivo {6}) · Busy {7} · clipe antes {8} depois {9} · kits {10}", view2, nome, hpAt, hpNext, me.hp, FBPlayer.HealCancels > c0, FBPlayer.LastHealCancel, FBMed.Busy, sysAt, me.sys ?? "-", FBMed.kits));
				if (nome == "agachar")
				{
					Do(delegate(FBInputState s)
					{
						s.crouchToggle = true;
						return s;
					});
					yield return Wait(0.5f);
				}
				if (nome == "mirar")
				{
					hold = Vector2.zero;
					yield return Wait(0.3f);
				}
			}
			FBMed.ResetPlayer(4, infinite: false);
			me.hp = 20f;
			yield return Wait(0.6f);
			Do(delegate(FBInputState s)
			{
				s.heal = true;
				return s;
			});
			yield return Wait(0.25f);
			hold = new Vector2(0f, 1f);
			yield return Wait(0.3f);
			hold = Vector2.zero;
			yield return Wait(0.3f);
			Log($"{view2} cancelar a 0,25 s (antes de começar a subir): kits {FBMed.kits} (esperado 4) · vida {me.hp:0} (esperado 20)");
			hold = new Vector2(0f, 1f);
			yield return Wait(1f);
			Do(delegate(FBInputState s)
			{
				s.heal = true;
				return s;
			});
			yield return Wait(0.3f);
			Log($"{view2} tentar curar andando: Busy {FBMed.Busy} (esperado False)");
			hold = Vector2.zero;
			yield return Wait(0.6f);
		}
		if (FBPlayer.I.FirstPerson)
		{
			Do(delegate(FBInputState s)
			{
				s.toggleView = true;
				return s;
			});
			yield return Wait(1f);
		}
		FBCharacter bot = bots.FirstOrDefault();
		if ((bool)bot)
		{
			bot.gameObject.SetActive(value: true);
			FBMatch.Teleport(bot, me.Pos + me.transform.forward * 4f, me.Yaw + 180f);
			bot.hp = 30f;
			bot.kits = 2;
			yield return Wait(0.5f);
			FBBot component = bot.GetComponent<FBBot>();
			typeof(FBBot).GetField("medT", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(component, 3f);
			Vector3 p0 = bot.Pos;
			string view2 = "";
			for (int i = 0; i < 14; i++)
			{
				view2 += $"{bot.hp:0}/{(bot.Pos - p0).Flat().magnitude:0.00}m ";
				yield return Wait(0.25f);
			}
			Log($"bot curando (vida/deslocamento a cada 0,25 s): {view2}· kits {bot.kits} (antes 2) · clipe {bot.sys}");
		}
		_ = Camera.main;
		me.protUntil = Time.time + 4f;
		yield return Wait(0.2f);
		Log(string.Format("proteção (3ª pessoa): aura no jogador {0} malhas · anel {1} · borda {2}", FBTactics.I.AuraCount(me), FBTactics.I.GetComponentsInChildren<Transform>(includeInactive: true).Count((Transform t) => t.name == "anel_protecao" && t.gameObject.activeSelf), GameObject.Find("protecao_borda") != null));
		yield return Wait(0.5f);
		Shot("protecao_tps");
		FBCharacter item = bots.FirstOrDefault((FBCharacter b) => (bool)b && b.team == me.team && b != bot) ?? bot;
		FBCharacter item2 = bots.FirstOrDefault((FBCharacter b) => (bool)b && b.team != me.team);
		(FBCharacter, string)[] array4 = new(FBCharacter, string)[2]
		{
			(item, "aliado"),
			(item2, "inimigo")
		};
		for (int i = 0; i < array4.Length; i++)
		{
			var (b2, view2) = array4[i];
			if ((bool)b2)
			{
				b2.gameObject.SetActive(value: true);
				FBMatch.Teleport(b2, me.Pos + me.transform.forward * 3.2f + me.transform.right * ((!(view2 == "aliado")) ? 1 : (-1)), me.Yaw + 180f);
				b2.protUntil = Time.time + 4f;
				yield return Wait(0.6f);
				Log($"proteção {view2} (time {b2.team}, eu {me.team}): aura {FBTactics.I.AuraCount(b2)} malhas");
			}
		}
		yield return Wait(0.3f);
		Shot("protecao_aliado_inimigo");
		Do(delegate(FBInputState s)
		{
			s.toggleView = true;
			return s;
		});
		yield return Wait(0.8f);
		me.protUntil = Time.time + 3f;
		yield return Wait(0.6f);
		GameObject edge = GameObject.Find("protecao_borda");
		Log($"proteção (1ª pessoa): borda ativa {(bool)edge && edge.activeInHierarchy} · aura no próprio corpo {FBTactics.I.AuraCount(me)} (esperado 0)");
		Shot("protecao_fp");
		me.protUntil = 0f;
		yield return Wait(0.5f);
		Log($"proteção acabou: aura {FBTactics.I.AuraCount(me)} · borda {(bool)edge && edge.activeInHierarchy}");
		Do(delegate(FBInputState s)
		{
			s.toggleView = true;
			return s;
		});
		yield return Wait(0.5f);
		FBPlayer.TestInput = null;
		Log("fim");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.view = keepView;
		FBSession.Save();
		yield return Wait(1f);
		Application.Quit();
		void Do(Func<FBInputState, FBInputState> f)
		{
			FBInputState fBInputState = default(FBInputState);
			fBInputState.slot = -1;
			FBInputState arg = fBInputState;
			press = f(arg);
			once = true;
		}
	}
}
}
