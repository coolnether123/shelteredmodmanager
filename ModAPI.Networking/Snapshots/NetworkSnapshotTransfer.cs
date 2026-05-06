using System;

namespace ModAPI.Networking.Snapshots
{
    public static class NetworkSnapshotTransfer
    {
        public static NetworkSnapshotChunk[] CreateChunks(string transferId, byte[] payload, int maxChunkPayloadBytes)
        {
            if (transferId == null || transferId.Length == 0)
                throw new ArgumentException("Transfer id is required.", "transferId");
            if (transferId.Length > NetworkDefaults.MaxHandshakeStringLength)
                throw new ArgumentOutOfRangeException("transferId", "Transfer id is too long.");
            if (payload == null)
                payload = new byte[0];
            if (maxChunkPayloadBytes <= 0 || maxChunkPayloadBytes > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("maxChunkPayloadBytes");

            int chunkCount = payload.Length == 0 ? 1 : ((payload.Length + maxChunkPayloadBytes - 1) / maxChunkPayloadBytes);
            if (chunkCount > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("payload", "Snapshot payload needs too many chunks.");

            NetworkSnapshotChunk[] chunks = new NetworkSnapshotChunk[chunkCount];
            for (int i = 0; i < chunkCount; i++)
            {
                int offset = i * maxChunkPayloadBytes;
                int count = Math.Min(maxChunkPayloadBytes, payload.Length - offset);
                if (count < 0)
                    count = 0;

                byte[] chunkPayload = new byte[count];
                if (count > 0)
                    Buffer.BlockCopy(payload, offset, chunkPayload, 0, count);

                NetworkSnapshotChunk chunk = new NetworkSnapshotChunk();
                chunk.TransferId = transferId;
                chunk.ChunkIndex = (ushort)i;
                chunk.ChunkCount = (ushort)chunkCount;
                chunk.TotalLength = payload.Length;
                chunk.Payload = chunkPayload;
                chunks[i] = chunk;
            }

            return chunks;
        }
    }

    public sealed class NetworkSnapshotTransferAssembler
    {
        private readonly string _transferId;
        private readonly byte[][] _chunks;
        private readonly bool[] _received;
        private readonly int _totalLength;
        private int _receivedCount;

        public NetworkSnapshotTransferAssembler(NetworkSnapshotChunk firstChunk)
        {
            if (firstChunk == null)
                throw new ArgumentNullException("firstChunk");
            firstChunk.Validate();

            _transferId = firstChunk.TransferId;
            _chunks = new byte[firstChunk.ChunkCount][];
            _received = new bool[firstChunk.ChunkCount];
            _totalLength = firstChunk.TotalLength;
            AddChunk(firstChunk);
        }

        public string TransferId { get { return _transferId; } }
        public int ChunkCount { get { return _chunks.Length; } }
        public int ReceivedCount { get { return _receivedCount; } }
        public bool IsComplete { get { return _receivedCount == _chunks.Length; } }

        public bool AddChunk(NetworkSnapshotChunk chunk)
        {
            if (chunk == null)
                throw new ArgumentNullException("chunk");
            chunk.Validate();
            if (!string.Equals(_transferId, chunk.TransferId, StringComparison.Ordinal))
                throw new InvalidOperationException("Snapshot chunk belongs to a different transfer.");
            if (chunk.ChunkCount != _chunks.Length)
                throw new InvalidOperationException("Snapshot chunk count changed during transfer.");
            if (chunk.TotalLength != _totalLength)
                throw new InvalidOperationException("Snapshot total length changed during transfer.");

            int index = chunk.ChunkIndex;
            if (_received[index])
                return false;

            byte[] payload = chunk.Payload ?? new byte[0];
            byte[] copy = new byte[payload.Length];
            if (copy.Length > 0)
                Buffer.BlockCopy(payload, 0, copy, 0, copy.Length);

            _chunks[index] = copy;
            _received[index] = true;
            _receivedCount++;
            return true;
        }

        public bool TryBuild(out byte[] payload)
        {
            if (!IsComplete)
            {
                payload = null;
                return false;
            }

            payload = new byte[_totalLength];
            int offset = 0;
            for (int i = 0; i < _chunks.Length; i++)
            {
                byte[] chunk = _chunks[i] ?? new byte[0];
                if (offset + chunk.Length > payload.Length)
                    throw new InvalidOperationException("Snapshot chunks exceed declared total length.");
                if (chunk.Length > 0)
                    Buffer.BlockCopy(chunk, 0, payload, offset, chunk.Length);
                offset += chunk.Length;
            }

            if (offset != payload.Length)
                throw new InvalidOperationException("Snapshot chunks do not match declared total length.");

            return true;
        }
    }
}
