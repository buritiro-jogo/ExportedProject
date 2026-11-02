using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBLivingSky : MonoBehaviour
{
	private class Bird
	{
		public Transform root;

		public PlayableGraph graph;

		public float phase;

		public float rx;

		public float rz;

		public float yOff;

		public float speed;

		public float bob;
	}

	public static FBLivingSky I;

	private Material sky;

	private float time;

	private readonly List<(Transform t, Material m, float vx, float vz, float op0)> clouds = new List<(Transform, Material, float, float, float)>();

	private readonly List<Bird> birds = new List<Bird>();

	private (float x, float z, float rx, float rz, float y) flight = (x: 0f, z: 0f, rx: 70f, rz: 58f, y: 40f);

	private bool hideBirds;

	private static Texture2D cloudTex;

	private static Texture3D volTex;

	public static void Start(Bounds? map)
	{
		if ((bool)I)
		{
			UnityEngine.Object.Destroy(I.gameObject);
		}
		I = new GameObject("FB_CeuVivo").AddComponent<FBLivingSky>();
		I.Build(map);
	}

	private static Texture2D CloudTex()
	{
		if ((bool)cloudTex)
		{
			return cloudTex;
		}
		int num = 256;
		Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: true);
		Color[] array = new Color[num * num];
		System.Random random = new System.Random(7);
		List<(float, float, float, float)> list = new List<(float, float, float, float)>();
		for (int i = 0; i < 15; i++)
		{
			list.Add((48f + (float)random.NextDouble() * 160f, 70f + (float)random.NextDouble() * 110f, 22f + (float)random.NextDouble() * 38f, 16f + (float)random.NextDouble() * 26f));
		}
		for (int j = 0; j < num; j++)
		{
			for (int k = 0; k < num; k++)
			{
				float num2 = 0f;
				foreach (var item in list)
				{
					float num3 = ((float)k - item.Item1) / item.Item3;
					float num4 = ((float)j - item.Item2) / item.Item4;
					num2 = Mathf.Max(num2, 1f - Mathf.Sqrt(num3 * num3 + num4 * num4));
				}
				float num5 = Fbm((float)k / 38f, (float)j / 38f) - 0.5f;
				float num6 = Mathf.Clamp01((num2 + num5 * 0.55f) * 2.2f);
				num6 = num6 * num6 * (3f - 2f * num6);
				float num7 = Mathf.Lerp(0.66f, 1f, Mathf.Clamp01((float)(j - 60) / 120f + num5 * 0.4f));
				array[j * num + k] = new Color(num7, num7, Mathf.Min(1f, num7 * 1.03f), num6 * 0.9f);
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply(updateMipmaps: true);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		return cloudTex = texture2D;
	}

	private static float VN(float x, float y)
	{
		int num = Mathf.FloorToInt(x);
		int num2 = Mathf.FloorToInt(y);
		float num3 = x - (float)num;
		float num4 = y - (float)num2;
		num3 = num3 * num3 * (3f - 2f * num3);
		num4 = num4 * num4 * (3f - 2f * num4);
		return Mathf.Lerp(Mathf.Lerp(H(num, num2), H(num + 1, num2), num3), Mathf.Lerp(H(num, num2 + 1), H(num + 1, num2 + 1), num3), num4);
	}

	private static float H(int x, int y)
	{
		int num = x * 374761393 + y * 668265263;
		int num2 = (num ^ (int)((uint)num >> 13)) * 1274126177;
		return (float)(((uint)num2 ^ ((uint)num2 >> 16)) & 0xFFFFFFu) / 16777215f;
	}

	private static float Fbm(float x, float y)
	{
		float num = 0f;
		float num2 = 0.5f;
		for (int i = 0; i < 4; i++)
		{
			num += num2 * VN(x, y);
			x *= 2.03f;
			y *= 2.03f;
			num2 *= 0.5f;
		}
		return num / 0.9375f;
	}

	private static Texture3D VolTex()
	{
		if ((bool)volTex)
		{
			return volTex;
		}
		byte[] array = new byte[32768];
		System.Random random = new System.Random(11);
		Vector3[] array2 = new Vector3[64];
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i] = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
		}
		for (int j = 0; j < 32; j++)
		{
			for (int k = 0; k < 32; k++)
			{
				for (int l = 0; l < 32; l++)
				{
					float num = 0f;
					float num2 = 0.5f;
					for (int m = 0; m < 3; m++)
					{
						int num3 = 4 << m;
						float num4 = (float)num3 / 32f;
						num += num2 * VN3((float)l * num4, (float)k * num4, (float)j * num4, num3);
						num2 *= 0.5f;
					}
					num /= 0.875f;
					Vector3 vector = new Vector3(l, k, j) * 0.125f;
					float num5 = 9f;
					for (int n = -1; n <= 1; n++)
					{
						for (int num6 = -1; num6 <= 1; num6++)
						{
							for (int num7 = -1; num7 <= 1; num7++)
							{
								int num8 = Mathf.FloorToInt(vector.x) + num7;
								int num9 = Mathf.FloorToInt(vector.y) + num6;
								int num10 = Mathf.FloorToInt(vector.z) + n;
								Vector3 vector2 = array2[W(num8, 4) + W(num9, 4) * 4 + W(num10, 4) * 4 * 4];
								num5 = Mathf.Min(num5, (new Vector3(num8, num9, num10) + vector2 - vector).sqrMagnitude);
							}
						}
					}
					float num11 = 1f - Mathf.Clamp01(Mathf.Sqrt(num5) * 1.15f);
					array[l + k * 32 + j * 32 * 32] = (byte)(Mathf.Clamp01(num * 0.55f + num11 * 0.55f) * 255f);
				}
			}
		}
		volTex = new Texture3D(32, 32, 32, TextureFormat.R8, mipChain: false)
		{
			wrapMode = TextureWrapMode.Repeat,
			filterMode = FilterMode.Bilinear,
			name = "fb_nuvem3d"
		};
		volTex.SetPixelData(array, 0);
		volTex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return volTex;
		static float P(int x, int y, int z, int per)
		{
			int num12 = (W(x, per) * 73856093) ^ (W(y, per) * 19349663) ^ (W(z, per) * 83492791) ^ (per * 40503);
			int num13 = (num12 ^ (int)((uint)num12 >> 13)) * 1274126177;
			return (float)(((uint)num13 ^ ((uint)num13 >> 16)) & 0xFFFFu) / 65535f;
		}
		static float VN3(float x, float y, float z, int per)
		{
			int ix = Mathf.FloorToInt(x);
			int iy = Mathf.FloorToInt(y);
			int iz = Mathf.FloorToInt(z);
			float num14 = x - (float)ix;
			float num15 = y - (float)iy;
			float num16 = z - (float)iz;
			num14 = num14 * num14 * (3f - 2f * num14);
			num15 = num15 * num15 * (3f - 2f * num15);
			num16 = num16 * num16 * (3f - 2f * num16);
			return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(L(0, 0, 0), L(1, 0, 0), num14), Mathf.Lerp(L(0, 1, 0), L(1, 1, 0), num14), num15), Mathf.Lerp(Mathf.Lerp(L(0, 0, 1), L(1, 0, 1), num14), Mathf.Lerp(L(0, 1, 1), L(1, 1, 1), num14), num15), num16);
			float L(int a, int b, int c)
			{
				return P(ix + a, iy + b, iz + c, per);
			}
		}
		static int W(int a, int per)
		{
			return (a % per + per) % per;
		}
	}

	private void ApplyTier()
	{
		FBGfxFlags f = FBGfx.F;
		if ((bool)sky)
		{
			sky.DisableKeyword("_FB_SKY_LITE");
			sky.DisableKeyword("_FB_VOLCLOUDS");
			if (f.volClouds && SystemInfo.supports3DTextures)
			{
				sky.SetTexture("_FBCloudVol", VolTex());
				sky.EnableKeyword("_FB_VOLCLOUDS");
			}
			else if (f.skyLite)
			{
				sky.EnableKeyword("_FB_SKY_LITE");
			}
		}
		foreach (var cloud in clouds)
		{
			if ((bool)cloud.t)
			{
				cloud.t.gameObject.SetActive(f.cloudSprites);
			}
		}
	}

	private void OnEnable()
	{
		FBGfx.Changed += ApplyTier;
	}

	private void OnDisable()
	{
		FBGfx.Changed -= ApplyTier;
	}

	private void Build(Bounds? map)
	{
		Shader shader = FBShaders.Sky;
		if ((bool)shader)
		{
			sky = new Material(shader)
			{
				name = "ceu_freebala"
			};
			RenderSettings.skybox = sky;
			DynamicGI.UpdateEnvironment();
		}
		Material material = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit));
		material.SetTexture("_BaseMap", CloudTex());
		material.mainTexture = CloudTex();
		material.SetFloat("_Cull", 0f);
		for (int i = 0; i < 12; i++)
		{
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
			gameObject.name = "nuvem";
			gameObject.transform.SetParent(base.transform, worldPositionStays: false);
			float num = 20f + UnityEngine.Random.value * 34f;
			gameObject.transform.localScale = new Vector3(num * (1.2f + UnityEngine.Random.value), num * (0.52f + UnityEngine.Random.value * 0.3f), 1f);
			gameObject.transform.localPosition = new Vector3((UnityEngine.Random.value - 0.5f) * 310f, 38f + UnityEngine.Random.value * 25f, (UnityEngine.Random.value - 0.5f) * 310f);
			Material material2 = new Material(material);
			float num2 = 0.48f + UnityEngine.Random.value * 0.24f;
			material2.SetColor("_BaseColor", new Color(1f, 1f, 1f, num2));
			Renderer component = gameObject.GetComponent<Renderer>();
			component.sharedMaterial = material2;
			component.shadowCastingMode = ShadowCastingMode.Off;
			clouds.Add((gameObject.transform, material2, 0.18f + UnityEngine.Random.value * 0.45f, (UnityEngine.Random.value - 0.5f) * 0.08f, num2));
		}
		ApplyTier();
		if (map.HasValue)
		{
			Bounds valueOrDefault = map.GetValueOrDefault();
			if (valueOrDefault.size.x > 0f)
			{
				flight = (x: valueOrDefault.center.x, z: valueOrDefault.center.z, rx: Mathf.Max(34f, valueOrDefault.size.x * 0.34f), rz: Mathf.Max(30f, valueOrDefault.size.z * 0.34f), y: Mathf.Max(28f, valueOrDefault.center.y + valueOrDefault.size.y * 0.5f + 14f));
			}
		}
		GameObject gameObject2 = Resources.Load<GameObject>("FBImported/Birds/hummingbird");
		if (!gameObject2)
		{
			return;
		}
		Renderer[] componentsInChildren = gameObject2.GetComponentsInChildren<Renderer>(includeInactive: true);
		Bounds bounds = ((componentsInChildren.Length != 0) ? componentsInChildren[0].bounds : new Bounds(Vector3.zero, Vector3.one));
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		float num3 = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.001f);
		float num4 = 0.42f / num3 * 1.12f;
		List<AnimationClip> source = new List<AnimationClip>();
		AnimationClip animationClip = source.FirstOrDefault((AnimationClip c) => Regex.IsMatch(c.name, "02_Forward_Flight_Loop|01_Hover_Idle_Loop")) ?? source.FirstOrDefault() ?? FBRegistry.I?.birdClip;
		for (int k = 0; k < 5; k++)
		{
			Transform transform = new GameObject("beija_flor").transform;
			transform.SetParent(base.transform, worldPositionStays: false);
			GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject2, transform, worldPositionStays: false);
			transform.localScale = Vector3.one * num4;
			array = gameObject3.GetComponentsInChildren<Renderer>();
			foreach (Renderer obj in array)
			{
				obj.shadowCastingMode = ShadowCastingMode.Off;
				if (obj is SkinnedMeshRenderer skinnedMeshRenderer)
				{
					skinnedMeshRenderer.updateWhenOffscreen = true;
				}
			}
			Bird bird = new Bird
			{
				root = transform,
				phase = (float)k * ((float)Math.PI * 2f / 5f) + (UnityEngine.Random.value - 0.5f) * 0.42f,
				rx = 0.55f + UnityEngine.Random.value * 0.45f,
				rz = 0.55f + UnityEngine.Random.value * 0.45f,
				yOff = (float)k * 1.8f,
				speed = 0.035f + UnityEngine.Random.value * 0.012f,
				bob = UnityEngine.Random.value * (float)Math.PI * 2f
			};
			if ((bool)animationClip)
			{
				Animator orAdd = gameObject3.GetOrAdd<Animator>();
				bird.graph = PlayableGraph.Create("passaro");
				AnimationClipPlayable value = AnimationClipPlayable.Create(bird.graph, animationClip);
				AnimationPlayableOutput.Create(bird.graph, "a", orAdd).SetSourcePlayable(value);
				bird.graph.Play();
			}
			birds.Add(bird);
		}
	}

	private void OnDestroy()
	{
		foreach (Bird bird in birds)
		{
			if (bird.graph.IsValid())
			{
				bird.graph.Destroy();
			}
		}
	}

	public void SetWeather(float night, bool rain, float dusk, Vector3 sunDirUnity, Vector3 moonDirUnity, float cover, Vector2 wind)
	{
		if ((bool)sky)
		{
			sky.SetFloat("_Night", night);
			sky.SetFloat("_Rain", rain ? 1 : 0);
			sky.SetFloat("_Dusk", dusk);
			sky.SetFloat("_Cover", rain ? 1f : cover);
			sky.SetVector("_SunDir", W(sunDirUnity));
			sky.SetVector("_MoonDir", W(moonDirUnity));
			sky.SetVector("_WindSky", new Vector4(0f - wind.x, wind.y, 0f, 0f));
		}
		Color white = Color.white;
		Color b = new Color(1f, 0.69f, 0.54f);
		Color b2 = new Color(0.114f, 0.145f, 0.2f);
		Color color = new Color(0.486f, 0.518f, 0.545f);
		foreach (var cloud in clouds)
		{
			Color value = (rain ? color : Color.Lerp(Color.Lerp(white, b, dusk * (1f - night)), b2, night));
			value.a = (rain ? Mathf.Min(1f, cloud.op0 + 0.3f) : (cloud.op0 * (1f - 0.4f * night)));
			cloud.m.SetColor("_BaseColor", value);
		}
		hideBirds = night > 0.5f || rain;
		foreach (Bird bird in birds)
		{
			bird.root.gameObject.SetActive(!hideBirds);
		}
		static Vector3 W(Vector3 v)
		{
			return new Vector3(0f - v.x, v.y, v.z).normalized;
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBLivingSky.LateUpdate");

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
		Camera main = Camera.main;
		if (!main)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		time += deltaTime;
		if ((bool)sky)
		{
			sky.SetFloat("_SkyTime", time);
		}
		base.transform.position = main.transform.position;
		foreach (var cloud in clouds)
		{
			Transform item = cloud.t;
			float item2 = cloud.vx;
			float item3 = cloud.vz;
			Vector3 localPosition = item.localPosition;
			localPosition.x += item2 * deltaTime;
			localPosition.z += item3 * deltaTime;
			if (localPosition.x > 190f)
			{
				localPosition.x = -190f;
			}
			if (localPosition.z > 190f)
			{
				localPosition.z = -190f;
			}
			else if (localPosition.z < -190f)
			{
				localPosition.z = 190f;
			}
			item.localPosition = localPosition;
			item.rotation = Quaternion.LookRotation(item.position - main.transform.position);
		}
		foreach (Bird bird in birds)
		{
			bird.phase += bird.speed * deltaTime;
			float x = flight.x + Mathf.Cos(bird.phase) * flight.rx * bird.rx;
			float z = flight.z + Mathf.Sin(bird.phase) * flight.rz * bird.rz;
			bird.root.position = new Vector3(x, flight.y + bird.yOff + Mathf.Sin(bird.phase * 2f + bird.bob) * 0.8f, z);
			bird.root.rotation = Quaternion.LookRotation(new Vector3(0f - Mathf.Sin(bird.phase), 0f, Mathf.Cos(bird.phase)));
		}
	}
}
}
