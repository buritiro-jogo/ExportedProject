using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
public class FBHeightFogFeature : FullScreenPassRendererFeature
{
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (renderingData.cameraData.renderType == CameraRenderType.Base && (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView))
		{
			base.AddRenderPasses(renderer, ref renderingData);
		}
	}
}
}
