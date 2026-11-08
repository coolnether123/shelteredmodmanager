public enum SearchMode { Start, Current, Next }
public enum RangeComparisonKind { Lower, Upper }
public enum FluentReplacementResult { Failed, UnsafeMatch, PatternReplaced, NoMatch, AmbiguousMatch }
public sealed class FluentTranspiler {
    public readonly List<CodeInstruction> Code = new List<CodeInstruction>();
    public int CurrentIndex;
    public IEnumerable<CodeInstruction> Instructions() { return Code; }
    public void AddWarning(string value) { }
    public void AddSoftFailure(string value) { }
    public FluentTranspiler ReplaceAt(int index, CodeInstruction value) { Code[index] = value; return this; }
    public FluentTranspiler ReplaceAtWithCall(int index, MethodInfo method) { return ReplaceAt(index, new CodeInstruction(OpCodes.Call, method)); }
}
public static class FluentTranspilerRecipeValidation {
    public static bool ValidateIntCompatibleReturn(FluentTranspiler t, MethodInfo m, string caller) { return m != null && m.IsStatic && m.ReturnType == typeof(int); }
    public static bool ValidateParameterCount(FluentTranspiler t, MethodInfo m, int count, string caller) { return m.GetParameters().Length == count; }
}
public static class FluentRecipeUtility {
    public static int GetSearchStartIndex(FluentTranspiler t, SearchMode mode) { return mode == SearchMode.Start ? 0 : t.CurrentIndex + (mode == SearchMode.Next ? 1 : 0); }
    public static List<int> BuildMeaningfulIndex(List<CodeInstruction> code, int start) { return Enumerable.Range(start, code.Count - start).Where(i => code[i].opcode != OpCodes.Nop).ToList(); }
    public static bool TryGetLdcI4Value(CodeInstruction i, out int value) { value = i.opcode == OpCodes.Ldc_I4 ? (int)i.operand : 0; return i.opcode == OpCodes.Ldc_I4; }
    public static bool IsLoadArgument(this CodeInstruction i, int index) { return index == 0 && i.opcode == OpCodes.Ldarg_0; }
}
public static class BoundReplacementContractFixture {
    public static int Count;
    private static void Check(bool result, string message) { Count++; if (!result) throw new Exception(message); }
    public static int Provider() { return int.MaxValue; }
    private static FluentTranspiler Create(OpCode branch, int value) {
        var t = new FluentTranspiler();
        t.Code.Add(new CodeInstruction(OpCodes.Ldarg_0));
        t.Code.Add(new CodeInstruction(OpCodes.Ldc_I4, value));
        t.Code.Add(new CodeInstruction(branch));
        return t;
    }
    private static bool Evaluate(FluentTranspiler t, int input) {
        var method = new DynamicMethod("bound", typeof(bool), new[] { typeof(int) }, typeof(BoundReplacementContractFixture).Module, true);
        var il = method.GetILGenerator(); var target = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        if (t.Code[1].opcode == OpCodes.Call) il.Emit(OpCodes.Call, (MethodInfo)t.Code[1].operand);
        else il.Emit(OpCodes.Ldc_I4, (int)t.Code[1].operand);
        il.Emit(t.Code[2].opcode, target);
        il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret);
        il.MarkLabel(target); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Ret);
        return ((Func<int, bool>)method.CreateDelegate(typeof(Func<int, bool>)))(input);
    }
    public static void Verify() {
        foreach (var branch in new[] { OpCodes.Blt, OpCodes.Blt_S, OpCodes.Bge, OpCodes.Bge_S }) {
            bool inRangeBranch = branch == OpCodes.Blt || branch == OpCodes.Blt_S;
            var t = Create(branch, 5);
            Check(new FluentBoundCheckSelection(t, 0, RangeComparisonKind.Upper, 4).ReplaceBoundWithConstant(8) == FluentReplacementResult.PatternReplaced, "An exclusive comparison must match its normalized upper bound");
            Check(Evaluate(t, 8) == inRangeBranch && Evaluate(t, 9) != inRangeBranch, "Replacing upper bound 4 with 8 must include 8 and exclude 9");
            var provider = Create(branch, 5);
            new FluentBoundCheckSelection(provider, 0, RangeComparisonKind.Upper, 4).ReplaceBoundWithCall(typeof(BoundReplacementContractFixture).GetMethod("Provider"));
            Check(Evaluate(provider, int.MaxValue) == inRangeBranch, "Provider upper bounds must support int.MaxValue without overflow");
        }
        var lower = Create(OpCodes.Blt, 5);
        new FluentBoundCheckSelection(lower, 0, RangeComparisonKind.Lower, 5).ReplaceBoundWithConstant(2);
        Check(Evaluate(lower, 1) && !Evaluate(lower, 2), "Lower-bound replacement must retain its original branch sense");
        var overflow = Create(OpCodes.Blt, int.MinValue);
        Check(new FluentBoundCheckSelection(overflow, 0, RangeComparisonKind.Upper, int.MaxValue).ReplaceBoundWithConstant(4) == FluentReplacementResult.NoMatch, "Subtracting one from int.MinValue must not invent an upper-bound match");
    }
}
