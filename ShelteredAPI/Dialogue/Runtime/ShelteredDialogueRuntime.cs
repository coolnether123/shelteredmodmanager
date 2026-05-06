using System;
using ModAPI.Core;
using ShelteredAPI.Dialogue.Adapters;
using ShelteredAPI.Dialogue.Selection;
using UnityEngine;

namespace ShelteredAPI.Dialogue.Runtime
{
    internal static class ShelteredDialogueRuntime
    {
        private const string RuntimeObjectName = "ShelteredAPI.Runtime";
        private static readonly object Sync = new object();
        private static ShelteredDialogueService _service;
        private static bool _installed;

        public static IShelteredDialogueService Service
        {
            get
            {
                EnsureInstalled();
                return _service;
            }
        }

        public static void EnsureInstalled()
        {
            if (_installed && _service != null)
                return;

            lock (Sync)
            {
                if (_installed && _service != null)
                    return;

                if (_service == null)
                {
                    IDialogueClock clock = new UnityDialogueClock();
                    IDialogueRandom random = new ModRandomDialogueRandom("ShelteredAPI.Dialogue");
                    IDialogueHistoryStore history = new BoundedDialogueHistoryStore();
                    _service = new ShelteredDialogueService(clock, random, history, null);
                    _service.RegisterChannel(DialogueChannel.AmbientSurvivorSpeech, new SurvivorSpeechChannelAdapter());
                }

                EnsureDriver();
                _installed = true;
            }
        }

        private static void EnsureDriver()
        {
            GameObject runtimeRoot = GameObject.Find(RuntimeObjectName);
            if ((UnityEngine.Object)runtimeRoot == (UnityEngine.Object)null)
            {
                runtimeRoot = new GameObject(RuntimeObjectName);
                UnityEngine.Object.DontDestroyOnLoad(runtimeRoot);
            }

            if ((UnityEngine.Object)runtimeRoot.GetComponent<ShelteredDialogueRuntimeDriver>() == (UnityEngine.Object)null)
                runtimeRoot.AddComponent<ShelteredDialogueRuntimeDriver>();
        }

        private sealed class UnityDialogueClock : IDialogueClock
        {
            public float TimeSeconds
            {
                get
                {
                    try
                    {
                        return RealTime.time;
                    }
                    catch
                    {
                        return Time.time;
                    }
                }
            }

            public int CurrentDay
            {
                get
                {
                    try
                    {
                        return GameTime.Day;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
        }

        private sealed class ModRandomDialogueRandom : IDialogueRandom
        {
            private readonly ModRandomStream _stream;

            public ModRandomDialogueRandom(string streamName)
            {
                try
                {
                    _stream = ModRandom.GetStream(streamName);
                }
                catch
                {
                    _stream = new ModRandomStream(Environment.TickCount);
                }
            }

            public float Range(float minInclusive, float maxInclusive)
            {
                if (maxInclusive <= minInclusive)
                    return minInclusive;

                return _stream.Range(minInclusive, maxInclusive);
            }

            public int Range(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                    return minInclusive;

                return _stream.Range(minInclusive, maxExclusive);
            }
        }
    }

    internal sealed class ShelteredDialogueRuntimeDriver : MonoBehaviour
    {
        private void Update()
        {
            ShelteredDialogueRuntime.Service.Update();
        }
    }
}
