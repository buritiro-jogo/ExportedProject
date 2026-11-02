using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBLaser
{
	private class E
	{
		public FBCharacter owner;

		public Transform emit;

		public bool fp;

		public bool flash;
	}

	private class Runner : MonoBehaviour
	{
		private static readonly int __fbt_LateUpdate = FBTempo.Id("FBLaser.Runner.LateUpdate");

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
			Tick();
		}
	}

	private static readonly List<E> all = new List<E>();

	private static readonly List<LineRenderer> beams = new List<LineRenderer>();

	private static readonly List<Transform> dots = new List<Transform>();

	private static readonly List<Light> lamps = new List<Light>();

	private static Material beamMat;

	private static Material dotMat;

	private static Runner runner;

	public static int LitBeams;

	public static int LitLamps;

	public static void Warm()
	{
		if (beams.Count > 0 && (bool)beams[0])
		{
			return;
		}
		beams.Clear();
		dots.Clear();
		lamps.Clear();
		GameObject gameObject = new GameObject("FB_Laser");
		Object.DontDestroyOnLoad(gameObject);
		runner = gameObject.AddComponent<Runner>();
		beamMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive: true);
		beamMat.SetColor("_BaseColor", new Color(1f, 0.08f, 0.05f, 0.55f));
		beamMat.color = new Color(1f, 0.08f, 0.05f, 0.55f);
		Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				float num = new Vector2((float)j - 15.5f, (float)i - 15.5f).magnitude / 15.5f;
				float num2 = Mathf.Clamp01(1f - num);
				texture2D.SetPixel(j, i, new Color(1f, 1f, 1f, num2 * num2 * num2));
			}
		}
		texture2D.Apply();
		dotMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive: true);
		dotMat.SetTexture("_BaseMap", texture2D);
		dotMat.mainTexture = texture2D;
		dotMat.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f, 1f));
		dotMat.SetFloat("_Cull", 0f);
		Mesh mesh = new Mesh
		{
			name = "ponto_laser"
		};
		mesh.SetVertices(new Vector3[4]
		{
			new Vector3(-0.5f, -0.5f),
			new Vector3(0.5f, -0.5f),
			new Vector3(-0.5f, 0.5f),
			new Vector3(0.5f, 0.5f)
		});
		mesh.SetUVs(0, new Vector2[4]
		{
			Vector2.zero,
			Vector2.right,
			Vector2.up,
			Vector2.one
		});
		mesh.SetTriangles(new int[6] { 0, 2, 1, 1, 2, 3 }, 0);
		for (int k = 0; k < 6; k++)
		{
			GameObject gameObject2 = new GameObject("feixe_" + k);
			gameObject2.transform.SetParent(gameObject.transform);
			LineRenderer lineRenderer = gameObject2.AddComponent<LineRenderer>();
			lineRenderer.sharedMaterial = beamMat;
			lineRenderer.positionCount = 2;
			lineRenderer.useWorldSpace = true;
			lineRenderer.startWidth = 0.0035f;
			lineRenderer.endWidth = 0.006f;
			lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
			lineRenderer.receiveShadows = false;
			lineRenderer.numCapVertices = 0;
			lineRenderer.enabled = false;
			beams.Add(lineRenderer);
			GameObject gameObject3 = new GameObject("ponto_" + k);
			gameObject3.transform.SetParent(gameObject.transform);
			gameObject3.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer meshRenderer = gameObject3.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = dotMat;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			gameObject3.SetActive(value: false);
			dots.Add(gameObject3.transform);
		}
		for (int l = 0; l < 3; l++)
		{
			Light light = new GameObject("lanterna_" + l).AddComponent<Light>();
			light.transform.SetParent(gameObject.transform);
			light.type = LightType.Spot;
			light.range = 30f;
			light.spotAngle = 42f;
			light.innerSpotAngle = 16f;
			light.color = new Color(1f, 0.97f, 0.9f);
			light.shadows = LightShadows.None;
			light.intensity = 0f;
			light.enabled = false;
			lamps.Add(light);
		}
	}

	public static void Bind(FBCharacter owner, GameObject weapon, WeaponDef d, bool fp)
	{
		all.RemoveAll((E e) => !e.owner || (e.owner == owner && e.fp == fp));
		if (!owner || !weapon || d == null)
		{
			return;
		}
		FBWeaponModel.Choice choice = FBWeaponModel.Of(d);
		string text = Json.S(FBWeaponModel.Att(d, "lasers", choice.laser), "node");
		if (string.IsNullOrEmpty(text) || text == "None")
		{
			return;
		}
		Transform transform = null;
		Transform[] componentsInChildren = weapon.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform2 in componentsInChildren)
		{
			if (transform2.name == text)
			{
				transform = transform2;
				break;
			}
		}
		if (!transform)
		{
			return;
		}
		Transform transform3 = null;
		componentsInChildren = transform.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform4 in componentsInChildren)
		{
			if (transform4 != transform && Regex.IsMatch(transform4.name, "SOCKET|Emit|Beam|Lens|Light", RegexOptions.IgnoreCase))
			{
				transform3 = transform4;
				break;
			}
		}
		if (!transform3)
		{
			Renderer[] componentsInChildren2 = transform.GetComponentsInChildren<Renderer>(includeInactive: true);
			Transform transform5 = new GameObject("laser_saida").transform;
			transform5.SetParent(transform, worldPositionStays: false);
			if (componentsInChildren2.Length != 0)
			{
				Bounds bounds = componentsInChildren2[0].bounds;
				Renderer[] array = componentsInChildren2;
				foreach (Renderer renderer in array)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				transform5.position = bounds.center;
			}
			transform3 = transform5;
		}
		Warm();
		all.Add(new E
		{
			owner = owner,
			emit = transform3,
			fp = fp,
			flash = (text.Contains("Flash") || text.Contains("Tactical"))
		});
	}

	private static void Tick()
	{
		int i = 0;
		int j = 0;
		Camera main = Camera.main;
		FBCharacter me = FBMatch.Player;
		bool flag = (bool)FBPlayer.I && FBPlayer.I.FirstPerson;
		int num = 0;
		float nightK = FBWeather.NightK;
		all.RemoveAll((E e) => !e.owner || !e.emit);
		foreach (E item in all.OrderBy((E e) => (!(e.owner == me)) ? 1 : 0))
		{
			FBCharacter owner = item.owner;
			bool flag2 = owner == me;
			if (!owner.alive || owner.knocked || !owner.aiming || owner.vehiclePose || !item.emit.gameObject.activeInHierarchy || (flag2 && item.fp != flag) || (!flag2 && (item.fp || num >= 3 || !main || (owner.Pos - main.transform.position).sqrMagnitude > 4900f)))
			{
				continue;
			}
			Vector3 position = item.emit.position;
			Vector3 vector;
			if (flag2 && (bool)main)
			{
				Vector3 position2 = main.transform.position;
				Vector3 forward = main.transform.forward;
				vector = (Physics.Raycast(position2 + forward * 0.3f, forward, out var hitInfo, 250f, FBWorld.SolidMask | 0x400, QueryTriggerInteraction.Ignore) ? hitInfo.point : (position2 + forward * 250f));
			}
			else
			{
				FBCharacter fBCharacter = owner.GetComponent<FBBot>()?.target;
				vector = (fBCharacter ? (fBCharacter.Pos + Vector3.up * 1.25f * fBCharacter.ScaleK) : (position + owner.transform.forward * 40f));
				num++;
			}
			Vector3 vector2 = vector - position;
			float magnitude = vector2.magnitude;
			if (magnitude < 0.05f)
			{
				continue;
			}
			vector2 /= magnitude;
			RaycastHit hitInfo2;
			bool flag3 = Physics.Raycast(position + vector2 * 0.05f, vector2, out hitInfo2, Mathf.Min(magnitude + 0.5f, 250f), FBWorld.SolidMask | 0x400, QueryTriggerInteraction.Ignore) && hitInfo2.collider.GetComponentInParent<FBCharacter>() != owner;
			Vector3 position3 = (flag3 ? hitInfo2.point : (position + vector2 * Mathf.Min(magnitude, 250f)));
			if (i < beams.Count)
			{
				LineRenderer lineRenderer = beams[i];
				Transform transform = dots[i];
				i++;
				lineRenderer.enabled = true;
				lineRenderer.SetPosition(0, position);
				lineRenderer.SetPosition(1, position3);
				Color color2 = (lineRenderer.startColor = new Color(1f, 0.08f, 0.05f, Mathf.Lerp(0.22f, 0.5f, nightK)));
				lineRenderer.endColor = new Color(color2.r, color2.g, color2.b, color2.a * 0.35f);
				transform.gameObject.SetActive(flag3);
				if (flag3 && (bool)main)
				{
					transform.position = hitInfo2.point + hitInfo2.normal * 0.01f;
					transform.rotation = Quaternion.LookRotation(main.transform.position - transform.position);
					transform.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(main.transform.position, hitInfo2.point) * 0.006f, 0.02f, 0.25f);
				}
			}
			if (item.flash && j < lamps.Count && (!FBQuality.Mobile || flag2))
			{
				Light light = lamps[j++];
				light.enabled = true;
				light.transform.SetPositionAndRotation(position + vector2 * 0.05f, Quaternion.LookRotation(vector2));
				light.intensity = Mathf.Lerp(2.5f, 14f, nightK);
			}
		}
		LitBeams = i;
		for (LitLamps = j; i < beams.Count; i++)
		{
			if ((bool)beams[i] && beams[i].enabled)
			{
				beams[i].enabled = false;
			}
			if ((bool)dots[i] && dots[i].gameObject.activeSelf)
			{
				dots[i].gameObject.SetActive(value: false);
			}
		}
		for (; j < lamps.Count; j++)
		{
			if ((bool)lamps[j] && lamps[j].enabled)
			{
				lamps[j].enabled = false;
				lamps[j].intensity = 0f;
			}
		}
	}
}
}
