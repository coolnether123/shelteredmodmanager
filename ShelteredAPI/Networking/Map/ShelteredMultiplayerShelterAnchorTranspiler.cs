using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ShelteredAPI.Networking
{
    internal static class ShelteredMultiplayerShelterAnchorTranspiler
    {
        private static readonly MethodInfo GameModeGetInstance =
            AccessTools.PropertyGetter(typeof(GameModeManager), "instance");
        private static readonly MethodInfo ShelterMapGetter =
            AccessTools.PropertyGetter(typeof(GameModeManager), "shelterMapWorldPosition");
        private static readonly MethodInfo GetActiveBunkerMapPixels =
            AccessTools.Method(typeof(ShelteredMultiplayerBunkerAnchorRuntime),
                "GetActiveBunkerMapPixels",
                Type.EmptyTypes);

        public static IEnumerable<CodeInstruction> ReplaceShelterMapGetterPairs(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            for (int i = 0; i < code.Count - 1; i++)
            {
                if (!IsCallTo(code[i], GameModeGetInstance))
                    continue;
                if (!IsCallTo(code[i + 1], ShelterMapGetter))
                    continue;

                code[i] = CopyMeta(code[i], new CodeInstruction(OpCodes.Call, GetActiveBunkerMapPixels));
                code[i + 1] = CopyMeta(code[i + 1], new CodeInstruction(OpCodes.Nop));
                i++;
            }

            return code;
        }

        private static bool IsCallTo(CodeInstruction instruction, MethodInfo method)
        {
            if (instruction == null || method == null)
                return false;
            if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
                return false;

            MethodInfo operand = instruction.operand as MethodInfo;
            return operand != null && operand == method;
        }

        private static CodeInstruction CopyMeta(CodeInstruction original, CodeInstruction replacement)
        {
            if (original == null || replacement == null)
                return replacement;
            if (original.labels != null && original.labels.Count > 0)
                replacement.labels.AddRange(original.labels);
            if (original.blocks != null && original.blocks.Count > 0)
                replacement.blocks.AddRange(original.blocks);
            return replacement;
        }
    }
}
