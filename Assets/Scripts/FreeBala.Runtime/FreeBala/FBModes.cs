using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBModes
{
	public class TacPt
	{
		public Vector3 pos;

		public float r = 7f;

		public float cap;

		public int owner = -1;

		public string id;

		public FBMarker mk;

		public int n0;

		public int n1;

		public bool contested;
	}

	public class Tatico : FBMode
	{
		private class Order
		{
			public TacPt pt;

			public float ang;

			public float rad;

			public float until;

			public bool defend;
		}

		private const int LIMIT = 250;

		private const float TIME = 600f;

		private const float CAP_T = 8f;

		private const float TICK = 2f;

		private readonly List<TacPt> pts = new List<TacPt>();

		private readonly int[] score = new int[2];

		private float t = 600f;

		private float tick;

		private readonly List<(float t, int team, string type)> q = new List<(float, int, string)>();

		private readonly Dictionary<FBCharacter, Order> orders = new Dictionary<FBCharacter, Order>();

		public override List<Vector4> Points => pts.Select((TacPt p) => new Vector4(p.pos.x, p.pos.y, p.pos.z, p.contested ? 3 : ((p.owner >= 0) ? ((p.owner == FBSession.team) ? 1 : 2) : 0))).ToList();

		public Tatico(FBMatch g)
			: base(g)
		{
			name = "TÁTICO";
			respawn = true;
			autoRespawn = 6f;
		}

		private static Color TeamCol(int team)
		{
			if (team >= 0)
			{
				if (team != FBSession.team)
				{
					return FBMode.RED;
				}
				return FBMode.BLUE;
			}
			return new Color(0.85f, 0.85f, 0.85f);
		}

		public override void Setup()
		{
			foreach (TacPt item in TacticalLayout(G))
			{
				item.mk = FBMarker.Make(item.pos, TeamCol(-1), item.r, 60f);
				item.mk.Label(item.id);
				pts.Add(item);
			}
			int team = FBSession.team;
			int team2 = 1 - team;
			G.SpawnSquad(team, 4, "patrol");
			G.SpawnSquad(team2, 5, "patrol");
			FBHUD.I?.MatchToast(string.Format("CONQUISTA · DOMINE OS PONTOS {0} · {1} PONTOS", string.Join(" ", pts.Select((TacPt p) => p.id)), 250), FBMode.GOLD, 4f);
			Debug.Log("[FreeBala][tatico] pontos: " + string.Join(" · ", pts.Select((TacPt p) => $"{p.id} {p.pos.x:0},{p.pos.z:0} r{p.r:0.0}")) + $" · menor distância entre pontos {MinSep():0} m");
		}

		private float MinSep()
		{
			float num = 1E+09f;
			for (int i = 0; i < pts.Count; i++)
			{
				for (int j = i + 1; j < pts.Count; j++)
				{
					num = Mathf.Min(num, (pts[i].pos - pts[j].pos).Flat().magnitude);
				}
			}
			return num;
		}

		private static bool Inside(FBCharacter a, TacPt p)
		{
			if ((a.Pos - p.pos).Flat().magnitude < p.r)
			{
				return Mathf.Abs(a.Pos.y - p.pos.y) < 4f;
			}
			return false;
		}

		public override Vector3? Goal(FBCharacter b)
		{
			if (b.team > 1 || pts.Count == 0)
			{
				return null;
			}
			orders.TryGetValue(b, out var value);
			if (value == null || Time.time > value.until || (!value.defend && value.pt.owner == b.team && value.pt.cap * Sgn(b.team) >= 0.999f && !value.pt.contested) || (value.defend && !Threatened(value.pt, b.team)))
			{
				value = (orders[b] = Assign(b, value));
			}
			if (value == null)
			{
				return null;
			}
			Vector3 at;
			return FBNav.Find(value.pt.pos + new Vector3(Mathf.Sin(value.ang), 0f, Mathf.Cos(value.ang)) * value.rad, 3f, out at) ? at : value.pt.pos;
		}

		public override bool Urgent(FBCharacter b)
		{
			if (orders.TryGetValue(b, out var value) && value != null && value.defend)
			{
				return (b.Pos - value.pt.pos).Flat().magnitude > value.pt.r;
			}
			return false;
		}

		public override Vector3? UrgentGoal(FBCharacter b)
		{
			return Goal(b);
		}

		private static float Sgn(int team)
		{
			return (team == 0) ? 1 : (-1);
		}

		private bool Threatened(TacPt p, int team)
		{
			if (p.owner == team)
			{
				if (!p.contested)
				{
					return p.cap * Sgn(team) < 0.98f;
				}
				return true;
			}
			return false;
		}

		public string OrdersDiag()
		{
			return string.Join(" · ", pts.Select((TacPt p) => string.Format("{0}[{1}{2}] {3}×{4}", p.id, (p.owner < 0) ? "-" : ((p.owner == FBSession.team) ? "nosso" : "deles"), p.contested ? ",disputa" : "", orders.Count((KeyValuePair<FBCharacter, Order> kv) => (bool)kv.Key && kv.Key.alive && kv.Key.team == FBSession.team && kv.Value?.pt == p), orders.Count((KeyValuePair<FBCharacter, Order> kv) => (bool)kv.Key && kv.Key.alive && kv.Key.team != FBSession.team && kv.Value?.pt == p)))) + $" · placar {score[FBSession.team]}×{score[1 - FBSession.team]}";
		}

		private Order Assign(FBCharacter b, Order old)
		{
			Dictionary<TacPt, int> dictionary = pts.ToDictionary((TacPt p) => p, (TacPt p) => orders.Count((KeyValuePair<FBCharacter, Order> kv) => (bool)kv.Key && kv.Key != b && kv.Key.alive && kv.Key.team == b.team && kv.Value != null && kv.Value.pt == p));
			TacPt tacPt = null;
			float num = -1E+09f;
			bool defend = false;
			foreach (TacPt pt in pts)
			{
				float magnitude = (b.Pos - pt.pos).Flat().magnitude;
				bool num2 = pt.owner == b.team;
				bool flag = Threatened(pt, b.team);
				if (!num2 || flag)
				{
					float num3 = (float)(flag ? 70 : ((pt.owner < 0) ? 55 : 40)) - magnitude * 0.35f - (float)(dictionary[pt] * (flag ? 10 : 18)) + UnityEngine.Random.Range(-8f, 8f);
					if (old != null && old.pt == pt)
					{
						num3 += 6f;
					}
					if (num3 > num)
					{
						num = num3;
						tacPt = pt;
						defend = flag;
					}
				}
			}
			if (tacPt == null)
			{
				tacPt = pts.OrderBy((TacPt p) => (b.Pos - p.pos).Flat().magnitude + UnityEngine.Random.Range(0f, 25f)).First();
			}
			return new Order
			{
				pt = tacPt,
				defend = defend,
				ang = UnityEngine.Random.Range(0f, (float)Math.PI * 2f),
				rad = UnityEngine.Random.Range(0.25f, 0.8f) * tacPt.r,
				until = Time.time + UnityEngine.Random.Range(12f, 22f)
			};
		}

		public override Vector3 SpawnPoint()
		{
			int my = FBSession.team;
			List<FBCharacter> en = (from a in FBMatch.Actors()
				where a.alive && a.team == 1 - my
				select a).ToList();
			List<TacPt> list = pts.Where((TacPt p) => p.owner == my && !p.contested && en.All((FBCharacter e) => (e.Pos - p.pos).Flat().magnitude > 35f)).ToList();
			if (list.Count > 0)
			{
				List<Vector3> list2 = G.TeamSpawns(1 - my);
				Vector3 ec = list2.Aggregate(Vector3.zero, (Vector3 s2, Vector3 p) => s2 + p) / Mathf.Max(1, list2.Count);
				TacPt tacPt = list.OrderBy((TacPt p) => (p.pos - ec).Flat().magnitude).First();
				Vector3? vector = FBNav.RandomPoint(tacPt.pos, tacPt.r + 6f);
				if (vector.HasValue)
				{
					return vector.GetValueOrDefault();
				}
			}
			return base.SpawnPoint();
		}

		private Vector3 BotSpawn(int team)
		{
			List<FBCharacter> en = (from a in FBMatch.Actors()
				where a.alive && a.team == 1 - team
				select a).ToList();
			List<TacPt> list = pts.Where((TacPt p) => p.owner == team && !p.contested && en.All((FBCharacter e) => (e.Pos - p.pos).Flat().magnitude > 40f)).ToList();
			if (list.Count > 0 && UnityEngine.Random.value < 0.6f)
			{
				Vector3? vector = FBNav.RandomPoint(FBUtil.Pick(list).pos, 12f);
				if (vector.HasValue)
				{
					return vector.GetValueOrDefault();
				}
			}
			return G.NextSpot(team);
		}

		public override void OnKill(FBCharacter v, FBCharacter k, bool head)
		{
			if ((bool)k && k != v && k.team < 2 && k.team != v.team)
			{
				score[k.team]++;
			}
			orders.Remove(v);
			if (v.isPlayer || v.team >= 2)
			{
				return;
			}
			q.Add((7f, v.team, v.skinId));
			G.Invoke2(6f, delegate
			{
				if ((bool)v)
				{
					FBMatch.All.Remove(v);
					FBMatch.Bots.Remove(v);
					UnityEngine.Object.Destroy(v.gameObject);
				}
			});
		}

		public override void Update(float dt)
		{
			t -= dt;
			for (int num = q.Count - 1; num >= 0; num--)
			{
				(float, int, string) value = q[num];
				value.Item1 -= dt;
				if (value.Item1 <= 0f)
				{
					G.MakeBot(value.Item3, value.Item2, BotSpawn(value.Item2), "patrol");
					q.RemoveAt(num);
				}
				else
				{
					q[num] = value;
				}
			}
			List<FBCharacter> source = (from a in FBMatch.Actors()
				where a.alive && !a.knocked && a.team < 2
				select a).ToList();
			FBCharacter player = FBMatch.Player;
			TacPt tacPt = null;
			foreach (TacPt p in pts)
			{
				p.mk.Spin(dt * 0.3f);
				p.n0 = source.Count((FBCharacter a) => a.team == 0 && Inside(a, p));
				p.n1 = source.Count((FBCharacter a) => a.team == 1 && Inside(a, p));
				p.contested = p.n0 > 0 && p.n1 > 0;
				int owner = p.owner;
				if (!p.contested && (p.n0 > 0 || p.n1 > 0))
				{
					float num2 = Mathf.Max(p.n0, p.n1) switch
					{
						2 => 1.5f, 
						1 => 1f, 
						_ => 1.8f, 
					} / 8f;
					p.cap = Mathf.MoveTowards(p.cap, (p.n0 > 0) ? 1 : (-1), num2 * dt);
				}
				else if (!p.contested)
				{
					p.cap = Mathf.MoveTowards(p.cap, (p.owner < 0) ? 0f : Sgn(p.owner), dt / 30f);
				}
				if (p.cap >= 1f)
				{
					p.owner = 0;
				}
				else if (p.cap <= -1f)
				{
					p.owner = 1;
				}
				else if (p.owner >= 0 && p.cap * Sgn(p.owner) <= 0f)
				{
					p.owner = -1;
				}
				if (p.owner != owner)
				{
					Changed(p, owner);
				}
				p.mk.SetColor(p.contested ? FBMode.GOLD : ((p.owner >= 0) ? TeamCol(p.owner) : Color.Lerp(TeamCol(-1), TeamCol((!(p.cap > 0f)) ? 1 : 0), Mathf.Abs(p.cap))));
				if ((bool)player && player.alive && Inside(player, p))
				{
					tacPt = p;
				}
			}
			tick += dt;
			if (tick >= 2f)
			{
				tick -= 2f;
				foreach (TacPt pt in pts)
				{
					if (pt.owner >= 0)
					{
						score[pt.owner]++;
					}
				}
			}
			int team = FBSession.team;
			if (score[0] >= 250 || score[1] >= 250 || t <= 0f)
			{
				G.EndMatch(score[team] > score[1 - team], "CONQUISTA", score[team], score[1 - team]);
			}
			else if (tacPt != null)
			{
				float num3 = tacPt.cap * Sgn(team);
				string s = (tacPt.contested ? ("PONTO " + tacPt.id + " · DISPUTADO") : ((tacPt.owner == team && num3 >= 0.999f) ? ("PONTO " + tacPt.id + " · DOMINADO") : ((tacPt.owner == 1 - team) ? $"NEUTRALIZANDO {tacPt.id} {Mathf.RoundToInt((1f - Mathf.Abs(tacPt.cap)) * 100f)}%" : $"DOMINANDO {tacPt.id} {Mathf.RoundToInt(Mathf.Max(0f, num3) * 100f)}%")));
				FBHUD.I?.SetBox(s, on: true);
				if (tacPt.contested)
				{
					FBHUD.I?.MatchToast("ELIMINE OS INIMIGOS NO PONTO " + tacPt.id, FBMode.RED, 0.4f);
				}
			}
			else
			{
				FBHUD.I?.SetBox("", on: false);
			}
		}

		private void Changed(TacPt p, int was)
		{
			int team = FBSession.team;
			foreach (FBCharacter item in (from kv in orders
				where kv.Value != null && kv.Value.pt == p
				select kv.Key).ToList())
			{
				orders.Remove(item);
			}
			if (p.owner == team)
			{
				FBAudio.Play2D("click", 0.9f);
				FBHUD.I?.Toast("PONTO " + p.id + " DOMINADO", "SUA EQUIPE CONTROLA O PONTO", "CONQUISTA", FBMode.BLUE);
			}
			else if (p.owner == 1 - team)
			{
				FBHUD.I?.Toast("PONTO " + p.id + " PERDIDO", "O INIMIGO DOMINOU O PONTO", "CONQUISTA", FBMode.RED);
			}
			else if (was == team)
			{
				FBHUD.I?.MatchToast("PONTO " + p.id + " NEUTRALIZADO PELO INIMIGO", FBMode.RED, 2.5f);
			}
			else if (was == 1 - team)
			{
				FBHUD.I?.MatchToast("PONTO " + p.id + " NEUTRALIZADO", FBMode.BLUE, 2f);
			}
		}

		private static string Hex(Color c)
		{
			return ColorUtility.ToHtmlStringRGB(c);
		}

		public override void Hud()
		{
			int team = FBSession.team;
			string center = string.Join(" ", pts.Select((TacPt p) => "<color=#" + Hex(p.contested ? FBMode.GOLD : ((p.owner >= 0) ? TeamCol(p.owner) : new Color(0.75f, 0.75f, 0.75f))) + ">" + p.id + "</color>"));
			FBHUD.I?.SetMatch("CONQUISTA · " + G.MapName + " · " + FBMode.Mmss(t), score[team].ToString(), center, score[1 - team].ToString(), (float)score[team] / 250f, (float)score[1 - team] / 250f, null);
		}

		public override void Dispose()
		{
			foreach (TacPt pt in pts)
			{
				if ((bool)pt.mk)
				{
					UnityEngine.Object.Destroy(pt.mk.gameObject);
				}
			}
			pts.Clear();
			orders.Clear();
		}
	}

	public class Classico : FBMode
	{
		private const int LIMIT = 40;

		private const float TIME = 480f;

		private readonly int[] score = new int[2];

		private float t = 480f;

		private readonly List<(float t, int team, string type)> q = new List<(float, int, string)>();

		public Classico(FBMatch g)
			: base(g)
		{
			name = "CLÁSSICO";
			respawn = true;
			autoRespawn = 5f;
		}

		public override void Setup()
		{
			G.SpawnSquad(FBSession.team, 3, "hunt");
			G.SpawnSquad(1 - FBSession.team, 4, "hunt");
			FBHUD.I?.MatchToast($"MATA-MATA · {40} ABATES", FBMode.GOLD, 3.5f);
		}

		public override void OnKill(FBCharacter v, FBCharacter k, bool head)
		{
			if ((bool)k && k.team != v.team && k.team < 2)
			{
				score[k.team]++;
			}
			if (!v.isPlayer)
			{
				q.Add((4f, v.team, v.skinId));
				G.Invoke2(6f, delegate
				{
					if ((bool)v)
					{
						FBMatch.All.Remove(v);
						FBMatch.Bots.Remove(v);
						UnityEngine.Object.Destroy(v.gameObject);
					}
				});
			}
			if (score[0] >= 40 || score[1] >= 40)
			{
				G.EndMatch(score[FBSession.team] >= 40, "MATA-MATA", score[0], score[1]);
			}
		}

		public override void Update(float dt)
		{
			t -= dt;
			if (t <= 0f)
			{
				G.EndMatch(score[FBSession.team] > score[1 - FBSession.team], "MATA-MATA", score[0], score[1]);
				return;
			}
			for (int num = q.Count - 1; num >= 0; num--)
			{
				(float, int, string) value = q[num];
				value.Item1 -= dt;
				if (value.Item1 <= 0f)
				{
					G.MakeBot(value.Item3, value.Item2, G.NextSpot(value.Item2), "hunt");
					q.RemoveAt(num);
				}
				else
				{
					q[num] = value;
				}
			}
			FBHUD.I?.SetBox($"ABATES {score[FBSession.team]}/{40}", on: true);
		}

		public override void Hud()
		{
			FBHUD.I?.SetMatch("MATA-MATA · " + G.MapName, score[0].ToString(), FBMode.Mmss(t), score[1].ToString(), (float)score[0] / 40f, (float)score[1] / 40f, null);
		}
	}

	public class Squad : FBMode
	{
		private const int WIN = 4;

		private const float ROUND_T = 150f;

		private const float BUY_T = 15f;

		private static readonly FBBrCore.Phase[] PH = new FBBrCore.Phase[4]
		{
			new FBBrCore.Phase
			{
				wait = 22f,
				shrink = 22f,
				k = 0.6f,
				dmg = 2f
			},
			new FBBrCore.Phase
			{
				wait = 14f,
				shrink = 18f,
				k = 0.5f,
				dmg = 4f
			},
			new FBBrCore.Phase
			{
				wait = 12f,
				shrink = 15f,
				k = 0.4f,
				dmg = 8f
			},
			new FBBrCore.Phase
			{
				wait = 10f,
				shrink = 14f,
				k = 0.01f,
				dmg = 14f
			}
		};

		private readonly int[] wins = new int[2];

		private int round;

		private string phase = "pre";

		private float pt;

		private float rt = 150f;

		private readonly List<int> results = new List<int>();

		private int lastSec;

		private FBZone Z;

		public override FBZone.Z Zone => Z?.zone;

		public override int Round => round;

		public Squad(FBMatch g)
			: base(g)
		{
			name = "4V4 ZONA";
			respawn = false;
			rounds = true;
			econ = true;
		}

		private (Vector3 c, float d) Center()
		{
			Vector3 vector = Cen(G.TeamSpawns(0));
			Vector3 vector2 = Cen(G.TeamSpawns(1));
			Vector3 mid = (vector + vector2) * 0.5f;
			List<Vector3> list = FBZone.AlleyPoints(G);
			List<Vector3> list2 = ((list.Count > 0) ? list : FBNav.Points());
			Vector3 vector3 = ((list2.Count > 0) ? list2.OrderBy((Vector3 p) => (p - mid).magnitude).First() : mid);
			return (c: vector3, d: (vector - vector2).magnitude + (mid - vector3).magnitude);
			static Vector3 Cen(List<Vector3> a)
			{
				return a.Aggregate(Vector3.zero, (Vector3 s, Vector3 p) => s + p) / Mathf.Max(1, a.Count);
			}
		}

		private void StartRound()
		{
			round++;
			phase = "buy";
			pt = 15f;
			rt = 150f;
			var (c, num) = Center();
			Z.Start(c, Mathf.Max(45f, num * 0.75f));
			G.ResetRound();
			foreach (FBCharacter item in FBMatch.Actors())
			{
				Z.zone.r = Mathf.Max(Z.zone.r, (item.Pos - Z.zone.c).Flat().magnitude + 15f);
			}
			Z.Plan();
			G.freeze = true;
			FBHUD.I?.MatchToast($"RODADA {round} · FASE DE COMPRA", FBMode.GOLD, 3f);
			lastSec = 0;
			FBAudio.Synth((wins[0] == 3 || wins[1] == 3) ? "lastround" : "round");
			FBEconomy.BuyPhase(on: true);
		}

		private void EndRound(int winner, string why)
		{
			if (!(phase != "live"))
			{
				phase = "post";
				pt = 4.5f;
				results.Add(winner);
				wins[winner]++;
				int team = FBSession.team;
				bool flag = winner == team;
				FBEconomy.Round(winner, round);
				FBHUD.I?.Toast(flag ? "RODADA VENCIDA" : "RODADA PERDIDA", $"{wins[team]} × {wins[1 - team]}", why, flag ? FBMode.GREEN : FBMode.RED);
				FBHUD.I?.MatchToast(flag ? "VITÓRIA NA RODADA" : "DERROTA NA RODADA", flag ? FBMode.GREEN : FBMode.RED, 4f);
				FBAudio.Synth(flag ? "win" : "lose");
			}
		}

		public override void Setup()
		{
			Z = new FBZone(G, PH);
			Z.Fx();
			G.SpawnSquad(FBSession.team, 3, "squad");
			G.SpawnSquad(1 - FBSession.team, 4, "squad");
			FBEconomy.Reset();
			StartRound();
		}

		public override Vector3? Goal(FBCharacter b)
		{
			FBZone.Z z = Z?.zone;
			if (z == null)
			{
				return null;
			}
			float magnitude = (b.Pos - z.c).Flat().magnitude;
			Vector3 value = ((z.to.HasValue && z.st == "shrink") ? z.to.Value.c : z.c);
			if (!(magnitude > z.r * 0.75f))
			{
				return null;
			}
			return value;
		}

		public override bool Urgent(FBCharacter b)
		{
			FBZone.Z z = Z?.zone;
			if (z != null)
			{
				return (b.Pos - z.c).Flat().magnitude > z.r * 0.92f;
			}
			return false;
		}

		public override Vector3? UrgentGoal(FBCharacter b)
		{
			return Z?.zone?.c;
		}

		public override void OnKill(FBCharacter v, FBCharacter k, bool head)
		{
			if (phase != "live")
			{
				return;
			}
			for (int i = 0; i < 2; i++)
			{
				if (!Alive(i).Any())
				{
					EndRound(1 - i, "TIME ELIMINADO");
				}
			}
		}

		public override void Update(float dt)
		{
			Z.Anim(dt);
			if (phase == "buy")
			{
				pt -= dt;
				FBEconomy.ShopTime(pt);
				FBHUD.I?.SetBox((pt > 3.5f) ? $"FASE DE COMPRA · {Mathf.CeilToInt(pt)}s · B = LOJA" : $"COMEÇA EM {Mathf.CeilToInt(pt)}", on: true);
				int num = Mathf.CeilToInt(pt);
				if (num != lastSec && pt > 0f)
				{
					lastSec = num;
					if (num <= 10 && num > 5)
					{
						FBAudio.Synth("tick");
					}
					if (num == 5)
					{
						FBAudio.Synth("count5");
					}
					if (num <= 3 && num >= 1)
					{
						FBAudio.Synth("beep", num);
						FBHUD.I?.Countdown(num.ToString());
					}
					if (num == 3)
					{
						FBAudio.Synth("riser", 3);
					}
				}
				if (pt <= 0f)
				{
					phase = "live";
					FBEconomy.BuyPhase(on: false);
					G.freeze = false;
					FBHUD.I?.SetBox("", on: false);
					FBHUD.I?.MatchToast("VALENDO!", FBMode.GREEN, 1.5f);
					FBAudio.Synth("go");
					FBHUD.I?.Countdown("VALENDO!");
					G.OnLive();
				}
			}
			else if (phase == "post")
			{
				pt -= dt;
				if (pt <= 0f)
				{
					int team = FBSession.team;
					if (wins[0] >= 4 || wins[1] >= 4)
					{
						G.EndMatch(wins[team] >= 4, "4V4 ZONA", wins[0], wins[1]);
						return;
					}
					StartRound();
				}
			}
			else
			{
				rt -= dt;
				Z.Step(dt);
				if (rt <= 0f)
				{
					float num2 = H(0);
					float num3 = H(1);
					EndRound(((num2 == num3) ? (Alive(0).Count() < Alive(1).Count()) : (!(num2 > num3))) ? 1 : 0, "TEMPO ESGOTADO");
				}
			}
			Z.Visual();
			float H(int t)
			{
				return Alive(t).Sum((FBCharacter a) => Mathf.Max(0f, a.hp));
			}
		}

		public override void Hud()
		{
			string arg = ((Z?.zone == null || phase != "live") ? "" : Z.Label());
			FBHUD.I?.SetMatch($"4V4 · RODADA {round} · {arg}", wins[0].ToString(), (phase == "buy") ? $"{Mathf.CeilToInt(pt)}" : FBMode.Mmss(rt), wins[1].ToString(), A(0), A(1), (from i in Enumerable.Range(0, 7)
				select (i < results.Count) ? ((results[i] != 0) ? FBMode.RED : FBMode.BLUE) : FBMode.DIM).ToArray());
			float A(int t)
			{
				return (float)Alive(t).Count() / (float)Mathf.Max(4, FBMatch.Actors().Count((FBCharacter x) => x.team == t));
			}
		}

		public override void Dispose()
		{
			Z?.Dispose();
			FBEconomy.BuyPhase(on: false);
		}
	}

	public class PoliciaLadrao : FBMode
	{
		private const int WIN = 4;

		private const float ROUND_T = 150f;

		private const float BUY_T = 12f;

		private readonly int[] wins = new int[2];

		private int round;

		private string phase = "pre";

		private float pt;

		private float rt = 150f;

		private readonly List<int> results = new List<int>();

		private int lastSec;

		private FBFarinhaBuriti.Board B;

		private FBMarker[] hm;

		private FBMarker[] zm;

		private GameObject[] meshes;

		private static readonly Color[] COL = new Color[2]
		{
			new Color(0.95f, 0.87f, 0.63f),
			new Color(0.85f, 0.31f, 0.11f)
		};

		public override int Round => round;

		public override List<Vector4> Points
		{
			get
			{
				if (B != null)
				{
					return B.items.Select((FBFarinhaBuriti.Item it) => new Vector4(it.pos.x, it.pos.y, it.pos.z, 0f)).Concat(B.zones.Select((Vector3 z) => new Vector4(z.x, z.y, z.z, 1f))).ToList();
				}
				return null;
			}
		}

		public PoliciaLadrao(FBMatch g)
			: base(g)
		{
			name = "FARINHA × BURITI";
			respawn = false;
			rounds = true;
			econ = true;
		}

		private Vector3 Base(int t)
		{
			List<Vector3> list = G.TeamSpawns(t);
			return list.Aggregate(Vector3.zero, (Vector3 a, Vector3 p) => a + p) / Mathf.Max(1, list.Count);
		}

		private Vector3 NearestNav(Vector3 p)
		{
			List<Vector3> list = FBNav.Points();
			if (list.Count <= 0)
			{
				return p;
			}
			return list.OrderBy((Vector3 q) => (q - p).Flat().magnitude).First();
		}

		private void StartRound()
		{
			round++;
			phase = "buy";
			pt = 12f;
			rt = 150f;
			G.ResetRound();
			FBFarinhaBuriti.Reset(B);
			G.freeze = true;
			lastSec = 0;
			FBEconomy.BuyPhase(on: true);
			FBAudio.Synth((wins[0] == 3 || wins[1] == 3) ? "lastround" : "round");
			int team = FBSession.team;
			FBHUD.I?.MatchToast($"RODADA {round} · CAPTURE {FBFarinhaBuriti.ART[team]} · DEFENDA {FBFarinhaBuriti.ART[1 - team]}", FBMode.GOLD, 3f);
		}

		private void EndRound(int winner, string why)
		{
			if (!(phase != "live"))
			{
				phase = "post";
				pt = 4.5f;
				results.Add(winner);
				wins[winner]++;
				int team = FBSession.team;
				bool flag = winner == team;
				FBEconomy.Round(winner, round);
				FBHUD.I?.Toast(flag ? "RODADA VENCIDA" : "RODADA PERDIDA", $"{wins[team]} × {wins[1 - team]}", why, flag ? FBMode.GREEN : FBMode.RED);
				FBHUD.I?.MatchToast(flag ? "VITÓRIA NA RODADA" : "DERROTA NA RODADA", flag ? FBMode.GREEN : FBMode.RED, 4f);
				FBAudio.Synth(flag ? "win" : "lose");
			}
		}

		private string Who(FBFarinhaBuriti.IActor a)
		{
			FBCharacter fBCharacter = a as FBCharacter;
			if (!fBCharacter || !fBCharacter.isPlayer)
			{
				return (fBCharacter ? fBCharacter.Name : FBFarinhaBuriti.TEAMS[a.Team]).ToUpper();
			}
			return "VOCÊ";
		}

		private void Events(List<FBFarinhaBuriti.Ev> ev)
		{
			int team = FBSession.team;
			foreach (FBFarinhaBuriti.Ev item in ev)
			{
				int team2 = item.item.team;
				string text = FBFarinhaBuriti.ITEMS[team2];
				FBCharacter fBCharacter = item.actor as FBCharacter;
				if (item.t == "pickup")
				{
					FBAudio.Play2D("click", 0.9f);
					if ((bool)fBCharacter && fBCharacter.isPlayer)
					{
						FBHUD.I?.Toast("VOCÊ PEGOU " + FBFarinhaBuriti.ART[team2], "LEVE ATÉ A SUA BASE (VERDE)", "FARINHA × BURITI", FBMode.GOLD);
					}
					else
					{
						FBHUD.I?.Toast(Who(item.actor) + " PEGOU " + FBFarinhaBuriti.ART[team2], (item.actor.Team == team) ? "ESCOLTE ATÉ A BASE" : "DERRUBE QUEM ESTÁ COM ELE", "FARINHA × BURITI", (item.actor.Team == team) ? FBMode.GOLD : FBMode.RED);
					}
				}
				else if (item.t == "drop")
				{
					FBHUD.I?.Toast(text + " NO CHÃO", (item.item.team == team) ? "PEGUE DE VOLTA!" : "DEVOLVA AO PEDESTAL!", "FARINHA × BURITI", FBMode.GOLD);
				}
				else if (item.t == "return")
				{
					FBAudio.Play2D("click", 0.8f);
					FBHUD.I?.Toast(text + " DEVOLVID" + ((team2 == 0) ? "A" : "O"), "VOLTOU PARA O PEDESTAL", "FARINHA × BURITI", FBMode.BLUE);
				}
				else if (item.t == "expire")
				{
					FBHUD.I?.Toast(text + " VOLTOU AO PEDESTAL", "", "FARINHA × BURITI", FBMode.BLUE);
				}
				else if (item.t == "deliver")
				{
					EndRound(team2, (team2 == 0) ? "FARINHA ENTREGUE · AGORA É SÓ COMER COM BURITI" : "BURITI ENTREGUE · AGORA É SÓ COMER COM FARINHA");
				}
			}
		}

		public override void Setup()
		{
			FBMatch.TEAM_NAME[0] = (FBMatch.TEAM_SHORT[0] = "FARINHA");
			FBMatch.TEAM_NAME[1] = (FBMatch.TEAM_SHORT[1] = "BURITI");
			Vector3 vector = Base(0);
			Vector3 vector2 = Base(1);
			Vector3 b = (vector + vector2) * 0.5f;
			Vector3[] array = new Vector3[2]
			{
				NearestNav(Vector3.Lerp(vector, b, 0.12f)),
				NearestNav(Vector3.Lerp(vector2, b, 0.12f))
			};
			Vector3[] array2 = new Vector3[2]
			{
				NearestNav(Vector3.Lerp(vector2, b, 0.28f)),
				NearestNav(Vector3.Lerp(vector, b, 0.28f))
			};
			B = FBFarinhaBuriti.Create(array2, array);
			hm = new FBMarker[2]
			{
				FBMarker.Make(array2[0], COL[0], 2.6f, 70f),
				FBMarker.Make(array2[1], COL[1], 2.6f, 70f)
			};
			zm = new FBMarker[2]
			{
				FBMarker.Make(array[0], new Color(0.21f, 0.82f, 0.35f), 4f, 14f),
				FBMarker.Make(array[1], new Color(0.21f, 0.82f, 0.35f), 4f, 14f)
			};
			meshes = new GameObject[2]
			{
				FBProps.FarinhaSaco(),
				FBProps.BuritiCacho()
			};
			G.SpawnSquad(FBSession.team, 3, "squad");
			G.SpawnSquad(1 - FBSession.team, 4, "squad");
			FBEconomy.Reset();
			StartRound();
		}

		public override Vector3? Goal(FBCharacter b)
		{
			if (B == null || phase != "live")
			{
				return null;
			}
			return FBFarinhaBuriti.BotGoal(B, b).goal;
		}

		public override bool Urgent(FBCharacter b)
		{
			if (B != null)
			{
				return FBFarinhaBuriti.BotGoal(B, b, () => 1f).urgent;
			}
			return false;
		}

		public override Vector3? UrgentGoal(FBCharacter b)
		{
			return FBFarinhaBuriti.BotGoal(B, b, () => 0f).goal ?? b.Pos;
		}

		public override void OnKill(FBCharacter v, FBCharacter k, bool head)
		{
			if (phase != "live")
			{
				return;
			}
			List<FBFarinhaBuriti.Ev> ev = new List<FBFarinhaBuriti.Ev>();
			FBFarinhaBuriti.Drop(B, v, ev);
			Events(ev);
			for (int i = 0; i < 2; i++)
			{
				if (!Alive(i).Any())
				{
					EndRound(1 - i, FBFarinhaBuriti.TEAMS[i] + " ELIMINADA");
				}
			}
		}

		public override void Update(float dt)
		{
			for (int i = 0; i < 2; i++)
			{
				hm[i].Spin(dt * 0.4f * (float)((i != 1) ? 1 : (-1)));
				zm[i].Spin((0f - dt) * 0.4f);
			}
			int team = FBSession.team;
			if (phase == "buy")
			{
				pt -= dt;
				FBEconomy.ShopTime(pt);
				FBHUD.I?.SetBox((pt > 3.5f) ? $"FASE DE COMPRA · {Mathf.CeilToInt(pt)}s · B = LOJA" : $"COMEÇA EM {Mathf.CeilToInt(pt)}", on: true);
				int num = Mathf.CeilToInt(pt);
				if (num != lastSec && pt > 0f)
				{
					lastSec = num;
					if (num == 5)
					{
						FBAudio.Synth("count5");
					}
					if (num <= 3 && num >= 1)
					{
						FBAudio.Synth("beep", num);
						FBHUD.I?.Countdown(num.ToString());
					}
				}
				if (pt <= 0f)
				{
					phase = "live";
					FBEconomy.BuyPhase(on: false);
					G.freeze = false;
					FBHUD.I?.Countdown("VALENDO!");
					FBAudio.Synth("go");
					G.OnLive();
				}
			}
			else if (phase == "post")
			{
				pt -= dt;
				if (pt <= 0f)
				{
					if (wins[0] >= 4 || wins[1] >= 4)
					{
						G.EndMatch(wins[team] >= 4, "FARINHA × BURITI", wins[0], wins[1]);
						return;
					}
					StartRound();
				}
			}
			else if (phase == "live")
			{
				rt -= dt;
				Events(FBFarinhaBuriti.Step(B, FBMatch.Actors().Cast<FBFarinhaBuriti.IActor>(), dt));
				if (rt <= 0f && phase == "live")
				{
					EndRound(FBFarinhaBuriti.TimeoutWinner(B, new int[2]
					{
						Alive(0).Count(),
						Alive(1).Count()
					}), "TEMPO ESGOTADO · CHEGOU MAIS PERTO");
				}
				FBFarinhaBuriti.Item item = B.items[team];
				FBFarinhaBuriti.Item item2 = B.items[1 - team];
				bool flag = item.carrier as FBCharacter == FBMatch.Player;
				string text = FBFarinhaBuriti.ART[team];
				string text2 = FBFarinhaBuriti.ART[1 - team];
				string text3 = FBFarinhaBuriti.ITEMS[team];
				string arg = FBFarinhaBuriti.ITEMS[1 - team];
				FBHUD.I?.SetBox(flag ? ("LEVE " + text + " ATÉ A SUA BASE (VERDE)") : ((item.st == "carried") ? ("ESCOLTE QUEM ESTÁ COM " + text) : ((item.st == "dropped") ? $"PEGUE {text} DO CHÃO · {Mathf.CeilToInt(item.dropT)}s" : ((item2.st == "carried") ? ("DERRUBE QUEM LEVA " + text2 + "!") : ((item2.st == "dropped") ? string.Format("ENCOSTE {0} {1} PARA DEVOLVER · {2}s", (text2 == "A FARINHA") ? "NA" : "NO", arg, Mathf.CeilToInt(item2.dropT)) : ("CAPTURE " + text + " (" + ((text3 == "FARINHA") ? "CREME" : "LARANJA") + ") · DEFENDA " + text2))))), on: true);
			}
			if (B != null)
			{
				for (int j = 0; j < 2; j++)
				{
					FBFarinhaBuriti.Item item3 = B.items[j];
					FBCharacter fBCharacter = item3.carrier as FBCharacter;
					Vector3 position = ((item3.st == "carried" && (bool)fBCharacter) ? (fBCharacter.Pos + Vector3.up * (2.25f + (float)j * 0.25f)) : (item3.pos + Vector3.up * 0.05f));
					meshes[j].transform.position = position;
					meshes[j].transform.Rotate(0f, dt * 1.6f * 57.29578f, 0f);
					meshes[j].SetActive(phase != "pre");
				}
			}
		}

		public override void Hud()
		{
			string text = ((B == null) ? "" : string.Join(" · ", from k in new int[2] { 0, 1 }
				select (!(B.items[k].st == "carried")) ? ((!(B.items[k].st == "dropped")) ? "" : (FBFarinhaBuriti.ITEMS[k] + " NO CHÃO")) : (FBFarinhaBuriti.ITEMS[k] + " EM FUGA") into s
				where s != ""
				select s));
			if (text == "")
			{
				text = "FARINHA × BURITI";
			}
			FBHUD.I?.SetMatch($"RODADA {round} · {text}", wins[0].ToString(), (phase == "buy") ? $"{Mathf.CeilToInt(pt)}" : FBMode.Mmss(rt), wins[1].ToString(), A(0), A(1), (from i in Enumerable.Range(0, 7)
				select (i < results.Count) ? ((results[i] != 0) ? FBMode.RED : FBMode.BLUE) : FBMode.DIM).ToArray());
			float A(int t)
			{
				return (float)Alive(t).Count() / (float)Mathf.Max(4, FBMatch.Actors().Count((FBCharacter x) => x.team == t));
			}
		}

		public override void Dispose()
		{
			foreach (FBMarker item in (hm ?? new FBMarker[0]).Concat(zm ?? new FBMarker[0]))
			{
				if ((bool)item)
				{
					UnityEngine.Object.Destroy(item.gameObject);
				}
			}
			if (meshes != null)
			{
				GameObject[] array = meshes;
				foreach (GameObject gameObject in array)
				{
					if ((bool)gameObject)
					{
						UnityEngine.Object.Destroy(gameObject);
					}
				}
			}
			FBEconomy.BuyPhase(on: false);
			FBMatch.TEAM_NAME[0] = "TIME AZUL";
			FBMatch.TEAM_NAME[1] = "TIME VERMELHO";
			FBMatch.TEAM_SHORT[0] = "AZUL";
			FBMatch.TEAM_SHORT[1] = "VERMELHO";
		}
	}

	public class FFA : FBMode
	{
		private const int LIMIT = 25;

		private const int BOTS = 7;

		private const float TIME = 600f;

		private float t = 600f;

		private readonly List<(float t, string type, int team, string name, int k, int d)> q = new List<(float, string, int, string, int, int)>();

		private int Top => Mathf.Max(FBMatch.Stats.kills, (from b in FBMatch.Bots
			where b
			select b.kills).DefaultIfEmpty(0).Max());

		public FFA(FBMatch g)
			: base(g)
		{
			name = "TODOS × TODOS";
			ffa = true;
			respawn = true;
			autoRespawn = 3f;
		}

		private List<Vector3> All()
		{
			MapDef map = G.Map;
			return map.spawnA.Concat(map.spawnB).Concat(map.bots).Concat(map.points)
				.ToList();
		}

		public override Vector3 SpawnPoint()
		{
			List<Vector3> list = All();
			if (list.Count == 0)
			{
				return Vector3.zero;
			}
			List<FBCharacter> alive = (from a in FBMatch.Actors()
				where a.alive
				select a).ToList();
			if (alive.Count == 0)
			{
				return FBUtil.Pick(list);
			}
			List<Vector3> list2 = list.OrderByDescending((Vector3 p) => alive.Min((FBCharacter a) => (a.Pos - p).Flat().magnitude)).ToList();
			return list2[UnityEngine.Random.Range(0, Mathf.Max(1, Mathf.CeilToInt((float)list2.Count / 4f)))];
		}

		public override void Setup()
		{
			FBMatch.FFA = true;
			List<string> list = G.typePool[0].Concat(G.typePool[1]).ToList();
			for (int i = 0; i < 7; i++)
			{
				FBCharacter fBCharacter = G.MakeBot(list[i % list.Count], i % 2, SpawnPoint(), "patrol");
				fBCharacter.kills = (fBCharacter.deaths = 0);
			}
			FBHUD.I?.MatchToast($"TODOS × TODOS · PRIMEIRO A {25} ABATES", FBMode.GOLD, 3.5f);
		}

		public override void OnKill(FBCharacter v, FBCharacter k, bool head)
		{
			if ((bool)v && !v.isPlayer)
			{
				q.Add((3f, v.skinId, v.team, v.displayName, v.kills, v.deaths));
				G.Invoke2(5f, delegate
				{
					if ((bool)v)
					{
						FBMatch.All.Remove(v);
						FBMatch.Bots.Remove(v);
						UnityEngine.Object.Destroy(v.gameObject);
					}
				});
			}
			if (FBMatch.Stats.kills >= 25 || Top >= 25)
			{
				G.EndMatch(FBMatch.Stats.kills >= Top, "TODOS × TODOS", FBMatch.Stats.kills, Top);
			}
		}

		public override void Update(float dt)
		{
			t -= dt;
			if (t <= 0f)
			{
				G.EndMatch(FBMatch.Stats.kills >= Top, "TODOS × TODOS", FBMatch.Stats.kills, Top);
				return;
			}
			for (int num = q.Count - 1; num >= 0; num--)
			{
				(float, string, int, string, int, int) value = q[num];
				value.Item1 -= dt;
				if (value.Item1 <= 0f)
				{
					FBCharacter fBCharacter = G.MakeBot(value.Item2, value.Item3, SpawnPoint(), "patrol");
					fBCharacter.displayName = value.Item4;
					fBCharacter.kills = value.Item5;
					fBCharacter.deaths = value.Item6;
					q.RemoveAt(num);
				}
				else
				{
					q[num] = value;
				}
			}
			FBHUD.I?.SetBox($"ABATES {FBMatch.Stats.kills}/{25}", on: true);
		}

		public override void Hud()
		{
			FBHUD.I?.SetMatch("TODOS × TODOS · " + G.MapName, FBMatch.Stats.kills.ToString(), FBMode.Mmss(t), $"{Top}/{25}", (float)FBMatch.Stats.kills / 25f, -1f, null);
		}

		public override void Dispose()
		{
			FBMatch.FFA = false;
		}
	}

	public class Treino : FBMode
	{
		private readonly List<float> q = new List<float>();

		private float t;

		private readonly List<FBRangeTarget> tgs = new List<FBRangeTarget>();

		private int hits;

		private int heads;

		private float? best;

		private float? last;

		private const int DUMMIES = 12;

		private Rect lanes;

		private bool hasLanes;

		public Treino(FBMatch g)
			: base(g)
		{
			name = "TREINO";
			respawn = true;
			autoRespawn = 2f;
			arsenal = true;
		}

		private Vector3? Spot()
		{
			if (hasLanes)
			{
				for (int i = 0; i < 16; i++)
				{
					float x = UnityEngine.Random.Range(lanes.xMin, lanes.xMax);
					float z = UnityEngine.Random.Range(lanes.yMin, lanes.yMax);
					float? num = FBWorld.GroundY(x, 3f, z, 2f, 8f);
					if (num.HasValue)
					{
						float gy = num.GetValueOrDefault();
						if (!FBMatch.Bots.Any((FBCharacter b) => (bool)b && b.alive && (b.Pos - new Vector3(x, gy, z)).sqrMagnitude < 4f))
						{
							return new Vector3(x, gy, z);
						}
					}
				}
			}
			List<Vector3> list = FBNav.Points();
			List<Vector3> list2 = list.Where(delegate(Vector3 p)
			{
				float magnitude = (p - FBMatch.Player.Pos).magnitude;
				return magnitude > 10f && magnitude < 45f;
			}).ToList();
			List<Vector3> list3 = ((list2.Count >= 4) ? list2 : list);
			if (list3.Count <= 0)
			{
				return null;
			}
			return FBUtil.Pick(list3);
		}

		private void Dummy()
		{
			int num = 1 - FBSession.team;
			Vector3? vector = Spot();
			if (vector.HasValue)
			{
				Vector3 valueOrDefault = vector.GetValueOrDefault();
				FBCharacter fBCharacter = G.MakeBot(FBUtil.Pick(G.typePool[num]), num, valueOrDefault, "dummy");
				Vector3 normalized = (FBMatch.Player.Pos - fBCharacter.Pos).Flat().normalized;
				fBCharacter.GetComponent<FBBot>().SetDummy(new Vector3(0f - normalized.z, 0f, normalized.x), (UnityEngine.Random.value < 0.6f) ? FBUtil.Rnd(1.5f, 3f) : 0f, FBUtil.Rnd(0.6f, 1.1f));
				fBCharacter.tagT = 1E+09f;
			}
		}

		public override void Setup()
		{
			List<object> list = Json.L(G.Level, "targets");
			if (list != null && list.Count > 0)
			{
				foreach (object item in list)
				{
					FBRangeTarget fBRangeTarget = FBRangeTarget.Make(item as Dictionary<string, object>);
					fBRangeTarget.onHit = delegate(FBRangeTarget x, bool h)
					{
						hits++;
						if (h)
						{
							heads++;
						}
						float num3 = Time.time - x.upAt;
						if (num3 < 6f)
						{
							last = num3;
							if (!best.HasValue || num3 < best)
							{
								best = num3;
							}
						}
						FBAudio.PlayAt("impact" + UnityEngine.Random.Range(1, 5), x.transform.position, 0.6f, 6f);
						if (h)
						{
							FBHUD.I?.MatchToast($"CABEÇA! · {x.dist} M", FBMode.GOLD, 0.9f);
						}
					};
					tgs.Add(fBRangeTarget);
				}
				float num = 1E+09f;
				float num2 = -1E+09f;
				float a = 1E+09f;
				foreach (FBRangeTarget tg in tgs)
				{
					Vector3 position = tg.transform.position;
					num = Mathf.Min(num, position.x);
					num2 = Mathf.Max(num2, position.x);
					a = Mathf.Min(a, position.z);
				}
				if (num2 > num)
				{
					hasLanes = true;
					lanes = Rect.MinMaxRect(num - 1.2f, Mathf.Max(a, -70f), num2 + 1.2f, -12f);
				}
				for (int i = 0; i < 12; i++)
				{
					Dummy();
				}
				FBHUD.I?.MatchToast("ESTANDE DE TIRO · TECLA N = ARSENAL (QUALQUER ARMA E ACESSÓRIO)", FBMode.GOLD, 5f);
			}
			else
			{
				for (int j = 0; j < 12; j++)
				{
					Dummy();
				}
				FBHUD.I?.MatchToast("TREINO LIVRE · OS ALVOS NÃO ATIRAM", FBMode.GOLD, 3.5f);
			}
		}

		public override void OnKill(FBCharacter v, FBCharacter k, bool head)
		{
			if (v.isPlayer)
			{
				return;
			}
			q.Add(3f);
			G.Invoke2(4f, delegate
			{
				if ((bool)v)
				{
					FBMatch.All.Remove(v);
					FBMatch.Bots.Remove(v);
					UnityEngine.Object.Destroy(v.gameObject);
				}
			});
		}

		public override void Update(float dt)
		{
			t += dt;
			for (int num = q.Count - 1; num >= 0; num--)
			{
				q[num] -= dt;
				if (q[num] <= 0f)
				{
					q.RemoveAt(num);
					Dummy();
				}
			}
			FBMatch.Player.GetComponent<FBWeaponUser>().RefillAll(4);
			int num2 = Mathf.RoundToInt(100f * (float)FBMatch.Stats.hits / (float)Mathf.Max(1, FBMatch.Stats.shots));
			FBHUD.I?.SetBox((tgs.Count > 0) ? string.Format("N = ARSENAL · ACERTOS {0} · CABEÇA {1} · PRECISÃO {2}%{3}", hits, heads, num2, last.HasValue ? $" · REAÇÃO {last:0.00}s · MELHOR {best:0.00}s" : "") : "TREINO · MUNIÇÃO INFINITA · N = ARSENAL", on: true);
		}

		public override void Hud()
		{
			int num = Mathf.RoundToInt(100f * (float)FBMatch.Stats.hits / (float)Mathf.Max(1, FBMatch.Stats.shots));
			FBHUD.I?.SetMatch("TREINO · " + G.MapName, $"{((tgs.Count > 0) ? (hits + FBMatch.Stats.kills) : FBMatch.Stats.kills)}", FBMode.Mmss(t), $"{num}%", -1f, -1f, null);
		}

		public override void Dispose()
		{
			foreach (FBRangeTarget tg in tgs)
			{
				if ((bool)tg)
				{
					UnityEngine.Object.Destroy(tg.gameObject);
				}
			}
			tgs.Clear();
		}
	}

	public class BattleRoyale : FBMode
	{
		private const int BOTS = 15;

		private const int BOTS_BIG = 49;

		private FBZone Z;

		private string phase = "air";

		private float t;

		private float clock;

		private bool over;

		private int total = 16;

		public override FBZone.Z Zone => Z?.zone;

		public BattleRoyale(FBMatch g)
			: base(g)
		{
			name = "BATTLE ROYALE";
			br = true;
			ffa = true;
			respawn = false;
			ffDamage = true;
		}

		public override Vector3 SpawnPoint()
		{
			List<Vector3> list = FBNav.Points();
			if (list.Count <= 0)
			{
				return Vector3.zero;
			}
			return FBUtil.Pick(list);
		}

		private void Finish(bool win, int place)
		{
			if (!over)
			{
				over = true;
				G.EndMatch(win, "BATTLE ROYALE", place, FBMatch.Stats.kills, place, total);
			}
		}

		public override void Setup()
		{
			FBMatch.FFA = true;
			over = false;
			phase = "air";
			Z = new FBZone(G, FBBrCore.PHASES);
			Z.Fx();
			List<Vector3> list = FBNav.Points();
			Vector3 c = ((list.Count > 0) ? (list.Aggregate(Vector3.zero, (Vector3 a, Vector3 p) => a + p) / list.Count) : Vector3.zero);
			float num = 60f;
			foreach (Vector3 item in list)
			{
				num = Mathf.Max(num, (item - c).Flat().magnitude);
			}
			Vector3 c2 = ((list.Count > 0) ? list.OrderBy((Vector3 p) => (p - c).magnitude).First() : c);
			Z.Start(c2, num * 1.05f);
			Z.Plan();
			List<string> list2 = G.typePool[0].Concat(G.typePool[1]).ToList();
			int num2 = ((num > 500f) ? 49 : 15);
			total = num2 + 1;
			for (int i = 0; i < num2; i++)
			{
				FBCharacter fBCharacter = G.MakeBot(list2[i % list2.Count], i % 2, (list.Count > 0) ? FBUtil.Pick(list) : c, "patrol");
				fBCharacter.kills = (fBCharacter.deaths = 0);
				fBCharacter.maxHp = (fBCharacter.hp = 200f);
			}
			FBMatch.Player.maxHp = (FBMatch.Player.hp = 200f);
			FBBattleRoyale.Setup(G, Z.zone);
			FBHUD.I?.MatchToast("BATTLE ROYALE · SALTE DO AVIÃO", FBMode.GOLD, 4f);
		}

		public override Vector3? Goal(FBCharacter b)
		{
			FBZone.Z z = Z?.zone;
			if (z == null || phase == "air")
			{
				return null;
			}
			float magnitude = (b.Pos - z.c).Flat().magnitude;
			Vector3 value = ((z.to.HasValue && z.st == "shrink") ? z.to.Value.c : z.c);
			if (!(magnitude > z.r * 0.75f))
			{
				return null;
			}
			return value;
		}

		public override bool Urgent(FBCharacter b)
		{
			FBZone.Z z = Z?.zone;
			if (z != null && phase != "air")
			{
				return (b.Pos - z.c).Flat().magnitude > z.r * 0.92f;
			}
			return false;
		}

		public override Vector3? UrgentGoal(FBCharacter b)
		{
			return Z?.zone?.c;
		}

		public override void OnKill(FBCharacter v, FBCharacter k, bool head)
		{
			int i = FBMatch.Actors().Count((FBCharacter a) => a.alive);
			if (v.isPlayer)
			{
				FBHUD.I?.Toast("VOCÊ FOI ELIMINADO", $"#{i + 1}", $"{FBMatch.Stats.kills} ABATES", FBMode.RED);
				G.Invoke2(3.5f, delegate
				{
					Finish(win: false, i + 1);
				});
			}
			else if (FBMatch.Player.alive && i == 1)
			{
				FBHUD.I?.MatchToast("BOOYAH!", FBMode.GOLD, 4f);
				FBAudio.Synth("win");
				G.Invoke2(2.5f, delegate
				{
					Finish(win: true, 1);
				});
			}
		}

		public override void Update(float dt)
		{
			Z.Anim(dt);
			clock += dt;
			if (phase == "air")
			{
				t += dt;
				if (!FBBattleRoyale.Airborne() || t > 75f)
				{
					phase = "live";
					FBHUD.I?.MatchToast("A ZONA VAI COMEÇAR A FECHAR", new Color(0.35f, 0.65f, 1f), 3f);
				}
			}
			else if (!over)
			{
				Z.Step(dt);
			}
			Z.Visual();
			FBBattleRoyale.Tick(dt);
		}

		public override void Hud()
		{
			int num = FBMatch.Actors().Count((FBCharacter a) => a.alive);
			FBHUD.I?.SetMatch("BATTLE ROYALE · " + ((phase == "air") ? "SALTO" : Z.Label()), $"{num}", FBMode.Mmss(clock), $"{FBMatch.Stats.kills}", 1f - (float)num / (float)total, -1f, null);
			FBHUD.I?.SetBox($"{num} VIVOS · {FBMatch.Stats.kills} ABATES", on: true);
		}

		public override void Dispose()
		{
			Z?.Dispose();
			FBMatch.FFA = false;
			FBBattleRoyale.End();
		}
	}

	public class DeRole : FBMode
	{
		public DeRole(FBMatch g)
			: base(g)
		{
			name = "DE ROLÊ";
			respawn = true;
			autoRespawn = 3f;
			role = true;
		}

		public override void Setup()
		{
			FBHUD.I?.MatchToast("DE ROLÊ · G = GPS · K = SOM · E NA OFICINA", FBMode.GOLD, 4f);
		}

		public override void Update(float dt)
		{
			FBRole.Tick(dt);
		}
	}

	public class Online : FBMode
	{
		public Online(FBMatch g)
			: base(g)
		{
			name = "ONLINE";
			respawn = true;
			autoRespawn = 3f;
		}

		public override void Setup()
		{
			FBHUD.I?.MatchToast("ONLINE · " + (FBNet.I ? FBNet.I.status : "CONECTANDO…"), FBMode.GOLD, 3f);
		}

		public override void Hud()
		{
			FBNet i = FBNet.I;
			if ((bool)i)
			{
				FBHUD.I?.SetMatch("ONLINE · " + G.MapName, i.score[0].ToString(), i.status, i.score[1].ToString(), -1f, -1f, null);
			}
		}

		public override void OnKill(FBCharacter v, FBCharacter k, bool head)
		{
		}
	}

	public static FBMode Create(string id, FBMatch g)
	{
		switch (id)
		{
		case "tatico":
			return new Tatico(g);
		case "classico":
			return new Classico(g);
		case "4v4":
		case "squad":
			return new Squad(g);
		case "pvl":
			return new PoliciaLadrao(g);
		case "10x10":
			return new FBModo10x10(g);
		case "ffa":
			return new FFA(g);
		case "treino":
			return new Treino(g);
		case "br":
			return new BattleRoyale(g);
		case "role":
			return new DeRole(g);
		default:
			return new Tatico(g);
		}
	}

	public static List<TacPt> TacticalLayout(FBMatch G)
	{
		Vector3 vector = Cen(G.TeamSpawns(0));
		Vector3 vector2 = Cen(G.TeamSpawns(1));
		Vector3 vector3 = (vector2 - vector).Flat();
		float magnitude = vector3.magnitude;
		List<Vector3> list = FBNav.Fixed();
		if (list.Count < 20)
		{
			list = FBNav.Points();
		}
		List<TacPt> list2 = new List<TacPt>();
		string[] IDS = new string[5] { "A", "B", "C", "D", "E" };
		if (magnitude < 30f || list.Count < 10)
		{
			return Fallback();
		}
		Vector3 vector4 = vector3 / magnitude;
		Vector3 vector5 = new Vector3(vector4.z, 0f, 0f - vector4.x);
		List<(Vector3 p, float t, float lat, float q, float dist)> list3 = new List<(Vector3, float, float, float, float)>();
		foreach (Vector3 item4 in list)
		{
			Vector3 lhs = (item4 - vector).Flat();
			float num = Vector3.Dot(lhs, vector4) / magnitude;
			float item = Vector3.Dot(lhs, vector5);
			if (num < 0.08f || num > 0.92f || (item4 - vector).Flat().magnitude < 26f || (item4 - vector2).Flat().magnitude < 26f || FBWorld.RayHit(item4 + Vector3.up * 1.2f, Vector3.up, 40f))
			{
				continue;
			}
			int num2 = 0;
			float num3 = 12f;
			Vector3 o = item4 + Vector3.up * 1.1f;
			for (int i = 0; i < 8; i++)
			{
				float f = (float)i * (float)Math.PI / 4f;
				Vector3 dir = new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f));
				if (FBWorld.RayHit(o, dir, 12f, out var hit))
				{
					if (hit.distance < 7f)
					{
						num2++;
					}
					num3 = Mathf.Min(num3, hit.distance);
				}
			}
			float item2 = ((num2 >= 2 && num2 <= 6) ? (1f - (float)Mathf.Abs(num2 - 4) * 0.2f) : 0.15f);
			list3.Add((item4, num, item, item2, num3));
		}
		if (list3.Count < 5)
		{
			return Fallback();
		}
		List<float> list4 = (from c in list3
			where c.t > 0.3f && c.t < 0.7f
			select Mathf.Abs(c.lat) into x
			orderby x
			select x).ToList();
		float num4 = Mathf.Min(magnitude * 0.45f, (list4.Count > 0) ? list4[Mathf.Clamp((int)((float)list4.Count * 0.85f), 0, list4.Count - 1)] : (magnitude * 0.3f));
		int team = FBSession.team;
		float num5 = ((team == 0) ? 0.2f : 0.8f);
		float item3 = 1f - num5;
		float num6 = ((team == 0) ? 1 : (-1));
		(float, float)[] array = new(float, float)[5]
		{
			(num5, 0f),
			(0.5f - 0.1f * num6, -0.62f * num6),
			(0.5f, 0f),
			(0.5f + 0.1f * num6, 0.62f * num6),
			(item3, 0f)
		};
		for (float sep = Mathf.Max(34f, magnitude * 0.22f); sep >= 20f; sep *= 0.88f)
		{
			list2.Clear();
			for (int j = 0; j < array.Length; j++)
			{
				(float, float) tuple = array[j];
				Vector3 vector6 = vector + vector4 * (tuple.Item1 * magnitude) + vector5 * (tuple.Item2 * num4);
				(Vector3, float, float, float, float)? tuple2 = null;
				float num7 = -1E+09f;
				foreach (var c2 in list3)
				{
					if (!list2.Any((TacPt q) => (q.pos - c2.Item1).Flat().magnitude < sep))
					{
						float num8 = 0f - (c2.Item1 - vector6).Flat().magnitude + c2.Item4 * 9f + Mathf.Min(c2.Item5, 8f) * 0.6f - Mathf.Abs(c2.Item1.y - vector.y) * 0.8f;
						if (num8 > num7)
						{
							num7 = num8;
							tuple2 = c2;
						}
					}
				}
				if (!tuple2.HasValue)
				{
					break;
				}
				list2.Add(new TacPt
				{
					pos = tuple2.Value.Item1,
					id = IDS[j],
					r = Mathf.Clamp(tuple2.Value.Item5 * 0.85f, 5.5f, 9f)
				});
			}
			if (list2.Count == 5)
			{
				return list2;
			}
		}
		if (list2.Count < 3)
		{
			return Fallback();
		}
		return list2;
		static Vector3 Cen(List<Vector3> a)
		{
			return a.Aggregate(Vector3.zero, (Vector3 s, Vector3 p) => s + p) / Mathf.Max(1, a.Count);
		}
		List<TacPt> Fallback()
		{
			List<TacPt> list5 = new List<TacPt>();
			foreach (Vector3 p2 in G.Map.points)
			{
				if (list5.Count >= 5)
				{
					break;
				}
				if (list5.All((TacPt q) => (q.pos - p2).Flat().magnitude > 15f))
				{
					list5.Add(new TacPt
					{
						pos = p2,
						id = IDS[list5.Count]
					});
				}
			}
			return list5;
		}
	}
}
}
