namespace ConsoleCommands
{
    public interface ICommand
    {
        /// <summary>
        /// The name of the command, used to call it from the console.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A short description of what the command does, for use in a help command.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Executes the command's logic.
        /// </summary>
        /// <param name="args">The arguments passed to the command.</param>
        /// <returns>A string result or message to display in the console.</returns>
        string Execute(string[] args);
    }
}