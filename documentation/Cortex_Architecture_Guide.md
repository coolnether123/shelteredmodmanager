# Cortex Architecture Guide

This document describes Cortex as it exists in the current codebase, what responsibilities it owns, how it interacts with the rest of Sheltered Mod Manager, and how it is designed to keep growing into a larger IDE-style environment.

For exact public contracts, prefer the code. This document is architectural and operational rather than a signature dump.

## Compatibility Matrix

| Scope | Applies To | Status |
|-------|------------|--------|
| Cortex shell, modules, docking, editor, decompiler, Roslyn integration | Current codebase | Supported |
| Current extension and plugin direction | Current codebase | Supported |
| Full IDE roadmap items not yet implemented | Design intent | Planned |

## 1. What Cortex Is

Cortex is the in-game IDE and tooling shell for Sheltered Mod Manager.

It is responsible for:
- presenting a modular workbench UI inside the game
- hosting tool windows such as Solution Explorer, logs, projects, references, and settings
- opening source files and decompiled files as tabbed documents
- routing language intelligence through an out-of-process Roslyn worker
- routing metadata/source fallback through the decompiler pipeline
- acting as a future host for additional Cortex-specific tools and plugins

It is not intended to be a monolithic script.

The architecture splits Cortex into:
- shell and layout orchestration
- shared state and persistence
- standalone modules
- service-driven backend logic
- an external language-service worker
- an external decompiler toolchain

That separation is what allows it to keep growing without collapsing into one large Unity UI class.

## 2. High-Level Design

At a high level, Cortex is composed of these layers:

1. `Cortex`
- the Unity-facing shell
- owns window lifecycle, module draw loop, language-service coordination, docking, state, and runtime orchestration

2. `Cortex.Core`
- models, abstractions, stores, project catalog, document service, source/decompiler services, worker client

3. `Cortex.Presentation`
- workbench presentation models and presenter logic

4. `Cortex.Rendering` and `Cortex.Renderers.Imgui`
- rendering abstractions, renderer-neutral surface models, and IMGUI renderer implementation

5. `Cortex.Host.Unity`
- Unity host composition and default workbench setup

6. `Cortex.Roslyn.Worker`
- out-of-process Roslyn worker on a modern .NET runtime
- handles classifications, diagnostics, hover, and definition resolution

7. `Decompiler`
- external decompiler toolchain used for metadata/source fallback and assembly browsing

## 3. Core User Experience Goals

Cortex is trying to feel like a lightweight Visual Studio-style environment inside the game:
- a central editor surface with tabbed documents
- docked side/bottom panels
- solution/workspace browsing
- decompiler browsing
- hover and go-to-definition
- logs and build output
- settings and keybinding support
- future extensibility for more tools

That is why the architecture favors:
- modular windows instead of one page
- service boundaries instead of module-to-module coupling
- a custom code surface instead of relying entirely on Unity text controls
- out-of-process language services instead of forcing Roslyn into Unity's old runtime

## 4. Shell Responsibilities

`CortexShell` is the central coordinator.

Its responsibilities include:
- initializing and shutting down Cortex services
- owning workbench state
- owning the active render pipeline
- drawing the main IDE window
- routing module rendering into dock hosts
- persisting layout, open docs, and selection state
- coordinating Roslyn requests and applying results
- coordinating source/decompiler navigation
- hosting future external Cortex tool integrations

The shell should stay orchestration-focused.

It should not become:
- a giant file parser
- a decompiler implementation
- a Roslyn implementation
- a module-specific UI dumping ground

## 5. Workbench Model

The workbench is intentionally modular.

Conceptually it is made of:
- containers/hosts
- tabs within those hosts
- a layout tree
- host overrides for docking
- persistence state for active tabs and hidden panels

This allows Cortex to behave like a real IDE surface rather than a fixed screen.

Current design intent:
- documents live in the central editor host
- tool windows can be docked on the sides or bottom
- tabs can be opened, focused, and closed independently
- additional tools can be added later without reworking the shell

## 6. Module System

Cortex functionality is split into modules rather than one monolithic UI.

Representative modules include:
- Editor
- File Explorer / Solution Explorer
- Projects
- Reference / decompiler browsing
- Logs
- Build
- Settings

Each module should:
- render only its own surface
- work through abstractions/services
- avoid directly reaching into unrelated module internals
- remain replaceable or evolvable

This is important because Cortex is expected to grow into a fuller IDE host.

## 7. Document System

Documents are represented as `DocumentSession` objects.

A document session tracks:
- file path
- current text
- original text snapshot
- dirty state
- highlighted line
- language analysis result
- document text version
- last analysis version
- mutation timestamps

This matters because Cortex is no longer treating document intelligence as a stateless lookup.

