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
| `Reliability` | Sequence comparison and ACK bitfield tracking. |
| `Transport` | UDP socket transport with pooled receive buffers and error events. |
| `Connections` | Neutral connection state record. |
| `Diagnostics` | `MMLog` bridge using the `Network` category. |

## First Implementation Scope

The first slice intentionally provides reusable primitives rather than a complete multiplayer stack:

- packet magic/version/header format,
- MTU-sized batching,
- reliable/unreliable channel markers,
- cumulative ACK bitfield helper,
- pooled UDP receive buffers,
- send/receive transport events,
- ModAPI network diagnostics.

The next slice should add the session layer:

- handshake request/response messages,
- protocol/content hash fields,
- peer registry,
- heartbeat scheduling,
- reliable resend queue,
- disconnect timeout handling,
- packet-loss simulation hooks for diagnostics.

Sheltered-specific systems such as host ticks, expedition route sync, deterministic encounter patches, save snapshots, and desync recovery stay outside this assembly.
