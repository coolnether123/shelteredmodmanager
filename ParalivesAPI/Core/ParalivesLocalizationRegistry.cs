using System;
using System.Collections.Generic;
using ModAPI.Core;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesLocalizationRegistry
    {
        private readonly object _sync = new object();
        private readonly List<ParalivesLocalizationEntry> _entries = new List<ParalivesLocalizationEntry>();

        public int RegisteredTranslationCount
        {
            get { lock (_sync) return _entries.Count; }
        }

        public bool Has(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                Translations translations = GetTranslationsOrNull();
                return translations != null
                    && translations.Dictionary != null
                    && translations.Dictionary.ContainsKey(key);
            }
            catch
            {
                return false;
            }
        }

        public string Translate(string key, params string[] parameters)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                TranslationItem item;
                return TryGetItem(key, out item)
                    ? FormatValue(item.Value, parameters)
                    : key;
            }
            catch
            {
                return key;
            }
        }

        public string Translate(ulong guid, params string[] parameters)
        {
            if (guid == 0UL)
                return string.Empty;

            try
            {
                Translations translations = GetTranslationsOrNull();
                if (translations == null || translations.Items == null)
                    return string.Empty;

                for (int i = 0; i < translations.Items.Length; i++)
                {
                    TranslationItem item = translations.Items[i];
                    if (item != null && item.GUID == guid)
                        return FormatValue(item.Value, parameters);
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public TranslationItem GetItemOrNull(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            try
            {
                TranslationItem item;
                return TryGetItem(key, out item) ? item : null;
            }
            catch
            {
                return null;
            }
        }

        public void Register(string key, string value)
        {
            Register(key, value, null, false);
        }

        public void Register(string key, string value, string infoForTranslators, bool doNotTranslate)
        {
            Register(new ParalivesLocalizationEntry(key, value, infoForTranslators, doNotTranslate, 0UL));
        }

        public void Register(ParalivesLocalizationEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException("entry");
            if (string.IsNullOrEmpty(entry.Key))
                throw new ArgumentException("Localization entries must have a non-empty key.", "entry");

            lock (_sync)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (string.Equals(_entries[i].Key, entry.Key, StringComparison.Ordinal))
                    {
                        _entries[i] = entry;
                        return;
                    }
                }

                _entries.Add(entry);
            }
        }

        public bool ApplyWhenReady()
        {
            try
            {
                return ApplyWhenReadyCore();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ParalivesLocalizationRegistry.ApplyWhenReady", "Failed to apply Paralives localization registrations: " + ex.Message);
                return false;
            }
        }

        private bool ApplyWhenReadyCore()
        {
            if (Settings.Instance == null)
                return false;

            Translations translations = Settings.Get<Translations>();
            if (translations == null)
                return false;

            ParalivesLocalizationEntry[] entries;
            lock (_sync)
                entries = _entries.ToArray();

            bool changed = false;
            for (int i = 0; i < entries.Length; i++)
                changed |= EnsureTranslation(translations, entries[i]);

            if (changed)
                translations.RebuildDictionnary();

            return changed;
        }

        private static bool EnsureTranslation(Translations translations, ParalivesLocalizationEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Key))
                return false;

            TranslationItem existing = FindExisting(translations, entry);
            if (existing != null)
                return UpdateExisting(existing, entry);

            TranslationItem item = new TranslationItem
            {
                GUID = entry.Guid != 0UL ? entry.Guid : ParalivesGuid.FromStableName("ParalivesAPI.Localization", entry.Key),
                Key = entry.Key,
                Value = entry.Value ?? string.Empty,
                OriginalValue = entry.Value ?? string.Empty,
                InfoForTranslators = entry.InfoForTranslators ?? string.Empty,
                DoNotTranslate = entry.DoNotTranslate,
                LocalizationState = LocalizationState.Localized
            };

            translations.Items = Append(translations.Items, item);
            return true;
        }

        private static TranslationItem FindExisting(Translations translations, ParalivesLocalizationEntry entry)
        {
            if (translations.Items == null)
                return null;

            for (int i = 0; i < translations.Items.Length; i++)
            {
                TranslationItem item = translations.Items[i];
                if (item == null)
                    continue;

                if (string.Equals(item.Key, entry.Key, StringComparison.Ordinal))
                    return item;
                if (entry.Guid != 0UL && item.GUID == entry.Guid)
                    return item;
            }

            return null;
        }

        private static bool UpdateExisting(TranslationItem item, ParalivesLocalizationEntry entry)
        {
            bool changed = false;
            string value = entry.Value ?? string.Empty;
            string info = entry.InfoForTranslators ?? string.Empty;

            if (item.Value != value)
            {
                item.Value = value;
                changed = true;
            }

            if (item.OriginalValue == null || item.OriginalValue.Length == 0)
            {
                item.OriginalValue = value;
                changed = true;
            }

            if (item.InfoForTranslators != info)
            {
                item.InfoForTranslators = info;
                changed = true;
            }

            if (item.DoNotTranslate != entry.DoNotTranslate)
            {
                item.DoNotTranslate = entry.DoNotTranslate;
                changed = true;
            }

            if (item.LocalizationState == LocalizationState.None || item.LocalizationState == LocalizationState.New)
            {
                item.LocalizationState = LocalizationState.Localized;
                changed = true;
            }

            return changed;
        }

        private static T[] Append<T>(T[] source, T item)
        {
            int length = source != null ? source.Length : 0;
            T[] result = new T[length + 1];
            if (length > 0)
                Array.Copy(source, result, length);

            result[length] = item;
            return result;
        }

        private static Translations GetTranslationsOrNull()
        {
            if (Settings.Instance == null || !Settings.Exists(typeof(Translations)))
                return null;

            return Settings.Get<Translations>();
        }

        private static bool TryGetItem(string key, out TranslationItem item)
        {
            item = null;
            Translations translations = GetTranslationsOrNull();
            if (translations == null || translations.Dictionary == null)
                return false;

            return translations.Dictionary.TryGetValue(key, out item) && item != null;
        }

        private static string FormatValue(string value, string[] parameters)
        {
            string text = value ?? string.Empty;
            if (parameters == null)
                return text;

            for (int i = 0; i < parameters.Length; i++)
                text = text.Replace("{" + i + "}", parameters[i] ?? string.Empty);

            return text;
        }
    }
}
