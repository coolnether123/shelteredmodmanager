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
}
