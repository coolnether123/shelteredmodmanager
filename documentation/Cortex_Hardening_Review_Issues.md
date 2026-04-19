# Cortex Hardening Review Issues

This document records Cortex issues found during the adversarial review pass.
It focuses on trust boundaries, runtime control paths, and update-loop behavior.

## Scope

Reviewed areas:

- desktop bridge and Avalonia host communication
- runtime shell bridge intent handling
- workspace tree synchronization
- pipe message framing
- bundled Tabby server startup

The issues below are implementation risks, not architecture objections. Cortex has a useful separation between shared contracts, portable runtime/core code, Unity host code, Avalonia host code, and plugins. The main hardening gap is that some process and filesystem boundaries are still treated like trusted in-process UI paths.

## Issue 1: Bridge accepts unauthenticated runtime intents

Priority: P1

Files:

- `Cortex.Shell.Unity.Imgui/CortexShell.cs`
- `Cortex.Host.Avalonia/Bridge/NamedPipeDesktopBridgeClient.cs`
- `Cortex.Bridge/BridgeMessageModels.cs`

Problem:

The desktop bridge creates a new runtime session from any `OpenSessionRequest` and stores the supplied launch token, but the runtime side does not verify that token against the token generated when launching the Avalonia host. Runtime intent handlers also process non-handshake messages without first requiring the envelope `SessionId` to match the active bridge session.

Impact:

A local same-user process that connects to the fixed pipe name before or instead of the real desktop host can send runtime intents. That path can drive actions such as workspace root changes, settings saves, file previews, search updates, and overlay state changes.

Recommended fix:

- Generate and retain the expected launch token on the runtime side when launching the external host.
- Reject `OpenSessionRequest` messages whose launch token does not match the expected token.
- Require matching `SessionId` for every non-handshake message.
- Reject intents before dispatch when there is no active authenticated session.
- Redact launch tokens from logs and status messages.

## Issue 2: Workspace tree is rebuilt every shell update

Priority: P1

Files:

- `Cortex/Shell/Bridge/RuntimeDesktopBridgeWorkspaceFeature.cs`
- `Cortex.Shell.Unity.Imgui/CortexShell.cs`

Problem:

`SynchronizeFromRuntime()` reloads project state, reselects the project, and calls `RefreshWorkspaceTree()` before checking whether the workspace state changed. `UpdateShell()` calls `PumpDesktopBridge()` every frame, and `PumpDesktopBridge()` calls this synchronization path even when no desktop bridge snapshot needs to be published.

Impact:

The runtime can recursively walk the workspace on every shell update. A normal repository can create avoidable frame-time cost; a large workspace, generated tree, or junction-heavy tree can freeze the Unity runtime.

Recommended fix:

- Add a cheap workspace token that includes workspace root, selected project, and project catalog revision.
- Rebuild the workspace tree only when that token changes or when an explicit refresh is requested.
- Skip bridge synchronization work entirely when no desktop client is connected.
- Keep filesystem traversal off the per-frame hot path.

## Issue 3: Pipe message length is trusted before allocation

Priority: P2

Files:

- `Cortex/Shell/Bridge/NamedPipeDesktopBridgeHost.cs`
- `Cortex.Host.Avalonia/Bridge/NamedPipeDesktopBridgeClient.cs`
- `Cortex/Shell/Bridge/BridgeMessageSerializer.cs`
- `Cortex.Host.Avalonia/Bridge/BridgeMessageSerializer.cs`

Problem:

The named-pipe bridge reads a 32-bit payload length and immediately allocates a byte array of that size. There is no maximum frame size before allocation. Deserialization failures are also not consistently converted into controlled connection faults.

Impact:

A malformed or hostile peer can send a large positive length and force large allocations or an out-of-memory failure. Invalid XML payloads can also terminate bridge read loops without useful diagnostics.

Recommended fix:

- Define a protocol maximum frame size.
- Reject payload lengths less than or equal to zero or greater than the maximum before allocation.
- Treat deserialization failure as a bad frame and close the connection.
- Add diagnostics that report frame rejection without dumping payload contents.
- Keep the maximum centralized so runtime and Avalonia sides cannot drift.

## Issue 4: Ollama API token is exposed in child process arguments

Priority: P2

Files:

- `Cortex.Tabby/BundledTabbyServerController.cs`
- `Cortex.Tabby.Server/Configuration/TabbyServerArgumentParser.cs`
- `Cortex.Tabby.Server/Configuration/TabbyServerOptions.cs`

Problem:

The bundled Tabby server is launched with `--ollama-api-token` in the command line. Command-line arguments are visible to local process inspection tools and can also appear in crash reports, telemetry, or diagnostic captures.

Impact:

If the Ollama endpoint uses a real bearer token, that token can leak to other local processes or logs.

Recommended fix:

- Pass the Ollama API token through an inherited environment variable, restricted temporary config file, or stdin.
- Prefer an environment variable for the shortest change if the server and launcher both run under the same user.
- Keep command-line logging redacted.
- Avoid echoing received tokens in server logs or health failures.
- Preserve the existing command-line option only as a backward-compatible fallback if needed.

## Hardening Order

1. Authenticate and validate bridge sessions.
2. Remove workspace tree traversal from the per-frame update path.
3. Add bounded pipe frame handling on both bridge endpoints.
4. Move secrets out of child process command-line arguments.

## Acceptance Checks

- A bridge client with the wrong launch token cannot open a session.
- A bridge message with a stale, empty, or mismatched `SessionId` is rejected before intent dispatch.
- No recursive workspace tree build occurs during a normal frame when workspace state did not change.
- Oversized pipe frames are rejected before allocation.
- The Ollama API token is not visible in the Tabby server process command line.
