using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ModAPI.Decompiler
{
    /// <summary>
    /// Extracts IL-level data for a method:
    /// raw IL bytes, instruction boundaries, and best-effort variable table.
    /// </summary>
    public sealed class ILAnalyzer
    {
        private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

        static ILAnalyzer()
        {
            var fields = typeof(OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var fi in fields)
            {
                if (fi.FieldType != typeof(OpCode))
                {
                    continue;
                }

                var opObj = fi.GetValue(null);
                if (opObj == null)
                {
                    continue;
                }

                var op = (OpCode)opObj;
                var value = unchecked((ushort)op.Value);
                if (value < 0x100)
                {
                    OneByteOpCodes[value] = op;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    TwoByteOpCodes[value & 0xff] = op;
                }
            }
        }

        /// <summary>
        /// Performs metadata + IL body analysis for a single method definition.
        /// </summary>
        public ILAnalysisResult Analyze(PEReader peReader, MetadataReader reader, MethodDefinitionHandle methodHandle)
        {
            var methodDef = reader.GetMethodDefinition(methodHandle);
            var typeDef = reader.GetTypeDefinition(methodDef.GetDeclaringType());
            var provider = new SignatureTypeNameProvider(reader);

            var ilBytes = Array.Empty<byte>();
            var instructionOffsets = new List<int>();
            var variables = new List<VariableEntry>();

            AddParameters(reader, methodDef, provider, variables);
            AddFields(reader, typeDef, provider, variables);

            if (methodDef.RelativeVirtualAddress != 0)
            {
                var body = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);
                ilBytes = body.GetILBytes() ?? Array.Empty<byte>();
                instructionOffsets = ParseInstructionOffsets(ilBytes);
                AddLocals(reader, body, provider, variables);
            }
            else
            {
                variables.Add(new VariableEntry
                {
                    Name = "<unknown>",
                    Type = "<unknown>",
                    IsLocal = true,
                    ILIndex = -1
                });
            }

            return new ILAnalysisResult
            {
                ILBytes = ilBytes,
                InstructionOffsets = instructionOffsets,
                Variables = variables
            };
        }

        /// <summary>
        /// Returns a coarse instruction count hint for map entries near a specific IL offset.
        /// </summary>
        public short GetInstructionCountAtOrAfterOffset(List<int> instructionOffsets, int ilOffset)
        {
            if (instructionOffsets == null || instructionOffsets.Count == 0)
            {
                return 0;
            }

            for (var i = 0; i < instructionOffsets.Count; i++)
            {
                if (instructionOffsets[i] >= ilOffset)
                {
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Reads method parameter names/types from metadata.
        /// </summary>
        private static void AddParameters(
            MetadataReader reader,
            MethodDefinition methodDef,
            SignatureTypeNameProvider provider,
            List<VariableEntry> variables)
        {
            var signature = methodDef.DecodeSignature(provider, null);
            var parameterNames = new List<string>();
            foreach (var ph in methodDef.GetParameters())
            {
                var parameter = reader.GetParameter(ph);
                if (parameter.SequenceNumber == 0)
                {
                    continue;
                }

                var pName = reader.GetString(parameter.Name);
                if (string.IsNullOrEmpty(pName))
                {
                    pName = "arg" + (parameter.SequenceNumber - 1);
                }

                parameterNames.Add(pName);
            }

            for (var i = 0; i < signature.ParameterTypes.Length; i++)
            {
                var pType = signature.ParameterTypes[i] ?? "object";
                var pName = i < parameterNames.Count ? parameterNames[i] : "arg" + i;
                variables.Add(new VariableEntry
                {
                    Name = pName,
                    Type = pType,
                    IsLocal = false,
                    ILIndex = i
                });
            }
        }

        /// <summary>
        /// Adds instance field entries as inspectable runtime variables.
        /// </summary>
        private static void AddFields(
            MetadataReader reader,
            TypeDefinition typeDef,
            SignatureTypeNameProvider provider,
            List<VariableEntry> variables)
        {
            foreach (var fh in typeDef.GetFields())
            {
                var field = reader.GetFieldDefinition(fh);
                var fName = reader.GetString(field.Name);
                var fType = field.DecodeSignature(provider, null);
                variables.Add(new VariableEntry
                {
                    Name = "this." + fName,
                    Type = fType ?? "object",
                    IsLocal = false,
                    ILIndex = -1
                });
            }
        }

        /// <summary>
        /// Adds locals when local signature metadata is available; otherwise keeps a placeholder.
        /// </summary>
        private static void AddLocals(
            MetadataReader reader,
            MethodBodyBlock body,
            SignatureTypeNameProvider provider,
            List<VariableEntry> variables)
        {
            if (body.LocalSignature.IsNil)
            {
                return;
            }

            try
            {
                var sig = reader.GetStandaloneSignature(body.LocalSignature);
                var decoder = new SignatureDecoder<string, object?>(provider, reader, null);
                var blobReader = reader.GetBlobReader(sig.Signature);
                ImmutableArray<string> locals = decoder.DecodeLocalSignature(ref blobReader);
                for (var i = 0; i < locals.Length; i++)
                {
                    variables.Add(new VariableEntry
                    {
                        Name = "<unknown_local_" + i + ">",
                        Type = string.IsNullOrEmpty(locals[i]) ? "<unknown>" : locals[i],
                        IsLocal = true,
                        ILIndex = i
                    });
                }
            }
            catch
            {
                variables.Add(new VariableEntry
                {
                    Name = "<unknown>",
                    Type = "<unknown>",
                    IsLocal = true,
                    ILIndex = -1
                });
            }
        }

        /// <summary>
        /// Walks IL bytes and collects opcode start offsets.
        /// </summary>
        private static List<int> ParseInstructionOffsets(byte[] ilBytes)
        {
            var offsets = new List<int>();
            var index = 0;
            while (index < ilBytes.Length)
            {
                offsets.Add(index);

                OpCode op;
                var b = ilBytes[index++];
                if (b == 0xfe)
                {
                    if (index >= ilBytes.Length) break;
                    op = TwoByteOpCodes[ilBytes[index++]];
                }
                else
                {
                    op = OneByteOpCodes[b];
                }

                index += GetOperandSize(op.OperandType, ilBytes, index);
            }

            return offsets;
        }

        /// <summary>
        /// Computes operand byte width for an opcode operand type.
        /// </summary>
        private static int GetOperandSize(OperandType operandType, byte[] il, int operandIndex)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    if (operandIndex + 4 > il.Length)
                    {
                        return 0;
                    }

                    var count = BitConverter.ToInt32(il, operandIndex);
                    return 4 + (count * 4);
                default:
                    return 0;
            }
        }
    }

    /// <summary>
    /// Transfer model for IL analysis output consumed by the decompiler engine.
    /// </summary>
    public sealed class ILAnalysisResult
    {
        public byte[] ILBytes { get; set; } = Array.Empty<byte>();
        public List<int> InstructionOffsets { get; set; } = new List<int>();
        public List<VariableEntry> Variables { get; set; } = new List<VariableEntry>();
    }
}
