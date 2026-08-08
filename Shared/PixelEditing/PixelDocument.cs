using System;

namespace ShelteredModManager.Shared.PixelEditing
{
    /// <summary>
    /// Owns a rectangular, row-major RGBA pixel buffer without UI or image-codec dependencies.
    /// Pixel (0, 0) is the first four bytes in the buffer; hosts decide how that maps to screen space.
    /// </summary>
    internal sealed class PixelDocument
    {
        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _rgbaBytes;

        public PixelDocument(int width, int height)
            : this(width, height, null)
        {
        }

        public PixelDocument(int width, int height, byte[] rgbaBytes)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException("width", "Pixel document width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException("height", "Pixel document height must be positive.");

            long requiredLength = (long)width * height * 4L;
            if (requiredLength > int.MaxValue)
                throw new ArgumentOutOfRangeException("width", "Pixel document dimensions are too large.");
            if (rgbaBytes != null && rgbaBytes.Length != (int)requiredLength)
                throw new ArgumentException("RGBA buffer length must equal width * height * 4.", "rgbaBytes");

            _width = width;
            _height = height;
            _rgbaBytes = new byte[(int)requiredLength];
            if (rgbaBytes != null)
                Buffer.BlockCopy(rgbaBytes, 0, _rgbaBytes, 0, rgbaBytes.Length);
        }

        public int Width
        {
            get { return _width; }
        }

        public int Height
        {
            get { return _height; }
        }

        public bool Contains(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _width && y < _height;
        }

        public Rgba32 GetPixel(int x, int y)
        {
            EnsureInBounds(x, y);
            int offset = OffsetOf(x, y);
            return new Rgba32(
                _rgbaBytes[offset],
                _rgbaBytes[offset + 1],
                _rgbaBytes[offset + 2],
                _rgbaBytes[offset + 3]);
        }

        public bool TryGetPixel(int x, int y, out Rgba32 color)
        {
            if (!Contains(x, y))
            {
                color = Rgba32.Transparent;
                return false;
            }

            color = GetPixel(x, y);
            return true;
        }

        public void SetPixel(int x, int y, Rgba32 color)
        {
            EnsureInBounds(x, y);
            int offset = OffsetOf(x, y);
            _rgbaBytes[offset] = color.R;
            _rgbaBytes[offset + 1] = color.G;
            _rgbaBytes[offset + 2] = color.B;
            _rgbaBytes[offset + 3] = color.A;
        }

        public bool TrySetPixel(int x, int y, Rgba32 color)
        {
            if (!Contains(x, y))
                return false;

            SetPixel(x, y, color);
            return true;
        }

        public void Fill(Rgba32 color)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                    SetPixel(x, y, color);
            }
        }

        public byte[] CopyRgbaBytes()
        {
            byte[] copy = new byte[_rgbaBytes.Length];
            Buffer.BlockCopy(_rgbaBytes, 0, copy, 0, copy.Length);
            return copy;
        }

        public PixelDocument Clone()
        {
            return new PixelDocument(_width, _height, _rgbaBytes);
        }

        public bool HasSamePixels(PixelDocument other)
        {
            if (other == null || other.Width != _width || other.Height != _height)
                return false;

            byte[] otherBytes = other._rgbaBytes;
            for (int i = 0; i < _rgbaBytes.Length; i++)
            {
                if (_rgbaBytes[i] != otherBytes[i])
                    return false;
            }

            return true;
        }

        private int OffsetOf(int x, int y)
        {
            return ((y * _width) + x) * 4;
        }

        private void EnsureInBounds(int x, int y)
        {
            if (!Contains(x, y))
                throw new ArgumentOutOfRangeException("x", "Pixel coordinate is outside the document.");
        }
    }
}
