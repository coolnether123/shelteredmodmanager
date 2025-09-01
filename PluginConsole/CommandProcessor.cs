using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

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
            string logPath;
            try
            {
                var baseDir = UnityEngine.Application.persistentDataPath;
                if (string.IsNullOrEmpty(baseDir)) baseDir = Directory.GetCurrentDirectory();
                Directory.CreateDirectory(baseDir);
                logPath = Path.Combine(baseDir, "mod_manager.log");
            }
            catch { logPath = Path.Combine(Directory.GetCurrentDirectory(), "mod_manager.log"); }

            void Log(string msg)
            {
                try
                {
                    using (var tw = File.AppendText(logPath))
                        tw.WriteLine($"[{DateTime.Now:HH:mm:ss}] CommandProcessor: {msg}");
                }
                catch { /* don’t crash logging */ }
                try { UnityEngine.Debug.Log($"[CommandProcessor] {msg}"); } catch { }
            }

            Log("RegisterCommands: begin");

            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            Log($"Assemblies loaded: {allAssemblies.Length}");
            foreach (var asm in allAssemblies.OrderBy(a => a.GetName().Name))
            {
                try { Log($"  - {asm.FullName} @ {asm.Location}"); }
                catch { Log($"  - {asm.FullName} @ <no location>"); }
            }

            var discovered = new List<Type>();

            foreach (var asm in allAssemblies)
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (typeof(ICommand).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        {
                            discovered.Add(t);
                            Log($"Found ICommand: {t.FullName}");
                        }
                    }
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    foreach (var t in (rtle.Types ?? new Type[0]))
                    {
                        if (t != null && typeof(ICommand).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        {
                            discovered.Add(t);
                            Log($"Found ICommand (RTLE): {t.FullName}");
                        }
                    }
                    if (rtle.LoaderExceptions != null)
                        foreach (var e in rtle.LoaderExceptions)
                            Log($"LoaderException: {e.Message}");
                }
                catch (Exception ex)
                {
                    Log($"Assembly scan failed for {asm.FullName}: {ex.Message}");
                }
            }

            foreach (var type in discovered.Distinct())
            {
                try
                {
                    var cmd = (ICommand)Activator.CreateInstance(type);
                    _commands[cmd.Name.ToLower()] = cmd;
                    Log($"Registered: {cmd.Name} ({type.FullName})");
                }
                catch (Exception ex)
                {
                    Log($"Instantiate failed for {type.FullName}: {ex}");
                }
            }

            // Safety net (keeps console usable even if discovery fails)
            if (!_commands.ContainsKey("help")) _commands["help"] = new HelpCommand();
            if (!_commands.ContainsKey("clear")) _commands["clear"] = new ClearCommand();
            if (!_commands.ContainsKey("scene")) _commands["scene"] = new SceneInfoCommand();

            Log($"RegisterCommands: done. Total={_commands.Count}");
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
