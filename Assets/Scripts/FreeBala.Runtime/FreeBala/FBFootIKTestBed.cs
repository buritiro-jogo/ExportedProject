using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBFootIKTestBed : MonoBehaviour
{
	public class Route
	{
		public string name;

		public string slug;

		public Vector2[] pts;

		public float speed;

		public float pause = 1.2f;
	}

	private class AutoShots : MonoBehaviour
	{
		public float secs = 4f;

		private IEnumerator Start()
		{
			string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
			Directory.CreateDirectory(dir);
			if (Application.CanStreamedLevelBeLoaded("IKTest"))
			{
				AsyncOperation op = SceneManager.LoadSceneAsync("IKTest");
				while (!op.isDone)
				{
					yield return null;
				}
			}
			else
			{
				SceneManager.SetActiveScene(SceneManager.CreateScene("IKTest_auto"));
				Camera[] array = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
				for (int j = 0; j < array.Length; j++)
				{
					array[j].enabled = false;
				}
				Canvas[] array2 = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j].enabled = false;
				}
			}
			FBFootIKTestBed bed = UnityEngine.Object.FindAnyObjectByType<FBFootIKTestBed>();
			if (!bed)
			{
				bed = new GameObject("TesteFootIK").AddComponent<FBFootIKTestBed>();
			}
			yield return null;
			yield return null;
			StringBuilder log = new StringBuilder();
			Debug.Log($"[FreeBala][iktest] {bed.actors.Count} caminhantes · nível {FBQuality.Level}");
			for (float w3 = 0f; w3 < 3f; w3 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			bed.sideView = true;
			for (int i = 0; i < bed.actors.Count && i < Routes.Length; i++)
			{
				FBCharacter a = bed.actors[i];
				if ((bool)a)
				{
					bed.Focus = i;
					Route r = Routes[i];
					bed.meters[a].Reset();
					FBQuality.FootIK = true;
					for (float w3 = 0f; w3 < secs * 0.5f; w3 += Time.unscaledDeltaTime)
					{
						yield return null;
					}
					yield return new WaitForEndOfFrame();
					ScreenCapture.CaptureScreenshot(Path.Combine(dir, "ik_" + r.slug + "_on_1.png"));
					for (float w3 = 0f; w3 < secs * 0.5f; w3 += Time.unscaledDeltaTime)
					{
						yield return null;
					}
					yield return new WaitForEndOfFrame();
					ScreenCapture.CaptureScreenshot(Path.Combine(dir, "ik_" + r.slug + "_on_2.png"));
					string rep = r.slug + " (" + r.name + "): " + bed.meters[a].Report() + " · " + (a.feet ? a.feet.DebugText() : "-");
					FBQuality.FootIK = false;
					for (int f = 0; f < 20; f++)
					{
						yield return null;
					}
					yield return new WaitForEndOfFrame();
					ScreenCapture.CaptureScreenshot(Path.Combine(dir, "ik_" + r.slug + "_off.png"));
					FBQuality.FootIK = true;
					Debug.Log("[FreeBala][iktest] " + rep);
					log.AppendLine(rep);
				}
			}
			File.WriteAllText(Path.Combine(dir, "ik_relatorio.txt"), log.ToString());
			yield return null;
			yield return null;
			if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbauto") < 0)
			{
				Application.Quit(0);
			}
		}
	}

	public string skin;

	public bool spawnOnStart = true;

	[NonSerialized]
	public readonly List<FBCharacter> actors = new List<FBCharacter>();

	[NonSerialized]
	public readonly Dictionary<FBCharacter, FBFootIKMeter> meters = new Dictionary<FBCharacter, FBFootIKMeter>();

	private int focus;

	private Camera cam;

	private float camYaw;

	[NonSerialized]
	public bool sideView;

	public static readonly Route[] Routes = new Route[10]
	{
		new Route
		{
			slug = "meiofio",
			name = "meio-fio (um pé em cada altura)",
			pts = new Vector2[2]
			{
				new Vector2(0.06f, 3f),
				new Vector2(0.06f, 36f)
			},
			speed = 1.6f
		},
		new Route
		{
			slug = "cruza_meiofio",
			name = "cruza meio-fio",
			pts = new Vector2[5]
			{
				new Vector2(-2f, 5f),
				new Vector2(2f, 9f),
				new Vector2(-2f, 13f),
				new Vector2(2f, 17f),
				new Vector2(-2f, 21f)
			},
			speed = 3f
		},
		new Route
		{
			slug = "escada",
			name = "escada andando",
			pts = new Vector2[2]
			{
				new Vector2(6f, 1f),
				new Vector2(6f, 19.5f)
			},
			speed = 1.6f,
			pause = 2f
		},
		new Route
		{
			slug = "escada_correndo",
			name = "escada correndo (jogador)",
			pts = new Vector2[2]
			{
				new Vector2(7.2f, 1f),
				new Vector2(7.2f, 19.5f)
			},
			speed = 5f
		},
		new Route
		{
			slug = "rampas",
			name = "rampas 12°/22°",
			pts = new Vector2[2]
			{
				new Vector2(12f, 1f),
				new Vector2(12f, 22f)
			},
			speed = 2.2f
		},
		new Route
		{
			slug = "morro",
			name = "morro",
			pts = new Vector2[2]
			{
				new Vector2(18f, 4f),
				new Vector2(18f, 36f)
			},
			speed = 2.5f
		},
		new Route
		{
			slug = "lado15",
			name = "terreno de lado 15°",
			pts = new Vector2[2]
			{
				new Vector2(26f, 6.5f),
				new Vector2(26f, 18.5f)
			},
			speed = 1.6f,
			pause = 2f
		},
		new Route
		{
			slug = "lado25",
			name = "terreno de lado 25°",
			pts = new Vector2[2]
			{
				new Vector2(26f, 22.5f),
				new Vector2(26f, 33f)
			},
			speed = 1.4f,
			pause = 2f
		},
		new Route
		{
			slug = "pedras",
			name = "pedras",
			pts = new Vector2[2]
			{
				new Vector2(33f, 3f),
				new Vector2(33f, 36f)
			},
			speed = 1.6f
		},
		new Route
		{
			slug = "obstaculos",
			name = "obstáculos 8–40 cm (jogador)",
			pts = new Vector2[2]
			{
				new Vector2(40f, 2f),
				new Vector2(40f, 36f)
			},
			speed = 5f
		}
	};

	public int Focus
	{
		get
		{
			return focus;
		}
		set
		{
			focus = Mathf.Clamp(value, 0, Mathf.Max(0, actors.Count - 1));
		}
	}

	public static GameObject BuildCourse()
	{
		GameObject gameObject = GameObject.Find("PistaIK");
		if ((bool)gameObject)
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(gameObject);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}
		GameObject root = new GameObject("PistaIK");
		root.layer = 8;
		Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
		Dictionary<string, Material> mats = new Dictionary<string, Material>();
		Color c2 = new Color(0.22f, 0.22f, 0.24f);
		Color c3 = new Color(0.62f, 0.6f, 0.56f);
		Color c4 = new Color(0.55f, 0.53f, 0.5f);
		Color c5 = new Color(0.45f, 0.36f, 0.25f);
		Color c6 = new Color(0.4f, 0.39f, 0.37f);
		Color c7 = new Color(0.5f, 0.36f, 0.22f);
		Box("chao", new Vector3(20f, -0.25f, 20f), new Vector3(110f, 0.5f, 90f), Vector3.zero, c2);
		Transform p2 = Lane("MeioFio").transform;
		Box("calcada", new Vector3(-1.5f, 0.075f, 20f), new Vector3(3f, 0.15f, 40f), Vector3.zero, c3, p2);
		Transform p3 = Lane("Escada").transform;
		int num = 10;
		float num2 = 4f;
		float x = 6.6f;
		for (int i = 0; i < num; i++)
		{
			float num3 = 0.18f * (float)(i + 1);
			Box("sobe" + i, new Vector3(x, num3 / 2f, num2 + 0.3f * ((float)i + 0.5f)), new Vector3(3f, num3, 0.3f), Vector3.zero, c4, p3);
		}
		float num4 = num2 + 0.3f * (float)num;
		float num5 = 0.18f * (float)num;
		Box("patamar", new Vector3(x, num5 / 2f, num4 + 1.5f), new Vector3(3f, num5, 3f), Vector3.zero, c4, p3);
		for (int j = 0; j < num; j++)
		{
			float num6 = num5 - 0.18f * (float)(j + 1);
			if (!(num6 <= 0f))
			{
				Box("desce" + j, new Vector3(x, num6 / 2f, num4 + 3f + 0.3f * ((float)j + 0.5f)), new Vector3(3f, num6, 0.3f), Vector3.zero, c4, p3);
			}
		}
		Transform p4 = Lane("Rampas").transform;
		float x2 = 12f;
		float num7 = 12f;
		float num8 = 8f;
		float num9 = 22f;
		float num10 = 0.4f;
		float num11 = num8 * Mathf.Sin(num7 * ((float)Math.PI / 180f));
		float num12 = num8 * Mathf.Cos(num7 * ((float)Math.PI / 180f));
		float num13 = 3f;
		GameObject gameObject2 = Box("rampa12", Vector3.zero, new Vector3(3f, num10, num8), new Vector3(0f - num7, 0f, 0f), c4, p4);
		gameObject2.transform.position = new Vector3(x2, num11 / 2f, num13 + num12 / 2f) - gameObject2.transform.up * num10 / 2f;
		Box("topo", new Vector3(x2, num11 / 2f, num13 + num12 + 1.5f), new Vector3(3f, num11, 3f), Vector3.zero, c4, p4);
		float num14 = num11 / Mathf.Sin(num9 * ((float)Math.PI / 180f));
		float num15 = num14 * Mathf.Cos(num9 * ((float)Math.PI / 180f));
		float num16 = num13 + num12 + 3f;
		GameObject gameObject3 = Box("rampa22", Vector3.zero, new Vector3(3f, num10, num14), new Vector3(num9, 0f, 0f), c4, p4);
		gameObject3.transform.position = new Vector3(x2, num11 / 2f, num16 + num15 / 2f) - gameObject3.transform.up * num10 / 2f;
		Transform parent2 = Lane("Morro").transform;
		GameObject gameObject4 = Prim(PrimitiveType.Sphere, "morro", new Vector3(18f, -1.2f, 20f), new Vector3(9f, 7f, 30f), Vector3.zero, c5, parent2);
		SphereCollider component = gameObject4.GetComponent<SphereCollider>();
		if ((bool)component)
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(component);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(component);
			}
		}
		gameObject4.AddComponent<MeshCollider>().sharedMesh = gameObject4.GetComponent<MeshFilter>().sharedMesh;
		Transform p5 = Lane("Inclinado").transform;
		Box("lado15", new Vector3(26f, 0.55f, 12.5f), new Vector3(6f, 0.6f, 15f), new Vector3(0f, 0f, 15f), c5, p5);
		Box("lado25", new Vector3(26f, 1.1f, 27.5f), new Vector3(6f, 0.6f, 13f), new Vector3(0f, 0f, 25f), c5, p5);
		Transform transform = Lane("Pedras").transform;
		System.Random rnd = new System.Random(7);
		for (int l = 0; l < 40; l++)
		{
			float num17 = R(0.18f, 0.55f);
			Vector3 pos2 = new Vector3(33f + R(-1.1f, 1.1f), R(-0.12f, 0.02f) * num17 / 0.3f, R(4f, 35f));
			if (l % 3 == 0)
			{
				Prim(PrimitiveType.Sphere, "pedra" + l, pos2, Vector3.one * num17, Vector3.zero, c6, transform);
			}
			else
			{
				Box("pedra" + l, pos2, new Vector3(num17, num17 * R(0.35f, 0.7f), num17 * R(0.6f, 1.2f)), new Vector3(R(-25f, 25f), R(0f, 360f), R(-25f, 25f)), c6, transform);
			}
		}
		Transform p6 = Lane("Obstaculos").transform;
		float[] array = new float[5] { 0.08f, 0.15f, 0.25f, 0.32f, 0.4f };
		for (int m = 0; m < array.Length; m++)
		{
			Box("obst" + (int)(array[m] * 100f) + "cm", new Vector3(40f, array[m] / 2f, 6 + m * 6), new Vector3(2.2f, array[m], (m % 2 == 0) ? 0.5f : 1.6f), Vector3.zero, c7, p6);
		}
		Box("lado60cm", new Vector3(42.4f, 0.3f, 12f), new Vector3(1.2f, 0.6f, 1.2f), Vector3.zero, c7, p6);
		Box("lado90cm", new Vector3(42.4f, 0.45f, 24f), new Vector3(1.2f, 0.9f, 1.2f), Vector3.zero, c7, p6);
		if (!UnityEngine.Object.FindAnyObjectByType<Light>())
		{
			GameObject obj = new GameObject("Sol");
			Light light = obj.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.2f;
			obj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
		}
		return root;
		GameObject Box(string n, Vector3 pos, Vector3 size, Vector3 euler, Color c, Transform p = null)
		{
			return Prim(PrimitiveType.Cube, n, pos, size, euler, c, p);
		}
		GameObject Lane(string n)
		{
			GameObject obj2 = new GameObject(n);
			obj2.layer = 8;
			obj2.transform.SetParent(root.transform, worldPositionStays: false);
			return obj2;
		}
		Material M(string k, Color c)
		{
			if (!mats.TryGetValue(k, out var value))
			{
				value = (shader ? new Material(shader)
				{
					name = "PistaIK_" + k,
					color = c
				} : null);
				mats[k] = value;
			}
			return value;
		}
		GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 size, Vector3 euler, Color c, Transform parent = null)
		{
			GameObject obj3 = GameObject.CreatePrimitive(t);
			obj3.name = name;
			obj3.layer = 8;
			obj3.transform.SetParent(parent ? parent : root.transform, worldPositionStays: false);
			obj3.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
			obj3.transform.localScale = size;
			obj3.isStatic = true;
			Renderer component2 = obj3.GetComponent<Renderer>();
			Material material = M(c.ToString(), c);
			if ((bool)component2 && (bool)material)
			{
				component2.sharedMaterial = material;
			}
			return obj3;
		}
		float R(float a, float b)
		{
			return a + (float)rnd.NextDouble() * (b - a);
		}
	}

	public static FBCharacter SpawnWalker(string skin, Route r)
	{
		FBRegistry i = FBRegistry.I;
		if (!i)
		{
			return null;
		}
		if (string.IsNullOrEmpty(skin) || !i.HasChar(skin))
		{
			skin = (i.HasChar("r_tutu") ? "r_tutu" : ((i.charIds.Count > 0) ? i.charIds[0] : null));
		}
		GameObject gameObject = ((skin != null) ? i.Char(skin) : null);
		if (!gameObject)
		{
			return null;
		}
		Vector3[] array = new Vector3[r.pts.Length];
		for (int j = 0; j < array.Length; j++)
		{
			float x = r.pts[j].x;
			float y = r.pts[j].y;
			array[j] = new Vector3(x, FBWorld.GroundY(x, 6f, y, 2f, 12f).GetValueOrDefault(), y);
		}
		Vector3 vector = (array[1] - array[0]).Flat();
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, array[0] + Vector3.up * 0.1f, Quaternion.LookRotation((vector.sqrMagnitude > 0.0001f) ? vector : Vector3.forward));
		gameObject2.name = "IK_" + r.name;
		Transform[] componentsInChildren = gameObject2.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (!transform.GetComponent<FBHitbox>())
			{
				transform.gameObject.layer = 10;
			}
		}
		FBCharacter orAdd = gameObject2.GetOrAdd<FBCharacter>();
		orAdd.skinId = skin;
		orAdd.displayName = r.name;
		CharacterController characterController = gameObject2.AddComponent<CharacterController>();
		float num = 1f / gameObject2.transform.lossyScale.y;
		float num2 = Json.F(GameData.Section("controller"), "m_Height", 1.8f);
		float a = Json.F(GameData.Section("controller"), "m_Radius", 0.3f);
		characterController.height = num2 * num;
		characterController.radius = Mathf.Max(a, 0.32f) * num;
		characterController.center = new Vector3(0f, num2 * 0.5f * num, 0f);
		characterController.stepOffset = 0.45f * num;
		characterController.slopeLimit = 50f;
		characterController.skinWidth = 0.04f * num;
		characterController.minMoveDistance = 0f;
		Animator component = gameObject2.GetComponent<Animator>();
		if ((bool)component)
		{
			component.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		}
		FBIKWalker fBIKWalker = gameObject2.AddComponent<FBIKWalker>();
		fBIKWalker.route = array;
		fBIKWalker.speed = r.speed;
		fBIKWalker.pause = r.pause;
		return orAdd;
	}

	public void SpawnAll()
	{
		foreach (FBCharacter actor in actors)
		{
			if ((bool)actor)
			{
				UnityEngine.Object.Destroy(actor.gameObject);
			}
		}
		actors.Clear();
		meters.Clear();
		Physics.SyncTransforms();
		Route[] routes = Routes;
		foreach (Route r in routes)
		{
			FBCharacter fBCharacter = SpawnWalker(skin, r);
			if ((bool)fBCharacter)
			{
				actors.Add(fBCharacter);
				meters[fBCharacter] = new FBFootIKMeter();
			}
		}
		if (actors.Count == 0)
		{
			Debug.LogWarning("[FreeBala][ik] sem personagens importados (FBRegistry) — rode Importar tudo no projeto principal");
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void AutoBoot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbiktest");
		if (num >= 0)
		{
			float result = 4f;
			if (num + 1 < commandLineArgs.Length)
			{
				float.TryParse(commandLineArgs[num + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out result);
			}
			GameObject obj = new GameObject("FB_IKAutoTeste");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<AutoShots>().secs = Mathf.Max(2f, result);
		}
	}

	private void Start()
	{
		GameData.Load();
		if (!GameObject.Find("PistaIK"))
		{
			BuildCourse();
		}
		cam = Camera.main;
		if (!cam)
		{
			cam = new GameObject("Main Camera").AddComponent<Camera>();
			cam.tag = "MainCamera";
		}
		if (spawnOnStart)
		{
			SpawnAll();
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBFootIKTestBed.Update");

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
		Keyboard current = Keyboard.current;
		if (current != null)
		{
			if (current.tabKey.wasPressedThisFrame && actors.Count > 0)
			{
				focus = (focus + 1) % actors.Count;
			}
			if (current.fKey.wasPressedThisFrame)
			{
				FBQuality.FootIK = !FBQuality.FootIK;
			}
			if (current.lKey.wasPressedThisFrame)
			{
				FBFootIK.Q.Lock = !FBFootIK.Q.Lock;
			}
			if (current.tKey.wasPressedThisFrame)
			{
				FBFootIK.Q.Tilt = !FBFootIK.Q.Tilt;
			}
		}
		foreach (KeyValuePair<FBCharacter, FBFootIKMeter> meter in meters)
		{
			meter.Value.Sample(meter.Key, Time.deltaTime);
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBFootIKTestBed.LateUpdate");

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
		if (!cam || actors.Count == 0)
		{
			return;
		}
		FBCharacter fBCharacter = actors[Mathf.Clamp(focus, 0, actors.Count - 1)];
		if ((bool)fBCharacter)
		{
			if (sideView)
			{
				Vector3 vector = fBCharacter.Pos + Vector3.up * 0.6f;
				cam.transform.position = vector + Quaternion.Euler(4f, fBCharacter.Yaw - 90f, 0f) * new Vector3(0f, 0f, -3f);
				cam.transform.LookAt(vector);
			}
			else
			{
				camYaw = Mathf.LerpAngle(camYaw, fBCharacter.Yaw + 60f, 1f - Mathf.Exp((0f - Time.deltaTime) * 2f));
				Vector3 vector2 = fBCharacter.Pos + Vector3.up * 0.7f;
				Vector3 vector3 = Quaternion.Euler(12f, camYaw, 0f) * new Vector3(0f, 0f, -3.4f);
				cam.transform.position = Vector3.Lerp(cam.transform.position, vector2 + vector3, 1f - Mathf.Exp((0f - Time.deltaTime) * 6f));
				cam.transform.LookAt(vector2);
			}
		}
	}

	private void OnGUI()
	{
		if (actors.Count == 0)
		{
			GUI.Label(new Rect(10f, 10f, 800f, 24f), "Sem personagens: rode FreeBala/Importar tudo no projeto principal.");
			return;
		}
		FBCharacter fBCharacter = actors[Mathf.Clamp(focus, 0, actors.Count - 1)];
		if ((bool)fBCharacter)
		{
			FBFootIKMeter value;
			string text = (meters.TryGetValue(fBCharacter, out value) ? value.Report() : "");
			GUI.Box(new Rect(8f, 8f, 760f, 92f), "");
			GUI.Label(new Rect(14f, 12f, 750f, 22f), string.Format("[{0}/{1}] {2} · IK {3} (F) · trava {4} (L) · tronco {5} (T) · Tab = próximo", focus + 1, actors.Count, fBCharacter.displayName, FBQuality.FootIK ? "ON" : "OFF", FBFootIK.Q.Lock ? "ON" : "OFF", FBFootIK.Q.Tilt ? "ON" : "OFF"));
			GUI.Label(new Rect(14f, 34f, 750f, 22f), fBCharacter.feet ? fBCharacter.feet.DebugText() : "sem FBFootIK");
			GUI.Label(new Rect(14f, 56f, 750f, 40f), text);
		}
	}
}
}
