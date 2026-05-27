namespace Manager.Core.Games.Models
{
    public sealed class GameApiAssembly
    {
        public string Name { get; set; }
        public bool IsRequiredForLaunch { get; set; }

        public GameApiAssembly()
        {
            Name = string.Empty;
        }

        public GameApiAssembly(string name, bool isRequiredForLaunch)
        {
            Name = name ?? string.Empty;
            IsRequiredForLaunch = isRequiredForLaunch;
        }
    }
}
