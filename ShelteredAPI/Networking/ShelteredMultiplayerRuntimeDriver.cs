using ModAPI.Core;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerRuntimeDriver : MonoBehaviour
    {
        private ShelteredMultiplayerWorldClockSyncService _worldClockSync;
        private ShelteredTravelSyncService _travelSync;

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
            EnsureRuntimeServices();
            MMLog.WriteWithSource(
                MMLog.LogLevel.Debug,
                MMLog.LogCategory.Network,
                "ShelteredAPI.MultiplayerHooks",
                "Runtime driver installed.");
        }

        private void Update()
        {
            EnsureRuntimeServices();
            _worldClockSync.Update(Time.deltaTime);
            ShelteredMultiplayerHookService.Instance.RuntimeUpdateTick();
        }

        private void OnApplicationQuit()
        {
            DisposeRuntimeServices();
            ShelteredMultiplayerHookService.Instance.Deactivate("application-quit");
        }

        private void OnDestroy()
        {
            DisposeRuntimeServices();
        }

        private void EnsureRuntimeServices()
        {
            if (_worldClockSync == null)
                _worldClockSync = new ShelteredMultiplayerWorldClockSyncService();
            if (_travelSync == null)
                _travelSync = new ShelteredTravelSyncService();
        }

        private void DisposeRuntimeServices()
        {
            if (_worldClockSync != null)
            {
                _worldClockSync.Dispose();
                _worldClockSync = null;
            }

            if (_travelSync != null)
            {
                _travelSync.Dispose();
                _travelSync = null;
            }
        }
    }
}
