using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Spine
{
    /// <summary>
    /// Maps ModSetting metadata into SettingDefinition instances.
    /// Scanners own member discovery and accessors; this class owns definition shape.
    /// </summary>
    public static class SettingDefinitionFactory
    {
        public static SettingDefinition Create(ModSettingAttribute attr, MemberInfo member, Type settingsType)
        {
            if (attr == null) throw new ArgumentNullException("attr");
            if (member == null) throw new ArgumentNullException("member");

            Type memberType = ResolveMemberType(member);
            var def = new SettingDefinition
            {
                Id = member.Name,
                FieldName = member.Name,
                Label = attr.Label ?? member.Name,
                LabelKey = attr.LabelKey,
                Tooltip = attr.Tooltip,
                TooltipKey = attr.TooltipKey,
                Mode = attr.Mode,
                Scope = attr.Scope,
                CarryOverToNewGamePlus = attr.CarryOverToNewGamePlus,
                NewGamePlusMerge = attr.NewGamePlusMerge,
                AllowExternalWrite = attr.AllowExternalWrite,
                MinValue = attr.MinValue,
                MaxValue = attr.MaxValue,
                StepSize = attr.StepSize,
                SliderStepMode = attr.SliderStepMode,
                ValueFormat = attr.ValueFormat,
                UnitSuffix = attr.UnitSuffix,
                TrueLabel = attr.TrueLabel,
                FalseLabel = attr.FalseLabel,
                ActionLabel = attr.ActionLabel,
                Placeholder = attr.Placeholder,
                ShowValueInput = attr.ShowValueInput,
                ShowStepperButtons = attr.ShowStepperButtons,
                FineStepSize = attr.FineStepSize > 0f ? (float?)attr.FineStepSize : null,
                LargeStepSize = attr.LargeStepSize > 0f ? (float?)attr.LargeStepSize : null,
                Category = attr.Category,
                SortOrder = attr.SortOrder,
                DependsOnId = attr.DependsOnId,
                ControlsChildVisibility = attr.ControlsChildVisibility,
                RequiresRestart = attr.RequiresRestart,
                HeaderColor = string.IsNullOrEmpty(attr.HeaderColor) ? (Color?)null : ParseColor(attr.HeaderColor),
                SyncMode = attr.SyncMode
            };

            ApplyViewVisibilityFromMode(def);
            ApplySettingType(attr, memberType, def);
            AttachCallbacks(attr, settingsType, def);

            return def;
        }

        public static void ApplyPresets(MemberInfo member, SettingDefinition def)
        {
            if (member == null || def == null)
                return;

            object[] presets = Attribute.GetCustomAttributes(member, typeof(ModSettingPresetAttribute));
            for (int i = 0; i < presets.Length; i++)
            {
                ModSettingPresetAttribute preset = presets[i] as ModSettingPresetAttribute;
                if (preset != null)
                    def.Presets[preset.PresetName] = preset.Value;
            }
        }

        public static Func<object, object> CreateGetter(MemberInfo member)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            Expression body;

            FieldInfo field = member as FieldInfo;
            if (field != null)
                body = Expression.Field(Expression.Convert(targetParam, field.DeclaringType), field);
            else
            {
                PropertyInfo prop = member as PropertyInfo;
                if (prop != null && prop.CanRead)
                    body = Expression.Property(Expression.Convert(targetParam, prop.DeclaringType), prop);
                else
                    return null;
            }

            return Expression.Lambda<Func<object, object>>(Expression.Convert(body, typeof(object)), targetParam).Compile();
        }

        public static Action<object, object> CreateSetter(MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null)
            {
                return delegate(object target, object value)
                {
                    try { field.SetValue(target, value); }
                    catch (Exception ex) { MMLog.WriteError("Error setting field " + field.Name + ": " + ex.Message); }
                };
            }

            PropertyInfo prop = member as PropertyInfo;
            if (prop != null && prop.CanWrite)
            {
                MethodInfo setMethod = prop.GetSetMethod(true);
                if (setMethod == null) return null;

                var targetParam = Expression.Parameter(typeof(object), "target");
                var valueParam = Expression.Parameter(typeof(object), "value");
                Expression body = Expression.Call(
                    Expression.Convert(targetParam, prop.DeclaringType),
                    setMethod,
                    Expression.Convert(valueParam, prop.PropertyType));

                return Expression.Lambda<Action<object, object>>(body, targetParam, valueParam).Compile();
            }

            return null;
        }

        private static Type ResolveMemberType(MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null) return field.FieldType;

            PropertyInfo property = member as PropertyInfo;
            if (property != null) return property.PropertyType;

            MethodInfo method = member as MethodInfo;
            if (method != null) return method.ReturnType;

            return typeof(void);
        }

        private static void ApplySettingType(ModSettingAttribute attr, Type memberType, SettingDefinition def)
        {
            if (attr.Type != SettingType.Unknown)
            {
                def.Type = attr.Type;
                if (memberType != null && memberType.IsEnum)
                    def.EnumType = memberType;
                return;
            }

            if (memberType == typeof(bool)) def.Type = SettingType.Bool;
            else if (memberType == typeof(int)) def.Type = SettingType.Int;
            else if (memberType == typeof(float)) def.Type = SettingType.Float;
            else if (memberType == typeof(string)) def.Type = SettingType.String;
            else if (memberType == typeof(KeyCode)) { def.Type = SettingType.Keybind; def.EnumType = memberType; }
            else if (memberType == typeof(Color)) def.Type = SettingType.Color;
            else if (memberType != null && memberType.IsEnum) { def.Type = SettingType.Enum; def.EnumType = memberType; }
        }

        private static void ApplyViewVisibilityFromMode(SettingDefinition def)
        {
            switch (def.Mode)
            {
                case SettingMode.Advanced:
                    def.ShowInSimpleView = false;
                    def.ShowInAdvancedView = true;
                    break;
                case SettingMode.Simple:
                case SettingMode.Both:
                default:
                    def.ShowInSimpleView = true;
                    def.ShowInAdvancedView = true;
                    break;
            }
        }

        private static void AttachCallbacks(ModSettingAttribute attr, Type settingsType, SettingDefinition def)
        {
            if (settingsType == null)
                return;

            AttachOnChanged(attr, settingsType, def);
            AttachVisibleWhen(attr, settingsType, def);
            AttachOptionsSource(attr, settingsType, def);
            AttachValidate(attr, settingsType, def);
        }

        private static void AttachOnChanged(ModSettingAttribute attr, Type settingsType, SettingDefinition def)
        {
            if (string.IsNullOrEmpty(attr.OnChanged))
                return;

            MethodInfo method = settingsType.GetMethod(attr.OnChanged, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                MMLog.WriteError("OnChanged method '" + attr.OnChanged + "' not found on type " + settingsType.Name);
                return;
            }

            def.OnChanged = delegate(object obj)
            {
                try { method.Invoke(obj, null); }
                catch (Exception ex) { MMLog.WriteError("Error invoking OnChanged '" + attr.OnChanged + "': " + ex); }
            };
        }

        private static void AttachVisibleWhen(ModSettingAttribute attr, Type settingsType, SettingDefinition def)
        {
            if (string.IsNullOrEmpty(attr.VisibilityMethod))
                return;

            def.VisibleWhen = delegate(object obj)
            {
                try
                {
                    MethodInfo method = settingsType.GetMethod(attr.VisibilityMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null) return (bool)method.Invoke(obj, null);

                    PropertyInfo property = settingsType.GetProperty(attr.VisibilityMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null) return (bool)property.GetValue(obj, null);

                    MMLog.WriteError("VisibilityMethod '" + attr.VisibilityMethod + "' not found on " + settingsType.Name);
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("Error executing VisibilityMethod '" + attr.VisibilityMethod + "': " + ex);
                }

                return true;
            };
        }

        private static void AttachOptionsSource(ModSettingAttribute attr, Type settingsType, SettingDefinition def)
        {
            if (string.IsNullOrEmpty(attr.OptionsSource))
                return;

            def.GetOptions = delegate(object obj)
            {
                try
                {
                    MethodInfo method = settingsType.GetMethod(attr.OptionsSource, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null) return (IEnumerable<string>)method.Invoke(obj, null);

                    PropertyInfo property = settingsType.GetProperty(attr.OptionsSource, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null) return (IEnumerable<string>)property.GetValue(obj, null);

                    MMLog.WriteError("OptionsSource '" + attr.OptionsSource + "' not found on " + settingsType.Name);
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("Error executing OptionsSource '" + attr.OptionsSource + "': " + ex);
                }

                return new string[0];
            };
        }

        private static void AttachValidate(ModSettingAttribute attr, Type settingsType, SettingDefinition def)
        {
            if (string.IsNullOrEmpty(attr.ValidateMethod))
                return;

            def.Validate = delegate(object newValue, object obj)
            {
                try
                {
                    MethodInfo method = settingsType.GetMethod(attr.ValidateMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null) return (bool)method.Invoke(obj, new[] { newValue });
                    MMLog.WriteError("ValidateMethod '" + attr.ValidateMethod + "' not found on " + settingsType.Name);
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("Error executing ValidateMethod '" + attr.ValidateMethod + "': " + ex);
                }

                return true;
            };
        }

        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length < 6) return Color.white;
                float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float a = hex.Length >= 8 ? int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f : 1f;
                return new Color(r, g, b, a);
            }
            catch { return Color.white; }
        }
    }
}
