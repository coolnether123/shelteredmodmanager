namespace ModAPI.Decompiler
{
    public sealed class DecompilerService
    {
        private readonly DecompilerEngine _engine = new DecompilerEngine();

        public string Decompile(string assemblyPath, int metadataToken, EntityKind entityKind, out string keyMap)
        {
            var artifact = _engine.Decompile(assemblyPath, metadataToken, entityKind);
            keyMap = artifact.GetMapText();
            return artifact.SourceCode;
        }
    }
}
