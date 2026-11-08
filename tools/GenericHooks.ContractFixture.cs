public static class GenericHooksContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Hook<T>() { }
    public static int Provider<T>() { return 3; }
    public static int Wrapper<T>(int value) { return value; }
    public static int Original() { return 1; }
    public static void Verify() {
        var hook = typeof(GenericHooksContractFixture).GetMethod("Hook");
        var provider = typeof(GenericHooksContractFixture).GetMethod("Provider");
        var wrapper = typeof(GenericHooksContractFixture).GetMethod("Wrapper");
        Check(!FluentTranspilerRecipeValidation.ValidateStaticMethod(null, hook, "fixture"), "An open generic hook must be rejected before emitting a call");
        Check(FluentTranspilerRecipeValidation.ValidateStaticMethod(null, hook.MakeGenericMethod(typeof(int)), "fixture"), "Closed generic hooks must remain supported");
        Check(!FluentRecipeUtility.IsStaticParameterlessValueProvider(provider), "An open generic provider must be rejected");
        Check(FluentRecipeUtility.IsStaticParameterlessValueProvider(provider.MakeGenericMethod(typeof(int))), "Closed generic providers must remain supported");
        Check(!FluentRecipeUtility.IsCompatibleUnaryWrapper(wrapper, typeof(int)), "An open generic wrapper must be rejected");
        Check(FluentRecipeUtility.IsCompatibleUnaryWrapper(wrapper.MakeGenericMethod(typeof(int)), typeof(int)), "Closed generic wrappers must remain supported");
        string reason;
        Check(!FluentTranspilerCompatibilityExtensions.IsCompatibleConstantReplacement(provider, typeof(int), out reason), "Compatibility providers must reject open generic methods");
        Check(!FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(typeof(GenericHooksContractFixture).GetMethod("Original"), provider, out reason), "Compatibility replacements must reject open generic methods");
    }
}
