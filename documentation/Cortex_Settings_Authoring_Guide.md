# Cortex Settings Authoring Guide

This guide describes the modular settings contribution system used by Cortex.

The design goal is the same one Spine settings aims for: modules declare metadata once, and the host does the repetitive work.

In Cortex that means a module should register:

1. One section contribution for a settings scope
2. One or more setting contributions inside that scope
3. Optional value/validation/action callbacks when the module owns its own storage

Cortex then builds:

- the left navigation group
- the section anchor in the continuous settings document
- search indexing and section reveal behavior
- the editor control shape for each setting
- default ordering inside the section
- modified-state tracking
- validation messages
- per-setting gear actions such as reset/copy/custom actions

## Recommended pattern

Register one section per scope, then register settings under that same `Scope` value.

```csharp
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

public sealed class ExamplePlugin : IWorkbenchPluginContributor
{
    public string PluginId
    {
        get { return "example.plugin"; }
    }

    public string DisplayName
    {
        get { return "Example Plugin"; }
    }

    public void Register(WorkbenchPluginContext context)
    {
        context.RegisterSettingSection(
            "Example",
            "extensions",
            "Extensions",
            "example.general",
            "Example",
            "Settings for the Example module.",
            new[] { "example", "sample", "demo" },
            100);

        context.RegisterSetting(
            "ExampleApiUrl",
            "API URL",
            "Base URL used by the example module.",
            "Example",
            "http://localhost:8080",
            SettingValueKind.String,
            0,
            SettingEditorKind.Path,
            "http://localhost:8080",
            "Set the endpoint the module should call.",
            new[] { "endpoint", "url", "api" },
            new SettingChoiceOption[0],
            false);

        context.RegisterSetting(
            "ExampleMode",
            "Mode",
            "Execution mode for the example module.",
            "Example",
            "Safe",
            SettingValueKind.String,
            10,
            SettingEditorKind.Choice,
            string.Empty,
            string.Empty,
            new[] { "mode", "safe", "fast" },
            new[]
            {
                new SettingChoiceOption { Value = "Safe", DisplayName = "Safe", Description = "Prefer guardrails and validation." },
                new SettingChoiceOption { Value = "Fast", DisplayName = "Fast", Description = "Prefer speed over extra checks." }
            },
            false);

        context.RegisterSetting(
            "ExampleNotes",
            "Notes",
            "Additional module instructions.",
            "Example",
            string.Empty,
            SettingValueKind.String,
            20,
            SettingEditorKind.MultilineText,
            "Describe extra behavior or reminders.",
            string.Empty,
            new[] { "notes", "instructions" },
            new SettingChoiceOption[0],
            false);
    }
}
```

## Section metadata

Use `RegisterSettingSection(...)` once per logical scope.

Fields:

- `scope`: stable scope label shared by the settings in that section
- `groupId`: left-nav group id, for example `tooling`, `ai`, `editor`, or `extensions`
- `groupTitle`: user-facing group title shown in the left nav
- `sectionId`: stable anchor id for the section in the scrolling document
- `sectionTitle`: visible section title
- `description`: summary text shown at the top of the section
- `keywords`: extra search terms
- `sortOrder`: section order inside the document

If a module registers settings without section metadata, Cortex still renders them using fallback grouping rules. Registering a section contribution is the preferred path because it gives the host enough information to build the correct navigation and search surface.

## Setting metadata

Use `RegisterSetting(...)` with the extended overload when you want Cortex to pick the right editor and search terms automatically.

Important fields:

- `settingId`: for built-in settings this can match a persisted `CortexSettings` field name; external modules can instead provide callbacks or let Cortex persist the value automatically by `settingId`
- `displayName`: user-facing label
- `description`: property-grid description text
- `scope`: must match the owning section contribution
- `defaultValue`: serialized default
- `valueKind`: persisted type
- `sortOrder`: ordering inside the section
- `editorKind`: preferred editor style
- `placeholderText`: example value shown as helper text when appropriate
- `helpText`: extra inline guidance
- `keywords`: search aliases
- `options`: selectable values for `Choice`
- `isSecret`: marks the value as hidden even if `editorKind` is left as `Auto`
- `isRequired`: enables host-side required-value validation
- `readValue`: optional callback used when the module owns persistence
- `writeValue`: optional callback used when the module owns persistence
- `readDefaultValue`: optional callback for dynamic defaults
- `validateValue`: optional callback for module-specific validation
- `actions`: optional gear-menu actions for that setting

