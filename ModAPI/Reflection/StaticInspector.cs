using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;

namespace ModAPI.Reflection
{
    public static class StaticInspector
    {
        public static Dictionary<string, object> CaptureStatics(Type type)
        {
            var dict = new Dictionary<string, object>();
            if (type == null) return dict;

            try
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (var field in fields)
                {
                    try
                    {
                        dict[field.Name] = field.GetValue(null);
                    }
                    catch
                    {
                        dict[field.Name] = "<error>";
                    }
                }

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (var prop in props)
                {
                    try
                    {
                        if (prop.CanRead)
                        {
                            dict["[P] " + prop.Name] = prop.GetValue(null, null);
                        }
                    }
                    catch
                    {
                        dict["[P] " + prop.Name] = "<error>";
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[StaticInspector] Failed to capture statics for " + type.Name + ": " + ex.Message);
            }

            return dict;
        }
    }
}
