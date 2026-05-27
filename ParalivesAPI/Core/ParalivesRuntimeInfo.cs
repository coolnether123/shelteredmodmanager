namespace ParalivesAPI.Core
{
    public sealed class ParalivesRuntimeInfo
    {
        public const string RegistryId = "GameRuntime.Paralives";

        public static readonly ParalivesRuntimeInfo Current = new ParalivesRuntimeInfo();

        private ParalivesRuntimeInfo()
        {
        }

        public string GameId
        {
            get { return "paralives"; }
        }

        public string DisplayName
        {
            get { return "Paralives"; }
        }
    }
}
