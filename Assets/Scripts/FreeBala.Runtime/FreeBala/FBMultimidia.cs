using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBMultimidia
{
	private class Ticker : MonoBehaviour
	{
		private float t;

		private static readonly int __fbt_Update = FBTempo.Id("FBMultimidia.Ticker.Update");

		private void Update()
		{
			if (!FBTempo.On)
			{
				Update__fbt();
				return;
			}
			long t0 = FBTempo.T0();
			Update__fbt();
			FBTempo.Soma(__fbt_Update, t0);
		}

		private void Update__fbt()
		{
			if ((t += Time.unscaledDeltaTime) > 0.25f)
			{
				t = 0f;
				Tick();
			}
		}
	}

	public static bool Open;

	private static GameObject go;

	private static Transform body;

	private static string tab = "fila";

	private static FBCar car;

	private static Text nowT;

	private static Text artT;

	private static Text timeT;

	private static Text stT;

	private static Image prog;

	private static Slider volS;

	private static readonly List<string> q = new List<string>();

	private static int qi;

	private static bool shuf;

	private static string rep = "all";

	private static float vol = 0.85f;

	private static int page;

	private static bool inited;

	private static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Load();
		FBParedao.OnEnded = delegate(FBCar c)
		{
			if (c == Car())
			{
				Next(auto: true);
			}
		};
		FBParedao.OnNeedTrack = delegate(FBCar c)
		{
			FBTrack fBTrack = FBMediaLib.Find((q.Count > 0) ? q[Mathf.Clamp(qi, 0, q.Count - 1)] : null) ?? FBMediaLib.lib.FirstOrDefault((FBTrack x) => x.folder != "Recebidas") ?? FBMediaLib.lib.FirstOrDefault();
			if (fBTrack != null)
			{
				PlayTrack(fBTrack, c);
			}
		};
		FBParedao.Changed += delegate
		{
			if (Open)
			{
				Render();
			}
		};
		FBMediaLib.Refresh();
	}

	private static void Load()
	{
		q.Clear();
		string @string = PlayerPrefs.GetString("fb.midia.v1", "");
		if (string.IsNullOrEmpty(@string))
		{
			return;
		}
		Dictionary<string, object> o = Json.Parse(@string) as Dictionary<string, object>;
		foreach (object item in Json.L(o, "q") ?? new List<object>())
		{
			q.Add(item as string);
		}
		qi = Json.I(o, "i");
		shuf = Json.B(o, "shuf");
		rep = Json.S(o, "rep") ?? "all";
		vol = Json.F(o, "vol", 0.85f);
		tab = Json.S(o, "tab") ?? "fila";
	}

	private static void Save()
	{
		PlayerPrefs.SetString("fb.midia.v1", "{\"q\":[" + string.Join(",", q.Select((string x) => "\"" + x + "\"")) + string.Format("],\"i\":{0},\"shuf\":{1},\"rep\":\"{2}\",\"vol\":{3},\"tab\":\"{4}\"", qi, shuf ? "true" : "false", rep, vol.ToString(CultureInfo.InvariantCulture), tab) + "}");
	}

	private static FBCar Car()
	{
		FBCharacter player = FBMatch.Player;
		if (!player)
		{
			return null;
		}
		return FBParedao.MineCar(player.Pos) ?? FBVehicles.CarOf(player);
	}

	public static bool PlayTrack(FBTrack t, FBCar c = null)
	{
		if ((object)c == null)
		{
			c = Car();
		}
		if (!c || !FBParedao.Has(c))
		{
			FBHUD.I?.Center("ENTRE NUM CARRO COM PAREDÃO", 1.6f);
			return false;
		}
		if (!FBParedao.CanControl(c))
		{
			FBHUD.I?.Center("ESTE PAREDÃO É DE " + (FBParedao.OwnerName(c) ?? "OUTRO JOGADOR"), 1.6f);
			return false;
		}
		FBParedao.SetVol(c, vol);
		return FBParedao.Play(c, t);
	}

	private static void PlayQueue(int i)
	{
		if (q.Count != 0)
		{
			qi = (i % q.Count + q.Count) % q.Count;
			Save();
			FBTrack fBTrack = FBMediaLib.Find(q[qi]);
			if (fBTrack != null)
			{
				PlayTrack(fBTrack);
			}
			Render();
		}
	}

	private static void Next(bool auto)
	{
		if (q.Count == 0)
		{
			FBParedao.Wall wall = FBParedao.State(Car());
			if (auto && wall?.tr != null && rep != "off")
			{
				FBTrack fBTrack = FBMediaLib.Find(wall.tr.h);
				if (fBTrack != null)
				{
					PlayTrack(fBTrack);
				}
			}
		}
		else if (auto && rep == "one")
		{
			PlayQueue(qi);
		}
		else if (shuf && q.Count > 1)
		{
			int num;
			do
			{
				num = UnityEngine.Random.Range(0, q.Count);
			}
			while (num == qi);
			PlayQueue(num);
		}
		else if (auto && rep == "off" && qi == q.Count - 1)
		{
			Render();
		}
		else
		{
			PlayQueue(qi + 1);
		}
	}

	private static void Prev()
	{
		FBCar fBCar = Car();
		if (FBParedao.Position(fBCar) > 4f)
		{
			FBParedao.Seek(fBCar, 0f);
		}
		else
		{
			PlayQueue(qi - 1);
		}
	}

	public static void Toggle()
	{
		Init();
		if (Open)
		{
			UnityEngine.Object.Destroy(go);
			Open = false;
			if (!FBInput.Touch)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			return;
		}
		FBCharacter player = FBMatch.Player;
		car = Car();
		if (!car)
		{
			FBCar fBCar = FBVehicles.Nearest(player ? player.Pos : Vector3.zero, 6f);
			if ((bool)fBCar && FBParedao.Has(fBCar))
			{
				car = fBCar;
			}
		}
		if (!car || !FBParedao.Has(car))
		{
			FBHUD.I?.Center("ENTRE NUM CARRO COM PAREDÃO (OU CHEGUE PERTO DO SEU)", 2f);
			return;
		}
		FBMediaLib.Refresh();
		q.RemoveAll((string id) => FBMediaLib.Find(id) == null);
		if (qi >= q.Count)
		{
			qi = 0;
		}
		go = FBUI.Canvas("Multimidia", 35).gameObject;
		Open = true;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		go.AddComponent<Ticker>();
		Render();
	}

	private static string Mmss(float s)
	{
		int num = Mathf.Max(0, Mathf.FloorToInt(s));
		return $"{num / 60}:{num % 60:00}";
	}

	private static void Tick()
	{
		if (Open && (bool)car)
		{
			FBParedao.Wall wall = FBParedao.State(car);
			float num = FBParedao.Position(car);
			float num2 = FBParedao.Duration(car);
			if ((bool)timeT)
			{
				timeT.text = Mmss(num) + " / " + Mmss(num2);
			}
			if ((bool)prog)
			{
				prog.rectTransform.anchorMax = new Vector2((num2 > 0f) ? Mathf.Clamp01(num / num2) : 0f, 1f);
			}
			if ((bool)stT)
			{
				string text = FBParedao.Buffering(car);
				stT.text = ((text != "") ? text : ((wall == null || !wall.on) ? "DESLIGADO" : (wall.playing ? "TOCANDO NO PAREDÃO" : "PAUSADO")));
			}
		}
	}

	private static void Render()
	{
		if (!Open || !go)
		{
			return;
		}
		Transform transform = go.transform;
		foreach (Transform item3 in transform)
		{
			UnityEngine.Object.Destroy(item3.gameObject);
		}
		FBParedao.Wall wall = FBParedao.State(car);
		Transform transform2 = FBUI.Panel(transform, "central", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 520f), new Color(0.07f, 0.08f, 0.1f, 0.95f)).transform;
		FBUI.Panel(transform2, "topo", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(0f, 2f), FBUI.Gold);
		FBUI.Label(transform2, "CENTRAL MULTIMÍDIA", 20, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, -30f), new Vector2(320f, 30f), TextAnchor.MiddleLeft, title: true);
		string text = FBParedao.OwnerName(car);
		FBUI.Label(transform2, (car.id + ((text != null) ? (" · PAREDÃO DE " + text) : "")).ToUpper(), 12, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(520f, -30f), new Vector2(420f, 24f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		FBUI.Button(transform2, "✕", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(40f, 36f), Toggle);
		FBTrack fBTrack = wall?.tr;
		float num = -235f;
		FBUI.Label(FBUI.Panel(transform2, "capa", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num - 110f, 120f), new Vector2(150f, 150f), Color.HSVToRGB((float)Hue(fBTrack?.h) / 360f, 0.7f, 0.35f)).transform, "♪", 64, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f), TextAnchor.MiddleCenter, title: false, new Color(1f, 1f, 1f, 0.7f));
		FBUI.Label(transform2, "TOCANDO AGORA", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num + 85f, 180f), new Vector2(240f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Gold);
		nowT = FBUI.Label(transform2, fBTrack?.n ?? "Nenhuma faixa", 20, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num + 85f, 145f), new Vector2(240f, 50f), TextAnchor.UpperLeft, title: true);
		artT = FBUI.Label(transform2, fBTrack?.a ?? "", 14, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num + 85f, 105f), new Vector2(240f, 22f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		stT = FBUI.Label(transform2, "", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num + 85f, 75f), new Vector2(240f, 18f), TextAnchor.MiddleLeft, title: false, new Color(0.37f, 0.88f, 0.54f));
		Image bar = FBUI.Panel(transform2, "barra", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num, 10f), new Vector2(390f, 6f), new Color(1f, 1f, 1f, 0.12f));
		prog = FBUI.Panel(bar.transform, "fi", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, FBUI.Gold);
		RectTransform rectTransform = prog.rectTransform;
		Vector2 offsetMin = (prog.rectTransform.offsetMax = Vector2.zero);
		rectTransform.offsetMin = offsetMin;
		bar.gameObject.AddComponent<Button>().onClick.AddListener(delegate
		{
			RectTransform rectTransform2 = bar.rectTransform;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform2, Pointer.current?.position.ReadValue() ?? Vector2.zero, null, out var localPoint);
			FBParedao.Seek(car, Mathf.Clamp01(localPoint.x / rectTransform2.rect.width + 0.5f) * FBParedao.Duration(car));
		});
		timeT = FBUI.Label(transform2, "", 12, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num, -8f), new Vector2(390f, 18f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
		float y = -55f;
		FBUI.Button(transform2, shuf ? "ALEAT ●" : "ALEAT", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num - 160f, y), new Vector2(70f, 40f), delegate
		{
			shuf = !shuf;
			Save();
			Render();
		});
		FBUI.Button(transform2, "⏮", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num - 80f, y), new Vector2(56f, 44f), Prev);
		FBUI.Button(transform2, (wall != null && wall.playing) ? "❚❚" : "▶", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num, y), new Vector2(72f, 60f), delegate
		{
			FBParedao.Wall wall2 = FBParedao.State(car);
			if (wall2?.tr == null)
			{
				if (q.Count > 0)
				{
					PlayQueue(qi);
				}
				else
				{
					FBParedao.OnNeedTrack?.Invoke(car);
				}
			}
			else
			{
				if (!wall2.on)
				{
					FBParedao.SetOn(car, on: true);
				}
				FBParedao.Pause(car, wall2.playing);
			}
		}, primary: true);
		FBUI.Button(transform2, "⏭", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num + 80f, y), new Vector2(56f, 44f), delegate
		{
			Next(auto: false);
		});
		FBUI.Button(transform2, (rep == "off") ? "REPETIR" : ((rep == "one") ? "REP 1" : "REP ●"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num + 160f, y), new Vector2(70f, 40f), delegate
		{
			rep = ((rep == "all") ? "one" : ((rep == "one") ? "off" : "all"));
			Save();
			Render();
		});
		FBUI.Label(transform2, "VOLUME", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num - 150f, -115f), new Vector2(80f, 20f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		volS = Slider(transform2, new Vector2(num + 30f, -115f), 280f, wall?.vol ?? vol, delegate(float v)
		{
			vol = v;
			Save();
			FBParedao.SetVol(car, v);
		});
		float num2 = 230f;
		int num3 = 0;
		(string, string)[] array = new(string, string)[3]
		{
			("fila", "FILA"),
			("lib", "BIBLIOTECA"),
			("par", "PAREDÃO")
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, string) tuple = array[i];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			string kk = item;
			FBUI.Button(transform2, item2, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num2 - 145f + (float)(num3++ * 145), 200f), new Vector2(138f, 38f), delegate
			{
				tab = kk;
				page = 0;
				Save();
				Render();
			}, tab == item);
		}
		body = FBUI.Rect(transform2, "corpo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(num2, -30f), new Vector2(440f, 410f));
		if (tab == "par")
		{
			RenderWall(wall);
		}
		else
		{
			RenderList((tab == "fila") ? (from x in q.Select(FBMediaLib.Find)
				where x != null
				select x).ToList() : FBMediaLib.lib, tab == "fila");
		}
		Tick();
	}

	private static void RenderList(List<FBTrack> list, bool fila)
	{
		int num = Mathf.Max(1, (list.Count + 7 - 1) / 7);
		page = Mathf.Clamp(page, 0, num - 1);
		if (list.Count == 0)
		{
			FBUI.Label(body, fila ? "FILA VAZIA — ESCOLHA FAIXAS NA BIBLIOTECA" : ("NENHUMA MÚSICA NA BIBLIOTECA\nCOLOQUE MP3/OGG/WAV EM:\n" + FBMediaLib.Dir), 13, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(420f, 140f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
			if (!fila)
			{
				FBUI.Button(body, "ABRIR PASTA DE MÚSICAS", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(260f, 42f), OpenFolder, primary: true);
			}
			return;
		}
		string text = FBParedao.State(car)?.tr?.h;
		int num2 = page * 7;
		int num3 = 0;
		while (num2 < Mathf.Min(list.Count, (page + 1) * 7))
		{
			FBTrack t = list[num2];
			int idx = num2;
			float y = 170 - num3 * 50;
			Image image = FBUI.Panel(body, "faixa", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(436f, 46f), (t.h == text) ? new Color(0.96f, 0.67f, 0.09f, 0.16f) : new Color(1f, 1f, 1f, 0.035f));
			FBUI.Label(image.transform, t.n ?? "?", 13, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 8f), new Vector2(280f, 20f), TextAnchor.MiddleLeft, title: false, (t.h == text) ? FBUI.Gold : Color.white);
			FBUI.Label(image.transform, t.a + ((t.folder == "Recebidas") ? " · RECEBIDA" : ""), 11, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, -11f), new Vector2(280f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
			image.gameObject.AddComponent<Button>().onClick.AddListener(delegate
			{
				if (fila)
				{
					PlayQueue(idx);
				}
				else
				{
					if (!q.Contains(t.h))
					{
						q.Add(t.h);
					}
					qi = q.IndexOf(t.h);
					Save();
					PlayTrack(t);
					Render();
				}
			});
			if (fila)
			{
				FBUI.Button(image.transform, "✕", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(36f, 32f), delegate
				{
					q.RemoveAt(idx);
					if (qi >= q.Count)
					{
						qi = 0;
					}
					Save();
					Render();
				});
			}
			else
			{
				FBUI.Button(image.transform, "+", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(36f, 32f), delegate
				{
					if (!q.Contains(t.h))
					{
						q.Add(t.h);
					}
					Save();
					FBHUD.I?.Center("NA FILA", 1f);
				});
			}
			num2++;
			num3++;
		}
		if (num > 1)
		{
			FBUI.Button(body, "◀", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120f, -190f), new Vector2(60f, 34f), delegate
			{
				page--;
				Render();
			});
			FBUI.Label(body, $"{page + 1}/{num}", 13, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(120f, 30f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
			FBUI.Button(body, "▶", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, -190f), new Vector2(60f, 34f), delegate
			{
				page++;
				Render();
			});
		}
		if (fila && q.Count > 0)
		{
			FBUI.Button(body, "LIMPAR", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, -190f), new Vector2(80f, 30f), delegate
			{
				q.Clear();
				qi = 0;
				Save();
				Render();
			});
		}
		if (fila)
		{
			return;
		}
		FBUI.Button(body, "TUDO NA FILA", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-175f, -190f), new Vector2(110f, 30f), delegate
		{
			q.Clear();
			q.AddRange(list.Select((FBTrack x) => x.h));
			qi = 0;
			Save();
			PlayQueue(0);
		});
	}

	private static void RenderWall(FBParedao.Wall W)
	{
		bool flag = W?.on ?? false;
		FBUI.Button(body, flag ? "PAREDÃO LIGADO" : "PAREDÃO DESLIGADO", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(420f, 50f), delegate
		{
			FBParedao.Toggle(car);
		}, flag);
		FBUI.Label(body, "PORTA-MALAS / CAÇAMBA", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, 120f), new Vector2(200f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		bool flag2 = W?.trunk ?? true;
		FBUI.Button(body, "ABERTO", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 88f), new Vector2(120f, 34f), delegate
		{
			FBParedao.SetTrunk(car, open: true);
		}, flag2);
		FBUI.Button(body, "FECHADO", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-25f, 88f), new Vector2(120f, 34f), delegate
		{
			FBParedao.SetTrunk(car, open: false);
		}, !flag2);
		FBUI.Label(body, "LED", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, 50f), new Vector2(200f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		string[] array = new string[5] { "DESLIG.", "COR", "RGB", "DEGRADÊ", "SINC" };
		string text = W?.ledM ?? "sinc";
		for (int k = 0; k < 5; k++)
		{
			string j = FBParedao.LED_MODES[k];
			FBUI.Button(body, array[k], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-172 + k * 86, 18f), new Vector2(82f, 34f), delegate
			{
				FBParedao.SetLed(car, j);
			}, text == j);
		}
		Color[] array2 = new Color[6]
		{
			new Color(1f, 0.165f, 0.427f),
			new Color(0.96f, 0.67f, 0.09f),
			new Color(0.2f, 0.8f, 1f),
			new Color(0.3f, 1f, 0.4f),
			new Color(0.6f, 0.3f, 1f),
			Color.white
		};
		for (int l = 0; l < array2.Length; l++)
		{
			Color c = array2[l];
			FBUI.Button(body, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-175 + l * 46, -24f), new Vector2(38f, 30f), delegate
			{
				FBParedao.SetLed(car, null, c);
			}).GetComponent<Image>().color = c;
		}
		FBUI.Label(body, "VU", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, -62f), new Vector2(200f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		string[] array3 = new string[3] { "DESLIG.", "RGB", "SINC" };
		string text2 = W?.vu ?? "sinc";
		for (int m = 0; m < 3; m++)
		{
			string i = FBParedao.VU_MODES[m];
			FBUI.Button(body, array3[m], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-145 + m * 100, -94f), new Vector2(94f, 34f), delegate
			{
				FBParedao.SetVu(car, i);
			}, text2 == i);
		}
		FBUI.Label(body, "O som continua tocando quando você sai do carro. Online, quem estiver perto recebe a faixa do seu aparelho (até 30 MB).", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(420f, 50f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
	}

	private static void OpenFolder()
	{
		try
		{
			Directory.CreateDirectory(FBMediaLib.Dir);
			Application.OpenURL(new Uri(FBMediaLib.Dir).AbsoluteUri);
		}
		catch
		{
		}
	}

	private static int Hue(string h)
	{
		int num = 0;
		string text = h ?? "x";
		foreach (char c in text)
		{
			num = (num * 31 + c) % 360;
		}
		return num;
	}

	private static Slider Slider(Transform p, Vector2 pos, float w, float val, Action<float> on)
	{
		RectTransform rectTransform = FBUI.Rect(p, "slider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(w, 20f));
		Image image = FBUI.Panel(rectTransform, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.14f));
		image.rectTransform.offsetMin = new Vector2(0f, 7f);
		image.rectTransform.offsetMax = new Vector2(0f, -7f);
		RectTransform rectTransform2 = FBUI.Rect(rectTransform, "fa", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
		rectTransform2.offsetMin = new Vector2(0f, 7f);
		rectTransform2.offsetMax = new Vector2(-10f, -7f);
		Image image2 = FBUI.Panel(rectTransform2, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FBUI.Gold);
		RectTransform rectTransform3 = image2.rectTransform;
		Vector2 offsetMin = (image2.rectTransform.offsetMax = Vector2.zero);
		rectTransform3.offsetMin = offsetMin;
		RectTransform rectTransform4 = FBUI.Rect(rectTransform, "ha", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
		rectTransform4.offsetMin = new Vector2(10f, 0f);
		rectTransform4.offsetMax = new Vector2(-10f, 0f);
		Image image3 = FBUI.Panel(rectTransform4, "kn", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(18f, 18f), Color.white);
		image3.sprite = FBUI.CircleSprite;
		Slider slider = rectTransform.gameObject.AddComponent<Slider>();
		slider.fillRect = image2.rectTransform;
		slider.handleRect = image3.rectTransform;
		slider.targetGraphic = image3;
		slider.minValue = 0f;
		slider.maxValue = 1f;
		slider.value = val;
		slider.onValueChanged.AddListener(delegate(float v)
		{
			on(v);
		});
		return slider;
	}
}
}
