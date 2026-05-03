using System;
using System.IO;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Game-neutral filesystem roots used by ModAPI framework services.
    /// </summary>
    public static class ModApiPaths
    {
        public static string GameRoot
        {
            get
            {
                try
                {
                    return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                }
                catch
                {
                    return Directory.GetCurrentDirectory();
                }
            }
        }

        public static string ModsRoot
        {
            get
            {
                try
                {
                    string gameRoot = GameRoot;
                    string lower = Path.Combine(gameRoot, "mods");
                    string upper = Path.Combine(gameRoot, "Mods");
                    if (Directory.Exists(lower)) return lower;
                    if (Directory.Exists(upper)) return upper;
                    EnsureDirectory(lower);
                    return lower;
                }
                catch
                {
                    return Path.Combine(Directory.GetCurrentDirectory(), "mods");
                }
            }
        }

        public static string SmmRoot
        {
            get { return Path.Combine(GameRoot, "SMM"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(Path.Combine(SmmRoot, "bin"), "mod_manager.ini"); }
        }

        public static string ManagerOptionsPath
        {
            get { return Path.Combine(Path.Combine(SmmRoot, "bin"), "manager_options.json"); }
        }

        public static string ModApiRoot
        {
            get
            {
                string root = Path.Combine(ModsRoot, "ModAPI");
                EnsureDirectory(root);
                return root;
            }
        }

        public static string UserRoot
        {
            get
            {
                string root = Path.Combine(ModApiRoot, "User");
                EnsureDirectory(root);
                return root;
            }
        }

        public static string LibsRoot
        {
            get
            {
                string root = Path.Combine(Directory.GetCurrentDirectory(), "libs");
                EnsureDirectory(root);
                return root;
            }
        }

        internal static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
