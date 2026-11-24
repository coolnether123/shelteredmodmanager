public sealed class ItemDefinition { public string Id; public int? CustomTypeId; }
public static class ContentOwnerAssemblyResolver { public static string ResolveModId(ItemDefinition item) { return "fixture"; } }
public static partial class ContentKeyContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var first = new ItemDefinition { Id = "fixture.weapon.iron", CustomTypeId = 10001 };
        var second = new ItemDefinition { Id = "fixture.armor.iron", CustomTypeId = 10002 };
        var name = BuildGeneratedLocalizationKey(first, true);
        Check(name != BuildGeneratedLocalizationKey(second, true), "Different registered items with the same leaf ID need distinct localization keys");
        first.Id = "fixture.weapon.a+b"; second.Id = "fixture.weapon.a_b";
        Check(BuildGeneratedLocalizationKey(first, true) != BuildGeneratedLocalizationKey(second, true), "Sanitizing readable key parts must not merge distinct registered item identities");
        first.Id = "fixture.weapon.iron";
        Check(name == BuildGeneratedLocalizationKey(first, true), "An item localization key must remain stable");
        Check(name != BuildGeneratedLocalizationKey(first, false), "Name and description keys must remain distinct");
    }
}
