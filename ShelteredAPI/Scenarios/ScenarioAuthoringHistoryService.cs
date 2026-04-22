using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    // Snapshot-based undo/redo for asset-reference edits. The authoring service
    // asks for a snapshot before any mutation; the history service records it and
    // exposes Undo/Redo that restore the SpriteSwaps list on the working definition.
    //
    // Scope is intentionally limited to sprite swaps — extending to other dirty
    // sections is a question of adding more SectionSnapshot kinds here.
    internal sealed class ScenarioAuthoringHistoryService
    {
        private const int MaxDepth = 64;

        private static readonly ScenarioAuthoringHistoryService _instance = new ScenarioAuthoringHistoryService();

        private readonly Stack<SpriteSwapSnapshot> _undo = new Stack<SpriteSwapSnapshot>();
        private readonly Stack<SpriteSwapSnapshot> _redo = new Stack<SpriteSwapSnapshot>();
        private string _boundDraftId;

        public static ScenarioAuthoringHistoryService Instance
        {
            get { return _instance; }
        }

        private ScenarioAuthoringHistoryService()
        {
        }

        public bool CanUndo
        {
            get { return _undo.Count > 0; }
        }

        public bool CanRedo
        {
            get { return _redo.Count > 0; }
        }

        public int UndoDepth
        {
            get { return _undo.Count; }
        }

        public int RedoDepth
        {
            get { return _redo.Count; }
        }

        // Call when the active authoring draft changes so stale snapshots don't leak
        // across sessions.
        public void BindSession(string draftId)
        {
            if (string.Equals(_boundDraftId, draftId, StringComparison.Ordinal))
                return;

            _boundDraftId = draftId;
            _undo.Clear();
            _redo.Clear();
        }

        public void Reset()
        {
            _boundDraftId = null;
            _undo.Clear();
            _redo.Clear();
        }

        // Capture before mutating. A new user action invalidates the redo stack.
        public void RecordSpriteSwapChange(ScenarioDefinition definition, string description)
        {
            if (definition == null)
                return;

            SpriteSwapSnapshot snapshot = new SpriteSwapSnapshot
            {
                Description = description,
                Rules = ScenarioSpriteSwapRuleEditor.SnapshotRules(definition)
            };
            PushUndo(snapshot);
            _redo.Clear();
        }

        public bool Undo(ScenarioDefinition definition, out string description)
        {
            description = null;
            if (definition == null || _undo.Count == 0)
                return false;

            SpriteSwapSnapshot redoPoint = new SpriteSwapSnapshot
            {
                Description = "Redo",
                Rules = ScenarioSpriteSwapRuleEditor.SnapshotRules(definition)
            };

            SpriteSwapSnapshot snapshot = _undo.Pop();
            ScenarioSpriteSwapRuleEditor.RestoreRules(definition, snapshot.Rules);
            _redo.Push(redoPoint);
            description = snapshot.Description;
            MMLog.WriteInfo("[ScenarioAuthoringHistory] Undo: " + (description ?? "<unnamed>")
                + " | undoDepth=" + _undo.Count + " redoDepth=" + _redo.Count);
            return true;
        }

        public bool Redo(ScenarioDefinition definition, out string description)
        {
            description = null;
            if (definition == null || _redo.Count == 0)
                return false;

            SpriteSwapSnapshot undoPoint = new SpriteSwapSnapshot
            {
                Description = "Undo",
                Rules = ScenarioSpriteSwapRuleEditor.SnapshotRules(definition)
            };

            SpriteSwapSnapshot snapshot = _redo.Pop();
            ScenarioSpriteSwapRuleEditor.RestoreRules(definition, snapshot.Rules);
            PushUndo(undoPoint);
            description = snapshot.Description;
            MMLog.WriteInfo("[ScenarioAuthoringHistory] Redo: " + (description ?? "<unnamed>")
                + " | undoDepth=" + _undo.Count + " redoDepth=" + _redo.Count);
            return true;
        }

        private void PushUndo(SpriteSwapSnapshot snapshot)
        {
            _undo.Push(snapshot);
            if (_undo.Count <= MaxDepth)
                return;

            // Trim the oldest entry. Stack<T> has no Dequeue; rebuild.
            SpriteSwapSnapshot[] keep = _undo.ToArray(); // top-first
            _undo.Clear();
            for (int i = keep.Length - 2; i >= 0; i--)
                _undo.Push(keep[i]);
        }

        private sealed class SpriteSwapSnapshot
        {
            public string Description;
            public List<SpriteSwapRule> Rules;
        }
    }
}
