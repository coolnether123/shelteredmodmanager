using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace ModAPI.Decompiler
{
    internal sealed class SignatureTypeNameProvider :
        ISignatureTypeProvider<string, object?>,
        ICustomAttributeTypeProvider<string>
    {
        private readonly MetadataReader _reader;

        public SignatureTypeNameProvider(MetadataReader reader)
        {
            _reader = reader;
        }

        public string GetArrayType(string elementType, ArrayShape shape)
        {
            return elementType + "[]";
        }

        public string GetByReferenceType(string elementType)
        {
            return "ref " + elementType;
        }

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            return "methodptr";
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return genericType + "<" + string.Join(", ", typeArguments) + ">";
        }

        public string GetGenericMethodParameter(object? genericContext, int index)
        {
            return "!!" + index;
        }

        public string GetGenericTypeParameter(object? genericContext, int index)
        {
            return "!" + index;
        }

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
        {
            return unmodifiedType;
        }

        public string GetPinnedType(string elementType)
        {
            return elementType;
        }

        public string GetPointerType(string elementType)
        {
            return elementType + "*";
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            switch (typeCode)
            {
                case PrimitiveTypeCode.Boolean: return "bool";
                case PrimitiveTypeCode.Byte: return "byte";
                case PrimitiveTypeCode.Char: return "char";
                case PrimitiveTypeCode.Double: return "double";
                case PrimitiveTypeCode.Int16: return "short";
                case PrimitiveTypeCode.Int32: return "int";
                case PrimitiveTypeCode.Int64: return "long";
                case PrimitiveTypeCode.IntPtr: return "nint";
                case PrimitiveTypeCode.Object: return "object";
                case PrimitiveTypeCode.SByte: return "sbyte";
                case PrimitiveTypeCode.Single: return "float";
                case PrimitiveTypeCode.String: return "string";
                case PrimitiveTypeCode.UInt16: return "ushort";
                case PrimitiveTypeCode.UInt32: return "uint";
                case PrimitiveTypeCode.UInt64: return "ulong";
                case PrimitiveTypeCode.UIntPtr: return "nuint";
                case PrimitiveTypeCode.Void: return "void";
                case PrimitiveTypeCode.TypedReference: return "typedref";
                default: return typeCode.ToString();
            }
        }

        public string GetSZArrayType(string elementType)
        {
            return elementType + "[]";
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            return BuildTypeName(reader.GetString(typeDef.Namespace), reader.GetString(typeDef.Name));
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var typeRef = reader.GetTypeReference(handle);
            return BuildTypeName(reader.GetString(typeRef.Namespace), reader.GetString(typeRef.Name));
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            var typeSpec = reader.GetTypeSpecification(handle);
            return typeSpec.DecodeSignature(this, genericContext);
        }

        public string GetUnsupportedSignatureType(byte rawTypeKind)
        {
            return "<unsupported:" + rawTypeKind + ">";
        }

        public string GetSystemType()
        {
            return "System.Type";
        }

        public bool IsSystemType(string type)
        {
            return string.Equals(type, "System.Type", StringComparison.Ordinal);
        }

        public string GetTypeFromSerializedName(string name)
        {
            return name;
        }

        public PrimitiveTypeCode GetUnderlyingEnumType(string type)
        {
            return PrimitiveTypeCode.Int32;
        }

        public bool IsEnum(string type)
        {
            return type.EndsWith("PrivacyLevel", StringComparison.Ordinal) || type.EndsWith(".PrivacyLevel", StringComparison.Ordinal);
        }

        private static string BuildTypeName(string ns, string name)
        {
            if (string.IsNullOrEmpty(ns))
            {
                return name;
            }

            return ns + "." + name;
        }
    }
}
