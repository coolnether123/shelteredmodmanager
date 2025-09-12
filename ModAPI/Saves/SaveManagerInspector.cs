using System.Reflection;
using System.Linq;
using ModAPI.Saves;

namespace ModAPI.Hooks
{
    // This is a temporary diagnostic class. 
    // It will be removed once the correct save method is identified.
    internal static class SaveManagerInspector
    {
        internal static void LogMethods()
        {
            MMLog.Write("--- Inspecting SaveManager Methods ---");
            var type = typeof(SaveManager);
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                string parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray());
                MMLog.Write($"Method: {method.ReturnType.Name} {method.Name}({parameters})");
            }
            MMLog.Write("--- End of SaveManager Inspection ---");
        }
    }
}
