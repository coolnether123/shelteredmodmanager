using Manager.Core.Games.Models;

namespace Manager.Core.Games.Profiles
{
    public static class GenericUnityGameProfileFactory
    {
        public const string ProfileId = "generic-unity";

        public static GameProfile Create()
        {
            GameRuntimeLayout layout = new GameRuntimeLayout();
            layout.RuntimeFolderName = "SMM";
            layout.BinFolderName = "bin";
            layout.ModsFolderName = "mods";

            GameProfile profile = new GameProfile();
            profile.Id = ProfileId;
            profile.DisplayName = "Generic Unity Game";
            profile.ManagerTitle = "Mod Manager";
            profile.LocateExecutableTitle = "Locate game executable";
            profile.ExecutableDialogFilter = "Game Executable|*.exe|All Executables|*.exe";
            profile.DefaultNexusGameDomain = string.Empty;
            profile.DefaultManagerNexusModId = 0;
            profile.ExecutableNames = new string[0];
            profile.ApiAssemblies = new GameApiAssembly[]
            {
                new GameApiAssembly("ModAPI", true)
            };
            profile.RequiredRuntimeFiles = new RuntimeFileRequirement[]
            {
                new RuntimeFileRequirement("winhttp.dll (in game folder)", @"winhttp.dll"),
                new RuntimeFileRequirement("SMM/Doorstop.dll", @"SMM\Doorstop.dll", @"SMM\bin\Doorstop.dll"),
                new RuntimeFileRequirement("SMM/ModAPI.dll", @"SMM\ModAPI.dll")
            };
            profile.LogFileRelativePaths = new string[]
            {
                @"SMM\mod_manager.log",
                @"mod_manager.log"
            };
            profile.RuntimeLayout = layout;
            profile.SupportsSaveDiscovery = false;
            profile.AboutContent = CreateAboutContent();
            return profile;
        }

        private static GameAboutContent CreateAboutContent()
        {
            GameAboutContent content = new GameAboutContent();
            content.Title = "Mod Manager";
            content.IssuesUrl = "https://github.com/coolnether123/shelteredmodmanager/issues";
            content.NexusGameLinkText = "Nexus Mods";
            content.NexusManagerLinkText = "Manager on Nexus";
            content.Description =
                "This profile uses the shared desktop mod-manager shell for Unity games that do not have a dedicated game integration yet.\n\n" +
                "Core features:\n" +
                "- Plugin discovery, dependency validation, and load order management.\n" +
                "- Nexus browsing and update checks when a Nexus game domain is configured.\n" +
                "- Doorstop-based launch using the same runtime layout as bundled profiles.\n\n" +
                "Game-specific behavior such as save-slot parsing, custom APIs, and branded links belongs in a dedicated game profile.";
            content.Credits =
                "- Coolnether123: 2025 maintenance and development.\n" +
                "- benjaminfoo: Original 2019 mod loader foundation.\n" +
                "- NeighTools: UnityDoorstop injection framework.\n" +
                "- Andreas Pardeike: Harmony runtime patching library.";
            return content;
        }
    }
}
