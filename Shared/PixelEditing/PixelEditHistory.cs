using System;
using System.Collections.Generic;

namespace ShelteredModManager.Shared.PixelEditing
{
    /// <summary>
    /// Bounded document snapshot history. Call RecordBeforeChange once per logical edit.
    /// </summary>
    public sealed class PixelEditHistory
    {
        private readonly int _capacity;
        private readonly List<PixelDocument> _undo = new List<PixelDocument>();
        private readonly List<PixelDocument> _redo = new List<PixelDocument>();

        public PixelEditHistory(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException("capacity", "History capacity must be positive.");
            _capacity = capacity;
        }

        public int Capacity { get { return _capacity; } }
        public int UndoCount { get { return _undo.Count; } }
        public int RedoCount { get { return _redo.Count; } }
        public bool CanUndo { get { return _undo.Count > 0; } }
        public bool CanRedo { get { return _redo.Count > 0; } }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        public void RecordBeforeChange(PixelDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            AddBounded(_undo, document.Clone());
            _redo.Clear();
        }

        public bool TryUndo(PixelDocument current, out PixelDocument restored)
        {
            if (current == null)
                throw new ArgumentNullException("current");
            if (_undo.Count == 0)
            {
                restored = null;
                return false;
            }

            AddBounded(_redo, current.Clone());
            restored = TakeLast(_undo);
            return true;
        }

        public bool TryRedo(PixelDocument current, out PixelDocument restored)
        {
            if (current == null)
                throw new ArgumentNullException("current");
            if (_redo.Count == 0)
            {
                restored = null;
                return false;
            }

            AddBounded(_undo, current.Clone());
            restored = TakeLast(_redo);
            return true;
        }

        private void AddBounded(List<PixelDocument> destination, PixelDocument snapshot)
        {
            destination.Add(snapshot);
            if (destination.Count > _capacity)
                destination.RemoveAt(0);
        }

        private static PixelDocument TakeLast(List<PixelDocument> source)
        {
            int index = source.Count - 1;
            PixelDocument document = source[index];
            source.RemoveAt(index);
            return document;
        }
    }
}
