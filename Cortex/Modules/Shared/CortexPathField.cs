using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using UnityEngine;

namespace Cortex.Modules.Shared
{
    internal sealed class CortexPathFieldOptions
    {
        public bool AllowBrowse = true;
        public bool AllowOpen = true;
        public bool AllowReveal;
        public bool AllowPaste;
        public bool AllowClear = true;
        public string BrowseButtonText = "Browse...";
        public string OpenButtonText = "Open";
        public string RevealButtonText = "Show";
        public PathSelectionRequest BrowseRequest;
    }

    internal static class CortexPathField
    {
        private const string LogPrefix = "[Cortex.PathField] ";
        private static readonly Dictionary<string, string> _pendingSelectionsByField = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string DrawValueEditor(
            string fieldId,
            string value,
            IPathInteractionService pathInteractionService,
            CortexPathFieldOptions options,
            params GUILayoutOption[] textFieldOptions)
        {
            var nextValue = value ?? string.Empty;
            var effectiveOptions = options ?? new CortexPathFieldOptions();
            var normalizedFieldId = fieldId ?? string.Empty;
            if (!string.IsNullOrEmpty(normalizedFieldId))
            {
                TryApplyCompletedSelection(normalizedFieldId, pathInteractionService, ref nextValue);
            }
            var isWaiting = !string.IsNullOrEmpty(normalizedFieldId) && _pendingSelectionsByField.ContainsKey(normalizedFieldId);

            GUILayout.BeginHorizontal();
            nextValue = GUILayout.TextField(nextValue, textFieldOptions);

            if (effectiveOptions.AllowBrowse)
            {
                var browseEnabled = GUI.enabled;
                GUI.enabled = browseEnabled && pathInteractionService != null && !isWaiting;
                if (GUILayout.Button(
                    isWaiting
                        ? "Waiting..."
                        : (string.IsNullOrEmpty(effectiveOptions.BrowseButtonText) ? "Browse..." : effectiveOptions.BrowseButtonText),
                    GUILayout.Width(74f),
                    GUILayout.Height(24f)))
                {
                    var requestId = string.Empty;
                    var request = CreateRequest(effectiveOptions.BrowseRequest, nextValue);
                    MMLog.WriteDebug(LogPrefix + "Browse requested. FieldId=" + normalizedFieldId +
                        ", Waiting=" + isWaiting +
                        ", HasService=" + (pathInteractionService != null) +
                        ", SelectionKind=" + request.SelectionKind +
                        ", InitialPath=" + (request.InitialPath ?? string.Empty) + ".");
                    if (pathInteractionService != null &&
                        pathInteractionService.TryBeginSelectPath(request, out requestId) &&
                        !string.IsNullOrEmpty(requestId) &&
                        !string.IsNullOrEmpty(normalizedFieldId))
                    {
                        _pendingSelectionsByField[normalizedFieldId] = requestId;
                        MMLog.WriteDebug(LogPrefix + "Browse request queued. FieldId=" + normalizedFieldId +
                            ", RequestId=" + requestId + ".");
                    }
                    else
                    {
                        MMLog.WriteWarning(LogPrefix + "Browse request was not queued. FieldId=" + normalizedFieldId +
                            ", HasService=" + (pathInteractionService != null) +
                            ", RequestId=" + (requestId ?? string.Empty) + ".");
                    }
                }
                GUI.enabled = browseEnabled;
            }

            if (effectiveOptions.AllowOpen)
            {
                var openEnabled = GUI.enabled;
                GUI.enabled = openEnabled && pathInteractionService != null && HasExistingPath(nextValue);
                if (GUILayout.Button(
                    string.IsNullOrEmpty(effectiveOptions.OpenButtonText) ? "Open" : effectiveOptions.OpenButtonText,
                    GUILayout.Width(58f),
                    GUILayout.Height(24f)))
                {
                    if (pathInteractionService != null)
                    {
                        pathInteractionService.TryOpenPath(nextValue);
                    }
                }
                GUI.enabled = openEnabled;
            }

            if (effectiveOptions.AllowReveal)
            {
                var revealEnabled = GUI.enabled;
                GUI.enabled = revealEnabled && pathInteractionService != null && HasExistingPath(nextValue);
                if (GUILayout.Button(
                    string.IsNullOrEmpty(effectiveOptions.RevealButtonText) ? "Show" : effectiveOptions.RevealButtonText,
                    GUILayout.Width(58f),
                    GUILayout.Height(24f)))
                {
                    if (pathInteractionService != null)
                    {
                        pathInteractionService.TryRevealPath(nextValue);
                    }
                }
                GUI.enabled = revealEnabled;
            }

            if (effectiveOptions.AllowPaste && GUILayout.Button("Paste", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                nextValue = GUIUtility.systemCopyBuffer ?? string.Empty;
            }

            if (effectiveOptions.AllowClear && GUILayout.Button("Clear", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                nextValue = string.Empty;
            }

            GUILayout.EndHorizontal();
            return nextValue;
        }

        private static bool TryApplyCompletedSelection(string fieldId, IPathInteractionService pathInteractionService, ref string value)
        {
            if (string.IsNullOrEmpty(fieldId) || pathInteractionService == null)
            {
                return false;
            }

            string requestId;
            if (!_pendingSelectionsByField.TryGetValue(fieldId, out requestId) || string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            PathSelectionResult result;
            if (!pathInteractionService.TryGetCompletedSelection(requestId, out result))
            {
                return false;
            }

            _pendingSelectionsByField.Remove(fieldId);
            if (result != null && result.Succeeded && !string.IsNullOrEmpty(result.SelectedPath))
            {
                MMLog.WriteDebug(LogPrefix + "Browse request completed. FieldId=" + fieldId +
                    ", RequestId=" + requestId +
                    ", SelectedPath=" + result.SelectedPath + ".");
                value = result.SelectedPath;
            }
            else if (result != null && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                MMLog.WriteWarning(LogPrefix + "Browse request failed. FieldId=" + fieldId +
                    ", RequestId=" + requestId +
                    ", Error=" + result.ErrorMessage + ".");
            }
            else
            {
                MMLog.WriteDebug(LogPrefix + "Browse request ended without selection. FieldId=" + fieldId +
                    ", RequestId=" + requestId + ".");
            }

            return true;
        }

        private static PathSelectionRequest CreateRequest(PathSelectionRequest template, string currentValue)
        {
            var request = new PathSelectionRequest();
            if (template != null)
            {
                request.SelectionKind = template.SelectionKind;
                request.Title = template.Title ?? string.Empty;
                request.InitialPath = template.InitialPath ?? string.Empty;
                request.SuggestedFileName = template.SuggestedFileName ?? string.Empty;
                request.Filter = template.Filter ?? string.Empty;
                request.CheckPathExists = template.CheckPathExists;
                request.RestoreDirectory = template.RestoreDirectory;
            }

            if (!string.IsNullOrEmpty(currentValue))
            {
                request.InitialPath = currentValue;
            }

            return request;
        }

        private static bool HasExistingPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                return System.IO.Directory.Exists(path) || System.IO.File.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
