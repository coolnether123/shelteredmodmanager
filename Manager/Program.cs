using System;
using System.Collections.Generic; // Added for List, if needed elsewhere, but not strictly for this snippet
using System.Linq; // Added for Linq, if needed elsewhere, but not strictly for this snippet
using System.Windows.Forms;
using System.Reflection; // Added for Assembly
using System.IO; // Added for Path

namespace Manager
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ManagerGUI());
        }


        public static string GameRootPath { get; set; }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Get the name of the assembly that failed to load
            string assemblyName = new AssemblyName(args.Name).Name;

            // Only attempt to resolve if GameRootPath has been set and exists
            if (string.IsNullOrEmpty(GameRootPath) || !File.Exists(GameRootPath))
            {
                return null; // Cannot resolve without a valid game path
            }

            // Construct the path to the Managed folder
            // Assuming GameRootPath is the path to Sheltered.exe
            string gameExeDirectory = Path.GetDirectoryName(GameRootPath);
            string managedPath = Path.Combine(Path.Combine(gameExeDirectory, "ShelteredWindows64_EOS_Data"), "Managed"); // Adjust "ShelteredWindows64_EOS_Data" if needed

            string assemblyPath = Path.Combine(managedPath, assemblyName + ".dll");

            if (File.Exists(assemblyPath))
            {
                // MMLog.Write($"[Manager] Resolving assembly: {assemblyName} from {assemblyPath}");
                return Assembly.LoadFrom(assemblyPath);
            }

            // If not found, return null to let the default resolution continue or fail
            return null;
        }
    }
}
