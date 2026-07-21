using System;
using System.Collections.Generic;
using System.IO;

namespace ShelteredAPI.Saves.Backups
{
    internal static class SaveBackupBlobCodec
    {
        internal const string CompressionName = "sheltered-lzss-v1";
        private const int WindowSize = 4095;
        private const int MinMatchLength = 3;
        private const int MaxMatchLength = 18;
        private const int MaxExpansionPerPayloadByte = MaxMatchLength / 2;
        private const int MaxMatchCandidates = 64;
        private const int HashSize = 65536;
        private const int MaxDecompressedSize = 128 * 1024 * 1024;
        private static readonly byte[] Magic = new byte[] { (byte)'S', (byte)'B', (byte)'L', (byte)'Z', 1 };

        internal static void WriteCompressed(string path, byte[] bytes)
        {
            File.WriteAllBytes(path, Compress(bytes ?? new byte[0]));
        }

        internal static byte[] ReadDecompressed(string path)
        {
            if (new FileInfo(path).Length > GetMaxEncodedLength(MaxDecompressedSize))
                throw new IOException("Compressed backup blob exceeds the supported size limit.");

            return Decompress(File.ReadAllBytes(path));
        }

        internal static byte[] Compress(byte[] input)
        {
            input = input ?? new byte[0];
            ValidateDecompressedSize(input.Length);

            List<byte> output = new List<byte>(GetMaxEncodedLength(input.Length));
            output.AddRange(Magic);
            WriteInt32(output, input.Length);

            MatchFinder matchFinder = new MatchFinder(input);
            int position = 0;
            while (position < input.Length)
            {
                int flagsIndex = output.Count;
                byte flags = 0;
                output.Add(0);

                for (int bit = 0; bit < 8 && position < input.Length; bit++)
                {
                    Match match = matchFinder.FindBestMatch(position);
                    int consumed;
                    if (match.Length >= MinMatchLength)
                    {
                        flags |= (byte)(1 << bit);
                        int encoded = (match.Offset << 4) | (match.Length - MinMatchLength);
                        output.Add((byte)(encoded & 0xFF));
                        output.Add((byte)((encoded >> 8) & 0xFF));
                        consumed = match.Length;
                    }
                    else
                    {
                        output.Add(input[position]);
                        consumed = 1;
                    }

                    matchFinder.AddPositions(position, consumed);
                    position += consumed;
                }

                output[flagsIndex] = flags;
            }

            return output.ToArray();
        }

        internal static byte[] Decompress(byte[] input)
        {
            if (input == null || input.Length < Magic.Length + 4)
                throw new IOException("Compressed backup blob is truncated.");
            if (input.Length > GetMaxEncodedLength(MaxDecompressedSize))
                throw new IOException("Compressed backup blob exceeds the supported size limit.");

            for (int i = 0; i < Magic.Length; i++)
            {
                if (input[i] != Magic[i])
                    throw new IOException("Compressed backup blob has an unsupported header.");
            }

            int outputLength = ReadInt32(input, Magic.Length);
            if (outputLength < 0)
                throw new IOException("Compressed backup blob has an invalid length.");
            ValidateDecompressedSize(outputLength);

            int payloadLength = input.Length - (Magic.Length + 4);
            if ((long)outputLength > (long)payloadLength * MaxExpansionPerPayloadByte)
                throw new IOException("Compressed backup blob cannot produce its declared length.");

            byte[] output = new byte[outputLength];
            int inputPosition = Magic.Length + 4;
            int outputPosition = 0;

            while (outputPosition < outputLength)
            {
                if (inputPosition >= input.Length)
                    throw new IOException("Compressed backup blob ended inside a token group.");

                byte flags = input[inputPosition++];
                int usedTokenCount = 0;
                for (int bit = 0; bit < 8 && outputPosition < outputLength; bit++)
                {
                    usedTokenCount++;
                    if ((flags & (1 << bit)) == 0)
                    {
                        if (inputPosition >= input.Length)
                            throw new IOException("Compressed backup blob ended inside a literal.");

                        output[outputPosition++] = input[inputPosition++];
                        continue;
                    }

                    if (inputPosition + 1 >= input.Length)
                        throw new IOException("Compressed backup blob ended inside a match.");

                    int encoded = input[inputPosition] | (input[inputPosition + 1] << 8);
                    inputPosition += 2;

                    int offset = encoded >> 4;
                    int length = (encoded & 0x0F) + MinMatchLength;
                    if (offset <= 0 || offset > outputPosition)
                        throw new IOException("Compressed backup blob contains an invalid match offset.");
                    if (length > outputLength - outputPosition)
                        throw new IOException("Compressed backup blob contains a match beyond its declared length.");

                    for (int i = 0; i < length; i++)
                    {
                        output[outputPosition] = output[outputPosition - offset];
                        outputPosition++;
                    }
                }

                if (outputPosition == outputLength && usedTokenCount < 8)
                {
                    int unusedMatchFlags = flags & (0xFF << usedTokenCount);
                    if (unusedMatchFlags != 0)
                        throw new IOException("Compressed backup blob has invalid flags beyond its declared length.");
                }
            }

            if (inputPosition != input.Length)
                throw new IOException("Compressed backup blob contains trailing data.");

            return output;
        }

