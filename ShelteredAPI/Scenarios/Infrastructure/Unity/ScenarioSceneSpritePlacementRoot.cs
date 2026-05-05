using UnityEngine;
namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioSceneSpritePlacementRoot
    {
        private const string RuntimeRootName = "ShelteredAPI.SceneSpritePlacements";
        private GameObject _runtimeRoot;

        public GameObject CreateFresh()
        {
            _runtimeRoot = new GameObject(RuntimeRootName);
            return _runtimeRoot;
        }

        public void Clear()
        {
            DestroyKnownRoot();
            DestroyNamedRoots();
            _runtimeRoot = null;
        }

        private void DestroyKnownRoot()
        {
            if (_runtimeRoot == null)
                return;

            Object.Destroy(_runtimeRoot);
        }

        private static void DestroyNamedRoots()
        {
            GameObject[] objects = Object.FindObjectsOfType<GameObject>();
            for (int i = 0; objects != null && i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate != null && candidate.name == RuntimeRootName)
                    Object.Destroy(candidate);
            }
        }
    }
}
