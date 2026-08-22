using System;

namespace Manager.Core.Models
{
    public enum NexusDirectDownloadAvailability
    {
        Unknown,
        Available,
        Limited,
        Unavailable
    }

    /// <summary>
    /// User-facing summary of the connected Nexus account and what SMM can likely do with it.
    /// </summary>
    public class NexusAccountStatus
    {
        public bool IsConfigured { get; set; }
        public bool IsConnected { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string[] MembershipRoles { get; set; }
        public string DownloadPreference { get; set; }
        public string DownloadLocation { get; set; }
        public NexusDirectDownloadAvailability DirectDownloadAvailability { get; set; }
        public string Summary { get; set; }
        public string DirectDownloadSummary { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CheckedAtUtc { get; set; }

        public NexusAccountStatus()
        {
            MembershipRoles = new string[0];
            UserName = string.Empty;
            DownloadPreference = string.Empty;
            DownloadLocation = string.Empty;
            Summary = string.Empty;
            DirectDownloadSummary = string.Empty;
            ErrorMessage = string.Empty;
            CheckedAtUtc = DateTime.UtcNow;
        }

        public bool HasRole(string role)
        {
            if (string.IsNullOrEmpty(role) || MembershipRoles == null)
                return false;

            for (int i = 0; i < MembershipRoles.Length; i++)
            {
                if (string.Equals(MembershipRoles[i], role, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public string GetMembershipLabel()
        {
            if (!IsConfigured)
                return "Not connected";

            if (!IsConnected)
                return "Connection failed";

            if (HasRole("premium"))
                return "Premium";

            if (HasRole("supporter"))
                return "Supporter";

            if (HasRole("member"))
                return "Member";

            if (MembershipRoles != null && MembershipRoles.Length > 0)
                return MembershipRoles[0];

            return "Unknown";
        }

        public static NexusAccountStatus CreateNotConfigured()
        {
            return new NexusAccountStatus
            {
                IsConfigured = false,
                IsConnected = false,
                DirectDownloadAvailability = NexusDirectDownloadAvailability.Unavailable,
                Summary = "Not signed in to Nexus.",
                DirectDownloadSummary = "Browse mods and check for updates without signing in. Sign in to install mods from Nexus."
            };
        }

        public static NexusAccountStatus CreateChecking()
        {
            return new NexusAccountStatus
            {
                IsConfigured = true,
                IsConnected = false,
                DirectDownloadAvailability = NexusDirectDownloadAvailability.Unknown,
                Summary = "Checking Nexus account...",
                DirectDownloadSummary = "Checking whether this account can download mods..."
            };
        }

        public static NexusAccountStatus CreateUnavailable(string message)
        {
            return new NexusAccountStatus
            {
                IsConfigured = true,
                IsConnected = false,
                DirectDownloadAvailability = NexusDirectDownloadAvailability.Unknown,
                Summary = "Could not verify the Nexus account.",
                DirectDownloadSummary = "We could not confirm whether this account can download mods.",
                ErrorMessage = message ?? string.Empty
            };
        }
    }
}
