using System;
using System.Collections.Generic;
using System.IO;

namespace Cortex.Core.Services
{
    public static class BundledToolPathResolver
    {
        public static string ResolveFromRelativeRoots(string basePath, IEnumerable<string> relativeRoots, params string[] fileNames)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(basePath) && relativeRoots != null)
            {
                foreach (var relativeRoot in relativeRoots)
                {
                    if (string.IsNullOrEmpty(relativeRoot))
                    {
                        continue;
                    }

                    for (var i = 0; fileNames != null && i < fileNames.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(fileNames[i]))
                        {
                            candidates.Add(Path.Combine(Path.Combine(basePath, relativeRoot), fileNames[i]));
                        }
                    }
                }
            }

            return ResolveCandidate(candidates);
        }

        public static string ResolveFromHostBin(string hostBinPath, string componentId, string legacyFolderName, params string[] fileNames)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(hostBinPath))
            {
                for (var i = 0; fileNames != null && i < fileNames.Length; i++)
                {
                    var fileName = fileNames[i];
                    if (string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(componentId))
                    {
                        candidates.Add(Path.Combine(Path.Combine(Path.Combine(hostBinPath, "tools"), componentId), fileName));
                    }

                    if (!string.IsNullOrEmpty(legacyFolderName))
                    {
                        candidates.Add(Path.Combine(Path.Combine(hostBinPath, legacyFolderName), fileName));
                    }
                }
            }

            return ResolveCandidate(candidates);
        }

        public static string ResolveFromToolRoot(string bundledToolRootPath, string componentId, params string[] fileNames)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(bundledToolRootPath) && !string.IsNullOrEmpty(componentId))
            {
                for (var i = 0; fileNames != null && i < fileNames.Length; i++)
                {
                    var fileName = fileNames[i];
                    if (string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }

                    candidates.Add(Path.Combine(Path.Combine(bundledToolRootPath, componentId), fileName));
                }
            }

            return ResolveCandidate(candidates);
        }

        public static string ResolveCandidate(IEnumerable<string> candidatePaths)
        {
            if (candidatePaths == null)
            {
                return string.Empty;
            }

            foreach (var candidatePath in candidatePaths)
            {
                try
                {
                    if (string.IsNullOrEmpty(candidatePath))
                    {
                        continue;
                    }

                    var fullPath = Path.GetFullPath(candidatePath);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }
    }
}
