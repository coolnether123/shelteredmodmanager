public enum SearchMode { Start, Current, Next }
public enum FluentReplacementResult { NoMatch, PatternReplaced, UnsafeMatch }
public static class FluentTranspilerFormatting { public static string FormatMethod(MethodInfo method) { return method.Name; } public static string FormatField(FieldInfo field) { return field.Name; } }
public sealed class FluentTranspiler {
    private readonly List<CodeInstruction> _instructions;
    private readonly DynamicMethod _method = new DynamicMethod("guard_fixture", typeof(void), Type.EmptyTypes);
    private int _index;
    public FluentTranspiler(params CodeInstruction[] instructions) { _instructions = instructions.ToList(); }
    public int CurrentIndex { get { return _index; } }
    public IEnumerable<CodeInstruction> Instructions() { return _instructions; }
    public void AddWarning(string message) { }
    public void AddSoftFailure(string message) { }
    public FluentTranspiler DefineLabel(out Label label) { label = _method.GetILGenerator().DefineLabel(); return this; }
    public FluentTranspiler MoveTo(int index) { _index = index; return this; }
    public FluentTranspiler ReplaceSequence(int count, params CodeInstruction[] replacement) { _instructions.RemoveRange(_index, count); _instructions.InsertRange(_index, replacement); return this; }
    public void Execute() {
        var generator = _method.GetILGenerator();
        foreach (var instruction in _instructions) {
            foreach (var label in instruction.labels) generator.MarkLabel(label);
            if (instruction.operand is MethodInfo) generator.Emit(instruction.opcode, (MethodInfo)instruction.operand);
            else if (instruction.operand is Label) generator.Emit(instruction.opcode, (Label)instruction.operand);
            else if (instruction.operand is Type) generator.Emit(instruction.opcode, (Type)instruction.operand);
            else generator.Emit(instruction.opcode);
        }
        ((Action)_method.CreateDelegate(typeof(Action)))();
    }
}
public static class TranspilerGuardContractFixture {
    public static int Count;
    private static int _calls;
    private static bool _enabled;
    public static bool Enabled() { return _enabled; }
    public static void Original() { _calls++; }
    public static void WithArgument(int value) { _calls += value; }
    public static int WithResult() { return 1; }
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var argument = typeof(TranspilerGuardContractFixture).GetMethod("WithArgument");
        var valueCall = typeof(TranspilerGuardContractFixture).GetMethod("WithResult");
        var prefixed = new FluentTranspiler(new CodeInstruction(OpCodes.Tailcall), new CodeInstruction(OpCodes.Call, typeof(TranspilerGuardContractFixture).GetMethod("Original")), new CodeInstruction(OpCodes.Ret));
        var prefixResult = new FluentCallGuardSelection(prefixed, typeof(TranspilerGuardContractFixture).GetMethod("Original"), SearchMode.Start).SkipOriginalWhen(delegate(FluentGuardBuilder guard) { guard.RequireCallTrue(typeof(TranspilerGuardContractFixture).GetMethod("Enabled")); });
        Check(prefixResult == FluentReplacementResult.UnsafeMatch && prefixed.Instructions().Count() == 3, "A call prefix must not be separated from its call by guard instructions");
        foreach (var call in new[] { argument, valueCall }) {
            var original = new CodeInstruction(OpCodes.Call, call);
            var unsafeCall = new FluentTranspiler(new CodeInstruction(OpCodes.Ldc_I4_1), original, new CodeInstruction(OpCodes.Ret));
            bool configured = false;
            var rejected = new FluentCallGuardSelection(unsafeCall, call, SearchMode.Start).SkipOriginalWhen(delegate(FluentGuardBuilder guard) { configured = true; guard.RequireCallTrue(typeof(TranspilerGuardContractFixture).GetMethod("Enabled")); });
            Check(rejected == FluentReplacementResult.UnsafeMatch && !configured && unsafeCall.Instructions().Count() == 3 && ReferenceEquals(unsafeCall.Instructions().ElementAt(1), original), "Unsupported call inputs or outputs must leave the instruction stream unchanged");
        }
        foreach (bool enabled in new[] { false, true }) {
            _calls = 0; _enabled = enabled;
            var input = new FluentTranspiler(new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Call, argument), new CodeInstruction(OpCodes.Ret));
            var applied = new FluentCallGuardSelection(input, argument, SearchMode.Start).IncludingPreviousInstruction().SkipOriginalWhen(delegate(FluentGuardBuilder guard) { guard.RequireCallTrue(typeof(TranspilerGuardContractFixture).GetMethod("Enabled")); });
            input.Execute();
            Check(applied == FluentReplacementResult.PatternReplaced && _calls == (enabled ? 0 : 1), "Including one argument load must leave both guard paths balanced");
        }
        foreach (bool enabled in new[] { false, true }) {
            _calls = 0; _enabled = enabled;
            var t = new FluentTranspiler(new CodeInstruction(OpCodes.Call, typeof(TranspilerGuardContractFixture).GetMethod("Original")), new CodeInstruction(OpCodes.Ret));
            var result = new FluentCallGuardSelection(t, typeof(TranspilerGuardContractFixture).GetMethod("Original"), SearchMode.Start).SkipOriginalWhen(delegate(FluentGuardBuilder guard) { guard.RequireCallTrue(typeof(TranspilerGuardContractFixture).GetMethod("Enabled")); });
            t.Execute();
            Check(result == FluentReplacementResult.PatternReplaced && _calls == (enabled ? 0 : 1), "RequireCallTrue must skip only when its condition holds");
        }
    }
}
