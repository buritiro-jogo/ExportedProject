using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBEletricista
{
	public static string Why;

	public static string ForceVariant;

	public static int Sparks;

	private static GameObject root;

	private static GameObject ladder;

	private static GameObject van;

	private static Transform piv;

	private static FBCharacter el;

	private static FBCharacter aj;

	private static string st = "";

	private static string variant = "escada";

	private static float stT;

	private static float talkT;

	private static float life;

	private static float sparkT;

	private static int lastTalk = -1;

	private static Vector3 ladderBase;

	private static Vector3 poleP;

	private static Vector3 poleTop;

	private static Vector3 toPole;

	private static Vector3 laneDir;

	private static Vector3 rackRear;

	private static Vector3 vanSide;

	private static float ladderLen;

	private static float workH;

	private static Rigidbody ladderRb;

	private static bool fought;

	private static readonly List<int> cutLamps = new List<int>();

	private const string CLIP = "X171_EletricistaTrabalhandoPoste";

	private static readonly string[] IDLE = new string[4] { "X171_Idle_Male", "NPC_Idle01_M", "NPC_Idle02_M", "Idle" };

	private static readonly string[] SMOKE = new string[2] { "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41", "X171_fumando_looped" };

	private static float carryS;

	private static Vector3 carryFrom;

	private static Vector3 carryTo;

	public static bool Active => root;

	public static string State => st;

	public static FBCharacter Worker => el;

	public static FBCharacter Helper => aj;

	public static GameObject Ladder => ladder;

	public static GameObject Van => van;

	public static Vector3 Base => ladderBase;

	public static string Variant => variant;

	public static int LampsCut => cutLamps.Count;

	public static float Cooldown()
	{
		return Random.Range(220f, 420f);
	}

	private static bool Has(string c)
	{
		if ((bool)FBRegistry.I)
		{
			return FBRegistry.I.Clip(c);
		}
		return false;
	}

	private static string Pick(string[] l)
	{
		return l.FirstOrDefault(Has);
	}

	private static void Log(string s)
	{
		FBStreetEvents.L("eletricista: " + s);
	}

	private static GameObject Part(string lib, string name)
	{
		GameObject pf = Resources.Load<GameObject>("X171/Eventos/" + lib);
		if (!pf)
		{
			return null;
		}
		Transform transform = pf.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault((Transform t) => t.parent == pf.transform && t.name.Split('.')[0] == name);
		if (!transform)
		{
			return null;
		}
		GameObject gameObject = Object.Instantiate(transform.gameObject);
		gameObject.name = "x171_" + name;
		Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].shadowCastingMode = ShadowCastingMode.On;
		}
		return gameObject;
	}

	private static Bounds WorldBounds(GameObject g)
	{
		Renderer[] componentsInChildren = g.GetComponentsInChildren<Renderer>();
		Bounds bounds = componentsInChildren[0].bounds;
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		return bounds;
	}

	private static void BuildPivot(GameObject g)
	{
		List<Vector3> list = new List<Vector3>();
		float num = 3.4028235E+38f;
		float num2 = -3.4028235E+38f;
		MeshFilter[] componentsInChildren = g.GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			Mesh sharedMesh = meshFilter.sharedMesh;
			if ((bool)sharedMesh && sharedMesh.isReadable)
			{
				Vector3[] vertices = sharedMesh.vertices;
				foreach (Vector3 position in vertices)
				{
					Vector3 item = meshFilter.transform.TransformPoint(position);
					list.Add(item);
					num = Mathf.Min(num, item.y);
					num2 = Mathf.Max(num2, item.y);
				}
			}
		}
		Vector3 vector;
		Vector3 vector2;
		if (list.Count == 0)
		{
			Bounds bounds = WorldBounds(g);
			vector = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
			vector2 = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
		}
		else
		{
			float num3 = num2 - num;
			Vector3 zero = Vector3.zero;
			Vector3 zero2 = Vector3.zero;
			int num4 = 0;
			int num5 = 0;
			foreach (Vector3 item2 in list)
			{
				if (item2.y > num2 - num3 * 0.06f)
				{
					zero2 += item2;
					num5++;
				}
				else if (item2.y < num + num3 * 0.06f)
				{
					zero += item2;
					num4++;
				}
			}
			vector = zero / Mathf.Max(1, num4);
			vector2 = zero2 / Mathf.Max(1, num5);
			vector.y = num;
			vector2.y = num2;
		}
		Vector3 upwards = vector2 - vector;
		ladderLen = upwards.magnitude;
		upwards /= Mathf.Max(0.0001f, ladderLen);
		Vector3 vector3 = new Vector3(upwards.x, 0f, upwards.z);
		Vector3 forward = ((vector3.sqrMagnitude > 0.0001f) ? (-vector3.normalized) : Vector3.forward);
		piv = new GameObject("x171_Escada_pivo").transform;
		piv.SetPositionAndRotation(vector, Quaternion.LookRotation(forward, upwards));
		g.transform.SetParent(piv, worldPositionStays: true);
	}

	private static void Lean(Vector3 b, Vector3 t)
	{
		Vector3 normalized = (t - b).normalized;
		Vector3 vector = (b - t).Flat();
		piv.SetPositionAndRotation(b, Quaternion.LookRotation((vector.sqrMagnitude > 0.0001f) ? vector.normalized : Vector3.forward, normalized));
	}

	private static void Lay(Vector3 b, Vector3 dir)
	{
		piv.SetPositionAndRotation(b, Quaternion.LookRotation(Vector3.up, dir.Flat().normalized));
	}

	private static Vector3 Along(float s)
	{
		return piv.position + piv.up * s;
	}

	public static bool Start()
	{
		Why = (root ? "já ativo" : ((FBPole.All.Count == 0) ? "sem postes" : ((!Has("X171_EletricistaTrabalhandoPoste")) ? "sem clipe X171_EletricistaTrabalhandoPoste" : ((!Resources.Load<GameObject>("X171/Eventos/Obra")) ? "sem X171/Eventos/Obra" : null))));
		if (Why != null)
		{
			return false;
		}
		Vector3 pp = FBStreetEvents.PlayerPos;
		float near = FBStreetEvents.ForceNear;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		IEnumerable<FBPole> source = FBPole.All.Where((FBPole p) => (bool)p && !p.down && !p.falling);
		IEnumerable<FBPole> enumerable;
		if (!(near > 0f))
		{
			enumerable = source.OrderBy((FBPole _) => Random.value).Take(80);
		}
		else
		{
			IEnumerable<FBPole> enumerable2 = source.OrderBy((FBPole p) => Mathf.Abs((p.basePos - pp).Flat().magnitude - near));
			enumerable = enumerable2;
		}
		source = enumerable;
		foreach (FBPole item in source)
		{
			num8++;
			Vector3 basePos = item.basePos;
			float magnitude = (basePos - pp).Flat().magnitude;
			bool num9;
			if (!(near > 0f))
			{
				if (magnitude < 40f)
				{
					goto IL_0196;
				}
				num9 = magnitude > 120f;
			}
			else
			{
				num9 = Mathf.Abs(magnitude - near) > 60f;
			}
			if (!num9)
			{
				if (near <= 0f && FBStreetEvents.Visible(basePos + Vector3.up * 2f))
				{
					num2++;
					continue;
				}
				float s;
				float lat;
				FBSeg fBSeg = FBRoads.NearestLane(basePos, 9f, out s, out lat);
				if (fBSeg == null)
				{
					num3++;
					continue;
				}
				Vector3 tan;
				Vector3 vector = FBRoads.At(fBSeg, s, out tan);
				tan.y = 0f;
				tan.Normalize();
				Vector3 vector2 = (vector - basePos).Flat();
				if (vector2.sqrMagnitude < 0.5f)
				{
					num4++;
					continue;
				}
				vector2.Normalize();
				Vector3 p2 = basePos + vector2 * 1.2f;
				if (!FBStreetEvents.Ground(ref p2, basePos.y, 0.6f))
				{
					num5++;
					continue;
				}
				if (!FBStreetEvents.Free(p2, 0.35f))
				{
					num6++;
					continue;
				}
				if (item.height < 5.5f)
				{
					num7++;
					continue;
				}
				Vector3 vp0 = vector - tan * 4.6f;
				if (FBVehicles.list.Any((FBCar c) => (bool)c && (c.transform.position - vp0).Flat().magnitude < 5.5f) || FBTransito.List.Any((FBTransito.Car c) => (bool)c.car && (c.pos - vp0).Flat().magnitude < 7f))
				{
					num6++;
					continue;
				}
				variant = ForceVariant ?? ((Random.value < 0.5f) ? "escada" : "energia");
				ForceVariant = null;
				Build(item, p2, vector, tan, vector2);
				Why = $"montou no poste a {magnitude:0} m ({variant})";
				return true;
			}
			goto IL_0196;
			IL_0196:
			num++;
		}
		Why = $"{num8} postes (de {FBPole.All.Count}): distância {num} · à vista {num2} · sem faixa a 9 m {num3} · em cima da faixa {num4} · sem chão {num5} · ocupado {num6} · baixo {num7}";
		return false;
	}

	private static void Build(FBPole pole, Vector3 lb, Vector3 lanePt, Vector3 ld, Vector3 toRoad)
	{
		root = new GameObject("FB_Evento_Eletricista");
		poleP = pole.basePos;
		ladderBase = lb;
		toPole = -toRoad;
		laneDir = ld;
		cutLamps.Clear();
		Sparks = 0;
		ladder = Part("Obra", "Escada");
		if (!ladder)
		{
			Object.Destroy(root);
			return;
		}
		BuildPivot(ladder);
		piv.SetParent(root.transform, worldPositionStays: true);
		BoxCollider boxCollider = ladder.AddComponent<BoxCollider>();
		Bounds bounds = WorldBounds(ladder);
		boxCollider.center = ladder.transform.InverseTransformPoint(bounds.center);
		boxCollider.size = ladder.transform.InverseTransformVector(bounds.size).Abs();
		float num = Mathf.Min(ladderLen * 0.965f, pole.height - 0.6f);
		float b = Mathf.Sqrt(Mathf.Max(0.25f, ladderLen * ladderLen - num * num));
		ladderBase = poleP + toRoad * Mathf.Max(0.9f, b);
		Vector3 p = ladderBase;
		if (FBStreetEvents.Ground(ref p, poleP.y, 0.8f))
		{
			ladderBase = p;
		}
		poleTop = poleP + Vector3.up * num + toRoad * 0.12f;
		workH = Mathf.Clamp(num - 1.55f, 2.2f, 5.6f);
		van = new GameObject("x171_CarroServico");
		van.transform.SetParent(root.transform, worldPositionStays: false);
		string[] array = new string[3] { "MictoLow_Merged", "VidrosLow_Micto", "EstruturaEscada" };
		foreach (string name in array)
		{
			GameObject gameObject = Part("CarroServico", name);
			if ((bool)gameObject)
			{
				gameObject.transform.SetParent(van.transform, worldPositionStays: true);
			}
		}
		Bounds bounds2 = default(Bounds);
		if (van.transform.childCount > 0)
		{
			Bounds bounds3 = WorldBounds(van);
			bool flag = bounds3.size.x > bounds3.size.z;
			float y = Mathf.Atan2(ld.x, ld.z) * 57.29578f + (float)(flag ? (-90) : 0);
			van.transform.rotation = Quaternion.Euler(0f, y, 0f);
			bounds3 = WorldBounds(van);
			Vector3 vector = lanePt - ld * 4.6f + toRoad * 0.2f;
			Vector3 p2 = vector;
			FBStreetEvents.Ground(ref p2, lanePt.y, 1.2f);
			van.transform.position += new Vector3(vector.x - bounds3.center.x, p2.y - bounds3.min.y, vector.z - bounds3.center.z);
			Bounds bounds4 = WorldBounds(van);
			BoxCollider boxCollider2 = van.AddComponent<BoxCollider>();
			boxCollider2.center = van.transform.InverseTransformPoint(bounds4.center);
			boxCollider2.size = van.transform.InverseTransformVector(bounds4.size).Abs();
			Transform[] componentsInChildren = van.GetComponentsInChildren<Transform>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].gameObject.layer = 11;
			}
			Transform transform = van.transform.Cast<Transform>().FirstOrDefault((Transform t) => t.name.Contains("EstruturaEscada"));
			bounds2 = (transform ? WorldBounds(transform.gameObject) : bounds4);
			rackRear = new Vector3(bounds4.center.x, bounds2.max.y + 0.04f, bounds4.center.z) - ld * (ladderLen * 0.5f);
			vanSide = bounds4.center - toRoad * 0f + toPole * (bounds4.extents.magnitude * 0f);
			vanSide = new Vector3(bounds4.center.x, p2.y, bounds4.center.z) + -toRoad * (Mathf.Min(bounds4.size.x, bounds4.size.z) * 0.5f + 0.7f);
			if (!FBStreetEvents.Ground(ref vanSide, p2.y, 1f))
			{
				vanSide = ladderBase - ld * 2f;
			}
		}
		for (int j = 0; j < 3; j++)
		{
			GameObject gameObject2 = Part("Obra", (j == 1) ? "SM_ConstructionPylons_01e" : "SM_ConstructionPylons_01d");
			if ((bool)gameObject2)
			{
				gameObject2.transform.SetParent(root.transform, worldPositionStays: true);
				Vector3 p3 = lanePt - ld * (8.5f + (float)j * 1.4f) + toRoad * ((float)j * 0.45f);
				FBStreetEvents.Ground(ref p3, lanePt.y, 1.2f);
				Bounds bounds5 = WorldBounds(gameObject2);
				gameObject2.transform.position += new Vector3(p3.x - bounds5.center.x, p3.y - bounds5.min.y, p3.z - bounds5.center.z);
				FBFisica.Make(gameObject2, "plastico", 2.2f);
			}
		}
		List<string> list = FBStreetEvents.FreeIds(FBNpcAnims.ByCls("eletricista"));
		string id = ((list.Count > 0) ? list[0] : FBStreetEvents.FreeCivilian(allowRepeat: true));
		string id2 = ((list.Count > 1) ? list[1] : FBStreetEvents.FreeCivilian(allowRepeat: true));
		el = FBStreetEvents.Spawn(id, vanSide, Yaw(ld), "eletricista");
		aj = FBStreetEvents.Spawn(id2, vanSide + ld * 1.1f, Yaw(-ld), "ajudante");
		FBCharacter[] array2 = new FBCharacter[2] { el, aj };
		foreach (FBCharacter fBCharacter in array2)
		{
			if (!fBCharacter)
			{
				continue;
			}
			fBCharacter.hp = (fBCharacter.maxHp = 60f);
			fBCharacter.gameObject.GetOrAdd<FBStreetActor>().Init(fBCharacter);
			fBCharacter.gameObject.GetOrAdd<FBLadderIK>().Clear();
			Collider[] componentsInChildren2 = fBCharacter.GetComponentsInChildren<Collider>();
			foreach (Collider collider in componentsInChildren2)
			{
				if (!(collider is CharacterController))
				{
					Physics.IgnoreCollision(collider, boxCollider);
				}
			}
		}
		FBStreetEvents.NoCollide(new FBCharacter[2] { el, aj });
		FBDamageEvents.OnDamage -= OnDamage;
		FBDamageEvents.OnDamage += OnDamage;
		life = 0f;
		fought = false;
		lastTalk = -1;
		talkT = 2f;
		sparkT = Random.Range(3f, 6f);
		if (variant == "energia")
		{
			Lean(ladderBase, poleTop);
			PutOnTop();
			HelperHold();
			CutPower(off: true);
			st = "trabalhando";
			stT = Random.Range(60f, 110f);
		}
		else
		{
			Lay(rackRear, ld);
			Idle(el);
			Idle(aj);
			st = "preparando";
			stT = ((FBStreetEvents.ForceNear > 0f) ? 2f : Random.Range(4f, 8f));
		}
		Log(string.Format("{0} · poste a {1:0} m · escada {2:0.0} · trabalho a {3:0.0} · {4}+{5}", variant, (poleP - FBStreetEvents.PlayerPos).Flat().magnitude, ladderLen, workH, el ? el.skinId : "-", aj ? aj.skinId : "-"));
	}

	private static float Yaw(Vector3 f)
	{
		return Mathf.Atan2(f.x, f.z) * 57.29578f;
	}

	private static void Idle(FBCharacter c)
	{
		if (!c)
		{
			return;
		}
		string text = Pick(IDLE);
		if (text != null)
		{
			c.PlayDance(text);
			if (c.dance != null)
			{
				c.dance.len = 1E+09f;
			}
		}
	}

	private static FBStreetActor A(FBCharacter c)
	{
		if (!c)
		{
			return null;
		}
		return c.GetComponent<FBStreetActor>();
	}

	private static FBLadderIK IK(FBCharacter c)
	{
		if (!c)
		{
			return null;
		}
		return c.GetComponent<FBLadderIK>();
	}

	private static void Hold(FBCharacter c, bool on)
	{
		CharacterController characterController = (c ? c.GetComponent<CharacterController>() : null);
		if ((bool)characterController)
		{
			characterController.enabled = !on;
		}
		FBFootIK fBFootIK = (c ? c.GetComponent<FBFootIK>() : null);
		if ((bool)fBFootIK)
		{
			fBFootIK.enabled = !on;
		}
		FBStreetActor fBStreetActor = A(c);
		if ((bool)fBStreetActor)
		{
			fBStreetActor.enabled = !on;
		}
	}

	private static void PutOnTop()
	{
		if ((bool)el)
		{
			Hold(el, on: true);
			el.PlayDance("X171_EletricistaTrabalhandoPoste");
			IK(el).Climb(piv, ladderLen, workH, toPole, atTop: true);
		}
	}

	private static void HelperHold()
	{
		if ((bool)aj)
		{
			Vector3 p = ladderBase - toPole * 0.55f;
			FBStreetEvents.Ground(ref p, ladderBase.y, 0.6f);
			FBMatch.Teleport(aj, p, Yaw(toPole));
			Hold(aj, on: true);
			Idle(aj);
			IK(aj).HoldRails(piv, 1.15f, toPole);
		}
	}

	private static void CutPower(bool off)
	{
		if (off)
		{
			cutLamps.Clear();
			IReadOnlyList<FBStreetLights.Lamp> lamps = FBStreetLights.Lamps;
			for (int i = 0; i < lamps.Count; i++)
			{
				if ((lamps[i].p - poleP).Flat().magnitude < 40f)
				{
					FBStreetLights.Cut(i, off: true);
					cutLamps.Add(i);
				}
			}
			FBFx.Sparks(poleTop + Vector3.up * 0.5f, Vector3.down);
			FBFisica.Sound("eletrico", poleTop, 0.9f, 14f);
			Log($"corte de energia: {cutLamps.Count} luminárias apagadas");
			return;
		}
		foreach (int cutLamp in cutLamps)
		{
			FBStreetLights.Cut(cutLamp, off: false);
		}
		if (cutLamps.Count > 0)
		{
			Log($"energia de volta ({cutLamps.Count} luminárias)");
		}
		cutLamps.Clear();
	}

	private static void OnDamage(FBDamage d)
	{
		if ((bool)el && d.victim == el && (st == "trabalhando" || st == "subindo" || st == "descendo"))
		{
			Fall((el.Pos - (d.source ? d.source.Pos : (el.Pos - el.Fwd))).Flat().normalized, "tiro");
		}
	}

	public static void Fall(Vector3 push, string why)
	{
		if (st != "trabalhando" && st != "subindo" && st != "descendo")
		{
			return;
		}
		st = "caiu";
		stT = 5f;
		Log("caiu (" + why + ")");
		CutPower(off: false);
		if ((bool)ladder)
		{
			piv.SetParent(root.transform, worldPositionStays: true);
			ladderRb = piv.gameObject.AddComponent<Rigidbody>();
			ladderRb.mass = 18f;
			ladderRb.AddForce(((push.sqrMagnitude > 0.01f) ? push : (-toPole)) * 120f + Vector3.up * 20f, ForceMode.Impulse);
			FBFisica.Watch(piv.gameObject, "metal", 18f);
		}
		if ((bool)aj)
		{
			IK(aj).Clear();
			Hold(aj, on: false);
			FBStreetActor fBStreetActor = A(aj);
			if ((bool)fBStreetActor)
			{
				fBStreetActor.run = true;
				fBStreetActor.Go(aj.Pos - toPole * 6f, 4f, 0.5f);
			}
		}
		if ((bool)el)
		{
			IK(el).Clear();
			el.StopDance(force: true);
			FBRagdoll.Of(el).Go(((push.sqrMagnitude > 0.01f) ? push : (-toPole)) * 2.5f + Vector3.down, el.Pos + Vector3.up * 1.2f);
			Say(el, 1f);
		}
	}

	private static void Say(FBCharacter c, float vol)
	{
		if ((bool)c)
		{
			int num;
			do
			{
				num = Random.Range(1, 7);
			}
			while (num == lastTalk);
			lastTalk = num;
			FBAudio.PlayAt("x171_eletricista_" + num, c.Pos + Vector3.up * 1.6f, vol, 16f);
		}
	}

	public static void Tick(float dt)
	{
		if (!root)
		{
			return;
		}
		life += dt;
		stT -= dt;
		FBCharacter player = FBMatch.Player;
		switch (st)
		{
		case "preparando":
		{
			if (!(stT <= 0f))
			{
				break;
			}
			st = "tirando";
			stT = 2.2f;
			FBCharacter[] array = new FBCharacter[2] { el, aj };
			foreach (FBCharacter fBCharacter in array)
			{
				if ((bool)fBCharacter)
				{
					fBCharacter.StopDance(force: true);
					A(fBCharacter).Go(rackRear + laneDir * ((fBCharacter == el) ? (ladderLen * 0.8f) : (ladderLen * 0.2f)) - -toPole * 0f + (vanSide - rackRear).Flat().normalized * 0.9f, 1.2f, 0.25f);
				}
			}
			break;
		}
		case "tirando":
		{
			float t = Mathf.SmoothStep(0f, 1f, 1f - stT / 2.2f);
			Vector3 normalized = (vanSide - rackRear).Flat().normalized;
			Vector3 b = new Vector3(rackRear.x, ladderBase.y + 1f * (el ? el.ScaleK : 1f), rackRear.z) + normalized * 1f;
			Lay(Vector3.Lerp(rackRear, b, t), laneDir);
			CarryHands();
			if (stT <= 0f)
			{
				st = "carregando";
				carryFrom = piv.position;
				carryTo = ladderBase - (ladderBase - poleP).Flat().normalized * 0f - laneDir * 0f;
				carryS = 0f;
			}
			break;
		}
		case "carregando":
		{
			Vector3 vector = (carryTo - carryFrom).Flat();
			float num = Mathf.Max(0.1f, vector.magnitude);
			vector /= num;
			carryS = Mathf.Min(num, carryS + 1.05f * dt);
			Vector3 vector2 = Vector3.Lerp(carryFrom, new Vector3(carryTo.x, carryFrom.y, carryTo.z), carryS / num);
			Vector3 p2 = vector2;
			FBStreetEvents.Ground(ref p2, carryTo.y, 2f);
			vector2.y = p2.y + 1f * (el ? el.ScaleK : 1f);
			Lay(vector2 - vector * ladderLen * 0.05f, vector);
			CarryHands(vector);
			if (carryS >= num - 0.02f)
			{
				st = "erguendo";
				stT = 2.4f;
			}
			break;
		}
		case "erguendo":
		{
			float t2 = Mathf.SmoothStep(0f, 1f, 1f - stT / 2.4f);
			Quaternion a2 = Quaternion.LookRotation(Vector3.up, (poleTop - ladderBase).Flat().normalized);
			Quaternion b3 = Quaternion.LookRotation((ladderBase - poleTop).Flat().normalized, (poleTop - ladderBase).normalized);
			piv.SetPositionAndRotation(Vector3.Lerp(piv.position, ladderBase, Mathf.Min(1f, dt * 6f)), Quaternion.Slerp(a2, b3, t2));
			if ((bool)el)
			{
				IK(el).Grip(Along(Mathf.Lerp(ladderLen * 0.55f, 1.6f, t2)), piv);
				A(el).Go(Along(Mathf.Lerp(ladderLen * 0.5f, 0.4f, t2)).Flat3(ladderBase.y) - toPole * 0.45f, 1.2f, 0.1f);
			}
			if ((bool)aj)
			{
				IK(aj).Grip(Along(0.35f), piv);
			}
			if (stT <= 0f)
			{
				Lean(ladderBase, poleTop);
				FBFisica.Sound("metal", poleTop, 0.7f, 10f);
				st = "subindo";
				IK(aj).Clear();
				HelperHold();
				StartClimb();
			}
			break;
		}
		case "subindo":
			if ((bool)el && IK(el).ClimbDone)
			{
				PutOnTop();
				st = "trabalhando";
				stT = Random.Range(35f, 60f);
				if (variant == "energia" || Random.value < 0.5f)
				{
					CutPower(off: true);
				}
			}
			break;
		case "trabalhando":
		{
			if ((bool)el && el.dance == null)
			{
				el.PlayDance("X171_EletricistaTrabalhandoPoste");
			}
			if ((sparkT -= dt) <= 0f && (bool)el && (bool)el.RightHand)
			{
				sparkT = Random.Range(4f, 9f);
				Sparks++;
				FBFx.Sparks(el.RightHand.position + toPole * 0.1f, -toPole);
				FBFisica.Sound("eletrico", el.RightHand.position, 0.7f, 9f);
			}
			float num3 = (player ? (player.Pos - ladderBase).Flat().magnitude : 99f);
			if (num3 < 7f && (talkT -= dt) <= 0f && (bool)el)
			{
				Say(el, 0.95f);
				talkT = Random.Range(6f, 10f);
			}
			if (num3 >= 7f)
			{
				talkT = Mathf.Min(talkT, 1.2f);
			}
			if (CarHitsLadder(out var push2))
			{
				Fall(push2, "batida");
			}
			else if (stT <= 0f)
			{
				CutPower(off: false);
				st = "descendo";
				el.StopDance(force: true);
				IK(el).Climb(piv, ladderLen, workH, toPole, atTop: false, -1);
				el.PlayDance("X171_EletricistaTrabalhandoPoste");
			}
			break;
		}
		case "descendo":
		{
			if (CarHitsLadder(out var push))
			{
				Fall(push, "batida");
			}
			else if ((bool)el && IK(el).ClimbDone)
			{
				IK(el).Clear();
				el.StopDance(force: true);
				Vector3 p = ladderBase - toPole * 0.6f;
				FBStreetEvents.Ground(ref p, ladderBase.y, 1f);
				FBMatch.Teleport(el, p, Yaw(-toPole));
				Hold(el, on: false);
				IK(aj)?.Clear();
				Hold(aj, on: false);
				if (variant == "escada")
				{
					st = "baixando";
					stT = 2.2f;
				}
				else
				{
					st = "pausa";
					stT = Random.Range(25f, 45f);
					Chat();
				}
			}
			break;
		}
		case "baixando":
		{
			float num2 = Mathf.SmoothStep(0f, 1f, 1f - stT / 2.2f);
			Quaternion b2 = Quaternion.LookRotation(Vector3.up, (vanSide - ladderBase).Flat().normalized);
			Quaternion a = Quaternion.LookRotation((ladderBase - poleTop).Flat().normalized, (poleTop - ladderBase).normalized);
			piv.SetPositionAndRotation(ladderBase + Vector3.up * num2 * 1f, Quaternion.Slerp(a, b2, num2));
			CarryHands((vanSide - ladderBase).Flat().normalized);
			if (stT <= 0f)
			{
				st = "guardando";
				carryFrom = piv.position;
				carryTo = rackRear;
				carryS = 0f;
			}
			break;
		}
		case "guardando":
		{
			Vector3 vector4 = (carryTo - carryFrom).Flat();
			float num4 = Mathf.Max(0.1f, vector4.magnitude);
			vector4 /= num4;
			carryS = Mathf.Min(num4, carryS + 1.05f * dt);
			Lay(Vector3.Lerp(carryFrom, new Vector3(carryTo.x, carryFrom.y, carryTo.z), carryS / num4), (carryS < num4 - 0.5f) ? vector4 : laneDir);
			CarryHands(vector4);
			if (carryS >= num4 - 0.02f)
			{
				Lay(rackRear, laneDir);
				FBFisica.Sound("metal", rackRear, 0.7f, 10f);
				FBCharacter[] array = new FBCharacter[2] { el, aj };
				for (int i = 0; i < array.Length; i++)
				{
					IK(array[i])?.Clear();
				}
				st = "pausa";
				stT = Random.Range(20f, 40f);
				Chat();
			}
			break;
		}
		case "pausa":
		{
			if (!(stT <= 0f))
			{
				break;
			}
			if (variant == "escada")
			{
				st = "preparando";
				stT = 1f;
				break;
			}
			st = "subindo";
			FBCharacter[] array = new FBCharacter[2] { el, aj };
			for (int i = 0; i < array.Length; i++)
			{
				array[i]?.StopDance(force: true);
			}
			HelperHold();
			StartClimb();
			break;
		}
		case "caiu":
			if (stT <= 0f && (bool)el)
			{
				FBRagdoll component = el.GetComponent<FBRagdoll>();
				Vector3 vector3 = (el.Hips ? el.Hips.position : el.Pos);
				Vector3 p3 = vector3;
				FBStreetEvents.Ground(ref p3, vector3.y, 3f);
				el.transform.position = new Vector3(vector3.x, p3.y, vector3.z);
				if ((bool)component)
				{
					component.Clear();
				}
				Hold(el, on: false);
				FBStreetActor fBStreetActor2 = A(el);
				fBStreetActor2.life = false;
				if (Random.value < 0.5f || !player)
				{
					st = "fugindo";
					fBStreetActor2.run = true;
					Vector3 normalized2 = (el.Pos - (player ? player.Pos : ladderBase)).Flat().normalized;
					fBStreetActor2.Go(FBNav.RandomPoint(el.Pos + normalized2 * 45f, 10f) ?? (el.Pos + normalized2 * 40f), 5f, 1.5f);
					Log("fugiu correndo");
				}
				else
				{
					st = "brigando";
					fBStreetActor2.run = true;
					Log("partiu para cima do jogador");
				}
				stT = 25f;
			}
			break;
		case "brigando":
		{
			if (!el || !player)
			{
				break;
			}
			FBStreetActor fBStreetActor = A(el);
			float magnitude = (player.Pos - el.Pos).Flat().magnitude;
			if ((bool)fBStreetActor && magnitude > 1.3f)
			{
				fBStreetActor.Go(player.Pos, 4.6f, 0.9f);
				if ((talkT -= dt) <= 0f)
				{
					Say(el, 1f);
					talkT = Random.Range(3f, 5f);
				}
			}
			else if (!fought)
			{
				fought = true;
				fBStreetActor?.Stop();
				AnimationClip animationClip = FBRegistry.I.clips.FirstOrDefault((AnimationClip c) => (bool)c && c.name.StartsWith("X171_") && c.name.ToLower().Contains("esbarr"));
				if ((bool)animationClip)
				{
					el.PlayDance(animationClip.name);
				}
				player.velocity += (player.Pos - el.Pos).Flat().normalized * 4.5f + Vector3.up * 1.5f;
				FBCombat.DamageActor(player, 5f, head: false, el, "soco");
				stT = 3f;
			}
			if (fought && stT <= 0f)
			{
				st = "fugindo";
				if ((bool)fBStreetActor)
				{
					fBStreetActor.run = false;
					fBStreetActor.Go(FBNav.RandomPoint(el.Pos + el.Fwd * -30f, 10f) ?? (el.Pos - el.Fwd * 25f), 1.4f, 1.5f);
				}
			}
			break;
		}
		}
		if (((!player || (player.Pos - ladderBase).Flat().magnitude > 160f) && life > 30f && !FBStreetEvents.Visible(ladderBase + Vector3.up * 2f)) || life > 900f)
		{
			End(now: false);
		}
	}

	private static void StartClimb()
	{
		if ((bool)el)
		{
			Hold(el, on: true);
			el.StopDance(force: true);
			el.PlayDance("X171_EletricistaTrabalhandoPoste");
			IK(el).Climb(piv, ladderLen, workH, toPole, atTop: false);
			st = "subindo";
		}
	}

	private static void Chat()
	{
		string sm = Pick(SMOKE);
		FBCharacter[] array = new FBCharacter[2] { el, aj };
		foreach (FBCharacter c in array)
		{
			if (!c)
			{
				continue;
			}
			Vector3 p = vanSide + laneDir * ((c == el) ? (-0.5f) : 0.5f);
			FBStreetEvents.Ground(ref p, vanSide.y, 1f);
			A(c)?.Go(p, 1.1f, 0.2f);
			FBCharacter cc = c;
			A(c).onArrive = delegate
			{
				cc.Yaw = Yaw((c == el) ? laneDir : (-laneDir));
				if (cc == el && sm != null)
				{
					cc.PlayDance(sm);
				}
				else
				{
					Idle(cc);
				}
			};
		}
	}

	private static void CarryHands(Vector3? walk = null)
	{
		Vector3 up = piv.up;
		Vector3 normalized = Vector3.Cross(Vector3.up, up).normalized;
		if ((bool)el)
		{
			Vector3 vector = Along(ladderLen * 0.78f);
			IK(el).Grip(vector, piv);
			FBStreetActor fBStreetActor = A(el);
			fBStreetActor.Go((vector - normalized * 0.38f).Flat3(ladderBase.y), 1.4f, 0.08f);
			fBStreetActor.faceYaw = Yaw(walk.GetValueOrDefault(up));
		}
		if ((bool)aj)
		{
			Vector3 vector2 = Along(ladderLen * 0.18f);
			IK(aj).Grip(vector2, piv);
			FBStreetActor fBStreetActor2 = A(aj);
			fBStreetActor2.Go((vector2 - normalized * 0.38f).Flat3(ladderBase.y), 1.4f, 0.08f);
			fBStreetActor2.faceYaw = Yaw(walk.GetValueOrDefault(up));
		}
	}

	private static bool CarHitsLadder(out Vector3 push)
	{
		push = Vector3.zero;
		foreach (FBCar item in FBVehicles.list)
		{
			if ((bool)item && !(Mathf.Abs(item.v) < 2f))
			{
				Bounds? bounds = item.GetComponentInChildren<Collider>()?.bounds;
				if (bounds.HasValue ? (bounds.Value.SqrDistance(ladderBase + Vector3.up * 0.8f) < 0.8f) : ((item.transform.position - ladderBase).Flat().magnitude < 2.2f))
				{
					push = item.transform.forward * Mathf.Sign(item.v);
					return true;
				}
			}
		}
		return false;
	}

	public static void End(bool now)
	{
		FBDamageEvents.OnDamage -= OnDamage;
		CutPower(off: false);
		FBCharacter[] array = new FBCharacter[2] { el, aj };
		foreach (FBCharacter fBCharacter in array)
		{
			if ((bool)fBCharacter)
			{
				FBStreetEvents.Despawn(fBCharacter);
			}
		}
		el = (aj = null);
		if ((bool)root)
		{
			Object.Destroy(root);
		}
		root = (ladder = (van = null));
		piv = null;
		st = "";
		ladderRb = null;
	}
}
}
