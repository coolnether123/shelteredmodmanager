using System.Text;
using UnityEngine;

namespace ConsoleCommands
{
    public class SceneInfoCommand : ICommand
    {
        public string Name { get { return "scene"; } }
        public string Description { get { return "Lists active GameObjects and their components in the current scene."; } }

        public string Execute(string[] args)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- Scene Info ---");

            GameObject[] allGameObjects = (GameObject[])GameObject.FindObjectsOfType(typeof(GameObject));

            if (allGameObjects == null || allGameObjects.Length == 0)
            {
                sb.AppendLine("No active GameObjects found.");
                return sb.ToString();
            }

            sb.AppendLine(string.Format("Found {0} active GameObjects:", allGameObjects.Length));
            sb.AppendLine("------------------");

            foreach (GameObject go in allGameObjects)
            {
                sb.AppendLine(string.Format("GameObject: {0} (Active: {1})", go.name, go.activeSelf));
                Component[] components = go.GetComponents<Component>();
                if (components != null && components.Length > 0)
                {
                    foreach (Component comp in components)
                    {
                        if (comp != null)
                        {
                            sb.AppendLine(string.Format("  - Component: {0}", comp.GetType().Name));
                        }
                    }
                }
                else
                {
                    sb.AppendLine("  (No components)");
                }
                sb.AppendLine("");
            }

            return sb.ToString();
        }
    }
}
