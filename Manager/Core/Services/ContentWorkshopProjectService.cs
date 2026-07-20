using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Manager.Core.Models;
using ShelteredModManager.ContentPacks;

namespace Manager.Core.Services
{
    /// <summary>
    /// Owns Content Workshop persistence, validation and packaging. The view never
    /// writes project or installation files directly.
    /// </summary>
    public sealed class ContentWorkshopProjectService
    {
        private const string AboutRelativePath = "About/About.json";
        private const string ContentRelativePath = "Content/content-pack.json";
        private const string ReadmeRelativePath = "README.md";
        private static readonly string[] ExportRoots =
            new string[] { "About", "Content", "Assets" };

        public ContentWorkshopProject Create(string rootPath, string modId, string name)
        {
            ContentWorkshopProject project = new ContentWorkshopProject();
            project.RootPath = NormalizeRoot(rootPath);
            project.About.id = (modId ?? string.Empty).Trim();
            project.About.name = (name ?? string.Empty).Trim();
            project.Content.modId = project.About.id;
            project.IsDirty = true;
            return project;
        }

        public ContentWorkshopOperationResult Save(ContentWorkshopProject project)
        {
            string error;
            if (!TryValidateProjectIdentity(project, out error))
                return ContentWorkshopOperationResult.Failed(error);

            ContentPackSerializationResult contentResult =
                ContentPackJsonSerializer.Serialize(project.Content);
            if (!contentResult.Success)
                return ContentWorkshopOperationResult.Failed(contentResult.ErrorMessage);

            try
            {
                EnsureProjectDirectories(project.RootPath);
                JavaScriptSerializer serializer = CreateSerializer();
                WriteAllTextAtomic(
                    ResolveContained(project.RootPath, AboutRelativePath),
                    serializer.Serialize(project.About));
                WriteAllTextAtomic(
                    ResolveContained(project.RootPath, ContentRelativePath),
                    contentResult.Json);

                string readmePath = ResolveContained(project.RootPath, ReadmeRelativePath);
                if (!File.Exists(readmePath))
                    WriteAllTextAtomic(readmePath, CreateReadme(project));

                project.IsDirty = false;
                return ContentWorkshopOperationResult.Succeeded(project.RootPath);
            }
            catch (Exception ex)
            {
                return ContentWorkshopOperationResult.Failed("Project save failed: " + ex.Message);
            }
        }

        public ContentWorkshopOperationResult Open(
            string rootPath,
            out ContentWorkshopProject project)
        {
            project = null;
            try
            {
                string root = NormalizeRoot(rootPath);
                string aboutPath = ResolveContained(root, AboutRelativePath);
                string contentPath = ResolveContained(root, ContentRelativePath);
                if (!File.Exists(aboutPath) || !File.Exists(contentPath))
                {
                    return ContentWorkshopOperationResult.Failed(
                        "Select a mod folder containing About/About.json and Content/content-pack.json.");
                }

                JavaScriptSerializer serializer = CreateSerializer();
                ContentWorkshopAbout about = serializer.Deserialize<ContentWorkshopAbout>(
                    File.ReadAllText(aboutPath));
                ContentPackSerializationResult contentResult =
                    ContentPackJsonSerializer.Deserialize(File.ReadAllText(contentPath));
                if (about == null)
                    return ContentWorkshopOperationResult.Failed("About/About.json is invalid.");
                if (!contentResult.Success)
                    return ContentWorkshopOperationResult.Failed(contentResult.ErrorMessage);

                ContentWorkshopProject loaded = new ContentWorkshopProject();
                loaded.RootPath = root;
                loaded.About = about;
                loaded.Content = contentResult.Document;
                loaded.IsDirty = false;

                string error;
                if (!TryValidateProjectIdentity(loaded, out error))
                    return ContentWorkshopOperationResult.Failed(error);

                project = loaded;
                return ContentWorkshopOperationResult.Succeeded(root);
            }
            catch (Exception ex)
            {
                return ContentWorkshopOperationResult.Failed("Project open failed: " + ex.Message);
            }
        }

        public ContentPackValidationResult Validate(ContentWorkshopProject project, bool requireAssets)
        {
            ContentPackValidationContext context = new ContentPackValidationContext();
            if (project != null)
            {
                context.ExpectedModId = project.ModId;
                context.ModRootPath = project.RootPath;
            }
            context.ValidateAssetFiles = requireAssets;
            return ContentPackValidator.Validate(project == null ? null : project.Content, context);
        }

