using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FreeBala
{
public class FBNet : MonoBehaviour
{
	public static FBNet I;

	public int myId;

	public bool open;

	public float ping = 80f;

	public string status = "CONECTANDO…";

	public readonly int[] score = new int[2];

	private readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();

	private readonly Dictionary<int, FBRemote> remotes = new Dictionary<int, FBRemote>();

	private float sendT;

	private float pingT;

	private const int F_SPRINT = 1;

	private const int F_CROUCH = 2;

	private const int F_AIM = 4;

	private const int F_SHOOT = 8;

	private const int F_GROUND = 16;

	private const int F_ALIVE = 32;

	private ClientWebSocket ws;

	private CancellationTokenSource cts;

	private float fullT;

	private string sentW = "\u0001";

	private string sentDn = "\u0001";

	private Vector3 sentP;

	private float sentYaw;

	private float sentPi;

	private int sentF = -1;

	private int sentHp = -1;

	private bool sentVe;

	private float lastSend;

	private async void Connect(string url)
	{
		ws = new ClientWebSocket();
		cts = new CancellationTokenSource();
		try
		{
			await ws.ConnectAsync(new Uri(url), cts.Token);
			open = true;
			OnOpen();
			Task.Run((Func<Task>)Receive);
		}
		catch (Exception ex)
		{
			status = "SEM CONEXÃO";
			FBHUD.I?.Center("SEM CONEXÃO: " + ex.Message, 5f);
		}
	}

	private async Task Receive()
	{
		byte[] buf = new byte[65536];
		StringBuilder sb = new StringBuilder();
		while (ws.State == WebSocketState.Open)
		{
			WebSocketReceiveResult webSocketReceiveResult;
			try
			{
				webSocketReceiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
			}
			catch
			{
				break;
			}
			if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
			{
				break;
			}
			sb.Append(Encoding.UTF8.GetString(buf, 0, webSocketReceiveResult.Count));
			if (webSocketReceiveResult.EndOfMessage)
			{
				inbox.Enqueue(sb.ToString());
				sb.Clear();
			}
		}
		open = false;
	}

	public void SendRaw(string s)
	{
		if (ws != null && ws.State == WebSocketState.Open)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, cts.Token);
		}
	}

	private void OnDestroy()
	{
		try
		{
			cts?.Cancel();
			ws?.Dispose();
		}
		catch
		{
		}
	}

	private void Awake()
	{
		I = this;
		base.gameObject.name = "FBNet";
		FBDamageEvents.OnDamage += OnDamage;
	}

	private void OnDisable()
	{
		FBDamageEvents.OnDamage -= OnDamage;
	}

	private void OnDamage(FBDamage d)
	{
		if (open && !(d.why == "net") && d.hit.valid && (bool)d.victim && d.victim.isRemote && (bool)d.source && d.source.isPlayer)
		{
			FBRemote component = d.victim.GetComponent<FBRemote>();
			if ((bool)component)
			{
				SendEv("hfx", new Dictionary<string, object>
				{
					["vid"] = component.netId,
					["p"] = R(Coords.ToWeb(d.hit.point)),
					["n"] = R(Coords.ToWeb(d.hit.normal)),
					["d"] = R(Coords.ToWeb(d.hit.dir)),
					["pt"] = d.hit.part ?? "",
					["head"] = d.head,
					["dmg"] = Mathf.RoundToInt(d.amount),
					["w"] = d.hit.weaponId ?? ""
				});
			}
		}
	}

	private void HitFx(Dictionary<string, object> m, FBRemote shooter)
	{
		int num = Json.I(m, "vid");
		if (num != myId && remotes.TryGetValue(num, out var value) && (bool)value && value.ch.alive)
		{
			FBHitInfo fBHitInfo = default(FBHitInfo);
			fBHitInfo.valid = true;
			fBHitInfo.point = Coords.FromWeb(Json.L(m, "p"));
			fBHitInfo.normal = Coords.FromWeb(Json.L(m, "n"));
			fBHitInfo.dir = Coords.FromWeb(Json.L(m, "d"));
			fBHitInfo.part = Json.S(m, "pt");
			fBHitInfo.weaponId = Json.S(m, "w");
			FBHitInfo hit = fBHitInfo;
			if ((bool)shooter)
			{
				hit.dist = Vector3.Distance(shooter.ch.Pos, hit.point);
			}
			FBDamage d = default(FBDamage);
			d.victim = value.ch;
			d.source = (shooter ? shooter.ch : null);
			d.type = FBDamageType.Bullet;
			d.amount = Json.F(m, "dmg");
			d.applied = 0f;
			d.hpBefore = value.ch.hp;
			d.hpAfter = value.ch.hp;
			d.head = Json.B(m, "head");
			d.hit = hit;
			d.why = "net";
			FBDamageEvents.Raise(in d);
		}
	}

	private void Start()
	{
		Connect(FBSession.server);
	}

	private void OnOpen()
	{
		Send(new Dictionary<string, object>
		{
			["t"] = "hello",
			["name"] = FBSession.name,
			["skin"] = FBSession.FullSkin,
			["team"] = FBSession.team,
			["code"] = ""
		});
		Send(new Dictionary<string, object> { ["t"] = "list" });
	}

	public void Send(Dictionary<string, object> o)
	{
		SendRaw(ToJson(o));
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBNet.Update");

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
		string result;
		while (inbox.TryDequeue(out result))
		{
			try
			{
				Recv(Json.Parse(result) as Dictionary<string, object>);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[FBNet] " + ex.Message);
			}
		}
		if (!open)
		{
			return;
		}
		sendT -= Time.deltaTime;
		pingT -= Time.deltaTime;
		if (pingT <= 0f)
		{
			pingT = 2f;
			Send(new Dictionary<string, object>
			{
				["t"] = "ping",
				["ts"] = Time.realtimeSinceStartup * 1000f
			});
		}
		FBPlayer i = FBPlayer.I;
		if (!(sendT > 0f) && (bool)i)
		{
			Dictionary<string, object> dictionary = BuildState(i, Time.realtimeSinceStartup);
			if (dictionary != null)
			{
				Send(dictionary);
			}
		}
	}

	public Dictionary<string, object> BuildState(FBPlayer p, float now)
	{
		FBCharacter ch = p.ch;
		int num = (ch.sprint ? 1 : 0) | (ch.crouch ? 2 : 0) | (ch.aiming ? 4 : 0) | (ch.shooting ? 8 : 0) | (ch.grounded ? 16 : 0) | (ch.alive ? 32 : 0);
		List<object> list = FBDrive.VehicleState();
		int num2 = Mathf.Max(0, Mathf.RoundToInt(ch.hp));
		string text = p.wu.Cur?.id;
		string text2 = ((ch.dance != null && !ch.dance.leaving) ? (ch.dance.clip ?? "") : "");
		float y = ch.transform.eulerAngles.y;
		float camPitch = p.camPitch;
		bool flag = now >= fullT;
		bool flag2 = flag || list != null || sentVe || num != sentF || num2 != sentHp || text != sentW || text2 != sentDn || (ch.transform.position - sentP).sqrMagnitude > 0.0004f || Mathf.Abs(Mathf.DeltaAngle(y, sentYaw)) > 0.5f || Mathf.Abs(camPitch - sentPi) > 0.5f;
		sendT = (flag2 ? 0.05f : 0.2f);
		if (!flag2 && now - lastSend < 0.2f)
		{
			return null;
		}
		float[] a = Coords.ToWeb(ch.transform.position);
		float[] a2 = Coords.ToWeb(ch.velocity);
		float x = Coords.YawToWeb(y);
		float x2 = Coords.YawToWeb(p.camYaw);
		Dictionary<string, object> dictionary = new Dictionary<string, object>
		{
			["t"] = "st",
			["p"] = R(a),
			["v"] = R(a2),
			["y"] = Round(x),
			["ay"] = Round(x2),
			["pi"] = Round(camPitch * ((float)Math.PI / 180f)),
			["f"] = num,
			["ln"] = Round(ch.lean),
			["hp"] = num2,
			["ve"] = list,
			["vt"] = null
		};
		if (flag || text != sentW)
		{
			dictionary["w"] = text;
			dictionary["ws"] = -1;
			dictionary["wa"] = "";
		}
		if (flag || text2 != sentDn)
		{
			dictionary["dn"] = text2;
			dictionary["wp"] = "";
		}
		if (flag)
		{
			dictionary["av"] = new float[2];
			fullT = now + 1f;
		}
		sentW = text;
		sentDn = text2;
		sentP = ch.transform.position;
		sentYaw = y;
		sentPi = camPitch;
		sentF = num;
		sentHp = num2;
		sentVe = list != null;
		lastSend = now;
		return dictionary;
	}

	private static float Round(float x)
	{
		return Mathf.Round(x * 100f) / 100f;
	}

	private static float[] R(float[] a)
	{
		for (int i = 0; i < a.Length; i++)
		{
			a[i] = Round(a[i]);
		}
		return a;
	}

	private void Recv(Dictionary<string, object> m)
	{
		string text = Json.S(m, "t");
		if (text == null)
		{
			return;
		}
		switch (text.Length)
		{
		case 2:
			switch (text[0])
			{
			case 'h':
				if (text == "hi")
				{
					myId = Json.I(m, "id");
				}
				break;
			case 's':
			{
				if (text == "st" && remotes.TryGetValue(Json.I(m, "id"), out var value) && (bool)value)
				{
					value.Push(m);
				}
				break;
			}
			case 'e':
				if (text == "ev")
				{
					Event(m);
				}
				break;
			}
			break;
		case 4:
			switch (text[0])
			{
			case 'p':
				if (text == "pong")
				{
					ping = Time.realtimeSinceStartup * 1000f - Json.F(m, "ts");
				}
				break;
			case 'c':
				if (text == "chat")
				{
					FBMatch.Feed(Json.S(m, "name") + ": " + Json.S(m, "msg"));
				}
				break;
			}
			break;
		case 5:
			switch (text[4])
			{
			case 's':
			{
				if (!(text == "rooms"))
				{
					break;
				}
				string text2 = null;
				foreach (object item in Json.L(m, "rooms") ?? new List<object>())
				{
					if (Json.S(item, "map") == FBSession.map && Json.I(item, "players") < Json.I(item, "max"))
					{
						text2 = Json.S(item, "id");
						break;
					}
				}
				if (text2 != null)
				{
					Send(new Dictionary<string, object>
					{
						["t"] = "join",
						["id"] = text2
					});
					break;
				}
				Send(new Dictionary<string, object>
				{
					["t"] = "create",
					["name"] = "Sala de " + FBSession.name,
					["mode"] = "tdm",
					["map"] = FBSession.map,
					["max"] = 10
				});
				break;
			}
			case '+':
				if (text == "peer+")
				{
					AddRemote(Json.O(m, "p"));
				}
				break;
			case '-':
			{
				if (!(text == "peer-"))
				{
					break;
				}
				int num2 = Json.I(m, "id");
				FBParedao.PeerLeft(num2);
				if (remotes.TryGetValue(num2, out var value2))
				{
					if ((bool)value2)
					{
						FBMatch.All.Remove(value2.ch);
						FBMatch.Remotes.Remove(value2.ch);
						UnityEngine.Object.Destroy(value2.gameObject);
					}
					remotes.Remove(num2);
				}
				break;
			}
			case 'e':
				if (text == "score")
				{
					List<object> list = Json.L(m, "teams");
					if (list != null && list.Count > 1)
					{
						score[0] = (int)Json.Fv(list[0]);
						score[1] = (int)Json.Fv(list[1]);
					}
					int num = Json.I(m, "tl");
					status = $"{num / 60}:{num % 60:00}";
				}
				break;
			}
			break;
		case 6:
		{
			if (!(text == "joined"))
			{
				break;
			}
			Dictionary<string, object> o = Json.O(m, "you");
			myId = Json.I(o, "id");
			FBCharacter fBCharacter = FBPlayer.I?.ch;
			if ((bool)fBCharacter)
			{
				fBCharacter.team = Json.I(o, "team");
			}
			foreach (object item2 in Json.L(m, "players"))
			{
				AddRemote(item2 as Dictionary<string, object>);
			}
			status = "SALA " + Json.S(Json.O(m, "room"), "id");
			FBHUD.I?.Center("ONLINE · " + status, 3f);
			break;
		}
		case 3:
			if (text == "end")
			{
				FBHUD.I?.Center("FIM DA PARTIDA", 6f);
				FBMatch.I?.EndMatch(score[FBSession.team] >= score[1 - FBSession.team], "ONLINE", score[0], score[1]);
			}
			break;
		}
	}

	private void Event(Dictionary<string, object> m)
	{
		int key = Json.I(m, "id");
		string text = Json.S(m, "e");
		remotes.TryGetValue(key, out var value);
		switch (text)
		{
		case "som":
		case "somreq":
		case "somchk":
			FBParedao.OnNet(m);
			break;
		case "hit":
			if (Json.I(m, "to") == myId)
			{
				FBCharacter fBCharacter = FBPlayer.I?.ch;
				if ((bool)fBCharacter && fBCharacter.alive)
				{
					FBCombat.DamageActor(fBCharacter, Json.F(m, "dmg"), Json.B(m, "head"), value ? value.ch : null);
				}
				break;
			}
			goto default;
		default:
			if (text == "die" && (bool)value)
			{
				value.ch.Die(null);
				int num = Json.I(m, "by");
				FBMatch.Feed(((num == myId) ? FBSession.name : ((remotes.TryGetValue(num, out var value2) && (bool)value2) ? value2.displayName : "?")) + " ✖ " + value.displayName, num == myId);
				if (num == myId)
				{
					FBMatch.Stats.kills++;
					if (Json.B(m, "head"))
					{
						FBMatch.Stats.heads++;
					}
				}
				break;
			}
			if (text == "shot" && (bool)value)
			{
				value.Shot();
				break;
			}
			if (text == "spawn" && (bool)value)
			{
				List<object> list = Json.L(m, "p");
				if (list != null)
				{
					value.Teleport(Coords.FromWeb(list));
				}
				value.ch.Revive();
				break;
			}
			if (text == "dance" && (bool)value)
			{
				string text2 = Json.S(m, "v");
				if (string.IsNullOrEmpty(text2))
				{
					value.ch.StopDance(force: true);
				}
				else
				{
					value.ch.PlayDance(text2);
				}
				break;
			}
			switch (text)
			{
			case "hfx":
				HitFx(m, value);
				return;
			case "stopdance":
				if ((bool)value)
				{
					value.ch.StopDance(force: true);
					return;
				}
				break;
			case "vo":
				return;
			}
			if (text == "glass")
			{
				Vector3 vector = Coords.FromWeb(Json.L(m, "p"));
				Collider[] array = Physics.OverlapSphere(vector, 0.6f, 4096);
				for (int i = 0; i < array.Length; i++)
				{
					array[i].GetComponent<FBGlass>()?.Break(vector, Vector3.forward, fromNet: true);
				}
			}
			break;
		}
	}

	public void SendHit(FBCharacter target, float dmg, bool head)
	{
		if (open)
		{
			FBRemote fBRemote = (target ? target.GetComponent<FBRemote>() : null);
			if ((bool)fBRemote)
			{
				Send(new Dictionary<string, object>
				{
					["t"] = "ev",
					["e"] = "hit",
					["to"] = fBRemote.netId,
					["dmg"] = Mathf.RoundToInt(dmg),
					["head"] = head,
					["w"] = FBPlayer.I?.wu.Cur?.name ?? ""
				});
			}
		}
	}

	public void SendVoice(int bank, int variation, float intensity)
	{
		if (open)
		{
			Send(new Dictionary<string, object>
			{
				["t"] = "ev",
				["e"] = "vo",
				["b"] = bank,
				["v"] = variation,
				["i"] = Mathf.RoundToInt(Mathf.Clamp01(intensity) * 100f)
			});
		}
	}

	public void SendShot(FBCharacter c = null)
	{
		if (open)
		{
			Send(new Dictionary<string, object>
			{
				["t"] = "ev",
				["e"] = "shot"
			});
		}
	}

	public void SendEv(string e, Dictionary<string, object> data)
	{
		if (open)
		{
			data["t"] = "ev";
			data["e"] = e;
			Send(data);
		}
	}

	public void SendEvent(string e)
	{
		if (open)
		{
			Send(new Dictionary<string, object>
			{
				["t"] = "ev",
				["e"] = e
			});
		}
	}

	public void SendDie(FBCharacter a, FBCharacter by, bool head)
	{
		if (open && (bool)a && a.isPlayer)
		{
			FBRemote fBRemote = (by ? by.GetComponent<FBRemote>() : null);
			Send(new Dictionary<string, object>
			{
				["t"] = "ev",
				["e"] = "die",
				["by"] = (fBRemote ? fBRemote.netId : 0),
				["head"] = head,
				["w"] = (((bool)by && by.weapon != null) ? by.weapon.name : "")
			});
		}
	}

	public void SendSpawn(FBCharacter a)
	{
		if (open && (bool)a)
		{
			Send(new Dictionary<string, object>
			{
				["t"] = "ev",
				["e"] = "spawn",
				["p"] = R(Coords.ToWeb(a.Pos)),
				["prot"] = Mathf.Max(0f, a.protUntil - Time.time)
			});
		}
	}

	public void SendGlass(FBGlass g, Vector3 p)
	{
		if (open && (bool)g)
		{
			Send(new Dictionary<string, object>
			{
				["t"] = "ev",
				["e"] = "glass",
				["p"] = R(Coords.ToWeb(p))
			});
		}
	}

	public string NameOf(int id)
	{
		if (id != myId)
		{
			if (!remotes.TryGetValue(id, out var value) || !value)
			{
				return null;
			}
			return value.displayName;
		}
		return FBSession.name;
	}

	public void Leave()
	{
		if (open)
		{
			Send(new Dictionary<string, object> { ["t"] = "leave" });
		}
	}

	private void AddRemote(Dictionary<string, object> p)
	{
		if (p == null)
		{
			return;
		}
		int num = Json.I(p, "id");
		if (num != myId && !remotes.ContainsKey(num))
		{
			string[] array = (Json.S(p, "skin") ?? "").Split('~');
			string text = array[0];
			string text2 = ((array.Length > 1) ? array[1] : "");
			if (!FBRegistry.I.HasChar(text))
			{
				text = FBRegistry.I.charIds[0];
			}
			FBCharacter fBCharacter = FBMatch.I.Spawn(text, text2, Json.I(p, "team"), Coords.FromWeb(Json.L(p, "p")), player: false);
			fBCharacter.isRemote = true;
			fBCharacter.displayName = Json.S(p, "name") ?? "Jogador";
			FBMatch.Remotes.Add(fBCharacter);
			CharacterController component = fBCharacter.GetComponent<CharacterController>();
			if ((bool)component)
			{
				UnityEngine.Object.Destroy(component);
			}
			FBWardrobe.ApplyTo(fBCharacter.gameObject, text2);
			FBRemote fBRemote = fBCharacter.gameObject.AddComponent<FBRemote>();
			fBRemote.netId = num;
			fBRemote.ch = fBCharacter;
			fBRemote.displayName = Json.S(p, "name") ?? "Jogador";
			remotes[num] = fBRemote;
		}
	}

	public static string ToJson(object o)
	{
		if (o != null)
		{
			if (!(o is string text))
			{
				if (!(o is bool))
				{
					if (!(o is float num))
					{
						if (!(o is double num2))
						{
							if (!(o is int num3))
							{
								if (!(o is long num4))
								{
									if (!(o is List<object> list))
									{
										if (!(o is float[] array))
										{
											if (o is Dictionary<string, object> dictionary)
											{
												StringBuilder stringBuilder = new StringBuilder("{");
												bool flag = true;
												foreach (KeyValuePair<string, object> item in dictionary)
												{
													if (!flag)
													{
														stringBuilder.Append(',');
													}
													flag = false;
													stringBuilder.Append(ToJson(item.Key)).Append(':').Append(ToJson(item.Value));
												}
												return stringBuilder.Append('}').ToString();
											}
											return ToJson(o.ToString());
										}
										StringBuilder stringBuilder2 = new StringBuilder("[");
										for (int i = 0; i < array.Length; i++)
										{
											if (i > 0)
											{
												stringBuilder2.Append(',');
											}
											stringBuilder2.Append(ToJson(array[i]));
										}
										return stringBuilder2.Append(']').ToString();
									}
									StringBuilder stringBuilder3 = new StringBuilder("[");
									for (int j = 0; j < list.Count; j++)
									{
										if (j > 0)
										{
											stringBuilder3.Append(',');
										}
										stringBuilder3.Append(ToJson(list[j]));
									}
									return stringBuilder3.Append(']').ToString();
								}
								return num4.ToString();
							}
							return num3.ToString();
						}
						return num2.ToString(CultureInfo.InvariantCulture);
					}
					return num.ToString(CultureInfo.InvariantCulture);
				}
				if (!(bool)o)
				{
					return "false";
				}
				return "true";
			}
			return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
		}
		return "null";
	}
}
}
