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
        private static readonly byte[] Magic = new byte[] { (byte)'S', (byte)'B', (byte)'L', (byte)'Z', 1 };

        internal static void WriteCompressed(string path, byte[] bytes)
        {
            File.WriteAllBytes(path, Compress(bytes ?? new byte[0]));
        }

        internal static byte[] ReadDecompressed(string path)
        {
            return Decompress(File.ReadAllBytes(path));
        }

        internal static byte[] Compress(byte[] input)
        {
            input = input ?? new byte[0];
            List<byte> output = new List<byte>(input.Length + 16);
            output.AddRange(Magic);
            WriteInt32(output, input.Length);

            int position = 0;
            while (position < input.Length)
            {
                int flagsIndex = output.Count;
                byte flags = 0;
                output.Add(0);

                for (int bit = 0; bit < 8 && position < input.Length; bit++)
                {
                    Match match = FindBestMatch(input, position);
                    if (match.Length >= MinMatchLength)
                    {
                        flags |= (byte)(1 << bit);
                        int encoded = (match.Offset << 4) | (match.Length - MinMatchLength);
                        output.Add((byte)(encoded & 0xFF));
                        output.Add((byte)((encoded >> 8) & 0xFF));
                        position += match.Length;
                    }
                    else
                    {
                        output.Add(input[position]);
                        position++;
                    }
                }

                output[flagsIndex] = flags;
            }

            return output.ToArray();
        }

        internal static byte[] Decompress(byte[] input)
        {
            if (input == null || input.Length < Magic.Length + 4)
                throw new InvalidDataException("Compressed backup blob is truncated.");

            for (int i = 0; i < Magic.Length; i++)
            {
                if (input[i] != Magic[i])
                    throw new InvalidDataException("Compressed backup blob has an unsupported header.");
            }

            int outputLength = ReadInt32(input, Magic.Length);
            if (outputLength < 0)
                throw new InvalidDataException("Compressed backup blob has an invalid length.");

            byte[] output = new byte[outputLength];
            int inputPosition = Magic.Length + 4;
            int outputPosition = 0;

            while (outputPosition < outputLength)
            {
                if (inputPosition >= input.Length)
                    throw new InvalidDataException("Compressed backup blob ended inside a token group.");

                byte flags = input[inputPosition++];
                for (int bit = 0; bit < 8 && outputPosition < outputLength; bit++)
                {
                    if ((flags & (1 << bit)) == 0)
                    {
                        if (inputPosition >= input.Length)
                            throw new InvalidDataException("Compressed backup blob ended inside a literal.");

                        output[outputPosition++] = input[inputPosition++];
                        continue;
                    }

                    if (inputPosition + 1 >= input.Length)
                        throw new InvalidDataException("Compressed backup blob ended inside a match.");

                    int encoded = input[inputPosition] | (input[inputPosition + 1] << 8);
                    inputPosition += 2;

                    int offset = encoded >> 4;
                    int length = (encoded & 0x0F) + MinMatchLength;
                    if (offset <= 0 || offset > outputPosition)
                        throw new InvalidDataException("Compressed backup blob contains an invalid match offset.");

                    for (int i = 0; i < length && outputPosition < outputLength; i++)
                    {
                        output[outputPosition] = output[outputPosition - offset];
                        outputPosition++;
                    }
                }
            }

            return output;
        }

        private static Match FindBestMatch(byte[] input, int position)
        {
            Match best = new Match();
            int searchStart = Math.Max(0, position - WindowSize);

            for (int candidate = searchStart; candidate < position; candidate++)
            {
                int length = 0;
                while (length < MaxMatchLength
                    && position + length < input.Length
                    && input[candidate + length] == input[position + length])
                {
                    length++;
                }

                if (length > best.Length && length >= MinMatchLength)
                {
                    best.Length = length;
                    best.Offset = position - candidate;
                    if (length == MaxMatchLength)
                        break;
                }
            }

            return best;
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
