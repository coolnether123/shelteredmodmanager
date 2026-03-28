using System;
using Microsoft.CodeAnalysis.Text;

namespace Cortex.Roslyn.Worker
{
    internal struct DocumentTextFingerprint : IEquatable<DocumentTextFingerprint>
    {
        public readonly bool HasValue;
        public readonly int Length;
        public readonly ulong Hash;

        private DocumentTextFingerprint(bool hasValue, int length, ulong hash)
        {
            HasValue = hasValue;
            Length = length;
            Hash = hash;
        }

        public static DocumentTextFingerprint From(string text)
        {
            if (text == null)
            {
                return default(DocumentTextFingerprint);
            }

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            for (var i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }

            return new DocumentTextFingerprint(true, text.Length, hash);
        }

        public static DocumentTextFingerprint From(SourceText text)
        {
            return text == null ? default(DocumentTextFingerprint) : From(text.ToString());
        }

        public bool Equals(DocumentTextFingerprint other)
        {
            return HasValue == other.HasValue &&
                Length == other.Length &&
                Hash == other.Hash;
        }

        public override bool Equals(object obj)
        {
            return obj is DocumentTextFingerprint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = HasValue ? 1 : 0;
                hashCode = (hashCode * 397) ^ Length;
                hashCode = (hashCode * 397) ^ Hash.GetHashCode();
                return hashCode;
            }
        }
    }
}
