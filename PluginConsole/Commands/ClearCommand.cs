namespace ConsoleCommands
{
    public class ClearCommand : ICommand
    {
        public string Name { get { return "clear"; } }
        public string Description { get { return "Clears the console output."; } }

        public string Execute(string[] args)
        {
            return "##CLEAR##"; // Special signal for the UI to clear the screen
        }
    }
}