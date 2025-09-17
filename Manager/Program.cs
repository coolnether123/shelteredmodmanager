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
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

                // Global exception handlers for better crash diagnostics
                try
                {
                    System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
                    System.Windows.Forms.Application.ThreadException += (sender, e) =>
                    {
                        try
                        {
                            System.Windows.Forms.MessageBox.Show(
                                "An unexpected error occurred:\n\n" + e.Exception.Message + "\n\n" + e.Exception.StackTrace,
                                "Sheltered Mod Manager - Error",
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Error);
                        }
                        catch { }
                    };
                    AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    {
                        try
                        {
                            var ex = e.ExceptionObject as Exception;
                            var msg = ex != null ? (ex.Message + "\n\n" + ex.StackTrace) : (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Unknown error");
                            System.Windows.Forms.MessageBox.Show(
                                "An unexpected error occurred (non-UI):\n\n" + msg,
                                "Sheltered Mod Manager - Error",
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Error);
                        }
                        catch { }
                    };
                }
                catch { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ManagerGUI());
            }
            catch (Exception ex)
            {
                try
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Manager failed to start:\n\n" + ex.Message + "\n\n" +
                        "If this persists, ensure .NET Framework 3.5 is installed.",
                        "Sheltered Mod Manager",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                }
                catch { }
            }
        }


        public static string GameRootPath { get; set; }
        public static string GameBitness { get; set; }

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

            // Dynamically determine the Data folder name from the executable name.
            string exeNameWithoutExtension = Path.GetFileNameWithoutExtension(GameRootPath);
            string dataFolder = exeNameWithoutExtension + "_Data";
            string dataFolderPath = Path.Combine(gameExeDirectory, dataFolder);

            string managedPath = Path.Combine(dataFolderPath, "Managed");

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

