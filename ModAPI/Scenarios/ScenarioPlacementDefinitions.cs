namespace ModAPI.Scenarios
{
    public enum ScenarioPlacementDefinitionKind
    {
        None = 0,
        Room = 1,
        Ladder = 2,
        RoomLight = 3
    }

    public static class ScenarioPlacementDefinitions
    {
        public const string Room = "Room";
        public const string RoomTop = "RoomTop";
        public const string Ladder = "Ladder";
        public const string RoomLight = "RoomLight";

        public const string PropertyLevel = "level";
        public const string PropertyLockDeconstruct = "lockDeconstruct";
        public const string PropertyMovable = "movable";
        public const string PropertySourceObjectId = "sourceObjectId";
        public const string PropertyCapturedName = "capturedName";
        public const string PropertyGridX = "gridX";
        public const string PropertyGridY = "gridY";
        public const string PropertyHorizontalPos = "horizontalPos";
        public const string PropertyAuthoringIdentity = "authoringIdentity";

        public static bool IsSpecialDefinition(string definitionReference)
        {
            ScenarioPlacementDefinitionKind kind;
            return TryParseSpecialKind(definitionReference, out kind);
        }

        public static bool TryParseSpecialKind(string definitionReference, out ScenarioPlacementDefinitionKind kind)
        {
            kind = ScenarioPlacementDefinitionKind.None;
            string value = TrimToNull(definitionReference);
            if (value == null)
                return false;

            if (string.Equals(value, Room, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, RoomTop, System.StringComparison.OrdinalIgnoreCase))
            {
                kind = ScenarioPlacementDefinitionKind.Room;
                return true;
            }

            if (string.Equals(value, Ladder, System.StringComparison.OrdinalIgnoreCase))
            {
                kind = ScenarioPlacementDefinitionKind.Ladder;
                return true;
            }

            if (string.Equals(value, RoomLight, System.StringComparison.OrdinalIgnoreCase))
            {
                kind = ScenarioPlacementDefinitionKind.RoomLight;
                return true;
            }

            return false;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
