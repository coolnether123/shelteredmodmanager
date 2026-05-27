namespace Manager.Core.Games.Models
{
    public sealed class GameAboutContent
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Credits { get; set; }
        public string IssuesUrl { get; set; }
        public string NexusGameUrl { get; set; }
        public string NexusManagerUrl { get; set; }
        public string NexusGameLinkText { get; set; }
        public string NexusManagerLinkText { get; set; }

        public GameAboutContent()
        {
            Title = string.Empty;
            Description = string.Empty;
            Credits = string.Empty;
            IssuesUrl = string.Empty;
            NexusGameUrl = string.Empty;
            NexusManagerUrl = string.Empty;
            NexusGameLinkText = "Nexus Mods";
            NexusManagerLinkText = "Manager on Nexus";
        }
    }
}
