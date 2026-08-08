using System;

namespace ShelteredModManager.Shared.PixelEditing
{
    /// <summary>
    /// Coordinates document editing state while leaving rendering, codecs, and persistence to hosts.
    /// </summary>
    internal sealed class PixelEditorSession
    {
        private PixelDocument _document;
        private PixelDocument _savedDocument;
        private readonly PixelClipboard _clipboard;
        private readonly PixelEditHistory _history;
        private PixelSelection _selection;
        private PixelEditorTool _activeTool;
        private Rgba32 _activeColor;
        private bool _dirty;
        private bool _strokeActive;
        private bool _strokeChanged;
        private PixelDocument _strokeBaseline;

        public PixelEditorSession(PixelDocument document, int historyCapacity)
            : this(document, historyCapacity, new PixelClipboard())
        {
        }

        public PixelEditorSession(PixelDocument document, int historyCapacity, PixelClipboard clipboard)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (clipboard == null)
                throw new ArgumentNullException("clipboard");

            _document = document.Clone();
            _savedDocument = document.Clone();
            _clipboard = clipboard;
            _history = new PixelEditHistory(historyCapacity);
            _selection = PixelSelection.Empty;
            _activeTool = PixelEditorTool.Paint;
            _activeColor = new Rgba32(0, 0, 0, 255);
        }

        public PixelDocument Document
        {
            get { return _document; }
        }

        public PixelClipboard Clipboard
        {
            get { return _clipboard; }
        }

        public PixelEditHistory History
        {
            get { return _history; }
        }

        public PixelSelection Selection
        {
            get { return _selection; }
        }

        public PixelEditorTool ActiveTool
        {
            get { return _activeTool; }
            set { _activeTool = value; }
        }

        public Rgba32 ActiveColor
        {
            get { return _activeColor; }
            set { _activeColor = value; }
        }

        public bool Dirty
        {
            get { return _dirty; }
        }

        public void BeginStroke()
        {
            if (_strokeActive)
                return;

            _strokeActive = true;
            _strokeChanged = false;
            _strokeBaseline = _document.Clone();
        }

        public bool PaintPixel(int x, int y)
        {
            Rgba32 color = _activeTool == PixelEditorTool.Erase
                ? Rgba32.Transparent
                : _activeColor;
            return SetPixel(x, y, color);
        }

        public bool SetPixel(int x, int y, Rgba32 color)
        {
            if (!_document.Contains(x, y) || _document.GetPixel(x, y) == color)
                return false;

            if (!_strokeActive)
                _history.RecordBeforeChange(_document);

            _document.SetPixel(x, y, color);
            UpdateDirty();
            if (_strokeActive)
                _strokeChanged = true;
            return true;
        }

        public void EndStroke()
        {
            if (!_strokeActive)
                return;

            _strokeActive = false;
            if (_strokeChanged
                && _strokeBaseline != null
                && !_document.HasSamePixels(_strokeBaseline))
            {
                _history.RecordBeforeChange(_strokeBaseline);
            }

            _strokeChanged = false;
            _strokeBaseline = null;
        }

        public bool PickColor(int x, int y)
        {
            Rgba32 color;
            if (!_document.TryGetPixel(x, y, out color))
                return false;

            _activeColor = color;
            return true;
        }

        public void SetSelection(PixelSelection selection)
        {
            _selection = selection.ClipTo(_document.Width, _document.Height);
        }

        public void ClearSelection()
        {
            _selection = PixelSelection.Empty;
        }

        public bool CopySelection()
        {
            return _clipboard.CopyFrom(_document, _selection);
        }

        public bool Paste(int targetX, int targetY)
        {
            if (!_clipboard.HasContent)
                return false;

            PixelDocument beforePaste = _document.Clone();
            if (!_clipboard.PasteInto(_document, targetX, targetY))
                return false;

            _history.RecordBeforeChange(beforePaste);
            UpdateDirty();
            _selection = new PixelSelection(
                Math.Max(0, targetX),
                Math.Max(0, targetY),
                _clipboard.Width,
                _clipboard.Height).ClipTo(_document.Width, _document.Height);
            return true;
        }

        public bool Undo()
        {
            EndStroke();
            PixelDocument restored;
            if (!_history.TryUndo(_document, out restored))
                return false;

            _document = restored;
            UpdateDirty();
            _selection = _selection.ClipTo(_document.Width, _document.Height);
            return true;
        }

        public bool Redo()
        {
            EndStroke();
            PixelDocument restored;
            if (!_history.TryRedo(_document, out restored))
                return false;

            _document = restored;
            UpdateDirty();
            _selection = _selection.ClipTo(_document.Width, _document.Height);
            return true;
        }

        public void MarkSaved()
        {
            _savedDocument = _document.Clone();
            _dirty = false;
        }

        public PixelEditorViewModel CreateViewModel()
        {
            return new PixelEditorViewModel(
                _document.Clone(),
                _activeTool,
                _activeColor,
                _selection,
                _clipboard.HasContent,
                _clipboard.Width,
                _clipboard.Height,
                _dirty,
                _history.CanUndo,
                _history.CanRedo);
        }

        private void UpdateDirty()
        {
            _dirty = _savedDocument == null || !_document.HasSamePixels(_savedDocument);
        }

    }
}
