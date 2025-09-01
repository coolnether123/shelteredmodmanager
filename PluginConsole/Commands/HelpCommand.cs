using System.Text;
using System.Linq;

namespace ConsoleCommands
{
    public class HelpCommand : ICommand
    {
        public string Name => "help";
        public string Description => "Lists all available commands.";

        public string Execute(string[] args)
        {
            var commands = CommandProcessor.Instance.Commands;
            if (commands.Count == 0)
            {
                return "No commands found.";
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Available commands:");

            int maxLength = commands.Values.Max(c => c.Name.Length);

            foreach (var command in commands.Values.OrderBy(c => c.Name))
            {
                stringBuilder.AppendLine(string.Format("  {0}  -  {1}", command.Name.PadRight(maxLength), command.Description));
            }

            return stringBuilder.ToString();
        }
    }
}