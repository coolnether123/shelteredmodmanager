using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Generic execution helpers for running fluent transpilers with caller-defined fallback policy.
    /// </summary>
    public static class FluentTranspilerExecution
    {
        public static IEnumerable<CodeInstruction> ExecuteOrOriginal(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator,
            Action<FluentTranspiler> transformer,
            Func<List<CodeInstruction>, MethodBase, Exception, IEnumerable<CodeInstruction>> onFailure)
        {
            return ExecuteOrOriginal(
                instructions,
                original,
                generator,
                TranspilerSafetyPolicy.DefaultExecuteProfile,
                transformer,
                onFailure);
        }

        public static IEnumerable<CodeInstruction> ExecuteOrOriginal(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator,
            FluentTranspiler.BuildProfile profile,
            Action<FluentTranspiler> transformer,
            Func<List<CodeInstruction>, MethodBase, Exception, IEnumerable<CodeInstruction>> onFailure)
        {
            var originalInstructions = CloneInstructions(instructions);

            try
            {
                return FluentTranspiler.Execute(
                    CloneInstructions(originalInstructions),
                    original,
                    generator,
                    profile,
                    transformer);
            }
            catch (Exception ex)
            {
                return onFailure != null
                    ? onFailure(originalInstructions, original, ex)
                    : originalInstructions;
            }
        }

        private static List<CodeInstruction> CloneInstructions(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
        }
    }
}
