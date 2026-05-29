using System;
using ModAPI.Core;
using UnityEngine;

namespace ParalivesAPI.Core
{
    internal static class ParalivesRuntimeHost
    {
        private static GameObject _runner;

        public static void Start()
        {
            if (_runner != null)
                return;

            _runner = new GameObject("ParalivesAPIRuntimeHost");
            UnityEngine.Object.DontDestroyOnLoad(_runner);
            _runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private float _nextApplyAt;

            private void Update()
            {
                if (Time.unscaledTime < _nextApplyAt)
                    return;

                _nextApplyAt = Time.unscaledTime + 1f;

                try
                {
                    ParalivesRuntimeInfo.Current.Localizations.ApplyWhenReady();
                    ParalivesRuntimeInfo.Current.Interactions.ApplyWhenReady();
                    ParalivesRuntimeInfo.Current.Notifications.ApplyWhenReady();
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("ParalivesRuntimeHost.Apply", "Paralives runtime host update failed: " + ex.Message);
                }
            }
        }
    }
}
