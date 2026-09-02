#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using OfflineExtraction.Raid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OfflineExtraction.EditorTools
{
    public static class RaidSceneGenerator
    {
        private const string ScenePath = "Assets/Scenes/Raid_Telecenter.unity";

        [InitializeOnLoadMethod]
        private static void EnsureRaidScene()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath)) CreateScene();
                EnsureBuildSettings();
            };
        }

        private static void CreateScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.color = new Color(.76f, .84f, .92f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var bootstrap = new GameObject("Raid Bootstrap");
            bootstrap.AddComponent<RaidBootstrap>();

            GameObject player = new("Player");
            player.transform.position = new Vector3(0f, 1f, -13f);
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 1.8f; character.radius = .34f; character.center = new Vector3(0f, .9f, 0f);
            player.AddComponent<RaidPlayerController>();
            GameObject cameraObject = new("First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 74f; camera.nearClipPlane = .03f;
            cameraObject.AddComponent<AudioListener>();

            CreateBlock("Ground", new Vector3(0f, -.5f, 0f), new Vector3(42f, 1f, 42f));
            CreateBlock("North Wall", new Vector3(0f, 3f, 20f), new Vector3(42f, 7f, 1f));
            CreateBlock("South Wall", new Vector3(0f, 3f, -20f), new Vector3(42f, 7f, 1f));
            CreateBlock("West Wall", new Vector3(-20f, 3f, 0f), new Vector3(1f, 7f, 42f));
            CreateBlock("East Wall", new Vector3(20f, 3f, 0f), new Vector3(1f, 7f, 42f));
            CreateBlock("Telecenter A", new Vector3(-9f, 2f, 3f), new Vector3(11f, 4f, 8f));
            CreateBlock("Telecenter B", new Vector3(7f, 1.5f, 8f), new Vector3(9f, 3f, 7f));
            CreateBlock("Broadcast Tower Base", new Vector3(9f, 2.5f, -5f), new Vector3(6f, 5f, 6f));
            CreateBlock("Service Corridor", new Vector3(-3f, 1.5f, -5f), new Vector3(3f, 3f, 12f));
            for (int i = 0; i < 7; i++)
                CreateBlock($"Cover {i + 1}", new Vector3(-14f + i * 4.5f, .65f, i % 2 == 0 ? -11f : 14f), new Vector3(2.6f, 1.3f, 1.4f));

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.055f, .075f, .08f);
            RenderSettings.fogDensity = .012f;
            RenderSettings.ambientLight = new Color(.18f, .21f, .22f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }

        private static void CreateBlock(string name, Vector3 position, Vector3 scale)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name; block.transform.position = position; block.transform.localScale = scale;
        }

        private static void EnsureBuildSettings()
        {
            var paths = new List<string> { "Assets/Scenes/SampleScene.unity", ScenePath };
            EditorBuildSettings.sceneListChanged -= EnsureBuildSettings;
            EditorBuildSettings.scenes = paths.ConvertAll(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }
    }
}
#endif
