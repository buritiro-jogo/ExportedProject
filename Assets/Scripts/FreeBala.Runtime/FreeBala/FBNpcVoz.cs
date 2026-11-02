using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FreeBala
{
public static class FBNpcVoz
{
	private class Line
	{
		public string c;

		public float d;
	}

	private static Dictionary<string, Dictionary<string, List<Line>>> cats;

	private static bool tried;

	private static readonly Dictionary<int, float> nextT = new Dictionary<int, float>();

	private static float globalT;

	private static readonly List<(float t, FBCharacter c, string cat, float vol, float refD)> queue = new List<(float, FBCharacter, string, float, float)>();

	public static int Said;

	public static string LastLine;

	public static bool Ready
	{
		get
		{
			Load();
			if (cats != null)
			{
				return cats.Count > 0;
			}
			return false;
		}
	}

	public static int Id(Object o)
	{
		return RuntimeHelpers.GetHashCode(o);
	}

	private static void Load()
	{
		if (tried)
		{
			return;
		}
		tried = true;
		TextAsset textAsset = Resources.Load<TextAsset>("Audio/171/npc/vozes");
		if (!textAsset)
		{
			return;
		}
		cats = new Dictionary<string, Dictionary<string, List<Line>>>();
		foreach (KeyValuePair<string, object> item in Json.O(Json.Parse(textAsset.text), "cats") ?? new Dictionary<string, object>())
		{
			Dictionary<string, List<Line>> dictionary = new Dictionary<string, List<Line>>();
			foreach (KeyValuePair<string, object> item2 in (item.Value as Dictionary<string, object>) ?? new Dictionary<string, object>())
			{
				dictionary[item2.Key] = (from o in (item2.Value as List<object>) ?? new List<object>()
					select new Line
					{
						c = Json.S(o, "c"),
						d = Json.F(o, "d", 1.5f)
					} into l
					where l.c != null
					select l).ToList();
			}
			cats[item.Key] = dictionary;
		}
	}

	public static string VozOf(FBCharacter c)
	{
		bool num = ((c.npcSet != null) ? (c.npcSet.sex == "f") : c.IsFemale);
		uint num2 = (uint)((c.skinId ?? "").GetHashCode() * 31 + Id(c));
		if (!num)
		{
			return "Homem" + (1 + num2 % 3);
		}
		if (num2 % 2 != 0)
		{
			return "Mulher2";
		}
		return "Mulher1";
	}

	public static float Say(FBCharacter c, string cat, float vol = 1f, float refD = 4f, float gap = 8f, float global = 1.2f, float delay = 0f)
	{
		if (!c || !c.alive || !Ready || !cats.TryGetValue(cat, out var value))
		{
			return 0f;
		}
		if (delay > 0f)
		{
			queue.Add((Time.time + delay, c, cat, vol, refD));
			return 0.1f;
		}
		int key = Id(c);
		if (nextT.TryGetValue(key, out var value2) && Time.time < value2)
		{
			return 0f;
		}
		if (global > 0f && Time.time < globalT)
		{
			return 0f;
		}
		string text = VozOf(c);
		if (!value.TryGetValue(text, out var value3) || value3.Count == 0)
		{
			string sx = (text.StartsWith("M") ? "Mulher" : "Homem");
			value3 = (from kv in value
				where kv.Key.StartsWith(sx) && kv.Value.Count > 0
				select kv.Value).FirstOrDefault();
		}
		if (value3 == null || value3.Count == 0)
		{
			return 0f;
		}
		Line line = value3[Random.Range(0, value3.Count)];
		AudioClip audioClip = Resources.Load<AudioClip>("Audio/171/npc/" + line.c);
		if (!audioClip)
		{
			return 0f;
		}
		Vector3 pos = c.Pos + Vector3.up * 1.6f * c.ScaleK;
		FBAudio.PlayAt(audioClip, pos, vol, refD, 0.03f);
		nextT[key] = Time.time + line.d + gap;
		if (global > 0f)
		{
			globalT = Time.time + global;
		}
		Said++;
		LastLine = cat + ":" + line.c;
		return line.d;
	}

	public static void Tick()
	{
		for (int num = queue.Count - 1; num >= 0; num--)
		{
			if (Time.time >= queue[num].t)
			{
				(float, FBCharacter, string, float, float) tuple = queue[num];
				queue.RemoveAt(num);
				Say(tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
			}
		}
	}

	public static void Reset()
	{
		nextT.Clear();
		queue.Clear();
		globalT = 0f;
	}
}
}
