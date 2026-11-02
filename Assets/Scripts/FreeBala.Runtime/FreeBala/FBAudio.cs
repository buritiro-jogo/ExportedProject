using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace FreeBala
{
public static class FBAudio
{
	private class Loop
	{
		public AudioSource src;

		public float target;

		public float cur;

		public string name;
	}

	private class Buf
	{
		public float[] d;

		public Buf(float s)
		{
			d = new float[Mathf.CeilToInt(44100f * s)];
		}
	}

	public static float Master = 0.9f;

	public static float SfxVol = 1f;

	public static float MusicVol = 0.35f;

	public static float VoiceVol = 1f;

	private static bool mixTried;

	private static AudioMixerGroup sfxG;

	private static AudioMixerGroup voiceG;

	private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

	private static readonly HashSet<string> missing = new HashSet<string>();

	private static GameObject root;

	private static readonly List<AudioSource> pool3 = new List<AudioSource>();

	private static readonly List<AudioSource> pool2 = new List<AudioSource>();

	private static int i3;

	private static int i2;

	private static readonly Dictionary<string, Loop> loops = new Dictionary<string, Loop>();

	private static readonly string[] Dirs = new string[9] { "User/", "", "sfx/", "Weapons/", "wsnd/", "veh/", "sg/", "amb/", "vo/" };

	private static Dictionary<string, string[]> alias;

	private static Dictionary<string, object> pack171;

	private static bool aliasTried;

	private static readonly Dictionary<int, AnimationCurve> curves = new Dictionary<int, AnimationCurve>();

	public static Action<string, Vector3, float> Played;

	private static Transform listener;

	private static readonly Dictionary<string, AudioClip> synth = new Dictionary<string, AudioClip>();

	private static readonly Dictionary<string, string> SG = new Dictionary<string, string>
	{
		["tick"] = "sg_tick",
		["count5"] = "sg_count5",
		["beep"] = "",
		["riser"] = "",
		["round"] = "sg_cs_newround",
		["lastround"] = "sg_cs_lastround",
		["go"] = "sg_cs_round",
		["buy"] = "sg_cs_buy",
		["win"] = "sg_cs_victory",
		["lose"] = "sg_match_defeat"
	};

	private const int SR = 44100;

	private static AudioListener ear;

	private static int earFrame = -1;

	public static Transform Ear
	{
		get
		{
			if (earFrame != Time.frameCount || !ear || !ear.isActiveAndEnabled)
			{
				earFrame = Time.frameCount;
				ear = null;
				AudioListener[] array = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
				foreach (AudioListener audioListener in array)
				{
					if (audioListener.isActiveAndEnabled)
					{
						ear = audioListener;
						break;
					}
				}
			}
			if (!ear)
			{
				if (!Camera.main)
				{
					return null;
				}
				return Camera.main.transform;
			}
			return ear.transform;
		}
	}

	public static AudioMixerGroup Group(bool voice)
	{
		if (!mixTried)
		{
			mixTried = true;
			AudioMixer audioMixer = Resources.Load<AudioMixer>("Audio/FBMixer");
			if ((bool)audioMixer)
			{
				AudioMixerGroup[] array = audioMixer.FindMatchingGroups("SFX");
				sfxG = ((array != null && array.Length > 0) ? array[0] : null);
				AudioMixerGroup[] array2 = audioMixer.FindMatchingGroups("Voz");
				voiceG = ((array2 != null && array2.Length > 0) ? array2[0] : sfxG);
			}
		}
		if (!voice)
		{
			return sfxG;
		}
		return voiceG;
	}

	private static void LoadPack171()
	{
		if (aliasTried)
		{
			return;
		}
		aliasTried = true;
		TextAsset textAsset = Resources.Load<TextAsset>("Audio/171/aliases");
		if (!textAsset)
		{
			return;
		}
		pack171 = Json.Parse(textAsset.text) as Dictionary<string, object>;
		alias = new Dictionary<string, string[]>();
		foreach (KeyValuePair<string, object> item in Json.O(pack171, "aliases") ?? new Dictionary<string, object>())
		{
			string[] array = (from s in (item.Value as List<object>)?.Select((object o) => o as string)
				where s != null
				select s).ToArray();
			if (array != null && array.Length != 0)
			{
				alias[item.Key] = array;
			}
		}
	}

	private static AudioClip Clip171(string n)
	{
		if (cache.TryGetValue(n, out var value))
		{
			return value;
		}
		value = Resources.Load<AudioClip>("Audio/171/" + n);
		if ((bool)value)
		{
			cache[n] = value;
		}
		return value;
	}

	public static string Weapon171(string id, string cls, string slot)
	{
		LoadPack171();
		Dictionary<string, object> dictionary = Json.O(pack171, "armas");
		if (dictionary == null)
		{
			return null;
		}
		string text = Json.S(Json.O(dictionary, id ?? ""), slot) ?? Json.S(Json.O(dictionary, "class:" + cls), slot);
		if (text == null || !Clip171(text))
		{
			return null;
		}
		return text;
	}

	public static AudioClip Clip(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		LoadPack171();
		if (alias != null && alias.TryGetValue(name, out var value))
		{
			AudioClip audioClip = Clip171(value[UnityEngine.Random.Range(0, value.Length)]);
			if ((bool)audioClip)
			{
				return audioClip;
			}
		}
		if (name.StartsWith("x171_"))
		{
			return Clip171(name);
		}
		if (cache.TryGetValue(name, out var value2))
		{
			return value2;
		}
		if (missing.Contains(name))
		{
			return null;
		}
		string[] dirs = Dirs;
		foreach (string text in dirs)
		{
			value2 = Resources.Load<AudioClip>("Audio/" + text + name);
			if ((bool)value2)
			{
				cache[name] = value2;
				return value2;
			}
		}
		missing.Add(name);
		return null;
	}

	public static bool Has(string name)
	{
		return Clip(name);
	}

	private static void Ensure()
	{
		if (!root)
		{
			// Correção: se o FB_Audio foi destruído, as listas ainda guardavam as fontes mortas e metade dos sons
			// dava MissingReferenceException (e o erro interrompia quem chamou, como a saída do carro).
			pool3.Clear();
			pool2.Clear();
			root = new GameObject("FB_Audio");
			UnityEngine.Object.DontDestroyOnLoad(root);
			root.AddComponent<FBAudioTicker>();
			for (int i = 0; i < 32; i++)
			{
				pool3.Add(New3D(i));
			}
			for (int j = 0; j < 12; j++)
			{
				pool2.Add(New2D());
			}
		}
	}

	private static AudioSource New3D(int i)
	{
		AudioSource audioSource = new GameObject("3D" + i).AddComponent<AudioSource>();
		audioSource.transform.SetParent(root.transform);
		audioSource.playOnAwake = false;
		audioSource.spatialBlend = 1f;
		audioSource.rolloffMode = AudioRolloffMode.Custom;
		audioSource.dopplerLevel = 0f;
		audioSource.maxDistance = 400f;
		audioSource.priority = 64;
		audioSource.outputAudioMixerGroup = Group(voice: false);
		AudioLowPassFilter audioLowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
		audioLowPassFilter.cutoffFrequency = 22000f;
		audioLowPassFilter.lowpassResonanceQ = 1f;
		return audioSource;
	}

	private static AudioSource New2D()
	{
		AudioSource audioSource = root.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		audioSource.spatialBlend = 0f;
		audioSource.priority = 32;
		audioSource.outputAudioMixerGroup = Group(voice: false);
		return audioSource;
	}

	// Correção: uma fonte do grupo que foi destruída é trocada por uma nova em vez de quebrar o som.
	private static AudioSource Next(List<AudioSource> pool, ref int idx, bool is3D)
	{
		int k = idx++ % pool.Count;
		if (!pool[k])
		{
			pool[k] = (is3D ? New3D(k) : New2D());
		}
		return pool[k];
	}

	private static AnimationCurve Inverse(float refD, float roll, float max)
	{
		List<Keyframe> list = new List<Keyframe>();
		for (int i = 0; i <= 24; i++)
		{
			float num = Mathf.Lerp(0f, max, Mathf.Pow((float)i / 24f, 2f));
			float value = ((num <= refD) ? 1f : (refD / (refD + roll * (num - refD))));
			list.Add(new Keyframe(num / max, value));
		}
		return new AnimationCurve(list.ToArray());
	}

	public static AnimationCurve RolloffCurve(float refD, float roll, float max)
	{
		return Inverse(refD, roll, max);
	}

	public static AnimationCurve Rolloff(float refD, float rolloff = 1.1f)
	{
		int key = Mathf.RoundToInt(refD * 10f) * 100 + Mathf.RoundToInt(rolloff * 10f);
		if (!curves.TryGetValue(key, out var value))
		{
			value = (curves[key] = Inverse(refD, rolloff, 400f));
		}
		return value;
	}

	public static AudioSource Play2D(string name, float vol = 1f, float jitter = 0.08f, float? rate = null, bool music = false)
	{
		AudioClip audioClip = Clip(name);
		if (!audioClip)
		{
			return null;
		}
		return Play2D(audioClip, vol, jitter, rate, music);
	}

	public static AudioSource Play2D(AudioClip c, float vol = 1f, float jitter = 0.08f, float? rate = null, bool music = false)
	{
		if (!c || !float.IsFinite(vol))
		{
			return null;
		}
		Ensure();
		AudioSource audioSource = Next(pool2, ref i2, is3D: false);
		audioSource.clip = c;
		audioSource.loop = false;
		audioSource.volume = Mathf.Clamp01(vol * (music ? MusicVol : SfxVol) * Master);
		audioSource.pitch = rate ?? (1f + (UnityEngine.Random.value - 0.5f) * jitter);
		audioSource.Play();
		return audioSource;
	}

	public static AudioSource PlayAt(string name, Vector3 pos, float vol = 1f, float refD = 4f, float jitter = 0.08f, float dur = 0f, float rolloff = 1.1f)
	{
		Played?.Invoke(name, pos, vol);
		return PlayAt(Clip(name), pos, vol, refD, jitter, dur, rolloff);
	}

	public static float LoopLevel(string key)
	{
		if (!loops.TryGetValue(key, out var value) || !value.src)
		{
			return 0f;
		}
		return value.target;
	}

	public static string LoopClip(string key)
	{
		if (!loops.TryGetValue(key, out var value) || !value.src || !value.src.clip)
		{
			return null;
		}
		return value.src.clip.name;
	}

	public static AudioSource PlayAt(AudioClip c, Vector3 pos, float vol = 1f, float refD = 4f, float jitter = 0.08f, float dur = 0f, float rolloff = 1.1f)
	{
		if (!c || !float.IsFinite(pos.x + pos.y + pos.z))
		{
			return null;
		}
		Ensure();
		AudioSource audioSource = Next(pool3, ref i3, is3D: true);
		audioSource.transform.position = pos;
		audioSource.clip = c;
		audioSource.loop = false;
		audioSource.volume = Mathf.Clamp01(vol * SfxVol * Master);
		audioSource.pitch = 1f + (UnityEngine.Random.value - 0.5f) * jitter;
		AudioLowPassFilter component = audioSource.GetComponent<AudioLowPassFilter>();
		if ((bool)component)
		{
			component.cutoffFrequency = 22000f;
		}
		audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff(refD, rolloff));
		audioSource.Play();
		if (dur > 0f)
		{
			audioSource.SetScheduledEndTime(AudioSettings.dspTime + (double)dur);
		}
		return audioSource;
	}

	public static AudioSource PlayShotAt(string name, Vector3 pos, float vol, float refD, float rolloff = 1.5f)
	{
		Transform transform = Listener();
		float num = (transform ? Vector3.Distance(transform.position, pos) : 0f);
		bool flag = false;
		if ((bool)transform && num > 1.5f)
		{
			Vector3 direction = (pos - transform.position) / num;
			flag = Physics.Raycast(transform.position, direction, num - 0.6f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		}
		AudioSource audioSource = PlayAt(name, pos, vol * (flag ? 0.62f : 1f), refD, 0.08f, 0f, rolloff);
		if (!audioSource)
		{
			return null;
		}
		audioSource.priority = 16;
		AudioLowPassFilter component = audioSource.GetComponent<AudioLowPassFilter>();
		if ((bool)component)
		{
			component.cutoffFrequency = Mathf.Lerp(flag ? 2600 : 22000, flag ? 900 : 5200, Mathf.InverseLerp(10f, 180f, num));
		}
		return audioSource;
	}

	private static Transform Listener()
	{
		if (!listener)
		{
			AudioListener audioListener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
			listener = (audioListener ? audioListener.transform : (Camera.main ? Camera.main.transform : null));
		}
		return listener;
	}

	public static void SetLoop(string key, string name, float vol, Vector3? pos = null, float? rate = null, float refD = 6f, float rolloff = 1.1f)
	{
		if (!float.IsFinite(vol))
		{
			vol = 0f;
		}
		Ensure();
		if (loops.TryGetValue(key, out var value) && (value.name != name || !value.src))
		{
			StopLoop(key);
			value = null;
		}
		if (value == null)
		{
			AudioClip audioClip = Clip(name);
			if (!audioClip)
			{
				return;
			}
			GameObject gameObject = new GameObject("Loop_" + key);
			gameObject.transform.SetParent(root.transform);
			AudioSource audioSource = gameObject.AddComponent<AudioSource>();
			audioSource.clip = audioClip;
			audioSource.loop = true;
			audioSource.volume = 0f;
			audioSource.playOnAwake = false;
			audioSource.priority = LoopPriority(key, pos.HasValue);
			audioSource.spatialBlend = (pos.HasValue ? 1 : 0);
			audioSource.rolloffMode = AudioRolloffMode.Custom;
			audioSource.maxDistance = 400f;
			audioSource.dopplerLevel = 0f;
			audioSource.outputAudioMixerGroup = Group(voice: false);
			if (pos.HasValue)
			{
				audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Inverse(refD, rolloff, 400f));
			}
			audioSource.time = UnityEngine.Random.value * audioClip.length * 0.9f;
			audioSource.Play();
			value = (loops[key] = new Loop
			{
				src = audioSource,
				name = name
			});
		}
		value.target = vol * SfxVol * Master;
		if (rate.HasValue && float.IsFinite(rate.Value))
		{
			value.src.pitch = Mathf.Lerp(value.src.pitch, rate.Value, 0.15f);
		}
		if (pos.HasValue)
		{
			value.src.transform.position = pos.Value;
		}
	}

	private static int LoopPriority(string key, bool is3D)
	{
		if (key.StartsWith("veh_") && !key.StartsWith("veh_remote_") && !key.StartsWith("veh_npc_"))
		{
			return 12;
		}
		if (key.StartsWith("som_") || key.StartsWith("paredao"))
		{
			return 40;
		}
		if (!is3D)
		{
			return 110;
		}
		return 170;
	}

	public static void StopLoop(string key)
	{
		if (loops.TryGetValue(key, out var value))
		{
			loops.Remove(key);
			if ((bool)value.src)
			{
				UnityEngine.Object.Destroy(value.src.gameObject);
			}
		}
	}

	public static void StopAllLoops()
	{
		foreach (string item in new List<string>(loops.Keys))
		{
			StopLoop(item);
		}
	}

	internal static void Tick(float dt)
	{
		float t = 1f - Mathf.Exp((0f - dt) / 0.08f);
		foreach (Loop value in loops.Values)
		{
			if ((bool)value.src && value.cur != value.target)
			{
				value.cur = ((Mathf.Abs(value.target - value.cur) < 0.0001f) ? value.target : Mathf.Lerp(value.cur, value.target, t));
				value.src.volume = value.cur;
			}
		}
	}

	public static void Synth(string kind, int n = 0)
	{
		try
		{
			if (SG.TryGetValue(kind, out var value) && Has("sg_count5"))
			{
				if (value != "")
				{
					Play2D(value, (kind == "tick") ? 0.55f : 0.8f, 0f);
				}
			}
			else if (!(kind == "count5"))
			{
				if (kind == "lastround")
				{
					kind = "round";
				}
				string key = kind + n;
				if (!synth.TryGetValue(key, out var value2))
				{
					value2 = (synth[key] = Make(kind, n));
				}
				if ((bool)value2)
				{
					Play2D(value2, 0.85f, 0f);
				}
			}
		}
		catch
		{
		}
	}

	private static float Env(float t, float vol, float atk, float dur)
	{
		if (t < 0f || t > dur)
		{
			return 0f;
		}
		if (t < atk)
		{
			return Mathf.Lerp(0.0001f, vol, t / atk);
		}
		return vol * Mathf.Pow(0.0001f / Mathf.Max(0.0002f, vol), (t - atk) / Mathf.Max(0.001f, dur - atk));
	}

	private static void Osc(Buf b, string type, float f0, float f1, float t0, float dur, float vol, float atk = 0.004f)
	{
		double num = 0.0;
		int num2 = (int)(t0 * 44100f);
		int num3 = (int)((dur + 0.05f) * 44100f);
		for (int i = 0; i < num3 && num2 + i < b.d.Length; i++)
		{
			float num4 = (float)i / 44100f;
			float num5 = ((f0 == f1) ? f0 : (f0 * Mathf.Pow(f1 / f0, Mathf.Min(1f, num4 / dur))));
			num += (double)(num5 / 44100f);
			float num6 = (float)(num - Math.Floor(num));
			float num7 = type switch
			{
				"triangle" => 1f - 4f * Mathf.Abs(num6 - 0.5f), 
				"square" => (num6 < 0.5f) ? 1 : (-1), 
				"sine" => Mathf.Sin(num6 * (float)Math.PI * 2f), 
				_ => 2f * num6 - 1f, 
			};
			b.d[num2 + i] += num7 * Env(num4, vol, atk, dur);
		}
	}

	private static void Noise(Buf b, float t0, float dur, float vol, string type, float f0, float f1, float q = 1f, float atk = 0.003f)
	{
		int num = (int)(t0 * 44100f);
		int num2 = (int)((dur + 0.05f) * 44100f);
		float num3 = 0f;
		float num4 = 0f;
		float num5 = 0f;
		float num6 = 0f;
		System.Random random = new System.Random(num + num2);
		for (int i = 0; i < num2 && num + i < b.d.Length; i++)
		{
			float num7 = (float)i / 44100f;
			float a = ((f0 == f1) ? f0 : (f0 * Mathf.Pow(f1 / f0, Mathf.Min(1f, num7 / dur))));
			float num8 = (float)random.NextDouble() * 2f - 1f;
			float f2 = (float)Math.PI * 2f * Mathf.Min(a, 19845f) / 44100f;
			float num9 = Mathf.Sin(f2) / (2f * q);
			float num10 = Mathf.Cos(f2);
			float num11 = 1f + num9;
			float num12 = -2f * num10;
			float num13 = 1f - num9;
			float num14;
			float num15;
			float num16;
			if (type == "lowpass")
			{
				num14 = (1f - num10) / 2f;
				num15 = 1f - num10;
				num16 = num14;
			}
			else if (type == "highpass")
			{
				num14 = (1f + num10) / 2f;
				num15 = 0f - (1f + num10);
				num16 = num14;
			}
			else
			{
				num14 = num9;
				num15 = 0f;
				num16 = 0f - num9;
			}
			float num17 = (num14 * num8 + num15 * num5 + num16 * num6 - num12 * num3 - num13 * num4) / num11;
			num6 = num5;
			num5 = num8;
			num4 = num3;
			num3 = num17;
			b.d[num + i] += num17 * Env(num7, vol, atk, dur);
		}
	}

	private static AudioClip Make(string kind, int n)
	{
		Buf buf = new Buf((kind == "riser") ? ((float)((n > 0) ? n : 3) + 0.3f) : ((kind == "go") ? 1.5f : 1.1f));
		switch (kind)
		{
		case "mark_enemy":
			Osc(buf, "square", 1320f, 1320f, 0f, 0.07f, 0.07f);
			Osc(buf, "square", 1760f, 1760f, 0.09f, 0.09f, 0.07f);
			Osc(buf, "sine", 880f, 880f, 0f, 0.2f, 0.12f);
			break;
		case "mark":
			Osc(buf, "sine", 980f, 1180f, 0f, 0.16f, 0.13f);
			Osc(buf, "triangle", 1960f, 2360f, 0f, 0.08f, 0.04f);
			break;
		case "tick":
			Osc(buf, "sine", 1650f, 1400f, 0f, 0.06f, 0.16f);
			Osc(buf, "triangle", 3300f, 3000f, 0f, 0.025f, 0.05f);
			Noise(buf, 0f, 0.025f, 0.1f, "highpass", 5000f, 5000f);
			break;
		case "beep":
		{
			float num4 = n switch
			{
				1 => 880, 
				2 => 740, 
				3 => 622, 
				_ => 700, 
			};
			Osc(buf, "sine", num4, num4, 0f, 0.42f, 0.26f);
			Osc(buf, "square", num4, num4, 0f, 0.16f, 0.05f);
			Osc(buf, "sine", num4 * 2f, num4 * 2f, 0f, 0.18f, 0.06f);
			Osc(buf, "sine", num4 / 2f, num4 / 2f, 0f, 0.5f, 0.16f);
			Osc(buf, "sine", 110f, 46f, 0f, 0.32f, 0.55f, 0.002f);
			Noise(buf, 0f, 0.03f, 0.18f, "bandpass", 2600f, 2600f, 1.5f);
			break;
		}
		case "riser":
		{
			float num5 = ((n > 0) ? n : 3);
			Osc(buf, "sawtooth", 110f, 440f, 0f, num5, 0.09f, num5 * 0.95f);
			Noise(buf, 0f, num5, 0.16f, "bandpass", 400f, 7500f, 2.5f, num5 * 0.97f);
			break;
		}
		case "go":
		{
			Osc(buf, "sine", 130f, 34f, 0f, 1.1f, 0.95f, 0.002f);
			Osc(buf, "triangle", 65f, 30f, 0f, 0.8f, 0.4f, 0.002f);
			Noise(buf, 0f, 0.9f, 0.55f, "lowpass", 9000f, 380f, 0.7f, 0.001f);
			Noise(buf, 0f, 0.08f, 0.4f, "highpass", 2500f, 2500f);
			(float, float)[] array2 = new(float, float)[4]
			{
				(146.8f, 0.11f),
				(220f, 0.09f),
				(293.7f, 0.08f),
				(440f, 0.05f)
			};
			for (int j = 0; j < array2.Length; j++)
			{
				var (num, vol) = array2[j];
				Osc(buf, "sawtooth", num, num, 0f, 1.2f, vol);
			}
			Osc(buf, "sine", 1760f, 1760f, 0.02f, 0.6f, 0.05f);
			Osc(buf, "sine", 2637f, 2637f, 0.05f, 0.5f, 0.03f);
			break;
		}
		case "round":
			Noise(buf, 0f, 0.55f, 0.22f, "bandpass", 350f, 3600f, 1.4f, 0.25f);
			Osc(buf, "sine", 85f, 40f, 0.45f, 0.55f, 0.7f, 0.002f);
			Noise(buf, 0.45f, 0.25f, 0.25f, "lowpass", 4000f, 300f);
			break;
		case "buy":
			Osc(buf, "sine", 1318.5f, 1318.5f, 0f, 0.16f, 0.16f);
			Osc(buf, "sine", 1975.5f, 1975.5f, 0.07f, 0.3f, 0.14f);
			Noise(buf, 0f, 0.04f, 0.12f, "bandpass", 4200f, 4200f, 3f);
			break;
		case "medopen":
			Noise(buf, 0f, 0.22f, 0.32f, "bandpass", 1800f, 5200f, 1.2f, 0.01f);
			Noise(buf, 0.24f, 0.4f, 0.16f, "highpass", 3000f, 6500f, 2f);
			Osc(buf, "square", 2400f, 2300f, 0.7f, 0.02f, 0.05f);
			Noise(buf, 0.7f, 0.03f, 0.2f, "bandpass", 3800f, 3800f, 4f);
			break;
		case "heal":
		{
			Noise(buf, 0f, 0.16f, 0.2f, "bandpass", 700f, 3000f, 1.5f, 0.01f);
			Noise(buf, 0.18f, 0.34f, 0.12f, "highpass", 2600f, 5400f, 1.7f, 0.015f);
			Osc(buf, "sine", 120f, 48f, 0.25f, 0.25f, 0.24f, 0.002f);
			for (int l = 0; l < 3; l++)
			{
				float num6 = (new float[3] { 392f, 523.3f, 659.3f })[l];
				Osc(buf, "triangle", num6, num6 * 1.04f, 0.28f + (float)l * 0.08f, 0.32f, 0.055f);
			}
			break;
		}
		case "medcancel":
			Noise(buf, 0f, 0.12f, 0.14f, "bandpass", 2600f, 900f, 1.4f);
			Osc(buf, "sine", 520f, 330f, 0f, 0.16f, 0.07f);
			break;
		case "glass":
		{
			float num2 = ((n <= 0) ? 1 : n);
			Noise(buf, 0f, 0.06f, 0.55f * num2, "highpass", 2500f, 1800f, 1f, 0.001f);
			Noise(buf, 0.01f, 0.45f, 0.32f * num2, "bandpass", 6500f, 3500f, 1.6f, 0.002f);
			for (int k = 0; k < 9; k++)
			{
				float num3 = 2600f + UnityEngine.Random.value * 4200f;
				float t = 0.04f + UnityEngine.Random.value * 0.5f;
				Osc(buf, "sine", num3, num3 * 0.98f, t, 0.09f, 0.05f * num2);
			}
			break;
		}
		case "deny":
			Osc(buf, "square", 155f, 140f, 0f, 0.2f, 0.07f);
			Osc(buf, "square", 117f, 110f, 0.11f, 0.22f, 0.07f);
			break;
		case "hover":
			Osc(buf, "sine", 2400f, 2600f, 0f, 0.035f, 0.03f);
			break;
		case "hit_body":
			Noise(buf, 0f, 0.075f, 0.22f, "bandpass", 280f, 1350f, 1.4f, 0.001f);
			Osc(buf, "triangle", 118f, 63f, 0f, 0.11f, 0.22f, 0.001f);
			break;
		case "hit_head":
			Noise(buf, 0f, 0.065f, 0.28f, "bandpass", 1100f, 4200f, 2.2f, 0.001f);
			Osc(buf, "sine", 980f, 620f, 0f, 0.12f, 0.12f, 0.001f);
			Osc(buf, "triangle", 220f, 110f, 0f, 0.1f, 0.12f, 0.001f);
			break;
		case "zone":
			Osc(buf, "sine", 540f, 540f, 0f, 0.16f, 0.11f, 0.003f);
			Osc(buf, "sine", 410f, 410f, 0.19f, 0.2f, 0.11f, 0.003f);
			Noise(buf, 0.37f, 0.22f, 0.08f, "bandpass", 650f, 1500f, 1.2f, 0.04f);
			break;
		case "lose":
		case "win":
		{
			float[] array = ((!(kind == "win")) ? new float[4] { 440f, 392f, 349.2f, 261.6f } : new float[4] { 523.3f, 659.3f, 784f, 1046.5f });
			for (int i = 0; i < 4; i++)
			{
				Osc(buf, "triangle", array[i], array[i], (float)i * 0.11f, 0.5f, 0.16f);
				Osc(buf, "sine", array[i] / 2f, array[i] / 2f, (float)i * 0.11f, 0.5f, 0.08f);
			}
			Osc(buf, "sine", 90f, 45f, 0f, 0.5f, 0.4f, 0.002f);
			break;
		}
		default:
			return null;
		}
		for (int m = 0; m < buf.d.Length; m++)
		{
			buf.d[m] = (float)Math.Tanh(buf.d[m] * 1.1f);
		}
		AudioClip audioClip = AudioClip.Create("synth_" + kind + n, buf.d.Length, 1, 44100, stream: false);
		audioClip.SetData(buf.d, 0);
		return audioClip;
	}

	public static void Listener(Transform cam)
	{
		Ensure();
		root.GetComponent<AudioListener>();
		if (!UnityEngine.Object.FindAnyObjectByType<AudioListener>())
		{
			root.AddComponent<AudioListener>();
		}
		if ((bool)cam && (bool)root.GetComponent<AudioListener>())
		{
			root.transform.SetPositionAndRotation(cam.position, cam.rotation);
		}
	}
}
}
