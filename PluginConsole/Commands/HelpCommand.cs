using System.Text;
using System.Linq;
using System;


namespace ConsoleCommands
{
    public class HelpCommand : ICommand
    {
        public string Name { get { return "help"; } }
        public string Description { get { return "Lists all available commands."; } }

        public string Execute(string[] args)
        {
            var commands = CommandProcessor.Instance.Commands;
            
            MMLog.Write("HelpCommand: Populating command list..."); // The requested debug line

            if (commands.Count == 0)
            {
                MMLog.Write("HelpCommand: No commands found.");
                return "No commands found.";
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Available commands:");

            // Log the list we're about to print
            MMLog.Write(string.Format("HelpCommand: {0} commands discovered.", commands.Count));
            foreach (var cmd in commands.Values.OrderBy(c => c.Name))
            {
                MMLog.Write(string.Format("HelpCommand:   {0} - {1}", cmd.Name, cmd.Description));
            }

            int maxLength = 0;
            foreach(var command in commands.Values)
            {
                if (command.Name.Length > maxLength)
                {
                    maxLength = command.Name.Length;
                }
            }

            foreach (var command in commands.Values.OrderBy(c => c.Name))
            {
                stringBuilder.AppendLine(string.Format("  {0}  -  {1}", command.Name.PadRight(maxLength), command.Description));
            }

            return stringBuilder.ToString();
        }
    }
}