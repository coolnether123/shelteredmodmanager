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

            if (_awaitingConfirmation)
            {
                string normalized = commandName;
                if (ConfirmYesResponses.Contains(normalized))
                {
                    return ConfirmPendingCommand();
                }

                if (ConfirmNoResponses.Contains(normalized))
                {
                    ClearPendingSuggestion();
                    return $"Pending command '{_pendingCommandName}' canceled.";
                }

                return $"Please type 'yes' or 'no' to confirm running '{_pendingCommandName}'.";
            }

            if (_commands.TryGetValue(commandName, out ICommand command))
            {
                ClearPendingSuggestion();
                try
                {
                    return command.Execute(args);
                }
                catch (Exception ex)
                {
                    return string.Format("Error executing command {0}: {1}", command.Name, ex.Message);
                }
            }

            return HandleUnknownCommand(commandName, args);
        }

        private string HandleUnknownCommand(string commandName, string[] args)
        {
            string suggestion = FindBestMatch(commandName);
            if (suggestion != null)
            {
                _pendingCommandName = suggestion;
                _pendingArgs = args;
                _awaitingConfirmation = true;
                return $"Unknown command '{commandName}'. Did you mean '{suggestion}'? Type 'yes' to run it, 'no' to cancel.";
            }

            return $"Unknown command '{commandName}'. Type 'help' for a list of available commands.";
        }

        private string ConfirmPendingCommand()
        {
            if (string.IsNullOrEmpty(_pendingCommandName) || !_commands.TryGetValue(_pendingCommandName, out ICommand pendingCommand))
            {
                ClearPendingSuggestion();
                return $"Pending command is no longer available.";
            }

            string result = "";
            try
            {
                result = pendingCommand.Execute(_pendingArgs);
            }
            catch (Exception ex)
            {
                result = $"Error executing command {pendingCommand.Name}: {ex.Message}";
            }

            ClearPendingSuggestion();
            return result;
        }

        private void ClearPendingSuggestion()
        {
            _awaitingConfirmation = false;
            _pendingCommandName = null;
            _pendingArgs = new string[0];
        }

        private string FindBestMatch(string input)
        {
            string bestMatch = null;
            int bestDistance = int.MaxValue;

            foreach (var commandName in _commands.Keys)
            {
                int distance = ComputeLevenshteinDistance(input, commandName);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = commandName;
                }
            }

            if (bestMatch == null || bestDistance > Math.Max(1, input.Length / 2))
            {
                return null;
            }

            double ratio = (double)bestDistance / Math.Max(input.Length, bestMatch.Length);
            return ratio <= 0.4 ? bestMatch : null;
        }

        private int ComputeLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int[,] d = new int[s.Length + 1, t.Length + 1];

            for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++) d[0, j] = j;

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[s.Length, t.Length];
        }

        private static readonly string[] ConfirmYesResponses = { "yes", "y" };
        private static readonly string[] ConfirmNoResponses = { "no", "n" };

        private string _pendingCommandName;
        private string[] _pendingArgs = new string[0];
        private bool _awaitingConfirmation;
    }
}
