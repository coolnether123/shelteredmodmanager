public static class ReplacementTypeIdentityContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    private static Type CreateType(string assemblyName) {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName);
        return module.DefineType("SharedName.Payload", TypeAttributes.Public).CreateType();
    }
    private static MethodInfo CreateMethod(Type value, string name, Type input) {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule(name).DefineType(name, TypeAttributes.Public);
        var method = type.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static, value, new[] { input });
        var il = method.GetILGenerator(); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Ret);
        return type.CreateType().GetMethod("Read");
    }
    public static void Verify() {
        var first = CreateType("FirstPayload"); var second = CreateType("SecondPayload");
        var original = CreateMethod(first, "OriginalPayloadMethod", first);
        var otherInput = CreateMethod(first, "OtherInputPayloadMethod", second);
        var otherOutput = CreateMethod(second, "OtherOutputPayloadMethod", first);
        string reason;
        Check(!FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(original, otherInput, out reason), "Matching full names in different assemblies must not make call parameters compatible");
        Check(!FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(original, otherOutput, out reason), "Matching full names in different assemblies must not make return types compatible");
        Check(FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(original, CreateMethod(first, "BaseInputPayloadMethod", typeof(object)), out reason), "Assignable base-class parameters must remain supported");
        Check(FluentTranspilerCompatibilityExtensions.IsCompatibleCallReplacement(original, original, out reason), "Identical runtime types must remain supported");
    }
}
