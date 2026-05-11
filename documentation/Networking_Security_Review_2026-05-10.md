# Sheltered Mod Manager Networking Security Review

Date: 2026-05-10

Reviewer: Codex

Follow-up status: the raw shelter save-sync service discussed below has been removed from active source after the multiplayer boundary was clarified. Private shelter saves should stay local; future save-backed sync must be narrow shared-world/map state.

Scope:
- `ModAPI.Networking`: host-neutral UDP transport, session, discovery, reliability, event, and snapshot helpers.
- `ShelteredAPI/Networking`: Sheltered-specific multiplayer menu, setup, save sync, event sync, world clock, travel, trade, location, bunker, raid, and persistence integration.
- Main game connection points: `ShelteredAPI/Harmony/MainMenuMultiplayerPatches.cs`, `ShelteredAPI/Networking/MultiplayerMenuController.cs`, `ShelteredAPI/Networking/MultiplayerConnectionTestService.cs`, and `ShelteredAPI/Core/ShelteredApiRuntimeBootstrap.cs`.

## Executive Summary

The module is architecturally cleaner than expected for a modded Unity multiplayer experiment, but it is not secure against hostile peers or hostile networks.

The transport is a custom UDP protocol with packet magic, versioning, sequence numbers, ACK bitfields, bounded packet size, explicit serializers, and a good separation between generic networking and Sheltered-specific gameplay code. That part is surprisingly disciplined.

The security boundary, however, is weak. Connections are accepted based on reachability plus plaintext metadata such as application id and compatibility hashes. There is no encryption, no packet authentication, no shared secret, no authenticated peer identity, no replay protection beyond reliability sequence bookkeeping, and no real trust boundary once a peer is connected.

Shock level: 8/10. The code shape is not shocking; the amount of gameplay and save authority reachable from an unauthenticated connected peer is.

## How It Connects To The Game

1. `ShelteredApiRuntimeBootstrap.EnsurePersistenceGuard` creates the persistent `ShelteredAPI.Runtime` object and installs `ShelteredMultiplayerRuntimeDriver` (`ShelteredAPI/Core/ShelteredApiRuntimeBootstrap.cs:75-91`).
2. `MainMenuMultiplayer_OnShow_Patch` patches `MainMenu.OnShow`, clones a menu button, labels it `Multiplayer`, and opens the multiplayer window on click (`ShelteredAPI/Harmony/MainMenuMultiplayerPatches.cs:17-75`, `144-147`).
3. `MainMenuMultiplayerShortcutHandler` also opens the same UI with `F4` while main menu input is available (`ShelteredAPI/Harmony/MainMenuMultiplayerPatches.cs:159-183`).
4. `MultiplayerMenuController` creates a persistent `ShelteredAPI.MultiplayerTestRuntime` object and owns `MultiplayerConnectionTestService` (`ShelteredAPI/Networking/MultiplayerMenuController.cs:6-15`, `30-44`, `68-75`).
5. `MultiplayerConnectionTestService.StartHost` creates `NetworkSession`, attaches setup/save/event services, starts host mode, then activates the Sheltered multiplayer coordinator (`ShelteredAPI/Networking/MultiplayerConnectionTestService.cs:217-245`).
6. `MultiplayerConnectionTestService.Join` resolves a manual endpoint, creates `NetworkSession`, attaches services, and joins as a client (`ShelteredAPI/Networking/MultiplayerConnectionTestService.cs:267-329`).
7. Incoming application messages are routed first to save sync, then setup, then test-message handling (`ShelteredAPI/Networking/MultiplayerConnectionTestService.cs:976-987`).

This means the button is not just a test harness: it can activate session state, synchronize saves, start setup flow, and drive live game hooks.

## Security Findings

### Critical: Connected Clients Can Send Save Snapshots To The Host

`ShelteredMultiplayerSaveSyncService.TryHandleMessage` accepts `SaveSyncChunkMessageType` in all modes and calls `HandleSnapshotChunk` without requiring the receiver to be a client (`ShelteredAPI/Networking/ShelteredMultiplayerSaveSyncService.cs:136-149`, `273-297`). Once chunks assemble, `ApplyCompletedSnapshot` applies the package regardless of host/client mode; it only creates a backup when the receiver is a client (`ShelteredAPI/Networking/ShelteredMultiplayerSaveSyncService.cs:299-323`).