The document version is now part of the language pipeline so:
- edits can be analyzed incrementally
- stale hover results can be ignored
- stale definition results can be ignored
- analysis can be debounced after mutations

## 8. Editor Surface

The editor currently has two modes:

1. Read-only code surface
- custom token-based renderer
- syntax coloring
- hover
- definition navigation
- right-click context menu
- folding
- line focus/navigation

2. Editable text mode
- custom writable code surface
- custom caret, selection, scroll, completion, and overlay behavior
- uses the same central editor context model as the read-only surface
- used when editing is unlocked

The current direction is not "read-only custom surface plus stock Unity text widgets."
Both read and edit paths are moving onto custom code surfaces so Cortex can support:
- precise symbol hit testing
- custom caret/selection behavior
- inline diagnostics
- smarter region folding
- richer interactions like rename, completion, and peek-like features

Unity IMGUI is still the host/event layer.
The important design choice is that Cortex should not depend on Unity's stock text widgets for IDE behavior, and non-renderer layers should not depend on Unity GUI types.

## 8.1 Editor Context And Selection Model

Cortex now treats editor interaction state as shared workbench state rather than local surface-only UI state.

`EditorContextService` is the central seam for:
- active editor context
- per-surface context snapshots
- selected symbol/target metadata
- hover response attachment
- semantic symbol context attachment

This matters because the selected symbol or hovered symbol is not just a paint concern.
Other workbench windows and workflows can build from the same cached context instead of scraping editor UI state.

That shared context is what should back:
- selected symbol highlighting
- related symbol occurrence highlighting
- hover metadata
- method inspector targeting
- future symbol-driven secondary panes

## 8.2 Hover Direction

The current hover design is:
- one shared hover controller/service for read and edit surfaces
- renderer-agnostic hover models in `Cortex.Rendering`
- IMGUI-specific hover drawing only in `Cortex.Renderers.Imgui`
- central hover publication through `EditorContextService`

The intended user experience is Visual Studio-style hover behavior:
- qualified symbol path
- signature/display parts
- overload summaries where available
- documentation/details
- sticky hover that remains active while moving from the token to the hover window
- interactive hover parts that can be clicked for navigation

That keeps hover behavior authoritative in one place instead of splitting semantic state and sticky-window behavior across the two editor surfaces.

## 8.3 Overlay Rendering Direction

Panels, popup menus, and hover surfaces are moving behind renderer abstractions.

The design intent is:
- keep `IRenderPipeline` small
- keep overlay rendering behind narrow interfaces
- keep render-time dependencies separate from editor service dependencies
- allow IMGUI to remain the default backend without making the Cortex layer IMGUI-dependent

In practice that means:
- renderer-neutral geometry and color models belong in `Cortex.Rendering`
- IMGUI drawing belongs in `Cortex.Renderers.Imgui`
- editor surfaces should consume renderer contracts, not construct IMGUI overlay widgets directly

## 9. Syntax Coloring and Semantic Data

Syntax coloring comes from Roslyn classifications, not hardcoded token guessing.

The flow is:
1. active document is queued for analysis
2. Roslyn returns classified spans and diagnostics
3. Cortex stores that analysis on the document session
4. the code surface renders token spans using a Visual Studio-inspired color map

This is designed to grow into:
- user-customizable syntax themes
- richer semantic styling
- inline warnings/errors
- future semantic adornments

## 10. Roslyn Integration

Cortex uses an out-of-process Roslyn worker instead of embedding Roslyn directly in the Unity runtime.

That was the correct architectural choice because:
- Unity-side Cortex is on an older runtime boundary
- Roslyn wants a modern .NET environment
- language services are cleaner when isolated
- crashes/timeouts are easier to contain out of process

Current Roslyn capabilities:
- document analysis
- classifications
- diagnostics
- hover
- definition lookup

## 11. Roslyn Request Pipeline

The earlier prototype approach was effectively:
- synchronous transport
- background-thread wrapper in the shell

That is no longer the direction.

The current direction is:
- queued transport in the client
- request IDs
- response correlation
- timeout handling
- shell-side request state
- document-version-aware stale response rejection
- debounced analysis after edits

This is specifically to avoid:
- UI hitching
- stale hover applying after newer edits
- stale definitions opening from old snapshots
- unnecessary status round-trips on the hot path

## 12. Performance Model

Performance matters because Cortex runs in-game.

Current performance principles:
- keep the Roslyn worker alive instead of restarting per request
- debounce analysis rather than analyze every frame
- avoid blocking the Unity main thread on language calls
- reject stale results instead of over-applying them
- cache document contexts in the worker by path+version
- warm known projects during initialize

This keeps editing and browsing responsive while still allowing rich language features.

The next performance step beyond the current code would be:
- persistent open/update/close document protocol
- delta-based edits instead of full text snapshots
- request prioritization
- cancellable hover/analysis work

