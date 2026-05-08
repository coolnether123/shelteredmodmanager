using ModAPI.Core;
using ShelteredAPI.Networking.Persistence;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerRuntimeDriver : MonoBehaviour
    {
        private ShelteredMultiplayerWorldClockCorrectionService _worldClockCorrection;
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
            _worldClockCorrection.Update(Time.deltaTime);
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
            if (_worldClockCorrection == null)
                _worldClockCorrection = new ShelteredMultiplayerWorldClockCorrectionService();
            if (_travelSync == null)
                _travelSync = new ShelteredTravelSyncService();
            ShelteredMultiplayerWorldPersistence.Instance.EnsureRegistered();
            if (RngDebugOptions.WorldTickProvider == null)
                RngDebugOptions.WorldTickProvider = ShelteredMultiplayerWorldClock.Instance.GetCurrentTick;
        }

        private void DisposeRuntimeServices()
        {
            if (_worldClockCorrection != null)
            {
                _worldClockCorrection.Dispose();
                _worldClockCorrection = null;
            }

            if (_travelSync != null)
            {
                _travelSync.Dispose();
                _travelSync = null;
            }
        }
    }
}
