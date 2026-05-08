namespace ShelteredAPI.Networking
{
    internal static class ShelteredMultiplayerTimeSettings
    {
        public const float VanillaDaySeconds = 600f;
        public const float MultiplayerDaySeconds = 384f;
        public const float GameSecondsPerDay = 86400f;
        public const float VanillaDayStartGameSeconds = 21600f;

        public const float CarefulBunkerIntensityMultiplier = 0.85f;
        public const float NormalBunkerIntensityMultiplier = 1f;
        public const float RushBunkerIntensityMultiplier = 1.15f;

        public const float RealtimeTimescale = 1f;
        public const float TimescaleEpsilon = 0.001f;
        public const float DaySecondsEpsilon = 0.001f;
    }
}
