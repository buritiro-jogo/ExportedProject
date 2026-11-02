using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBVoiceDirector
{
	private class Gas
	{
		public bool inZone;

		public bool first;

		public float exposure;

		public float lastZoneT = -99f;

		public float nextT;

		public float exitT;

		public float breath;
	}

	private class Actor
	{
		public bool isPlayer;

		public bool dead;

		public float lastT = -99f;

		public float lastEnd = -99f;

		public float hpFrac = 1f;

		public int lastPrio;

		public readonly float[] bankT = new float[NB];

		public readonly int[] lastVar = new int[NB];

		public bool pending;

		public float t0;

		public float dmg;

		public float maxHp = 100f;

		public float hpAfter;

		public bool head;

		public bool limb;

		public bool body;

		public bool fire;

		public bool down;

		public int types;

		public Gas gas = new Gas();

		public Actor()
		{
			for (int i = 0; i < NB; i++)
			{
				bankT[i] = -99f;
				lastVar[i] = -1;
			}
		}
	}

	public float burstWindow = 0.16f;

	public float gasHold = 1.6f;

	public float playerGap = 0.35f;

	public float npcGap = 0.6f;

	public int maxVoices = 8;

	public float breathDecay = 2.5f;

	public Func<int, FBVoiceBank, int> countOf = (int k, FBVoiceBank b) => 3;

	public Func<int, FBVoiceBank, int, float> durationOf = (int k, FBVoiceBank b, int v) => 0.8f;

	public Action<FBVoiceCue> onCue;

	public Action<int, FBVoiceBank, string> onSuppress;

	public readonly FBVoiceProfiles prof;

	private readonly System.Random rnd;

	private static readonly int NB = Enum.GetValues(typeof(FBVoiceBank)).Length;

	private readonly Dictionary<int, Actor> actors = new Dictionary<int, Actor>();

	private readonly List<(int key, int prio, float end)> active = new List<(int, int, float)>();

	private float lastTick = -1f;

	private float R => (float)rnd.NextDouble();

	public FBVoiceDirector(FBVoiceProfiles p = null, int seed = 12345)
	{
		prof = p ?? new FBVoiceProfiles();
		rnd = new System.Random(seed);
	}

	private Actor A(int key)
	{
		if (!actors.TryGetValue(key, out var value))
		{
			value = (actors[key] = new Actor());
		}
		return value;
	}

	public void Forget(int key)
	{
		actors.Remove(key);
		active.RemoveAll(((int key, int prio, float end) v) => v.key == key);
	}

	public int ActiveVoices(float now)
	{
		active.RemoveAll(((int key, int prio, float end) v) => v.end <= now);
		return active.Count;
	}

	public bool InGas(int key)
	{
		if (actors.TryGetValue(key, out var value))
		{
			return value.gas.inZone;
		}
		return false;
	}

	public bool Breathing(int key)
	{
		if (actors.TryGetValue(key, out var value) && !value.gas.inZone)
		{
			return value.gas.breath > 0f;
		}
		return false;
	}

	public void Hit(in FBVoiceHit h, float now)
	{
		Actor actor = A(h.key);
		actor.isPlayer = h.isPlayer;
		actor.maxHp = Mathf.Max(1f, h.maxHp);
		actor.hpFrac = Mathf.Clamp01(h.hpAfter / actor.maxHp);
		if (h.killed)
		{
			actor.pending = false;
			bool flag = h.type == FBDamageType.Zone;
			actor.gas = Reset(actor.gas);
			Play(actor, h.key, flag ? FBVoiceBank.GasMorte : FBVoiceBank.Morte, 1f, now, force: true);
			actor.dead = true;
			return;
		}
		if (actor.dead && h.hpAfter > 0f)
		{
			actor.dead = false;
		}
		if (actor.dead)
		{
			return;
		}
		if (h.knocked)
		{
			actor.pending = false;
			Play(actor, h.key, FBVoiceBank.Caido, 1f, now, force: true);
		}
		else if (h.type == FBDamageType.Zone)
		{
			Gas gas = actor.gas;
			if (!gas.inZone)
			{
				gas.inZone = true;
				gas.first = true;
				gas.breath = 0f;
				gas.nextT = now + 0.45f + R * 0.3f;
			}
			gas.lastZoneT = now;
		}
		else if (h.type == FBDamageType.Bleed)
		{
			Play(actor, h.key, FBVoiceBank.Sangrando, 0.4f, now);
		}
		else if (!(h.applied <= 0f) || h.type != 0)
		{
			if (!actor.pending)
			{
				actor.pending = true;
				actor.t0 = now;
				actor.dmg = 0f;
				actor.types = 0;
				actor.head = (actor.limb = (actor.body = (actor.fire = false)));
			}
			actor.dmg += Mathf.Max(0f, h.applied);
			actor.types |= 1 << (int)h.type;
			actor.hpAfter = h.hpAfter;
			actor.down = h.down;
			if (h.head)
			{
				actor.head = true;
			}
			else if (h.limb)
			{
				actor.limb = true;
			}
			else
			{
				actor.body = true;
			}
			if (h.fire)
			{
				actor.fire = true;
			}
		}
	}

	public void External(int key, FBVoiceBank bank, int variation, float intensity, float now)
	{
		Actor actor = A(key);
		actor.isPlayer = false;
		Play(actor, key, bank, intensity, now, force: true, variation);
	}

	public void Tick(float now)
	{
		float dt = ((lastTick < 0f) ? 0f : Mathf.Clamp(now - lastTick, 0f, 0.5f));
		lastTick = now;
		foreach (KeyValuePair<int, Actor> actor in actors)
		{
			Actor value = actor.Value;
			if (!value.dead)
			{
				if (value.pending && now - value.t0 >= burstWindow)
				{
					value.pending = false;
					Flush(value, actor.Key, now);
				}
				GasTick(value, actor.Key, now, dt);
			}
		}
	}

	private void Flush(Actor a, int key, float now)
	{
		float num = a.dmg / a.maxHp;
		FBVoiceBank best = FBVoiceBank.DorLeve;
		int bp = -1;
		if (T(FBDamageType.Explosion))
		{
			Cand(FBVoiceBank.Explosao);
		}
		if (T(FBDamageType.Vehicle))
		{
			Cand(FBVoiceBank.Atropelo);
		}
		if (T(FBDamageType.Fall))
		{
			Cand((num >= 0.25f) ? FBVoiceBank.QuedaGrave : FBVoiceBank.Queda);
		}
		if (a.fire)
		{
			Cand(FBVoiceBank.Fogo);
		}
		if (T(FBDamageType.Environment) || T(FBDamageType.Other))
		{
			Cand(a.fire ? FBVoiceBank.Fogo : ((num >= 0.3f) ? FBVoiceBank.DorGrave : FBVoiceBank.Ambiente));
		}
		if (T(FBDamageType.Takedown))
		{
			Cand(FBVoiceBank.DorGrave);
		}
		if (T(FBDamageType.Bullet))
		{
			if (a.down)
			{
				Cand(FBVoiceBank.Sangrando);
			}
			else if (a.head)
			{
				Cand(FBVoiceBank.Cabeca);
			}
			else if (num >= 0.25f)
			{
				Cand(FBVoiceBank.DorGrave);
			}
			else if (num >= 0.11f)
			{
				Cand(FBVoiceBank.DorMedia);
			}
			else if (a.limb && !a.body)
			{
				Cand(FBVoiceBank.Membro);
			}
			else
			{
				Cand(FBVoiceBank.DorLeve);
			}
		}
		if (bp >= 0)
		{
			if (a.hpFrac < 0.3f && bp <= prof.Rule(FBVoiceBank.DorMedia).prio && R < 0.65f)
			{
				best = FBVoiceBank.PoucaVida;
			}
			float inten = ((best == FBVoiceBank.Explosao || best == FBVoiceBank.Atropelo) ? 1f : Mathf.Clamp(0.25f + num * 2.2f, 0.25f, 1f));
			Play(a, key, best, inten, now);
		}
		void Cand(FBVoiceBank b)
		{
			int prio = prof.Rule(b).prio;
			if (prio > bp)
			{
				bp = prio;
				best = b;
			}
		}
		bool T(FBDamageType t)
		{
			return (a.types & (1 << (int)t)) != 0;
		}
	}

	private static Gas Reset(Gas g)
	{
		return new Gas
		{
			exposure = 0f
		};
	}

	private float Sev(Actor a)
	{
		return Mathf.Clamp01(0.55f * Mathf.Clamp01(a.gas.exposure / 18f) + 0.6f * (1f - a.hpFrac));
	}

	private void GasTick(Actor a, int key, float now, float dt)
	{
		Gas gas = a.gas;
		if (gas.inZone && now - gas.lastZoneT > gasHold)
		{
			gas.inZone = false;
			gas.exitT = now;
			gas.breath = ((gas.exposure > 1.5f) ? Mathf.Max(0.35f, Sev(a)) : 0f);
			gas.nextT = now + 0.7f + R * 0.4f;
		}
		if (gas.inZone)
		{
			gas.exposure += dt;
			if (!(now < gas.nextT))
			{
				float num = Sev(a);
				FBVoiceBank bank;
				if (gas.first)
				{
					bank = FBVoiceBank.GasIrrita;
				}
				else if (a.hpFrac < 0.3f && R < 0.5f)
				{
					bank = FBVoiceBank.GasFraca;
				}
				else
				{
					float r = R;
					bank = ((r < 0.42f + 0.3f * num) ? FBVoiceBank.GasTosse : ((r < 0.78f) ? FBVoiceBank.GasResp : ((r < 0.9f && num < 0.5f) ? FBVoiceBank.GasIrrita : ((r < 0.95f) ? FBVoiceBank.GasEsforco : FBVoiceBank.GasTosse))));
				}
				bool flag = Play(a, key, bank, 0.4f + 0.6f * num, now);
				if (flag)
				{
					gas.first = false;
				}
				gas.nextT = now + (flag ? (Mathf.Lerp(4.2f, 1.7f, num) * (0.8f + 0.4f * R)) : 0.5f);
			}
			return;
		}
		if (gas.exposure > 0f)
		{
			gas.exposure = Mathf.Max(0f, gas.exposure - dt * 0.5f);
		}
		if (!(gas.breath <= 0f) && !(now < gas.nextT))
		{
			float num2 = gas.breath * Mathf.Exp((0f - (now - gas.exitT)) / breathDecay);
			if (num2 < 0.15f)
			{
				gas.breath = 0f;
				return;
			}
			Play(a, key, FBVoiceBank.GasResp, num2, now);
			gas.nextT = now + Mathf.Lerp(1.5f, 2.4f, 1f - num2);
		}
	}

	private bool No(int key, FBVoiceBank b, string why)
	{
		onSuppress?.Invoke(key, b, why);
		return false;
	}

	private bool Play(Actor a, int key, FBVoiceBank bank, float inten, float now, bool force = false, int variation = -1)
	{
		FBVoiceRule fBVoiceRule = prof.Rule(bank);
		int prio = fBVoiceRule.prio;
		if (!force)
		{
			if (!a.isPlayer && fBVoiceRule.npcChance < 1f && R > fBVoiceRule.npcChance)
			{
				a.bankT[(uint)bank] = now;
				return No(key, bank, "sorteio npc");
			}
			if (now - a.bankT[(uint)bank] < fBVoiceRule.cd)
			{
				return No(key, bank, "cooldown do banco");
			}
		}
		bool num = now < a.lastEnd;
		bool flag = false;
		if (num)
		{
			if (prio > a.lastPrio)
			{
				flag = true;
			}
			else
			{
				if (!force)
				{
					return No(key, bank, "falando (prioridade igual/maior)");
				}
				flag = true;
			}
		}
		else if (!force && now - a.lastT < (a.isPlayer ? playerGap : npcGap) && prio <= a.lastPrio)
		{
			return No(key, bank, "intervalo do personagem");
		}
		int num2 = -1;
		active.RemoveAll(((int key, int prio, float end) v) => v.end <= now);
		if (flag)
		{
			active.RemoveAll(((int key, int prio, float end) v) => v.key == key);
		}
		if (active.Count >= maxVoices)
		{
			int index = 0;
			for (int i = 1; i < active.Count; i++)
			{
				if (active[i].prio < active[index].prio || (active[i].prio == active[index].prio && active[i].end < active[index].end))
				{
					index = i;
				}
			}
			if (active[index].prio >= prio)
			{
				return No(key, bank, "limite global de vozes");
			}
			num2 = active[index].key;
			active.RemoveAt(index);
			if (actors.TryGetValue(num2, out var value))
			{
				value.lastEnd = now;
			}
		}
		int num3 = Mathf.Max(1, countOf(key, bank));
		int num4 = variation;
		if (num4 < 0)
		{
			num4 = rnd.Next(num3);
			if (num3 > 1 && num4 == a.lastVar[(uint)bank])
			{
				num4 = (num4 + 1 + rnd.Next(num3 - 1)) % num3;
			}
		}
		a.lastVar[(uint)bank] = num4;
		a.lastT = now;
		a.lastPrio = prio;
		a.bankT[(uint)bank] = now;
		float num5 = Mathf.Max(0.1f, durationOf(key, bank, num4));
		a.lastEnd = now + num5;
		active.Add((key, prio, now + num5));
		onCue?.Invoke(new FBVoiceCue
		{
			key = key,
			bank = bank,
			variation = num4,
			prio = prio,
			intensity = Mathf.Clamp01(inten),
			interrupt = flag,
			steal = num2
		});
		return true;
	}
}
}
