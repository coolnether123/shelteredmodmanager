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
    public FluentTranspiler MatchCall(Type type, string name) {
        return FindSequence(SearchMode.Start, delegate(CodeInstruction instruction) {
            MethodInfo method = instruction.operand as MethodInfo;
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && method != null && method.DeclaringType == type && method.Name == name;
        });
    }
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
        VerifyLocalization();
        VerifyLogs();
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
