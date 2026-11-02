using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public class FBStreetEvents : MonoBehaviour
{
	public static FBStreetEvents I;

	private bool ownTraffic;

	private float acaiT;

	private float bacT;

	private float eleT;

	private float obraT;

	private float rachaT;

	public static string Log;

	public static bool ForceAcai;

	public static bool ForceBaculejo;

	public static bool ForceEletricista;

	public static bool ForceObra;

	public static bool ForceRacha;

	public static float ForceNear = -1f;

	private static readonly Collider[] hitsF = new Collider[16];

	public static Vector3 PlayerPos
	{
		get
		{
			if (!FBMatch.Player)
			{
				if (!Camera.main)
				{
					return Vector3.zero;
				}
				return Camera.main.transform.position;
			}
			return FBMatch.Player.Pos;
		}
	}

	public static void L(string s)
	{
		Log = s;
		Debug.Log("[FreeBala][ruas-eventos] " + s);
	}

	public static void Boot(FBMatch g)
	{
		if (!I && !FBSession.online && (bool)g && !(g.mapId == "estande") && !(g.mapId == "bermuda") && FBRoads.Load(g.mapId))
		{
			I = new GameObject("FB_EventosRua").AddComponent<FBStreetEvents>();
		}
	}

	private void Start()
	{
		ownTraffic = FBSession.mode != "role";
		if (ownTraffic)
		{
			FBTransito.StartSpecialOnly();
		}
		FBGiroflex.Warm();
		acaiT = Random.Range(75f, 200f);
		bacT = Random.Range(60f, 170f);
		eleT = Random.Range(90f, 240f);
		obraT = Random.Range(30f, 150f);
		rachaT = Random.Range(90f, 240f);
		L($"{FBSession.map}/{FBSession.mode}: açaí em {acaiT:0} s, baculejo em {bacT:0} s, eletricista em {eleT:0} s");
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBStreetEvents.Update");

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
		float deltaTime = Time.deltaTime;
		FBMatch i = FBMatch.I;
		if (!(deltaTime <= 0f) && (bool)i && !(i.state == "loading"))
		{
			if (ownTraffic)
			{
				FBTransito.Update(deltaTime);
			}
			if (ForceAcai)
			{
				ForceAcai = false;
				acaiT = 0f;
				FBAcai.End(now: true);
			}
			if (ForceBaculejo)
			{
				ForceBaculejo = false;
				bacT = 0f;
				FBBaculejo.End(now: true);
			}
			if (ForceEletricista)
			{
				ForceEletricista = false;
				eleT = 0f;
				FBEletricista.End(now: true);
			}
			if (ForceObra)
			{
				ForceObra = false;
				obraT = 0f;
				FBObra.End(now: true);
			}
			if (ForceRacha)
			{
				ForceRacha = false;
				rachaT = 0f;
				FBRacha.End(now: true);
			}
			if (!FBAcai.Active && (acaiT -= deltaTime) <= 0f)
			{
				acaiT = (FBAcai.Start() ? FBAcai.Cooldown() : 12f);
			}
			if (!FBBaculejo.Active && (bacT -= deltaTime) <= 0f)
			{
				bacT = (FBBaculejo.Start() ? FBBaculejo.Cooldown() : 15f);
			}
			if (!FBEletricista.Active && (eleT -= deltaTime) <= 0f)
			{
				eleT = (FBEletricista.Start() ? FBEletricista.Cooldown() : 20f);
			}
			if (!FBObra.Active && (obraT -= deltaTime) <= 0f)
			{
				obraT = (FBObra.Start() ? FBObra.Cooldown() : 25f);
			}
			if (!FBRacha.Active && FBSession.mode == "role" && (rachaT -= deltaTime) <= 0f)
			{
				rachaT = (FBRacha.Start() ? FBRacha.Cooldown() : 30f);
			}
			FBAcai.Tick(deltaTime);
			FBBaculejo.Tick(deltaTime);
			FBEletricista.Tick(deltaTime);
			FBObra.Tick(deltaTime);
			FBRacha.Tick(deltaTime);
		}
	}

	private void OnDestroy()
	{
		FBAcai.End(now: true);
		FBBaculejo.End(now: true);
		FBEletricista.End(now: true);
		FBObra.End(now: true);
		FBRacha.End(now: true);
		FBGiroflex.Reset();
		if (ownTraffic)
		{
			FBTransito.Clear();
		}
		if (I == this)
		{
			I = null;
		}
	}

	public static bool Visible(Vector3 p, float r = 1.2f)
	{
		Camera main = Camera.main;
		if (!main)
		{
			return false;
		}
		Vector3 position = main.transform.position;
		Vector3 vector = p + Vector3.up * 1.2f - position;
		float magnitude = vector.magnitude;
		if (magnitude > 260f)
		{
			return false;
		}
		if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(main), new Bounds(p + Vector3.up, new Vector3(r * 2f, 2.4f, r * 2f))))
		{
			return false;
		}
		return !Physics.Raycast(position, vector / magnitude, magnitude - 1f, 256, QueryTriggerInteraction.Ignore);
	}

	public static bool Free(Vector3 feet, float r = 0.3f)
	{
		int num = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (r + 0.3f), feet + Vector3.up * 1.7f, r, hitsF, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < num; i++)
		{
			if (!Loose(hitsF[i]))
			{
				return false;
			}
		}
		return true;
	}

	public static bool Loose(Collider c)
	{
		if ((bool)c && (bool)c.attachedRigidbody && !c.attachedRigidbody.isKinematic)
		{
			return c.GetComponentInParent<FBPropBody>();
		}
		return false;
	}

	public static bool Ground(ref Vector3 p, float refY, float tol = 0.7f)
	{
		float? num = FBWorld.GroundY(p.x, refY + 1.1f, p.z, 0.2f, 3f);
		if (num.HasValue)
		{
			float valueOrDefault = num.GetValueOrDefault();
			if (Mathf.Abs(valueOrDefault - refY) < tol)
			{
				p.y = valueOrDefault;
				return true;
			}
		}
		return false;
	}

	public static FBCharacter Spawn(string id, Vector3 p, float yaw, string seed)
	{
		FBMatch i = FBMatch.I;
		if (!i || string.IsNullOrEmpty(id))
		{
			return null;
		}
		FBCharacter fBCharacter = i.Spawn(id, null, 2, p, player: false, bot: false, civ: true);
		if (!fBCharacter)
		{
			return null;
		}
		FBCivilian component = fBCharacter.GetComponent<FBCivilian>();
		if ((bool)component)
		{
			component.enabled = false;
			Object.Destroy(component);
		}
		FBMatch.Civs.Remove(fBCharacter);
		fBCharacter.hp = (fBCharacter.maxHp = 60f);
		FBNpcAnims.Apply(fBCharacter, seed);
		fBCharacter.SetWeaponVisible(v: false);
		fBCharacter.combat = false;
		fBCharacter.civ = true;
		FBMatch.Teleport(fBCharacter, p, yaw);
		return fBCharacter;
	}

	public static void Despawn(FBCharacter c)
	{
		if ((bool)c)
		{
			if ((bool)FBVehicles.CarOf(c))
			{
				FBDrive.Release(c);
			}
			FBMatch.All.Remove(c);
			FBMatch.Civs.Remove(c);
			Object.Destroy(c.gameObject);
		}
	}

	public static List<string> FreeIds(IEnumerable<string> pool)
	{
		return (from id in pool
			where FBRegistry.I.HasChar(id) && !FBMatch.All.Any((FBCharacter a) => (bool)a && a.skinId == id)
			select id into _
			orderby Random.value
			select _).ToList();
	}

	public static string FreeCivilian(bool allowRepeat = false)
	{
		List<string> list = FreeIds(FBNpcAnims.Roster(FBSession.skin));
		if (list.Count > 0)
		{
			return list[0];
		}
		if (!allowRepeat)
		{
			return null;
		}
		List<string> list2 = (from id in FBNpcAnims.Roster(FBSession.skin)
			where FBRegistry.I.HasChar(id)
			select id).ToList();
		if (list2.Count <= 0)
		{
			return null;
		}
		return list2[Random.Range(0, list2.Count)];
	}

	public static void NoCollide(IEnumerable<FBCharacter> who)
	{
		List<CharacterController> list = (from c in who
			where c
			select c.GetComponent<CharacterController>() into c
			where c
			select c).ToList();
		for (int i = 0; i < list.Count; i++)
		{
			for (int j = i + 1; j < list.Count; j++)
			{
				Physics.IgnoreCollision(list[i], list[j], ignore: true);
			}
		}
	}
}
}
