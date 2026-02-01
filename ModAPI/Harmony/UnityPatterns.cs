using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Common Unity-specific IL patterns and replacements.
    /// Focuses on universal Unity engine types (Vector2, Time, GameObject, etc.).
    /// </summary>
    public static class UnityPatterns
    {
        #region Unity Version Compatibility
        
        private static readonly bool _vector2ZeroIsField;
        private static readonly bool _vector3ZeroIsField;
        private static readonly FieldInfo _vector2ZeroField;
        private static readonly FieldInfo _vector3ZeroField;

        // Static constructor checks Unity version compatibility at startup
        static UnityPatterns()
        {
            _vector2ZeroField = typeof(Vector2).GetField("zero", BindingFlags.Public | BindingFlags.Static);
            _vector2ZeroIsField = _vector2ZeroField != null;
            
            _vector3ZeroField = typeof(Vector3).GetField("zero", BindingFlags.Public | BindingFlags.Static);
            _vector3ZeroIsField = _vector3ZeroField != null;
            
            if (!_vector2ZeroIsField)
                MMLog.WriteWarning($"[UnityPatterns] Vector2.zero is not a field in this Unity version. ReplaceVectorZero will skip.");
            
            if (!_vector3ZeroIsField)
                MMLog.WriteWarning($"[UnityPatterns] Vector3.zero is not a field in this Unity version. ReplaceVectorZero will skip.");
        }
        
        #endregion
        
        #region Vector Field/Property Replacements (Universal)
        
        /// <summary>
        /// Replace Vector2.zero or Vector3.zero property access with field access.
        /// </summary>
        public static FluentTranspiler ReplaceVectorZero(this FluentTranspiler t, Type vectorType)
        {
            if (vectorType != typeof(Vector2) && vectorType != typeof(Vector3))
                throw new ArgumentException($"Expected Vector2 or Vector3, got {vectorType.Name}");

            var prop = vectorType.GetProperty("zero", BindingFlags.Public | BindingFlags.Static);
            if (prop != null)
            {
                var field = vectorType.GetField("zero", BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                {
                    throw new InvalidOperationException($"{vectorType.Name}.zero is a property but no static 'zero' field found.");
                }
                
                return t.MatchCall(vectorType, "get_zero")
                        .ReplaceWith(OpCodes.Ldsfld, field);
            }
            return t;
        }

        /// <summary>
        /// Replace ALL Vector2.zero or Vector3.zero calls in the method.
        /// </summary>
        public static FluentTranspiler ReplaceAllVectorZero(this FluentTranspiler t, Type vectorType)
        {
            if (vectorType != typeof(Vector2) && vectorType != typeof(Vector3))
                throw new ArgumentException($"Expected Vector2 or Vector3, got {vectorType.Name}");

            t.Reset();
            while (true)
            {
                t.MatchCallNext(vectorType, "get_zero");
                if (!t.HasMatch) break;
                t.ReplaceWith(OpCodes.Ldsfld, vectorType.GetField("zero"));
            }
            return t;
        }
        
        #endregion

        #region Vector Pattern Replacements (Legacy Sequence)

        /// <summary>
        /// Replace the legacy 'new Vector2(0,0)' IL sequence with a custom static call.
        /// </summary>
        public static FluentTranspiler ReplaceVectorZeroWithCall(
            this FluentTranspiler t, 
            Type type, 
            string method,
            bool preserveInstructionCount = true)
        {
            return t.ReplaceAllPatterns(PatternVector2Zero(), new[] { new CodeInstruction(OpCodes.Call, type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) }, preserveInstructionCount);
        }

        #endregion

        #region Time Replacements
        
        /// <summary>
        /// Replace Time.deltaTime getter with custom implementation.
        /// </summary>
        public static FluentTranspiler ReplaceTimeDeltaTime(
            this FluentTranspiler t, 
            Type overrideType, 
            string overrideMethod)
        {
            ValidateStaticMethod(overrideType, overrideMethod, typeof(float));
            
            return t
                .MatchCall(typeof(Time), "get_deltaTime")
                .ReplaceWithCall(overrideType, overrideMethod);
        }

        /// <summary>
        /// Replace ALL Time.deltaTime calls in the method.
        /// </summary>
        public static FluentTranspiler ReplaceAllTimeDeltaTime(
            this FluentTranspiler t, 
            Type overrideType, 
            string overrideMethod)
        {
            ValidateStaticMethod(overrideType, overrideMethod, typeof(float));
            
            return t.ReplaceAllCalls(
                typeof(Time), "get_deltaTime",
                overrideType, overrideMethod);
        }
        
        #endregion

        #region GetComponent Replacement
        
        /// <summary>
        /// Replace GetComponent(Type) calls with custom factory.
        /// </summary>
        public static FluentTranspiler ReplaceGetComponent(
            this FluentTranspiler t,
            Type factoryType,
            string factoryMethod)
        {
            ValidateStaticMethod(factoryType, factoryMethod, null);
            
            return t
                .MatchCall(typeof(GameObject), "GetComponent", parameterTypes: new[] { typeof(Type) })
                .ReplaceWithCall(factoryType, factoryMethod);
        }

        /// <summary>
        /// Replace generic GetComponent<T> calls with custom factory.
        /// </summary>
        public static FluentTranspiler ReplaceGetComponentGeneric<T>(
            this FluentTranspiler t,
            Type factoryType,
            string factoryMethod)
        {
            ValidateStaticMethod(factoryType, factoryMethod, null);
            
            return t
                .MatchCall(typeof(GameObject), "GetComponent", genericArguments: new[] { typeof(T) })
                .ReplaceWithCall(factoryType, factoryMethod);
        }
        
        #endregion

        #region Debug Helpers
        
        /// <summary>
        /// Insert a debug log before the CURRENT match.
        /// </summary>
        public static FluentTranspiler InsertDebugLogBefore(
            this FluentTranspiler t,
            string message)
        {
            if (!t.HasMatch) return t;
            
            return t
                .InsertBefore(OpCodes.Ldstr, message)
                .InsertBefore(OpCodes.Call, typeof(Debug).GetMethod("Log", 
                    new[] { typeof(object) }));
        }
        
        /// <summary>
        /// Insert a debug log after the CURRENT match.
        /// </summary>
        public static FluentTranspiler InsertDebugLogAfter(
            this FluentTranspiler t,
            string message)
        {
            if (!t.HasMatch) return t;
            
            return t
                .InsertAfter(OpCodes.Ldstr, message)
                .InsertAfter(OpCodes.Call, typeof(Debug).GetMethod("Log", 
                    new[] { typeof(object) }));
        }
        
        #endregion

        #region Vector Object Predicates (Helper)

        /// <summary>Check if current instruction is a newobj for Vector2.</summary>
        public static bool IsNewobjVector2(this FluentTranspiler t)
        {
            return t.IsNewobj(typeof(UnityEngine.Vector2));
        }

        /// <summary>Check if current instruction is a newobj for Vector3.</summary>
        public static bool IsNewobjVector3(this FluentTranspiler t)
        {
            return t.IsNewobj(typeof(UnityEngine.Vector3));
        }

        #endregion

        #region Pattern Sequence Helpers

        /// <summary>
        /// Helper to create a pattern that matches: ldc.r4 0, ldc.r4 0, newobj Vector2
        /// </summary>
        public static Func<CodeInstruction, bool>[] PatternVector2Zero()
        {
            return new Func<CodeInstruction, bool>[]
            {
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f1 && Math.Abs(f1) < 0.0001f,
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f2 && Math.Abs(f2) < 0.0001f,
                instr => instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci && ci.DeclaringType == typeof(UnityEngine.Vector2)
            };
        }

        /// <summary>
        /// Helper to create a pattern that matches: ldc.r4 0, ldc.r4 0, ldc.r4 0, newobj Vector3
        /// </summary>
        public static Func<CodeInstruction, bool>[] PatternVector3Zero()
        {
            return new Func<CodeInstruction, bool>[]
            {
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f1 && Math.Abs(f1) < 0.0001f,
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f2 && Math.Abs(f2) < 0.0001f,
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f3 && Math.Abs(f3) < 0.0001f,
                instr => instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci && ci.DeclaringType == typeof(UnityEngine.Vector3)
            };
        }

        #endregion

        #region Validation Helpers
        
        /// <summary>
        /// Validate that a static method exists with the expected return type.
        /// </summary>
        public static void ValidateStaticMethod(Type type, string methodName, Type expectedReturnType)
        {
            var method = type.GetMethod(methodName, 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                
            if (method == null)
            {
                throw new ArgumentException($"Static method {type.Name}.{methodName} not found");
            }
            
            if (!method.IsStatic)
            {
                throw new ArgumentException($"{type.Name}.{methodName} must be static");
            }
            
            if (expectedReturnType != null && method.ReturnType != expectedReturnType)
            {
                throw new ArgumentException(
                    $"{type.Name}.{methodName} must return {expectedReturnType.Name}, " +
                    $"but returns {method.ReturnType.Name}");
            }
        }
        
        #endregion
    }
}
