using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeBala
{
public static class FBSiren
{
	private class S
	{
		public FBCar car;

		public AudioSource src;

		public AudioLowPassFilter lp;

		public int mode;

		public float vol = 1f;

		public float gain;

		public float occK;

		public float ot;

		public bool occ;

		public bool stopping;

		public FBGiroflex.Bar bar;

		public bool ownBar;
	}

	private class Runner : MonoBehaviour
	{
		private static readonly int __fbt_LateUpdate = FBTempo.Id("FBSiren.Runner.LateUpdate");

		private void LateUpdate()
		{
			if (!FBTempo.On)
			{
				LateUpdate__fbt();
				return;
			}
			long t0 = FBTempo.T0();
			LateUpdate__fbt();
			FBTempo.Soma(__fbt_LateUpdate, t0);
		}

		private void LateUpdate__fbt()
		{
			Tick(Time.deltaTime);
		}
	}

	public static readonly string[] MODES = new string[4] { null, "police_siren_loop", "police_siren_rapida", "police_siren_wail" };

	private static readonly string[] NAMES = new string[4] { "DESLIGADA", "PADRÃO", "RÁPIDA", "LENTA" };

	public const float NEAR = 6f;

	public const float FAR = 320f;

	private static readonly Dictionary<FBCar, S> all = new Dictionary<FBCar, S>();

	private static AnimationCurve curve;

	private static Runner runner;

	private static readonly List<S> dead = new List<S>();

	private static Runner R
	{
		get
		{
			if (!runner)
			{
				GameObject gameObject = new GameObject("FBSiren");
				Object.DontDestroyOnLoad(gameObject);
				runner = gameObject.AddComponent<Runner>();
			}
			return runner;
		}
	}

	public static bool IsPolice(FBCar c)
	{
		if ((bool)c && c.kind != "moto" && c.id != null)
		{
			if (!c.id.Contains("Viatura") && !c.id.Contains("Caveirao"))
			{
				return c.id.Contains("Caveirão");
			}
			return true;
		}
		return false;
	}

	public static int Mode(FBCar c)
	{
		if (!c || !all.TryGetValue(c, out var value) || value.stopping)
		{
			return 0;
		}
		return value.mode;
	}

	public static bool Lights(FBCar c)
	{
		if ((bool)c && all.TryGetValue(c, out var value) && FBGiroflex.Alive(value.bar))
		{
			return value.bar.on;
		}
		return false;
	}

	public static void Set(FBCar car, int mode, float vol = 1f)
	{
		if (!car)
		{
			return;
		}
		_ = R;
		if (!all.TryGetValue(car, out var value))
		{
			if (mode <= 0)
			{
				return;
			}
			value = (all[car] = new S
			{
				car = car
			});
		}
		value.vol = Mathf.Clamp01(vol);
		if (mode <= 0)
		{
			value.stopping = true;
			return;
		}
		mode = Mathf.Clamp(mode, 1, MODES.Length - 1);
		AudioClip audioClip = FBAudio.Clip(MODES[mode]);
		if ((bool)audioClip)
		{
			if (!value.src)
			{
				Make(value);
			}
			if (value.mode != mode || !value.src.isPlaying || value.stopping)
			{
				value.src.clip = audioClip;
				value.src.time = Random.value * audioClip.length * 0.5f;
				value.src.Play();
			}
			value.mode = mode;
			value.stopping = false;
			Giro(value, on: true);
		}
	}

	public static void Volume(FBCar car, float vol)
	{
		if ((bool)car && all.TryGetValue(car, out var value))
		{
			value.vol = Mathf.Clamp01(vol);
		}
	}

	public static void SetLights(FBCar car, bool on)
	{
		if (!car)
		{
			return;
		}
		_ = R;
		if (!all.TryGetValue(car, out var value))
		{
			if (!on)
			{
				return;
			}
			value = (all[car] = new S
			{
				car = car,
				stopping = true
			});
		}
		Giro(value, on);
	}

	public static void Burst(FBCar car, bool pierce = false)
	{
		if ((bool)car)
		{
			AudioClip audioClip = FBAudio.Clip(pierce ? "police_siren_pierce" : "police_siren_yelp") ?? FBAudio.Clip("police_siren_yelp");
			if ((bool)audioClip)
			{
				GameObject gameObject = new GameObject("sirene_toque");
				gameObject.transform.SetParent(car.transform, worldPositionStays: false);
				gameObject.transform.position = Roof(car);
				AudioSource audioSource = gameObject.AddComponent<AudioSource>();
				audioSource.playOnAwake = false;
				audioSource.priority = 8;
				audioSource.clip = audioClip;
				audioSource.volume = 0.9f * FBAudio.SfxVol * FBAudio.Master;
				Shape(audioSource);
				audioSource.Play();
				Object.Destroy(gameObject, audioClip.length + 0.1f);
			}
		}
	}

	public static void Stop(FBCar car)
	{
		if ((bool)car && all.TryGetValue(car, out var value))
		{
			Kill(value);
		}
	}

	public static void Reset()
	{
		foreach (S item in new List<S>(all.Values))
		{
			Kill(item);
		}
		all.Clear();
	}

	private static Vector3 Roof(FBCar car)
	{
		Bounds lbox = car.lbox;
		return car.transform.TransformPoint(new Vector3(lbox.center.x, lbox.max.y + 0.1f, lbox.center.z + lbox.size.z * 0.05f));
	}

	private static void Shape(AudioSource a)
	{
		if (curve == null)
		{
			curve = new AnimationCurve();
			for (int i = 0; i <= 48; i++)
			{
				float num = 320f * (float)i * (float)i / 2304f;
				float num2 = ((num <= 6f) ? 1f : (6f / num));
				float num3 = Mathf.Clamp01((320f - num) / 80f);
				curve.AddKey(new Keyframe(num / 320f, num2 * num3 * num3));
			}
			for (int j = 0; j < curve.length; j++)
			{
				curve.SmoothTangents(j, 0f);
			}
		}
		a.spatialBlend = 1f;
		a.rolloffMode = AudioRolloffMode.Custom;
		a.maxDistance = 320f;
		a.minDistance = 6f;
		a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
		a.dopplerLevel = 0.25f;
		a.spread = 25f;
		a.outputAudioMixerGroup = FBAudio.Group(voice: false);
	}

	private static void Make(S s)
	{
		GameObject gameObject = new GameObject("sirene");
		gameObject.transform.SetParent(s.car.transform, worldPositionStays: false);
		Bounds lbox = s.car.lbox;
		gameObject.transform.localPosition = new Vector3(lbox.center.x, lbox.max.y + 0.1f, lbox.center.z + lbox.size.z * 0.05f);
		AudioSource audioSource = gameObject.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		audioSource.loop = true;
		audioSource.priority = 8;
		audioSource.volume = 0f;
		Shape(audioSource);
		s.lp = gameObject.AddComponent<AudioLowPassFilter>();
		s.lp.cutoffFrequency = 20000f;
		s.lp.lowpassResonanceQ = 1f;
		s.src = audioSource;
		s.gain = 0f;
	}

	private static void Giro(S s, bool on)
	{
		if (s.bar != null && !FBGiroflex.Alive(s.bar))
		{
			s.bar = null;
		}
		if (on && s.bar == null)
		{
			s.bar = FBGiroflex.Find(s.car);
			s.ownBar = s.bar == null;
			if (s.ownBar)
			{
				s.bar = FBGiroflex.Attach(s.car);
			}
		}
		if (s.bar != null)
		{
			s.bar.on = on;
		}
	}

	private static void Kill(S s)
	{
		if ((bool)s.src)
		{
			Object.Destroy(s.src.gameObject);
		}
		s.src = null;
		if (s.bar != null)
		{
			if (s.ownBar)
			{
				FBGiroflex.Detach(s.bar);
			}
			else
			{
				s.bar.on = false;
			}
		}
		s.bar = null;
		all.Remove(s.car);
	}

	private static void Tick(float dt)
	{
		Transform ear = FBAudio.Ear;
		Vector3 vector = (ear ? ear.position : Vector3.zero);
		dead.Clear();
		foreach (S value in all.Values)
		{
			if (!value.car)
			{
				dead.Add(value);
				continue;
			}
			if (!value.src)
			{
				if (value.stopping && (!FBGiroflex.Alive(value.bar) || !value.bar.on))
				{
					dead.Add(value);
				}
				continue;
			}
			float target = (value.stopping ? 0f : value.vol);
			value.gain = Mathf.MoveTowards(value.gain, target, dt / 0.06f);
			if (value.stopping && value.gain <= 0f)
			{
				Object.Destroy(value.src.gameObject);
				value.src = null;
				value.mode = 0;
				if (!FBGiroflex.Alive(value.bar) || !value.bar.on)
				{
					dead.Add(value);
				}
				continue;
			}
			Vector3 position = value.src.transform.position;
			float num = Vector3.Distance(position, vector);
			bool flag = FBDrive.Cur == value.car && value.car.lbox.Contains(value.car.transform.InverseTransformPoint(vector));
			if ((value.ot += dt) > 0.25f)
			{
				value.ot = 0f;
				value.occ = !flag && num > 5f && num < 320f && FBParedao.Occluded(vector, position);
			}
			value.occK = Mathf.MoveTowards(value.occK, value.occ ? 1 : 0, dt * 3f);
			float a = Mathf.Max(1800f, 20000f * Mathf.Exp((0f - num) / 140f));
			float b = (flag ? 4200f : Mathf.Lerp(a, Mathf.Min(a, 900f), value.occK));
			value.lp.cutoffFrequency = Mathf.Lerp(value.lp.cutoffFrequency, b, Mathf.Min(1f, dt * 8f));
			value.src.volume = value.gain * (flag ? 0.5f : 1f) * (1f - 0.5f * value.occK) * FBAudio.SfxVol * FBAudio.Master;
			value.src.spatialBlend = (flag ? 0.35f : 1f);
		}
		foreach (S item in dead)
		{
			if (item.bar != null && item.ownBar && !item.car)
			{
				FBGiroflex.Detach(item.bar);
			}
			all.Remove(item.car);
		}
		FBGiroflex.Tick(dt);
	}

	public static void PlayerControl(FBCar car)
	{
		Keyboard current = Keyboard.current;
		if (current == null || !IsPolice(car) || !current.bKey.wasPressedThisFrame)
		{
			return;
		}
		if (current.shiftKey.isPressed)
		{
			bool flag = !Lights(car);
			SetLights(car, flag);
			FBHUD.I?.Center(flag ? "GIROFLEX LIGADO" : "GIROFLEX DESLIGADO", 1f);
			return;
		}
		int num = (Mode(car) + 1) % MODES.Length;
		Set(car, num);
		if (num == 0)
		{
			SetLights(car, on: false);
		}
		FBHUD.I?.Center("SIRENE: " + NAMES[num], 1f);
	}

	public static string Diag(FBCar car)
	{
		if (!car || !all.TryGetValue(car, out var value))
		{
			return "sem sirene";
		}
		Transform ear = FBAudio.Ear;
		float num = (((bool)ear && (bool)value.src) ? Vector3.Distance(ear.position, value.src.transform.position) : (-1f));
		float num2 = (value.src ? (value.src.volume * ((num <= 6f) ? 1f : curve.Evaluate(Mathf.Clamp01(num / 320f)))) : 0f);
		return $"modo {value.mode} · tocando {(bool)value.src && value.src.isPlaying} · d {num:0.0} m · ganho efetivo {num2:0.000} ({((num2 > 0f) ? (20f * Mathf.Log10(num2)) : (-99f)):0.0} dB) · corte {(value.lp ? value.lp.cutoffFrequency : 0f):0} Hz · prédio {value.occK:0.0} · giroflex {value.bar != null && value.bar.on}";
	}
}
}
