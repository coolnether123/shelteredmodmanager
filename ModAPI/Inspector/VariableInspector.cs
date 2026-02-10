using System;
using System.Reflection;

namespace ModAPI.Inspector
{
    // Backward-compatible alias for older code paths.
    public class VariableInspector : RuntimeVariableEditor
    {
    }
    
    public struct VariableEditRequest 
    {
        public object Target;
        public FieldInfo Field;
        public object NewValue;
        public DateTime RequestTime;
        public int ValidationHash;
    }
}
