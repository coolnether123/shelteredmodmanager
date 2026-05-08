using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal enum ShelteredMultiplayerLocalBunkerIntensityMode
    {
        Careful = 0,
        Normal = 1,
        Rush = 2
    }

    internal static class ShelteredMultiplayerTimePolicy
    {
        private const string LogSource = "ShelteredAPI.MultiplayerTime";
        private static readonly FieldInfo GameTimeDayLengthField = AccessTools.Field(typeof(GameTime), "day_length_in_seconds");
        private static readonly FieldInfo GameTimeDaySecondsField = AccessTools.Field(typeof(GameTime), "day_seconds");
        private static readonly FieldInfo GameTimeRealToGameMultiplierField = AccessTools.Field(typeof(GameTime), "real_to_game_seconds_multiplier");
        private static readonly FieldInfo GameTimeGameToRealMultiplierField = AccessTools.Field(typeof(GameTime), "game_to_real_seconds_multiplier");
        private static readonly FieldInfo CameraFastForwardField = AccessTools.Field(typeof(BasicCamera), "m_isFastForwarding");
        private static readonly FieldInfo CameraSlowDownField = AccessTools.Field(typeof(BasicCamera), "m_isSlowedDown");
        private static readonly object Sync = new object();

        private static Func<float> _timeScaleReader = ReadUnityTimeScale;
        private static Action<float> _timeScaleWriter = WriteUnityTimeScale;
        private static ShelteredMultiplayerLocalBunkerIntensityMode _localBunkerIntensityMode = ShelteredMultiplayerLocalBunkerIntensityMode.Normal;
        private static bool _ownsGameTimeScale;
        private static float _originalDaySeconds = ShelteredMultiplayerTimeSettings.VanillaDaySeconds;

        public static ShelteredMultiplayerLocalBunkerIntensityMode LocalBunkerIntensityMode
        {
            get
            {
                lock (Sync)
                {
                    return _localBunkerIntensityMode;
                }
            }
        }

        public static float CurrentLocalBunkerIntensityMultiplier
        {
            get { return GetLocalBunkerIntensityMultiplier(LocalBunkerIntensityMode); }
        }

        public static float SharedWorldTravelCompensationMultiplier
        {
            get { return ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds / ShelteredMultiplayerTimeSettings.VanillaDaySeconds; }
        }

        public static void SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode mode, string source)
        {
            bool changed = false;
            lock (Sync)
            {
                if (_localBunkerIntensityMode != mode)
                {
                    _localBunkerIntensityMode = mode;
                    changed = true;
                }
            }

            ForceRealtimeTimescale();

            if (changed)
            {
                TryWrite(MMLog.LogLevel.Info,
                    "Local bunker intensity mode changed to " + mode + " ("
                    + CurrentLocalBunkerIntensityMultiplier.ToString("0.###")
                    + "x local multiplier reserved for future bunker simulation). Source=" + (source ?? string.Empty) + ".");
            }
        }

        public static bool TryHandleFastForward(bool active, object camera, string source)
        {
            if (!ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                return false;

            if (active)
                SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode.Rush, source);
            else if (LocalBunkerIntensityMode == ShelteredMultiplayerLocalBunkerIntensityMode.Rush)
                SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode.Normal, source);

            SetCameraSpeedState(camera, active, false);
            return true;
        }

        public static bool TryHandleSlowDown(bool active, object camera, string source)
        {
            if (!ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                return false;

            if (active)
                SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode.Careful, source);
            else if (LocalBunkerIntensityMode == ShelteredMultiplayerLocalBunkerIntensityMode.Careful)
                SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode.Normal, source);

            SetCameraSpeedState(camera, false, active);
            return true;
        }

        public static bool IsFastModeActive()
        {
            return ShelteredMultiplayerHookService.Instance.IsMultiplayerActive
                && LocalBunkerIntensityMode == ShelteredMultiplayerLocalBunkerIntensityMode.Rush;
        }

        public static bool IsSlowModeActive()
        {
            return ShelteredMultiplayerHookService.Instance.IsMultiplayerActive
                && LocalBunkerIntensityMode == ShelteredMultiplayerLocalBunkerIntensityMode.Careful;
        }

        public static float ApplyTravelDistance(float distanceWorldUnits)
        {
            if (!ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                return distanceWorldUnits;

            return distanceWorldUnits * SharedWorldTravelCompensationMultiplier;
        }

        public static void ApplyGameTimePolicy(GameTime gameTime)
        {
            bool multiplayerActive = ShelteredMultiplayerHookService.Instance.IsMultiplayerActive;
            if (!multiplayerActive && !_ownsGameTimeScale)
                return;

            float targetDaySeconds = multiplayerActive ? ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds : _originalDaySeconds;
            if (gameTime != null && GameTimeDayLengthField != null)
            {
                float current = Convert.ToSingle(GameTimeDayLengthField.GetValue(gameTime));
                if (!_ownsGameTimeScale)
                    _originalDaySeconds = current > 0f ? current : ShelteredMultiplayerTimeSettings.VanillaDaySeconds;

                if (Math.Abs(current - targetDaySeconds) > ShelteredMultiplayerTimeSettings.DaySecondsEpsilon)
                    GameTimeDayLengthField.SetValue(gameTime, targetDaySeconds);
            }

            ApplyGameTimeStaticConversion(targetDaySeconds);
            _ownsGameTimeScale = multiplayerActive;
        }

        public static void ForceRealtimeTimescale()
        {
            if (!ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                return;

            if (Math.Abs(ReadTimeScale() - ShelteredMultiplayerTimeSettings.RealtimeTimescale) > ShelteredMultiplayerTimeSettings.TimescaleEpsilon)
                WriteTimeScale(ShelteredMultiplayerTimeSettings.RealtimeTimescale);
        }

        internal static float GetLocalBunkerIntensityMultiplier(ShelteredMultiplayerLocalBunkerIntensityMode mode)
        {
            if (mode == ShelteredMultiplayerLocalBunkerIntensityMode.Careful)
                return ShelteredMultiplayerTimeSettings.CarefulBunkerIntensityMultiplier;
            if (mode == ShelteredMultiplayerLocalBunkerIntensityMode.Rush)
                return ShelteredMultiplayerTimeSettings.RushBunkerIntensityMultiplier;

            return ShelteredMultiplayerTimeSettings.NormalBunkerIntensityMultiplier;
        }

        private static void ApplyGameTimeStaticConversion(float daySeconds)
        {
            if (daySeconds <= 0f)
                daySeconds = ShelteredMultiplayerTimeSettings.VanillaDaySeconds;

            float gameToReal = daySeconds / ShelteredMultiplayerTimeSettings.GameSecondsPerDay;
            float realToGame = 1f / gameToReal;

            if (GameTimeDaySecondsField != null)
                GameTimeDaySecondsField.SetValue(null, daySeconds);
            if (GameTimeGameToRealMultiplierField != null)
                GameTimeGameToRealMultiplierField.SetValue(null, gameToReal);
            if (GameTimeRealToGameMultiplierField != null)
                GameTimeRealToGameMultiplierField.SetValue(null, realToGame);
        }

        private static void SetCameraSpeedState(object camera, bool fastForward, bool slowDown)
        {
            if (camera == null)
                return;

            if (CameraFastForwardField != null)
                CameraFastForwardField.SetValue(camera, fastForward);
            if (CameraSlowDownField != null)
                CameraSlowDownField.SetValue(camera, slowDown);
        }

        internal static void OverrideTimeScaleAccessorsForTests(Func<float> reader, Action<float> writer)
        {
            lock (Sync)
            {
                _timeScaleReader = reader ?? ReadUnityTimeScale;
                _timeScaleWriter = writer ?? WriteUnityTimeScale;
            }
        }

        internal static void ResetTimeScaleAccessorsForTests()
        {
            OverrideTimeScaleAccessorsForTests(null, null);
        }

        private static float ReadTimeScale()
        {
            Func<float> reader;
            lock (Sync)
            {
                reader = _timeScaleReader;
            }

            return reader();
        }

        private static void WriteTimeScale(float value)
        {
            Action<float> writer;
            lock (Sync)
            {
                writer = _timeScaleWriter;
            }

            writer(value);
        }

        private static float ReadUnityTimeScale()
        {
            return Time.timeScale;
        }

        private static void WriteUnityTimeScale(float value)
        {
            Time.timeScale = value;
        }

        private static void TryWrite(MMLog.LogLevel level, string message)
        {
            try
            {
                MMLog.WriteWithSource(level, MMLog.LogCategory.Network, LogSource, message);
            }
            catch
            {
                // GuardrailAllow: SilentCatch - time-policy logging is diagnostic-only and must not affect tick decisions.
            }
        }
    }
}
