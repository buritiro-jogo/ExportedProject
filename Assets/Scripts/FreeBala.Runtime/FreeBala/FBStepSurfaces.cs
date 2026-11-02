using System.Text.RegularExpressions;

namespace FreeBala
{
public static class FBStepSurfaces
{
	private static readonly Regex rxAsf = new Regex("asphalt|asfalto|road|rodovia|estrada|avenida|street|pista|piche", RegexOptions.IgnoreCase);

	private static readonly Regex rxGrass = new Regex("grass|grama|gramado|lawn|mato|capim|relva|turf", RegexOptions.IgnoreCase);

	private static readonly Regex rxGlass = new Regex("vidro|glass|caco", RegexOptions.IgnoreCase);

	private static readonly Regex rxWater = new Regex("agua|água|water|poça|poca|puddle", RegexOptions.IgnoreCase);

	public static FBStepSurf FromName(string name, int kind)
	{
		if (name == null)
		{
			name = "";
		}
		if (rxWater.IsMatch(name) && !FBWater.NaoEAgua(name))
		{
			return FBStepSurf.Agua;
		}
		if (rxGlass.IsMatch(name))
		{
			return FBStepSurf.Vidro;
		}
		if (kind == 0 || kind == 1)
		{
			if (rxGrass.IsMatch(name))
			{
				return FBStepSurf.Grama;
			}
			if (kind == 0 && rxAsf.IsMatch(name))
			{
				return FBStepSurf.Asfalto;
			}
		}
		if (kind == 0)
		{
			int num = FBWorld.SurfOf(name);
			if (num != 0)
			{
				kind = num;
			}
		}
		return kind switch
		{
			1 => FBStepSurf.Terra, 
			2 => FBStepSurf.Areia, 
			3 => FBStepSurf.Lama, 
			4 => FBStepSurf.Metal, 
			5 => FBStepSurf.Madeira, 
			_ => FBStepSurf.Concreto, 
		};
	}

	public static string ClipBase(FBStepSurf s)
	{
		return "Steps/step_" + s.ToString().ToLowerInvariant();
	}

	public static float Gain(FBStepSurf s)
	{
		return s switch
		{
			FBStepSurf.Metal => 1.15f, 
			FBStepSurf.Madeira => 1.05f, 
			FBStepSurf.Vidro => 1.1f, 
			FBStepSurf.Grama => 0.8f, 
			FBStepSurf.Areia => 0.85f, 
			FBStepSurf.Agua => 1f, 
			_ => 1f, 
		};
	}
}
}
