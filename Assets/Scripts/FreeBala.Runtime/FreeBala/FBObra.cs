using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
public static class FBObra
{
	public static string Why;

	private static GameObject root;

	private static Vector3 center;

	private static readonly List<FBCharacter> npcs = new List<FBCharacter>();

	private static float life;

	private static bool fled;

	private static readonly (string m, float x, float y, float yaw)[][] LAYOUTS = new(string, float, float, float)[3][]
	{
		new(string, float, float, float)[19]
		{
			("Cacamba_grande", 310f, -586f, -90f),
			("SM_Crate_01a", 310f, 392f, -20f),
			("SM_ConcretePipe_03", -104f, -145f, -10f),
			("Cerca_de_obra1", 219f, -24f, -10f),
			("Placa_de_prefeitura", -66f, 344f, 125f),
			("SM_ConstructionPylons_01d", -141f, -629f, 70f),
			("SM_ConstructionPylons_01e", -55f, -620f, 23f),
			("SM_ConstructionPylons_01d", -283f, -500f, 93f),
			("SM_ConstructionPylons_01e", -259f, -572f, 124f),
			("SM_ConstructionPylons_01d", -162f, -567f, 28f),
			("SM_Tarp_01", -619f, 213f, -67f),
			("SM_Tarp_02", -212f, -490f, -5f),
			("SM_Tarp_03", -141f, 62f, -76f),
			("Monte_de_areia", -55f, -357f, -3f),
			("Monte_de_brita", 146f, -293f, -19f),
			("*ObraDeRua_Medindo", -84f, -81f, -105f),
			("*ObraDeRua_VendoPlanos", 89f, 171f, -95f),
			("*ObraDeRua_Varrendo", 304f, 283f, -135f),
			("*", 98f, -20f, 90f)
		},
		new(string, float, float, float)[23]
		{
			("Cacamba_grande", 0f, 187f, 4f),
			("SM_Crate_01a", -283f, -121f, -22f),
			("Cerca_de_obra1", -397f, -409f, 68f),
			("Cerca_de_obra1", -557f, -239f, 31f),
			("Cerca_de_obra1", -646f, -46f, 15f),
			("Cerca_de_obra1", -103f, -431f, 92f),
			("Cerca_de_obra1", 129f, -424f, 88f),
			("Cerca_de_obra1", 364f, -418f, 96f),
			("Placa_de_prefeitura", -755f, -63f, -145f),
			("SM_ConstructionPylons_01d", 473f, -307f, 12f),
			("SM_ConstructionPylons_01e", 531f, -259f, -40f),
			("SM_ConstructionPylons_01d", 483f, -213f, -94f),
			("SM_ConstructionPylons_01d", 551f, -204f, -15f),
			("SM_ConstructionPylons_01d", 516f, -149f, -37f),
			("SM_ConstructionPylons_01d", 581f, -92f, -35f),
			("SM_Tarp_01", 197f, 95f, -170f),
			("SM_Tarp_03", -418f, 34f, -172f),
			("Monte_de_areia", 248f, -103f, -55f),
			("Monte_de_brita", 98f, -47f, -55f),
			("*", -457f, 205f, -29f),
			("*ObraDeRua_Varrendo", -427f, 11f, 34f),
			("*ObraDeRua_Medindo", 115f, -124f, 0f),
			("*ObraDeRua_VendoPlanos", 216f, -70f, -197f)
		},
		new(string, float, float, float)[27]
		{
			("Cacamba_grande", 411f, 443f, 2f),
			("SM_Crate_01a", -537f, 174f, 14f),
			("SM_ConcretePipe_03", -39f, -156f, 99f),
			("Cerca_de_obra2", -805f, -348f, -6f),
			("Cerca_de_obra2", -769f, -143f, -15f),
			("Cerca_de_obra2", -652f, 10f, -63f),
			("Cerca_de_obra2", -459f, 52f, -92f),
			("Cerca_de_obra2", -253f, 56f, -86f),
			("Placa_de_prefeitura", -930f, -160f, 149f),
			("SM_ConstructionPylons_01d", 759f, -396f, -68f),
			("SM_ConstructionPylons_01d", 651f, -329f, -35f),
			("SM_ConstructionPylons_01e", 687f, -400f, 36f),
			("SM_ConstructionPylons_01d", 681f, -252f, 8f),
			("SM_ConstructionPylons_01d", 638f, -173f, 17f),
			("SM_ConstructionPylons_01d", 587f, -94f, 17f),
			("SM_ConstructionPylons_01d", 120f, 133f, -15f),
			("SM_Tarp_01", 76f, 429f, -170f),
			("SM_Tarp_04", 280f, 124f, -104f),
			("SM_Tarp_06", 88f, 36f, -170f),
			("Monte_de_brita", -431f, 73f, -55f),
			("Monte_de_areia", 68f, -474f, -55f),
			("Monte_de_brita", 163f, -501f, -55f),
			("Monte_de_areia", -477f, 181f, -55f),
			("*ObraDeRua_Varrendo", 79f, 125f, -181f),
			("*", -635f, -191f, 38f),
			("*ObraDeRua_Medindo", -149f, -118f, -14f),
			("*ObraDeRua_VendoPlanos", -495f, 57f, -245f)
		}
	};

