public enum SearchMode { Start, Current, Next }
public partial class FluentTranspiler {
    private readonly CodeMatcher _matcher;
    public int Failures;
    public FluentTranspiler(params CodeInstruction[] instructions) { _matcher = new CodeMatcher(instructions); }
    public bool HasMatch { get { return _matcher.IsValid; } }
    public int CurrentIndex { get { return _matcher.Pos; } }
    public CodeInstruction Current { get { return HasMatch ? _matcher.Instruction : null; } }
    public FluentTranspiler Next() { _matcher.Advance(1); return this; }
    public IEnumerable<CodeInstruction> Instructions() { return _matcher.Instructions(); }
    private void AddSoftFailure(string message) { Failures++; }
    private void AddWarning(string message) { }
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
    }
}
