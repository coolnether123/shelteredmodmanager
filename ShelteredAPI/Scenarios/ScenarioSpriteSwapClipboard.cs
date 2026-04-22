using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    // Thread-confined clipboard (editor runs on main thread) holding the last rule the
    // user explicitly copied. Kept separate from history so paste survives undo/redo.
    internal static class ScenarioSpriteSwapClipboard
    {
        private static SpriteSwapRule _rule;
        private static string _sourceDisplay;

        public static bool HasRule
        {
            get { return _rule != null; }
        }

        public static string SourceDisplay
        {
            get { return _sourceDisplay; }
        }

        public static void Copy(SpriteSwapRule rule, string sourceDisplay)
        {
            SpriteSwapRule clone = ScenarioSpriteSwapRuleEditor.CloneRule(rule);
            if (clone == null)
                return;

            // Detach origin so the paste target path will drive placement.
            clone.TargetPath = null;
            _rule = clone;
            _sourceDisplay = string.IsNullOrEmpty(sourceDisplay) ? "<copied>" : sourceDisplay;
        }

        public static SpriteSwapRule Peek()
        {
            return _rule;
        }

        public static SpriteSwapRule TakeClone()
        {
            return ScenarioSpriteSwapRuleEditor.CloneRule(_rule);
        }

        public static string Describe()
        {
            if (_rule == null)
                return "Clipboard is empty.";
            return "Copied: " + ScenarioSpriteSwapRuleEditor.DescribeRuleShort(_rule)
                + " (from " + (_sourceDisplay ?? "<unknown>") + ")";
        }

        public static void Clear()
        {
            _rule = null;
            _sourceDisplay = null;
        }
    }
}
