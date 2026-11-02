using System;
using UnityEngine.Rendering;

[Serializable]
public sealed class VolumetricFogRenderPassEventParameter : VolumeParameter<VolumetricFogRenderPassEvent>
{
	public VolumetricFogRenderPassEventParameter(VolumetricFogRenderPassEvent value, bool overrideState = false)
		: base(value, overrideState)
	{
	}
}
