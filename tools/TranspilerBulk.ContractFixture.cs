using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

namespace TranspilerBulkFixture {
    public partial class FluentTranspiler {
        private readonly CodeMatcher _matcher = new CodeMatcher(new[] { new CodeInstruction(System.Reflection.Emit.OpCodes.Ret) });
        private readonly MethodBase _originalMethod = null;
        private readonly string _callerMod = "fixture";
        private int _failures;
        private bool ResolvePatternPreserveMode(bool requested, int length) { return requested; }
        private void LogTrace(string message) { }
        private void AddSoftFailure(string message) { _failures++; }
        private void AddWarning(string message) { }
        private bool CanSafelyPatchWithExceptionBlocks(List<CodeInstruction> instructions, List<int> positions, int length, int replaceLength, string name) { return true; }
        private bool CanSafelyPadWithNops(List<CodeInstruction> instructions, List<int> positions, int length, int replaceLength) { return true; }
        private void SetInstructionSafe(CodeInstruction instruction) { throw new Exception("Unexpected mutation"); }
        private void ReplaceSequence(int count, bool record, params CodeInstruction[] instructions) { throw new Exception("Unexpected mutation"); }
        private void RecordPatchEdit(string name, int before, IEnumerable<CodeInstruction> removed, int after, IEnumerable<CodeInstruction> added, string detail, string confidence) { }
        public bool UnchangedWithFailure { get { return _failures == 1 && _matcher.Instructions().Count == 1 && _matcher.Instructions()[0].opcode == System.Reflection.Emit.OpCodes.Ret; } }
    }
    public static class TranspilerBulkContractFixture {
        public static int Count;
        public static void Verify() {
            var validPattern = new Func<CodeInstruction, bool>[] { delegate(CodeInstruction instruction) { return instruction.opcode == System.Reflection.Emit.OpCodes.Nop; } };
            VerifyRejected(new Func<CodeInstruction, bool>[0], new CodeInstruction[0]);
            VerifyRejected(null, new CodeInstruction[0]);
            VerifyRejected(validPattern, null);
            VerifyRejected(new Func<CodeInstruction, bool>[] { null }, new CodeInstruction[0]);
            VerifyRejected(validPattern, new CodeInstruction[0]);
        }
        private static void VerifyRejected(Func<CodeInstruction, bool>[] pattern, CodeInstruction[] replacement) {
            var transpiler = new FluentTranspiler();
            Exception failure = null;
            var worker = new Thread(delegate() {
                try { transpiler.ReplaceAllPatterns(pattern, replacement); }
                catch (Exception error) { failure = error; }
            });
            worker.IsBackground = true;
            worker.Start();
            if (!worker.Join(1000)) {
                worker.Abort();
                worker.Join();
                throw new Exception("An empty replacement pattern must finish without looping forever");
            }
            Count++;
            if (failure != null) throw new Exception("Invalid or absent patterns must report a soft failure: " + failure.Message);
            if (!transpiler.UnchangedWithFailure) throw new Exception("Rejected bulk patterns must leave the original instructions intact");
        }
    }
}
