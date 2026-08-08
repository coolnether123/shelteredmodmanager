using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ShelteredAPI.Saves.Backups
{
    internal static class DurableFileWriter
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(IntPtr handle);

        internal static void WriteNew(string path, byte[] bytes)
        {
            byte[] payload = bytes ?? new byte[0];
            using (FileStream stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                stream.Write(payload, 0, payload.Length);
                FlushToDisk(stream);
            }
        }

        private static void FlushToDisk(FileStream stream)
        {
            stream.Flush();

#pragma warning disable 618
            IntPtr handle = stream.Handle;
#pragma warning restore 618
            if (!FlushFileBuffers(handle))
            {
                throw new IOException(
                    "The durable file write could not be flushed to disk. Win32 error "
                    + Marshal.GetLastWin32Error() + ".");
            }
        }
    }

    /// <summary>
    /// Defines the single filesystem-segment policy shared by backup timelines and branch markers.
    /// </summary>
    internal static class SaveBackupPathPolicy
    {
        private const int MaximumSegmentLength = 96;

        internal static string SanitizeSegment(string value)
        {
            string safe = string.IsNullOrEmpty(value) ? "unknown" : value;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                safe = safe.Replace(invalid[i], '_');

            safe = safe.Replace('\\', '_').Replace('/', '_').Replace(':', '_').Replace('|', '_');
            return safe.Length > MaximumSegmentLength ? safe.Substring(0, MaximumSegmentLength) : safe;
        }
    }
}
