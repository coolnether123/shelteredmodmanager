using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Editor.Context;

namespace Cortex.Services.Semantics.Context
{
    internal sealed class EditorContextStoreService
    {
        private readonly EditorCommandContextFactory _contextFactory;
        private readonly EditorContextProjectionService _projectionService;

        public EditorContextStoreService(EditorCommandContextFactory contextFactory, EditorContextProjectionService projectionService)
        {
            _contextFactory = contextFactory;
            _projectionService = projectionService;
        }

        public void StoreSnapshot(CortexShellState state, EditorContextSnapshot snapshot, bool setActive)
        {
            if (state == null || state.EditorContext == null || snapshot == null || string.IsNullOrEmpty(snapshot.ContextKey))
            {
                return;
            }

            state.EditorContext.ContextsByKey[snapshot.ContextKey] = snapshot;
            if (!string.IsNullOrEmpty(snapshot.SurfaceId))
            {
                state.EditorContext.SurfaceContextKeys[snapshot.SurfaceId] = snapshot.ContextKey;
            }

            if (setActive)
            {
                state.EditorContext.ActiveSurfaceId = snapshot.SurfaceId ?? string.Empty;
                state.EditorContext.ActiveContextKey = snapshot.ContextKey ?? string.Empty;
            }

            TrimOldContexts(state);
        }

        public EditorContextSnapshot GetContext(CortexShellState state, string contextKey)
        {
            if (state == null || state.EditorContext == null || string.IsNullOrEmpty(contextKey))
            {
                return null;
            }

            EditorContextSnapshot snapshot;
            return state.EditorContext.ContextsByKey.TryGetValue(contextKey, out snapshot) ? snapshot : null;
        }

        public EditorContextSnapshot GetSurfaceContext(CortexShellState state, string surfaceId)
        {
            if (state == null || state.EditorContext == null || string.IsNullOrEmpty(surfaceId))
            {
                return null;
            }

            string contextKey;
            return state.EditorContext.SurfaceContextKeys.TryGetValue(surfaceId, out contextKey)
                ? GetContext(state, contextKey)
                : null;
        }

        public EditorCommandTarget ResolveTarget(CortexShellState state, string contextKey)
        {
            return _projectionService.ProjectTarget(GetContext(state, contextKey));
        }

        public EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey)
        {
            var target = ResolveTarget(state, contextKey);
            return target != null ? _contextFactory.CreateForTarget(state, target) : null;
        }

        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string contextKey, string hoverKey)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot == null || snapshot.Semantic == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(hoverKey) || string.Equals(snapshot.HoverKey ?? string.Empty, hoverKey, StringComparison.Ordinal)
                ? snapshot.Semantic.HoverResponse
                : null;
        }

        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey)
        {
            var snapshot = FindLatestMatchingHoverSnapshot(state, hoverKey, true);
            return snapshot != null && snapshot.Semantic != null ? snapshot.Semantic.HoverResponse : null;
        }

        public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string contextKey, string hoverKey)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot == null || snapshot.Semantic == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(hoverKey) || string.Equals(snapshot.HoverKey ?? string.Empty, hoverKey, StringComparison.Ordinal)
                ? snapshot.Semantic.HoverContent
                : null;
        }

        public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string hoverKey)
        {
            var snapshot = FindLatestMatchingHoverSnapshot(state, hoverKey, false);
            return snapshot != null && snapshot.Semantic != null ? snapshot.Semantic.HoverContent : null;
        }

        public EditorContextSnapshot ResolveHoverMutationSnapshot(CortexShellState state, string contextKey, string hoverKey)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot != null || state == null || state.EditorContext == null || string.IsNullOrEmpty(hoverKey))
            {
                return snapshot;
            }

            return FindLatestMatchingHoverSnapshot(state, hoverKey, true);
        }

        private EditorContextSnapshot FindLatestMatchingHoverSnapshot(CortexShellState state, string hoverKey, bool requireResponse)
        {
            if (state == null || state.EditorContext == null || string.IsNullOrEmpty(hoverKey))
            {
                return null;
            }

            EditorContextSnapshot bestSnapshot = null;
            foreach (var pair in state.EditorContext.ContextsByKey)
            {
                var snapshot = pair.Value;
                if (snapshot == null || !string.Equals(snapshot.HoverKey ?? string.Empty, hoverKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (snapshot.Semantic == null)
                {
                    continue;
                }

                if (requireResponse && snapshot.Semantic.HoverResponse == null)
                {
                    continue;
                }

                if (!requireResponse && snapshot.Semantic.HoverContent == null)
                {
                    continue;
                }

                if (bestSnapshot == null || snapshot.CapturedUtc > bestSnapshot.CapturedUtc)
                {
                    bestSnapshot = snapshot;
                }
            }

            return bestSnapshot;
        }

        private static void TrimOldContexts(CortexShellState state)
        {
            if (state == null || state.EditorContext == null || state.EditorContext.ContextsByKey.Count <= 32)
            {
                return;
            }

            string oldestKey = string.Empty;
            var oldestUtc = DateTime.MaxValue;
            foreach (var pair in state.EditorContext.ContextsByKey)
            {
                if (pair.Value == null || string.Equals(pair.Key, state.EditorContext.ActiveContextKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (pair.Value.CapturedUtc < oldestUtc)
                {
                    oldestUtc = pair.Value.CapturedUtc;
                    oldestKey = pair.Key;
                }
            }

            if (!string.IsNullOrEmpty(oldestKey))
            {
                state.EditorContext.ContextsByKey.Remove(oldestKey);
            }
        }
    }
}
