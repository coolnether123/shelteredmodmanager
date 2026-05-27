using Manager.Core.Games.Models;
using Manager.Core.Games.Saves;
using Manager.Core.Models;

namespace Manager.Core.Games.Profiles
{
    public static class ShelteredGameProfileFactory
    {
        public const string ProfileId = "sheltered";

        public static GameProfile Create()
        {
            GameRuntimeLayout layout = new GameRuntimeLayout();
            layout.RuntimeFolderName = "SMM";
            layout.BinFolderName = "bin";
            layout.ModsFolderName = "mods";
            layout.DoorstopTargetAssemblyRelativeCandidates = new string[]
            {
                @"SMM\bin\Doorstop.dll",
                @"SMM\Doorstop.dll"
            };

            GameProfile profile = new GameProfile();
            profile.Id = ProfileId;
            profile.DisplayName = "Sheltered";
            profile.ManagerTitle = "Sheltered Mod Manager";
            profile.LocateExecutableTitle = "Locate Sheltered.exe";
            profile.ExecutableDialogFilter = "Sheltered Executable|Sheltered.exe;ShelteredWindows64_EOS.exe|All Executables|*.exe";
            profile.DefaultNexusGameDomain = "sheltered";
            profile.DefaultManagerNexusModId = 1;
            profile.SteamAppId = "356040";
            profile.SteamCommonFolderName = "Sheltered";
            profile.GogCommonFolderName = "Sheltered";
            profile.ExecutableNames = new string[] { "Sheltered.exe", "ShelteredWindows64_EOS.exe" };
            profile.CommonInstallDirectories = new string[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Sheltered",
                @"C:\Program Files\Steam\steamapps\common\Sheltered",
                @"C:\Program Files (x86)\GOG Galaxy\Games\Sheltered",
                @"C:\Program Files\GOG Galaxy\Games\Sheltered",
                @"C:\GOG Games\Sheltered"
            };
            profile.ApiAssemblies = new GameApiAssembly[]
            {
                new GameApiAssembly("ModAPI", true),
                new GameApiAssembly("ShelteredAPI", true),
                new GameApiAssembly("ModAPI.Networking", true)
            };
            profile.RequiredRuntimeFiles = new RuntimeFileRequirement[]
            {
                new RuntimeFileRequirement("winhttp.dll (in game folder)", @"winhttp.dll"),
                new RuntimeFileRequirement("SMM/Doorstop.dll", @"SMM\Doorstop.dll", @"SMM\bin\Doorstop.dll"),
                new RuntimeFileRequirement("SMM/ModAPI.dll", @"SMM\ModAPI.dll"),
                new RuntimeFileRequirement("SMM/bin/ShelteredAPI.dll", @"SMM\ShelteredAPI.dll", @"SMM\bin\ShelteredAPI.dll"),
                new RuntimeFileRequirement("SMM/bin/ModAPI.Networking.dll", @"SMM\bin\ModAPI.Networking.dll")
            };
            profile.LogFileRelativePaths = new string[]
            {
                @"SMM\mod_manager.log",
                @"mod_manager.log"
            };
            profile.RuntimeLayout = layout;
            profile.SupportsSaveDiscovery = true;
            profile.SaveDiscovery = new ShelteredSaveDiscoveryStrategy();
            profile.BuiltInRuntimeOptions = CreateBuiltInRuntimeOptions();
            profile.AboutContent = CreateAboutContent();
            return profile;
        }

        private static ManagerBooleanOptionDefinition[] CreateBuiltInRuntimeOptions()
        {
            return new ManagerBooleanOptionDefinition[]
            {
                new ManagerBooleanOptionDefinition
                {
                    id = "ShelteredAPI.PatchCustomScenarioEditor",
                    owner = "ShelteredAPI",
                    label = "Custom Scenario Editor",
                    description = "Enables ShelteredAPI's custom scenario editor hooks and the Add New Scenario editor entry.",
                    defaultValue = true,
                    requiresRestart = true,
                    sortOrder = 100
                }
            };
        }

        private static GameAboutContent CreateAboutContent()
        {
            GameAboutContent content = new GameAboutContent();
            content.Title = "Sheltered Mod Manager";
            content.IssuesUrl = "https://github.com/coolnether123/shelteredmodmanager/issues";
            content.NexusGameUrl = "https://www.nexusmods.com/games/sheltered";
            content.NexusManagerUrl = "https://www.nexusmods.com/sheltered/mods/1";
            content.NexusGameLinkText = "Nexus Mods - Sheltered";
            content.NexusManagerLinkText = "Sheltered Mod Manager on Nexus";
            content.Description =
                "Sheltered Mod Manager is a modding framework for Sheltered by Unicube and Team17. It installs non-destructively alongside the game and supports Steam/GOG 32-bit builds and Epic 64-bit builds.\n\n" +
                "Core features:\n" +
                "- Plugin loader with dependency resolution and load order management.\n" +
                "- Unlimited save slots for vanilla scenarios with mod tracking and verification.\n" +
                "- Desktop and in-game mod managers.\n" +
                "- Rebindable Sheltered and mod-defined keybindings.\n" +
                "Experimental scenario support:\n" +
                "- Custom scenario browser, XML scenario packs, triggers, scheduled effects, and win/loss runtime support.\n\n" +
                "Developer API:\n" +
                "- ModAPI.dll provides the neutral modding framework surface.\n" +
                "- ShelteredAPI.dll provides Sheltered content, saves, UI, input, events, actors, scenarios, and Harmony integration.\n" +
                "- ModManagerBase, attribute settings, Spine settings UI, isolated persistence, event bus, and runtime inspector (F9).\n\n" +
                "Originally created by benjaminfoo in 2019. Maintained by Coolnether123 from 2025 to present with the original author's permission.";
            content.Credits =
                "- Coolnether123: 2025 maintenance and development.\n" +
                "- benjaminfoo: Original 2019 mod loader foundation.\n" +
                "- Team17: Publisher of Sheltered.\n" +
                "- Unicube: Original game developers.\n" +
                "- NeighTools: UnityDoorstop injection framework.\n" +
                "- Andreas Pardeike: Harmony runtime patching library.";
            return content;
        }
    }
}