If `readValue` / `writeValue` are omitted, Cortex falls back to:

1. the legacy `CortexSettings` reflection path for built-in settings
2. the host-managed `ModuleSettings` value bag for arbitrary module settings

## Supported editor kinds

- `Auto`: let Cortex choose from `valueKind`
- `Text`: single-line text field
- `Path`: single-line field with clipboard-oriented path actions
- `MultilineText`: larger text area
- `Choice`: option toolbar driven by `SettingChoiceOption[]`
- `Secret`: masked input for tokens, keys, and passwords

## Search and reveal behavior

Cortex indexes:

- section titles and descriptions
- section keywords
- setting names, descriptions, ids, and scopes
- setting help text and placeholder text
- choice option names and descriptions

When a search query matches a section, Cortex reveals that section inside the continuous settings document and exposes its anchors in the left navigation.

## Built-in example

The Roslyn language-service settings already use this pattern in [ShelteredWorkbenchSettingContributions.cs](/D:/Projects/_Archived/Sheltered%20Modding/shelteredmodmanager/Cortex.Host.Sheltered/Composition/ShelteredWorkbenchSettingContributions.cs).

That registration gives Cortex enough information to:

- place `Language Service` under the `Tooling` group
- render its section in the main scrollable document
- expose it in search
- show typed controls for its settings

## Module-owned storage example

When a module owns its own settings object, register callbacks instead of depending on `CortexSettings` fields:

```csharp
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

public sealed class ExamplePlugin : IWorkbenchPluginContributor
{
    private readonly ExampleSettings _settings = new ExampleSettings();

    public string PluginId
    {
        get { return "example.plugin"; }
    }

    public string DisplayName
    {
        get { return "Example Plugin"; }
    }

    public void Register(WorkbenchPluginContext context)
    {
        context.RegisterSettingSection(
            "Example",
            "extensions",
            "Extensions",
            "example.general",
            "Example",
            "Settings for the Example module.",
            new[] { "example", "sample", "demo" },
            100);

        context.RegisterSetting(
            "example.api.url",
            "API URL",
            "Base URL used by the example module.",
            "Example",
            "http://localhost:8080",
            SettingValueKind.String,
            0,
            SettingEditorKind.Text,
            "http://localhost:8080",
            "Set the endpoint the module should call.",
            new[] { "endpoint", "url", "api" },
            new SettingChoiceOption[0],
            false,
            true,
            delegate { return _settings.ApiUrl; },
            delegate(string value) { _settings.ApiUrl = value ?? string.Empty; },
            delegate { return "http://localhost:8080"; },
            delegate(string value)
            {
                System.Uri uri;
                return System.Uri.TryCreate(value ?? string.Empty, System.UriKind.Absolute, out uri)
                    ? null
                    : new SettingValidationResult
                    {
                        Severity = SettingValidationSeverity.Error,
                        Message = "Enter a valid absolute URL."
                    };
            },
            new[]
            {
                new SettingActionContribution
                {
                    ActionId = "example.ping",
                    DisplayName = "Use staging endpoint",
                    Execute = delegate(SettingActionInvocation invocation)
                    {
                        invocation.SetDraftValue("https://staging.example.dev");
                        invocation.SetStatusMessage("Updated Example API URL to the staging endpoint.");
                    }
                }
            });
    }
}
```

That gives the module:

- host-managed rendering/search/nav behavior
- host-managed modified indicators and reset-to-default
- host-managed gear menu chrome
- module-owned persistence and validation logic

## Design rules

- Register section metadata once per scope.
- Keep scope names stable.
- Prefer the extended `RegisterSetting(...)` overload for new work.
- Prefer callback-backed settings for module-owned storage instead of extending `CortexSettings`.
- Add keywords for likely search terms, acronyms, and legacy names.
- Keep module-specific UI logic out of the settings renderer when metadata can express the same intent.
- Treat Cortex as the settings host and your module as the metadata source.
