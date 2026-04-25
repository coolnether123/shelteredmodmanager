using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    public sealed class ScenarioApplyResult
    {
        private readonly List<string> _messages = new List<string>();

        public int FamilyChanges { get; set; }
        public int InventoryChanges { get; set; }
        public int BunkerChanges { get; set; }
        public int TriggerChanges { get; set; }
        public int ConditionChanges { get; set; }
        public int SpriteSwapChanges { get; set; }

        public string[] Messages
        {
            get { return _messages.ToArray(); }
        }

        public void AddMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
                _messages.Add(message);
        }
    }
}
