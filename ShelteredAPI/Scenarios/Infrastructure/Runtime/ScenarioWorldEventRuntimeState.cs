using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal static class ScenarioWorldEventRuntimeState
    {
        private static ScenarioDefinition _definition;
        private static int _authoredRadioBroadcastDispatches;

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

        /// <summary>
        /// Lets a scheduled authored radio event use the same vanilla broadcast
        /// method as a survivor interaction while radio-odds suppression is active.
        /// </summary>
        public static bool IsDispatchingAuthoredRadioBroadcast
        {
            get { return _authoredRadioBroadcastDispatches > 0; }
        }

        public static void BeginAuthoredRadioBroadcastDispatch()
        {
            _authoredRadioBroadcastDispatches++;
        }

        public static void EndAuthoredRadioBroadcastDispatch()
        {
            if (_authoredRadioBroadcastDispatches > 0)
                _authoredRadioBroadcastDispatches--;
        }
    }
}
