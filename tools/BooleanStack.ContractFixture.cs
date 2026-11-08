public static class BooleanStackContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static bool Boolean(bool value) { return value; }
    public static int Integer(int value) { return value; }
    public static void Verify() {
        Check(FluentTranspilerRecipeValidation.CanEmitDefaultValue(typeof(bool)), "Removing a boolean call must support its false default value");
        var instruction = FluentTranspilerRecipeValidation.CreateDefaultValueInstruction(typeof(bool));
        Check(instruction != null && instruction.opcode == OpCodes.Ldc_I4_0, "The boolean default must emit ldc.i4.0");
        var method = new DynamicMethod("false_default", typeof(bool), Type.EmptyTypes);
        var il = method.GetILGenerator(); il.Emit(instruction.opcode); il.Emit(OpCodes.Ret);
        Check(!((Func<bool>)method.CreateDelegate(typeof(Func<bool>)))(), "The emitted boolean default must execute as false");
        var boolean = typeof(BooleanStackContractFixture).GetMethod("Boolean"); var integer = typeof(BooleanStackContractFixture).GetMethod("Integer");
        Check(FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(null, boolean, integer, "fixture"), "Boolean call inputs and returns use the Int32 stack family");
        string reason;
        Check(FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(boolean, integer, out reason), "Compatibility validation must recognize boolean Int32 stack values");
    }
}
