using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class WorkspaceBrowserService : IWorkspaceBrowserService
    {
        public WorkspaceTreeNode BuildTree(string rootPath, WorkspaceTreeKind kind)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return null;
            }

            try
            {
                rootPath = Path.GetFullPath(rootPath);
            }
            catch
            {
                return null;
            }

            var root = new WorkspaceTreeNode
            {
                Name = Path.GetFileName(rootPath),
                FullPath = rootPath,
                RelativePath = string.Empty,
                IsDirectory = true,
                HasChildren = true,
                ChildrenLoaded = true,
                NodeKind = WorkspaceTreeNodeKind.Folder
            };

            PopulateDirectory(root, rootPath, kind);
            return root;
        }

        private static void PopulateDirectory(WorkspaceTreeNode node, string rootPath, WorkspaceTreeKind kind)
        {
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(node.FullPath);
            }
            catch
            {
                directories = new string[0];
            }

            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < directories.Length; i++)
            {
                var directory = directories[i];
                if (ShouldSkipDirectory(directory, kind))
                {
                    continue;
                }

                var child = new WorkspaceTreeNode
                {
                    Name = Path.GetFileName(directory),
                    FullPath = directory,
                    RelativePath = BuildRelativePath(rootPath, directory),
                    IsDirectory = true,
                    HasChildren = true,
                    ChildrenLoaded = true,
                    NodeKind = WorkspaceTreeNodeKind.Folder
                };

                PopulateDirectory(child, rootPath, kind);
                if (child.Children.Count > 0)
                {
                    node.Children.Add(child);
                }
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(node.FullPath);
            }
            catch
            {
                files = new string[0];
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Length; i++)
            {
                if (!ShouldIncludeFile(files[i], kind))
                {
                    continue;
                }

                node.Children.Add(new WorkspaceTreeNode
                {
                    Name = Path.GetFileName(files[i]),
                    FullPath = files[i],
                    RelativePath = BuildRelativePath(rootPath, files[i]),
                    IsDirectory = false,
                    HasChildren = false,
                    ChildrenLoaded = true,
                    NodeKind = WorkspaceTreeNodeKind.File
                });
            }
        }

        private static bool ShouldSkipDirectory(string directory, WorkspaceTreeKind kind)
        {
            var name = Path.GetFileName(directory) ?? string.Empty;
            if (string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return kind == WorkspaceTreeKind.ProjectSource &&
                (string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase));
        }

        private static bool ShouldIncludeFile(string filePath, WorkspaceTreeKind kind)
        {
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

        private static string BuildRelativePath(string rootPath, string fullPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return fullPath;
            }

            var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            return fullPath.Substring(normalizedRoot.Length);
        }
    }
}
