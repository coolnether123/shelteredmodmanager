using System;
using System.Collections.Generic;
using System.Text;

namespace ModAPI.Decompiler
{
    /// <summary>
    /// CLI output modes for generated artifacts.
    /// </summary>
    public enum OutputFormat
    {
        Source,
        Json,
        Binary
    }

    /// <summary>
    /// Privacy visibility levels used by ModPrivacy policy.
    /// </summary>
    public enum PrivacyLevel
    {
        Public = 0,
        Obfuscated = 1,
        Private = 2
    }

    /// <summary>
    /// Effective privacy resolution output.
    /// </summary>
    public sealed class PrivacyDecision
    {
        public PrivacyLevel Level { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Human-readable privacy probe response used by --privacy-check.
    /// </summary>
    public sealed class PrivacyCheckResult
    {
        public PrivacyLevel Level { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string MethodSignature { get; set; } = string.Empty;
    }

    /// <summary>
    /// Line-level source mapping entry.
    /// </summary>
    public sealed class SourceMapEntry
    {
        public int SourceLineNumber { get; set; }
        public int ILOffset { get; set; }
        public short InstructionCount { get; set; }
    }

    /// <summary>
    /// Variable table entry for parameters, fields, or locals.
    /// </summary>
    public sealed class VariableEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsLocal { get; set; }
        public int ILIndex { get; set; }
    }

    /// <summary>
    /// Canonical transfer model for a single decompiled method.
    /// </summary>
    public sealed class MethodArtifact
    {
        public int MetadataToken { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string MethodSignature { get; set; } = string.Empty;
        public string SourceCode { get; set; } = string.Empty;
        public List<SourceMapEntry> SourceMap { get; set; } = new List<SourceMapEntry>();
        public List<VariableEntry> Variables { get; set; } = new List<VariableEntry>();
        public byte[] ILBytes { get; set; } = Array.Empty<byte>();
        public long TimestampTicksUtc { get; set; } = DateTime.UtcNow.Ticks;
        public PrivacyLevel PrivacyLevel { get; set; }
        public string PrivacyReason { get; set; } = string.Empty;

        /// <summary>
        /// Generates the text map representation expected by Unity-side cache consumers.
        /// </summary>
        public string GetMapText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("LineNumber=ILOffset");
            foreach (var entry in SourceMap)
            {
                sb.Append(entry.SourceLineNumber);
                sb.Append('=');
                sb.Append(entry.ILOffset);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
