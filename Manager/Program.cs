using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Reflection;
using System.IO;
using System.Threading;

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
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += Application_ThreadException;
                    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                }
                catch { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try
                {
                    MessageBox.Show(
                        "Manager failed to start:\n\n" + ex.Message + "\n\n" +
                        "If this persists, ensure .NET Framework 3.5 is installed.",
                        "Sheltered Mod Manager",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch { }
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    "An unexpected error occurred:\n\n" + e.Exception.Message + "\n\n" + e.Exception.StackTrace,
                    "Sheltered Mod Manager - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch { }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = e.ExceptionObject as Exception;
                string msg;
                if (ex != null)
                {
                    msg = ex.Message + "\n\n" + ex.StackTrace;
                }
                else if (e.ExceptionObject != null)
                {
                    msg = e.ExceptionObject.ToString();
                }
                else
                {
                    msg = "Unknown error";
                }

                MessageBox.Show(
                    "An unexpected error occurred (non-UI):\n\n" + msg,
                    "Sheltered Mod Manager - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch { }
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
                byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
                return Assembly.Load(assemblyBytes);
            }

            // If not found, return null to let the default resolution continue or fail
            return null;
        }
    }
}
