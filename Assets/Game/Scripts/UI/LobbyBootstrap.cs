using OfflineExtraction.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OfflineExtraction.UI
{
    public static class LobbyBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "SampleScene") return;
            if (Object.FindAnyObjectByType<LobbyPrototype>() != null) return;
            var root = new GameObject("Lobby Prototype");
            root.AddComponent<LobbyPrototype>();
            var hub = new GameObject("3D Shelter Prototype");
            hub.AddComponent<HubWorldBuilder>();
        }
    }
}