	private static readonly string[] EXTRA_CAIXA = new string[3] { "Marmita_Marmitex_tampada", "Garrafa_de_cafe_5L", "Enxada" };

	public static bool Active => root;

	public static Vector3 Center => center;

	public static List<FBCharacter> Workers => npcs;

	public static float Cooldown()
	{
		return Random.Range(260f, 480f);
	}

	private static void Log(string s)
	{
		FBStreetEvents.L("obra: " + s);
	}

	private static GameObject Piece(string name)
	{
		GameObject pf = Resources.Load<GameObject>("X171/Eventos/Obra");
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
		return gameObject;
	}

	private static Bounds Bnds(GameObject g)
	{
		Renderer[] componentsInChildren = g.GetComponentsInChildren<Renderer>();
		Bounds result = ((componentsInChildren.Length != 0) ? componentsInChildren[0].bounds : new Bounds(g.transform.position, Vector3.zero));
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			result.Encapsulate(renderer.bounds);
		}
		return result;
	}

	public static bool Start()
	{
		Why = (root ? "já ativo" : ((!FBRegistry.I) ? "sem registro" : ((!Resources.Load<GameObject>("X171/Eventos/Obra")) ? "sem X171/Eventos/Obra" : null)));
		if (Why != null)
		{
			return false;
		}
		Vector3 playerPos = FBStreetEvents.PlayerPos;
		float forceNear = FBStreetEvents.ForceNear;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = ((forceNear > 0f) ? 400 : 60);
		for (int i = 0; i < num9; i++)
		{
			Vector3? vector = FBNav.RandomPoint(playerPos, (forceNear > 0f) ? (forceNear + 40f) : 120f);
			if (!vector.HasValue)
			{
				num++;
				continue;
			}
			Vector3 value = vector.Value;
			float magnitude = (value - playerPos).Flat().magnitude;
			bool num10;
			if (!(forceNear > 0f))
			{
				if (magnitude < 45f)
				{
					goto IL_0111;
				}
				num10 = magnitude > 130f;
			}
			else
			{
				num10 = Mathf.Abs(magnitude - forceNear) > 45f;
			}
			if (!num10)
			{
				if (forceNear <= 0f && FBStreetEvents.Visible(value + Vector3.up))
				{
					num3++;
					continue;
				}
				float s;
				float lat;
				FBSeg fBSeg = FBRoads.NearestLane(value, 16f, out s, out lat);
				if (fBSeg == null)
				{
					num4++;
					continue;
				}
				FBRoads.At(fBSeg, s, out var tan);
				tan.y = 0f;
				if (tan.sqrMagnitude < 0.01f)
				{
					num4++;
					continue;
				}
				tan.Normalize();
				if (FBRoads.IsRoad(value))
				{
					num5++;
					continue;
				}
				bool flag = true;
				for (int j = 0; j < 8 && flag; j++)
				{
					Vector3 p = value + Quaternion.Euler(0f, j * 45, 0f) * Vector3.forward * 5.5f;
					float? num11 = FBWorld.GroundY(p.x, value.y + 1.5f, p.z, 0.3f, 3f);
					if (num11.HasValue)
					{
						float valueOrDefault = num11.GetValueOrDefault();
						if (!(Mathf.Abs(valueOrDefault - value.y) > 0.5f) && !FBRoads.IsRoad(p))
						{
							continue;
						}
					}
					flag = false;
				}
				if (!flag)
				{
					num6++;
					continue;
				}
				if (Physics.CheckBox(value + Vector3.up * 1.4f, new Vector3(5.5f, 1.1f, 5.5f), Quaternion.identity, (FBWorld.SolidMask & -257) | 0x800, QueryTriggerInteraction.Ignore))
				{
					num7++;
					continue;
				}
				if (Physics.CheckBox(value + Vector3.up * 1.6f, new Vector3(5f, 0.9f, 5f), Quaternion.identity, 256, QueryTriggerInteraction.Ignore))
				{
					num8++;
					continue;
				}
				Build(value, Mathf.Atan2(tan.x, tan.z) * 57.29578f);
				Why = $"montou a {magnitude:0} m";
				return true;
			}
			goto IL_0111;
			IL_0111:
			num2++;
		}
		Why = $"{num9} tentativas: sem ponto {num} · distância {num2} · à vista {num3} · sem faixa a 16 m {num4} · no asfalto {num5} · não plano/rua a 5,5 m {num6} · carro/objeto {num7} · parede/poste {num8}";
		return false;
	}

	private static void Build(Vector3 c, float yaw)
	{
		root = new GameObject("FB_Evento_Obra");
		center = c;
		root.transform.SetPositionAndRotation(c, Quaternion.Euler(0f, yaw, 0f));
		npcs.Clear();
		life = 0f;
		fled = false;
		(string m, float x, float y, float yaw)[] obj = LAYOUTS[Random.Range(0, LAYOUTS.Length)];
		int num = 0;
		(string, float, float, float)[] array = obj;
		for (int i = 0; i < array.Length; i++)
		{
			(string, float, float, float) tuple = array[i];
			string item = tuple.Item1;
			float item2 = tuple.Item2;
			float item3 = tuple.Item3;
			float item4 = tuple.Item4;
			Vector3 vector = root.transform.TransformPoint(new Vector3((0f - item2) / 100f, 0f, item3 / 100f));
			float num2 = yaw + item4;
			if (item.StartsWith("*"))
			{
				string text = ((item.Length > 1) ? ("X171_" + item.Substring(1)) : null);
				Vector3 p = vector;
				FBStreetEvents.Ground(ref p, c.y, 1.2f);
				FBCharacter fBCharacter = FBStreetEvents.Spawn(FBStreetEvents.FreeIds(FBNpcAnims.ByCls("pedreiro")).FirstOrDefault() ?? FBStreetEvents.FreeCivilian(allowRepeat: true), p, num2, "obra");
				if ((bool)fBCharacter)
				{
					npcs.Add(fBCharacter);
					FBStreetActor obj2 = fBCharacter.GetComponent<FBStreetActor>().OrNull() ?? fBCharacter.gameObject.AddComponent<FBStreetActor>();
					obj2.life = false;
					obj2.faceYaw = num2;
					if (text != null && (bool)FBRegistry.I.Clip(text))
					{
						fBCharacter.PlayDance(text);
					}
				}
				continue;
			}
			GameObject gameObject = Piece(item);
			if (!gameObject)
			{
				continue;
			}
			gameObject.transform.SetParent(root.transform, worldPositionStays: true);
			gameObject.transform.rotation = Quaternion.Euler(0f, num2, 0f) * gameObject.transform.rotation;
			Bounds bounds = Bnds(gameObject);
			Vector3 p2 = vector;
			FBStreetEvents.Ground(ref p2, c.y, 1.2f);
			gameObject.transform.position += new Vector3(vector.x - bounds.center.x, p2.y - bounds.min.y, vector.z - bounds.center.z);
			if (!item.StartsWith("SM_Tarp"))
			{
				Bounds bounds2 = Bnds(gameObject);
				BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
				boxCollider.center = gameObject.transform.InverseTransformPoint(bounds2.center);
				boxCollider.size = gameObject.transform.InverseTransformVector(bounds2.size).Abs();
			}
			if (item.StartsWith("SM_ConstructionPylons"))
			{
				FBFisica.Make(gameObject, "plastico", 2.2f);
			}
			else if (item.StartsWith("Cerca_de_obra"))
			{
				FBFisica.Make(gameObject, "grade", 14f);
			}
			else if (item == "Placa_de_prefeitura")
			{
				FBFisica.Make(gameObject, "metal", 9f);
			}
			else if (item == "SM_Crate_01a")
			{
				FBFisica.Make(gameObject, "madeira", 12f);
			}
			if (item == "SM_Crate_01a")
			{
				float y = Bnds(gameObject).max.y;
				int num3 = 0;
				string[] eXTRA_CAIXA = EXTRA_CAIXA;
				for (int j = 0; j < eXTRA_CAIXA.Length; j++)
				{
					GameObject gameObject2 = Piece(eXTRA_CAIXA[j]);
					if ((bool)gameObject2)
					{
						gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: true);
						Bounds bounds3 = Bnds(gameObject2);
						Vector3 vector2 = Bnds(gameObject).center + gameObject.transform.right * (num3 - 1) * 0.25f;
						gameObject2.transform.position += new Vector3(vector2.x - bounds3.center.x, y - bounds3.min.y, vector2.z - bounds3.center.z);
						num3++;
					}
				}
			}
			num++;
		}
		FBStreetEvents.NoCollide(npcs);
		Bounds bounds4 = new Bounds(c, Vector3.zero);
		bool flag = false;
		Renderer[] componentsInChildren = root.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren)
		{
			if (!renderer.GetComponentInParent<FBCharacter>())
			{
				if (!flag)
				{
					bounds4 = renderer.bounds;
					flag = true;
				}
				else
				{
					bounds4.Encapsulate(renderer.bounds);
				}
			}
		}
		if (flag)
		{
			GameObject gameObject3 = new GameObject("obra_desvio");
			gameObject3.transform.SetParent(root.transform, worldPositionStays: false);
			gameObject3.transform.position = bounds4.center;
			gameObject3.transform.rotation = Quaternion.identity;
			NavMeshObstacle navMeshObstacle = gameObject3.AddComponent<NavMeshObstacle>();
			navMeshObstacle.shape = NavMeshObstacleShape.Box;
			navMeshObstacle.size = bounds4.size + new Vector3(0.6f, 0f, 0.6f);
			navMeshObstacle.carving = true;
			navMeshObstacle.carveOnlyStationary = true;
		}
		AudioClip audioClip = FBAudio.Clip("x171_obra_ambiente");
		if ((bool)audioClip)
		{
			AudioSource audioSource = root.AddComponent<AudioSource>();
			audioSource.clip = audioClip;
			audioSource.loop = true;
			audioSource.spatialBlend = 1f;
			audioSource.minDistance = 4f;
			audioSource.maxDistance = 45f;
			audioSource.rolloffMode = AudioRolloffMode.Linear;
			audioSource.volume = 0.8f;
			audioSource.Play();
		}
		Log($"montada a {(c - FBStreetEvents.PlayerPos).Flat().magnitude:0} m · {num} peças · {npcs.Count} pedreiros");
	}

	public static void Tick(float dt)
	{
		if (!root)
		{
			return;
		}
		life += dt;
		if (!fled)
		{
			Vector3? lastNoiseP = FBBotBrain.lastNoiseP;
			if (lastNoiseP.HasValue)
			{
				Vector3 valueOrDefault = lastNoiseP.GetValueOrDefault();
				if (Time.time - FBBotBrain.lastNoiseT < 1f && (valueOrDefault - center).magnitude < 28f)
				{
					fled = true;
					Log("tiro perto: pedreiros correm");
					foreach (FBCharacter npc in npcs)
					{
						if ((bool)npc)
						{
							npc.StopDance(force: true);
							FBStreetActor component = npc.GetComponent<FBStreetActor>();
							if ((bool)component)
							{
								component.faceYaw = null;
								component.run = true;
								Vector3 normalized = (npc.Pos - valueOrDefault).Flat().normalized;
								component.Go(FBNav.RandomPoint(npc.Pos + normalized * 40f, 10f) ?? (npc.Pos + normalized * 35f), 5f, 1.5f);
							}
						}
					}
				}
			}
		}
		FBCharacter player = FBMatch.Player;
		if (((!player || (player.Pos - center).Flat().magnitude > 170f) && life > 40f && !FBStreetEvents.Visible(center + Vector3.up)) || life > 1200f)
		{
			End(now: false);
		}
	}

	public static void End(bool now)
	{
		foreach (FBCharacter npc in npcs)
		{
			if ((bool)npc)
			{
				FBStreetEvents.Despawn(npc);
			}
		}
		npcs.Clear();
		if ((bool)root)
		{
			Object.Destroy(root);
		}
		root = null;
	}
}
}
