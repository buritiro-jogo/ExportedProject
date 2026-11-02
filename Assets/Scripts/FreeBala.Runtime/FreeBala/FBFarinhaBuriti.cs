using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBFarinhaBuriti
{
	public interface IActor
	{
		Vector3 Pos { get; }

		int Team { get; }

		bool Alive { get; }

		bool Knocked { get; }
	}

	public class Item
	{
		public int team;

		public Vector3 home;

		public Vector3 pos;

		public string st = "home";

		public IActor carrier;

		public float dropT;
	}

	public class Board
	{
		public Vector3[] zones;

		public Item[] items;
	}

	public struct Ev
	{
		public string t;

		public Item item;

		public IActor actor;
	}

	public const float PICK = 2.4f;

	public const float DELIVER = 4f;

	public const float DROP_T = 20f;

	public static readonly string[] TEAMS = new string[2] { "FARINHA", "BURITI" };

	public static readonly string[] ITEMS = new string[2] { "FARINHA", "BURITI" };

	public static readonly string[] ART = new string[2] { "A FARINHA", "O BURITI" };

	private static float XZ(Vector3 a, Vector3 b)
	{
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	public static Board Create(Vector3[] homes, Vector3[] zones)
	{
		Board board = new Board();
		board.zones = (Vector3[])zones.Clone();
		board.items = new Item[2]
		{
			new Item
			{
				team = 0,
				home = homes[0],
				pos = homes[0]
			},
			new Item
			{
				team = 1,
				home = homes[1],
				pos = homes[1]
			}
		};
		return board;
	}

	public static void Reset(Board B)
	{
		Item[] items = B.items;
		foreach (Item obj in items)
		{
			obj.pos = obj.home;
			obj.st = "home";
			obj.carrier = null;
			obj.dropT = 0f;
		}
	}

	public static List<Ev> Step(Board B, IEnumerable<IActor> actors, float dt)
	{
		List<Ev> list2 = new List<Ev>();
		List<IActor> list = new List<IActor>(actors);
		Item[] items = B.items;
		foreach (Item it in items)
		{
			if (it.st == "carried")
			{
				if (it.carrier == null || !it.carrier.Alive || it.carrier.Knocked)
				{
					Drop(B, it.carrier, list2);
					continue;
				}
				it.pos = it.carrier.Pos;
				if (XZ(it.pos, B.zones[it.team]) < 4f)
				{
					list2.Add(new Ev
					{
						t = "deliver",
						item = it
					});
				}
				continue;
			}
			if (it.st == "dropped")
			{
				IActor actor = Near(1 - it.team);
				if (actor != null)
				{
					it.pos = it.home;
					it.st = "home";
					it.dropT = 0f;
					list2.Add(new Ev
					{
						t = "return",
						item = it,
						actor = actor
					});
					continue;
				}
				it.dropT -= dt;
				if (it.dropT <= 0f)
				{
					it.pos = it.home;
					it.st = "home";
					list2.Add(new Ev
					{
						t = "expire",
						item = it
					});
					continue;
				}
			}
			IActor actor2 = Near(it.team);
			if (actor2 != null)
			{
				it.st = "carried";
				it.carrier = actor2;
				it.dropT = 0f;
				list2.Add(new Ev
				{
					t = "pickup",
					item = it,
					actor = actor2
				});
			}
			IActor Near(int team, float ymax = 2.5f)
			{
				return list.Find((IActor a) => a.Alive && !a.Knocked && a.Team == team && new Vector2(a.Pos.x - it.pos.x, a.Pos.z - it.pos.z).magnitude < 2.4f && Mathf.Abs(a.Pos.y - it.pos.y) < ymax);
			}
		}
		return list2;
	}

	public static void Drop(Board B, IActor actor, List<Ev> ev)
	{
		Item[] items = B.items;
		foreach (Item item in items)
		{
			if (item.st == "carried" && (actor == null || item.carrier == actor))
			{
				Vector3 pos = actor?.Pos ?? item.pos;
				item.st = "dropped";
				item.carrier = null;
				item.pos = pos;
				item.dropT = 20f;
				ev?.Add(new Ev
				{
					t = "drop",
					item = item,
					actor = actor
				});
			}
		}
	}

	public static float Progress(Board B, Item it)
	{
		float num = Mathf.Max(1f, XZ(it.home, B.zones[it.team]));
		return Mathf.Min(1f, XZ(it.pos, B.zones[it.team]) / num);
	}

	public static int TimeoutWinner(Board B, int[] alive)
	{
		float num = Progress(B, B.items[0]);
		float num2 = Progress(B, B.items[1]);
		if (Mathf.Abs(num - num2) >= 0.05f)
		{
			return (!(num < num2)) ? 1 : 0;
		}
		if (alive[0] != alive[1])
		{
			return (alive[0] <= alive[1]) ? 1 : 0;
		}
		return 0;
	}

	public static (Vector3? goal, bool urgent) BotGoal(Board B, IActor actor, Func<float> rnd = null)
	{
		if (rnd == null)
		{
			rnd = () => UnityEngine.Random.value;
		}
		Item item = B.items[actor.Team];
		Item item2 = B.items[1 - actor.Team];
		if (item.carrier == actor)
		{
			return (goal: B.zones[actor.Team], urgent: true);
		}
		if (item2.st == "dropped")
		{
			return (goal: item2.pos, urgent: false);
		}
		if (item2.st == "carried")
		{
			return (goal: (rnd() < 0.7f) ? new Vector3?(item2.pos) : null, urgent: true);
		}
		if (item.st == "dropped")
		{
			return (goal: item.pos, urgent: false);
		}
		if (item.st == "carried")
		{
			return (goal: (rnd() < 0.6f) ? new Vector3?(item.pos) : null, urgent: false);
		}
		float num = rnd();
		return (goal: (num < 0.5f) ? new Vector3?(item.home) : ((num < 0.75f) ? new Vector3?(item2.home) : null), urgent: false);
	}
}
}
