namespace Manager.Core.Services
{
    /// <summary>
    /// Public-client registration values reviewed by Nexus Mods. The client ID
    /// is intentionally empty until Nexus approves and registers the app.
    /// </summary>
    internal static class NexusOAuthConfiguration
    {
        internal const string ClientId = "";
        internal const string RedirectUri = "http://127.0.0.1:52147/callback";
        internal const string AuthorizationEndpoint = "https://users.nexusmods.com/oauth/authorize";
        internal const string TokenEndpoint = "https://users.nexusmods.com/oauth/token";
        internal const int CallbackPort = 52147;
        internal const string CallbackPath = "/callback";

        internal static bool IsRegistered
        {
            get { return !string.IsNullOrEmpty(ClientId); }
        }
    }
}
