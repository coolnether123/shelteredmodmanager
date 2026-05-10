using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Harmony;

namespace ShelteredAPI.Networking.Tests
{
    internal static class FluentTranspilerIntentTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("FluentTranspiler_ReplaceMethodCall_ReplacesCompatibleCall", ReplaceMethodCallReplacesCompatibleCall));
            tests.Add(new TestCase("FluentTranspiler_ReplaceMethodCall_RejectsInvalidSignature", ReplaceMethodCallRejectsInvalidSignature));
            tests.Add(new TestCase("FluentTranspiler_InsertBeforeCall_DetectsMultipleMatches", InsertBeforeCallDetectsMultipleMatches));
            tests.Add(new TestCase("FluentTranspiler_WrapReturnValue_InsertsBeforeEveryReturn", WrapReturnValueInsertsBeforeEveryReturn));
            tests.Add(new TestCase("FluentTranspiler_InsertBeforeCallWithLocals_ValidatesLocalCapture", InsertBeforeCallWithLocalsValidatesLocalCapture));
            tests.Add(new TestCase("FluentTranspiler_InjectGuardBeforeCall_PreservesStackShape", InjectGuardBeforeCallPreservesStackShape));
        }

        private static void ReplaceMethodCallReplacesCompatibleCall()
        {
            MethodInfo original = Method(typeof(SampleCalls), "Increment", typeof(int));
            MethodInfo replacement = Method(typeof(SampleHooks), "Double", typeof(int));

            FluentTranspiler transpiler = TranspilerTestHarness.FromInstructions(
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Call, original),
                new CodeInstruction(OpCodes.Ret));

            transpiler.ReplaceMethodCall(typeof(SampleCalls), "Increment", typeof(SampleHooks), "Double");

            List<CodeInstruction> result = transpiler.Instructions().ToList();
            TestAssert.Equal(replacement, result[1].operand, "Compatible replacement call should be emitted.");
            TestAssert.Equal(0, transpiler.Warnings.Count, "Compatible replacement should not warn.");
        }

        private static void ReplaceMethodCallRejectsInvalidSignature()
        {
            MethodInfo original = Method(typeof(SampleCalls), "Increment", typeof(int));

            FluentTranspiler transpiler = TranspilerTestHarness.FromInstructions(
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Call, original),
                new CodeInstruction(OpCodes.Ret));

            transpiler.ReplaceMethodCall(typeof(SampleCalls), "Increment", typeof(SampleHooks), "WrongString");

            List<CodeInstruction> result = transpiler.Instructions().ToList();
            TestAssert.Equal(original, result[1].operand, "Invalid replacement should leave original call unchanged.");
            TestAssert.True(ContainsWarning(transpiler, "Invalid replacement signature"), "Invalid signature should be diagnosed.");
        }

        private static void InsertBeforeCallDetectsMultipleMatches()
        {
            MethodInfo target = Method(typeof(SampleCalls), "Ping");

            FluentTranspiler transpiler = TranspilerTestHarness.FromInstructions(
                new CodeInstruction(OpCodes.Call, target),
                new CodeInstruction(OpCodes.Call, target),
                new CodeInstruction(OpCodes.Ret));

            transpiler.InsertBeforeCall(typeof(SampleCalls), "Ping", typeof(SampleHooks), "BeforePing");

            TestAssert.Equal(3, transpiler.Instructions().Count(), "Ambiguous insert should not mutate instructions.");
            TestAssert.True(ContainsWarning(transpiler, "Pattern found multiple times"), "Ambiguous call pattern should be diagnosed.");
        }

        private static void WrapReturnValueInsertsBeforeEveryReturn()
        {
            MethodInfo originalMethod = Method(typeof(FluentTranspilerIntentTests), "ReturnsInt");
            MethodInfo wrapper = Method(typeof(SampleHooks), "ClampInt", typeof(int));

            FluentTranspiler transpiler = FluentTranspiler.For(
                new[]
                {
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Ret)
                },
                originalMethod);

            transpiler.WrapReturnValue(typeof(SampleHooks), "ClampInt");

            List<CodeInstruction> result = transpiler.Instructions().ToList();
            TestAssert.Equal(OpCodes.Call, result[1].opcode, "Wrapper call should be inserted before ret.");
            TestAssert.Equal(wrapper, result[1].operand, "Wrapper call should target the configured hook.");
            TestAssert.Equal(OpCodes.Ret, result[2].opcode, "Return should remain after wrapper.");
        }

        private static void InsertBeforeCallWithLocalsValidatesLocalCapture()
        {
            MethodInfo target = Method(typeof(SampleCalls), "Ping");
            MethodInfo hook = Method(typeof(SampleHooks), "ObserveInt", typeof(int));
            MethodInfo originalMethod = Method(typeof(FluentTranspilerIntentTests), "MethodWithIntLocal");

            FluentTranspiler transpiler = FluentTranspiler.For(
                new[]
                {
                    new CodeInstruction(OpCodes.Call, target),
                    new CodeInstruction(OpCodes.Ret)
                },
                originalMethod);

            transpiler.InsertBeforeCallWithLocals(
                typeof(SampleCalls),
                "Ping",
                typeof(SampleHooks),
                "ObserveInt",
                new[] { 0 },
                hookParameterTypes: new[] { typeof(int) });

            List<CodeInstruction> result = transpiler.Instructions().ToList();
            TestAssert.Equal(OpCodes.Ldloc_0, result[0].opcode, "Local capture should load local 0 before the hook.");
            TestAssert.Equal(OpCodes.Call, result[1].opcode, "Local capture hook should be inserted before target call.");
            TestAssert.Equal(hook, result[1].operand, "Local capture hook should target configured method.");
        }

        private static void InjectGuardBeforeCallPreservesStackShape()
        {
            MethodInfo target = Method(typeof(SampleCalls), "Increment", typeof(int));
            MethodInfo guard = Method(typeof(SampleHooks), "ShouldRun");
            MethodInfo originalMethod = Method(typeof(FluentTranspilerIntentTests), "ReturnsInt");
            DynamicMethod dynamicMethod = new DynamicMethod(
                "GuardIntentTest",
                typeof(int),
                Type.EmptyTypes,
                typeof(FluentTranspilerIntentTests),
                true);
            ILGenerator generator = dynamicMethod.GetILGenerator();

            FluentTranspiler transpiler = FluentTranspiler.For(
                new[]
                {
                    new CodeInstruction(OpCodes.Ldc_I4_2),
                    new CodeInstruction(OpCodes.Call, target),
                    new CodeInstruction(OpCodes.Ret)
                },
                originalMethod,
                generator);

            transpiler.InjectGuardBeforeCall(typeof(SampleCalls), "Increment", typeof(SampleHooks), "ShouldRun");

            List<CodeInstruction> result = transpiler.Instructions().ToList();
            TestAssert.True(result.Any(i => i.opcode == OpCodes.Call && object.Equals(i.operand, guard)), "Guard call should be inserted.");
            TestAssert.True(result.Any(i => i.opcode == OpCodes.Pop), "Skipped call path should pop original call arguments.");
            TestAssert.True(result.Any(i => i.opcode == OpCodes.Ldc_I4_0), "Skipped call path should push default int return value.");
            TestAssert.True(result.Any(i => i.opcode == OpCodes.Call && object.Equals(i.operand, target)), "Original target call should remain on allowed path.");
        }

        private static bool ContainsWarning(FluentTranspiler transpiler, string text)
        {
            for (int i = 0; i < transpiler.Warnings.Count; i++)
            {
                if (transpiler.Warnings[i].IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static MethodInfo Method(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = parameters == null || parameters.Length == 0
                ? type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                : type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null);

            if (method == null)
                throw new InvalidOperationException(type.Name + "." + name + " was not found.");

            return method;
        }

        private static int ReturnsInt()
        {
            return 1;
        }

        private static int MethodWithIntLocal()
        {
            int value = Environment.TickCount;
            return value;
        }

        private static class SampleCalls
        {
            public static void Ping()
            {
            }

            public static int Increment(int value)
            {
                return value + 1;
            }
        }

        private static class SampleHooks
        {
            public static void BeforePing()
            {
            }

            public static void ObserveInt(int value)
            {
            }

            public static int Double(int value)
            {
                return value * 2;
            }

            public static string WrongString(string value)
            {
                return value;
            }

            public static int ClampInt(int value)
            {
                return Math.Max(0, value);
            }

            public static bool ShouldRun()
            {
                return true;
            }
        }
    }
}
