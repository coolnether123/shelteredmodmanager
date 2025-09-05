using System.Text;
using ConsoleCommands;

namespace PluginConsole.Commands
{
    public class AboutCommand : ICommand
    {
        public string Name => "about";
        public string Description => "Displays information about loaded mods.";

        public string Execute(string[] args)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- Loaded Mods ---");

            if (PluginManager.LoadedMods == null || PluginManager.LoadedMods.Count == 0)
            {
                sb.AppendLine("No mods currently loaded.");
                return sb.ToString();
            }

            foreach (var mod in PluginManager.LoadedMods)
            {
                sb.AppendLine($"  ID: {mod.Id}");
                sb.AppendLine($"  Name: {mod.Name}");
                sb.AppendLine($"  Version: {mod.Version}");
                sb.AppendLine($"  Authors: {(mod.About.authors != null ? string.Join(", ", mod.About.authors) : "N/A")}");
                sb.AppendLine($"  Description: {mod.About.description}");
                sb.AppendLine("-------------------");
            }

            return sb.ToString();
        }
    }
}
