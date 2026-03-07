# ModAPI Developer Commenting Standard (Current v1.3 Line)

This project is shipped as a developer-facing platform. Comments should optimize for onboarding speed.

## Goals

- Explain intent and usage, not obvious syntax.
- Make architecture and lifecycle boundaries discoverable in IDE tooltips.
- Keep comments stable under refactors.

## What to Document First

1. Public APIs used by mod authors (`IPluginContext`, actors, events, save APIs, registries).
2. Loader lifecycle boundaries (bootstrap, plugin init/start, scene hooks, shutdown).
3. High-risk internals (reflection paths, Harmony patching strategy, threading/main-thread assumptions).

## Preferred Style

- Use XML doc comments (`///`) on public/protected APIs and important internal entry points.
- Use short inline comments only where runtime behavior is non-obvious.
- Keep comments imperative and practical: "Use X when..." or "Runs before Y...".

## Avoid

- Restating code literally.
- Large historical notes inside core runtime files.
- Long prose where a short summary is enough.

## Recommended Rollout Order

1. `ModAPI/Core/*`
2. `ModAPI/Actors/*`
3. `ModAPI/Events/*` and save lifecycle APIs
4. `ModAPI/Harmony/*`
5. `ModAPI/UI/*` and runtime UI hooks
