public struct Vector2 { public static Vector2 zero { get { return new Vector2(); } } }
public struct Vector3 { public static readonly Vector3 zero = new Vector3(); public static Vector3 get_zero() { return zero; } }
public sealed class FluentTranspiler {
    public readonly List<CodeInstruction> Code = new List<CodeInstruction>();
    private int _index = -1;
    public bool HasMatch { get { return _index >= 0 && _index < Code.Count; } }
    public FluentTranspiler Reset() { _index = -1; return this; }
    public FluentTranspiler MatchCall(Type type, string name) { _index = -1; return MatchCallNext(type, name); }
    public FluentTranspiler MatchCallNext(Type type, string name) {
        for (int i = _index + 1; i < Code.Count; i++) {
            var method = Code[i].operand as MethodInfo;
            if (Code[i].opcode == OpCodes.Call && method != null && method.DeclaringType == type && method.Name == name) { _index = i; return this; }
        }
        _index = Code.Count; return this;
    }
    public FluentTranspiler ReplaceWith(OpCode opcode, object operand) { Code[_index] = new CodeInstruction(opcode, operand); return this; }
}
public static class UnityZeroContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var getter = typeof(Vector2).GetProperty("zero").GetGetMethod();
        var instruction = new CodeInstruction(OpCodes.Call, getter);
        var t = new FluentTranspiler(); t.Code.Add(instruction);
        UnityPatterns.ReplaceAllVectorZero(t, typeof(Vector2));
        Check(ReferenceEquals(t.Code[0], instruction), "A property-only Unity zero must preserve its getter instead of emitting a null field load");
        var supported = new FluentTranspiler(); supported.Code.Add(new CodeInstruction(OpCodes.Call, typeof(Vector3).GetMethod("get_zero"))); supported.Code.Add(new CodeInstruction(OpCodes.Call, typeof(Vector3).GetMethod("get_zero"))); UnityPatterns.ReplaceAllVectorZero(supported, typeof(Vector3));
        // Model a Unity build that also exposes a zero field.
        var field = typeof(Vector3).GetField("zero");
        Check(supported.Code.TrueForAll(i => i.opcode == OpCodes.Ldsfld && Equals(i.operand, field)), "Every supported zero getter must become a valid static field load");
        Check(t.Code[0].operand != null, "Bulk vector-zero replacement must retain a valid operand");
    }
}
