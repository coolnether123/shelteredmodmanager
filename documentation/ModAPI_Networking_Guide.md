# ModAPI Networking Guide

`ModAPI.Networking.dll` is the host-neutral networking companion assembly for ModAPI v1.4 work.

It owns transport and protocol infrastructure only. It does not know about Sheltered, Unity scene state, game managers, Harmony patches, or multiplayer gameplay rules.

## Assembly Rule

- Reference `ModAPI.dll` for neutral framework APIs.
- Reference `ModAPI.Networking.dll` when a mod or runtime assembly needs host-neutral networking primitives.
- Reference `ShelteredAPI.dll` when networking behavior needs Sheltered game state, UI, saves, Harmony patches, or Unity object integration.

Dependency direction:

```text
Sheltered-owned multiplayer runtime
  -> ShelteredAPI
  -> ModAPI.Networking
  -> ModAPI
```

`ModAPI.Networking` must not reference:

- `UnityEngine`
- `Harmony`
- `ShelteredAPI`
- `Assembly-CSharp`
- Sheltered gameplay vocabulary such as bunkers, expeditions, factions, items, panels, or save managers

Any Harmony patches for networked gameplay belong in `ShelteredAPI` or another Sheltered-owned runtime assembly.

## Current Module Map

| Area | Purpose |
| --- | --- |
| `Buffers` | Fixed-size pooled byte buffers for send/receive paths. |
| `Serialization` | Primitive little-endian packet readers and writers. |
| `Protocol` | Packet headers, message channels, message frames, batch writer/reader. |
| `Reliability` | Sequence comparison, ACK bitfield tracking, resend retention, and ACK cleanup. |
| `Transport` | UDP socket transport with pooled receive buffers, error events, and optional send-side simulation hooks. |
| `Connections` | Neutral connection state record and peer registry. |
| `Sessions` | Host/client session facade, handshake, peer lifecycle, heartbeats, disconnects, and raw message APIs. |
| `Diagnostics` | `MMLog` bridge, counters, latency estimates, and compact recent-event snapshots. |
| `Events` | Opaque event envelopes and typed payload serializer registry for higher-level event sync. |
| `Snapshots` | Byte-oriented chunking/reassembly helpers for higher-level snapshot formats. |
| `Addressing` | Manual endpoint parsing and best-effort local IPv4 adapter selection. |
| `Discovery` | Optional direct endpoint and broadcast discovery helpers. |

## First Implementation Scope

The first slice intentionally provides reusable primitives rather than a complete multiplayer stack:

- packet magic/version/header format,
- MTU-sized batching,
- reliable/unreliable channel markers,
- cumulative ACK bitfield helper and ACK-only flush packets,
- pooled UDP receive buffers,
- optional outbound packet loss, latency, and jitter simulation knobs for diagnostics,
- send/receive transport events,
- ModAPI network diagnostics,
- host/client session startup,
- versioned handshake accept/reject flow,
- peer connected/disconnected/message/error events,
- heartbeat and timeout handling,
- reliable resend queue with ACK cleanup and duplicate receive suppression,
- raw send-to-host, send-to-peer, and broadcast APIs,
- manual direct-IP endpoint parsing,
- optional LAN/broadcast discovery,
- generic event envelopes,
- byte snapshot chunk/reassembly helpers.

Remaining neutral networking work should focus on hardening and acceptance coverage:

- ordered reliable-channel policy if callers need strict ordering beyond duplicate suppression,
- two-process localhost test harness,
- broader packet loss/jitter/latency soak tests,
- public examples for host start, direct-IP join, reliable/unreliable sends, broadcast, and clean shutdown.

## Diagnostics Simulation

`NetworkConfig` exposes disabled-by-default simulation fields for local testing:

- `SimulatedPacketLossPercent`
- `SimulatedLatencyMilliseconds`
- `SimulatedJitterMilliseconds`

These apply in `UdpSocketTransport.Send` only. They are intended for diagnostics and acceptance tests, not for production gameplay policy.

Sheltered-specific systems such as host ticks, expedition route sync, deterministic encounter patches, save snapshots, and desync recovery stay outside this assembly.
