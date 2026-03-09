using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class SourceLookupIndexService : ISourceLookupIndex
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, RootFileIndex> _rootIndexes = new Dictionary<string, RootFileIndex>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WorkspaceTreeNode> _treeCache = new Dictionary<string, WorkspaceTreeNode>(StringComparer.OrdinalIgnoreCase);

        public void RefreshRoot(string rootPath)
        {
            var normalizedRoot = NormalizeDirectory(rootPath);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                return;
            }

            var index = BuildIndex(normalizedRoot);
            lock (_sync)
            {
                _rootIndexes[normalizedRoot] = index;
                RemoveCachedTrees_NoLock(normalizedRoot);
            }
        }

        public void RefreshRoots(IList<string> rootPaths)
        {
            if (rootPaths == null)
            {
                return;
            }

            for (var i = 0; i < rootPaths.Count; i++)
            {
                RefreshRoot(rootPaths[i]);
            }
        }

        public string ResolvePath(IList<string> searchRoots, string rawPath)
        {
            var normalizedPath = NormalizeLookupPath(rawPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return string.Empty;
            }

            var directPath = TryResolveDirectPath(normalizedPath);
            if (!string.IsNullOrEmpty(directPath))
            {
                return directPath;
            }

            var fileName = SafeGetFileName(normalizedPath);
            if (string.IsNullOrEmpty(fileName))
            {
                return string.Empty;
            }

            var roots = NormalizeRoots(searchRoots);
            for (var i = 0; i < roots.Count; i++)
            {
                var match = ResolvePathWithinRoot(roots[i], normalizedPath, fileName);
                if (!string.IsNullOrEmpty(match))
                {
                    return match;
                }
            }

            return string.Empty;
        }

        public string ResolveAssemblyPath(IList<string> searchRoots, string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return string.Empty;
            }

            var fileName = assemblyName.Trim() + ".dll";
            var roots = NormalizeRoots(searchRoots);
            for (var i = 0; i < roots.Count; i++)
            {
                var rootPath = roots[i];
                if (File.Exists(rootPath))
                {
                    if (string.Equals(SafeGetFileName(rootPath), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return rootPath;
                    }

                    continue;
                }

                var combined = Path.Combine(rootPath, fileName);
                if (File.Exists(combined))
                {
                    return Path.GetFullPath(combined);
                }

                var index = GetOrBuildIndex(rootPath);
                if (index == null)
                {
                    continue;
                }

                string assemblyPath;
                if (index.AssemblyFiles.TryGetValue(assemblyName, out assemblyPath))
                {
                    return assemblyPath;
                }

                List<string> matches;
                if (index.FilesByName.TryGetValue(fileName, out matches) && matches.Count > 0)
                {
                    return matches[0];
                }
            }

            return string.Empty;
        }

        public IList<string> GetProjectFiles(string rootPath)
        {
            var index = GetOrBuildIndex(rootPath);
            if (index == null)
            {
                return new List<string>();
            }

            return new List<string>(index.ProjectFiles);
        }

        public WorkspaceTreeNode BuildTree(string rootPath, WorkspaceTreeKind kind)
        {
            var normalizedRoot = NormalizeDirectory(rootPath);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                return null;
            }

            var cacheKey = BuildTreeCacheKey(normalizedRoot, kind);
            lock (_sync)
            {
                WorkspaceTreeNode cached;
                if (_treeCache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }

            var index = GetOrBuildIndex(normalizedRoot);
            if (index == null)
            {
                return null;
            }

            var tree = BuildTreeFromIndex(index, kind);
            lock (_sync)
            {
                _treeCache[cacheKey] = tree;
            }

            return tree;
        }

        private string ResolvePathWithinRoot(string rootPath, string normalizedPath, string fileName)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return string.Empty;
            }

            if (File.Exists(rootPath))
            {
                return string.Equals(SafeGetFileName(rootPath), fileName, StringComparison.OrdinalIgnoreCase)
                    ? rootPath
                    : string.Empty;
            }

            var combined = Path.Combine(rootPath, normalizedPath);
            if (File.Exists(combined))
            {
                return Path.GetFullPath(combined);
            }

            var index = GetOrBuildIndex(rootPath);
            if (index == null)
            {
                return string.Empty;
            }

            var relativeMatch = FindRelativePathMatch(index, normalizedPath);
            if (!string.IsNullOrEmpty(relativeMatch))
            {
                return relativeMatch;
            }

            List<string> nameMatches;
            if (index.FilesByName.TryGetValue(fileName, out nameMatches) && nameMatches.Count > 0)
            {
                return nameMatches[0];
            }

            return string.Empty;
        }

        private static string FindRelativePathMatch(RootFileIndex index, string rawPath)
        {
            if (index == null || string.IsNullOrEmpty(rawPath))
            {
                return string.Empty;
            }

            var normalizedRelative = NormalizeRelativePath(rawPath);
            if (string.IsNullOrEmpty(normalizedRelative))
            {
                return string.Empty;
            }

            List<string> directMatches;
            if (index.FilesByRelativePath.TryGetValue(normalizedRelative, out directMatches) && directMatches.Count > 0)
            {
                return directMatches[0];
            }

            for (var i = 0; i < index.Files.Count; i++)
            {
                var indexedRelative = index.Files[i].RelativePath;
                if (PathEndsWith(indexedRelative, normalizedRelative) || PathEndsWith(normalizedRelative, indexedRelative))
                {
                    return index.Files[i].FullPath;
                }
            }

            return string.Empty;
        }

        private RootFileIndex GetOrBuildIndex(string rootPath)
        {
            var normalizedRoot = NormalizeDirectory(rootPath);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                return null;
            }

            lock (_sync)
            {
                RootFileIndex existing;
                if (_rootIndexes.TryGetValue(normalizedRoot, out existing))
                {
                    return existing;
                }
            }

            var built = BuildIndex(normalizedRoot);
            lock (_sync)
            {
                RootFileIndex existing;
                if (_rootIndexes.TryGetValue(normalizedRoot, out existing))
                {
                    return existing;
                }

                _rootIndexes[normalizedRoot] = built;
                return built;
            }
        }

        private static RootFileIndex BuildIndex(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return null;
            }

            var index = new RootFileIndex(rootPath);
            var pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(current);
                }
                catch
                {
                    directories = new string[0];
                }

                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                for (var i = directories.Length - 1; i >= 0; i--)
                {
                    if (!ShouldSkipDirectory(directories[i]))
                    {
                        pending.Push(directories[i]);
                    }
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(current);
                }
                catch
                {
                    files = new string[0];
                }

                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < files.Length; i++)
                {
                    var fullPath = files[i];
                    var relativePath = BuildRelativePath(rootPath, fullPath);
                    var entry = new IndexedFileEntry(fullPath, relativePath);
                    index.Files.Add(entry);
                    AddPath(index.FilesByName, entry.FileName, entry.FullPath);
                    AddPath(index.FilesByRelativePath, entry.RelativePath, entry.FullPath);

                    if (string.Equals(Path.GetExtension(entry.FullPath), ".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        var assemblyName = Path.GetFileNameWithoutExtension(entry.FullPath);
                        if (!string.IsNullOrEmpty(assemblyName) && !index.AssemblyFiles.ContainsKey(assemblyName))
                        {
                            index.AssemblyFiles[assemblyName] = entry.FullPath;
                        }
                    }

                    if (string.Equals(Path.GetExtension(entry.FullPath), ".csproj", StringComparison.OrdinalIgnoreCase) &&
                        !IsBuildArtifact(entry.FullPath))
                    {
                        index.ProjectFiles.Add(entry.FullPath);
                    }
                }
            }

            index.Files.Sort(CompareIndexedFiles);
            index.ProjectFiles.Sort(StringComparer.OrdinalIgnoreCase);
            return index;
        }

        private static int CompareIndexedFiles(IndexedFileEntry left, IndexedFileEntry right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            return string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase);
        }

        private static WorkspaceTreeNode BuildTreeFromIndex(RootFileIndex index, WorkspaceTreeKind kind)
        {
            if (index == null)
            {
                return null;
            }

            var rootName = Path.GetFileName(index.RootPath);
            if (string.IsNullOrEmpty(rootName))
            {
                rootName = index.RootPath;
            }

            var root = new WorkspaceTreeNode
            {
                Name = rootName,
                FullPath = index.RootPath,
                RelativePath = string.Empty,
                IsDirectory = true,
                HasChildren = false,
                ChildrenLoaded = true,
                NodeKind = WorkspaceTreeNodeKind.Folder
            };

            var directories = new Dictionary<string, WorkspaceTreeNode>(StringComparer.OrdinalIgnoreCase);
            directories[string.Empty] = root;

            for (var i = 0; i < index.Files.Count; i++)
            {
                var entry = index.Files[i];
                if (!ShouldIncludeFile(entry.FullPath, kind))
                {
                    continue;
                }

                var segments = entry.RelativePath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                {
                    continue;
                }

                var parent = root;
                var relativeDirectory = string.Empty;
                for (var segmentIndex = 0; segmentIndex < segments.Length - 1; segmentIndex++)
                {
                    relativeDirectory = string.IsNullOrEmpty(relativeDirectory)
                        ? segments[segmentIndex]
                        : relativeDirectory + "\\" + segments[segmentIndex];

                    WorkspaceTreeNode directoryNode;
                    if (!directories.TryGetValue(relativeDirectory, out directoryNode))
                    {
                        directoryNode = new WorkspaceTreeNode
                        {
                            Name = segments[segmentIndex],
                            FullPath = Path.Combine(index.RootPath, relativeDirectory),
                            RelativePath = relativeDirectory,
                            IsDirectory = true,
                            HasChildren = true,
                            ChildrenLoaded = true,
                            NodeKind = WorkspaceTreeNodeKind.Folder
                        };

                        directories[relativeDirectory] = directoryNode;
                        parent.Children.Add(directoryNode);
                        parent.HasChildren = true;
                    }

                    parent = directoryNode;
                }

                parent.Children.Add(new WorkspaceTreeNode
                {
                    Name = segments[segments.Length - 1],
                    FullPath = entry.FullPath,
                    RelativePath = entry.RelativePath,
                    IsDirectory = false,
                    HasChildren = false,
                    ChildrenLoaded = true,
                    NodeKind = WorkspaceTreeNodeKind.File
                });
                parent.HasChildren = true;
            }

            SortTree(root);
            root.HasChildren = root.Children.Count > 0;
            return root;
        }

        private static void SortTree(WorkspaceTreeNode node)
        {
            if (node == null || node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            node.Children.Sort(CompareWorkspaceNodes);
            for (var i = 0; i < node.Children.Count; i++)
            {
                SortTree(node.Children[i]);
            }
        }

        private static int CompareWorkspaceNodes(WorkspaceTreeNode left, WorkspaceTreeNode right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            if (left.IsDirectory != right.IsDirectory)
            {
                return left.IsDirectory ? -1 : 1;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipDirectory(string directoryPath)
        {
            var name = Path.GetFileName(directoryPath) ?? string.Empty;
            return string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldIncludeFile(string filePath, WorkspaceTreeKind kind)
        {
            if (kind == WorkspaceTreeKind.ProjectSource && IsBuildArtifact(filePath))
            {
                return false;
            }

            var extension = Path.GetExtension(filePath) ?? string.Empty;
            if (kind == WorkspaceTreeKind.DecompiledCache)
            {
                return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".map", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
        }

        private static string TryResolveDirectPath(string rawPath)
        {
            try
            {
                if (Path.IsPathRooted(rawPath) && File.Exists(rawPath))
                {
                    return Path.GetFullPath(rawPath);
                }

                if (File.Exists(rawPath))
                {
                    return Path.GetFullPath(rawPath);
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static void AddPath(IDictionary<string, List<string>> map, string key, string value)
        {
            if (map == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            {
                return;
            }

            List<string> values;
            if (!map.TryGetValue(key, out values))
            {
                values = new List<string>();
                map[key] = values;
            }

            if (!ContainsPath(values, value))
            {
                values.Add(value);
            }
        }

        private static string BuildRelativePath(string rootPath, string fullPath)
        {
            if (string.IsNullOrEmpty(rootPath) || string.IsNullOrEmpty(fullPath))
            {
                return string.Empty;
            }

            var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeRelativePath(fullPath);
            }

            return NormalizeRelativePath(fullPath.Substring(normalizedRoot.Length));
        }

        private static string NormalizeLookupPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Trim().Trim('"').Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static string NormalizeRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim();
            if (normalized.Length > 1 && normalized[1] == ':')
            {
                normalized = normalized.Substring(2);
            }

            return normalized.TrimStart(Path.DirectorySeparatorChar);
        }

        private static string NormalizeDirectory(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return string.Empty;
            }

            try
            {
                var fullPath = Path.GetFullPath(rootPath.Trim());
                return Directory.Exists(fullPath) ? fullPath : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static List<string> NormalizeRoots(IList<string> roots)
        {
            var normalized = new List<string>();
            if (roots == null)
            {
                return normalized;
            }

            for (var i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                try
                {
                    var fullPath = Path.GetFullPath(root.Trim());
                    if ((Directory.Exists(fullPath) || File.Exists(fullPath)) && !ContainsPath(normalized, fullPath))
                    {
                        normalized.Add(fullPath);
                    }
                }
                catch
                {
                }
            }

            return normalized;
        }

        private static bool PathEndsWith(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return left.EndsWith(Path.DirectorySeparatorChar + right, StringComparison.OrdinalIgnoreCase);
        }

        private void RemoveCachedTrees_NoLock(string rootPath)
        {
            var keysToRemove = new List<string>();
            foreach (var pair in _treeCache)
            {
                if (pair.Key.StartsWith(rootPath + "|", StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < keysToRemove.Count; i++)
            {
                _treeCache.Remove(keysToRemove[i]);
            }
        }

        private static string BuildTreeCacheKey(string rootPath, WorkspaceTreeKind kind)
        {
            return rootPath + "|" + (int)kind;
        }

        private static string SafeGetFileName(string path)
        {
            try
            {
                return Path.GetFileName(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsBuildArtifact(string path)
        {
            return path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsPath(IList<string> values, string candidate)
        {
            if (values == null || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RootFileIndex
        {
            public readonly string RootPath;
            public readonly List<IndexedFileEntry> Files = new List<IndexedFileEntry>();
            public readonly Dictionary<string, List<string>> FilesByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<string>> FilesByRelativePath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, string> AssemblyFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> ProjectFiles = new List<string>();

            public RootFileIndex(string rootPath)
            {
                RootPath = rootPath;
            }
        }

        private sealed class IndexedFileEntry
        {
            public readonly string FullPath;
            public readonly string RelativePath;
            public readonly string FileName;

            public IndexedFileEntry(string fullPath, string relativePath)
            {
                FullPath = fullPath;
                RelativePath = relativePath;
                FileName = SafeGetFileName(fullPath);
            }
        }
    }
}
