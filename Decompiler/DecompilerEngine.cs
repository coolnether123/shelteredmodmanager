using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Metadata;

namespace ModAPI.Decompiler
{
    /// <summary>
    /// High-level orchestration layer for method decompilation.
    /// Pipeline:
    /// 1) Evaluate ModPrivacy policy,
    /// 2) Analyze IL/variables,
    /// 3) Produce source and IL mapping,
    /// 4) Return a single method artifact for serializers/CLI output.
    /// </summary>
    public sealed class DecompilerEngine
    {
        private readonly PrivacyInspector _privacyInspector = new PrivacyInspector();
        private readonly ILAnalyzer _ilAnalyzer = new ILAnalyzer();

        /// <summary>
        /// Decompiles a single method token from an assembly into a transport-friendly artifact.
        /// Applies privacy policy before performing full decompilation.
        /// </summary>
        public MethodArtifact Decompile(string assemblyPath, int methodToken)
        {
            var privacy = _privacyInspector.Check(assemblyPath, methodToken);

            using (var fs = File.OpenRead(assemblyPath))
            using (var peReader = new PEReader(fs))
            {
                var reader = peReader.GetMetadataReader();
                var methodHandle = MetadataTokens.MethodDefinitionHandle(methodToken);
                if (methodHandle.IsNil)
                {
                    throw new ArgumentException("Token is not a method definition token.", nameof(methodToken));
                }

                var il = _ilAnalyzer.Analyze(peReader, reader, methodHandle);

                var artifact = new MethodArtifact
                {
                    MetadataToken = methodToken,
                    MethodName = privacy.MethodName,
                    MethodSignature = privacy.MethodSignature,
                    ILBytes = il.ILBytes,
                    Variables = il.Variables,
                    TimestampTicksUtc = DateTime.UtcNow.Ticks,
                    PrivacyLevel = privacy.Level,
                    PrivacyReason = privacy.Reason ?? string.Empty
                };

                if (privacy.Level == PrivacyLevel.Private)
                {
                    artifact.SourceCode = "// [Private] Access denied by mod author.";
                    return artifact;
                }

                if (privacy.Level == PrivacyLevel.Obfuscated)
                {
                    artifact.SourceCode = BuildObfuscatedStub(privacy.MethodSignature, privacy.Reason ?? string.Empty);
                    return artifact;
                }

                PopulateSourceAndMap(assemblyPath, methodToken, il, artifact);
                return artifact;
            }
        }

        /// <summary>
        /// Uses ICSharpCode.Decompiler to render C# and collect source-to-IL mapping.
        /// Falls back to a synthetic map when semantic IL ranges are not emitted.
        /// </summary>
        private void PopulateSourceAndMap(string assemblyPath, int methodToken, ILAnalysisResult il, MethodArtifact artifact)
        {
            var module = new PEFile(assemblyPath);
            var targetFramework = module.DetectTargetFrameworkId();
            var resolver = new UniversalAssemblyResolver(assemblyPath, false, targetFramework);
            resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));

            var settings = new DecompilerSettings
            {
                ThrowOnAssemblyResolveErrors = false,
                UseDebugSymbols = true
            };

            var decompiler = new CSharpDecompiler(module, resolver, settings);
            var handle = MetadataTokens.EntityHandle(methodToken);
            var syntaxTree = decompiler.Decompile(handle);

            var sourceWriter = new StringWriter();
            var innerWriter = new TextWriterTokenWriter(sourceWriter) { IndentationString = "    " };
            var tracker = new LineTrackingTokenWriter(innerWriter);
            var visitor = new CSharpOutputVisitor(tracker, settings.CSharpFormattingOptions);
            syntaxTree.AcceptVisitor(visitor);

            artifact.SourceCode = sourceWriter.ToString();
            artifact.SourceMap = tracker
                .BuildSourceMap(_ilAnalyzer, il.InstructionOffsets)
                .ToList();

