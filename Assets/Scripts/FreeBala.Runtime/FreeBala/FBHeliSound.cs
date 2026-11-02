using UnityEngine;

namespace FreeBala
{
public class FBHeliSound : MonoBehaviour
{
	private AudioSource rotor;

	private AudioLowPassFilter lp;

	public float vol = 0.9f;

	public float rpm = 1f;

	private const float MIX = 0.45f;

	public static FBHeliSound Attach(GameObject g, float vol)
	{
		if (!g)
		{
			return null;
		}
		FBHeliSound obj = g.GetComponent<FBHeliSound>().OrNull() ?? g.AddComponent<FBHeliSound>();
		obj.vol = vol;
		return obj;
	}

	private void Start()
	{
		AudioClip audioClip = FBAudio.Clip("ie_heli_loop");
		if (!audioClip)
		{
			base.enabled = false;
			return;
		}
		rotor = base.gameObject.AddComponent<AudioSource>();
		rotor.clip = audioClip;
		rotor.loop = true;
		rotor.playOnAwake = false;
		rotor.spatialBlend = 1f;
		rotor.dopplerLevel = 0.5f;
		rotor.spread = 70f;
		rotor.priority = 16;
		rotor.rolloffMode = AudioRolloffMode.Custom;
		rotor.maxDistance = 1000f;
		rotor.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FBAudio.RolloffCurve(30f, 0.78f, 1000f));
		rotor.outputAudioMixerGroup = FBAudio.Group(voice: false);
		rotor.volume = 0f;
		rotor.time = Random.value * audioClip.length * 0.9f;
		rotor.Play();
		lp = base.gameObject.AddComponent<AudioLowPassFilter>();
		lp.cutoffFrequency = 22000f;
		lp.lowpassResonanceQ = 1f;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBHeliSound.Update");

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
		if ((bool)rotor)
		{
			Transform ear = FBAudio.Ear;
			float value = (ear ? Vector3.Distance(ear.position, base.transform.position) : 120f);
			float num = Mathf.Clamp01(vol * 0.45f * FBAudio.SfxVol * FBAudio.Master);
			rotor.volume = Mathf.MoveTowards(rotor.volume, num, Time.deltaTime * 1.5f);
			rotor.pitch = Mathf.Lerp(0.68f, 1f, Mathf.Clamp01(rpm));
			if ((bool)lp)
			{
				lp.cutoffFrequency = 22000f * Mathf.Pow(0.04090909f, Mathf.Clamp01(Mathf.InverseLerp(40f, 700f, value)));
			}
			if (!rotor.isPlaying && num > 0f)
			{
				rotor.Play();
			}
		}
	}

	private void OnDisable()
	{
		if ((bool)rotor)
		{
			rotor.Pause();
		}
	}

	private void OnEnable()
	{
		if ((bool)rotor)
		{
			rotor.UnPause();
		}
	}
}
}
