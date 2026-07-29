using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ParalivesAPI.Core
{
    public interface IParalivesOccupationPanelProvider
    {
        bool CanProvide(ulong characterGuid, int occupationIndex);

        ParalivesOccupationPanel BuildPanel(ulong characterGuid, int occupationIndex);
    }

    internal sealed class ParalivesOccupationPanelProviderRegistry
    {
        private readonly object _sync = new object();
        private readonly List<IParalivesOccupationPanelProvider> _providers =
            new List<IParalivesOccupationPanelProvider>();

        public int RegisteredProviderCount
        {
            get
            {
                lock (_sync)
                    return _providers.Count;
            }
        }

        public IDisposable Register(IParalivesOccupationPanelProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");

            lock (_sync)
            {
                if (!_providers.Contains(provider))
                    _providers.Add(provider);
            }

            return new Registration(this, provider);
        }

        public IDisposable Register(
            Func<ulong, int, bool> canProvide,
            Func<ulong, int, ParalivesOccupationPanel> buildPanel)
        {
            if (canProvide == null)
                throw new ArgumentNullException("canProvide");
            if (buildPanel == null)
                throw new ArgumentNullException("buildPanel");

            return Register(new DelegateOccupationPanelProvider(canProvide, buildPanel));
        }

        public bool Unregister(IParalivesOccupationPanelProvider provider)
        {
            if (provider == null)
                return false;

            lock (_sync)
                return _providers.Remove(provider);
        }

        internal bool TryApplyTo(global::UIOccupations window)
        {
            if (window == null || window.UIListPerformanceDataItems == null)
                return false;

            ulong characterGuid;
            int occupationIndex;
            if (!TryGetWindowContext(window, out characterGuid, out occupationIndex))
                return false;

            IParalivesOccupationPanelProvider[] providers;
            lock (_sync)
                providers = _providers.ToArray();

            if (providers.Length == 0)
                return false;

            int rowIndex = window.UIListPerformanceDataItems.CurrentItems == null
                ? 0
                : window.UIListPerformanceDataItems.CurrentItems.Count;
            bool applied = false;
            for (int i = 0; i < providers.Length; i++)
            {
                IParalivesOccupationPanelProvider provider = providers[i];
                if (!CanProviderContribute(provider, characterGuid, occupationIndex))
                    continue;

                ParalivesOccupationPanel panel = BuildPanel(provider, characterGuid, occupationIndex);
                if (panel == null)
                    continue;

                applied |= ApplyPanel(window, panel, ref rowIndex);
            }

            return applied;
        }

        private static bool TryGetWindowContext(
            global::UIOccupations window,
            out ulong characterGuid,
            out int occupationIndex)
        {
            characterGuid = 0UL;
            occupationIndex = -1;

            try
            {
                int playerIndex = window.PlayerOwnerIndex;
                occupationIndex = window.SelectedOccupationIndex;
                if (playerIndex < 0 || occupationIndex < 0)
                    return false;
                if (global::PlayerManager.Instance == null
                    || global::PlayerManager.Instance.Players == null
                    || playerIndex >= global::PlayerManager.Instance.Players.Count)
                {
                    return false;
                }

                characterGuid = global::PlayerManager.Instance.Players[playerIndex].GetSelectedCharacterGUID();
                return characterGuid != 0UL;
            }
            catch
            {
                characterGuid = 0UL;
                occupationIndex = -1;
                return false;
            }
        }

        private static bool CanProviderContribute(
            IParalivesOccupationPanelProvider provider,
            ulong characterGuid,
            int occupationIndex)
        {
            if (provider == null)
                return false;

            try
            {
                return provider.CanProvide(characterGuid, occupationIndex);
            }
            catch (Exception ex)
            {
                WarnProvider(provider, "CanProvide", ex);
                return false;
            }
        }

        private static ParalivesOccupationPanel BuildPanel(
            IParalivesOccupationPanelProvider provider,
            ulong characterGuid,
            int occupationIndex)
        {
            try
            {
                return provider.BuildPanel(characterGuid, occupationIndex);
            }
            catch (Exception ex)
            {
                WarnProvider(provider, "BuildPanel", ex);
                return null;
            }
        }

        private static bool ApplyPanel(
            global::UIOccupations window,
            ParalivesOccupationPanel panel,
            ref int rowIndex)
        {
            bool applied = false;
            if (panel.PerformanceLabel != null
                && panel.PerformanceLabel.HasValue
                && window.LabelJobPerformance != null)
            {
                window.LabelJobPerformance.ForceTextOverride(ResolveText(panel.PerformanceLabel));
                applied = true;
            }

            if (panel.ReplacePerformanceRows)
            {
                window.UIListPerformanceDataItems.DeactivateAndPoolAllItems();
                rowIndex = 0;
                applied = true;
            }

            if (panel.Rows == null)
                return applied;

            for (int i = 0; i < panel.Rows.Count; i++)
                applied |= TryAppendRow(window, panel.Rows[i], ref rowIndex);

            return applied;
        }

        private static bool TryAppendRow(
            global::UIOccupations window,
            ParalivesOccupationPanelRow row,
            ref int rowIndex)
        {
            if (row == null || row.Label == null || !row.Label.HasValue)
                return false;

            global::UIOccupationsJobPerformanceDataItem item =
                window.UIListPerformanceDataItems.GetItemAtIndex<global::UIOccupationsJobPerformanceDataItem>(rowIndex);
            if (item == null)
                return false;

            rowIndex++;
            string label = ResolveText(row.Label);
            string tooltip = ResolveText(row.Tooltip);
            item.Refresh(label, tooltip, row.IsPositive);
            if (item.Label != null)
                item.Label.ForceTextOverride(label);
            if (item.Tooltip != null)
            {
                item.Tooltip.TranslationKeyOfTextToShow = string.Empty;
                item.Tooltip.TextToShow = tooltip;
            }

            return true;
        }

        private static string ResolveText(ParalivesUiText text)
        {
            if (text == null || !text.HasValue)
                return string.Empty;
            if (!string.IsNullOrEmpty(text.Text))
                return text.Text;
            if (string.IsNullOrEmpty(text.TranslationKey))
                return string.Empty;

            try
            {
                return ParalivesRuntimeInfo.Current.Localizations.Translate(
                    text.TranslationKey,
                    text.Parameters) ?? text.TranslationKey;
            }
            catch
            {
                return text.TranslationKey;
            }
        }

        private static void WarnProvider(
            IParalivesOccupationPanelProvider provider,
            string method,
            Exception exception)
        {
            string providerName = provider == null || provider.GetType() == null
                ? "unknown"
                : provider.GetType().FullName;
            MMLog.WarnOnce(
                "ParalivesOccupationPanelProvider." + method + "." + providerName,
                "Paralives occupation panel provider " + providerName + "." + method + " failed: " + exception.Message);
        }

        private sealed class Registration : IDisposable
        {
            private ParalivesOccupationPanelProviderRegistry _registry;
            private IParalivesOccupationPanelProvider _provider;

            public Registration(
                ParalivesOccupationPanelProviderRegistry registry,
                IParalivesOccupationPanelProvider provider)
            {
                _registry = registry;
                _provider = provider;
            }

            public void Dispose()
            {
                ParalivesOccupationPanelProviderRegistry registry = _registry;
                IParalivesOccupationPanelProvider provider = _provider;
                _registry = null;
                _provider = null;

                if (registry != null)
                    registry.Unregister(provider);
            }
        }

        private sealed class DelegateOccupationPanelProvider : IParalivesOccupationPanelProvider
        {
            private readonly Func<ulong, int, bool> _canProvide;
            private readonly Func<ulong, int, ParalivesOccupationPanel> _buildPanel;

            public DelegateOccupationPanelProvider(
                Func<ulong, int, bool> canProvide,
                Func<ulong, int, ParalivesOccupationPanel> buildPanel)
            {
                _canProvide = canProvide;
                _buildPanel = buildPanel;
            }

            public bool CanProvide(ulong characterGuid, int occupationIndex)
            {
                return _canProvide(characterGuid, occupationIndex);
            }

            public ParalivesOccupationPanel BuildPanel(ulong characterGuid, int occupationIndex)
            {
                return _buildPanel(characterGuid, occupationIndex);
            }
        }
    }
}