        public ContentWorkshopOperationResult ImportIcon(
            ContentWorkshopProject project,
            string sourcePngPath,
            string assetName,
            out string relativePath)
        {
            relativePath = null;
            string error;
            if (!TryValidateProjectIdentity(project, out error))
                return ContentWorkshopOperationResult.Failed(error);
            if (string.IsNullOrEmpty(sourcePngPath) || !File.Exists(sourcePngPath))
                return ContentWorkshopOperationResult.Failed("Choose an existing PNG image.");

            string safeName = MakeSafeFileName(assetName);
            if (safeName.Length == 0)
                return ContentWorkshopOperationResult.Failed("The icon name has no valid filename characters.");
            relativePath = "Assets/Icons/" + safeName + ".png";

            try
            {
                using (Image image = Image.FromFile(sourcePngPath))
                {
                    if (image.Width <= 0 || image.Height <= 0 ||
                        image.Width > 2048 || image.Height > 2048)
                    {
                        return ContentWorkshopOperationResult.Failed(
                            "Icon dimensions must be between 1 and 2048 pixels.");
                    }

                    EnsureProjectDirectories(project.RootPath);
                    string destination = ResolveContained(project.RootPath, relativePath);
                    using (Bitmap normalized = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb))
                    using (Graphics graphics = Graphics.FromImage(normalized))
                    {
                        graphics.DrawImageUnscaled(image, 0, 0);
                        SavePngAtomic(normalized, destination);
                    }
                }

                ContentPackAssetValidationResult validation = ContentPackPathPolicy.ValidateIcon(
                    project.RootPath, relativePath, true, 4L * 1024L * 1024L, 2048);
                if (!validation.Success)
                    return ContentWorkshopOperationResult.Failed(validation.ErrorMessage);

                project.IsDirty = true;
                return ContentWorkshopOperationResult.Succeeded(
                    ResolveContained(project.RootPath, relativePath));
            }
            catch (Exception ex)
            {
                return ContentWorkshopOperationResult.Failed("Icon import failed: " + ex.Message);
            }
        }

        public ContentWorkshopOperationResult ExportFolder(
            ContentWorkshopProject project,
            string destinationParent)
        {
            ContentWorkshopOperationResult ready = PrepareForExport(project);
            if (!ready.Success)
                return ready;

            try
            {
                string parent = NormalizeRoot(destinationParent);
                if (!Directory.Exists(parent))
                    Directory.CreateDirectory(parent);
                string target = ResolveContained(parent, project.ModId);
                if (Directory.Exists(target) || File.Exists(target))
                    return ContentWorkshopOperationResult.Failed("Export target already exists: " + target);

                string staging = ResolveContained(
                    parent,
                    "." + project.ModId + ".export-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                try
                {
                    CopyExportFiles(project.RootPath, staging);
                    Directory.Move(staging, target);
                }
                finally
                {
                    TryDeleteDirectory(staging);
                }
                return ContentWorkshopOperationResult.Succeeded(target);
            }
            catch (Exception ex)
            {
                return ContentWorkshopOperationResult.Failed("Folder export failed: " + ex.Message);
            }
        }

        public ContentWorkshopOperationResult ExportZip(
            ContentWorkshopProject project,
            string zipPath)
        {
            ContentWorkshopOperationResult ready = PrepareForExport(project);
            if (!ready.Success)
                return ready;
            if (!string.Equals(Path.GetExtension(zipPath), ".zip", StringComparison.OrdinalIgnoreCase))
                zipPath += ".zip";
            if (File.Exists(zipPath))
                return ContentWorkshopOperationResult.Failed("ZIP target already exists: " + zipPath);

            string temporary = zipPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                string parent = Path.GetDirectoryName(Path.GetFullPath(zipPath));
                if (!Directory.Exists(parent))
                    Directory.CreateDirectory(parent);
                ContentWorkshopZipWriter.WriteProject(project.RootPath, project.ModId, temporary);
                File.Move(temporary, zipPath);
                return ContentWorkshopOperationResult.Succeeded(Path.GetFullPath(zipPath));
            }
            catch (Exception ex)
            {
                TryDeleteFile(temporary);
                return ContentWorkshopOperationResult.Failed("ZIP export failed: " + ex.Message);
            }
        }

