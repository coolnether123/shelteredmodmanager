using System.Collections.Generic;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Snapshots;

namespace ModAPI.Networking.Tests
{
    internal static class SnapshotTransferTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Snapshot transfer chunks arbitrary bytes", SnapshotTransferChunksBytes));
            tests.Add(new TestCase("Snapshot chunk rejects oversized payload before writing", SnapshotChunkRejectsOversizedPayloadBeforeWriting));
        }

        private static void SnapshotTransferChunksBytes()
        {
            byte[] payload = new byte[11];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i + 1);

            NetworkSnapshotChunk[] chunks = NetworkSnapshotTransfer.CreateChunks("snapshot-1", payload, 4);
            TestAssert.Equal(3, chunks.Length, "Payload should be split into three chunks.");

            byte[] buffer = new byte[256];
            BitWriter writer = new BitWriter(buffer);
            chunks[1].WriteTo(ref writer);
            BitReader reader = new BitReader(buffer, 0, writer.Position);
            NetworkSnapshotChunk roundTripped = NetworkSnapshotChunk.ReadFrom(ref reader);
            TestAssert.Equal((ushort)1, roundTripped.ChunkIndex, "Chunk metadata should round-trip.");

            NetworkSnapshotTransferAssembler assembler = new NetworkSnapshotTransferAssembler(chunks[2]);
            TestAssert.True(assembler.AddChunk(chunks[0]), "Assembler should accept a new chunk.");
            TestAssert.True(assembler.AddChunk(chunks[1]), "Assembler should accept the final chunk.");
            byte[] rebuilt;
            TestAssert.True(assembler.TryBuild(out rebuilt), "Assembler should complete after all chunks arrive.");
            TestAssert.BytesEqual(payload, rebuilt, "Snapshot payload should rebuild exactly.");
        }

        private static void SnapshotChunkRejectsOversizedPayloadBeforeWriting()
        {
            NetworkSnapshotChunk chunk = new NetworkSnapshotChunk();
            chunk.TransferId = "oversized";
            chunk.ChunkIndex = 0;
            chunk.ChunkCount = 1;
            chunk.TotalLength = ushort.MaxValue + 1;
            chunk.Payload = new byte[ushort.MaxValue + 1];

            byte[] buffer = new byte[NetworkDefaults.MaxPacketSize];
            BitWriter writer = new BitWriter(buffer);
            bool threw = false;
            try
            {
                chunk.WriteTo(ref writer);
            }
            catch (System.InvalidOperationException)
            {
                threw = true;
            }

            TestAssert.True(threw, "Oversized snapshot chunks must be rejected before writing a truncated length.");
        }
    }
}
