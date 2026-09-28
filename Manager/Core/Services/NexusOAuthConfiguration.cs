namespace Manager.Core.Services
{
    /// <summary>
    /// Public-client registration values approved by Nexus Mods. The desktop
    /// client uses PKCE and never embeds the issued client secret.
    /// </summary>
    internal static class NexusOAuthConfiguration
    {
        internal const string ClientId = "sheltered_mod_manager";
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
