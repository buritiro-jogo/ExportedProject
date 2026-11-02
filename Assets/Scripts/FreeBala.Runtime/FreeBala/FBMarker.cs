using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBMarker : MonoBehaviour
{
	private Material rm;

	private Material bm;

	private Transform ringT;

	private TextMesh label;

	public static FBMarker Make(Vector3 p, Color c, float r, float beamH)
	{
		GameObject gameObject = new GameObject("Marcador");
		gameObject.transform.position = p;
		FBMarker fBMarker = gameObject.AddComponent<FBMarker>();
		fBMarker.rm = Mat(new Color(c.r, c.g, c.b, 0.55f), add: false);
		GameObject gameObject2 = FBZone.Ring(48, r - 0.35f, r, fBMarker.rm);
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.transform.localPosition = Vector3.up * 0.15f;
		fBMarker.ringT = gameObject2.transform;
		fBMarker.bm = Mat(new Color(c.r, c.g, c.b, 0.28f), add: true);
		GameObject obj = FBZone.Cylinder(8, beamH, fBMarker.bm);
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		obj.transform.localPosition = Vector3.up * beamH / 2f;
		obj.transform.localScale = new Vector3(0.25f, 1f, 0.25f);
		return fBMarker;
	}

	private static Material Mat(Color c, bool add)
	{
		Material material = new Material(FBShaders.Unlit);
		material.SetColor("_BaseColor", c);
		material.SetFloat("_Surface", 1f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		if (material.renderQueue < 3000)
		{
			material.renderQueue = 3000;
		}
		material.SetFloat("_Cull", 0f);
		material.SetFloat("_ZWrite", 0f);
		material.SetInt("_SrcBlend", 5);
		material.SetInt("_DstBlend", add ? 1 : 10);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.renderQueue = 3050;
		return material;
	}

	public void Spin(float a)
	{
		if ((bool)ringT)
		{
			ringT.Rotate(0f, a * 57.29578f, 0f);
		}
	}

	public void SetColor(Color c)
	{
		rm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.55f));
		bm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.28f));
		if ((bool)label)
		{
			label.color = new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f));
		}
	}

	public void Label(string s)
	{
		GameObject gameObject = new GameObject("Letra");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.transform.localPosition = Vector3.up * 4.2f;
		label = gameObject.AddComponent<TextMesh>();
		label.text = s;
		label.anchor = TextAnchor.MiddleCenter;
		label.alignment = TextAlignment.Center;
		label.fontSize = 96;
		label.characterSize = 0.06f;
		label.fontStyle = FontStyle.Bold;
		Font builtinResource = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if ((bool)builtinResource)
		{
			label.font = builtinResource;
			MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
			component.sharedMaterial = new Material(builtinResource.material);
			component.sharedMaterial.renderQueue = 3100;
			component.shadowCastingMode = ShadowCastingMode.Off;
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBMarker.LateUpdate");

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
		if ((bool)label)
		{
			Camera main = Camera.main;
			if ((bool)main)
			{
				Transform obj = label.transform;
				Vector3 vector = obj.position - main.transform.position;
				obj.rotation = Quaternion.LookRotation((vector.sqrMagnitude > 0.0001f) ? vector : main.transform.forward, Vector3.up);
				obj.localScale = Vector3.one * Mathf.Clamp(vector.magnitude / 18f, 1f, 6f);
			}
		}
	}
}
}
