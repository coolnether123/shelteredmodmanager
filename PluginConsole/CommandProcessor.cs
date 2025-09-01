using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ConsoleCommands
{
    public class CommandProcessor
    {
        public static CommandProcessor Instance { get; private set; }
        public IDictionary<string, ICommand> Commands { get { return _commands; } }

        private readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>();

        public CommandProcessor()
        {
            Instance = this;
            RegisterCommands();
        }

        private void RegisterCommands()
        {
            var commandTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(ICommand).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in commandTypes)
            {
                ICommand command = Activator.CreateInstance(type) as ICommand;
                if (command != null)
                {
                    _commands[command.Name.ToLower()] = command;
                }
            }
        }

        public string ProcessCommand(string inputLine)
        {
            if (inputLine == null || inputLine.Trim() == "")
            {
                return "";
            }

            string[] parts = inputLine.Split(' ');
            string commandName = parts[0].ToLower();
            string[] args = parts.Length > 1 ? parts.Skip(1).ToArray() : new string[0];

            if (_commands.TryGetValue(commandName, out ICommand command))
            {
                try
                {
                    return command.Execute(args);
                }
                catch (Exception ex)
                {
                    return string.Format("Error executing command {0}: {1}", command.Name, ex.Message);
                }
            }

            return string.Format("Unknown command: '{0}'", commandName);
        }
    }
}