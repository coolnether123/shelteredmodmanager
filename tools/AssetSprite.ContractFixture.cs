public sealed class Texture2D { public int width = 8, height = 8; }
public struct Rect { public Rect(float x, float y, float w, float h) { } }
public struct Vector2 { public Vector2(float x, float y) { } }
public sealed class Sprite {
    public float PixelsPerUnit;
    public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float scale) { return new Sprite { PixelsPerUnit = scale }; }
}
public static partial class AssetLoader {
    private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private static readonly Texture2D Texture = new Texture2D();
    private static string CacheKey(Assembly assembly, string path) { return assembly.FullName + ":" + path; }
    public static Texture2D LoadTexture(Assembly assembly, string path) { return Texture; }
    public static Texture2D LoadTexture(string root, string path) { return Texture; }
}
public static class AssetSpriteContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var assembly = typeof(AssetSpriteContractFixture).Assembly;
        var assemblySmall = AssetLoader.LoadSprite(assembly, "icon.png", 32f);
        var assemblyLarge = AssetLoader.LoadSprite(assembly, "icon.png", 100f);
        Check(assemblySmall.PixelsPerUnit == 32f && assemblyLarge.PixelsPerUnit == 100f, "Assembly sprite cache must honor each requested pixels-per-unit value");
        Check(ReferenceEquals(assemblySmall, AssetLoader.LoadSprite(assembly, "icon.png", 32f)), "Identical assembly sprite requests must reuse their sprite");
        var rootSmall = AssetLoader.LoadSprite(@"C:\mods\fixture", "icon.png", 32f);
        var rootLarge = AssetLoader.LoadSprite(@"C:\mods\fixture", "icon.png", 100f);
        Check(rootSmall.PixelsPerUnit == 32f && rootLarge.PixelsPerUnit == 100f, "Explicit-root sprite cache must honor each requested pixels-per-unit value");
        Check(ReferenceEquals(rootSmall, AssetLoader.LoadSprite(@"C:\mods\fixture", "icon.png", 32f)), "Identical explicit-root sprite requests must reuse their sprite");
        var culture = CultureInfo.CurrentCulture;
        try {
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
            var french = AssetLoader.LoadSprite(assembly, "fraction.png", 32.5f);
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Check(ReferenceEquals(french, AssetLoader.LoadSprite(assembly, "fraction.png", 32.5f)), "Sprite cache identity must be independent of the current culture");
        } finally { System.Threading.Thread.CurrentThread.CurrentCulture = culture; }
    }
}
