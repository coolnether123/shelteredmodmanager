using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal static class ScenarioWorldEventRuntimeState
    {
        private static ScenarioDefinition _definition;

        public static void Bind(ScenarioDefinition definition)
        {
            _definition = definition;
        }

        public static ScenarioVanillaSuppressionDefinition Suppression
        {
            get { return _definition != null ? _definition.VanillaSuppression : null; }
        }

        public static bool SuppressRandomVisitors
        {
            get { return Suppression != null && Suppression.RandomVisitors; }
        }

        public static bool SuppressBinman
        {
            get { return Suppression != null && Suppression.Binman; }
        }

        public static bool SuppressRaids
        {
            get { return Suppression != null && Suppression.Raids; }
        }

        public static bool SuppressStasisVisitors
        {
            get { return Suppression != null && Suppression.StasisVisitors; }
        }

        public static bool SuppressRadioBroadcastOdds
        {
            get { return Suppression != null && Suppression.RadioBroadcastOdds; }
        }
    }
}
