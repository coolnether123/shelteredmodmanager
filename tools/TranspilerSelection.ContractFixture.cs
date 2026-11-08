public enum SearchMode { Start, Current, Next }
public partial class FluentTranspiler {
    private readonly CodeMatcher _matcher;
    private readonly List<StackExpectation> _stackExpectations = new List<StackExpectation>();
    private readonly List<StackDeltaExpectation> _stackDeltaExpectations = new List<StackDeltaExpectation>();
    private int _lastStackCheckPos = -1;
    private readonly Dictionary<Label, int> _labelIndexCache = new Dictionary<Label, int>();
    private bool _labelIndexCacheDirty = true;
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
    private void AddSoftFailure(TranspilerDiagnosticCategory category, string message) { Failures++; }
    private void AddWarning(string message) { }
    private void AddNote(string message) { }
    public bool MatchesCall(Type type, string name, MethodInfo method, bool inherited, Type[] genericArguments) { return BuildCallPredicate(type, name, null, genericArguments, inherited)(new CodeInstruction(OpCodes.Call, method)); }
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
        VerifyInheritedCalls();
        VerifyGenericCalls();
        VerifyLocalCapture();
        VerifyLocalLoadEncoding();
        VerifyLocalLabelIndexes();
    }
    private static void VerifyLocalLabelIndexes() {
        var il = new DynamicMethod("labels", typeof(void), Type.EmptyTypes).GetILGenerator();
        var label = il.DefineLabel();
        var target = new CodeInstruction(OpCodes.Ret);
        target.labels.Add(label);
        var t = new FluentTranspiler(new CodeInstruction(OpCodes.Nop), target);
        Check(t.LabelToIndex(label) == 1, "Initial branch target must resolve");
        t.FindSequence(SearchMode.Start, delegate(CodeInstruction i) { return i.opcode == OpCodes.Nop; });
        t.CaptureLocal("0");
        Check(t.LabelToIndex(label) == 2, "CaptureLocal must update cached branch indexes after inserting a load");
        var resolved = new List<CodeInstruction>(t.Instructions())[t.LabelToIndex(label)];
        Check(resolved.opcode == OpCodes.Ret && resolved.labels.Contains(label), "Branch navigation must still select the original target");
    }
    private static void VerifyLocalLoadEncoding() {
        foreach (int index in new[] { 3, 4, 255, 256 }) {
            var t = new FluentTranspiler(new CodeInstruction(OpCodes.Ret));
            t.FindSequence(SearchMode.Start, delegate(CodeInstruction i) { return i.opcode == OpCodes.Ret; });
            t.CaptureLocal(index.ToString());
            var load = new List<CodeInstruction>(t.Instructions())[0];
            var method = new DynamicMethod("loadLocal" + index, typeof(int), Type.EmptyTypes);
            var il = method.GetILGenerator();
            LocalBuilder target = null;
            for (int i = 0; i <= index; i++) target = il.DeclareLocal(typeof(int));
            il.Emit(OpCodes.Ldc_I4, 42);
            il.Emit(OpCodes.Stloc, target);
            if (load.opcode == OpCodes.Ldloc_S) il.Emit(load.opcode, unchecked((byte)Convert.ToInt32(load.operand)));
            else if (load.opcode == OpCodes.Ldloc) il.Emit(load.opcode, unchecked((short)Convert.ToInt32(load.operand)));
            else il.Emit(load.opcode);
            il.Emit(OpCodes.Ret);
            try { Check(((Func<int>)method.CreateDelegate(typeof(Func<int>)))() == 42, "CaptureLocal must load index " + index + " without truncation"); }
            catch (Exception error) { throw new Exception("CaptureLocal index " + index + " emitted " + load.opcode + ": " + error.Message); }
        }
        foreach (string index in new[] { "-1", "65536" }) {
            var t = new FluentTranspiler(new CodeInstruction(OpCodes.Ret));
            t.FindSequence(SearchMode.Start, delegate(CodeInstruction i) { return i.opcode == OpCodes.Ret; });
            t.CaptureLocal(index);
            Check(new List<CodeInstruction>(t.Instructions()).Count == 1 && t.Failures == 1, "An unencodable local index must fail without changing instructions");
        }
    }
    private static void VerifyLocalCapture() {
        var generator = new DynamicMethod("locals", typeof(void), Type.EmptyTypes).GetILGenerator();
        generator.DeclareLocal(typeof(int));
        var local = generator.DeclareLocal(typeof(string));
        foreach (var instruction in new[] { new CodeInstruction(OpCodes.Ldloc_S, local), new CodeInstruction(OpCodes.Ldloc_S, (byte)7), new CodeInstruction(OpCodes.Ldloc_2) }) {
            var t = new FluentTranspiler(instruction);
            t.MatchLoadLocal("captured");
            Check(t.CaptureLocalIndex("captured") == instruction.LocalIndex(), "Named local capture must support all Harmony operand encodings");
        }
        Check(new FluentTranspiler(new CodeInstruction(OpCodes.Nop)).CaptureLocalIndex("missing") == -1, "A missing named local must return the documented unavailable index");
    }
    public static void GenericCall<T>() { }
    private static void VerifyGenericCalls() {
        var t = new FluentTranspiler();
        var call = typeof(TranspilerSelectionContractFixture).GetMethod("GenericCall").MakeGenericMethod(typeof(FirstDomain.Item));
        Check(t.MatchesCall(typeof(TranspilerSelectionContractFixture), "GenericCall", call, false, new[] { typeof(FirstDomain.Item) }), "A closed generic call must match its exact type argument");
        Check(!t.MatchesCall(typeof(TranspilerSelectionContractFixture), "GenericCall", call, false, new[] { typeof(SecondDomain.Item) }), "Closed generic arguments with the same short name must remain distinct");
    }
    private static void VerifyInheritedCalls() {
        var t = new FluentTranspiler();
        var inherited = typeof(CallBase).GetMethod("Inherited");
        Check(t.MatchesCall(typeof(CallDerived), "Inherited", inherited, true, null), "includeInherited must find calls declared on the requested type's base class");
        Check(!t.MatchesCall(typeof(CallDerived), "Inherited", inherited, false, null), "An exact type match must exclude calls declared on its base class");
        Check(!t.MatchesCall(typeof(CallBase), "Own", typeof(CallDerived).GetMethod("Own"), true, null), "Inheritance matching must not include methods available only on a derived type");
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
public class CallBase { public void Inherited() { } }
public class CallDerived : CallBase { public void Own() { } }
public enum TranspilerDiagnosticCategory { Match }
namespace FirstDomain { public sealed class Item { } }
namespace SecondDomain { public sealed class Item { } }
namespace ModAPI.Core { public static class MMLog {
    public static void WriteInfo(string message) { }
    public static void WriteDebug(string message) { }
    public static void WriteWarning(string message) { }
    public static void WriteError(string message) { }
} }