## 13. Decompiler Integration

The decompiler is a first-class part of Cortex, not an afterthought.

It is used for:
- assembly browsing
- metadata definitions when source is unavailable
- opening DLL members/types in editor tabs
- XML documentation lookup beside DLLs

Definition behavior is intended to be:
- source if source exists
- decompiler fallback when the symbol lives in an assembly only

That is what enables navigating into things like:
- ModAPI helpers
- game assemblies
- framework-adjacent code available through the managed runtime

## 14. XML Documentation

When Cortex opens metadata-backed definitions, it can also read XML documentation files beside the target DLL.

This is designed so hover/reference surfaces can show:
- summary text
- remarks
- returns
- parameter descriptions

The XML parsing path now uses `System.Xml` instead of `System.Xml.Linq` so it remains compatible with the game runtime environment.

## 15. Solution Explorer and Workspace Model

The right-side explorer is intended to unify:
- real source workspace trees
- logical decompiler trees

That means Cortex supports both:
- source-first project browsing
- assembly/decompiler browsing

The decompiler branch is a logical tree, not just a filesystem mirror.

That distinction matters because IDE explorers often mix:
- physical files
- logical nodes
- generated views
- metadata-backed trees

## 16. Logs and Build Output

Cortex includes bottom-hosted operational surfaces for:
- runtime logs
- build output
- navigation from diagnostics/logs back to source

These are important because Cortex is not only a viewer; it is intended to be the live workflow surface for building and debugging mods while the game is running.

## 17. Settings and User Configuration

Cortex settings cover:
- workspace roots
- mod source links
- decompiler paths/cache paths
- Roslyn worker path/timeout
- theme
- build configuration and timeouts
- log panel behavior
- editing/saving toggles

This design allows Cortex to work across:
- source projects
- loaded mods
- assemblies without source
- future plugins/tools

## 18. Extension Direction

Cortex is intentionally being prepared to support more tools over time.

That includes:
- additional built-in modules
- external Cortex tools
- future mod-supplied Cortex plugins

The extension direction is:
- Cortex shell remains the host
- modules remain isolated
- plugins contribute tools/services/panels rather than patching random internals

This is why there is already work around external workbench plugin loading and Cortex-specific plugin abstractions.

## 19. Future Growth Areas

Cortex is already useful, but it is also clearly positioned to grow.

Natural next areas include:
- full custom editable code surface
- caret/selection model
- inline diagnostics and squiggles
- signature help
- completion
- find references
- rename
- symbol search
- document outline
- code actions / quick fixes
- more robust keybinding management
- stronger theme customization for syntax token classes
- richer plugin contribution APIs

The current architecture supports that growth because the core responsibilities are already separated.

## 20. Architectural Strengths

The strongest parts of Cortex today are:
- clear module boundaries
- shell/service separation
- central render pipeline direction instead of hardwiring IMGUI across the shell
- out-of-process Roslyn design
- decompiler fallback integration
- document session model
- central editor context model for symbol-driven workflows
- modular workbench layout
- compatibility with future external tools

Those choices are what make Cortex more than a prototype overlay.

## 21. Current Limitations

The major current limitations are mostly about depth, not direction:
- shared hover is centralized, but richer cancellation/scheduling policies for language requests can still improve responsiveness under heavy churn
- not every overlay or editor interaction has been fully pulled behind renderer-agnostic contracts yet
- some editor investigation/debug logging is still temporary and local to the implementation path; that should be refactored later into a cleaner Cortex-wide debugging approach instead of remaining scattered long-term
- transport is now queued, but there is still room for richer request scheduling and cancellation
- worker communication still uses whole-document snapshots rather than incremental deltas
- not every metadata symbol resolves to the most granular possible member in every case
- some advanced IDE features are still planned rather than complete

These are expected next-step limitations, not foundational design failures.

## 22. Design Principles

Cortex should continue following these principles:
- single responsibility per module/service
- shell as coordinator, not god object
- language intelligence out of process
- decompiler as a first-class navigation path
- versioned document state
- stale-response rejection
- extension-friendly workbench design
- runtime-safe dependencies for the in-game process

## 23. Practical Summary

If ModAPI is the runtime modding platform, Cortex is the in-game development environment layered on top of it.

Today, Cortex already provides:
- modular IDE shell
- tabbed document editor
- workspace and decompiler browsing
- Roslyn-backed color/hover/definition features
- decompiler fallback for metadata symbols
- logs, build, and settings surfaces

Its long-term value is that it is not boxed into being only a file viewer.

It is being built as:
- a host for multiple tools
- a live in-game development surface
- a bridge between source, assemblies, runtime logs, and future editor intelligence

That makes Cortex a platform, not just a panel.
