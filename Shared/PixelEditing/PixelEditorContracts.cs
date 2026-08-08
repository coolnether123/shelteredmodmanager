namespace ShelteredModManager.Shared.PixelEditing
{
    internal enum PixelEditorTool
    {
        Paint = 0,
        Erase = 1,
        Pick = 2,
        Select = 3
    }

    /// <summary>
    /// Host-provided codec. The shared editor does not depend on Unity or System.Drawing.
    /// </summary>
    internal interface IPixelImageCodec
    {
        bool TryDecode(byte[] encodedImage, out PixelDocument document, out string error);
        bool TryEncodePng(PixelDocument document, out byte[] encodedPng, out string error);
    }

    /// <summary>
    /// Host-specific persistence destination, such as a scenario patch or full item-icon PNG.
    /// </summary>
    internal interface IPixelEditorDestination
    {
        bool TrySave(
            PixelDocument document,
            string assetId,
            out PixelEditorSaveResult result,
            out string error);
    }

    internal sealed class PixelEditorSaveResult
    {
        public PixelEditorSaveResult(string assetId, string relativePath)
        {
            AssetId = assetId;
            RelativePath = relativePath;
        }

        public string AssetId { get; private set; }
        public string RelativePath { get; private set; }
    }

    /// <summary>
    /// Detached read model suitable for either an IMGUI or WinForms renderer.
    /// </summary>
    internal sealed class PixelEditorViewModel
    {
        internal PixelEditorViewModel(
            PixelDocument document,
            PixelEditorTool activeTool,
            Rgba32 activeColor,
            PixelSelection selection,
            bool hasClipboard,
            int clipboardWidth,
            int clipboardHeight,
            bool dirty,
            bool canUndo,
            bool canRedo)
        {
            Document = document;
            ActiveTool = activeTool;
            ActiveColor = activeColor;
            Selection = selection;
            HasClipboard = hasClipboard;
            ClipboardWidth = clipboardWidth;
            ClipboardHeight = clipboardHeight;
            Dirty = dirty;
            CanUndo = canUndo;
            CanRedo = canRedo;
        }

        public PixelDocument Document { get; private set; }
        public PixelEditorTool ActiveTool { get; private set; }
        public Rgba32 ActiveColor { get; private set; }
        public PixelSelection Selection { get; private set; }
        public bool HasClipboard { get; private set; }
        public int ClipboardWidth { get; private set; }
        public int ClipboardHeight { get; private set; }
        public bool Dirty { get; private set; }
        public bool CanUndo { get; private set; }
        public bool CanRedo { get; private set; }
    }
}
