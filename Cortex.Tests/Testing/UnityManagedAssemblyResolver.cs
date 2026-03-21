using System;
using System.IO;
using System.Reflection;

namespace Cortex.Tests.Testing
{
    internal static class UnityManagedAssemblyResolver
    {
        private const string ManagedAssemblyRoot = @"D:\Epic Games\Sheltered\ShelteredWindows64_EOS_Data\Managed";
        private static bool _registered;

        public static void Run(Action action)
        {
            EnsureRegistered();
            if (action != null)
            {
                action();
            }
        }

        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += ResolveManagedAssembly;
            _registered = true;
        }

        private static Assembly ResolveManagedAssembly(object sender, ResolveEventArgs args)
        {
            var requestedName = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(requestedName))
            {
                return null;
            }

            var candidatePath = Path.Combine(ManagedAssemblyRoot, requestedName + ".dll");
            return File.Exists(candidatePath)
                ? Assembly.LoadFrom(candidatePath)
                : null;
        }
    }
}
