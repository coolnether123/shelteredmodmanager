using System;
using System.Reflection;

namespace ModAPI.Inspector
{
    // Backward-compatible alias for older code paths.
    internal class VariableInspector : RuntimeVariableEditor
    {
    }
    
    internal struct VariableEditRequest
    {
        public object Target;
        public FieldInfo Field;
        public object NewValue;
        public DateTime RequestTime;
        public int ValidationHash;
    }
}
