using System.Reflection;

namespace ModAPI.Harmony
{
    internal static class FluentTranspilerFormatting
    {
        internal static string FormatMethod(MethodInfo method)
        {
            if (method == null)
            {
                return "<null>";
            }

            return $"{method.DeclaringType?.FullName}.{method.Name}";
        }

        internal static string FormatField(FieldInfo field)
        {
            if (field == null)
            {
                return "<null>";
            }

            return $"{field.DeclaringType?.FullName}.{field.Name}";
        }
    }
}
