using System;
using ModAPI.Core;

namespace ModAPI.Reflection
{
    /// <summary>
    /// Developer Experience (DX) extension methods for easier private member access.
    /// Extension methods for common game objects to safely get/set private fields.
    /// </summary>
    public static class SafeExtensions
    {
        /// <summary>
        /// Safely get a private field value from any object.
        /// Catches exceptions and returns default(T) on failure.
        /// </summary>
        public static T GetPrivateField<T>(this object obj, string fieldName)
        {
            if (obj == null) return default(T);
            
            if (Safe.TryGetField<T>(obj, fieldName, out var value))
            {
                return value;
            }
            
            return default(T);
        }

        /// <summary>
        /// Safely set a private field value on any object.
        /// </summary>
        public static bool SetPrivateField(this object obj, string fieldName, object value)
        {
            if (obj == null) return false;
            return Safe.SetField(obj, fieldName, value);
        }

        /// <summary>
        /// Safely call a private method on any object.
        /// </summary>
        public static bool InvokePrivateMethod(this object obj, string methodName, params object[] args)
        {
            if (obj == null) return false;
            return Safe.InvokeMethod(obj, methodName, args);
        }

        /// <summary>
        /// Safely call a private method and return a result.
        /// </summary>
        public static T CallPrivateMethod<T>(this object obj, string methodName, params object[] args)
        {
            if (obj == null) return default(T);
            Safe.TryCall<T>(obj, methodName, out var result, args);
            return result;
        }

        /// <summary>
        /// Safely get a private property value.
        /// </summary>
        public static T GetPrivateProperty<T>(this object obj, string propertyName)
        {
            if (obj == null) return default(T);
            if (Safe.TryGetProperty<T>(obj, propertyName, out var value))
            {
                return value;
            }
            return default(T);
        }
    }
}
