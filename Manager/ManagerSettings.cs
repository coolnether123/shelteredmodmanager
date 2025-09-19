namespace Manager
{
    public static class ManagerSettings
    {
        public static bool SkipHarmonyDependencyCheck { get; internal set; } = false; // Default to false
        // Add other manager-specific settings here as needed
    }
}