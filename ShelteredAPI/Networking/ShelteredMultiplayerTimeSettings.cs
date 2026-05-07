namespace ShelteredAPI.Networking
{
    internal static class ShelteredMultiplayerTimeSettings
    {
        public const float VanillaDaySeconds = 600f;
        public const float MultiplayerDaySeconds = 384f;
        public const float GameSecondsPerDay = 86400f;

        public const float SlowMapSpeedFactor = 0.85f;
        public const float NormalMapSpeedFactor = 1f;
        public const float FastMapSpeedFactor = 1.15f;

        public const float RealtimeTimescale = 1f;
        public const float TimescaleEpsilon = 0.001f;
        public const float DaySecondsEpsilon = 0.001f;
    }
}
