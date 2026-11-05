using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ModAPI.Decompiler;

internal static class Program
{
    private static int assertions;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Main(string[] args)
    {
        // Inspect the game assembly without loading it into the runtime or writing to it.
        using var stream = File.OpenRead(args[0]);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var methods = 0;
        foreach (var handle in reader.MethodDefinitions)
        {
            var token = MetadataTokens.GetToken(handle);
            var actual = MetadataTokens.MethodDefinitionHandle(token);
            Check(actual == handle, "Full token changed the method handle.");
            Check(reader.GetString(reader.GetMethodDefinition(actual).Name) ==
                  reader.GetString(reader.GetMethodDefinition(handle).Name),
                  "Full token changed the method name.");
            methods++;
        }

        var first = reader.MethodDefinitions.First();
        var privacy = new PrivacyInspector().Check(args[0], MetadataTokens.GetToken(first));
        Check(privacy.MethodName.Length > 0, "Real assembly privacy probe failed.");

        var bodyMethod = reader.MethodDefinitions.First(handle =>
            reader.GetMethodDefinition(handle).RelativeVirtualAddress != 0);
        var artifact = new DecompilerEngine().Decompile(args[0], MetadataTokens.GetToken(bodyMethod));
        Check(artifact.SourceCode.Length > 0 && artifact.ILBytes.Length > 0,
              "Real assembly decompile probe failed.");

        Console.WriteLine("Real decompile token: 0x" + MetadataTokens.GetToken(bodyMethod).ToString("X8"));
        Console.WriteLine("Real game token probe: " + methods + " methods");
        Console.WriteLine("Decompiler regression assertions passed: " + assertions);
    }
}
