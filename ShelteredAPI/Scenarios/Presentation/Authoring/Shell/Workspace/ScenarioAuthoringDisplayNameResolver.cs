using System;
using System.Globalization;
using System.Text;

using ShelteredAPI.Content;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed class ScenarioAuthoringDisplayName
    {
        public string Text { get; set; }
        public string StorageId { get; set; }
        public string LocalizationKey { get; set; }
        public bool LocalizationResolved { get; set; }
    }

    internal interface IScenarioAuthoringDisplayNameResolver
    {
        ScenarioAuthoringDisplayName Resolve(
            string literalText,
            string localizationKey,
            string storageId,
            string fallbackText);
    }

    /// <summary>
    /// Converts authored storage values into player-facing shell language. The shared
    /// instance is called only while the cached shell presentation is rebuilt on Unity's
    /// main thread; renderers must consume the resulting text instead of resolving again.
    /// </summary>
    internal sealed class ScenarioAuthoringDisplayNameResolver : IScenarioAuthoringDisplayNameResolver
    {
        private readonly bool _allowVanillaLocalization;

        public static readonly ScenarioAuthoringDisplayNameResolver ShellRebuild =
            new ScenarioAuthoringDisplayNameResolver(true);

        public ScenarioAuthoringDisplayNameResolver()
            : this(true)
        {
        }

        internal ScenarioAuthoringDisplayNameResolver(bool allowVanillaLocalization)
        {
            _allowVanillaLocalization = allowVanillaLocalization;
        }

        public ScenarioAuthoringDisplayName Resolve(
            string literalText,
            string localizationKey,
            string storageId,
            string fallbackText)
        {
            string literal = TrimToNull(literalText);
            string key = TrimToNull(localizationKey);
            string id = TrimToNull(storageId);
            string fallback = TrimToNull(fallbackText);
            if (key == null && IsLikelyLocalizationKey(literal))
                key = literal;

            ScenarioAuthoringDisplayName result = new ScenarioAuthoringDisplayName
            {
                StorageId = id,
                LocalizationKey = key,
                LocalizationResolved = false
            };

            if (literal != null && !IsLikelyLocalizationKey(literal)
                && !string.Equals(literal, id, StringComparison.OrdinalIgnoreCase))
            {
                result.Text = literal;
                return result;
            }

            string localized;
            if (TryModLocalization(key, out localized) || TryVanillaLocalization(key, out localized))
            {
                result.Text = localized;
                result.LocalizationResolved = true;
                return result;
            }

            if (fallback != null)
            {
                result.Text = fallback;
                return result;
            }

            string humanized;
            result.Text = TryHumanizeStorageId(id, out humanized) ? humanized : "Untitled";
            return result;
        }

        private static bool TryModLocalization(string key, out string localized)
        {
            localized = null;
            string value;
            if (key == null || !ModLocalization.TryGet(key, out value))
                return false;
            localized = UsableLocalization(value, key);
            return localized != null;
        }

        private bool TryVanillaLocalization(string key, out string localized)
        {
            localized = null;
            if (!_allowVanillaLocalization || key == null)
                return false;
            try
            {
                localized = UsableLocalization(Localization.Get(key), key);
                return localized != null;
            }
            catch
            {
                return false;
            }
        }

        private static string UsableLocalization(string value, string key)
        {
            string text = TrimToNull(value);
            return text == null || string.Equals(text, key, StringComparison.OrdinalIgnoreCase)
                ? null
                : text;
        }

        internal static bool IsLikelyLocalizationKey(string value)
        {
            string text = TrimToNull(value);
            if (text == null || text.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
                return false;
            bool separator = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '.' || c == '_' || c == ':' || c == '/')
                {
                    separator = true;
                    continue;
                }
                if (!char.IsLetterOrDigit(c) && c != '-')
                    return false;
            }
            return separator;
        }

        private static bool TryHumanizeStorageId(string storageId, out string humanized)
        {
            humanized = null;
            if (storageId == null || storageId.Length > 64 || storageId.IndexOf('/') >= 0
                || storageId.IndexOf('\\') >= 0 || storageId.IndexOf(':') >= 0)
                return false;

            StringBuilder words = new StringBuilder(storageId.Length + 8);
            bool hasLetter = false;
            bool previousSpace = true;
            for (int i = 0; i < storageId.Length; i++)
            {
                char c = storageId[i];
                if (c == '_' || c == '-' || c == '.')
                {
                    if (!previousSpace)
                        words.Append(' ');
                    previousSpace = true;
                    continue;
                }
                if (!char.IsLetterOrDigit(c))
                    return false;
                if (char.IsLetter(c))
                    hasLetter = true;
                words.Append(c);
                previousSpace = false;
            }
            if (!hasLetter)
                return false;

            string value = words.ToString().Trim();
            if (value.Length == 0)
                return false;
            humanized = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
            return true;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
