public enum SearchMode { Start, Current, Next }
public partial class FluentTranspiler {
    private readonly CodeMatcher _matcher;
    private readonly List<StackExpectation> _stackExpectations = new List<StackExpectation>();
    private readonly List<StackDeltaExpectation> _stackDeltaExpectations = new List<StackDeltaExpectation>();
    private int _lastStackCheckPos = -1;
    public int Failures;
    public FluentTranspiler(params CodeInstruction[] instructions) { _matcher = new CodeMatcher(instructions); }
    public bool HasMatch { get { return _matcher.IsValid; } }
    public int CurrentIndex { get { return _matcher.Pos; } }
    public CodeInstruction Current { get { return HasMatch ? _matcher.Instruction : null; } }
    public FluentTranspiler Next() { _matcher.Advance(1); return this; }
    public IEnumerable<CodeInstruction> Instructions() { return _matcher.Instructions(); }
    public FluentTranspiler MatchCall(Type type, string name) {
        return FindSequence(SearchMode.Start, delegate(CodeInstruction instruction) {
            MethodInfo method = instruction.operand as MethodInfo;
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && method != null && method.DeclaringType == type && method.Name == name;
        });
    }
    private void AddSoftFailure(string message) { Failures++; }
    private void AddWarning(string message) { }
    private void AddNote(string message) { }
    public bool HasStackDelta(int index, int start, int end, int delta) {
        return _stackDeltaExpectations.Count > index && _stackDeltaExpectations[index].startIndex == start && _stackDeltaExpectations[index].endIndex == end && _stackDeltaExpectations[index].expectedDelta == delta;
    }
}
public static class TranspilerSelectionContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var t = new FluentTranspiler(new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Ret));
        t.FindSequence(SearchMode.Start, delegate(CodeInstruction instruction) { return instruction.opcode == OpCodes.Ret; });
        Check(t.HasMatch && t.CurrentIndex == 1, "A successful predicate match must select its instruction");
        t.FindSequence(SearchMode.Start, delegate(CodeInstruction instruction) { return instruction.opcode == OpCodes.Ldstr; });
        Check(!t.HasMatch && t.Current == null && t.Failures == 1, "A missing predicate match must clear the previous selection");
        t.FindSequence(SearchMode.Start, delegate(CodeInstruction instruction) { return instruction.opcode == OpCodes.Ldc_I4_1; });
        Check(t.HasMatch && t.CurrentIndex == 0, "A later search from Start must recover after a missing match");
        VerifyLocalization();
        VerifyLogs();
        VerifyCombinators();
        VerifyStackAnchors();
    }
    private static void VerifyStackAnchors() {
        var t = new FluentTranspiler(new CodeInstruction(OpCodes.Nop), new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Pop), new CodeInstruction(OpCodes.Ret));
        t.FindSequence(SearchMode.Start, delegate(CodeInstruction i) { return i.opcode == OpCodes.Nop; });
        t.ExpectStack(0).Next().Next().EnsureStack(1);
        Check(t.HasStackDelta(0, 0, 2, 1), "ExpectStack must anchor the following EnsureStack delta check");
        t.Next().EnsureStack(-1);
        Check(t.HasStackDelta(1, 2, 3, -1), "EnsureStack must anchor the next delta check");
        t.FindSequence(SearchMode.Start, delegate(CodeInstruction i) { return i.opcode == OpCodes.Nop; });
        t.ExpectStack(0).Next().EnsureStack(0);
        Check(t.HasStackDelta(2, 0, 1, 0), "A later ExpectStack must reset the delta anchor");
    }
    private static void VerifyCombinators() {
        Func<FluentTranspiler, FluentTranspiler> first = delegate(FluentTranspiler t) { return t.FindSequence(SearchMode.Current, delegate(CodeInstruction i) { return i.opcode == OpCodes.Ldc_I4_1; }); };
        Func<FluentTranspiler, FluentTranspiler> last = delegate(FluentTranspiler t) { return t.FindSequence(SearchMode.Current, delegate(CodeInstruction i) { return i.opcode == OpCodes.Ret; }); };
        var either = new FluentTranspiler(new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Ret));
        first(either);
        bool fallbackCalled = false;
        either.MatchEither(first, delegate(FluentTranspiler t) { fallbackCalled = true; return last(t); });
        Check(either.HasMatch && either.CurrentIndex == 0 && !fallbackCalled, "MatchEither must accept a first pattern at the current instruction");
        var gap = new FluentTranspiler(new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Nop), new CodeInstruction(OpCodes.Ret));
        first(gap);
        gap.MatchWithGap(first, last, 1);
        Check(!gap.HasMatch, "MatchWithGap must reject a callback that searches beyond the maximum gap");
        gap.FindSequence(SearchMode.Start, delegate(CodeInstruction i) { return i.opcode == OpCodes.Ldc_I4_1; });
        gap.MatchWithGap(first, last, 2);
        Check(gap.HasMatch && gap.CurrentIndex == 2, "MatchWithGap must accept the inclusive maximum gap");
        var same = new FluentTranspiler(new CodeInstruction(OpCodes.Ldc_I4_1));
        first(same);
        same.MatchWithGap(first, first, 0);
        Check(same.HasMatch && same.CurrentIndex == 0, "MatchWithGap must accept a boundary selected without advancing");
        var missing = new FluentTranspiler(new CodeInstruction(OpCodes.Nop));
        missing.FindSequence(SearchMode.Start, delegate(CodeInstruction i) { return i.opcode == OpCodes.Nop; });
        missing.MatchWithGap(first, last, 2);
        Check(!missing.HasMatch, "A missing start boundary must leave no stale selection");
    }
    private static void VerifyLocalization() {
        var get = typeof(Localization).GetMethod("Get");
        var t = new FluentTranspiler(new CodeInstruction(OpCodes.Ldstr, "first"), new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Call, get), new CodeInstruction(OpCodes.Pop), new CodeInstruction(OpCodes.Ldstr, "second"), new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Call, get));
        ShelteredPatterns.MatchUILocalization(t, "second");
        Check(t.HasMatch && t.CurrentIndex == 6, "Localization matching must select the requested key's call");
        ShelteredPatterns.MatchUILocalization(t, "absent");
        Check(!t.HasMatch, "An absent localization key must not select another key's call");
        ShelteredPatterns.MatchUILocalization(t);
        Check(t.HasMatch && t.CurrentIndex == 2, "An unspecified localization key must match the first Get call");
    }
    private static void VerifyLogs() {
        foreach (string name in new[] { "WriteInfo", "WriteDebug", "WriteWarning", "WriteError" }) {
            var t = new FluentTranspiler(new CodeInstruction(OpCodes.Ldstr, "message"), new CodeInstruction(OpCodes.Call, typeof(ModAPI.Core.MMLog).GetMethod(name)));
            ShelteredPatterns.MatchLog(t);
            Check(t.HasMatch && t.CurrentIndex == 1 && t.Failures == 0, "Log matching must accept " + name + " without requiring other log levels");
        }
        var absent = new FluentTranspiler(new CodeInstruction(OpCodes.Ret));
        ShelteredPatterns.MatchLog(absent);
        Check(!absent.HasMatch && absent.Failures == 1, "Log matching must report an absent log call once");
    }
}
public static class Localization { public static string Get(string key, bool suppressWarnings) { return key; } }
namespace ModAPI.Core { public static class MMLog {
    public static void WriteInfo(string message) { }
    public static void WriteDebug(string message) { }
    public static void WriteWarning(string message) { }
    public static void WriteError(string message) { }
} }
