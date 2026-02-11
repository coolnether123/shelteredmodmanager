# ModAPI Developer Commenting Standard (v1.2)

This project is shipped as a developer-facing platform. Comments should optimize for onboarding speed.

## Goals
- Explain intent and usage, not obvious syntax.
- Make architecture and lifecycle boundaries discoverable in IDE tooltips.
- Keep comments stable under refactors.

## What to document first
1. Public APIs used by mod authors (`IPluginContext`, events, save APIs, registries).
2. Loader lifecycle boundaries (bootstrap, plugin init/start, scene hooks, shutdown).
3. High-risk internals (reflection paths, Harmony patching strategy, threading/main-thread assumptions).

## Preferred style
- Use XML doc comments (`///`) on public/protected APIs and important internal entry points.
- Use short inline comments only where runtime behavior is non-obvious.
- Keep comments imperative and practical: "Use X when..." / "Runs before Y..."

## Avoid
- Restating code literally.
- Large historical notes inside core runtime files.
- Long prose where a 1-2 sentence summary is enough.

## Loader baseline (completed)
- `ModAPI/Core/PluginManager.cs`
- `ModAPI/Core/PluginRunner.cs`
- `ModAPI/Core/PluginContextImpl.cs`
- `ModAPI/Core/PrefixedLogger.cs`

## Recommended rollout order
1. `ModAPI/Core/*` public-facing contracts and manager classes
2. `ModAPI/Events/*` and `ModAPI/Saves/*` lifecycle APIs
3. `ModAPI/Harmony/*` developer helpers and transpiler APIs
4. `ModAPI/UI/*` extension helpers and hooks