        private static int GetMaxEncodedLength(int outputLength)
        {
            return Magic.Length + 4 + outputLength + ((outputLength + 7) / 8);
        }

        private static void ValidateDecompressedSize(int length)
        {
            if (length > MaxDecompressedSize)
                throw new IOException("Backup blob decompressed length exceeds the supported size limit.");
        }

        private sealed class MatchFinder
        {
            private readonly byte[] _input;
            private readonly int[] _heads;
            private readonly int[] _previous;
            private readonly int[] _positions;

            internal MatchFinder(byte[] input)
            {
                _input = input;
                _heads = CreateMissingArray(HashSize);
                _previous = CreateMissingArray(WindowSize);
                _positions = CreateMissingArray(WindowSize);
            }

            internal Match FindBestMatch(int position)
            {
                Match best = new Match();
                if (position + MinMatchLength > _input.Length)
                    return best;

                int candidate = _heads[GetHash(_input, position)];
                int earliestCandidate = Math.Max(0, position - WindowSize);
                int candidatesChecked = 0;

                while (candidate >= earliestCandidate && candidatesChecked < MaxMatchCandidates)
                {
                    int slot = candidate % WindowSize;
                    if (_positions[slot] != candidate)
                        break;

                    int length = 0;
                    while (length < MaxMatchLength
                        && position + length < _input.Length
                        && _input[candidate + length] == _input[position + length])
                    {
                        length++;
                    }

                    if (length >= MinMatchLength && length >= best.Length)
                    {
                        best.Length = length;
                        best.Offset = position - candidate;
                    }

                    candidate = _previous[slot];
                    candidatesChecked++;
                }

                return best;
            }

            internal void AddPositions(int position, int count)
            {
                int end = position + count;
                for (int current = position; current < end; current++)
                {
                    if (current + MinMatchLength > _input.Length)
                        return;

                    int hash = GetHash(_input, current);
                    int slot = current % WindowSize;
                    _positions[slot] = current;
                    _previous[slot] = _heads[hash];
                    _heads[hash] = current;
                }
            }
        }

        private static int[] CreateMissingArray(int length)
        {
            int[] values = new int[length];
            for (int i = 0; i < values.Length; i++)
                values[i] = -1;

            return values;
        }

        private static int GetHash(byte[] input, int position)
        {
            int hash = input[position];
            hash = ((hash << 5) - hash) ^ input[position + 1];
            hash = ((hash << 5) - hash) ^ input[position + 2];
            return hash & (HashSize - 1);
        }

        private static void WriteInt32(List<byte> output, int value)
        {
            output.Add((byte)(value & 0xFF));
            output.Add((byte)((value >> 8) & 0xFF));
            output.Add((byte)((value >> 16) & 0xFF));
            output.Add((byte)((value >> 24) & 0xFF));
        }

        private static int ReadInt32(byte[] input, int offset)
        {
            return input[offset]
                | (input[offset + 1] << 8)
                | (input[offset + 2] << 16)
                | (input[offset + 3] << 24);
        }

        private struct Match
        {
            public int Offset;
            public int Length;
        }
    }
}
