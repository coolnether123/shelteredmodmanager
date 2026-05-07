using System;
using System.Collections.Generic;
using ModAPI.Networking.Events;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class EventSyncTests
    {
        private const string TestApplicationId = "ModAPI.Networking.Tests.Events";

        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Network event envelope round-trips metadata and payload", EnvelopeRoundTripsMetadataAndPayload));
            tests.Add(new TestCase("Network event registry serializes typed payloads", RegistrySerializesTypedPayloads));
            tests.Add(new TestCase("Network event dispatcher carries intent and authoritative events", DispatcherCarriesIntentAndAuthoritativeEvents));
        }

        private static void EnvelopeRoundTripsMetadataAndPayload()
        {
            NetworkEventEnvelope envelope = NetworkEventEnvelope.Create(
                "example.event",
                3,
                NetworkEventPhase.Intent,
                7,
                42,
                new byte[] { 1, 2, 3 });
            envelope.CorrelationId = "parent";

            NetworkEventEnvelope copy = NetworkEventEnvelope.FromPayload(envelope.ToPayload());

            TestAssert.Equal("example.event", copy.EventName, "Event name should round-trip.");
            TestAssert.Equal(3, copy.EventVersion, "Event version should round-trip.");
            TestAssert.Equal(NetworkEventPhase.Intent, copy.Phase, "Event phase should round-trip.");
            TestAssert.Equal(7, copy.SenderPeerId, "Sender peer id should round-trip.");
            TestAssert.Equal((uint)42, copy.WorldTick, "World tick should round-trip.");
            TestAssert.Equal("parent", copy.CorrelationId, "Correlation id should round-trip.");
            TestAssert.BytesEqual(new byte[] { 1, 2, 3 }, copy.Payload, "Payload should round-trip.");
            TestAssert.True(!string.IsNullOrEmpty(copy.EventId), "Event id should be assigned.");
        }

        private static void RegistrySerializesTypedPayloads()
        {
            NetworkEventRegistry registry = new NetworkEventRegistry();
            registry.Register(new SamplePayloadSerializer());

            SamplePayload source = new SamplePayload();
            source.Name = "expedition.start";
            source.Value = 99;

            byte[] payload = registry.SerializePayload(SamplePayloadSerializer.Name, SamplePayloadSerializer.Version, source);
            NetworkEventEnvelope envelope = NetworkEventEnvelope.Create(
                SamplePayloadSerializer.Name,
                SamplePayloadSerializer.Version,
                NetworkEventPhase.Authoritative,
                1,
                10,
                payload);

            object decoded;
            string error;
            TestAssert.True(registry.TryDeserializePayload(envelope, out decoded, out error), "Registered payload should deserialize. " + error);
            SamplePayload copy = (SamplePayload)decoded;
            TestAssert.Equal(source.Name, copy.Name, "Typed payload string should round-trip.");
            TestAssert.Equal(source.Value, copy.Value, "Typed payload int should round-trip.");
        }

        private static void DispatcherCarriesIntentAndAuthoritativeEvents()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            NetworkEventDispatcher hostEvents = null;
            NetworkEventDispatcher clientEvents = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                hostEvents = new NetworkEventDispatcher(host);
                clientEvents = new NetworkEventDispatcher(client);

                NetworkEventEnvelope hostReceived = null;
                NetworkEventEnvelope clientReceived = null;
                hostEvents.EventReceived += delegate(object sender, NetworkEventReceivedEventArgs e)
                {
                    hostReceived = e.Envelope;
                };
                clientEvents.EventReceived += delegate(object sender, NetworkEventReceivedEventArgs e)
                {
                    clientReceived = e.Envelope;
                };
                hostEvents.Start();
                clientEvents.Start();

                NetworkTestUtilities.Connect(host, client, TestApplicationId);

                NetworkEventEnvelope intent = NetworkEventEnvelope.Create(
                    "example.intent",
                    1,
                    NetworkEventPhase.Intent,
                    client.LocalPeerId,
                    5,
                    new byte[] { 8 });
                TestAssert.True(clientEvents.SendToHost(intent, NetworkChannel.Reliable), "Client intent send should queue.");
                NetworkTestUtilities.PumpUntil(host, client, delegate { return hostReceived != null; },
                    "Host did not receive the client event intent.");

                TestAssert.Equal(NetworkEventPhase.Intent, hostReceived.Phase, "Host should receive an intent event.");
                TestAssert.BytesEqual(new byte[] { 8 }, hostReceived.Payload, "Host should receive exact intent payload.");

                NetworkEventEnvelope authoritative = hostReceived.AsAuthoritative(host.LocalPeerId, 6);
                TestAssert.Equal(1, hostEvents.Broadcast(authoritative, NetworkChannel.Reliable),
                    "Host authoritative broadcast should target one client.");
                NetworkTestUtilities.PumpUntil(host, client, delegate { return clientReceived != null; },
                    "Client did not receive the authoritative event.");

                TestAssert.Equal(NetworkEventPhase.Authoritative, clientReceived.Phase, "Client should receive authoritative phase.");
                TestAssert.Equal(hostReceived.EventId, clientReceived.CorrelationId, "Authoritative event should correlate to the original intent.");
            }
            finally
            {
                if (clientEvents != null)
                    clientEvents.Dispose();
                if (hostEvents != null)
                    hostEvents.Dispose();
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private sealed class SamplePayload
        {
            public string Name;
            public int Value;
        }

        private sealed class SamplePayloadSerializer : NetworkEventPayloadSerializer<SamplePayload>
        {
            public const string Name = "sample.payload";
            public const ushort Version = 1;

            public SamplePayloadSerializer()
                : base(Name, Version)
            {
            }

            protected override void Write(SamplePayload value, ref BitWriter writer)
            {
                writer.WriteString(value != null ? value.Name : string.Empty);
                writer.WriteInt32(value != null ? value.Value : 0);
            }

            protected override SamplePayload Read(ref BitReader reader)
            {
                SamplePayload payload = new SamplePayload();
                payload.Name = reader.ReadString();
                payload.Value = reader.ReadInt32();
                return payload;
            }
        }
    }
}