            if (artifact.SourceMap.Count == 0)
            {
                artifact.SourceMap = BuildSyntheticMap(artifact.SourceCode, il.InstructionOffsets);
            }
        }

        /// <summary>
        /// Generates a safe obfuscated body that preserves method signature visibility.
        /// </summary>
        private static string BuildObfuscatedStub(string signature, string reason)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ModPrivacy(PrivacyLevel.Obfuscated)]");
            sb.AppendLine(signature);
            sb.AppendLine("{");
            sb.Append("    // [Obfuscated");
            if (!string.IsNullOrWhiteSpace(reason))
            {
                sb.Append(": ");
                sb.Append(reason);
            }

            sb.AppendLine("] { /* Implementation hidden */ }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Creates a deterministic line-to-offset map when semantic ranges are unavailable.
        /// This guarantees map output for downstream Unity tooling.
        /// </summary>
        private static List<SourceMapEntry> BuildSyntheticMap(string sourceCode, List<int> instructionOffsets)
        {
            var map = new List<SourceMapEntry>();
            if (string.IsNullOrEmpty(sourceCode))
            {
                return map;
            }

            if (instructionOffsets == null || instructionOffsets.Count == 0)
            {
                instructionOffsets = new List<int> { 0 };
            }

            var lines = sourceCode.Split('\n');
            var offsetIndex = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                var text = lines[i];
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var ilOffset = instructionOffsets[Math.Min(offsetIndex, instructionOffsets.Count - 1)];
                map.Add(new SourceMapEntry
                {
                    SourceLineNumber = i + 1,
                    ILOffset = ilOffset,
                    InstructionCount = 1
                });

                if (offsetIndex < instructionOffsets.Count - 1)
                {
                    offsetIndex++;
                }
            }

            return map;
        }

        /// <summary>
        /// Captures line numbers while the decompiler writes source so we can map source lines to IL offsets.
        /// </summary>
        private sealed class LineTrackingTokenWriter : TokenWriter
        {
            private readonly TokenWriter _inner;
            private int _line = 1;
            private readonly Dictionary<int, int> _lineToIL = new Dictionary<int, int>();

            public LineTrackingTokenWriter(TokenWriter inner)
            {
                _inner = inner;
            }

            /// <summary>
            /// Converts tracked line data into the contract source-map model.
            /// </summary>
            public IEnumerable<SourceMapEntry> BuildSourceMap(ILAnalyzer analyzer, List<int> instructionOffsets)
            {
                foreach (var kv in _lineToIL.OrderBy(x => x.Key))
                {
                    var count = analyzer.GetInstructionCountAtOrAfterOffset(instructionOffsets, kv.Value);
                    yield return new SourceMapEntry
                    {
                        SourceLineNumber = kv.Key,
                        ILOffset = kv.Value,
                        InstructionCount = count
                    };
                }
            }

            public override void StartNode(AstNode node)
            {
                if (node is Statement || node is Expression)
                {
                    var firstRange = node.GetILRanges().FirstOrDefault();
                    if (!firstRange.IsEmpty && !_lineToIL.ContainsKey(_line))
                    {
                        _lineToIL[_line] = firstRange.Start;
                    }
                }

                _inner.StartNode(node);
            }

            public override void EndNode(AstNode node) => _inner.EndNode(node);
            public override void WriteIdentifier(Identifier identifier) => _inner.WriteIdentifier(identifier);
            public override void WriteKeyword(Role role, string keyword) => _inner.WriteKeyword(role, keyword);
            public override void WriteToken(Role role, string token) => _inner.WriteToken(role, token);
            public override void Space() => _inner.Space();
            public override void Indent() => _inner.Indent();
            public override void Unindent() => _inner.Unindent();
            public override void WriteComment(CommentType commentType, string content) => _inner.WriteComment(commentType, content);
            public override void WritePreProcessorDirective(PreProcessorDirectiveType type, string argument) => _inner.WritePreProcessorDirective(type, argument);
            public override void WritePrimitiveValue(object value, LiteralFormat format) => _inner.WritePrimitiveValue(value, format);
            public override void WritePrimitiveType(string type) => _inner.WritePrimitiveType(type);
            public override void WriteInterpolatedText(string text) => _inner.WriteInterpolatedText(text);

            public override void NewLine()
            {
                _inner.NewLine();
                _line++;
            }
        }
    }
}
