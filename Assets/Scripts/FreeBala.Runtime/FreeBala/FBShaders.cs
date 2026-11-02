using UnityEngine;

namespace FreeBala
{
public static class FBShaders
{
	private static FBShaderRefs refs;

	private static readonly string[] RS = new string[10] { "_Surface", "_Blend", "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha", "_ZWrite", "_AlphaClip", "_BlendOp", "_AlphaToMask" };

	private static FBShaderRefs R
	{
		get
		{
			if (!refs)
			{
				return refs = Resources.Load<FBShaderRefs>("FBShaderRefs");
			}
			return refs;
		}
	}

	public static Shader Lit => Pick(R ? R.lit : null, "Universal Render Pipeline/Lit", "Standard");

	public static Shader Unlit => Pick(R ? R.unlit : null, "Universal Render Pipeline/Unlit", "Unlit/Color");

	public static Shader ParticlesUnlit => Pick(R ? R.particlesUnlit : null, "Universal Render Pipeline/Particles/Unlit", "Sprites/Default");

	public static Shader Sky => Pick(R ? R.sky : null, "FreeBala/Sky");

	public static Shader Foliage => Pick(R ? R.foliage : null, "FreeBala/Foliage", "Universal Render Pipeline/Lit");

	public static Shader HeightFog => Pick(R ? R.heightFog : null, "FreeBala/HeightFog");

	public static Shader Contrail => Pick(R ? R.contrail : null, "FreeBala/Contrail");

	public static Shader Sprites => Pick(R ? R.sprites : null, "Sprites/Default");

	private static Shader Pick(Shader s, string name, string fb = null)
	{
		Shader shader;
		if (!s)
		{
			shader = Shader.Find(name);
			if ((object)shader == null)
			{
				if (fb == null)
				{
					return null;
				}
				return Shader.Find(fb);
			}
		}
		else
		{
			shader = s;
		}
		return shader;
	}

	public static Material Transparent(Material m, bool additive = false, bool clip = false)
	{
		Shader shader = m.shader;
		string text = ((shader == ParticlesUnlit) ? "Particles" : ((shader == Unlit) ? "Unlit" : ((shader == Lit) ? "Lit" : null)));
		Material material = ((text != null) ? Resources.Load<Material>("FBMats/" + text + "_" + (additive ? "Add" : (clip ? "AlphaClip" : "Alpha"))) : null);
		if ((bool)material)
		{
			Material material2 = new Material(material)
			{
				name = m.name
			};
			material2.CopyMatchingPropertiesFromMaterial(m);
			string[] rS = RS;
			foreach (string name in rS)
			{
				if (material.HasProperty(name))
				{
					material2.SetFloat(name, material.GetFloat(name));
				}
			}
			material2.shaderKeywords = material.shaderKeywords;
			material2.renderQueue = material.renderQueue;
			material2.SetFloat("_Surface", 1f);
			return material2;
		}
		return Legacy(m, additive);
	}

	public static Material TransparentInPlace(Material m, bool additive = false)
	{
		return Legacy(m, additive);
	}

	private static Material Legacy(Material m, bool additive)
	{
		m.SetFloat("_Surface", 1f);
		m.SetFloat("_Blend", additive ? 2 : 0);
		m.SetOverrideTag("RenderType", "Transparent");
		m.SetFloat("_SrcBlend", 5f);
		m.SetFloat("_DstBlend", additive ? 1 : 10);
		m.SetFloat("_SrcBlendAlpha", 1f);
		m.SetFloat("_DstBlendAlpha", additive ? 1 : 10);
		m.SetFloat("_ZWrite", 0f);
		m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		m.DisableKeyword("_ALPHATEST_ON");
		m.renderQueue = 3000;
		return m;
	}
}
}
