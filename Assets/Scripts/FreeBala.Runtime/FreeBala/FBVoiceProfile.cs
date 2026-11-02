using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBVoiceProfile
{
	public string sex;

	public Vector2 pitch = Vector2.one;

	public readonly Dictionary<FBVoiceBank, string[]> banks = new Dictionary<FBVoiceBank, string[]>();

	public int Count(FBVoiceBank b)
	{
		if (!banks.TryGetValue(b, out var value))
		{
			return 0;
		}
		return value.Length;
	}

	public string Clip(FBVoiceBank b, int v)
	{
		if (!banks.TryGetValue(b, out var value) || value.Length == 0)
		{
			return null;
		}
		return value[Mathf.Abs(v) % value.Length];
	}
}
}
