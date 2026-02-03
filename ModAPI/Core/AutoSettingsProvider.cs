using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using ModAPI.Spine;

namespace ModAPI.Core
{
    public class AutoSettingsProvider : ISettingsProvider
    {
        private readonly Type _type;
        private readonly object _settings;
        private readonly IPluginContext _ctx;
        private readonly string _path;

        public AutoSettingsProvider(Type type, object settings, IPluginContext ctx)
        {
            _type = type;
            _settings = settings;
            _ctx = ctx;
            _path = Path.Combine(ctx.Mod.RootPath, "config.json");
        }

        public void LoadInto(object target)
        {
            if (File.Exists(_path))
            {
                try
                {
                    string json = File.ReadAllText(_path);
                    JsonUtility.FromJsonOverwrite(json, target);
                }
                catch (Exception ex)
                {
                    _ctx.Log.Error("Failed to load settings from " + _path + ": " + ex.Message);
                }
            }
        }

        public void Save()
        {
            // 1. Validation (Main Thread)
            _ctx.RunNextFrame(() =>
            {
                (_settings as IModSettingsValidator)?.Validate();

                // 2. Serialization (Main Thread requirement for JsonUtility)
                string json = JsonUtility.ToJson(_settings, true);

                // 3. IO (Background Thread)
                ModThreads.RunAsync(() =>
                {
                    try
                    {
                        File.WriteAllText(_path, json);
                    }
                    catch (Exception ex)
                    {
                         // Basic error reporting, avoiding Unity API on background thread if possible
                         // But we can't easily log back to main thread without dispatching
                    }
                });
            });
        }

        // ISettingsProvider Implementation

        public IEnumerable<SettingDefinition> GetSettings()
        {
            // Delegate scanning to Spine's helper
            return SpineSettingsHelper.Scan(_settings);
        }

        public object GetSettingsObject()
        {
            return _settings;
        }

        public void OnSettingsLoaded()
        {
            // Trigger validation
            (_settings as IModSettingsValidator)?.Validate();
        }

        public void ResetToDefaults()
        {
            try
            {
                object defaults = Activator.CreateInstance(_type);
                foreach (var field in _type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (field.IsLiteral || field.IsInitOnly) continue;
                    field.SetValue(_settings, field.GetValue(defaults));
                }
                Save();
            }
            catch (Exception ex)
            {
                _ctx.Log.Error("Failed to reset settings to defaults: " + ex.Message);
            }
        }
    }
}
