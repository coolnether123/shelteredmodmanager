using System;
using System.IO;

namespace ShelteredModManager.ContentPacks
{
    public sealed class ContentPackAssetValidationResult
    {
        public bool Success;
        public string NormalizedPath;
        public string FullPath;
        public string ErrorMessage;
    }

    /// <summary>Central path-containment and PNG safety policy for content-pack assets.</summary>
    public static class ContentPackPathPolicy
    {
        private static readonly byte[] PngSignature =
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static ContentPackAssetValidationResult ValidateIcon(
            string modRootPath,
            string relativePath,
            bool requireFile,
            long maximumBytes,
            int maximumDimension)
        {
            string normalized;
            string fullPath;
            string error;
            if (!TryResolveAsset(modRootPath, relativePath, out normalized, out fullPath, out error))
                return Failed(error);

            if (!string.Equals(Path.GetExtension(normalized), ".png", StringComparison.OrdinalIgnoreCase))
                return Failed("Icon assets must use the .png extension.");

            if (!requireFile)
                return Succeeded(normalized, fullPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return Failed("Icon asset was not found: " + normalized);

            try
            {
                FileInfo info = new FileInfo(fullPath);
                if (info.Length <= 0)
                    return Failed("Icon asset is empty: " + normalized);
                if (maximumBytes > 0 && info.Length > maximumBytes)
                    return Failed("Icon asset exceeds the configured file-size limit: " + normalized);

                int width;
                int height;
                if (!TryReadPngDimensions(fullPath, out width, out height))
                    return Failed("Icon asset is not a supported PNG file: " + normalized);
                if (width <= 0 || height <= 0)
                    return Failed("Icon asset has invalid dimensions: " + normalized);
                if (maximumDimension > 0 && (width > maximumDimension || height > maximumDimension))
                    return Failed("Icon asset exceeds the configured dimension limit: " + normalized);
            }
            catch (Exception ex)
            {
                return Failed("Icon asset could not be inspected: " + ex.Message);
            }

            return Succeeded(normalized, fullPath);
        }

        public static bool TryResolveAsset(
            string modRootPath,
            string relativePath,
            out string normalizedPath,
            out string fullPath,
            out string errorMessage)
        {
            normalizedPath = null;
            fullPath = null;
            errorMessage = null;

            string candidate = (relativePath ?? string.Empty).Trim().Replace('\\', '/');
            if (candidate.Length == 0)
            {
                errorMessage = "Asset path is empty.";
                return false;
            }
            if (Path.IsPathRooted(candidate) || candidate.IndexOf(':') >= 0)
            {
                errorMessage = "Asset path must be relative to the mod root.";
                return false;
            }
            if (!candidate.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Asset path must begin with Assets/.";
                return false;
            }

            string[] segments = candidate.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 || segments[i] == "." || segments[i] == "..")
                {
                    errorMessage = "Asset path contains an empty or traversal segment.";
                    return false;
                }
            }

            normalizedPath = string.Join("/", segments);
            if (string.IsNullOrEmpty(modRootPath))
                return true;

            try
            {
                string root = Path.GetFullPath(modRootPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string rootWithSeparator = root + Path.DirectorySeparatorChar;
                string localPath = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
                string resolved = Path.GetFullPath(Path.Combine(root, localPath));
                if (!resolved.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Asset path resolves outside the mod root.";
                    return false;
                }

                fullPath = resolved;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Asset path is invalid: " + ex.Message;
                return false;
            }
        }

        private static bool TryReadPngDimensions(string path, out int width, out int height)
        {
            width = 0;
            height = 0;

            byte[] header = new byte[24];
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int offset = 0;
                while (offset < header.Length)
                {
                    int read = stream.Read(header, offset, header.Length - offset);
                    if (read <= 0)
                        return false;
                    offset += read;
                }
            }

            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (header[i] != PngSignature[i])
                    return false;
            }
            if (header[12] != 73 || header[13] != 72 || header[14] != 68 || header[15] != 82)
                return false;

            width = ReadBigEndianInt32(header, 16);
            height = ReadBigEndianInt32(header, 20);
            return true;
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            unchecked
            {
                return (bytes[offset] << 24)
                    | (bytes[offset + 1] << 16)
                    | (bytes[offset + 2] << 8)
                    | bytes[offset + 3];
            }
        }

        private static ContentPackAssetValidationResult Succeeded(string normalized, string fullPath)
        {
            return new ContentPackAssetValidationResult
            {
                Success = true,
                NormalizedPath = normalized,
                FullPath = fullPath
            };
        }

        private static ContentPackAssetValidationResult Failed(string error)
        {
            return new ContentPackAssetValidationResult
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }
}
