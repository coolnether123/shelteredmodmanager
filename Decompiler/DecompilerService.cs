namespace ModAPI.Decompiler
{
    public sealed class DecompilerService
    {
        private readonly DecompilerEngine _engine = new DecompilerEngine();

        public string DecompileMethod(string assemblyPath, int methodToken, out string keyMap)
        {
            var artifact = _engine.Decompile(assemblyPath, methodToken);
            keyMap = artifact.GetMapText();
            return artifact.SourceCode;
        }
    }
}
