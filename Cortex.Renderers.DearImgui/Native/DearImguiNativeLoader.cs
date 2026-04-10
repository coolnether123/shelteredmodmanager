using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Cortex.Core.Diagnostics;

namespace Cortex.Renderers.DearImgui.Native
{
    internal static class DearImguiNativeLoader
    {
        private static readonly CortexLogger Log = CortexLog.ForSource("Cortex.DearImgui");
        private static readonly object SyncRoot = new object();
        private static IntPtr _moduleHandle;
        private static bool _loadAttempted;
        private static string _loadedPath = string.Empty;

        public static void EnsureLoaded()
        {
            if (_moduleHandle != IntPtr.Zero)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_moduleHandle != IntPtr.Zero)
                {
                    return;
                }

                if (_loadAttempted)
                {
                    throw new DllNotFoundException("cimgui.dll could not be loaded from the Cortex Dear ImGui runtime folder.");
                }

                _loadAttempted = true;
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDirectory = !string.IsNullOrEmpty(assemblyLocation)
                    ? Path.GetDirectoryName(assemblyLocation)
                    : string.Empty;
                var candidatePath = !string.IsNullOrEmpty(assemblyDirectory)
                    ? Path.Combine(assemblyDirectory, "cimgui.dll")
                    : string.Empty;
                if (string.IsNullOrEmpty(candidatePath) || !File.Exists(candidatePath))
                {
                    throw new DllNotFoundException("cimgui.dll was not found next to Cortex.Renderers.DearImgui.dll. Expected path: " + (candidatePath ?? string.Empty));
                }

                LogEnvironment(assemblyLocation, assemblyDirectory, candidatePath);
                ApplyNativeSearchPath(assemblyDirectory);
                _moduleHandle = LoadLibrary(candidatePath);
                if (_moduleHandle == IntPtr.Zero)
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    var message = new Win32Exception(errorCode).Message;
                    throw new DllNotFoundException("Failed to load cimgui.dll from " + candidatePath + ". Win32Error=" + errorCode + " (" + message + ").");
                }

                _loadedPath = candidatePath;
                Log.WriteInfo("Preloaded cimgui.dll from " + candidatePath + ".");
                LogNameResolutionProbe("cimgui");
                LogNameResolutionProbe("cimgui.dll");
                LogExportProbe(_moduleHandle, "igCreateContext");
            }
        }

        public static string DescribeState()
        {
            return "LoadAttempted=" + _loadAttempted +
                ", Handle=" + _moduleHandle +
                ", LoadedPath=" + (_loadedPath ?? string.Empty) + ".";
        }

        public static IntPtr GetRequiredExport(string exportName)
        {
            if (string.IsNullOrEmpty(exportName))
            {
                throw new ArgumentException("An export name is required.", "exportName");
            }

            EnsureLoaded();

            var export = GetProcAddress(_moduleHandle, exportName);
            if (export != IntPtr.Zero)
            {
                return export;
            }

            var errorCode = Marshal.GetLastWin32Error();
            throw new EntryPointNotFoundException(
                "Failed to resolve export '" + exportName + "' from cimgui.dll. Win32Error=" + errorCode + " (" + new Win32Exception(errorCode).Message + ").");
        }

        private static void ApplyNativeSearchPath(string assemblyDirectory)
        {
            if (string.IsNullOrEmpty(assemblyDirectory))
            {
                return;
            }

            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (currentPath.IndexOf(assemblyDirectory, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Environment.SetEnvironmentVariable("PATH", assemblyDirectory + Path.PathSeparator + currentPath);
                Log.WriteInfo("Prepended Dear ImGui runtime directory to PATH: " + assemblyDirectory + ".");
            }

            if (!SetDllDirectory(assemblyDirectory))
            {
                var errorCode = Marshal.GetLastWin32Error();
                Log.WriteWarning("SetDllDirectory failed for " + assemblyDirectory + ". Win32Error=" + errorCode + ".");
            }
        }

        private static void LogEnvironment(string assemblyLocation, string assemblyDirectory, string candidatePath)
        {
            Log.WriteInfo(
                "Loader environment. " +
                "AssemblyLocation=" + (assemblyLocation ?? string.Empty) +
                ", AssemblyDirectory=" + (assemblyDirectory ?? string.Empty) +
                ", CandidatePath=" + (candidatePath ?? string.Empty) +
                ", CurrentDirectory=" + (Environment.CurrentDirectory ?? string.Empty) +
                ", AppBase=" + (AppDomain.CurrentDomain.BaseDirectory ?? string.Empty) + ".");
        }

        private static void LogNameResolutionProbe(string libraryName)
        {
            if (string.IsNullOrEmpty(libraryName))
            {
                return;
            }

            var handle = LoadLibrary(libraryName);
            if (handle == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                Log.WriteWarning("LoadLibrary probe failed for '" + libraryName + "'. Win32Error=" + errorCode + " (" + new Win32Exception(errorCode).Message + ").");
                return;
            }

            Log.WriteInfo("LoadLibrary probe succeeded for '" + libraryName + "'. Handle=" + handle + ".");
        }

        private static void LogExportProbe(IntPtr moduleHandle, string exportName)
        {
            if (moduleHandle == IntPtr.Zero || string.IsNullOrEmpty(exportName))
            {
                return;
            }

            var export = GetProcAddress(moduleHandle, exportName);
            if (export == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                Log.WriteWarning("GetProcAddress failed for '" + exportName + "'. Win32Error=" + errorCode + " (" + new Win32Exception(errorCode).Message + ").");
                return;
            }

            Log.WriteInfo("GetProcAddress succeeded for '" + exportName + "'. Address=" + export + ".");
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
