using System;
using System.Collections.Generic;
using ModAPI.Networking.Events;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Sessions;
using ShelteredAPI.Networking.Trade;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTradeEventTests
    {
        private const string TestApplicationId = "ShelteredAPI.Networking.Tests.Trade";

        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Trade gameplay envelope round-trips metadata and cargo", TradeEnvelopeRoundTripsMetadataAndCargo));
            tests.Add(new TestCase("Trade offer intent is accepted by the host authoritatively", HostAcceptsTradeOfferIntentAuthoritatively));
        }

        private static void TradeEnvelopeRoundTripsMetadataAndCargo()
        {
            NetworkEventRegistry registry = new NetworkEventRegistry();
            registry.Register(new ShelteredNetworkGameplayEventSerializer());

            ShelteredMultiplayerTradeEvent tradeEvent = CreateValidOffer();
            tradeEvent.EventId = "event-1";
            tradeEvent.CorrelationId = "corr-1";
            tradeEvent.WorldTick = 77;

            ShelteredNetworkGameplayEvent gameplayEvent = ShelteredMultiplayerTradeContractCodec.ToGameplayEvent(tradeEvent);
            byte[] payload = registry.SerializePayload(
                ShelteredNetworkGameplayEvent.EnvelopeEventName,
                ShelteredNetworkGameplayEvent.CurrentVersion,
                gameplayEvent);

            NetworkEventEnvelope envelope = NetworkEventEnvelope.Create(
                ShelteredNetworkGameplayEvent.EnvelopeEventName,
                ShelteredNetworkGameplayEvent.CurrentVersion,
                NetworkEventPhase.Intent,
                2,
                77,
                payload);
            envelope.EventId = "event-1";
            envelope.CorrelationId = "corr-1";

            NetworkEventEnvelope copyEnvelope = NetworkEventEnvelope.FromPayload(envelope.ToPayload());
            object decoded;
            string error;
            TestAssert.True(registry.TryDeserializePayload(copyEnvelope, out decoded, out error), "Trade gameplay payload should deserialize. " + error);

            ShelteredNetworkGameplayEvent decodedGameplay = (ShelteredNetworkGameplayEvent)decoded;
            ShelteredMultiplayerTradeEvent decodedTrade = ShelteredMultiplayerTradeContractCodec.FromGameplayEvent(decodedGameplay);

            TestAssert.Equal(ShelteredNetworkEventKinds.TradeOfferIntent, decodedTrade.EventKind, "Trade event kind should round-trip.");
            TestAssert.Equal("event-1", decodedTrade.EventId, "Trade event id should round-trip.");
            TestAssert.Equal("corr-1", decodedTrade.CorrelationId, "Trade correlation id should round-trip.");
            TestAssert.Equal((uint)77, decodedTrade.WorldTick, "Trade world tick should round-trip.");
            TestAssert.Equal("trade-1", decodedTrade.TradeId, "Trade id should round-trip.");
            TestAssert.Equal(1, decodedTrade.Cargo.Count, "Cargo line count should round-trip.");
            TestAssert.Equal("water", decodedTrade.Cargo[0].ItemId, "Cargo item id should round-trip.");
            TestAssert.Equal(3, decodedTrade.Cargo[0].Count, "Cargo count should round-trip.");
            TestAssert.Equal("player-a", decodedTrade.Cargo[0].SourceOwnerId, "Cargo source owner should round-trip.");
            TestAssert.Equal("player-b", decodedTrade.Cargo[0].TargetOwnerId, "Cargo target owner should round-trip.");
        }

        private static void HostAcceptsTradeOfferIntentAuthoritatively()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            ShelteredMultiplayerEventSyncService hostEvents = null;
            ShelteredMultiplayerEventSyncService clientEvents = null;
            ShelteredMultiplayerTradeService tradeService = null;
            Action<ShelteredNetworkEventContext> authoritativeHandler = null;

            try
            {
                host = new NetworkSession(NetworkTestHarness.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestHarness.CreateLoopbackConfig());
                hostEvents = new ShelteredMultiplayerEventSyncService(host, null);
                clientEvents = new ShelteredMultiplayerEventSyncService(client, null);
                tradeService = new ShelteredMultiplayerTradeService();

                ShelteredMultiplayerTradeEvent accepted = null;
                string acceptedCorrelationId = string.Empty;
                authoritativeHandler = delegate(ShelteredNetworkEventContext context)
                {
                    if (context == null || context.GameplayEvent == null)
                        return;
                    if (!string.Equals(context.GameplayEvent.EventKind, ShelteredNetworkEventKinds.TradeOfferAccepted, StringComparison.Ordinal))
                        return;

                    accepted = ShelteredMultiplayerTradeContractCodec.FromGameplayEvent(context.GameplayEvent);
                    acceptedCorrelationId = context.Envelope.CorrelationId;
                };
                ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += authoritativeHandler;

                NetworkTestHarness.Connect(host, client, TestApplicationId);

                ShelteredNetworkGameplayEvent intent = ShelteredMultiplayerTradeContractCodec.ToGameplayEvent(CreateValidOffer());
                TestAssert.True(clientEvents.PublishIntent(intent), "Client trade intent should queue.");

                NetworkTestHarness.PumpUntil(host, client, delegate { return accepted != null; },
                    "Client did not receive the host-authoritative trade acceptance.");

                TestAssert.Equal(ShelteredNetworkEventKinds.TradeOfferAccepted, accepted.EventKind, "Host should convert a valid offer intent into an accepted event.");
                TestAssert.Equal("trade-1", accepted.TradeId, "Accepted trade id should match the intent.");
                TestAssert.Equal("player-a", accepted.SourceOwnerId, "Accepted source owner should match the intent.");
                TestAssert.Equal("player-b", accepted.TargetOwnerId, "Accepted target owner should match the intent.");
                TestAssert.Equal(1, accepted.Cargo.Count, "Accepted event should preserve cargo.");
                TestAssert.True(!string.IsNullOrEmpty(accepted.EventId), "Authoritative event id should be assigned.");
                TestAssert.True(!string.IsNullOrEmpty(accepted.CorrelationId), "Authoritative event should correlate to the intent.");
                TestAssert.Equal(acceptedCorrelationId, accepted.CorrelationId, "Payload and envelope correlation id should match.");
            }
            finally
            {
                if (authoritativeHandler != null)
                    ShelteredMultiplayerNetworkEvents.AuthoritativeReceived -= authoritativeHandler;
                if (tradeService != null)
                    tradeService.Dispose();
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

        private static ShelteredMultiplayerTradeEvent CreateValidOffer()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = new ShelteredMultiplayerTradeEvent();
            tradeEvent.EventKind = ShelteredNetworkEventKinds.TradeOfferIntent;
            tradeEvent.TradeId = "trade-1";
            tradeEvent.SourceOwnerId = "player-a";
            tradeEvent.TargetOwnerId = "player-b";

            ShelteredTradeCargoDto cargo = new ShelteredTradeCargoDto();
            cargo.ItemId = "water";
            cargo.Count = 3;
            cargo.SourceOwnerId = "player-a";
            cargo.TargetOwnerId = "player-b";
            tradeEvent.Cargo.Add(cargo);

            return tradeEvent;
        }
    }
}
