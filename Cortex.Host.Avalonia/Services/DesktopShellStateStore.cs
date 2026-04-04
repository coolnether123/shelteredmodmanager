using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cortex.Host.Avalonia.Models;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopShellStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private readonly string _stateFilePath;

        public DesktopShellStateStore(string stateFilePath)
        {
            _stateFilePath = stateFilePath ?? string.Empty;
        }

        public DesktopShellState Load(IReadOnlyList<DesktopWorkbenchSurfaceDefinition> surfaces)
        {
            var shellState = new DesktopShellState();
            if (!string.IsNullOrEmpty(_stateFilePath) && File.Exists(_stateFilePath))
            {
                try
                {
                    shellState = JsonSerializer.Deserialize<DesktopShellState>(File.ReadAllText(_stateFilePath), JsonOptions) ?? new DesktopShellState();
                }
                catch
                {
                    shellState = new DesktopShellState();
                }
            }

            Normalize(shellState, surfaces);
            return shellState;
        }

        public void Save(DesktopShellState shellState, IReadOnlyList<DesktopWorkbenchSurfaceDefinition> surfaces)
        {
            if (string.IsNullOrEmpty(_stateFilePath))
            {
                return;
            }

            Normalize(shellState, surfaces);
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath) ?? string.Empty);
            File.WriteAllText(_stateFilePath, JsonSerializer.Serialize(shellState, JsonOptions));
        }

        private static void Normalize(DesktopShellState shellState, IReadOnlyList<DesktopWorkbenchSurfaceDefinition> surfaces)
        {
            if (shellState == null)
            {
                return;
            }

            var stateLookup = new Dictionary<string, DesktopShellSurfaceState>(StringComparer.OrdinalIgnoreCase);
            if (shellState.SurfaceStates != null)
            {
                foreach (var state in shellState.SurfaceStates)
                {
                    if (state != null && !string.IsNullOrEmpty(state.SurfaceId))
                    {
                        stateLookup[state.SurfaceId] = state;
                    }
                }
            }

            shellState.SurfaceStates = new List<DesktopShellSurfaceState>();
            foreach (var surface in surfaces ?? Array.Empty<DesktopWorkbenchSurfaceDefinition>())
            {
                DesktopShellSurfaceState state;
                if (!stateLookup.TryGetValue(surface.SurfaceId, out state))
                {
                    state = new DesktopShellSurfaceState
                    {
                        SurfaceId = surface.SurfaceId,
                        IsVisible = surface.DefaultVisible
                    };
                }

                if (surface.IsRequired)
                {
                    state.IsVisible = true;
                }

                shellState.SurfaceStates.Add(new DesktopShellSurfaceState
                {
                    SurfaceId = surface.SurfaceId,
                    IsVisible = state.IsVisible
                });
            }

            if (!shellState.SurfaceStates.Any(state => string.Equals(state.SurfaceId, shellState.ActiveSurfaceId, StringComparison.OrdinalIgnoreCase) && state.IsVisible))
            {
                var activeSurface = shellState.SurfaceStates.FirstOrDefault(state => state.IsVisible);
                shellState.ActiveSurfaceId = activeSurface != null ? activeSurface.SurfaceId : string.Empty;
            }
        }
    }
}
