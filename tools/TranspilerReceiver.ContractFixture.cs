public enum TranspilerDiagnosticCategory { Validation }
public sealed class FluentTranspiler { public void AddWarning(TranspilerDiagnosticCategory category, string message) { } }
public static class FluentTranspilerFormatting { public static string FormatMethod(MethodInfo method) { return method.Name; } }
public struct ReceiverCounter { public int Value; public int Read() { return Value; } }
public sealed class ReceiverObject { public int Read() { return 3; } }
public static class TranspilerReceiverContractFixture {
    public static int Count;
    public static int ReadRef(ref ReceiverCounter counter) { return ++counter.Value; }
    public static int ReadValue(ReceiverCounter counter) { return counter.Value; }
    public static int ReadObject(ReceiverObject value) { return value.Read(); }
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var original = typeof(ReceiverCounter).GetMethod("Read");
        var byRef = typeof(TranspilerReceiverContractFixture).GetMethod("ReadRef");
        var byValue = typeof(TranspilerReceiverContractFixture).GetMethod("ReadValue");
        Check(FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(null, original, byRef, "fixture"), "Struct call recipes must accept a managed-reference receiver");
        Check(!FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(null, original, byValue, "fixture"), "Struct call recipes must reject a value receiver that does not match the stack");
        Check(FluentTranspilerRecipeValidation.ValidateHookCanReceiveOriginalArguments(null, original, byRef, "fixture"), "Struct enclosing-method hooks must receive this by reference");
        Check(!FluentTranspilerRecipeValidation.ValidateHookCanReceiveOriginalArguments(null, original, byValue, "fixture"), "Struct enclosing-method hooks must reject this by value");
        string reason;
        Check(FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(original, byRef, out reason), "Compatibility call replacement must accept a managed-reference receiver");
        Check(!FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(original, byValue, out reason), "Compatibility call replacement must reject a value receiver");
        var referenceMethod = typeof(ReceiverObject).GetMethod("Read");
        var referenceHook = typeof(TranspilerReceiverContractFixture).GetMethod("ReadObject");
        Check(FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(null, referenceMethod, referenceHook, "fixture"), "Reference-type receivers must retain their existing call contract");
        Check(FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(referenceMethod, referenceHook, out reason), "Reference-type compatibility receivers must remain valid");
        var method = new DynamicMethod("struct_hook", typeof(int), Type.EmptyTypes);
        var il = method.GetILGenerator();
        var counter = il.DeclareLocal(typeof(ReceiverCounter));
        il.Emit(OpCodes.Ldloca, counter); il.Emit(OpCodes.Initobj, typeof(ReceiverCounter));
        il.Emit(OpCodes.Ldloca, counter); il.Emit(OpCodes.Call, byRef); il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldloca, counter); il.Emit(OpCodes.Call, original); il.Emit(OpCodes.Ret);
        Check(((Func<int>)method.CreateDelegate(typeof(Func<int>)))() == 1, "An accepted struct receiver hook must execute and update the original value");
    }
}
