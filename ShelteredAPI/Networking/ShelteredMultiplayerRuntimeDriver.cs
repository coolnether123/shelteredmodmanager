using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerRuntimeDriver : MonoBehaviour
    {
        internal static void EnsureInstalled(GameObject runtimeRoot)
        {
            if (runtimeRoot == null)
                return;

            if (runtimeRoot.GetComponent<ShelteredMultiplayerRuntimeDriver>() == null)
                runtimeRoot.AddComponent<ShelteredMultiplayerRuntimeDriver>();

            ShelteredMultiplayerHookService.Instance.EnsureInstalled();
        }

        private void Awake()
        {
            ShelteredMultiplayerHookService.Instance.EnsureInstalled();
            MMLog.WriteWithSource(
                MMLog.LogLevel.Debug,
                MMLog.LogCategory.Network,
                "ShelteredAPI.MultiplayerHooks",
                "Runtime driver installed.");
        }

        private void Update()
        {
            ShelteredMultiplayerHookService.Instance.RuntimeUpdateTick();
        }

        private void OnApplicationQuit()
        {
            ShelteredMultiplayerHookService.Instance.Deactivate("application-quit");
        }
    }
}
