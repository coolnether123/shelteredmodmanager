using System;
using System.Diagnostics;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios.Presentation.Selection
{
    /// <summary>
    /// Coordinates process-lifetime book cache warming from a scene-bound host.
    /// Failures disable only this optimization; normal book construction remains
    /// the authoritative lazy fallback.
    /// </summary>
    internal static class ScenarioBookPrewarmService
    {
        private static bool _completed;
        private static bool _disabled;
        private static bool _started;
        private static bool _warningLogged;
        private static ScenarioBookPrewarmHost _host;

        internal static void TryStart(MainMenu mainMenu)
        {
            if (_completed || _disabled || _started)
                return;

            try
            {
                if (mainMenu == null || !IsMenuScene() || _host != null)
                    return;

                GameObject hostObject = new GameObject("ShelteredAPI_ScenarioBookPrewarm");
                hostObject.hideFlags = HideFlags.HideAndDontSave;
                _host = hostObject.AddComponent<ScenarioBookPrewarmHost>();
                _started = true;
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        internal static void Complete(long textureElapsedMilliseconds, long catalogElapsedMilliseconds, bool textureSkippedForOpen)
        {
            if (_completed || _disabled)
                return;

            _completed = true;
            try
            {
                MMLog.WriteInfo("[ScenarioBookPrewarm] Complete. texturesMs="
                    + textureElapsedMilliseconds
                    + " catalogMs=" + catalogElapsedMilliseconds
                    + " textures=" + (textureSkippedForOpen ? "book-opened" : "warmed") + ".");
            }
            catch
            {
            }
        }

        internal static void Disable(Exception exception)
        {
            _disabled = true;
            if (_warningLogged)
                return;

            _warningLogged = true;
            try
            {
                MMLog.WriteWarning("[ScenarioBookPrewarm] Disabled after prewarm failure: "
                    + (exception != null ? exception.Message : "unknown error") + ".");
            }
            catch
            {
            }
        }

        internal static void NotifyHostDestroyed(ScenarioBookPrewarmHost host)
        {
            if (object.ReferenceEquals(_host, host))
                _host = null;
        }

        internal static bool IsMenuScene()
        {
            try
            {
                return string.Equals(SceneManager.GetActiveScene().name, "MenuScene", StringComparison.Ordinal);
            }
            catch
            {
                try
                {
                    return string.Equals(UnityEngine.Application.loadedLevelName, "MenuScene", StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Scene-bound main-thread pump for the menu-idle prewarm coordinator.
    /// </summary>
    internal sealed class ScenarioBookPrewarmHost : MonoBehaviour
    {
        private readonly Stopwatch _textureTimer = new Stopwatch();
        private readonly Stopwatch _catalogTimer = new Stopwatch();
        private int _framesUntilStart = 1;
        private int _nextTextureIndex;
        private int _catalogStartVersion;
        private bool _started;
        private bool _texturesComplete;
        private bool _catalogComplete;
        private bool _textureSkippedForOpen;

        private void Update()
        {
            try
            {
                if (!ScenarioBookPrewarmService.IsMenuScene())
                {
                    Destroy(gameObject);
                    return;
                }

                if (_framesUntilStart > 0)
                {
                    _framesUntilStart--;
                    return;
                }

                if (!_started)
                    BeginPrewarm();

                UpdateTexturePrewarm();
                UpdateCatalogPrewarm();

                if (_texturesComplete && _catalogComplete)
                {
                    ScenarioBookPrewarmService.Complete(
                        _textureTimer.ElapsedMilliseconds,
                        _catalogTimer.ElapsedMilliseconds,
                        _textureSkippedForOpen);
                    Destroy(gameObject);
                }
            }
            catch (Exception ex)
            {
                ScenarioBookPrewarmService.Disable(ex);
                Destroy(gameObject);
            }
        }

        private void BeginPrewarm()
        {
            _started = true;
            _textureTimer.Start();
            _catalogTimer.Start();
            _catalogStartVersion = ScenarioBookBrowserDataSource.SharedSnapshotVersion;

            IScenarioSelectionCatalogService catalog = ScenarioCompositionRoot.Resolve<IScenarioSelectionCatalogService>();
            ScenarioBookBrowserDataSource.BeginSharedRefreshAsync(catalog);
        }

        private void UpdateTexturePrewarm()
        {
            if (_texturesComplete)
                return;

            if (ScenarioBookBrowserPanel.IsShowing)
            {
                _textureSkippedForOpen = true;
                _texturesComplete = true;
                _textureTimer.Stop();
                return;
            }

            _nextTextureIndex = ProceduralTextureLibrary.PrewarmAll(
                new FieldManualPalette(),
                _nextTextureIndex,
                1);

            if (_nextTextureIndex >= ProceduralTextureLibrary.BookChromeTextureCount)
            {
                _texturesComplete = true;
                _textureTimer.Stop();
            }
        }

        private void UpdateCatalogPrewarm()
        {
            if (_catalogComplete
                || ScenarioBookBrowserDataSource.IsSharedRefreshRunning
                || ScenarioBookBrowserDataSource.SharedSnapshotVersion <= _catalogStartVersion)
            {
                return;
            }

            string error = ScenarioBookBrowserDataSource.SharedSnapshotError;
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException("Catalog prewarm failed: " + error);

            _catalogComplete = true;
            _catalogTimer.Stop();
        }

        private void OnDestroy()
        {
            ScenarioBookPrewarmService.NotifyHostDestroyed(this);
        }
    }
}
