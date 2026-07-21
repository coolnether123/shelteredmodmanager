using System;

namespace ShelteredModManager.Shared.PixelEditing
{
    /// <summary>
    /// Holds a detached pixel region that can be pasted across editor sessions.
    /// </summary>
    public sealed class PixelClipboard
    {
        private PixelDocument _content;

        public bool HasContent
        {
            get { return _content != null; }
        }

        public int Width
        {
            get { return _content != null ? _content.Width : 0; }
        }

        public int Height
        {
            get { return _content != null ? _content.Height : 0; }
        }

        public void Clear()
        {
            _content = null;
        }

        public bool CopyFrom(PixelDocument source, PixelSelection selection)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            PixelSelection clipped = selection.IsEmpty
                ? new PixelSelection(0, 0, source.Width, source.Height)
                : selection.ClipTo(source.Width, source.Height);
            if (clipped.IsEmpty)
                return false;

            PixelDocument copy = new PixelDocument(clipped.Width, clipped.Height);
            for (int y = 0; y < clipped.Height; y++)
            {
                for (int x = 0; x < clipped.Width; x++)
                    copy.SetPixel(x, y, source.GetPixel(clipped.X + x, clipped.Y + y));
            }

            _content = copy;
            return true;
        }

        public bool PasteInto(PixelDocument destination, int targetX, int targetY)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (_content == null)
                return false;

            bool changed = false;
            for (int y = 0; y < _content.Height; y++)
            {
                for (int x = 0; x < _content.Width; x++)
                {
                    int destinationX = targetX + x;
                    int destinationY = targetY + y;
                    if (!destination.Contains(destinationX, destinationY))
                        continue;

                    Rgba32 color = _content.GetPixel(x, y);
                    if (destination.GetPixel(destinationX, destinationY) == color)
                        continue;

                    destination.SetPixel(destinationX, destinationY, color);
                    changed = true;
                }
            }

            return changed;
        }

        public PixelDocument CopyContent()
        {
            return _content != null ? _content.Clone() : null;
        }
    }
}
