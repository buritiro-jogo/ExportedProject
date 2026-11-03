// Renderer feature da névoa de altura (FBHeightFog.shader). Arquivo próprio: ScriptableObject precisa do nome do arquivo = nome da classe.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    // névoa de altura: o passe de tela cheia do URP, mas só na câmera base (a câmera da arma é overlay e não pode reaplicar a névoa)
    public class FBHeightFogFeature : FullScreenPassRendererFeature
    {
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType != CameraRenderType.Base || renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;
            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
