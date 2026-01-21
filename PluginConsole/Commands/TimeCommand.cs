using System;
using System.Reflection;
using System.Text;

namespace ConsoleCommands
{
    public class TimeCommand : ICommand
    {
        public string Name => "time";
        public string Description => "Gets or sets the game time. Usage: time [add|set] [days]";

        public string Execute(string[] args)
        {
            if (args.Length == 0)
            {
                return $"Current day is {GameTime.Day}.";
            }

            if (args.Length < 2)
            {
                return "Invalid arguments. Usage: time <add|set> <days>";
            }

            string subCommand = args[0].ToLower();
            if (!int.TryParse(args[1], out int days))
            {
                return "Invalid number of days.";
            }

            switch (subCommand)
            {
                case "add":
                    return AddDays(days);
                case "set":
                    return SetDay(days);
                default:
                    return "Invalid sub-command. Use 'add' or 'set'.";
            }
        }

        private string AddDays(int days)
        {
            FieldInfo dayField = typeof(GameTime).GetField("current_day", BindingFlags.NonPublic | BindingFlags.Static);
            if (dayField != null)
            {
                int currentDay = (int)dayField.GetValue(null);
                dayField.SetValue(null, currentDay + days);
                return $"Advanced time by {days} days. New day is {GameTime.Day}.";
            }
            return "Could not advance time.";
        }

        private string SetDay(int day)
        {
            FieldInfo dayField = typeof(GameTime).GetField("current_day", BindingFlags.NonPublic | BindingFlags.Static);
            if (dayField != null)
            {
                dayField.SetValue(null, day);
                return $"Set current day to {day}.";
            }
            return "Could not set day.";
        }
    }
}
