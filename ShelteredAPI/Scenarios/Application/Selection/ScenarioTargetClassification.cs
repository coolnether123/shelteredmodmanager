namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioTargetClassification
    {
        public ScenarioTargetScope PrimaryScope { get; set; }
        public ScenarioTargetScope[] SecondaryScopes { get; set; }
        public float Confidence { get; set; }
        public string Reason { get; set; }
        public string Source { get; set; }

        public bool Matches(ScenarioTargetScope scope)
        {
            if (scope == ScenarioTargetScope.Unknown)
                return false;

            if (PrimaryScope == scope)
                return true;

            for (int i = 0; SecondaryScopes != null && i < SecondaryScopes.Length; i++)
            {
                if (SecondaryScopes[i] == scope)
                    return true;
            }

            return false;
        }
    }
}
