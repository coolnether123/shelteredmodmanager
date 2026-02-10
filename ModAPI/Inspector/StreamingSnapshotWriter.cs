using System;
using System.IO;
using System.Text;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    public sealed class StreamingSnapshotWriter : IDisposable
    {
        private const ushort SNAPSHOT_VERSION = 4;
        private static readonly byte[] SNAPSHOT_MAGIC = Encoding.ASCII.GetBytes("MODT");
        private const int FLUSH_INTERVAL = 100;

        private FileStream _fileStream;
        private BinaryWriter _writer;
        private int _frameCount;
        private int _frameCountOffset;
        private int _endOffsetOffset;
        private string _path;
        private bool _started;

        public string PathOnDisk
        {
            get { return _path ?? string.Empty; }
        }

        public void BeginSnapshot(string path, SnapshotBuffer snapshot)
        {
            Dispose();

            _path = path;
            _fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_fileStream);
            _frameCount = 0;

            _writer.Write(SNAPSHOT_MAGIC);
            _writer.Write(SNAPSHOT_VERSION);
            _writer.Write(snapshot.StartTime.Ticks);
            _writer.Write(snapshot.StartOffset);

            _endOffsetOffset = (int)_fileStream.Position;
            _writer.Write(-1); // EndOffset placeholder

            _frameCountOffset = (int)_fileStream.Position;
            _writer.Write(0); // Frame count placeholder

            _writer.Write(snapshot.StartMethod != null ? snapshot.StartMethod.DeclaringType.FullName + "." + snapshot.StartMethod.Name : "<unknown>");
            _started = true;
        }

        public void WriteFrame(TraceFrame frame)
        {
            if (!_started || _writer == null || frame == null) return;
            frame.Write(_writer);

            _frameCount++;
            if ((_frameCount % FLUSH_INTERVAL) == 0)
            {
                _writer.Flush();
            }
        }

        public void EndSnapshot(int endOffset, string metaPath, SnapshotBuffer snapshot)
        {
            if (!_started || _writer == null || _fileStream == null) return;

            var endPos = _fileStream.Position;

            _fileStream.Seek(_endOffsetOffset, SeekOrigin.Begin);
            _writer.Write(endOffset);

            _fileStream.Seek(_frameCountOffset, SeekOrigin.Begin);
            _writer.Write(_frameCount);

            _fileStream.Seek(endPos, SeekOrigin.Begin);
            _writer.Flush();

            WriteMetaFile(metaPath, snapshot, _frameCount, endOffset);

            MMLog.WriteInfo("[ExecutionTracer] Snapshot saved: " + _path + " (" + _frameCount + " frames)");
            Dispose();
        }

        private static void WriteMetaFile(string metaPath, SnapshotBuffer snapshot, int frameCount, int endOffset)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Version=4");
                sb.AppendLine("StartMethod=" + (snapshot.StartMethod != null ? snapshot.StartMethod.DeclaringType + "." + snapshot.StartMethod.Name : "<unknown>"));
                sb.AppendLine("StartOffset=" + snapshot.StartOffset);
                sb.AppendLine("EndOffset=" + endOffset);
                sb.AppendLine("FrameCount=" + frameCount);
                sb.AppendLine("DroppedFrameCount=" + snapshot.DroppedFrameCount);
                sb.AppendLine("LosslessTrace=" + snapshot.LosslessTrace);
                sb.AppendLine("StartTimeUtcTicks=" + snapshot.StartTime.Ticks);
                File.WriteAllText(metaPath, sb.ToString());
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ExecutionTracer] Failed to write meta file: " + ex.Message);
            }
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Close();
                _writer = null;
            }

            if (_fileStream != null)
            {
                _fileStream.Close();
                _fileStream = null;
            }

            _started = false;
        }
    }
}