Impact:
- A connected client can craft save-sync chunks and overwrite host-side save files.
- The package target is derived from network data (`FromPackage`) and then applied to disk (`ShelteredAPI/Networking/ShelteredMultiplayerSaveSyncService.cs:681-695`, `816-843`).
- Path traversal inside package files is guarded by `CombineSafe`, but the host still accepts remote save content into its own save roots.

Recommendation:
- Only clients should accept `SaveSyncChunkMessageType` from the host.
- Hosts should ignore or disconnect peers sending save chunks.
- Validate `peer.IsHost` on clients before applying snapshots.
- Add explicit transfer direction and session/peer binding into the snapshot protocol.

### Critical: Connected Clients Can Send Setup Release Messages

`ShelteredMultiplayerSetupService.TryHandleMessage` accepts `ReleaseStartMessageType` and calls `HandleReleaseStart` (`ShelteredAPI/Networking/ShelteredMultiplayerSetupService.cs:223-236`). `HandleReleaseStart` does not verify that the local session is a client, that the peer is the host, or that the host-side readiness checks passed. If the session id matches, it releases world start and sets the world tick (`ShelteredAPI/Networking/ShelteredMultiplayerSetupService.cs:444-477`).

Impact:
- A connected client can potentially release the host's world-start gate early.
- This bypasses `ReleaseStartFromHost`, which does perform readiness checks (`ShelteredAPI/Networking/ShelteredMultiplayerSetupService.cs:200-220`).

Recommendation:
- On hosts, reject `ReleaseStartMessageType` from clients.
- On clients, accept `ReleaseStartMessageType` only from peer id `0` / host.
- Make the handler signature include `NetworkPeer peer` and enforce sender role.

### High: Authoritative Gameplay Events Are Not Sender-Role Checked

`ShelteredMultiplayerEventSyncService.HandleEnvelope` raises `AuthoritativeReceived` for any envelope whose phase is `Authoritative` (`ShelteredAPI/Networking/ShelteredMultiplayerEventSyncService.cs:119-155`). There is no check that authoritative events came from the host.

Several subscribers apply authoritative events directly, for example:
- Travel applies authoritative travel state (`ShelteredAPI/Networking/Travel/ShelteredTravelSyncService.cs:293-320`).
- Location/loot applies authoritative location state (`ShelteredAPI/Networking/Locations/ShelteredLocationLootService.cs:220-228`).
- Trade applies authoritative trade state (`ShelteredAPI/Networking/Trade/ShelteredMultiplayerTradeService.cs:125-135`).
- Bunker assignments register bunker state from authoritative events (`ShelteredAPI/Networking/ShelteredMultiplayerBunkerAssignments.cs:527-555`).
- Raid state applies authoritative raid events (`ShelteredAPI/Networking/Raids/ShelteredRaidIntentService.cs:56-64`).

Impact:
- A malicious connected client can bypass the intent validation path by sending `Authoritative` envelopes directly.
- Host-side and client-side gameplay state can be mutated by messages that should be host-only.

Recommendation:
- In event sync, reject authoritative envelopes unless `_session.Mode == Client && peer.IsHost`, or local host-generated event with `peer == null`.
- On host, treat remote authoritative envelopes as protocol violations.
- Add tests that clients cannot inject authoritative event phases.

### High: No Cryptographic Authentication Or Encryption

The packet header validates only `PacketMagic` and `ProtocolVersion` (`ModAPI.Networking/Protocol/NetworkPacketHeader.cs:52-55`). The handshake sends plaintext application id, session id, nonce, content hash, mod hash, display name, stable peer id, and reconnect token (`ModAPI.Networking/Protocol/NetworkHandshakeMessages.cs:19-29`, `76-89`). The host validates equality of those metadata fields, but there is no shared secret, signature, HMAC, encryption, or challenge-response (`ModAPI.Networking/Sessions/NetworkHost.cs:170-218`).

