namespace ParalivesAPI.Core
{
    public sealed class ParalivesCompatibilityReport
    {
        public ParalivesCompatibilityReport()
        {
            TargetTypeName = string.Empty;
            RequiredMembers = new string[0];
            FoundMembers = new string[0];
            MissingMembers = new string[0];
        }

        public bool TargetExists { get; internal set; }

        public string TargetTypeName { get; internal set; }

        public string[] RequiredMembers { get; internal set; }

        public string[] FoundMembers { get; internal set; }

        public string[] MissingMembers { get; internal set; }

        public bool IsCompatible
        {
            get { return TargetExists && MissingMembers.Length == 0; }
        }
    }
}
