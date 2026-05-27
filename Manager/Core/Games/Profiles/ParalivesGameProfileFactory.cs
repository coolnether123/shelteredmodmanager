using Manager.Core.Games.Models;
using Manager.Core.Games.Saves;

namespace Manager.Core.Games.Profiles
{
    public static class ParalivesGameProfileFactory
    {
        public const string ProfileId = "paralives";

        public static GameProfile Create()
        {
            GameRuntimeLayout layout = new GameRuntimeLayout();
            layout.RuntimeFolderName = "SMM";
            layout.BinFolderName = "bin";
            layout.ModsFolderName = "mods";

            GameProfile profile = new GameProfile();
            profile.Id = ProfileId;
            profile.DisplayName = "Paralives";
            profile.ManagerTitle = "Paralives Mod Manager";
            profile.LocateExecutableTitle = "Locate Paralives.exe";
            profile.ExecutableDialogFilter = "Paralives Executable|Paralives.exe|All Executables|*.exe";
            profile.DefaultNexusGameDomain = string.Empty;
            profile.DefaultManagerNexusModId = 0;
            profile.SteamAppId = "1118520";
            profile.SteamCommonFolderName = "Paralives";
            profile.ExecutableNames = new string[] { "Paralives.exe" };
            profile.CommonInstallDirectories = new string[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Paralives",
                @"C:\Program Files\Steam\steamapps\common\Paralives"
            };
            profile.ApiAssemblies = new GameApiAssembly[]
            {
                new GameApiAssembly("ModAPI", true),
                new GameApiAssembly("ParalivesAPI", false),
                new GameApiAssembly("ModAPI.Networking", false)
            };
            profile.RequiredRuntimeFiles = new RuntimeFileRequirement[]
            {
                new RuntimeFileRequirement("winhttp.dll (in game folder)", @"winhttp.dll"),
                new RuntimeFileRequirement("SMM/Doorstop.dll", @"SMM\Doorstop.dll", @"SMM\bin\Doorstop.dll"),
                new RuntimeFileRequirement("SMM/ModAPI.dll", @"SMM\ModAPI.dll"),
                new RuntimeFileRequirement("SMM/bin/ParalivesAPI.dll", @"SMM\bin\ParalivesAPI.dll"),
                new RuntimeFileRequirement("SMM/bin/ModAPI.Networking.dll", @"SMM\bin\ModAPI.Networking.dll")
            };
            profile.LogFileRelativePaths = new string[]
            {
                @"SMM\mod_manager.log",
                @"mod_manager.log"
            };
            profile.RuntimeLayout = layout;
            profile.SaveDiscovery = new NoOpSaveDiscoveryStrategy();
            profile.SupportsSaveDiscovery = false;
            profile.AboutContent = CreateAboutContent();
            return profile;
        }

        private static GameAboutContent CreateAboutContent()
        {
            GameAboutContent content = new GameAboutContent();
            content.Title = "Paralives Mod Manager";
            content.IssuesUrl = "https://github.com/coolnether123/shelteredmodmanager/issues";
            content.NexusGameLinkText = "Nexus Mods";
            content.NexusManagerLinkText = "Manager on Nexus";
            content.Description =
                "This profile uses the shared ModAPI loader shell for Paralives.\n\n" +
                "Core features:\n" +
                "- Plugin discovery, dependency validation, and load order management.\n" +
                "- Doorstop-based launch from the Paralives install folder.\n" +
                "- Paralives-specific runtime code is isolated in ParalivesAPI.dll.\n\n" +
                "Save-slot parsing and higher-level game APIs can be added to the dedicated Paralives runtime assembly.";
            content.Credits =
                "- Coolnether123: 2025 maintenance and development.\n" +
                "- benjaminfoo: Original 2019 mod loader foundation.\n" +
                "- NeighTools: UnityDoorstop injection framework.\n" +
                "- Andreas Pardeike: Harmony runtime patching library.";
            return content;
        }
    }
}
