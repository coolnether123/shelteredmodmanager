using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class CommandRegistry : ICommandRegistry
    {
        private readonly Dictionary<string, CommandDefinition> _commands;
        private readonly Dictionary<string, CommandHandlerRegistration> _handlers;

        public CommandRegistry()
        {
            _commands = new Dictionary<string, CommandDefinition>(StringComparer.OrdinalIgnoreCase);
            _handlers = new Dictionary<string, CommandHandlerRegistration>(StringComparer.OrdinalIgnoreCase);
        }

        public void Register(CommandDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.CommandId))
            {
                return;
            }

            _commands[definition.CommandId] = definition;
        }

        public void RegisterHandler(string commandId, CommandHandler handler, CommandEnablement canExecute)
        {
            if (string.IsNullOrEmpty(commandId) || handler == null)
            {
                return;
            }

            _handlers[commandId] = new CommandHandlerRegistration
            {
                CommandId = commandId,
                Handler = handler,
                CanExecute = canExecute
            };
        }

        public CommandDefinition Get(string commandId)
        {
            if (string.IsNullOrEmpty(commandId))
            {
                return null;
            }

            CommandDefinition definition;
            return _commands.TryGetValue(commandId, out definition) ? definition : null;
        }

        public IList<CommandDefinition> GetAll()
        {
            var results = new List<CommandDefinition>(_commands.Values);
            results.Sort(delegate(CommandDefinition left, CommandDefinition right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public bool CanExecute(string commandId, CommandExecutionContext context)
        {
            if (string.IsNullOrEmpty(commandId))
            {
                return false;
            }

            CommandHandlerRegistration registration;
            if (!_handlers.TryGetValue(commandId, out registration) || registration == null || registration.Handler == null)
            {
                return false;
            }

            return registration.CanExecute == null || registration.CanExecute(context);
        }

        public bool Execute(string commandId, CommandExecutionContext context)
        {
            if (!CanExecute(commandId, context))
            {
                return false;
            }

            _handlers[commandId].Handler(context);
            return true;
        }
    }
}
