using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBOficina
{
	public static bool on;

	private static GameObject ui;

	private static FBCar car;

	private static FBTuneData T;

	private static string tab = "inicio";

	private static float camYaw = 35f;

	private static float camPitch = 14f;

	private static float camDist = 7.5f;

	private static float fov = 45f;

	private static int selSlot = -1;

	private static Transform body;

	private static readonly (string pre, string name)[] SOM_TIPOS = new(string, string)[6]
	{
		("CaixaSom", "Caixa de som"),
		("Caixa_Grave", "Caixa grave"),
		("Caixa_Medio", "Caixa média"),
		("Corneteiras", "Corneteira"),
		("TrioGoiano", "Trio goiano"),
		("HackMovel", "Rack móvel")
	};

	private static readonly string[] PRESETS = new string[18]
	{
		"#f4f4f0", "#c9cbcd", "#55595e", "#18191b", "#8e1712", "#e2231a", "#ff6a00", "#ffc400", "#d9b21c", "#2e5a3a",
		"#00c853", "#00e5ff", "#1d3f7a", "#2962ff", "#7b1fa2", "#ff2a6d", "#c9b98f", "#5d4037"
	};

	private const int SLOTS = 20;

	private static readonly Dictionary<string, Sprite> icos = new Dictionary<string, Sprite>();

	private static string sub = "";

	private static readonly (string id, string name, string ico)[] TABS = new(string, string, string)[5]
	{
		("inicio", "INÍCIO", "Oficina_AbaInicio"),
		("carro", "CARRO", "Oficina_AbaCarro"),
		("roda", "RODA", "Oficina_AbaRoda"),
		("som", "SOM", "Oficina_AbaSom"),
		("skin", "SKIN", "Oficina_AbaSkin")
	};

	private static int page;

	private static float y;

	public static void Toggle()
	{
		if (on)
		{
			Close();
			return;
		}
		FBCharacter player = FBMatch.Player;
		FBCar fBCar = FBVehicles.CarOf(player) ?? FBRole.roleCar;
		if (!fBCar || fBCar.ie)
		{
			FBHUD.I?.Center("SÓ COM O SEU CARRO DO ROLÊ (O = OFICINA)", 2f);
		}
		else if ((bool)player && Vector3.Distance(player.Pos, fBCar.transform.position) > 12f)
		{
			FBHUD.I?.Center("CHEGUE PERTO DO SEU CARRO", 1.6f);
		}
		else
		{
			Open(fBCar);
		}
	}

	public static void Open(FBCar c)
	{
		car = c;
		T = FBTune.Load(c.id) ?? new FBTuneData();
		on = true;
		FBDrive.hold = FBVehicles.CarOf(FBMatch.Player) == c;
		if (!T.somSet && (bool)c.soundWallNode)
		{
			T.som = new List<FBSomSlot>();
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		Build();
	}

	private static void Close()
	{
		on = false;
		FBDrive.hold = false;
		FBDrive.rev = false;
		if ((bool)car)
		{
			FBTune.Save(car.id, T);
		}
		if ((bool)ui)
		{
			UnityEngine.Object.Destroy(ui);
		}
		ui = null;
		if (!FBInput.Touch)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	private static void Changed()
	{
		FBTune.Apply(car, T);
		FBTune.Save(car.id, T);
	}

	public static bool Camera(Camera cam, float dt)
	{
		if (!on || !car)
		{
			return false;
		}
		Mouse current = Mouse.current;
		if (current != null && current.rightButton.isPressed)
		{
			goto IL_0051;
		}
		if (current != null && current.leftButton.isPressed)
		{
			EventSystem current2 = EventSystem.current;
			if ((object)current2 != null && !current2.IsPointerOverGameObject())
			{
				goto IL_0051;
			}
		}
		goto IL_009a;
		IL_009a:
		if (current != null)
		{
			float f = current.scroll.ReadValue().y;
			if (Mathf.Abs(f) > 0.01f)
			{
				camDist = Mathf.Clamp(camDist - Mathf.Sign(f) * 0.6f, 3.5f, 14f);
			}
		}
		Vector3 vector = car.transform.position + Vector3.up * 1f;
		Quaternion quaternion = Quaternion.Euler(camPitch, car.transform.eulerAngles.y + 180f + camYaw, 0f);
		cam.transform.position = vector - quaternion * Vector3.forward * camDist;
		cam.transform.rotation = Quaternion.LookRotation(vector - cam.transform.position);
		cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp((0f - dt) * 6f));
		return true;
		IL_0051:
		Vector2 vector2 = current.delta.ReadValue();
		camYaw += vector2.x * 0.25f;
		camPitch = Mathf.Clamp(camPitch - vector2.y * 0.2f, -5f, 70f);
		goto IL_009a;
	}

	private static Color Hex(string h)
	{
		if (!ColorUtility.TryParseHtmlString(h, out var color))
		{
			return Color.white;
		}
		return color;
	}

	private static Sprite Ico(string n)
	{
		if (icos.TryGetValue(n, out var value))
		{
			return value;
		}
		Texture2D texture2D = Resources.Load<Texture2D>("UI/Oficina/" + n);
		value = (texture2D ? Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f) : null);
		icos[n] = value;
		return value;
	}

	private static Button IcoBtn(Transform p, string ico, string label, Vector2 pos, Vector2 size, Action a, bool sel = false)
	{
		Button button = FBUI.Button(p, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, a);
		Image component = button.GetComponent<Image>();
		Sprite sprite = Ico(ico);
		if ((bool)sprite)
		{
			component.sprite = sprite;
			component.preserveAspect = true;
			component.color = (sel ? FBUI.Gold : Color.white);
		}
		else
		{
			component.color = (sel ? FBUI.Gold : new Color(1f, 1f, 1f, 0.08f));
		}
		if (!string.IsNullOrEmpty(label))
		{
			FBUI.Label(button.transform, label, 10, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -9f), new Vector2(size.x + 30f, 16f), TextAnchor.MiddleCenter, title: false, sel ? FBUI.Gold : Color.white);
		}
		return button;
	}

	public static void TestTab(string t, string s)
	{
		if (on)
		{
			tab = t;
			sub = s;
			Build();
		}
	}

	public static void TestClose()
	{
		if (on)
		{
			Close();
		}
	}

	private static void Build()
	{
		if ((bool)ui)
		{
			UnityEngine.Object.Destroy(ui);
		}
		Canvas canvas = FBUI.Canvas("Oficina", 36);
		ui = canvas.gameObject;
		Image image = FBUI.Panel(canvas.transform, "topo", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), new Vector2(0f, 64f), new Color(0f, 0f, 0f, 0.55f));
		image.rectTransform.offsetMin = new Vector2(0f, image.rectTransform.offsetMin.y);
		image.rectTransform.offsetMax = new Vector2(0f, image.rectTransform.offsetMax.y);
		FBUI.Label(image.transform, "OFICINA · " + (Json.S(car.def, "name") ?? car.id).Replace('_', ' ').ToUpperInvariant(), 21, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(260f, 0f), new Vector2(480f, 40f), TextAnchor.MiddleLeft, title: true, FBUI.Gold);
		FBUI.Label(image.transform, "FOV", 10, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-420f, 0f), new Vector2(40f, 20f), TextAnchor.MiddleRight, title: false, FBUI.Mut);
		Slider(image.transform, new Vector2(1f, 0.5f), new Vector2(-330f, 0f), 150f, Mathf.InverseLerp(25f, 70f, fov), delegate(float v)
		{
			fov = Mathf.Lerp(25f, 70f, v);
		});
		FBUI.Button(image.transform, "VOLTAR", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(140f, 40f), Close);
		for (int j = 0; j < TABS.Length; j++)
		{
			(string, string, string) tuple = TABS[j];
			string i = tuple.Item1;
			string item = tuple.Item2;
			string item2 = tuple.Item3;
			bool flag = tab == i;
			string n = item2 + ((!flag) ? "_Normal" : ((i == "inicio") ? "_Selecionado" : "_Selecionada"));
			Button button = FBUI.Button(canvas.transform, Ico(n) ? "" : item, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((j - 2) * 118, 46f), new Vector2(108f, 62f), delegate
			{
				tab = i;
				sub = "";
				selSlot = -1;
				Build();
			}, flag && !Ico(n));
			Image component = button.GetComponent<Image>();
			if ((bool)Ico(n))
			{
				component.sprite = Ico(n);
				component.preserveAspect = true;
				component.color = Color.white;
				FBUI.Label(button.transform, item, 11, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(120f, 16f), TextAnchor.MiddleCenter, title: true, flag ? FBUI.Gold : Color.white);
			}
		}
		Image image2 = FBUI.Panel(canvas.transform, "painel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-210f, 20f), new Vector2(390f, 600f), new Color(0.07f, 0.08f, 0.1f, 0.93f));
		body = image2.transform;
		y = 272f;
		if (tab == "carro")
		{
			Subs(("pintura", "PINTURA"), ("bodykit", "BODYKIT"), ("acess", "ACESSÓRIOS"));
		}
		if (tab == "roda")
		{
			Subs(("rodas", "RODAS"), ("susp", "SUSPENSÃO"));
		}
		switch (tab)
		{
		case "inicio":
			InicioTab();
			break;
		case "carro":
			if (sub == "bodykit")
			{
				BodykitTab();
			}
			else if (sub == "acess")
			{
				AcessTab();
			}
			else
			{
				PinturaTab();
			}
			break;
		case "roda":
			if (sub == "susp")
			{
				SuspTab();
			}
			else
			{
				RodasTab();
			}
			break;
		case "som":
			SomTab();
			break;
		case "skin":
			SkinTab();
			break;
		}
		Text[] componentsInChildren = image2.GetComponentsInChildren<Text>(includeInactive: true);
		foreach (Text text in componentsInChildren)
		{
			if ((bool)text.GetComponentInParent<Button>())
			{
				text.resizeTextForBestFit = true;
				text.resizeTextMinSize = 7;
				text.resizeTextMaxSize = Mathf.Min(text.fontSize, 14);
				text.horizontalOverflow = HorizontalWrapMode.Wrap;
			}
		}
	}

	private static void Subs(params (string id, string name)[] s)
	{
		if (string.IsNullOrEmpty(sub))
		{
			sub = s[0].id;
		}
		float num = 350f / (float)s.Length - 6f;
		for (int j = 0; j < s.Length; j++)
		{
			string i = s[j].id;
			FBUI.Button(body, s[j].name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-175f + num / 2f + (float)j * (num + 6f), y), new Vector2(num, 34f), delegate
			{
				sub = i;
				Build();
			}, sub == i);
		}
		y -= 44f;
	}

	private static void InicioTab()
	{
		IcoBtn(body, "Oficina_ConcertarCarro", "CONSERTAR", new Vector2(-120f, y - 22f), new Vector2(58f, 58f), delegate
		{
			FBCarDamage.Repair(car);
			FBHUD.I?.Center("CARRO CONSERTADO", 1.2f);
		});
		IcoBtn(body, T.turbo ? "Oficina_TurboON" : "Oficina_TurboOFF", T.turbo ? "TURBO ON" : "TURBO OFF", new Vector2(0f, y - 22f), new Vector2(58f, 58f), delegate
		{
			T.turbo = !T.turbo;
			Changed();
			Build();
		}, T.turbo);
		IcoBtn(body, "Oficina_MudarPlaca", "PLACA", new Vector2(120f, y - 22f), new Vector2(58f, 58f), delegate
		{
			tab = "skin";
			sub = "placa";
			Build();
		});
		y -= 84f;
		string fab = FBReb.Tracao(car.id) ?? "FWD";
		string text = (string.IsNullOrEmpty(T.tracao) ? fab : T.tracao);
		H("TRAÇÃO · de fábrica " + fab);
		(string, string, float)[] array = new(string, string, float)[3]
		{
			("FWD", "Oficina_Tracao_FWD", -120f),
			("RWD", "Oficina_Tracao_RWD", 0f),
			("4x4", "Oficina_Tracao_4x4", 120f)
		};
		for (int k = 0; k < array.Length; k++)
		{
			(string, string, float) tuple = array[k];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			float item3 = tuple.Item3;
			string j = item;
			IcoBtn(body, item2, item, new Vector2(item3, y - 16f), new Vector2(84f, 40f), delegate
			{
				T.tracao = ((j == fab) ? "" : j);
				Changed();
				Build();
			}, text == j);
		}
		y -= 62f;
		List<string> all = FBReb.Roncos;
		string fabR = FBCarSound.ProfileFor(car.id, car);
		string text2 = (string.IsNullOrEmpty(T.ronco) ? fabR : T.ronco);
		H($"RONCO ({all.Count}) · de fábrica {FBReb.RoncoNome(fabR).ToUpperInvariant()}");
		int num = Mathf.Clamp(page, 0, Mathf.Max(0, (all.Count - 1) / 8));
		int num2 = num * 8;
		int num3 = 0;
		while (num2 < Mathf.Min(all.Count, (num + 1) * 8))
		{
			string i = all[num2];
			float x = ((num3 % 2 == 0) ? (-88) : 88);
			float num4 = y - (float)(num3 / 2 * 34);
			FBUI.Button(body, ((i == fabR) ? "★ " : "") + FBReb.RoncoNome(i), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, num4), new Vector2(170f, 30f), delegate
			{
				T.ronco = ((i == fabR) ? "" : i);
				Changed();
				Build();
			}, text2 == i);
			num2++;
			num3++;
		}
		y -= 140f;
		Btns(("◀", delegate
		{
			page = Mathf.Max(0, page - 1);
		}), ($"{num + 1}/{(all.Count + 8 - 1) / 8}", delegate
		{
		}), ("▶", delegate
		{
			page = Mathf.Min((all.Count - 1) / 8, page + 1);
		}), ("FÁBRICA", delegate
		{
			T.ronco = "";
		}));
		EventTrigger t = FBUI.Button(body, "TESTAR RONCO (SEGURE)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y - 6f), new Vector2(300f, 40f), delegate
		{
		}, primary: true).gameObject.AddComponent<EventTrigger>();
		Add(t, EventTriggerType.PointerDown, delegate
		{
			FBDrive.rev = true;
		});
		Add(t, EventTriggerType.PointerUp, delegate
		{
			FBDrive.rev = false;
		});
		y -= 48f;
		FBUI.Label(body, FBDrive.hold ? "Motor ligado no elevador da oficina." : "Entre no carro para ouvir o ronco.", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(340f, 20f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
	}

	private static void PinturaTab()
	{
		H("COR DA PINTURA");
		Swatches(delegate(Color c)
		{
			T.hasPaint = true;
			T.paint = c;
			T.skin = "";
		});
		Row("METÁLICO", T.metal, delegate(float v)
		{
			T.metal = v;
		});
		Row("BRILHO", T.brilho, delegate(float v)
		{
			T.brilho = v;
		});
		H("VIDROS");
		Row("FUMÊ", Mathf.InverseLerp(0.2f, 0.95f, T.fume), delegate(float v)
		{
			T.fume = Mathf.Lerp(0.2f, 0.95f, v);
		});
		Swatches(delegate(Color c)
		{
			T.hasVidro = true;
			T.vidro = c * 0.35f;
		}, 9);
		H("FAROL");
		Btns(("BRANCO", delegate
		{
			T.hasFarol = true;
			T.farol = Color.white;
		}), ("XENON", delegate
		{
			T.hasFarol = true;
			T.farol = new Color(0.75f, 0.85f, 1f);
		}), ("AMARELO", delegate
		{
			T.hasFarol = true;
			T.farol = new Color(1f, 0.85f, 0.35f);
		}), ("TIRAR", delegate
		{
			T.hasFarol = false;
		}));
		H("NEON");
		Btns((T.neon ? "DESLIGAR NEON" : "INSTALAR NEON", delegate
		{
			T.neon = !T.neon;
		}));
		if (T.neon)
		{
			Swatches(delegate(Color c)
			{
				T.neonC = c;
			}, 9);
		}
	}

	private static void BodykitTab()
	{
		if (!FBReb.HasBodykit(car))
		{
			H("ESTE CARRO NÃO TEM BODYKIT NO REB");
			FBUI.Label(body, "No REB têm bodykit: Saveiro Cross, Gol G2, Chevette, Golf, Gol G5 e BMW 320i.", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y - 10f), new Vector2(350f, 40f), TextAnchor.UpperLeft, title: false, FBUI.Mut);
			return;
		}
		string[] array = new string[5] { "Oficina_Bodykit_Aerofolio", "Oficina_Bodykit_Capo", "Oficina_Bodykit_Frente", "Oficina_Bodykit_Lateral", "Oficina_Bodykit_Traseira" };
		for (int i = 0; i < FBReb.ZONAS.Length; i++)
		{
			string z = FBReb.ZONAS[i];
			if (!FBReb.HasZone(car, z))
			{
				continue;
			}
			int zk = i;
			List<int> list = FBReb.ZoneVariants(car, z);
			IcoBtn(body, array[i], "", new Vector2(-150f, y - 18f), new Vector2(46f, 46f), delegate
			{
			});
			FBUI.Label(body, FBReb.ZONA_NOMES[i], 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-30f, y), new Vector2(200f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
			List<(string, int)> list2 = new List<(string, int)> { ("FÁBRICA", -1) };
			for (int j = 0; j < list.Count; j++)
			{
				list2.Add(($"KIT {j + 1}", list[j]));
			}
			float num = 250f / (float)list2.Count - 6f;
			for (int k = 0; k < list2.Count; k++)
			{
				var (text, v) = list2[k];
				FBUI.Button(body, text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-100f + num / 2f + (float)k * (num + 6f) + 25f, y - 26f), new Vector2(num, 30f), delegate
				{
					T.bk[zk] = v;
					Changed();
					Build();
				}, ((T.bk != null && zk < T.bk.Length) ? T.bk[zk] : (-1)) == v);
			}
			y -= 62f;
		}
		Btns(("TUDO DE FÁBRICA", delegate
		{
			for (int l = 0; l < T.bk.Length; l++)
			{
				T.bk[l] = -1;
			}
		}));
	}

	private static void AcessTab()
	{
		bool flag = false;
		if (FBReb.HasBandeira(car))
		{
			flag = true;
			H("BANDEIRAS");
			IcoBtn(body, "Oficina_Bandeira1", "ESQUERDA", new Vector2(-120f, y - 20f), new Vector2(54f, 54f), delegate
			{
				T.bandL = !T.bandL;
				Changed();
				Build();
			}, T.bandL);
			IcoBtn(body, "Oficina_Bandeira2", "DIREITA", new Vector2(-40f, y - 20f), new Vector2(54f, 54f), delegate
			{
				T.bandR = !T.bandR;
				Changed();
				Build();
			}, T.bandR);
			for (int l = 0; l < FBReb.BANDEIRAS.Length; l++)
			{
				int k = l;
				FBUI.Button(body, FBReb.BANDEIRAS[l], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((l % 2 == 0) ? 68 : 150, y - 4f - (float)(l / 2 * 32)), new Vector2(78f, 28f), delegate
				{
					T.bandeira = k;
					if (!T.bandL && !T.bandR)
					{
						T.bandL = (T.bandR = true);
					}
					Changed();
					Build();
				}, T.bandeira == k);
			}
			y -= 80f;
		}
		int num = Mathf.Max(FBReb.Forros(car, "L"), FBReb.Forros(car, "R"));
		int num2 = Mathf.Max(FBReb.Forros(car, "L02"), FBReb.Forros(car, "R02"));
		if (num > 1 || num2 > 1)
		{
			flag = true;
			H("FORROS DAS PORTAS");
			if (num > 1)
			{
				IcoBtn(body, "Oficina_PortaFrente", "", new Vector2(-150f, y - 14f), new Vector2(40f, 40f), delegate
				{
				});
				for (int m = 0; m < num; m++)
				{
					int j = m;
					FBUI.Button(body, (m == 0) ? "ORIGINAL" : ("MODELO " + m), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-50 + m * 120, y - 14f), new Vector2(112f, 30f), delegate
					{
						T.forroF = j;
						Changed();
						Build();
					}, T.forroF == j);
				}
				y -= 46f;
			}
			if (num2 > 1)
			{
				IcoBtn(body, "Oficina_PortaTras", "", new Vector2(-150f, y - 14f), new Vector2(40f, 40f), delegate
				{
				});
				for (int n = 0; n < num2; n++)
				{
					int i = n;
					FBUI.Button(body, (n == 0) ? "ORIGINAL" : ("MODELO " + n), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-50 + n * 120, y - 14f), new Vector2(112f, 30f), delegate
					{
						T.forroT = i;
						Changed();
						Build();
					}, T.forroT == i);
				}
				y -= 46f;
			}
			FBUI.Label(body, "Abra as portas (U) para ver o forro.", 10, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y + 4f), new Vector2(340f, 16f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
			y -= 18f;
		}
		if (FBReb.HasEngate(car))
		{
			flag = true;
			H("ENGATE");
			IcoBtn(body, T.engate ? "Oficina_RemoverReboque" : "Oficina_InstalarReboque", T.engate ? "REMOVER ENGATE" : "INSTALAR ENGATE", new Vector2(0f, y - 22f), new Vector2(60f, 60f), delegate
			{
				T.engate = !T.engate;
				Changed();
				Build();
			}, T.engate);
			y -= 80f;
		}
		if (!flag)
		{
			H("ESTE CARRO NÃO TEM ACESSÓRIOS NO REB");
		}
	}

	private static void RodasTab()
	{
		H("RODAS (" + GameData.Wheels.Count + ")");
		List<string> list = GameData.Wheels.Keys.Where((string k) => FBRegistry.I.Wheel(k)).ToList();
		int num = 12;
		int num2 = Mathf.Clamp(page, 0, Mathf.Max(0, (list.Count - 1) / num));
		int num3 = num2 * num;
		int num4 = 0;
		while (num3 < Mathf.Min(list.Count, (num2 + 1) * num))
		{
			string id = list[num3];
			float x = -120 + num4 % 3 * 120;
			float num5 = y - (float)(num4 / 3 * 46);
			FBUI.Button(body, id.Replace('_', ' '), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, num5), new Vector2(114f, 40f), delegate
			{
				T.wheel = id;
				Changed();
				Build();
			}, T.wheel == id || (string.IsNullOrEmpty(T.wheel) && car.curWid == id));
			num3++;
			num4++;
		}
		y -= 190f;
		Btns(("◀", delegate
		{
			page = Mathf.Max(0, page - 1);
		}), ($"{num2 + 1}/{(list.Count + num - 1) / num}", delegate
		{
		}), ("▶", delegate
		{
			page++;
		}), ("DE FÁBRICA", delegate
		{
			T.wheel = "";
		}));
		float[] pn = FBReb.Pneus(car.id);
		int pi = ((T.pneu >= 0) ? T.pneu : Array.FindIndex(pn, (float v) => Mathf.Abs(v - 1f) < 0.001f));
		H($"TAMANHO DO PNEU · {pn[Mathf.Clamp(pi, 0, pn.Length - 1)]:0.00}×");
		Btns(("◀", delegate
		{
			T.pneu = Mathf.Max(0, pi - 1);
		}), ("DE FÁBRICA", delegate
		{
			T.pneu = -1;
			T.wsize = 1f;
		}), ("▶", delegate
		{
			T.pneu = Mathf.Min(pn.Length - 1, pi + 1);
		}));
		H("COR DO ARO");
		Swatches(delegate(Color c)
		{
			T.hasRodaCor = true;
			T.rodaCor = c;
		}, 9);
		Btns(("ORIGINAL", delegate
		{
			T.hasRodaCor = false;
		}));
	}

	private static void SuspTab()
	{
		H("DIANTEIRA");
		Row("ALTURA", Mathf.InverseLerp(-0.18f, 0.25f, T.fh), delegate(float v)
		{
			T.fh = Mathf.Lerp(-0.18f, 0.25f, v);
		});
		Row("POSIÇÃO RODA", Mathf.InverseLerp(-0.08f, 0.15f, T.foff), delegate(float v)
		{
			T.foff = Mathf.Lerp(-0.08f, 0.15f, v);
		});
		Row("CAMBAGEM", Mathf.InverseLerp(-5f, 18f, T.fcam), delegate(float v)
		{
			T.fcam = Mathf.Lerp(-5f, 18f, v);
		});
		H("TRASEIRA");
		Row("ALTURA", Mathf.InverseLerp(-0.18f, 0.25f, T.rh), delegate(float v)
		{
			T.rh = Mathf.Lerp(-0.18f, 0.25f, v);
		});
		Row("POSIÇÃO RODA", Mathf.InverseLerp(-0.08f, 0.15f, T.roff), delegate(float v)
		{
			T.roff = Mathf.Lerp(-0.08f, 0.15f, v);
		});
		Row("CAMBAGEM", Mathf.InverseLerp(-5f, 18f, T.rcam), delegate(float v)
		{
			T.rcam = Mathf.Lerp(-5f, 18f, v);
		});
		Btns(("ZERAR", delegate
		{
			T.fh = (T.rh = (T.foff = (T.roff = (T.fcam = (T.rcam = 0f)))));
		}));
	}

	private static void SkinTab()
	{
		if (sub == "placa")
		{
			H("PLACA MERCOSUL");
			GameObject gameObject = new GameObject("campo", typeof(RectTransform), typeof(Image), typeof(InputField));
			gameObject.transform.SetParent(body, worldPositionStays: false);
			RectTransform obj = (RectTransform)gameObject.transform;
			obj.anchoredPosition = new Vector2(0f, y - 10f);
			obj.sizeDelta = new Vector2(240f, 44f);
			gameObject.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.93f);
			Text text = FBUI.Label(gameObject.transform, "", 24, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true, Color.black);
			RectTransform rectTransform = text.rectTransform;
			Vector2 offsetMin = (text.rectTransform.offsetMax = Vector2.zero);
			rectTransform.offsetMin = offsetMin;
			text.supportRichText = false;
			InputField component = gameObject.GetComponent<InputField>();
			component.textComponent = text;
			component.characterLimit = 7;
			component.text = T.placa ?? "";
			component.onEndEdit.AddListener(delegate(string s)
			{
				T.placa = s.ToUpperInvariant();
				Changed();
			});
			y -= 60f;
			Btns(("ALEATÓRIA", delegate
			{
				System.Random random = new System.Random();
				T.placa = string.Format("{0}{1}{2}{3}{4}{5}{6}", "NOQR"[random.Next(4)], "ABCDEFGHIJKLMNPQRSTUVWXYZ"[random.Next(25)], "ABCDEFGHIJKLMNPQRSTUVWXYZ"[random.Next(25)], random.Next(10), "ABCDEFGHIJKLMNPQRSTUVWXYZ"[random.Next(25)], random.Next(10), random.Next(10));
			}), ("TIRAR", delegate
			{
				T.placa = "";
			}), ("SKINS", delegate
			{
				sub = "";
			}));
			return;
		}
		H("SKINS");
		List<string> list = (from s in Json.L(car.def, "skins") ?? new List<object>()
			select s as string into s
			where Resources.Load<Texture2D>("CarSkins/" + s)
			select s).ToList();
		Btns(("PINTURA (SEM SKIN)", delegate
		{
			T.skin = "";
		}), ("PLACA", delegate
		{
			sub = "placa";
		}));
		foreach (string item in list)
		{
			string i = item;
			FBUI.Button(body, item.Replace('_', ' '), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(340f, 36f), delegate
			{
				T.skin = i;
				Changed();
				Build();
			}, T.skin == i);
			y -= 40f;
		}
		if (list.Count == 0)
		{
			FBUI.Label(body, "ESTE CARRO NÃO TEM SKIN DO REB", 12, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(340f, 24f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
		}
	}

	private static void Add(EventTrigger t, EventTriggerType k, Action a)
	{
		EventTrigger.Entry entry = new EventTrigger.Entry
		{
			eventID = k
		};
		entry.callback.AddListener(delegate
		{
			a();
		});
		t.triggers.Add(entry);
	}

	private static void H(string s)
	{
		FBUI.Label(body, s, 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(350f, 20f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		y -= 26f;
	}

	private static void Row(string label, float v01, Action<float> set)
	{
		FBUI.Label(body, label, 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-115f, y), new Vector2(120f, 20f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		Slider(body, new Vector2(0.5f, 0.5f), new Vector2(55f, y), 210f, v01, delegate(float v)
		{
			set(v);
			Changed();
		});
		y -= 32f;
	}

	private static void Btns(params (string t, Action a)[] bs)
	{
		float num = Mathf.Min(170f, 350f / (float)bs.Length - 6f);
		for (int i = 0; i < bs.Length; i++)
		{
			Action a = bs[i].a;
			FBUI.Button(body, bs[i].t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-175f + num / 2f + (float)i * (num + 6f), y), new Vector2(num, 34f), delegate
			{
				a();
				Changed();
				Build();
			});
		}
		y -= 40f;
	}

	private static void Swatches(Action<Color> set, int n = 18)
	{
		for (int i = 0; i < Mathf.Min(n, PRESETS.Length); i++)
		{
			Color c = Hex(PRESETS[i]);
			float x = -160 + i % 9 * 40;
			float num = y - (float)(i / 9 * 38);
			FBUI.Button(body, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, num), new Vector2(34f, 32f), delegate
			{
				set(c);
				Changed();
				Build();
			}).GetComponent<Image>().color = c;
		}
		y -= (Mathf.Min(n, PRESETS.Length) + 8) / 9 * 38 + 4;
	}

	private static Slider Slider(Transform p, Vector2 anchor, Vector2 pos, float w, float val, Action<float> on)
	{
		RectTransform rectTransform = FBUI.Rect(p, "slider", anchor, anchor, pos, new Vector2(w, 20f));
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
		slider.value = Mathf.Clamp01(val);
		slider.onValueChanged.AddListener(delegate(float v)
		{
			on(v);
		});
		return slider;
	}

	private static void SomTab()
	{
		(Vector3 p, float w, bool bed)? a = FBTune.SomAnchor(car.def);
		if (!a.HasValue)
		{
			H("ESTE CARRO NÃO TEM LUGAR PARA SOM");
			return;
		}
		if (!T.somSet)
		{
			T.somSet = true;
			FBTuneData t = T;
			if (t.som == null)
			{
				t.som = new List<FBSomSlot>();
			}
		}
		H($"SLOTS {T.som.Count}/{20}");
		for (int j = 0; j < T.som.Count; j++)
		{
			int i = j;
			float x2 = -140 + j % 5 * 70;
			float num = y - (float)(j / 5 * 34);
			FBUI.Button(body, (j + 1).ToString() ?? "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x2, num), new Vector2(62f, 30f), delegate
			{
				selSlot = i;
				Build();
			}, selSlot == i);
		}
		y -= (T.som.Count + 4) / 5 * 34 + 6;
		FBSomSlot s;
		if (selSlot >= 0 && selSlot < T.som.Count)
		{
			s = T.som[selSlot];
			H("MÓDULO " + (selSlot + 1) + " · " + s.m.Replace('_', ' '));
			Btns(("◀", delegate
			{
				M(new Vector3(-0.05f, 0f, 0f));
			}), ("▶", delegate
			{
				M(new Vector3(0.05f, 0f, 0f));
			}), ("FRENTE", delegate
			{
				M(new Vector3(0f, 0f, 0.05f));
			}), ("TRÁS", delegate
			{
				M(new Vector3(0f, 0f, -0.05f));
			}));
			Btns(("SOBE", delegate
			{
				M(new Vector3(0f, 0.05f, 0f));
			}), ("DESCE", delegate
			{
				M(new Vector3(0f, -0.05f, 0f));
			}), ("GIRA", delegate
			{
				s.r = (s.r + 15f) % 360f;
			}), ("TIRAR", delegate
			{
				T.som.RemoveAt(selSlot);
				selSlot = -1;
			}));
			Row("TAMANHO", Mathf.InverseLerp(0.5f, 1.6f, s.s), delegate(float v)
			{
				s.s = Mathf.Lerp(0.5f, 1.6f, v);
			});
		}
		if (T.som.Count < 20)
		{
			H("ADICIONAR");
			List<string> source = GameData.Som.Keys.Where((string k) => FBRegistry.I.Som(k)).ToList();
			(string, string)[] sOM_TIPOS = SOM_TIPOS;
			for (int l = 0; l < sOM_TIPOS.Length; l++)
			{
				(string, string) tuple = sOM_TIPOS[l];
				string pre = tuple.Item1;
				string item = tuple.Item2;
				List<string> list = source.Where((string m) => m.StartsWith(pre)).ToList();
				if (list.Count == 0)
				{
					continue;
				}
				int idx = 0;
				string arg = item;
				FBUI.Button(body, $"{arg} ({list.Count})", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(340f, 32f), delegate
				{
					string text = list[(T.som.Count((FBSomSlot x) => x.m.StartsWith(pre)) + idx) % list.Count];
					Dictionary<string, object> o2 = Json.O(GameData.Som, text);
					List<object> list2 = Json.L(o2, "min");
					List<object> list3 = Json.L(o2, "max");
					float num2 = Json.Fv(list3[0]) - Json.Fv(list2[0]);
					float num3 = Json.Fv(list3[1]) - Json.Fv(list2[1]);
					int num4 = 0;
					float num5 = 0f;
					float num6 = T.som.Where((FBSomSlot o) => Mathf.Abs(o.p.y) < 0.01f).Sum(delegate(FBSomSlot o)
					{
						Dictionary<string, object> o3 = Json.O(GameData.Som, o.m);
						return (Json.Fv(Json.L(o3, "max")[0]) - Json.Fv(Json.L(o3, "min")[0])) * o.s;
					});
					if (num6 + num2 <= a.Value.w)
					{
						num5 = (0f - a.Value.w) / 2f + num6 + num2 / 2f;
					}
					else
					{
						num4 = 1;
						num5 = 0f;
					}
					T.som.Add(new FBSomSlot
					{
						m = text,
						p = new Vector3(num5, (float)num4 * (num3 + 0.02f) - Json.Fv(list2[1]), 0f - Json.Fv(list2[2])),
						r = 0f,
						s = 1f
					});
					selSlot = T.som.Count - 1;
					Changed();
					Build();
				});
				y -= 36f;
			}
		}
		H("CORES");
		Btns(("CAIXA", delegate
		{
			T.hasCaixa = true;
			T.caixa = Hex(PRESETS[UnityEngine.Random.Range(0, PRESETS.Length)]);
		}), ("FALANTE", delegate
		{
			T.hasFalante = true;
			T.falante = Hex(PRESETS[UnityEngine.Random.Range(0, PRESETS.Length)]);
		}), ("CORNETA", delegate
		{
			T.hasCorneta = true;
			T.corneta = Hex(PRESETS[UnityEngine.Random.Range(0, PRESETS.Length)]);
		}), ("ORIGINAIS", delegate
		{
			T.hasCaixa = (T.hasFalante = (T.hasCorneta = false));
		}));
		FBUI.Label(body, "LED / VU e músicas: central multimídia (K).", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(340f, 20f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
		void M(Vector3 d)
		{
			s.p += d;
			FBTune.BuildSom(car, T);
			FBTune.Save(car.id, T);
		}
	}
}
}
