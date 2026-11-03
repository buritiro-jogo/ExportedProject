// Menu "FreeBala/Testes/Cena de Foot IK": gera Scenes/IKTest.unity com a pista do FBFootIKTestBed (meio-fio, escadas, rampas, morro,
// terreno inclinado, pedras, obstáculos), luz, câmera e o objeto que faz nascer os caminhantes em rota ao dar Play.
// A cena não entra no Build Settings (é só de teste); o teste PlayMode FBFootIKTests monta a mesma pista sozinho.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBIKTestScene
    {
        public const string Path = FBImport.SceneDir + "/IKTest.unity";

        [MenuItem("FreeBala/Testes/Cena de Foot IK")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory(FBImport.SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            FBFootIKTestBed.BuildCourse();
            var cam = new GameObject("Main Camera"); cam.tag = "MainCamera"; var c = cam.AddComponent<Camera>(); c.nearClipPlane = .05f; c.farClipPlane = 400;
            cam.AddComponent<AudioListener>(); cam.transform.SetPositionAndRotation(new Vector3(-4, 3, -3), Quaternion.Euler(15, 40, 0));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .68f, .78f); RenderSettings.ambientEquatorColor = new Color(.5f, .5f, .5f); RenderSettings.ambientGroundColor = new Color(.3f, .28f, .25f);
            new GameObject("TesteFootIK").AddComponent<FBFootIKTestBed>();
            EditorSceneManager.SaveScene(scene, Path);
            Debug.Log($"[FreeBala][ik] cena de teste salva em {Path} — dê Play (Tab troca personagem, F liga/desliga IK, L trava, T tronco)");
        }
    }
}
