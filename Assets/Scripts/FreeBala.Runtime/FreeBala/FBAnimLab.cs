using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBAnimLab : MonoBehaviour
{
	public class St
	{
		public string id;

		public float speed;

		public Vector2 dir;

		public bool combat = true;

		public bool hold;

		public bool aim;

		public bool fire;

		public bool crouch;

		public bool crouchFast;

		public bool sprint;

		public bool relaxed;

		public bool reload;

		public bool swap;

		public bool heal;

		public bool healCrouch;

		public bool revive;

		public bool knocked;

		public bool treated;

		public float shotAt = 0.92f;

		public float len = 2.6f;

		public string views = "fe";
	}

	[DefaultExecutionOrder(-200)]
	public class Actor : MonoBehaviour
	{
		public St st;

		public float t;

		public float fireT;

		public FBCharacter ch;

		public FBWeaponUser wu;

		public bool swapped;

		public bool reloaded;

		private void Awake()
		{
			ch = GetComponent<FBCharacter>();
			wu = GetComponent<FBWeaponUser>();
		}

		private static readonly int __fbt_Update = FBTempo.Id("FBAnimLab.Actor.Update");

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
			if (st == null || !ch)
			{
				return;
			}
			float deltaTime = Time.deltaTime;
			t += deltaTime;
			WeaponDef weaponDef = (wu ? wu.Cur : null);
			ch.combat = st.combat && weaponDef != null && !st.knocked;
			ch.relaxed = st.relaxed;
			ch.crouch = st.crouch || st.knocked;
			ch.CrouchFast = st.crouchFast;
			ch.prone = st.knocked;
			ch.aiming = st.aim && weaponDef != null;
			ch.combatHold = (st.hold ? 1 : 0);
			ch.sprint = st.sprint;
			if (st.fire && weaponDef != null)
			{
				fireT -= deltaTime;
				if (fireT <= 0f)
				{
					fireT = (weaponDef.auto ? weaponDef.Interval : Mathf.Max(0.25f, weaponDef.Interval));
					ch.lastShot = 0f;
					try
					{
						FBFx.Muzzle(ch.MuzzlePos, 0.5f, weaponDef.cls);
					}
					catch
					{
					}
				}
			}
			ch.shooting = st.fire && ch.lastShot < 0.15f;
			if (st.reload && !reloaded && (bool)wu && weaponDef != null)
			{
				reloaded = true;
				wu.ammo[wu.cur] = 0;
				wu.reserve[wu.cur] = weaponDef.mag * 4;
				wu.Reload();
			}
			if (st.swap && !swapped && (bool)wu)
			{
				swapped = true;
				int num = ((wu.cur != 2) ? 2 : 0);
				if (wu.slots[num] != null)
				{
					wu.SwitchTo(num);
				}
			}
			if (st.heal)
			{
				ch.sys = (st.healCrouch ? (Has("FF_SYS_KO_Revive") ?? Has("FF_SYS_Heal_Loop")) : ((t < 0.35f) ? Has("FF_SYS_Heal_Start") : Has("FF_SYS_Heal_Loop")));
			}
			if (st.revive)
			{
				ch.sys = ch.RescuePose(on: true, deltaTime);
			}
			if (st.treated)
			{
				ch.revivedBy = this;
				ch.reviveTick = Time.time;
			}
			if (st.knocked)
			{
				ch.knocked = true;
				ch.koAt = Time.time - 2f;
				ch.sys = FBCombat.KoPose(ch, st.speed > 0.1f);
			}
			if (st.heal || st.revive || st.knocked)
			{
				ch.SetWeaponVisible(v: false);
			}
			ch.StowCurrent((st.heal || st.revive) && weaponDef != null);
			Vector3 vector = new Vector3(st.dir.x, 0f, st.dir.y) * st.speed * ch.ScaleK;
			ch.velocity = base.transform.rotation * vector;
			ch.grounded = true;
			float speed = st.speed;
			float num2 = Remap(speed);
			ch.animVel = ((speed > 0.05f) ? (new Vector2(st.dir.x, st.dir.y).normalized * num2) : Vector2.zero);
			ch.hasAim = true;
			ch.aimPoint = base.transform.position + base.transform.forward * 30f + Vector3.up * 1.35f * ch.ScaleK;
		}

		private static string Has(string c)
		{
			if (!FBRegistry.I || !FBRegistry.I.Clip(c))
			{
				return null;
			}
			return c;
		}

		private static float Remap(float s)
		{
			Dictionary<string, object> o = GameData.Section("puppet");
			float num = Json.F(o, "playerWalkSpeed", 4f);
			float num2 = Json.F(o, "playerRunSpeed", 6.8f);
			float num3 = Json.F(o, "aiWalkAnimSpeed", 2f);
			float num4 = Json.F(o, "aiRunAnimSpeed", 5f);
			if (!(s <= num))
			{
				return num3 + (s - num) / (num2 - num) * (num4 - num3);
			}
			return s / num * num3;
		}

		public void Leave()
		{
			if (st != null)
			{
				if (st.revive)
				{
					ch.RescuePose(on: false, 0f);
					ch.RescuePose(on: false, 99f);
					ch.sys = null;
				}
				ch.StowCurrent(on: false);
				if (st.knocked)
				{
					ch.knocked = false;
					ch.prone = false;
					ch.sys = null;
					ch.revivedBy = null;
				}
				if (st.heal)
				{
					ch.sys = null;
				}
				ch.SetWeaponVisible(!ch.UnarmedLocomotion);
				ch.lastShot = 9f;
				ch.combatHold = 0f;
				ch.shooting = (ch.aiming = false);
				st = null;
				t = 0f;
				swapped = (reloaded = false);
			}
		}
	}

	private class M
	{
		public readonly List<float> aim = new List<float>();

		public readonly List<float> lh = new List<float>();

		public readonly List<float> pop = new List<float>();

		public readonly List<float> rate = new List<float>();

		public readonly Dictionary<string, int> clips = new Dictionary<string, int>();

		public float elbowMin = 999f;

		public float kneeRev;

		public int n;

		public float stretchMax;

		public float stretchFrac;

		public float squashMin = 9f;

		public float popMax;

		public float holsterCm;

		public float gapCm;

		public string worstBone = "-";

		public string popBone = "-";

		public string holsterWhat = "-";

		public string gapWhat = "-";

		public readonly Dictionary<Transform, Quaternion> prev = new Dictionary<Transform, Quaternion>();
	}

	private string skinArg = "all";

	private string setArg = "all";

	private bool raw;

	private static readonly List<AnimatorClipInfo> ciL = new List<AnimatorClipInfo>();

	private static readonly HumanBodyBones[] POP = new HumanBodyBones[17]
	{
		HumanBodyBones.Hips,
		HumanBodyBones.Spine,
		HumanBodyBones.Chest,
		HumanBodyBones.Neck,
		HumanBodyBones.Head,
		HumanBodyBones.LeftUpperArm,
		HumanBodyBones.LeftLowerArm,
		HumanBodyBones.LeftHand,
		HumanBodyBones.RightUpperArm,
		HumanBodyBones.RightLowerArm,
		HumanBodyBones.RightHand,
		HumanBodyBones.LeftUpperLeg,
		HumanBodyBones.LeftLowerLeg,
		HumanBodyBones.LeftFoot,
		HumanBodyBones.RightUpperLeg,
		HumanBodyBones.RightLowerLeg,
		HumanBodyBones.RightFoot
	};

	private static readonly Dictionary<Mesh, (int[] a, int[] b, float[] len, BoneWeight[] bw)> edgeCache = new Dictionary<Mesh, (int[], int[], float[], BoneWeight[])>();

	private readonly List<FBCharacter> chars = new List<FBCharacter>();

	private readonly List<Actor> actors = new List<Actor>();

	private Camera cam;

	private RenderTexture tile;

	private Texture2D sheet;

	private const int TW = 256;

	private const int TH = 320;

	private const int COLS = 8;

	private string dir;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] a = Environment.GetCommandLineArgs();
		int i = Array.IndexOf(a, "-fbanimlab");
		if (i >= 0)
		{
			GameObject obj = new GameObject("FB_LabAnimacao");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBAnimLab fBAnimLab = obj.AddComponent<FBAnimLab>();
			fBAnimLab.skinArg = Arg(1) ?? "all";
			fBAnimLab.setArg = Arg(2) ?? "all";
			fBAnimLab.raw = Array.IndexOf(a, "-fblabraw") >= 0;
		}
		string Arg(int k)
		{
			if (i + k >= a.Length || a[i + k].StartsWith("-"))
			{
				return null;
			}
			return a[i + k];
		}
	}

	private static St S(string id)
	{
		return new St
		{
			id = id
		};
	}

	private static List<St> States(string set)
	{
		bool num = set == "desarmado";
		bool flag = set == "desarmado" || set == "fuzil" || set == "pistola";
		float speed = (num ? 2.2f : ((set == "pistola") ? 4.4f : 4.2f));
		float speed2 = ((set == "pistola") ? 3.7f : 3.4f);
		float speed3 = 7.4f;
		List<St> list = new List<St>();
		if (num)
		{
			list.Add(new St
			{
				id = "parado",
				combat = false,
				relaxed = true,
				views = "fec"
			});
			list.Add(new St
			{
				id = "andar",
				combat = false,
				relaxed = true,
				speed = speed,
				dir = Vector2.up
			});
			list.Add(new St
			{
				id = "lateral_dir",
				combat = false,
				relaxed = true,
				speed = speed,
				dir = Vector2.right
			});
			list.Add(new St
			{
				id = "lateral_esq",
				combat = false,
				relaxed = true,
				speed = speed,
				dir = Vector2.left
			});
			list.Add(new St
			{
				id = "re",
				combat = false,
				relaxed = true,
				speed = speed,
				dir = Vector2.down
			});
			list.Add(new St
			{
				id = "sprint",
				combat = false,
				relaxed = true,
				speed = speed3,
				dir = Vector2.up,
				sprint = true,
				views = "fec"
			});
			list.Add(new St
			{
				id = "agachado",
				combat = false,
				relaxed = true,
				crouch = true
			});
			list.Add(new St
			{
				id = "agachado_andando",
				combat = false,
				relaxed = true,
				crouch = true,
				speed = 3f,
				dir = Vector2.up
			});
			list.Add(new St
			{
				id = "agachado_rapido",
				combat = false,
				relaxed = true,
				crouch = true,
				crouchFast = true,
				speed = 4.4f,
				dir = Vector2.up
			});
			list.Add(new St
			{
				id = "agachado_lateral",
				combat = false,
				relaxed = true,
				crouch = true,
				speed = 3f,
				dir = Vector2.right
			});
			list.Add(new St
			{
				id = "cura",
				combat = false,
				heal = true,
				views = "fec"
			});
			list.Add(new St
			{
				id = "reanimar",
				combat = false,
				revive = true,
				views = "fec"
			});
			list.Add(new St
			{
				id = "caido",
				combat = false,
				knocked = true
			});
			list.Add(new St
			{
				id = "sendo_reanimado",
				combat = false,
				knocked = true,
				treated = true,
				views = "fec"
			});
			list.Add(new St
			{
				id = "caido_andando",
				combat = false,
				knocked = true,
				speed = 1.2f,
				dir = Vector2.up
			});
			return list;
		}
		list.Add(new St
		{
			id = "patrulha_parado",
			views = "fec"
		});
		if (flag)
		{
			list.Add(new St
			{
				id = "relaxado_parado",
				relaxed = true,
				len = 4.6f
			});
		}
		list.Add(new St
		{
			id = "combate_parado",
			hold = true
		});
		list.Add(new St
		{
			id = "mirando",
			aim = true
		});
		list.Add(new St
		{
			id = "atirando",
			aim = true,
			fire = true
		});
		if (flag)
		{
			list.Add(new St
			{
				id = "andar",
				speed = speed,
				dir = Vector2.up,
				views = "fec"
			});
			list.Add(new St
			{
				id = "andar_combate",
				hold = true,
				speed = speed2,
				dir = Vector2.up
			});
			list.Add(new St
			{
				id = "mirando_andando",
				aim = true,
				speed = 3f,
				dir = Vector2.up
			});
			list.Add(new St
			{
				id = "lateral_dir",
				hold = true,
				speed = speed2,
				dir = Vector2.right
			});
			list.Add(new St
			{
				id = "lateral_esq",
				hold = true,
				speed = speed2,
				dir = Vector2.left
			});
			list.Add(new St
			{
				id = "re",
				hold = true,
				speed = speed2,
				dir = Vector2.down
			});
			list.Add(new St
			{
				id = "diagonal",
				hold = true,
				speed = speed2,
				dir = new Vector2(0.707f, 0.707f)
			});
		}
		else
		{
			list.Add(new St
			{
				id = "andar_combate",
				hold = true,
				speed = speed2,
				dir = Vector2.up
			});
		}
		list.Add(new St
		{
			id = "sprint",
			speed = speed3,
			dir = Vector2.up,
			sprint = true,
			views = "fec"
		});
		if (flag)
		{
			list.Add(new St
			{
				id = "agachado",
				crouch = true,
				views = "fec"
			});
			list.Add(new St
			{
				id = "agachado_andando",
				crouch = true,
				hold = true,
				speed = 3f,
				dir = Vector2.up
			});
			list.Add(new St
			{
				id = "agachado_rapido",
				crouch = true,
				crouchFast = true,
				speed = 4.4f,
				dir = Vector2.up
			});
		}
		list.Add(new St
		{
			id = "agachado_mirando",
			crouch = true,
			aim = true
		});
		list.Add(new St
		{
			id = "recarga",
			hold = true,
			reload = true,
			shotAt = 0.5f
		});
		if (flag)
		{
			list.Add(new St
			{
				id = "troca_arma",
				swap = true,
				shotAt = 0.2f,
				len = 2.2f,
				views = "fec"
			});
			list.Add(new St
			{
				id = "cura",
				combat = false,
				heal = true,
				views = "fec"
			});
			list.Add(new St
			{
				id = "cura_agachado",
				combat = false,
				heal = true,
				healCrouch = true,
				crouch = true
			});
			list.Add(new St
			{
				id = "reanimar",
				combat = false,
				revive = true,
				views = "fec"
			});
			list.Add(new St
			{
				id = "caido",
				combat = false,
				knocked = true
			});
			list.Add(new St
			{
				id = "sendo_reanimado",
				combat = false,
				knocked = true,
				treated = true,
				views = "fec"
			});
		}
		return list;
	}

	private void Sample(FBCharacter c, M m, float dt, bool settle)
	{
		Animator an = c.an;
		if (!an || !an.isHuman)
		{
			return;
		}
		Quaternion quaternion = Quaternion.Inverse(c.transform.rotation);
		HumanBodyBones[] pOP = POP;
		for (int i = 0; i < pOP.Length; i++)
		{
			HumanBodyBones humanBoneId = pOP[i];
			Transform boneTransform = an.GetBoneTransform(humanBoneId);
			if (!boneTransform)
			{
				continue;
			}
			Quaternion quaternion2 = quaternion * boneTransform.rotation;
			if (m.prev.TryGetValue(boneTransform, out var value) && settle && dt > 0f)
			{
				float num = Quaternion.Angle(value, quaternion2) / dt;
				m.pop.Add(num);
				if (num > m.popMax)
				{
					m.popMax = num;
					m.popBone = humanBoneId.ToString();
				}
			}
			m.prev[boneTransform] = quaternion2;
		}
		if (!settle)
		{
			return;
		}
		m.n++;
		if (c.velocity.sqrMagnitude > 0.3f)
		{
			m.rate.Add(c.LocoRate);
			an.GetCurrentAnimatorClipInfo(0, ciL);
			string key = "-";
			float num2 = 0f;
			foreach (AnimatorClipInfo item in ciL)
			{
				if (item.weight > num2 && (bool)item.clip)
				{
					num2 = item.weight;
					key = item.clip.name;
				}
			}
			m.clips[key] = ((!m.clips.TryGetValue(key, out var value2)) ? 1 : (value2 + 1));
		}
		if ((bool)c.weaponGO && c.weaponGO.activeInHierarchy && c.CombatStance && (bool)c.rig && c.rig.AimW > 0.9f)
		{
			Vector3 to = c.aimPoint - c.MuzzlePos;
			if (to.sqrMagnitude > 1f)
			{
				m.aim.Add(Vector3.Angle(c.BarrelDir(), to));
			}
		}
		if ((bool)c.rig && c.rig.LeftTargetValid && c.rig.IkW > 0.9f && (bool)c.LeftHand)
		{
			m.lh.Add((c.LeftHand.position - c.rig.LeftTarget).magnitude);
		}
		bool[] array = new bool[2] { false, true };
		foreach (bool right in array)
		{
			Transform transform = U(HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperArm);
			Transform transform2 = U(HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerArm);
			Transform transform3 = U(HumanBodyBones.RightHand, HumanBodyBones.LeftHand);
			if ((bool)transform && (bool)transform2 && (bool)transform3)
			{
				m.elbowMin = Mathf.Min(m.elbowMin, Vector3.Angle(transform.position - transform2.position, transform3.position - transform2.position));
			}
			Transform transform4 = U(HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftUpperLeg);
			Transform transform5 = U(HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftLowerLeg);
			Transform transform6 = U(HumanBodyBones.RightFoot, HumanBodyBones.LeftFoot);
			if ((bool)transform4 && (bool)transform5 && (bool)transform6 && Vector3.Angle(transform4.position - transform5.position, transform6.position - transform5.position) < 160f && Vector3.Dot(transform5.position - (transform4.position + transform6.position) * 0.5f, c.transform.forward) < -0.035f * c.ScaleK)
			{
				m.kneeRev += 1f;
			}
			Transform U(HumanBodyBones r, HumanBodyBones l)
			{
				return an.GetBoneTransform(right ? r : l);
			}
		}
	}

	private static void Stretch(FBCharacter c, M m)
	{
		Mesh mesh = new Mesh();
		SkinnedMeshRenderer[] componentsInChildren = c.GetComponentsInChildren<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
		{
			if (!skinnedMeshRenderer.enabled || !skinnedMeshRenderer.sharedMesh || !skinnedMeshRenderer.sharedMesh.isReadable)
			{
				continue;
			}
			Transform obj = skinnedMeshRenderer.transform;
			bool flag = false;
			Transform transform = obj;
			while ((bool)transform && transform != c.transform)
			{
				if (transform.name.StartsWith("Coldre_") || ((bool)c.weaponGO && transform == c.weaponGO.transform))
				{
					flag = true;
					break;
				}
				transform = transform.parent;
			}
			if (flag)
			{
				continue;
			}
			Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
			if (!edgeCache.TryGetValue(sharedMesh, out (int[], int[], float[], BoneWeight[]) value))
			{
				Vector3[] vertices = sharedMesh.vertices;
				int[] triangles = sharedMesh.triangles;
				HashSet<long> hashSet = new HashSet<long>();
				List<int> list = new List<int>();
				List<int> list2 = new List<int>();
				List<float> list3 = new List<float>();
				for (int j = 0; j < triangles.Length; j += 3)
				{
					for (int k = 0; k < 3; k++)
					{
						int num = triangles[j + k];
						int num2 = triangles[j + (k + 1) % 3];
						if (num > num2)
						{
							int num3 = num2;
							num2 = num;
							num = num3;
						}
						long item = ((long)num << 32) | (uint)num2;
						if (hashSet.Add(item))
						{
							float magnitude = (vertices[num] - vertices[num2]).magnitude;
							if (!(magnitude < 1E-05f))
							{
								list.Add(num);
								list2.Add(num2);
								list3.Add(magnitude);
							}
						}
					}
				}
				value = (list.ToArray(), list2.ToArray(), list3.ToArray(), sharedMesh.boneWeights);
				edgeCache[sharedMesh] = value;
			}
			try
			{
				skinnedMeshRenderer.BakeMesh(mesh, useScale: true);
			}
			catch
			{
				continue;
			}
			Vector3[] vertices2 = mesh.vertices;
			if (vertices2.Length != sharedMesh.vertexCount)
			{
				continue;
			}
			int num4 = value.Item1.Length;
			if (num4 == 0)
			{
				continue;
			}
			float[] array = new float[num4];
			for (int l = 0; l < num4; l++)
			{
				array[l] = (vertices2[value.Item1[l]] - vertices2[value.Item2[l]]).magnitude / value.Item3[l];
			}
			float[] array2 = (float[])array.Clone();
			Array.Sort(array2);
			float num5 = Mathf.Max(1E-06f, array2[num4 / 2]);
			int num6 = 0;
			int num7 = -1;
			float num8 = 0f;
			float num9 = 9f;
			for (int n = 0; n < num4; n++)
			{
				float num10 = array[n] / num5;
				if (num10 > 1.6f)
				{
					num6++;
				}
				if (num10 > num8)
				{
					num8 = num10;
					num7 = n;
				}
				if (num10 < num9)
				{
					num9 = num10;
				}
			}
			m.stretchMax = Mathf.Max(m.stretchMax, num8);
			m.stretchFrac = Mathf.Max(m.stretchFrac, (float)num6 / (float)num4);
			m.squashMin = Mathf.Min(m.squashMin, num9);
			if (num7 >= 0 && value.Item4 != null && value.Item4.Length > value.Item1[num7])
			{
				int boneIndex = value.Item4[value.Item1[num7]].boneIndex0;
				Transform[] bones = skinnedMeshRenderer.bones;
				if (boneIndex >= 0 && boneIndex < bones.Length && (bool)bones[boneIndex])
				{
					m.worstBone = bones[boneIndex].name;
				}
			}
		}
		UnityEngine.Object.Destroy(mesh);
	}

	private static void Holster(FBCharacter c, M m)
	{
		(float, string, float, string) tuple = HolsterPenetration(c);
		if (tuple.Item1 > m.holsterCm)
		{
			(m.holsterCm, m.holsterWhat, _, _) = tuple;
		}
		if (tuple.Item3 > m.gapCm)
		{
			m.gapCm = tuple.Item3;
			m.gapWhat = tuple.Item4;
		}
	}

	private static float P(List<float> l, float q)
	{
		if (l.Count == 0)
		{
			return 0f;
		}
		List<float> list = new List<float>(l);
		list.Sort();
		return list[Mathf.Clamp(Mathf.RoundToInt(q * (float)(list.Count - 1)), 0, list.Count - 1)];
	}

	public static (float cm, string what, float gap, string gapWhat) HolsterPenetration(FBCharacter c)
	{
		List<GameObject> hs = c.HolsterObjects.Where((GameObject o) => (bool)o && o.activeInHierarchy).ToList();
		if (hs.Count == 0 || !c.Chest || !c.an)
		{
			return (cm: 0f, what: "-", gap: 0f, gapWhat: "-");
		}
		c.TorsoAxes(out var up, out var fw, out var _);
		c.ThighAxes(out var _, out var side, out var _);
		HashSet<Transform> hashSet = new HashSet<Transform>();
		Transform[] array = new Transform[2]
		{
			c.an.GetBoneTransform(HumanBodyBones.LeftUpperArm),
			c.an.GetBoneTransform(HumanBodyBones.RightUpperArm)
		};
		float shR = 0.13f * c.ScaleK;
		HumanBodyBones[] array2 = new HumanBodyBones[2]
		{
			HumanBodyBones.LeftLowerArm,
			HumanBodyBones.RightLowerArm
		};
		Transform[] componentsInChildren;
		foreach (HumanBodyBones humanBoneId in array2)
		{
			Transform boneTransform = c.an.GetBoneTransform(humanBoneId);
			if ((bool)boneTransform)
			{
				componentsInChildren = boneTransform.GetComponentsInChildren<Transform>(includeInactive: true);
				foreach (Transform item in componentsInChildren)
				{
					hashSet.Add(item);
				}
			}
		}
		HashSet<Transform> hashSet2 = new HashSet<Transform>();
		componentsInChildren = array;
		foreach (Transform transform in componentsInChildren)
		{
			if (!transform)
			{
				continue;
			}
			Transform[] componentsInChildren2 = transform.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform item2 in componentsInChildren2)
			{
				if (!hashSet.Contains(item2))
				{
					hashSet2.Add(item2);
				}
			}
		}
		List<Vector3> list = new List<Vector3>();
		Mesh mesh = new Mesh();
		Renderer[] componentsInChildren3 = c.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren3)
		{
			if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || IsGun(renderer.transform) || renderer is ParticleSystemRenderer)
			{
				continue;
			}
			bool[] array3 = null;
			Vector3[] V;
			int[] triangles;
			if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
			{
				if (!skinnedMeshRenderer.sharedMesh || !skinnedMeshRenderer.sharedMesh.isReadable)
				{
					continue;
				}
				skinnedMeshRenderer.BakeMesh(mesh, useScale: true);
				Matrix4x4 matrix4x = Matrix4x4.TRS(skinnedMeshRenderer.transform.position, skinnedMeshRenderer.transform.rotation, Vector3.one);
				V = mesh.vertices;
				for (int l = 0; l < V.Length; l++)
				{
					V[l] = matrix4x.MultiplyPoint3x4(V[l]);
				}
				triangles = skinnedMeshRenderer.sharedMesh.triangles;
				BoneWeight[] boneWeights = skinnedMeshRenderer.sharedMesh.boneWeights;
				Transform[] bones = skinnedMeshRenderer.bones;
				if (boneWeights.Length == V.Length)
				{
					array3 = new bool[V.Length];
					int i;
					for (i = 0; i < boneWeights.Length; i++)
					{
						int boneIndex = boneWeights[i].boneIndex0;
						Transform transform2 = ((boneIndex >= 0 && boneIndex < bones.Length) ? bones[boneIndex] : null);
						array3[i] = (bool)transform2 && (hashSet.Contains(transform2) || (hashSet2.Contains(transform2) && array.All((Transform u) => !u || (V[i] - u.position).magnitude > shR)));
					}
				}
			}
			else
			{
				if (!(renderer is MeshRenderer))
				{
					continue;
				}
				MeshFilter component = renderer.GetComponent<MeshFilter>();
				if ((object)component == null || !component.sharedMesh || !component.sharedMesh.isReadable)
				{
					continue;
				}
				Matrix4x4 localToWorldMatrix = renderer.transform.localToWorldMatrix;
				V = component.sharedMesh.vertices;
				for (int m = 0; m < V.Length; m++)
				{
					V[m] = localToWorldMatrix.MultiplyPoint3x4(V[m]);
				}
				triangles = component.sharedMesh.triangles;
				if (hashSet.Contains(renderer.transform) || ((bool)renderer.transform.parent && hashSet.Contains(renderer.transform.parent)))
				{
					continue;
				}
			}
			for (int n = 0; n + 2 < triangles.Length; n += 3)
			{
				list.Add(V[triangles[n]]);
				list.Add(V[triangles[n + 1]]);
				list.Add(V[triangles[n + 2]]);
			}
		}
		UnityEngine.Object.Destroy(mesh);
		if (list.Count == 0)
		{
			return (cm: 0f, what: "sem malha legível", gap: 0f, gapWhat: "-");
		}
		float num = 0f;
		string item3 = "-";
		float num2 = 0f;
		string item4 = "-";
		foreach (GameObject item5 in hs)
		{
			string text = item5.name.Replace("Coldre_", "");
			WeaponDef value;
			string text2 = (GameData.Weapons.TryGetValue(text, out value) ? FBCharacter.HolsterSlot(value.cls) : "back");
			Vector3 vector = ((text2 == "back") ? fw : ((text2 == "sling") ? (-fw) : (-side)));
			Vector3 au = Vector3.Cross(vector, (Mathf.Abs(Vector3.Dot(vector, up)) < 0.9f) ? up : fw).normalized;
			Vector3 av = Vector3.Cross(vector, au);
			List<Vector3> list2 = new List<Vector3>();
			MeshFilter[] componentsInChildren4 = item5.GetComponentsInChildren<MeshFilter>();
			foreach (MeshFilter meshFilter in componentsInChildren4)
			{
				if ((bool)meshFilter.sharedMesh && meshFilter.sharedMesh.isReadable && (bool)meshFilter.GetComponent<Renderer>() && meshFilter.GetComponent<Renderer>().enabled)
				{
					Vector3[] vertices = meshFilter.sharedMesh.vertices;
					Matrix4x4 localToWorldMatrix2 = meshFilter.transform.localToWorldMatrix;
					int num3 = Mathf.Max(1, vertices.Length / 600);
					for (int num4 = 0; num4 < vertices.Length; num4 += num3)
					{
						list2.Add(localToWorldMatrix2.MultiplyPoint3x4(vertices[num4]));
					}
				}
			}
			if (list2.Count == 0)
			{
				continue;
			}
			float num5 = 0.02f * c.ScaleK;
			float num6 = list2.Min((Vector3 p) => Vector3.Dot(p, au)) - num5;
			float num7 = list2.Max((Vector3 p) => Vector3.Dot(p, au)) + num5;
			float num8 = list2.Min((Vector3 p) => Vector3.Dot(p, av)) - num5;
			float num9 = list2.Max((Vector3 p) => Vector3.Dot(p, av)) + num5;
			int num10 = Mathf.Clamp(Mathf.CeilToInt((num7 - num6) / num5), 1, 400);
			int num11 = Mathf.Clamp(Mathf.CeilToInt((num9 - num8) / num5), 1, 400);
			List<int>[] array4 = new List<int>[num10 * num11];
			for (int num12 = 0; num12 < list.Count; num12 += 3)
			{
				float a = Vector3.Dot(list[num12], au);
				float a2 = Vector3.Dot(list[num12 + 1], au);
				float b = Vector3.Dot(list[num12 + 2], au);
				float a3 = Vector3.Dot(list[num12], av);
				float a4 = Vector3.Dot(list[num12 + 1], av);
				float b2 = Vector3.Dot(list[num12 + 2], av);
				int num13 = Mathf.FloorToInt((Mathf.Min(a, Mathf.Min(a2, b)) - num6) / num5);
				int num14 = Mathf.FloorToInt((Mathf.Max(a, Mathf.Max(a2, b)) - num6) / num5);
				int num15 = Mathf.FloorToInt((Mathf.Min(a3, Mathf.Min(a4, b2)) - num8) / num5);
				int num16 = Mathf.FloorToInt((Mathf.Max(a3, Mathf.Max(a4, b2)) - num8) / num5);
				if (num14 < 0 || num16 < 0 || num13 >= num10 || num15 >= num11)
				{
					continue;
				}
				for (int num17 = Mathf.Max(0, num13); num17 <= Mathf.Min(num10 - 1, num14); num17++)
				{
					for (int num18 = Mathf.Max(0, num15); num18 <= Mathf.Min(num11 - 1, num16); num18++)
					{
						List<int>[] array5 = array4;
						int j = num17 * num11 + num18;
						(array5[j] ?? (array5[j] = new List<int>())).Add(num12);
					}
				}
			}
			float num19 = 0f;
			float num20 = 1E+09f;
			foreach (Vector3 item6 in list2)
			{
				int num21 = Mathf.FloorToInt((Vector3.Dot(item6, au) - num6) / num5);
				int num22 = Mathf.FloorToInt((Vector3.Dot(item6, av) - num8) / num5);
				if (num21 < 0 || num22 < 0 || num21 >= num10 || num22 >= num11 || array4[num21 * num11 + num22] == null)
				{
					continue;
				}
				Vector3 o2 = item6 - vector * 2f;
				float num23 = 1E+09f;
				float num24 = -1E+09f;
				foreach (int item7 in array4[num21 * num11 + num22])
				{
					if (RayTri(o2, vector, list[item7], list[item7 + 1], list[item7 + 2], out var t2))
					{
						num23 = Mathf.Min(num23, t2);
						num24 = Mathf.Max(num24, t2);
					}
				}
				if (num23 < 2f && num24 > 2f)
				{
					num19 = Mathf.Max(num19, 2f - num23);
				}
				else if (num23 > 2f && num23 < 3f)
				{
					num20 = Mathf.Min(num20, num23 - 2f);
				}
			}
			if (num19 > num)
			{
				num = num19;
				item3 = text + "@" + text2;
			}
			if (num19 <= 0f && num20 < 100000000f && num20 > num2)
			{
				num2 = num20;
				item4 = text + "@" + text2;
			}
		}
		return (cm: num * 100f / Mathf.Max(0.01f, c.ScaleK), what: item3, gap: num2 * 100f / Mathf.Max(0.01f, c.ScaleK), gapWhat: item4);
		bool IsGun(Transform t)
		{
			if ((bool)c.weaponGO && Under(t, c.weaponGO))
			{
				return true;
			}
			foreach (GameObject item8 in hs)
			{
				if (Under(t, item8))
				{
					return true;
				}
			}
			while ((bool)t && t != c.transform)
			{
				if (t.name.StartsWith("Coldre_"))
				{
					return true;
				}
				t = t.parent;
			}
			return false;
		}
		static bool Under(Transform t, GameObject g)
		{
			while ((bool)t)
			{
				if (t.gameObject == g)
				{
					return true;
				}
				t = t.parent;
			}
			return false;
		}
	}

	private static bool RayTri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
	{
		t = 0f;
		Vector3 vector = b - a;
		Vector3 vector2 = c - a;
		Vector3 rhs = Vector3.Cross(d, vector2);
		float num = Vector3.Dot(vector, rhs);
		if (Mathf.Abs(num) < 1E-09f)
		{
			return false;
		}
		float num2 = 1f / num;
		Vector3 lhs = o - a;
		float num3 = Vector3.Dot(lhs, rhs) * num2;
		if (num3 < 0f || num3 > 1f)
		{
			return false;
		}
		Vector3 rhs2 = Vector3.Cross(lhs, vector);
		float num4 = Vector3.Dot(d, rhs2) * num2;
		if (num4 < 0f || num3 + num4 > 1f)
		{
			return false;
		}
		t = Vector3.Dot(vector2, rhs2) * num2;
		return t > 0f;
	}

	private FBCharacter Spawn(string skin, Vector3 pos)
	{
		GameObject gameObject = FBRegistry.I.Char(skin);
		if (!gameObject)
		{
			return null;
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, pos, Quaternion.identity);
		gameObject2.name = "Lab_" + skin;
		Transform[] componentsInChildren = gameObject2.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].gameObject.layer = 10;
		}
		FBCharacter orAdd = gameObject2.GetOrAdd<FBCharacter>();
		orAdd.skinId = skin;
		orAdd.isBot = true;
		orAdd.team = 1;
		FBWardrobe.ApplyTo(gameObject2, null);
		CharacterController characterController = gameObject2.AddComponent<CharacterController>();
		float num = 1f / gameObject2.transform.lossyScale.y;
		float num2 = Json.F(GameData.Section("controller"), "m_Height", 1.8f);
		characterController.height = num2 * num;
		characterController.radius = 0.32f * num;
		characterController.center = new Vector3(0f, num2 * 0.5f * num, 0f);
		characterController.enabled = false;
		gameObject2.AddComponent<FBWeaponUser>();
		Animator component = gameObject2.GetComponent<Animator>();
		if ((bool)component)
		{
			component.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		}
		Actor item = gameObject2.AddComponent<Actor>();
		actors.Add(item);
		if (raw && (bool)orAdd.rig)
		{
			orAdd.rig.bypass = true;
		}
		return orAdd;
	}

	private void Show(FBCharacter only)
	{
		foreach (FBCharacter @char in chars)
		{
			if ((bool)@char)
			{
				Renderer[] componentsInChildren = @char.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].forceRenderingOff = (bool)only && @char != only;
				}
			}
		}
	}

	private void Capture(string name, string views)
	{
		foreach (char c in views)
		{
			int num = Mathf.CeilToInt((float)chars.Count / 8f);
			if (!sheet || sheet.height != num * 320)
			{
				sheet = new Texture2D(2048, num * 320, TextureFormat.RGB24, mipChain: false);
			}
			for (int j = 0; j < chars.Count; j++)
			{
				FBCharacter fBCharacter = chars[j];
				if ((bool)fBCharacter)
				{
					Show(fBCharacter);
					float scaleK = fBCharacter.ScaleK;
					float num2 = c switch
					{
						'e' => -90, 
						'f' => 35, 
						_ => 150, 
					};
					Vector3 vector = fBCharacter.transform.position + Vector3.up * ((fBCharacter.crouch || fBCharacter.prone) ? 0.7f : 0.95f) * scaleK;
					cam.transform.position = vector + Quaternion.Euler(0f, fBCharacter.transform.eulerAngles.y + num2, 0f) * Vector3.forward * 3.1f * scaleK + Vector3.up * 0.15f * scaleK;
					cam.transform.LookAt(vector);
					cam.targetTexture = tile;
					cam.Render();
					RenderTexture.active = tile;
					sheet.ReadPixels(new Rect(0f, 0f, 256f, 320f), j % 8 * 256, (num - 1 - j / 8) * 320);
					RenderTexture.active = null;
				}
			}
			Show(null);
			sheet.Apply(updateMipmaps: false);
			File.WriteAllBytes(Path.Combine(dir, $"{name}_{c}.jpg"), sheet.EncodeToJPG(85));
		}
		cam.targetTexture = null;
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste", "lab");
		Directory.CreateDirectory(dir);
		for (float w2 = 0f; w2 < 2.5f; w2 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		GameData.Load();
		FBQuality.Set(3);
		FBFootIK.Q.IgnoreVisibility = true;
		FBFootIK.Q.Lock = false;
		Scene activeScene = SceneManager.GetActiveScene();
		SceneManager.SetActiveScene(SceneManager.CreateScene("AnimLab"));
		AsyncOperation un = SceneManager.UnloadSceneAsync(activeScene);
		while (un != null && !un.isDone)
		{
			yield return null;
		}
		FBFootIKTestBed.BuildCourse();
		Physics.SyncTransforms();
		RenderSettings.ambientMode = AmbientMode.Trilight;
		RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.74f);
		RenderSettings.ambientEquatorColor = new Color(0.48f, 0.48f, 0.48f);
		RenderSettings.ambientGroundColor = new Color(0.26f, 0.24f, 0.22f);
		Light[] array = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
		for (int i = 0; i < array.Length; i++)
		{
			UnityEngine.Object.Destroy(array[i].gameObject);
		}
		Light light = new GameObject("Sol").AddComponent<Light>();
		light.type = LightType.Directional;
		light.intensity = 1.35f;
		light.shadows = LightShadows.Soft;
		light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
		Light light2 = new GameObject("Preenchimento").AddComponent<Light>();
		light2.type = LightType.Directional;
		light2.intensity = 0.45f;
		light2.shadows = LightShadows.None;
		light2.transform.rotation = Quaternion.Euler(25f, 150f, 0f);
		cam = new GameObject("LabCam").AddComponent<Camera>();
		cam.tag = "MainCamera";
		cam.fieldOfView = 38f;
		cam.nearClipPlane = 0.05f;
		cam.farClipPlane = 200f;
		cam.clearFlags = CameraClearFlags.Color;
		cam.backgroundColor = new Color(0.56f, 0.62f, 0.68f);
		cam.transform.position = new Vector3(0f, 6f, -26f);
		cam.transform.LookAt(new Vector3(0f, 1f, -12f));
		tile = new RenderTexture(256, 320, 24, RenderTextureFormat.ARGB32)
		{
			antiAliasing = 4
		};
		FBRegistry reg = FBRegistry.I;
		List<string> list = new List<string>();
		if (skinArg == "all")
		{
			list.AddRange(from s in GameData.Skins
				where s.id.StartsWith("r_") && reg.HasChar(s.id)
				select s.id);
			string[] array2 = new string[2] { "m", "f" };
			foreach (string sx in array2)
			{
				SkinDef skinDef = GameData.Skins.FirstOrDefault((SkinDef s) => s.id.StartsWith("n2_") && s.sex == sx && reg.HasChar(s.id));
				if (skinDef != null)
				{
					list.Add(skinDef.id);
				}
			}
		}
		else
		{
			list.AddRange(skinArg.Split(',').Where(reg.HasChar));
		}
		for (int j = 0; j < list.Count; j++)
		{
			FBCharacter fBCharacter = Spawn(list[j], new Vector3(-30 + j % 16 * 4, 0f, -14 + j / 16 * 5));
			if ((bool)fBCharacter)
			{
				chars.Add(fBCharacter);
			}
		}
		File.WriteAllText(Path.Combine(dir, "lab_indice.txt"), $"colunas {8}, quadro {256}x{320}\n" + string.Join("\n", chars.Select((FBCharacter c, int k) => $"{k + 1:00} (linha {k / 8 + 1}, coluna {k % 8 + 1}): {c.skinId} · {c.Name}")));
		Debug.Log(string.Format("[FreeBala][lab] {0} personagens · pós-animação {1}", chars.Count, raw ? "DESLIGADA" : "ligada"));
		cam.targetTexture = tile;
		for (float w2 = 0f; w2 < 4.5f; w2 += Time.unscaledDeltaTime)
		{
			cam.Render();
			yield return null;
		}
		cam.targetTexture = null;
		string rifle = First("fuzil", FBSession.primary) ?? First("fuzil", "ak");
		string text = First("submetralhadora", FBSession.smg);
		string text2 = First("pistola", FBSession.secondary);
		string text3 = First("escopeta", null);
		string text4 = First("sniper", null);
		List<(string, string[], int)> list2 = new List<(string, string[], int)>();
		list2.Add(("desarmado", new string[3] { rifle, text, text2 }, 3));
		list2.Add(("fuzil", new string[3] { rifle, text, text2 }, 0));
		list2.Add(("pistola", new string[3] { rifle, text, text2 }, 2));
		list2.Add(("smg", new string[3] { rifle, text, text2 }, 1));
		list2.Add(("escopeta", new string[3] { text3, text, text2 }, 0));
		list2.Add(("sniper", new string[3] { text4, text, text2 }, 0));
		List<(string, string[], int)> list3 = list2;
		if (setArg != "all")
		{
			HashSet<string> want = new HashSet<string>(setArg.Split(','));
			list3 = list3.Where<(string, string[], int)>(((string id, string[] give, int slot) s) => want.Contains(s.id)).ToList();
		}
		StringBuilder csv = new StringBuilder("conjunto;estado;personagem;erro_mira_p95_graus;mao_esq_p95_cm;estalo_p99_graus_s;estalo_max;cotovelo_min_graus;joelho_invertido_quadros;estiramento_max;estiramento_frac;compressao_min;osso_pior;osso_estalo;coldre_dentro_cm;coldre_pior;coldre_folga_cm;coldre_folga_pior;ritmo_passo_med;clipe_base\n");
		CultureInfo ic = CultureInfo.InvariantCulture;
		bool first = true;
		foreach (var item3 in list3)
		{
			string setId2 = item3.Item1;
			string[] item = item3.Item2;
			int item2 = item3.Item3;
			foreach (Actor actor in actors)
			{
				actor.Leave();
				if (item == null)
				{
					actor.wu.Strip();
					continue;
				}
				actor.wu.Strip();
				actor.wu.Give(item.Where((string x) => x != null).ToArray());
				actor.wu.Equip((item2 >= 3) ? 3 : ((actor.wu.slots[item2] != null) ? item2 : 0), instant: true);
			}
			for (float w2 = 0f; w2 < 0.8f; w2 += Time.deltaTime)
			{
				yield return null;
			}
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			int num = Array.IndexOf(commandLineArgs, "-fblabestados");
			HashSet<string> only = ((num >= 0 && num + 1 < commandLineArgs.Length) ? new HashSet<string>(commandLineArgs[num + 1].Split(',')) : null);
			foreach (St st in from x in States(setId2)
				where only == null || only.Contains(x.id)
				select x)
			{
				if (first)
				{
					first = false;
					foreach (Actor actor2 in actors)
					{
						actor2.Leave();
						actor2.st = st;
						actor2.t = 0f;
					}
					for (float w6 = 0f; w6 < st.len; w6 += Time.deltaTime)
					{
						yield return null;
					}
					foreach (Actor actor3 in actors)
					{
						actor3.Leave();
					}
					yield return null;
				}
				Dictionary<FBCharacter, M> ms = chars.ToDictionary((FBCharacter c) => c, (FBCharacter c) => new M());
				foreach (Actor actor4 in actors)
				{
					actor4.Leave();
					actor4.st = st;
					actor4.t = 0f;
				}
				float w2 = 0f;
				bool shot2 = false;
				int si2 = 0;
				float[] SF = new float[4] { 0.3f, 0.5f, 0.7f, 0.9f };
				while (w2 < st.len)
				{
					yield return null;
					float deltaTime = Time.deltaTime;
					w2 += deltaTime;
					bool flag = w2 > 0.6f;
					foreach (FBCharacter @char in chars)
					{
						if ((bool)@char)
						{
							Sample(@char, ms[@char], deltaTime, flag);
						}
					}
					if (flag && si2 < SF.Length && w2 >= st.len * SF[si2])
					{
						si2++;
						yield return new WaitForEndOfFrame();
						foreach (FBCharacter char2 in chars)
						{
							if ((bool)char2)
							{
								Holster(char2, ms[char2]);
							}
						}
					}
					if (shot2 || !(w2 >= st.len * st.shotAt))
					{
						continue;
					}
					shot2 = true;
					yield return new WaitForEndOfFrame();
					foreach (FBCharacter char3 in chars)
					{
						if ((bool)char3)
						{
							Stretch(char3, ms[char3]);
							Holster(char3, ms[char3]);
						}
					}
					Capture(setId2 + "_" + st.id, st.views);
				}
				float num2 = 0f;
				float num3 = 0f;
				float num4 = 0f;
				float num5 = 0f;
				float num6 = 0f;
				float num7 = 0f;
				float num8 = 9f;
				float num9 = 0f;
				string text5 = "-";
				string text6 = "-";
				string arg = "-";
				string text7 = "-";
				foreach (FBCharacter char4 in chars)
				{
					M m = ms[char4];
					float b = P(m.aim, 0.95f);
					float b2 = P(m.lh, 0.95f) * 100f;
					float b3 = P(m.pop, 0.99f);
					csv.AppendLine(string.Join(";", setId2, st.id, char4.skinId, b.ToString("0.0", ic), b2.ToString("0.0", ic), b3.ToString("0", ic), P(m.pop, 1f).ToString("0", ic), m.elbowMin.ToString("0", ic), m.kneeRev.ToString("0", ic), m.stretchMax.ToString("0.00", ic), (m.stretchFrac * 100f).ToString("0.00", ic), m.squashMin.ToString("0.00", ic), m.worstBone, m.popBone, m.holsterCm.ToString("0.0", ic), m.holsterWhat, m.gapCm.ToString("0.0", ic), m.gapWhat, P(m.rate, 0.5f).ToString("0.00", ic), (m.clips.Count > 0) ? m.clips.OrderByDescending((KeyValuePair<string, int> k) => k.Value).First().Key : "-"));
					num2 = Mathf.Max(num2, b);
					num3 = Mathf.Max(num3, b2);
					num4 = Mathf.Max(num4, b3);
					if (m.stretchMax > num5)
					{
						num5 = m.stretchMax;
						text5 = char4.skinId + "/" + m.worstBone;
					}
					if (m.holsterCm > num6)
					{
						num6 = m.holsterCm;
						text6 = char4.skinId + "/" + m.holsterWhat;
					}
					if (m.gapCm > num7)
					{
						num7 = m.gapCm;
						text7 = char4.skinId + "/" + m.gapWhat;
					}
					if (m.rate.Count <= 0)
					{
						continue;
					}
					float num10 = P(m.rate, 0.5f);
					if (num10 < num8)
					{
						num8 = num10;
						arg = m.clips.OrderByDescending((KeyValuePair<string, int> k) => k.Value).First().Key;
					}
					num9 = Mathf.Max(num9, num10);
				}
				Debug.Log(string.Format("[FreeBala][lab] {0}/{1}: pior erro de mira {2:0.0}° · pior mão esq. {3:0.0} cm · pior estalo p99 {4:0}°/s · pior estiramento {5:0.00}× ({6}) · arma guardada dentro do corpo {7:0.0} cm ({8}) · maior afastamento {9:0.0} cm ({10}){11}", setId2, st.id, num2, num3, num4, num5, text5, num6, text6, num7, text7, (num9 > 0f) ? $" · ritmo do passo {num8:0.00}–{num9:0.00} (mais lento: {arg})" : ""));
				Debug.Log($"[FreeBala][lab] {setId2}/{st.id} rig (modo {FBRig.Mode}): ergueu {FBRig.LiftN} quadros (média {((FBRig.LiftN > 0) ? (FBRig.LiftSum / (float)FBRig.LiftN) : 0f):0.0}°) · centralizou {FBRig.CenterN} quadros · diag {FBRig.DbgN} q: cru {FBRig.DbgRaw / (float)Mathf.Max(1, FBRig.DbgN):0.0}° punho {FBRig.DbgWrist / (float)Mathf.Max(1, FBRig.DbgN):0.0}° corpo {FBRig.DbgClear / (float)Mathf.Max(1, FBRig.DbgN):0.0}° fim {FBRig.DbgLeft / (float)Mathf.Max(1, FBRig.DbgN):0.0}° · low ready {FBRig.DbgLower / (float)Mathf.Max(1, FBRig.DbgN):0.0}° · empurrão {100f * FBRig.DbgPush / (float)Mathf.Max(1, FBRig.DbgN):0.0} cm · punho no limite {FBRig.DbgClamp} · longe {FBRig.DbgFar}");
				FBRig.CenterN = 0;
				FBRig.LiftN = 0;
				FBRig.LiftSum = 0f;
				FBRig.DbgFar = 0;
				FBRig.DbgClamp = 0;
				FBRig.DbgN = 0;
				FBRig.DbgRaw = (FBRig.DbgWrist = (FBRig.DbgClear = (FBRig.DbgLeft = (FBRig.DbgLower = (FBRig.DbgPush = 0f)))));
			}
			foreach (Actor actor5 in actors)
			{
				actor5.Leave();
			}
		}
		File.WriteAllText(Path.Combine(dir, "lab_metricas.csv"), csv.ToString());
		foreach (FBCharacter char5 in chars)
		{
			if ((bool)char5)
			{
				char5.gameObject.SetActive(value: false);
			}
		}
		FBFootIK.Q.Lock = true;
		List<string> list4 = new string[3]
		{
			"r_tutu",
			GameData.Skins.FirstOrDefault((SkinDef s) => s.id.StartsWith("r_") && s.sex == "f")?.id,
			"r_aka_junin"
		}.Where((string x) => x != null && reg.HasChar(x)).Distinct().ToList();
		StringBuilder tlog = new StringBuilder();
		foreach (string setId2 in list4)
		{
			bool[] array3 = new bool[2] { false, true };
			foreach (bool shot2 in array3)
			{
				List<FBCharacter> walkers = new List<FBCharacter>();
				Dictionary<FBCharacter, FBFootIKMeter> meters = new Dictionary<FBCharacter, FBFootIKMeter>();
				FBFootIKTestBed.Route[] routes = FBFootIKTestBed.Routes;
				foreach (FBFootIKTestBed.Route r in routes)
				{
					FBCharacter fBCharacter2 = FBFootIKTestBed.SpawnWalker(setId2, r);
					if ((bool)fBCharacter2)
					{
						walkers.Add(fBCharacter2);
						meters[fBCharacter2] = new FBFootIKMeter();
						if (shot2)
						{
							fBCharacter2.gameObject.AddComponent<FBWeaponUser>().Give(rifle);
							fBCharacter2.combat = true;
						}
					}
				}
				for (float w2 = 0f; w2 < 5f; w2 += Time.deltaTime)
				{
					yield return null;
					foreach (FBCharacter item4 in walkers)
					{
						if ((bool)item4)
						{
							meters[item4].Sample(item4, Time.deltaTime);
						}
					}
				}
				yield return new WaitForEndOfFrame();
				chars.Clear();
				chars.AddRange(walkers);
				Capture("terreno_" + setId2 + "_" + (shot2 ? "fuzil" : "desarmado"), "e");
				for (int l = 0; l < walkers.Count; l++)
				{
					tlog.AppendLine(setId2 + " " + (shot2 ? "fuzil" : "desarmado") + " " + FBFootIKTestBed.Routes[l].slug + ": " + meters[walkers[l]].Report());
				}
				foreach (FBCharacter item5 in walkers)
				{
					if ((bool)item5)
					{
						UnityEngine.Object.Destroy(item5.gameObject);
					}
				}
				chars.Clear();
				yield return null;
			}
		}
		File.WriteAllText(Path.Combine(dir, "lab_terreno.txt"), tlog.ToString());
		Debug.Log("[FreeBala][lab] fim · " + dir);
		yield return null;
		Application.Quit(0);
		string First(string cls, string pref)
		{
			if (pref == null || !GameData.Weapons.TryGetValue(pref, out var value) || !(value.cls == cls) || !reg.Weapon(pref))
			{
				return (from x in GameData.Weapons.Values
					where x.cls == cls && (bool)reg.Weapon(x.id)
					select x.id).FirstOrDefault();
			}
			return pref;
		}
	}
}
}
