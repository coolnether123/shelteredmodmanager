using System;

namespace ShelteredModManager.Shared.PixelEditing
{
    /// <summary>
    /// Framework-neutral 8-bit RGBA color used by the shared pixel editor.
    /// </summary>
    internal struct Rgba32 : IEquatable<Rgba32>
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public readonly byte A;

        public Rgba32(byte red, byte green, byte blue, byte alpha)
        {
            R = red;
            G = green;
            B = blue;
            A = alpha;
        }

        public static Rgba32 Transparent
        {
            get { return new Rgba32(0, 0, 0, 0); }
        }

        public bool Equals(Rgba32 other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object obj)
        {
            return obj is Rgba32 && Equals((Rgba32)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = R;
                hash = (hash * 397) ^ G;
                hash = (hash * 397) ^ B;
                hash = (hash * 397) ^ A;
                return hash;
            }
        }

        public static bool operator ==(Rgba32 left, Rgba32 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Rgba32 left, Rgba32 right)
        {
            return !left.Equals(right);
        }
    }
}