Impact:
- Anyone who can reach the UDP port and match or omit allowed metadata can connect.
- Anyone on-path can read and alter traffic.
- UDP source spoofing or same-LAN spoofing can affect ACKs, disconnects, or gameplay messages.
- Compatibility hashes prove sameness only if both peers are honest; they are not authentication.

Recommendation:
- For private LAN only, add an explicit pairing code and HMAC every packet with a session key derived from it.
- For internet play, use a standard secure transport or platform relay/auth layer instead of raw unauthenticated UDP.
- Treat session id and compatibility hash as routing/compatibility metadata, not secrets.

### High: Handshake Allows Empty Session Id And Nonce From Clients

Host sessions generate `SessionId` and `SessionNonce` if they are empty (`ModAPI.Networking/Sessions/NetworkSession.cs:85-100`). But `NetworkHost.ValidateRequest` accepts an empty client session id and nonce because `MatchesOptional(..., allowEmptyActual: true)` is used for both fields (`ModAPI.Networking/Sessions/NetworkHost.cs:187-199`, `301-309`). `MultiplayerConnectionTestService.CreateOptions` does not set a session id or nonce for clients (`ShelteredAPI/Networking/MultiplayerConnectionTestService.cs:601-612`).

Impact:
- A client does not need to know the host's session id or nonce to join.
- LAN discovery/manual endpoint behaves conveniently, but the session id/nonce are not an access control mechanism.

Recommendation:
- Require a join code, password, invite token, or host-approved pairing step before assigning a peer id.
- If session id is meant only for deterministic seeding, rename/security-document it so it is not mistaken for auth.

### High: Snapshot Assembly Allows Memory Exhaustion

`NetworkSnapshotChunk.Validate` allows any non-negative `TotalLength` and any positive `ChunkCount` up to `ushort.MaxValue` (`ModAPI.Networking/Snapshots/NetworkSnapshotChunk.cs:41-59`). `NetworkSnapshotTransferAssembler` allocates arrays based on `ChunkCount` and, when complete, allocates `new byte[_totalLength]` (`ModAPI.Networking/Snapshots/NetworkSnapshotTransfer.cs:55-65`, `100-123`). The save sync service stores assemblers in an unbounded dictionary (`ShelteredAPI/Networking/ShelteredMultiplayerSaveSyncService.cs:31-33`, `273-297`).

Impact:
- A connected peer can create many assemblers or declare a very large total length.
- A one-chunk transfer with a huge `TotalLength` can attempt a huge allocation at build time.

Recommendation:
- Add maximum snapshot bytes, maximum chunk count, maximum concurrent transfers per peer, and transfer timeouts.
- Require chunk payload lengths to match declared total length rules.
- Disconnect peers that send invalid or excessive snapshot transfer metadata.

### Medium: Receive And Send Queues Are Unbounded

The network session queues received packets and transport errors without a cap (`ModAPI.Networking/Sessions/NetworkSession.cs:21-23`, `659-672`). Application messages are also queued in an unbounded list before flush (`ModAPI.Networking/Sessions/NetworkSession.cs:460-472`, `585-598`). The UDP receive loop hands each datagram into that queue with a pooled buffer (`ModAPI.Networking/Transport/UdpSocketTransport.cs:166-184`).

Impact:
- Packet floods can consume memory if the main-thread update loop cannot drain fast enough.
- A stalled frame or paused game can accumulate network data.

Recommendation:
- Add max pending packet/error/outbound counts.
- Drop oldest or newest packets with diagnostics when caps are reached.
- Add per-peer and pre-handshake rate limits.

### Medium: Reconnect Identity Is Predictable And Plaintext

`CreateStablePeerId` uses role plus `Environment.MachineName`, and `ReconnectToken` is set equal to that stable id (`ShelteredAPI/Networking/MultiplayerConnectionTestService.cs:601-612`, `631-645`). Resume matching accepts stable peer id or reconnect token (`ModAPI.Networking/Sessions/NetworkHost.cs:252-276`).

Impact:
- Reconnect identity is not a secret.
- Another local-network peer that knows or guesses machine names can impersonate a disconnected peer's resume identity.

Recommendation:
- Generate random reconnect tokens with adequate entropy.
- Store them per session, expire them, and never derive them from machine names.
- Bind reconnect tokens to the original peer/session and authenticate them.

