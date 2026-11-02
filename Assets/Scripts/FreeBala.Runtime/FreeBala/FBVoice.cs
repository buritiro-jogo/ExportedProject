using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBVoice
{
	private class Who
	{
		public FBCharacter ch;

		public FBVoiceProfile prof;

		public float pitch;

		public string skin;
	}

	private class Slot
	{
		public AudioSource src;

		public int key = -1;

		public Transform follow;

		public float lift;
	}

	public struct Trace
	{
		public float t;

		public FBCharacter ch;

		public string sex;

		public string kind;

		public string type;

		public string bank;

		public string decision;

		public string clip;

		public float amount;

		public float hp;

		public float vol;

		public float pitch;

		public float intensity;

		public float dist;

		public int voices;
	}

	private static readonly Dictionary<int, Who> who = new Dictionary<int, Who>();

	private static readonly List<Slot> slots = new List<Slot>();

	private static GameObject root;

	private static float bodyHitT;

	public static Action<Trace> trace;

	private static readonly Dictionary<int, string> lastType = new Dictionary<int, string>();

	private static readonly Dictionary<FBCharacter, int> keys = new Dictionary<FBCharacter, int>();

	private static int nextKey = 1;

	public static FBVoiceDirector D { get; private set; }

	private static float DistToListener(Vector3 p)
	{
		AudioListener audioListener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
		if (!audioListener)
		{
			return -1f;
		}
		return Vector3.Distance(audioListener.transform.position, p);
	}

	private static void Ensure()
	{
		if (D != null)
		{
			return;
		}
		D = new FBVoiceDirector(FBVoiceProfiles.I, Environment.TickCount)
		{
			maxVoices = (Application.isMobilePlatform ? 5 : 8)
		};
		D.countOf = (int k, FBVoiceBank b) => who.TryGetValue(k, out var value3) ? value3.prof.Count(b) : 0;
		D.durationOf = delegate(int k, FBVoiceBank b, int v)
		{
			AudioClip audioClip = ClipOf(k, b, v);
			return (!audioClip) ? 0.8f : (audioClip.length / Mathf.Max(0.5f, PitchOf(k, b)));
		};
		D.onCue = Perform;
		D.onSuppress = delegate(int k, FBVoiceBank b, string why)
		{
			if (trace != null && who.TryGetValue(k, out var value) && (bool)value.ch)
			{
				trace(new Trace
				{
					t = Time.time,
					ch = value.ch,
					sex = value.prof.sex,
					kind = "voz",
					type = (lastType.TryGetValue(k, out var value2) ? value2 : ""),
					bank = b.ToString(),
					decision = "suprimida: " + why,
					hp = value.ch.hp,
					dist = DistToListener(value.ch.Pos),
					voices = D.ActiveVoices(Time.time)
				});
			}
		};
		root = new GameObject("FB_Voice");
		UnityEngine.Object.DontDestroyOnLoad(root);
		root.AddComponent<FBVoiceRunner>();
		for (int i = 0; i < D.maxVoices + 2; i++)
		{
			AudioSource audioSource = new GameObject("Voz" + i).AddComponent<AudioSource>();
			audioSource.transform.SetParent(root.transform);
			audioSource.playOnAwake = false;
			audioSource.loop = false;
			audioSource.spatialBlend = 1f;
			audioSource.rolloffMode = AudioRolloffMode.Custom;
			audioSource.dopplerLevel = 0f;
			audioSource.maxDistance = 400f;
			audioSource.spread = 30f;
			audioSource.priority = 64;
			audioSource.outputAudioMixerGroup = FBAudio.Group(voice: true);
			slots.Add(new Slot
			{
				src = audioSource
			});
		}
	}

	public static int Key(FBCharacter a)
	{
		if (!keys.TryGetValue(a, out var value))
		{
			value = (keys[a] = nextKey++);
		}
		return value;
	}

	private static Who Reg(FBCharacter a)
	{
		int key = Key(a);
		if (!FBVoice.who.TryGetValue(key, out var value) || value.skin != a.skinId)
		{
			FBVoiceProfiles i = FBVoiceProfiles.I;
			FBVoiceProfile prof = i.ProfileOf(i.SexOf(a.skinId, a.npcSet?.sex));
			value = (FBVoice.who[key] = new Who
			{
				ch = a,
				prof = prof,
				pitch = i.PitchOf(a.skinId, prof),
				skin = a.skinId
			});
		}
		return value;
	}

	public static string SexOf(FBCharacter a)
	{
		Ensure();
		return Reg(a).prof.sex;
	}

	private static AudioClip ClipOf(int k, FBVoiceBank b, int v)
	{
		if (!who.TryGetValue(k, out var value))
		{
			return null;
		}
		return FBAudio.Clip("Voice/" + value.prof.Clip(b, v));
	}

	private static float PitchOf(int k, FBVoiceBank b)
	{
		Who value;
		return (who.TryGetValue(k, out value) ? value.pitch : 1f) * FBVoiceProfiles.I.Rule(b).pitch;
	}

	public static void OnDamage(in FBDamage d)
	{
		FBCharacter victim = d.victim;
		if ((bool)victim)
		{
			Ensure();
			Reg(victim);
			string text = d.hit.part ?? "";
			string text2 = d.why ?? "";
			FBDamageType type = ((d.type != FBDamageType.Other || !d.source || !(d.source != victim)) ? d.type : FBDamageType.Bullet);
			lastType[Key(victim)] = type.ToString();
			if (trace != null)
			{
				trace(new Trace
				{
					t = Time.time,
					ch = victim,
					sex = Reg(victim).prof.sex,
					kind = "dano",
					type = type.ToString() + (d.head ? "/cabeça" : ((text != "") ? ("/" + text) : "")) + (d.killed ? "/morto" : (d.knocked ? "/caído" : "")),
					amount = d.applied,
					hp = d.hpAfter,
					dist = DistToListener(victim.Pos),
					voices = D.ActiveVoices(Time.time)
				});
			}
			FBVoiceHit fBVoiceHit = default(FBVoiceHit);
			fBVoiceHit.key = Key(victim);
			fBVoiceHit.type = type;
			fBVoiceHit.applied = d.applied;
			fBVoiceHit.hpAfter = d.hpAfter;
			fBVoiceHit.maxHp = victim.maxHp;
			fBVoiceHit.head = d.head || text == "head";
			fBVoiceHit.limb = text.Contains("arm") || text.Contains("leg") || text.Contains("hand") || text.Contains("foot");
			fBVoiceHit.killed = d.killed;
			fBVoiceHit.knocked = d.knocked;
			fBVoiceHit.down = victim.knocked && !d.knocked;
			fBVoiceHit.isPlayer = victim.isPlayer;
			fBVoiceHit.fire = text2.Contains("fire") || text2.Contains("burn") || text2.Contains("fogo") || text2.Contains("molotov");
			FBVoiceHit h = fBVoiceHit;
			D.Hit(in h, Time.time);
			if (victim.isPlayer && (d.type == FBDamageType.Bullet || d.type == FBDamageType.Explosion) && d.applied > 0f && Time.time - bodyHitT > 0.12f)
			{
				bodyHitT = Time.time;
				FBAudio.Play2D("ie_bodyhit", 0.5f, 0f, UnityEngine.Random.Range(0.9f, 1.1f));
			}
		}
	}

	public static void Hurt(FBCharacter a, float dmg, string why)
	{
		if ((bool)a)
		{
			FBDamageType type = FBDamageEvents.TypeOf(why, default(FBHitInfo));
			FBDamage d = default(FBDamage);
			d.victim = a;
			d.type = type;
			d.amount = dmg;
			d.applied = dmg;
			d.hpBefore = a.hp + dmg;
			d.hpAfter = a.hp;
			d.why = why;
			OnDamage(in d);
		}
	}

	public static void OnNet(FBCharacter a, int bank, int variation, float intensity)
	{
		if ((bool)a && bank >= 0 && bank < Enum.GetValues(typeof(FBVoiceBank)).Length)
		{
			Ensure();
			Reg(a);
			D.External(Key(a), (FBVoiceBank)bank, variation, intensity, Time.time);
		}
	}

	private static void Perform(FBVoiceCue c)
	{
		if (!who.TryGetValue(c.key, out var value) || !value.ch)
		{
			return;
		}
		if (c.interrupt || c.steal >= 0)
		{
			foreach (Slot slot2 in slots)
			{
				if (slot2.key == c.key || slot2.key == c.steal)
				{
					slot2.src.Stop();
					slot2.key = -1;
					slot2.follow = null;
				}
			}
		}
		AudioClip audioClip = ClipOf(c.key, c.bank, c.variation);
		if (!audioClip)
		{
			return;
		}
		FBVoiceRule fBVoiceRule = FBVoiceProfiles.I.Rule(c.bank);
		FBCharacter ch = value.ch;
		bool flag = ch.isPlayer && !ch.isRemote;
		Slot slot = null;
		foreach (Slot slot3 in slots)
		{
			if (!slot3.src.isPlaying)
			{
				slot = slot3;
				break;
			}
		}
		if (slot == null)
		{
			slot = slots[0];
			foreach (Slot slot4 in slots)
			{
				if (slot4.src.time > slot.src.time)
				{
					slot = slot4;
				}
			}
			slot.src.Stop();
		}
		AudioSource src = slot.src;
		slot.key = c.key;
		slot.follow = (ch.Head ? ch.Head : ch.transform);
		slot.lift = (ch.Head ? 0.05f : (1.5f * ch.ScaleK));
		src.transform.position = slot.follow.position + Vector3.up * slot.lift;
		src.clip = audioClip;
		src.pitch = PitchOf(c.key, c.bank) * (1f + (UnityEngine.Random.value - 0.5f) * 0.06f);
		src.volume = Mathf.Clamp01(fBVoiceRule.vol * Mathf.Lerp(0.55f, 1f, c.intensity) * (flag ? 0.75f : 1f) * FBAudio.VoiceVol * FBAudio.Master);
		src.spatialBlend = ((!flag) ? 1 : 0);
		if (!flag)
		{
			src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FBAudio.Rolloff(fBVoiceRule.refD, 1.15f));
		}
		src.Play();
		if (trace != null)
		{
			trace(new Trace
			{
				t = Time.time,
				ch = ch,
				sex = value.prof.sex,
				kind = "voz",
				type = (lastType.TryGetValue(c.key, out var value2) ? value2 : ""),
				bank = c.bank.ToString(),
				decision = ((c.steal >= 0) ? "tocou (roubou vaga)" : (c.interrupt ? "tocou (interrompeu)" : "tocou")),
				clip = audioClip.name,
				vol = src.volume,
				pitch = src.pitch,
				intensity = c.intensity,
				hp = ch.hp,
				dist = (flag ? 0f : DistToListener(src.transform.position)),
				voices = D.ActiveVoices(Time.time)
			});
		}
		if (flag && (bool)FBNet.I)
		{
			FBNet.I.SendVoice((int)c.bank, c.variation, c.intensity);
		}
	}

	internal static void Tick()
	{
		if (D == null)
		{
			return;
		}
		D.Tick(Time.time);
		foreach (Slot slot in slots)
		{
			if (slot.key >= 0)
			{
				if (!slot.src.isPlaying)
				{
					slot.key = -1;
					slot.follow = null;
				}
				else if ((bool)slot.follow)
				{
					slot.src.transform.position = slot.follow.position + Vector3.up * slot.lift;
				}
				else
				{
					slot.src.Stop();
				}
			}
		}
		if (Time.frameCount % 120 != 0)
		{
			return;
		}
		List<int> list = null;
		foreach (KeyValuePair<int, Who> item in who)
		{
			if (!item.Value.ch)
			{
				(list ?? (list = new List<int>())).Add(item.Key);
			}
		}
		if (list != null)
		{
			foreach (int item2 in list)
			{
				who.Remove(item2);
				D.Forget(item2);
			}
		}
		List<FBCharacter> list2 = null;
		foreach (KeyValuePair<FBCharacter, int> key in keys)
		{
			if (!key.Key)
			{
				(list2 ?? (list2 = new List<FBCharacter>())).Add(key.Key);
			}
		}
		if (list2 == null)
		{
			return;
		}
		foreach (FBCharacter item3 in list2)
		{
			keys.Remove(item3);
		}
	}
}
}
