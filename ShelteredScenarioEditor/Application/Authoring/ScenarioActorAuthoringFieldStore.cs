using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Actors;
using ModAPI.Core;
using ModAPI.Util;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal static class ScenarioActorAuthoringFieldStore
    {

        public static bool TryGetRegistry(out IActorAuthoringCapabilityRegistry registry)
        {
            return GameRuntimeApis.TryGetActorAuthoringCapabilities(out registry);
        }

        public static IList<ActorAuthoringFieldDefinition> GetApplicableFields(FamilyMemberConfig member)
        {
            IActorAuthoringCapabilityRegistry registry;
            if (!TryGetRegistry(out registry) || registry == null)
                return new List<ActorAuthoringFieldDefinition>();

            return registry.GetFields(ResolveActorKind(member));
        }

        public static bool IsProviderModLoaded(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return false;
            if (ModRegistry.GetMod(modId) != null)
                return true;

            IActorAuthoringCapabilityRegistry registry;
            if (TryGetRegistry(out registry))
            {
                IList<IActorAuthoringCapabilityProvider> providers = registry.GetProviders();
                for (int i = 0; providers != null && i < providers.Count; i++)
                {
                    IActorAuthoringCapabilityProvider provider = providers[i];
                    if (provider != null && string.Equals(provider.ProviderModId, modId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public static string BuildFieldToken(ActorAuthoringFieldDefinition field)
        {
            return (field != null ? field.ComponentId : string.Empty) + "|" + (field != null ? field.Id : string.Empty);
        }

        public static bool TryFindField(FamilyMemberConfig member, string token, out ActorAuthoringFieldDefinition field)
        {
            field = null;
            if (string.IsNullOrEmpty(token))
                return false;

            IList<ActorAuthoringFieldDefinition> fields = GetApplicableFields(member);
            for (int i = 0; fields != null && i < fields.Count; i++)
            {
                ActorAuthoringFieldDefinition candidate = fields[i];
                if (string.Equals(BuildFieldToken(candidate), token, StringComparison.OrdinalIgnoreCase))
                {
                    field = candidate;
                    return true;
                }
            }

            return false;
        }

        public static string GetValue(FamilyMemberConfig member, ActorAuthoringFieldDefinition field)
        {
            ScenarioActorComponentDefinition component = FindComponent(member, field);
            string value;
            if (component != null && TryReadField(component.PayloadJson, field, out value))
                return value;

            return field != null ? field.DefaultValue : null;
        }

        public static bool SetValue(FamilyMemberConfig member, ActorAuthoringFieldDefinition field, string value)
        {
            if (member == null || field == null || string.IsNullOrEmpty(field.ComponentId) || string.IsNullOrEmpty(field.Id))
                return false;
            if (member.ActorComponents == null)
                return false;

            ScenarioActorComponentDefinition component = FindComponent(member, field);
            if (component == null)
            {
                component = new ScenarioActorComponentDefinition();
                component.ComponentId = field.ComponentId;
                component.OwnerModId = field.RequiredModId;
                component.Version = field.ComponentVersion < 1 ? 1 : field.ComponentVersion;
                component.PayloadJson = "{}";
                member.ActorComponents.Add(component);
            }

            component.OwnerModId = string.IsNullOrEmpty(component.OwnerModId) ? field.RequiredModId : component.OwnerModId;
            component.Version = component.Version < 1 ? (field.ComponentVersion < 1 ? 1 : field.ComponentVersion) : component.Version;
            component.PayloadJson = WriteField(component.PayloadJson, field, NormalizeValue(field, value));
            return true;
        }

        public static ScenarioActorComponentDefinition FindComponent(FamilyMemberConfig member, ActorAuthoringFieldDefinition field)
        {
            if (member == null || member.ActorComponents == null || field == null || string.IsNullOrEmpty(field.ComponentId))
                return null;

            for (int i = 0; i < member.ActorComponents.Count; i++)
            {
                ScenarioActorComponentDefinition component = member.ActorComponents[i];
                if (component != null && string.Equals(component.ComponentId, field.ComponentId, StringComparison.OrdinalIgnoreCase))
                    return component;
            }

            return null;
        }

        public static string NormalizeValue(ActorAuthoringFieldDefinition field, string value)
        {
            string raw = value ?? string.Empty;
            if (field == null)
                return raw;

            if (field.ValueType == ActorAuthoringFieldValueType.Bool)
                return IsTruthy(raw) ? "true" : "false";
            if (field.ValueType == ActorAuthoringFieldValueType.Int)
            {
                int parsed;
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    int.TryParse(field.DefaultValue ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
                if (field.MinInt.HasValue && parsed < field.MinInt.Value) parsed = field.MinInt.Value;
                if (field.MaxInt.HasValue && parsed > field.MaxInt.Value) parsed = field.MaxInt.Value;
                return parsed.ToString(CultureInfo.InvariantCulture);
            }
            if (field.ValueType == ActorAuthoringFieldValueType.Float)
            {
                float parsed;
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    float.TryParse(field.DefaultValue ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
                if (field.MinFloat.HasValue && parsed < field.MinFloat.Value) parsed = field.MinFloat.Value;
                if (field.MaxFloat.HasValue && parsed > field.MaxFloat.Value) parsed = field.MaxFloat.Value;
                return parsed.ToString("R", CultureInfo.InvariantCulture);
            }
            if (field.ValueType == ActorAuthoringFieldValueType.StringEnum)
                return NormalizeEnumValue(field, raw);
            if (field.ValueType == ActorAuthoringFieldValueType.Color)
                return NormalizeColor(raw);

            return raw;
        }

        public static string NextEnumValue(ActorAuthoringFieldDefinition field, string current)
        {
            if (field == null || field.EnumValues == null || field.EnumValues.Length == 0)
                return current ?? string.Empty;

            int currentIndex = -1;
            for (int i = 0; i < field.EnumValues.Length; i++)
            {
                if (string.Equals(field.EnumValues[i], current, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            return field.EnumValues[(currentIndex + 1) % field.EnumValues.Length] ?? string.Empty;
        }

        public static bool TryReadField(string payloadJson, ActorAuthoringFieldDefinition field, out string value)
        {
            value = null;
            ManualJsonObject root;
            string error;
            if (field == null || string.IsNullOrEmpty(field.Id) || !ManualJson.TryParseObject(payloadJson ?? "{}", out root, out error))
                return false;

            ManualJsonValue jsonValue = root.Get(field.Id);
            if (jsonValue == null || jsonValue.Type == ManualJsonValueType.Null)
                return false;

            switch (jsonValue.Type)
            {
                case ManualJsonValueType.Boolean:
                    value = jsonValue.BooleanValue ? "true" : "false";
                    return true;
                case ManualJsonValueType.Number:
                    value = jsonValue.NumberText;
                    return true;
                case ManualJsonValueType.String:
                    value = jsonValue.StringValue;
                    return true;
                default:
                    return false;
            }
        }

        private static string WriteField(string payloadJson, ActorAuthoringFieldDefinition field, string value)
        {
            Dictionary<string, ManualJsonValue> values = new Dictionary<string, ManualJsonValue>(StringComparer.Ordinal);
            ManualJsonObject root;
            string error;
            if (ManualJson.TryParseObject(payloadJson ?? "{}", out root, out error))
            {
                foreach (KeyValuePair<string, ManualJsonValue> pair in root.Properties)
                    values[pair.Key] = pair.Value;
            }

            values[field.Id] = ToJsonValue(field, value);
            List<string> keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            ManualJsonObject next = new ManualJsonObject();
            for (int i = 0; i < keys.Count; i++)
                next.Set(keys[i], values[keys[i]]);
            return ManualJson.Serialize(next, false);
        }

        private static ManualJsonValue ToJsonValue(ActorAuthoringFieldDefinition field, string value)
        {
            if (field.ValueType == ActorAuthoringFieldValueType.Bool)
                return ManualJsonValue.Boolean(IsTruthy(value));
            if (field.ValueType == ActorAuthoringFieldValueType.Int || field.ValueType == ActorAuthoringFieldValueType.Float)
                return ManualJsonValue.Number(value);
            return ManualJsonValue.String(value ?? string.Empty);
        }

        private static ActorKind ResolveActorKind(FamilyMemberConfig member)
        {
            ActorKind kind;
            if (member != null && member.ActorRef != null && TryParseActorKind(member.ActorRef.Kind, out kind))
                return kind;

            return ActorKind.Synthetic;
        }

        private static bool TryParseActorKind(string value, out ActorKind kind)
        {
            kind = ActorKind.Synthetic;
            if (string.IsNullOrEmpty(value))
                return false;
            try
            {
                kind = (ActorKind)Enum.Parse(typeof(ActorKind), value, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeEnumValue(ActorAuthoringFieldDefinition field, string value)
        {
            for (int i = 0; field.EnumValues != null && i < field.EnumValues.Length; i++)
            {
                if (string.Equals(field.EnumValues[i], value, StringComparison.OrdinalIgnoreCase))
                    return field.EnumValues[i] ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(field.DefaultValue))
                return field.DefaultValue;
            return field.EnumValues != null && field.EnumValues.Length > 0 ? field.EnumValues[0] ?? string.Empty : string.Empty;
        }

        private static string NormalizeColor(string value)
        {
            string raw = (value ?? string.Empty).Trim();
            if (raw.StartsWith("#", StringComparison.Ordinal))
                raw = raw.Substring(1);
            if (raw.Length == 6)
                raw += "FF";
            if (raw.Length != 8)
                return "#FFFFFFFF";
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                    return "#FFFFFFFF";
            }

            return "#" + raw.ToUpperInvariant();
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
