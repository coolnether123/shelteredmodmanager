namespace Manager.Core.Games.Models
{
    public sealed class RuntimeFileRequirement
    {
        public string DisplayName { get; set; }
        public string[] RelativeCandidates { get; set; }

        public RuntimeFileRequirement()
        {
            DisplayName = string.Empty;
            RelativeCandidates = new string[0];
        }

        public RuntimeFileRequirement(string displayName, params string[] relativeCandidates)
        {
            DisplayName = displayName ?? string.Empty;
            RelativeCandidates = relativeCandidates ?? new string[0];
        }
    }
}
