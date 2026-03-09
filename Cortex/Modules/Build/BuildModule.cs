using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex.Modules.Build
{
    public sealed class BuildModule
    {
        private Vector2 _scroll = Vector2.zero;
        private string _configuration = string.Empty;

        public void Draw(IBuildCommandResolver resolver, IBuildExecutor executor, IRestartCoordinator restartCoordinator, ISourcePathResolver sourcePathResolver, IDocumentService documentService, CortexShellState state)
        {
            if (state.SelectedProject == null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Select a project first. The build view uses the selected project definition and verifies the output assembly before allowing restart.");
                GUILayout.EndVertical();
                return;
            }

            if (string.IsNullOrEmpty(_configuration))
            {
                _configuration = state.Settings != null ? state.Settings.DefaultBuildConfiguration : "Debug";
            }

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Configuration", GUILayout.Width(80f));
            _configuration = GUILayout.TextField(_configuration, GUILayout.Width(120f));
            if (GUILayout.Button("Build", GUILayout.Width(90f)))
            {
                ExecuteBuild(resolver, executor, restartCoordinator, state, false, false, _configuration);
            }
            if (GUILayout.Button("Clean & Build", GUILayout.Width(120f)))
            {
                ExecuteBuild(resolver, executor, restartCoordinator, state, true, false, _configuration);
            }
            if (GUILayout.Button("Verify & Restart", GUILayout.Width(130f)))
            {
                ExecuteBuild(resolver, executor, restartCoordinator, state, false, true, _configuration);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Timeout: " + (state.Settings != null ? state.Settings.BuildTimeoutMs / 1000 : 300) + "s");
            GUILayout.EndHorizontal();

            var result = state.LastBuildResult;
            if (result == null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("No build has been run yet.");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Last build: " + (result.Success ? "Success" : "Failure") +
                " | ExitCode=" + result.ExitCode +
                " | Duration=" + result.Duration.TotalSeconds.ToString("F2") + "s" +
                " | TimedOut=" + (result.TimedOut ? "Yes" : "No") +
                " | OutputUpdated=" + (result.OutputAssemblyUpdated ? "Yes" : "No"));
            GUILayout.EndVertical();

            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            if (result.Diagnostics.Count > 0)
            {
                GUILayout.Label("Diagnostics");
                for (var i = 0; i < result.Diagnostics.Count; i++)
                {
                    var item = result.Diagnostics[i];
                    var label = item.Severity + ": " + item.FilePath + "(" + item.Line + "," + item.Column + ") " + item.Code + " " + item.Message;
                    if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
                    {
                        var path = sourcePathResolver != null ? sourcePathResolver.ResolveCandidatePath(state.SelectedProject, state.Settings, item.FilePath) : string.Empty;
                        if (!string.IsNullOrEmpty(path))
                        {
                            CortexModuleUtil.OpenDocument(documentService, state, path, item.Line);
                            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                            state.StatusMessage = "Opened " + path + " from build diagnostics.";
                        }
                    }
                }
                GUILayout.Space(8f);
            }

            GUILayout.Label("Output");
            for (var i = 0; i < result.OutputLines.Count; i++)
            {
                var line = result.OutputLines[i] ?? string.Empty;
                if (GUILayout.Button(line, GUILayout.ExpandWidth(true)))
                {
                    var location = sourcePathResolver != null ? sourcePathResolver.ResolveTextLocation(line, state.SelectedProject, state.Settings) : null;
                    if (location != null && location.Success)
                    {
                        CortexModuleUtil.OpenDocument(documentService, state, location.ResolvedPath, location.LineNumber);
                        state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                        state.StatusMessage = "Opened " + location.ResolvedPath + " from build output.";
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        private void ExecuteBuild(IBuildCommandResolver resolver, IBuildExecutor executor, IRestartCoordinator restartCoordinator, CortexShellState state, bool clean, bool restartAfter, string configuration)
        {
            var command = resolver.Resolve(state.SelectedProject, clean, configuration);
            if (command == null)
            {
                state.StatusMessage = "No build command could be resolved.";
                return;
            }

            command.TimeoutMs = state.Settings != null ? state.Settings.BuildTimeoutMs : 300000;
            state.LastBuildResult = executor.Execute(command);

            if (!state.LastBuildResult.Success)
            {
                state.StatusMessage = state.LastBuildResult.TimedOut ? "Build timed out." : "Build failed.";
                return;
            }

            state.StatusMessage = "Build succeeded.";
            if (!restartAfter)
            {
                return;
            }

            string errorMessage;
            if (restartCoordinator.RequestCurrentSessionRestart(out errorMessage))
            {
                state.StatusMessage = "Build verified. Restart requested.";
            }
            else
            {
                state.StatusMessage = "Restart failed: " + errorMessage;
            }
        }
    }
}
