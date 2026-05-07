using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal enum ShelteredMultiplayerMapSpeedMode
    {
        Slow = 0,
        Normal = 1,
        Fast = 2
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

        private static ShelteredMultiplayerMapSpeedMode _mapSpeedMode = ShelteredMultiplayerMapSpeedMode.Normal;
        private static bool _ownsGameTimeScale;
        private static float _originalDaySeconds = ShelteredMultiplayerTimeSettings.VanillaDaySeconds;

        public static ShelteredMultiplayerMapSpeedMode MapSpeedMode
        {
            get
            {
                lock (Sync)
                {
                    return _mapSpeedMode;
                }
            }
        }

        public static float MultiplayerMapCompensationMultiplier
        {
            get { return ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds / ShelteredMultiplayerTimeSettings.VanillaDaySeconds; }
        }

        public static float CurrentMapSpeedFactor
        {
            get { return GetMapSpeedFactor(MapSpeedMode); }
        }

        public static float CurrentTravelSpeedMultiplier
        {
            get { return MultiplayerMapCompensationMultiplier * CurrentMapSpeedFactor; }
        }

        public static void SetMapSpeedMode(ShelteredMultiplayerMapSpeedMode mode, string source)
        {
            bool changed = false;
            lock (Sync)
            {
                if (_mapSpeedMode != mode)
                {
                    _mapSpeedMode = mode;
                    changed = true;
                }
            }

            ForceRealtimeTimescale();

            if (changed)
            {
                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Map speed mode changed to " + mode + " (" + CurrentTravelSpeedMultiplier.ToString("0.###")
                    + "x travel multiplier). Source=" + (source ?? string.Empty) + ".");
            }
        }

        public static bool TryHandleFastForward(bool active, object camera, string source)
        {
            if (!ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                return false;

            if (active)
                SetMapSpeedMode(ShelteredMultiplayerMapSpeedMode.Fast, source);
            else if (MapSpeedMode == ShelteredMultiplayerMapSpeedMode.Fast)
                SetMapSpeedMode(ShelteredMultiplayerMapSpeedMode.Normal, source);

            SetCameraSpeedState(camera, active, false);
            return true;
        }

        public static bool TryHandleSlowDown(bool active, object camera, string source)
        {
            if (!ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                return false;

            if (active)
                SetMapSpeedMode(ShelteredMultiplayerMapSpeedMode.Slow, source);
            else if (MapSpeedMode == ShelteredMultiplayerMapSpeedMode.Slow)
                SetMapSpeedMode(ShelteredMultiplayerMapSpeedMode.Normal, source);

            SetCameraSpeedState(camera, false, active);
            return true;
        }

        public static bool IsFastModeActive()
        {
            return ShelteredMultiplayerHookService.Instance.IsMultiplayerActive
                && MapSpeedMode == ShelteredMultiplayerMapSpeedMode.Fast;
        }

        public static bool IsSlowModeActive()
        {
            return ShelteredMultiplayerHookService.Instance.IsMultiplayerActive
                && MapSpeedMode == ShelteredMultiplayerMapSpeedMode.Slow;
        }

        public static float ApplyTravelDistance(float distanceWorldUnits)
        {
            if (!ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                return distanceWorldUnits;

            return distanceWorldUnits * CurrentTravelSpeedMultiplier;
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
            if (ShelteredMultiplayerHookService.Instance.IsMultiplayerActive && Math.Abs(Time.timeScale - ShelteredMultiplayerTimeSettings.RealtimeTimescale) > ShelteredMultiplayerTimeSettings.TimescaleEpsilon)
                Time.timeScale = ShelteredMultiplayerTimeSettings.RealtimeTimescale;
        }

        internal static float GetMapSpeedFactor(ShelteredMultiplayerMapSpeedMode mode)
        {
            if (mode == ShelteredMultiplayerMapSpeedMode.Slow)
                return ShelteredMultiplayerTimeSettings.SlowMapSpeedFactor;
            if (mode == ShelteredMultiplayerMapSpeedMode.Fast)
                return ShelteredMultiplayerTimeSettings.FastMapSpeedFactor;

            return ShelteredMultiplayerTimeSettings.NormalMapSpeedFactor;
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
    }
}
