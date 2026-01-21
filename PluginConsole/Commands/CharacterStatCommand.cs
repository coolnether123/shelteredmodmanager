using System;
using System.Linq;
using System.Collections.Generic; 
using UnityEngine;
using ModAPI.Core;

// Assuming these are the correct namespaces for the decompiled game classes.
// You might need to adjust these based on the actual decompiled project structure.
// If a class is in the global namespace, no 'using' is strictly required for it.
// Example: using Sheltered.Game.Characters;
// Example: using Sheltered.Game.Managers;



namespace ConsoleCommands
{
    public class CharacterStatCommand : ICommand
    {
        public string Name => "character_stat";
        public string Description => "Manipulates character statistics. Usage: character_stat <character_name> <stat_type> <action> <value>";

        public string Execute(string[] args)
        {
            // If called with no parameters, list available options.
            if (args.Length == 0)
            {
                var lines = new List<string>();

                // Characters
                List<string> characterNames = new List<string>();
                try
                {
                    if (FamilyManager.Instance != null)
                    {
                        foreach (var member in FamilyManager.Instance.GetAllFamilyMembers())
                        {
                            if (member != null && !string.IsNullOrEmpty(member.firstName))
                            {
                                characterNames.Add(member.firstName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MMLog.Write($"Warning: Failed to enumerate family members: {ex.Message}");
                }

                characterNames = characterNames.Distinct(StringComparer.OrdinalIgnoreCase)
                                               .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                                               .ToList();

                lines.Add("Available characters: " + (characterNames.Count > 0 ? string.Join(", ", characterNames.ToArray()) : "<none>"));

                // Stat types supported by this command
                var statTypes = new[] { "health", "max_health", "strength", "dexterity", "intelligence", "charisma", "perception" };
                lines.Add("Supported stat_types: " + string.Join(", ", statTypes));

                // Actions
                lines.Add("Actions for health/max_health: set, add, subtract");
                lines.Add("Actions for core stats (strength, dexterity, intelligence, charisma, perception): set (level), add (experience)");
                lines.Add("Usage: character_stat <character_name> <stat_type> <action> <value>");

                string message = string.Join("\n", lines.ToArray());
                MMLog.Write(message);
                return message;
            }

            if (args.Length < 4)
            {
                MMLog.Write("Error: Not enough arguments. Usage: character_stat <character_name> <stat_type> <action> <value>");
                return "Error: Not enough arguments."; // Return a short message for the console
            }

            string characterName = args[0];
            string statTypeString = args[1].ToLower();
            string action = args[2].ToLower();
            int value;

            if (!int.TryParse(args[3], out value))
            {
                MMLog.Write("Error: Invalid value. Value must be an integer.");
                return "Error: Invalid value.";
            }

            // Find the character
            FamilyMember targetCharacter = null;
            if (FamilyManager.Instance != null)
            {
                // Note: This finds the first character with the matching first name.
                // If multiple family members have the same first name, this could be ambiguous.
                // A more robust solution might involve character IDs or full names.
                foreach (var member in FamilyManager.Instance.GetAllFamilyMembers())
                {
                    if (member.firstName.Equals(characterName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetCharacter = member;
                        break;
                    }
                }
            }

            if (targetCharacter == null)
            {
                MMLog.Write($"Error: Character '{characterName}' not found.");
                return $"Error: Character '{characterName}' not found.";
            }

            // Process stat modification
            string resultMessage = "";
            switch (statTypeString)
            {
                case "health":
                    resultMessage = HandleHealthStat(targetCharacter, action, value);
                    break;
                case "max_health":
                    resultMessage = HandleMaxHealthStat(targetCharacter, action, value);
                    break;
                case "strength":
                case "dexterity":
                case "intelligence":
                case "charisma":
                case "perception":
                    resultMessage = HandleCoreStat(targetCharacter, statTypeString, action, value);
                    break;
                // Add cases for other stats like hunger, thirst, loyalty, trauma if direct manipulation methods are found
                // For example, if there's a HungerStat class with a SetValue method:
                // case "hunger":
                //     resultMessage = HandleHungerStat(targetCharacter, action, value);
                //     break;
                default:
                    resultMessage = $"Error: Unknown stat type '{statTypeString}'. Supported types: health, max_health, strength, dexterity, intelligence, charisma, perception.";
                    break;
            }

            MMLog.Write(resultMessage);
            return resultMessage; // Return the message for console display
        }

        private string HandleHealthStat(FamilyMember character, string action, int value)
        {
            switch (action)
            {
                case "set":
                    int currentHealth = character.health;
                    int delta = value - currentHealth;
                    if (delta > 0)
                    {
                        character.Heal(delta);
                    }
                    else if (delta < 0)
                    {
                        character.Damage(Mathf.Abs(delta));
                    }
                    return $"Set {character.firstName}'s health to {character.health}.";
                case "add":
                    character.Heal(value);
                    return $"Added {value} health to {character.firstName}. Current health: {character.health}.";
                case "subtract":
                    character.Damage(value);
                    return $"Subtracted {value} health from {character.firstName}. Current health: {character.health}.";
                default:
                    return $"Error: Invalid action '{action}' for health. Supported actions: set, add, subtract.";
            }
        }

        private string HandleMaxHealthStat(FamilyMember character, string action, int value)
        {
            switch (action)
            {
                case "set":
                    character.SetMaxHealth(value);
                    // Ensure current health doesn't exceed new max health
                    if (character.health > character.maxHealth)
                    {
                        character.Damage(character.health - character.maxHealth);
                    }
                    return $"Set {character.firstName}'s max health to {character.maxHealth}.";
                case "add":
                    character.SetMaxHealth(character.maxHealth + value);
                    return $"Added {value} to {character.firstName}'s max health. Current max health: {character.maxHealth}.";
                case "subtract":
                    character.SetMaxHealth(character.maxHealth - value);
                    // Ensure current health doesn't exceed new max health
                    if (character.health > character.maxHealth)
                    {
                        character.Damage(character.health - character.maxHealth);
                    }
                    return $"Subtracted {value} from {character.firstName}'s max health. Current max health: {character.maxHealth}.";
                default:
                    return $"Error: Invalid action '{action}' for max_health. Supported actions: set, add, subtract.";
            }
        }

        private string HandleCoreStat(FamilyMember character, string statTypeString, string action, int value)
        {
            BaseStats.StatType statType;
            switch (statTypeString)
            {
                case "strength": statType = BaseStats.StatType.Strength; break;
                case "dexterity": statType = BaseStats.StatType.Dexterity; break;
                case "intelligence": statType = BaseStats.StatType.Intelligence; break;
                case "charisma": statType = BaseStats.StatType.Charisma; break;
                case "perception": statType = BaseStats.StatType.Perception; break;
                default: return $"Internal Error: Unhandled stat type '{statTypeString}'."; // Should not happen
            }

            BaseStat stat = character.BaseStats.GetStatByEnum(statType);
            if (stat == null)
            {
                return $"Error: Could not retrieve stat '{statTypeString}' for {character.firstName}.";
            }

            switch (action)
            {
                case "set":
                    // To set a specific level, we need to calculate the experience required for that level.
                    // This assumes BaseStat.ExpLevel is accessible and accurate.
                    // In a real implementation, you'd ideally access the actual ExpLevel array from BaseStat
                    // or a game data manager. For this example, it's hardcoded based on decompiled info.
                    int targetLevel = value;
                    int[] ExpLevel = GetExpLevelArray(); // Get the hardcoded ExpLevel array

                    if (targetLevel < 0 || targetLevel >= ExpLevel.Length)
                    {
                        return $"Error: Invalid level '{targetLevel}'. Level must be between 0 and {ExpLevel.Length - 1}.";
                    }

                    int targetExp = ExpLevel[targetLevel];
                    int currentExp = stat.Exp;
                    int expDelta = targetExp - currentExp;

                    if (expDelta > 0)
                    {
                        stat.IncreaseExp(expDelta);
                        return $"Set {character.firstName}'s {statTypeString} level to {stat.Level} (added {expDelta} experience).";
                    }
                    else if (expDelta < 0)
                    {
                        // Direct subtraction of experience/level is not supported by BaseStat API.
                        // This would require reflection to modify private fields, which is generally discouraged for stability.
                        return $"Error: Cannot directly decrease {statTypeString} level/experience using 'set' with a lower value. Only 'add' is supported for increasing experience.";
                    }
                    else
                    {
                        return $"{character.firstName}'s {statTypeString} is already at the target level/experience.";
                    }

                case "add":
                    stat.IncreaseExp(value);
                    return $"Added {value} experience to {character.firstName}'s {statTypeString}. Current level: {stat.Level}, Current Exp: {stat.Exp}.";
                case "subtract":
                    // Direct subtraction of experience/level is not supported by BaseStat API.
                    // This would require reflection to modify private fields, which is generally discouraged for stability.
                    return $"Error: Cannot subtract experience from {statTypeString}. Only 'add' is supported for increasing experience.";
                default:
                    return $"Error: Invalid action '{action}' for core stats. Supported actions: set (level), add (experience).";
            }
        }

        // Helper method to get the ExpLevel array.
        // In a real mod, you might try to access the actual BaseStat.ExpLevel via reflection
        // or ensure this array is kept in sync with game updates.
        private int[] GetExpLevelArray()
        {
            // This array is hardcoded based on the decompiled BaseStat.ExpLevel array.
            return new int[21]
            {
                0, 100, 200, 400, 600, 900, 1200, 1600, 2000, 2500,
                3000, 3600, 4200, 4900, 5600, 6400, 7200, 8100, 9000, 10000,
                11000
            };
        }
    }
}