        public ContentWorkshopOperationResult Install(
            ContentWorkshopProject project,
            string modsRoot)
        {
            ContentWorkshopOperationResult ready = PrepareForExport(project);
            if (!ready.Success)
                return ready;

            try
            {
                string root = NormalizeRoot(modsRoot);
                if (!Directory.Exists(root))
                    return ContentWorkshopOperationResult.Failed("The configured mods folder does not exist.");
                string target = ResolveContained(root, project.ModId);
                if (Directory.Exists(target) || File.Exists(target))
                {
                    return ContentWorkshopOperationResult.Failed(
                        "A mod with this ID is already installed. Remove it explicitly before installing this export.");
                }

                string staging = ResolveContained(
                    root,
                    "." + project.ModId + ".install-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                try
                {
                    CopyExportFiles(project.RootPath, staging);
                    Directory.Move(staging, target);
                }
                finally
                {
                    TryDeleteDirectory(staging);
                }
                return ContentWorkshopOperationResult.Succeeded(target);
            }
            catch (Exception ex)
            {
                return ContentWorkshopOperationResult.Failed("Local install failed: " + ex.Message);
            }
        }

        private ContentWorkshopOperationResult PrepareForExport(ContentWorkshopProject project)
        {
            ContentWorkshopOperationResult save = Save(project);
            if (!save.Success)
                return save;
            ContentPackValidationResult validation = Validate(project, true);
            if (!validation.IsValid)
            {
                return ContentWorkshopOperationResult.Failed(
                    "Fix the " + validation.ErrorCount + " validation error(s) before exporting.");
            }
            return ContentWorkshopOperationResult.Succeeded(project.RootPath);
        }

        private static bool TryValidateProjectIdentity(ContentWorkshopProject project, out string error)
        {
            error = null;
            if (project == null || project.About == null || project.Content == null)
            {
                error = "A Content Workshop project is required.";
                return false;
            }
            if (string.IsNullOrEmpty(project.RootPath))
            {
                error = "Choose a project folder.";
                return false;
            }
            if (string.IsNullOrEmpty(project.About.id) ||
                !string.Equals(project.About.id, project.Content.modId, StringComparison.OrdinalIgnoreCase))
            {
                error = "About id and content-pack modId must be present and match.";
                return false;
            }
            if (string.IsNullOrEmpty((project.About.name ?? string.Empty).Trim()) ||
                string.IsNullOrEmpty((project.About.version ?? string.Empty).Trim()))
            {
                error = "Mod name and version are required.";
                return false;
            }
            return true;
        }

        private static void EnsureProjectDirectories(string root)
        {
            Directory.CreateDirectory(ResolveContained(root, "About"));
            Directory.CreateDirectory(ResolveContained(root, "Content"));
            Directory.CreateDirectory(ResolveContained(root, "Assets/Icons"));
        }

        private static void CopyExportFiles(string sourceRoot, string destinationRoot)
        {
            for (int i = 0; i < ExportRoots.Length; i++)
            {
                string source = ResolveContained(sourceRoot, ExportRoots[i]);
                if (Directory.Exists(source))
                    CopyDirectory(source, ResolveContained(destinationRoot, ExportRoots[i]));
            }
            string readme = ResolveContained(sourceRoot, ReadmeRelativePath);
            if (File.Exists(readme))
                File.Copy(readme, ResolveContained(destinationRoot, ReadmeRelativePath), false);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string sourceRoot = NormalizeRoot(source) + Path.DirectorySeparatorChar;
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string full = Path.GetFullPath(files[i]);
                if (!full.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A project file resolved outside its source folder.");
                string relative = full.Substring(sourceRoot.Length);
                string target = ResolveContained(destination, relative);
                string targetDirectory = Path.GetDirectoryName(target);
                if (!Directory.Exists(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);
                File.Copy(full, target, false);
            }
        }

        internal static string ResolveContained(string rootPath, string relativePath)
        {
            string root = NormalizeRoot(rootPath);
            string rootedPrefix = root + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(
                root,
                (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(rootedPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path resolves outside the selected root.");
            return candidate;
        }

        private static string NormalizeRoot(string rootPath)
        {
            if (string.IsNullOrEmpty((rootPath ?? string.Empty).Trim()))
                throw new ArgumentException("A folder path is required.", "rootPath");
            return Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string MakeSafeFileName(string value)
        {
            string candidate = Path.GetFileNameWithoutExtension((value ?? string.Empty).Trim());
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder output = new StringBuilder();
            for (int i = 0; i < candidate.Length; i++)
            {
                char c = candidate[i];
                if (Array.IndexOf(invalid, c) < 0 && c != '/' && c != '\\')
                    output.Append(c);
            }
            return output.ToString().Trim().ToLowerInvariant();
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = ContentPackJsonSerializer.MaximumJsonLength;
            serializer.RecursionLimit = ContentPackJsonSerializer.MaximumRecursionLimit;
            return serializer;
        }

        private static string CreateReadme(ContentWorkshopProject project)
        {
            return "# " + project.About.name + "\r\n\r\n" +
                (project.About.description ?? string.Empty) + "\r\n\r\n" +
                "This data-driven Sheltered content pack was created with Sheltered Mod Manager's Content Workshop.\r\n" +
                "Install it as a normal mod folder. Categories requiring custom behavior may also need a code plugin.\r\n";
        }

        private static void WriteAllTextAtomic(string path, string content)
        {
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, content ?? string.Empty, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                string backup = path + ".bak";
                TryDeleteFile(backup);
                File.Replace(temporary, path, backup);
                TryDeleteFile(backup);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private static void SavePngAtomic(Bitmap bitmap, string path)
        {
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            bitmap.Save(temporary, ImageFormat.Png);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }
    }

    /// <summary>Minimal stored-entry ZIP writer compatible with .NET 3.5.</summary>
    internal static class ContentWorkshopZipWriter
    {
        private sealed class Entry
        {
            public string Name;
            public byte[] Data;
            public uint Crc;
            public uint Offset;
        }

        public static void WriteProject(string projectRoot, string modId, string outputPath)
        {
            List<Entry> entries = new List<Entry>();
            AddTree(entries, projectRoot, "About", modId);
            AddTree(entries, projectRoot, "Content", modId);
            AddTree(entries, projectRoot, "Assets", modId);
            AddFile(entries, projectRoot, "README.md", modId);

            using (FileStream stream = File.Create(outputPath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    entry.Offset = (uint)stream.Position;
                    byte[] name = Encoding.UTF8.GetBytes(entry.Name);
                    writer.Write(0x04034b50u);
                    writer.Write((ushort)20);
                    writer.Write((ushort)0x0800);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write(entry.Crc);
                    writer.Write((uint)entry.Data.Length);
                    writer.Write((uint)entry.Data.Length);
                    writer.Write((ushort)name.Length);
                    writer.Write((ushort)0);
                    writer.Write(name);
                    writer.Write(entry.Data);
                }

                uint centralOffset = (uint)stream.Position;
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    byte[] name = Encoding.UTF8.GetBytes(entry.Name);
                    writer.Write(0x02014b50u);
                    writer.Write((ushort)20);
                    writer.Write((ushort)20);
                    writer.Write((ushort)0x0800);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write(entry.Crc);
                    writer.Write((uint)entry.Data.Length);
                    writer.Write((uint)entry.Data.Length);
                    writer.Write((ushort)name.Length);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write(0u);
                    writer.Write(entry.Offset);
                    writer.Write(name);
                }
                uint centralSize = (uint)stream.Position - centralOffset;
                writer.Write(0x06054b50u);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)entries.Count);
                writer.Write((ushort)entries.Count);
                writer.Write(centralSize);
                writer.Write(centralOffset);
                writer.Write((ushort)0);
            }
        }

        private static void AddTree(List<Entry> entries, string root, string folder, string modId)
        {
            string source = ContentWorkshopProjectService.ResolveContained(root, folder);
            if (!Directory.Exists(source))
                return;
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = Path.GetFullPath(files[i]).Substring(prefix.Length).Replace('\\', '/');
                Add(entries, files[i], modId + "/" + relative);
            }
        }

        private static void AddFile(List<Entry> entries, string root, string relative, string modId)
        {
            string source = ContentWorkshopProjectService.ResolveContained(root, relative);
            if (File.Exists(source))
                Add(entries, source, modId + "/" + relative);
        }

        private static void Add(List<Entry> entries, string path, string entryName)
        {
            byte[] data = File.ReadAllBytes(path);
            entries.Add(new Entry { Name = entryName, Data = data, Crc = Crc32(data) });
        }

        private static uint Crc32(byte[] bytes)
        {
            uint crc = 0xffffffffu;
            for (int i = 0; i < bytes.Length; i++)
            {
                crc ^= bytes[i];
                for (int bit = 0; bit < 8; bit++)
                    crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
            }
            return ~crc;
        }
    }
}
