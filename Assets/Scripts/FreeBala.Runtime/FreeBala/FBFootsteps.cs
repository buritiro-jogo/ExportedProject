using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBFootsteps : MonoBehaviour
{
	public struct Trace
	{
		public FBCharacter ch;

		public FBStepSurf surf;

		public string clip;

		public string mode;

		public float vol;

		public float speed;

		public float dist;

		public int foot;
	}

	private class St
	{
		public Transform lf;

		public Transform rf;

		public bool hasBones;

		public FBFootContact c = new FBFootContact();

		public Vector3 prev;

		public float speed;

		public float airT;

		public bool wasGround = true;

		public bool init;

		public readonly int[] last = new int[16];
	}

	public static float Volume = 1f;

	public const float MaxDist = 40f;

	public static Action<Trace> trace;

	private readonly Dictionary<FBCharacter, St> st = new Dictionary<FBCharacter, St>();

	private readonly List<AudioSource> pool = new List<AudioSource>();

	private AudioSource own;

	private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

	private static readonly List<FBGlass> broken = new List<FBGlass>();

	private float glassScan;

	private static readonly Dictionary<(Collider, int), FBStepSurf> cache = new Dictionary<(Collider, int), FBStepSurf>();

	public static FBFootsteps I { get; private set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (!I)
		{
			GameObject obj = new GameObject("FB_Passos");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			I = obj.AddComponent<FBFootsteps>();
		}
	}

	private void Awake()
	{
		int num = (Application.isMobilePlatform ? 8 : 12);
		for (int i = 0; i < num; i++)
		{
			AudioSource audioSource = new GameObject("Passo" + i).AddComponent<AudioSource>();
			audioSource.transform.SetParent(base.transform);
			audioSource.playOnAwake = false;
			audioSource.spatialBlend = 1f;
			audioSource.rolloffMode = AudioRolloffMode.Custom;
			audioSource.maxDistance = 400f;
			audioSource.dopplerLevel = 0f;
			audioSource.priority = 160;
			audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FBAudio.Rolloff(3f, 1.3f));
			audioSource.outputAudioMixerGroup = FBAudio.Group(voice: false);
			pool.Add(audioSource);
		}
		own = base.gameObject.AddComponent<AudioSource>();
		own.playOnAwake = false;
		own.spatialBlend = 0f;
		own.priority = 100;
		own.outputAudioMixerGroup = FBAudio.Group(voice: false);
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBFootsteps.LateUpdate");

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
		if (!FBMatch.I)
		{
			if (this.st.Count > 0)
			{
				this.st.Clear();
			}
			return;
		}
		float deltaTime = Time.deltaTime;
		if (deltaTime <= 0f)
		{
			return;
		}
		Vector3 a = Listener();
		if ((glassScan -= deltaTime) <= 0f)
		{
			glassScan = 2f;
			broken.Clear();
			FBGlass[] array = UnityEngine.Object.FindObjectsByType<FBGlass>(FindObjectsSortMode.None);
			foreach (FBGlass fBGlass in array)
			{
				if (fBGlass.broken)
				{
					broken.Add(fBGlass);
				}
			}
		}
		foreach (FBCharacter item in FBMatch.All)
		{
			if (!item)
			{
				continue;
			}
			if (!this.st.TryGetValue(item, out var value))
			{
				value = (this.st[item] = Make(item));
			}
			Vector3 position = item.transform.position;
			if (!value.init)
			{
				value.prev = position;
				value.init = true;
				continue;
			}
			Vector3 vector = position - value.prev;
			vector.y = 0f;
			value.prev = position;
			float num = vector.magnitude / deltaTime;
			if (num > 25f)
			{
				num = 0f;
			}
			value.speed = Mathf.Lerp(value.speed, num, 1f - Mathf.Exp((0f - deltaTime) / 0.12f));
			bool num2 = item.alive && item.sys == null && !InVehicle(item);
			float num3 = Vector3.Distance(a, position);
			if (!num2 || num3 > 40f)
			{
				value.c.Reset();
				value.airT = 0f;
				value.wasGround = true;
				continue;
			}
			bool grounded = item.grounded;
			if (!grounded)
			{
				value.airT += deltaTime;
			}
			else if (!value.wasGround && value.airT > 0.35f)
			{
				Land(item, value, num3, value.airT);
			}
			if (grounded)
			{
				value.airT = 0f;
			}
			value.wasGround = grounded;
			float num4 = 1f / Mathf.Max(0.01f, item.ScaleK);
			float hl = ((value.hasBones && (bool)value.lf) ? ((value.lf.position.y - position.y) * num4) : 0f);
			float hr = ((value.hasBones && (bool)value.rf) ? ((value.rf.position.y - position.y) * num4) : 0f);
			bool run = item.sprint || value.speed > 3.6f;
			int num5 = value.c.Update(deltaTime, value.hasBones && (bool)value.lf && (bool)value.rf, hl, hr, value.speed, grounded, run);
			if (num5 >= 0)
			{
				Step(item, value, num5, num3, run, value.c.Fallback ? "passada" : "osso");
			}
		}
		if (Time.frameCount % 300 != 0)
		{
			return;
		}
		List<FBCharacter> list = new List<FBCharacter>();
		foreach (KeyValuePair<FBCharacter, St> item2 in this.st)
		{
			if (!item2.Key)
			{
				list.Add(item2.Key);
			}
		}
		foreach (FBCharacter item3 in list)
		{
			this.st.Remove(item3);
		}
	}

	private static St Make(FBCharacter ch)
	{
		St st = new St();
		Animator animator = (ch.an ? ch.an : ch.GetComponent<Animator>());
		if ((bool)animator && animator.isHuman)
		{
			st.lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
			st.rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
			st.hasBones = (bool)st.lf && (bool)st.rf;
		}
		for (int i = 0; i < st.last.Length; i++)
		{
			st.last[i] = -1;
		}
		return st;
	}

	private static bool InVehicle(FBCharacter ch)
	{
		if (ch.vehiclePose || (bool)FBVehicles.CarOf(ch))
		{
			return true;
		}
		Transform parent = ch.transform.parent;
		if ((bool)parent)
		{
			return parent.GetComponentInParent<FBCar>();
		}
		return false;
	}

	private static Vector3 Listener()
	{
		AudioListener audioListener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
		if (!audioListener)
		{
			if (!Camera.main)
			{
				return Vector3.zero;
			}
			return Camera.main.transform.position;
		}
		return audioListener.transform.position;
	}

	public static FBStepSurf SurfaceAt(Vector3 p)
	{
		float? num = FBWorld.WaterAt(p.x, p.z);
		if (num.HasValue)
		{
			float valueOrDefault = num.GetValueOrDefault();
			if (p.y < valueOrDefault + 0.15f)
			{
				return FBStepSurf.Agua;
			}
		}
		foreach (FBGlass item in broken)
		{
			if ((bool)item)
			{
				Vector3 position = item.transform.position;
				if (new Vector2(position.x - p.x, position.z - p.z).sqrMagnitude < 4.84f && p.y < position.y + 0.3f && p.y > position.y - 4f)
				{
					return FBStepSurf.Vidro;
				}
			}
		}
		if (!Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out var hitInfo, 1.2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
		{
			return FBStepSurf.Concreto;
		}
		return Classify(hitInfo);
	}

	public static FBStepSurf Classify(RaycastHit h)
	{
		Collider collider = h.collider;
		if (!collider)
		{
			return FBStepSurf.Concreto;
		}
		MeshCollider meshCollider = collider as MeshCollider;
		Mesh mesh = (meshCollider ? meshCollider.sharedMesh : null);
		int num = 0;
		if ((bool)mesh && mesh.subMeshCount > 1 && h.triangleIndex >= 0)
		{
			int num2 = h.triangleIndex * 3;
			for (int i = 0; i < mesh.subMeshCount; i++)
			{
				SubMeshDescriptor subMesh = mesh.GetSubMesh(i);
				if (num2 >= subMesh.indexStart && num2 < subMesh.indexStart + subMesh.indexCount)
				{
					num = i;
					break;
				}
			}
		}
		if (cache.TryGetValue((collider, num), out var value))
		{
			return value;
		}
		FBSurface component = collider.GetComponent<FBSurface>();
		Renderer component2 = collider.GetComponent<Renderer>();
		Material[] array = (component2 ? component2.sharedMaterials : null);
		string text = ((array != null && num < array.Length && (bool)array[num]) ? array[num].name : "");
		value = FBStepSurfaces.FromName(collider.name + " " + text, component ? component.kind : 0);
		cache[(collider, num)] = value;
		return value;
	}

	private int Count(FBStepSurf s)
	{
		string text = FBStepSurfaces.ClipBase(s);
		if (counts.TryGetValue(text, out var value))
		{
			return value;
		}
		for (value = 0; value < 16; value++)
		{
			if (!FBAudio.Has(text + value))
			{
				break;
			}
		}
		counts[text] = value;
		return value;
	}

	private AudioClip Pick(St s, FBStepSurf surf, out string name)
	{
		FBStepSurf fBStepSurf = surf;
		int num = Count(fBStepSurf);
		if (num == 0)
		{
			fBStepSurf = FBStepSurf.Concreto;
			num = Count(fBStepSurf);
		}
		name = null;
		if (num == 0)
		{
			return null;
		}
		int num2 = (int)fBStepSurf;
		int num3 = UnityEngine.Random.Range(0, num);
		if (num > 1 && num3 == s.last[num2])
		{
			num3 = (num3 + 1 + UnityEngine.Random.Range(0, num - 1)) % num;
		}
		s.last[num2] = num3;
		name = FBStepSurfaces.ClipBase(fBStepSurf) + num3;
		return FBAudio.Clip(name);
	}

	private void Step(FBCharacter ch, St s, int foot, float dist, bool run, string mode, float boost = 1f)
	{
		Transform transform = ((foot == 0) ? s.lf : s.rf);
		Vector3 vector = (((bool)transform && s.hasBones) ? transform.position : ch.transform.position);
		FBStepSurf fBStepSurf = SurfaceAt(new Vector3(vector.x, ch.transform.position.y, vector.z));
		string clip;
		AudioClip audioClip = Pick(s, fBStepSurf, out clip);
		if (!audioClip)
		{
			return;
		}
		bool flag = ch.isPlayer && !ch.isRemote;
		bool flag2 = ch.crouch || ch.prone;
		float num = ((!flag) ? (flag2 ? 0.12f : (run ? 0.36f : 0.25f)) : (flag2 ? 0.07f : (run ? 0.2f : 0.13f))) * (1.6f * FBStepSurfaces.Gain(fBStepSurf) * (ch.civ ? 0.8f : 1f) * Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01((s.speed - 1f) / 4f)) * boost);
		float pitch = UnityEngine.Random.Range(0.92f, 1.07f) * (run ? 1.05f : 1f) * (flag2 ? 0.96f : 1f);
		float num2 = Mathf.Clamp01(num * Volume * FBAudio.SfxVol * FBAudio.Master);
		AudioSource audioSource;
		if (flag)
		{
			audioSource = own;
		}
		else
		{
			audioSource = null;
			float num3 = -1f;
			AudioSource audioSource2 = null;
			Vector3 b = Listener();
			foreach (AudioSource item in pool)
			{
				if (!item.isPlaying)
				{
					audioSource = item;
					break;
				}
				float num4 = Vector3.Distance(item.transform.position, b);
				if (num4 > num3)
				{
					num3 = num4;
					audioSource2 = item;
				}
			}
			if (!audioSource)
			{
				if (num3 <= dist)
				{
					return;
				}
				audioSource = audioSource2;
				audioSource.Stop();
			}
			audioSource.transform.position = vector + Vector3.up * 0.05f;
		}
		audioSource.pitch = pitch;
		if (flag)
		{
			audioSource.volume = 1f;
			audioSource.PlayOneShot(audioClip, num2);
		}
		else
		{
			audioSource.clip = audioClip;
			audioSource.volume = num2;
			audioSource.Play();
		}
		if (run && !flag2 && UnityEngine.Random.value < 0.45f)
		{
			AudioClip audioClip2 = FBAudio.Clip("Steps/roupa" + UnityEngine.Random.Range(0, 5));
			if ((bool)audioClip2)
			{
				audioSource.PlayOneShot(audioClip2, flag ? (num2 * 0.55f) : 0.8f);
			}
		}
		trace?.Invoke(new Trace
		{
			ch = ch,
			surf = fBStepSurf,
			clip = clip,
			mode = mode,
			vol = num2,
			speed = s.speed,
			dist = (flag ? 0f : dist),
			foot = foot
		});
	}

	public static void LandNow(FBCharacter ch, float air)
	{
		Boot();
		if ((bool)ch && (bool)I && I.st.TryGetValue(ch, out var value))
		{
			float dist = (Camera.main ? Vector3.Distance(Camera.main.transform.position, ch.transform.position) : 0f);
			I.Land(ch, value, dist, Mathf.Max(0.36f, air));
			value.wasGround = true;
			value.airT = 0f;
		}
	}

	private void Land(FBCharacter ch, St s, float dist, float air)
	{
		float num = Mathf.Lerp(1.3f, 2.2f, Mathf.Clamp01((air - 0.35f) / 1.2f));
		Step(ch, s, 0, dist, run: false, "pouso", num);
		Step(ch, s, 1, dist, run: false, "pouso", num * 0.8f);
		if (!ch.isPlayer || ch.isRemote)
		{
			FBAudio.PlayAt("ie_land", ch.transform.position, Mathf.Min(0.7f, 0.25f + air * 0.3f));
		}
	}
}
}
