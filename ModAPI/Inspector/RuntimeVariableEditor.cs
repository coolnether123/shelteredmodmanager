using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    public class RuntimeVariableEditor
    {
        private readonly Queue<VariableEditRequest> _pendingEdits = new Queue<VariableEditRequest>();

        public void RequestEdit(object target, FieldInfo field, object newValue)
        {
            var request = new VariableEditRequest
            {
                Target = target,
                Field = field,
                NewValue = newValue,
                RequestTime = DateTime.UtcNow,
                ValidationHash = ComputeTypeHash(field.FieldType, newValue)
            };

            _pendingEdits.Enqueue(request);
        }

        public void ProcessPendingEdits()
        {
            while (_pendingEdits.Count > 0)
            {
                var edit = _pendingEdits.Dequeue();
                try
                {
                    if (edit.NewValue != null && !edit.Field.FieldType.IsAssignableFrom(edit.NewValue.GetType()))
                    {
                        MMLog.WriteWarning("Type mismatch: cannot assign " + edit.NewValue.GetType() + " to " + edit.Field.FieldType);
                        continue;
                    }

                    var oldValue = edit.Field.GetValue(edit.Target);
                    var converted = ConvertValue(edit.NewValue, edit.Field.FieldType);
                    edit.Field.SetValue(edit.Target, converted);
                    MMLog.WriteInfo("Variable edited: " + edit.Field.Name + " = " + converted + " (was " + oldValue + ")");
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("Failed to edit " + edit.Field.Name + ": " + ex.Message);
                }
            }
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsAssignableFrom(value.GetType())) return value;
            return Convert.ChangeType(value, targetType);
        }

        private static int ComputeTypeHash(Type type, object value)
        {
            return type.GetHashCode() ^ (value != null ? value.GetHashCode() : 0);
        }
    }
}
