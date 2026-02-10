using System;
using System.IO;
using System.Text;

namespace ModAPI.Decompiler
{
    /// <summary>
    /// Writes MethodArtifact data in MODT binary v2 format.
    /// This is optimized for fast runtime loading by ModAPI.
    /// </summary>
    public sealed class BinarySerializer
    {
        private const ushort FormatVersion = 2;

        /// <summary>
        /// Serializes one method artifact into a .modtrace payload.
        /// </summary>
        public void Write(string outputPath, MethodArtifact artifact)
        {
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var bw = new BinaryWriter(fs, Encoding.UTF8))
            {
                WriteHeader(bw, artifact.TimestampTicksUtc);
                WriteMethodTable(bw, artifact);
                WriteSourceMap(bw, artifact);
                WriteVariableTable(bw, artifact);
            }
        }

        private static void WriteHeader(BinaryWriter bw, long timestampTicksUtc)
        {
            bw.Write(Encoding.ASCII.GetBytes("MODT"));
            bw.Write(FormatVersion);
            bw.Write(timestampTicksUtc);
        }

        private static void WriteMethodTable(BinaryWriter bw, MethodArtifact artifact)
        {
            bw.Write(1); // MethodCount
            bw.Write(artifact.MetadataToken);

            var nameBytes = Encoding.UTF8.GetBytes(artifact.MethodName ?? string.Empty);
            if (nameBytes.Length > short.MaxValue)
            {
                throw new InvalidOperationException("Method name is too long for MODT format.");
            }

            bw.Write((short)nameBytes.Length);
            bw.Write(nameBytes);

            var il = artifact.ILBytes ?? Array.Empty<byte>();
            bw.Write(il.Length);
            bw.Write(il);
        }

        private static void WriteSourceMap(BinaryWriter bw, MethodArtifact artifact)
        {
            bw.Write(artifact.SourceMap.Count);
            for (var i = 0; i < artifact.SourceMap.Count; i++)
            {
                var entry = artifact.SourceMap[i];
                bw.Write(entry.SourceLineNumber);
                bw.Write(entry.ILOffset);
                bw.Write(entry.InstructionCount);
            }
        }

        private static void WriteVariableTable(BinaryWriter bw, MethodArtifact artifact)
        {
            bw.Write(artifact.Variables.Count);
            for (var i = 0; i < artifact.Variables.Count; i++)
            {
                var v = artifact.Variables[i];
                bw.Write(v.Name ?? string.Empty);
                bw.Write(v.Type ?? string.Empty);
                bw.Write(v.IsLocal);
                bw.Write(v.ILIndex);
            }
        }
    }
}
