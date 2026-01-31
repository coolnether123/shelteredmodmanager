using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Harmony
{
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
        
        #region Vector Replacements
        
        /// <summary>
        /// Replace Vector2.zero or Vector3.zero property access.
        /// Includes matching - do NOT pre-match.
        /// </summary>
        public static FluentTranspiler ReplaceVectorZero(this FluentTranspiler t, Type vectorType)
        {
            if (vectorType != typeof(Vector2) && vectorType != typeof(Vector3))
                throw new ArgumentException($"Expected Vector2 or Vector3, got {vectorType.Name}");
            // Unity version compatibility: Vector2.zero is a property in some versions and a field in others. 
            // Checking both ensures the transpiler works across different Unity engine builds.
            var prop = vectorType.GetProperty("zero", BindingFlags.Public | BindingFlags.Static);
            if (prop != null)
            {
                // Is a property (older Unity or specific build)
                var field = vectorType.GetField("zero", BindingFlags.Public | BindingFlags.Static);
                
                // Ensures that if 'zero' is a property, the expected backing field 
                // is also present, catching potential engine implementation changes early.
                if (field == null)
                {
                    throw new InvalidOperationException(
                        $"{vectorType.Name}.zero is a property but no static 'zero' field found. " +
                        "This Unity version may use internal calls or intrinsics.");
                }
                
                return t.MatchCall(vectorType, "get_zero")
                        .ReplaceWith(OpCodes.Ldsfld, field);
            }
            
            // Is already a field or not found - nothing to "replace" via call matching
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

        #region Time Replacements
        
        /// <summary>
        /// Replace Time.deltaTime getter with custom implementation.
        /// Includes matching - do NOT pre-match.
        /// </summary>
        /// <param name="overrideType">Type containing the replacement method (must be static).</param>
        /// <param name="overrideMethod">Name of the static method returning float.</param>
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

        #region DontDestroyOnLoad
        
        /// <summary>
        /// Remove a call to DontDestroyOnLoad.
        /// Includes matching - do NOT pre-match.
        /// ⚠️ WARNING: If DontDestroyOnLoad(new GameObject()) is used, 
        /// the GameObject is created but not preserved! This can 
        /// lead to memory leaks or unexpected object destruction.
        /// NOTE: Stack management: DontDestroyOnLoad consumes one argument. 
        /// Replacing it with a Pop ensures the stack remains balanced after the call is removed.
        /// </summary>
        public static FluentTranspiler NukeDontDestroyOnLoad(this FluentTranspiler t)
        {
            return t
                .MatchCall(typeof(UnityEngine.Object), "DontDestroyOnLoad")
                .ReplaceWith(OpCodes.Pop);
        }

        /// <summary>
        /// Remove ALL calls to DontDestroyOnLoad in the method.
        /// </summary>
        public static FluentTranspiler NukeAllDontDestroyOnLoad(this FluentTranspiler t)
        {
            t.Reset();
            while (true)
            {
                t.MatchCallNext(typeof(UnityEngine.Object), "DontDestroyOnLoad");
                if (!t.HasMatch) break;
                t.ReplaceWith(OpCodes.Pop);
            }
            return t;
        }
        
        #endregion

        #region GetComponent Replacement
        
        /// <summary>
        /// Replace GetComponent(Type) calls with custom factory.
        /// Includes matching - do NOT pre-match.
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
        /// Includes matching - do NOT pre-match.
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
        /// Requires pre-match - use after MatchCall/MatchOpCode.
        /// </summary>
        public static FluentTranspiler InsertDebugLogBefore(
            this FluentTranspiler t,
            string message)
        {
            if (!t.HasMatch) return t;  // Silent skip if no match
            
            return t
                .InsertBefore(OpCodes.Ldstr, message)
                .InsertBefore(OpCodes.Call, typeof(Debug).GetMethod("Log", 
                    new[] { typeof(object) }));
        }
        
        /// <summary>
        /// Insert a debug log after the CURRENT match.
        /// Requires pre-match - use after MatchCall/MatchOpCode.
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

        #region Validation Helpers
        
        /// <summary>
        /// Validate that a static method exists with the expected return type.
        /// </summary>
        private static void ValidateStaticMethod(Type type, string methodName, Type expectedReturnType)
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
