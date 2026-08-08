using System;

namespace ShelteredModManager.Shared.PixelEditing
{
    /// <summary>
    /// Immutable rectangular pixel selection.
    /// </summary>
    internal struct PixelSelection : IEquatable<PixelSelection>
    {
        private readonly int _x;
        private readonly int _y;
        private readonly int _width;
        private readonly int _height;

        public PixelSelection(int x, int y, int width, int height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException("width");
            if (height < 0)
                throw new ArgumentOutOfRangeException("height");

            _x = x;
            _y = y;
            _width = width;
            _height = height;
        }

        public static PixelSelection Empty
        {
            get { return new PixelSelection(0, 0, 0, 0); }
        }

        public int X { get { return _x; } }
        public int Y { get { return _y; } }
        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public bool IsEmpty { get { return _width == 0 || _height == 0; } }

        public static PixelSelection FromCorners(int startX, int startY, int endX, int endY)
        {
            int left = Math.Min(startX, endX);
            int top = Math.Min(startY, endY);
            int right = Math.Max(startX, endX);
            int bottom = Math.Max(startY, endY);
            return new PixelSelection(left, top, (right - left) + 1, (bottom - top) + 1);
        }

        public PixelSelection ClipTo(int documentWidth, int documentHeight)
        {
            if (documentWidth <= 0 || documentHeight <= 0 || IsEmpty)
                return Empty;

            int left = Math.Max(0, _x);
            int top = Math.Max(0, _y);
            long rawRight = (long)_x + _width;
            long rawBottom = (long)_y + _height;
            int right = (int)Math.Min(documentWidth, Math.Max(0L, rawRight));
            int bottom = (int)Math.Min(documentHeight, Math.Max(0L, rawBottom));
            if (right <= left || bottom <= top)
                return Empty;

            return new PixelSelection(left, top, right - left, bottom - top);
        }

        public bool Contains(int x, int y)
        {
            return !IsEmpty
                && x >= _x
                && y >= _y
                && (long)x < (long)_x + _width
                && (long)y < (long)_y + _height;
        }

        public bool Equals(PixelSelection other)
        {
            return _x == other._x
                && _y == other._y
                && _width == other._width
                && _height == other._height;
        }

        public override bool Equals(object obj)
        {
            return obj is PixelSelection && Equals((PixelSelection)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _x;
                hash = (hash * 397) ^ _y;
                hash = (hash * 397) ^ _width;
                hash = (hash * 397) ^ _height;
                return hash;
            }
        }
    }
}