### Medium: Discovery Leaks Session Metadata And Machine Names

Discovery responses include application id, session id, peer counts, max peers, and display name (`ModAPI.Networking/Protocol/NetworkDiscoveryMessages.cs:40-57`; `ModAPI.Networking/Sessions/NetworkHost.cs:288-298`). Display names include `Environment.MachineName` (`ShelteredAPI/Networking/MultiplayerConnectionTestService.cs:615-629`).

Impact:
- LAN discovery exposes host identity details to anyone on the broadcast domain.
- Session id becomes observable before connection.

Recommendation:
- Use a user-controlled display name.
- Avoid sending session id in unauthenticated discovery replies unless needed.
- Add a privacy note in the UI if discovery remains enabled.

### Medium: Host Binds Publicly By Default

The UDP transport binds to `IPAddress.Any` and sets `ReuseAddress` (`ModAPI.Networking/Transport/UdpSocketTransport.cs:50-53`). The connection service enables broadcast and discovery (`ShelteredAPI/Networking/MultiplayerConnectionTestService.cs:580-581`).

Impact:
- The host listens on all IPv4 interfaces, including VPN and potentially public interfaces.
- That is normal for many game servers, but unsafe without authentication.

Recommendation:
- Let the user choose LAN-only interface binding.
- Warn clearly when hosting on all interfaces.
- Keep internet play disabled until authentication exists.

## Positive Findings

- The generic `ModAPI.Networking` layer stays game-neutral. I found no Unity, Harmony, ShelteredAPI, or gameplay-type references in that project during the review.
- Packet size defaults to 1200 bytes, which is a good UDP/MTU choice (`ModAPI.Networking/NetworkDefaults.cs:10-13`).
- The code uses explicit primitive serializers rather than dangerous general-purpose deserialization such as `BinaryFormatter`.
- Reserved session message ids are separated from application messages (`ModAPI.Networking/Sessions/NetworkSession.cs:443-449`).
- Compatibility hashes are useful for honest-peer mismatch detection (`ShelteredAPI/Networking/Compatibility/ShelteredMultiplayerCompatibilityHasher.cs:16-24`, `60-63`).
- Save package file paths are protected against obvious traversal through `CombineSafe` (`ShelteredAPI/Networking/ShelteredMultiplayerSaveSyncService.cs:985-997`).
- Setup state has stale-session checks on the client path (`ShelteredAPI/Networking/ShelteredMultiplayerSetupService.cs:332-365`).

## Standardness Assessment

Standard or reasonably normal:
- Custom UDP packet header with magic, protocol version, sequence, ACK, ACK bitfield.
- Reliable-unordered delivery for selected application messages.
- MTU-sized packet batching.
- LAN broadcast discovery.
- Explicit app/game layer above a neutral transport layer.
- Compatibility hash check before gameplay sync.

Non-standard or risky:
- No cryptographic packet authentication for a protocol that mutates saves and gameplay state.
- Treating source endpoint plus handshake metadata as peer identity.
- Accepting empty session id/nonce during join.
- Letting generic authoritative event phases cross the network without sender-role checks.
- Handling save sync as direct network-to-filesystem application messages.
- Using a class named `MultiplayerConnectionTestService` as the main game-facing multiplayer runtime.
- No queue caps, rate limits, or transfer budgets around UDP input and snapshot assembly.

## Priority Fix List

1. Block remote authoritative envelopes from non-host peers.
2. Block `ReleaseStartMessageType` on hosts and require clients to accept release only from the host.
3. Add receive queue caps and pre-handshake rate limiting.
4. Replace machine-name reconnect tokens with random per-session bearer tokens.
5. Add an optional join code and packet HMAC before considering internet or untrusted LAN use.
6. Keep raw shelter save transfer removed; reconnect/catchup should use explicit shared-world/map snapshots and events only.
7. Add tests for the remaining role-confusion cases: client release to host and client authoritative event to host.

## Bottom Line

This is well-structured prototype networking, not secure multiplayer infrastructure. I would be comfortable testing it on a trusted local network with throwaway saves. I would not expose it to the internet, VPN strangers, public Wi-Fi, or any untrusted peer until the critical sender-role checks and authentication story are fixed.
