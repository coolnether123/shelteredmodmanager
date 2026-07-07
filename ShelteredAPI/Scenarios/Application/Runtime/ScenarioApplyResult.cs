using System.Collections.Generic;

using ShelteredAPI.Content;
namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal sealed class ScenarioApplyResult
    {
        private readonly List<string> _messages = new List<string>();

        public int FamilyChanges { get; set; }
        public int InventoryChanges { get; set; }
        public int BunkerChanges { get; set; }
        public int TriggerChanges { get; set; }
        public int ConditionChanges { get; set; }
        public int SpriteSwapChanges { get; set; }
        public int MapChanges { get; set; }

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
