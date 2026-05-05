using System;
using System.Globalization;
using System.Reflection;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal static class ScenarioObjectStatePropertyService
    {
        private const string Prefix = "state.";
        private static readonly string[] IntegrityFields =
        {
            "m_Integrity",
            "m_AutoDegrade",
            "m_DegradeInterval",
            "m_BeingFixed",
            "m_DuctTaped",
            "m_fireTimer",
            "m_catastrophicFail"
        };
        private static readonly string[] WaterCondenserFields = { "m_WaterGenerated" };
        private static readonly string[] FoodBowlFields = { "m_filled", "m_medicine" };
        private static readonly string[] RatTrapFields = { "m_hasBeenUsed", "isRatTrapped", "m_uses" };
        private static readonly string[] SnareTrapFields = { "m_hasBeenUsed", "isDeerTrapped", "m_uses" };
        private static readonly string[] PlanterFields =
        {
            "readyToWater",
            "wiltTimer",
            "growTimer",
            "plantStage",
            "timesWatered",
            "m_seedPlanted",
            "wilted",
            "m_fertilizerUsed",
            "currentFood",
            "currentSeeds",
            "currentPlant"
        };

        public static void Capture(Obj_Base obj, ObjectPlacement placement)
        {
            if (obj == null || placement == null)
                return;

            Set(placement, "recipeId", obj.recipeId);
            Set(placement, "name", obj.GetName());
            Set(placement, "enabled", obj.IsEnabled());
            Set(placement, "selectable", obj.selectable);
            Set(placement, "disablable", obj.disablable);
            Set(placement, "beingUpgraded", obj.IsBeingUpgraded);

            CaptureFields(obj, placement, typeof(Obj_Integrity), IntegrityFields);
            CaptureFields(obj, placement, typeof(Obj_WaterCondenser), WaterCondenserFields);
            CaptureFields(obj, placement, typeof(Obj_FoodBowl), FoodBowlFields);
            CaptureFields(obj, placement, typeof(Obj_RatTrap), RatTrapFields);
            CaptureFields(obj, placement, typeof(Obj_SnareTrap), SnareTrapFields);
            CaptureFields(obj, placement, typeof(Obj_Planter), PlanterFields);
        }

        public static void Apply(Obj_Base obj, ObjectPlacement placement)
        {
            if (obj == null || placement == null)
                return;

            string recipeId = Get(placement, "recipeId");
            if (!string.IsNullOrEmpty(recipeId))
                obj.SetRecipeId(recipeId);

            string name = Get(placement, "name");
            if (!string.IsNullOrEmpty(name))
                obj.SetName(name);

            obj.selectable = ScenarioPropertyBag.GetBool(placement.CustomProperties, Key("selectable"), obj.selectable);
            obj.disablable = ScenarioPropertyBag.GetBool(placement.CustomProperties, Key("disablable"), obj.disablable);
            obj.SetIsBeingUpgraded(ScenarioPropertyBag.GetBool(placement.CustomProperties, Key("beingUpgraded"), obj.IsBeingUpgraded));

            ApplyFields(obj, placement, typeof(Obj_Integrity), IntegrityFields);

            ApplyFields(obj, placement, typeof(Obj_WaterCondenser), WaterCondenserFields);
            InvokeIfPresent(obj, typeof(Obj_WaterCondenser), "UpdateWaterLevelSprite", null);

            ApplyFields(obj, placement, typeof(Obj_FoodBowl), FoodBowlFields);
            RefreshFoodBowl(obj);

            ApplyFields(obj, placement, typeof(Obj_RatTrap), RatTrapFields);
            RefreshTrapSprite(obj, typeof(Obj_RatTrap), "isRatTrapped", "ratTrappedSprite", "unsprungTrapSprite");

            ApplyFields(obj, placement, typeof(Obj_SnareTrap), SnareTrapFields);
            RefreshTrapSprite(obj, typeof(Obj_SnareTrap), "isDeerTrapped", "deerTrappedSprite", "unsprungTrapSprite");

            ApplyFields(obj, placement, typeof(Obj_Planter), PlanterFields);
            int plantStage = ScenarioPropertyBag.GetInt(placement.CustomProperties, Key("plantStage"), 1);
            InvokeIfPresent(obj, typeof(Obj_Planter), "UpdatePlantSprite", new object[] { Math.Max(plantStage - 1, 0) });

            bool enabled = ScenarioPropertyBag.GetBool(placement.CustomProperties, Key("enabled"), true);
            if (enabled)
                obj.EnableObject();
            else
                obj.DisableObject();
        }

        private static void CaptureFields(Obj_Base obj, ObjectPlacement placement, Type componentType, string[] fieldNames)
        {
            if (obj == null || placement == null || componentType == null || !componentType.IsInstanceOfType(obj))
                return;

            for (int i = 0; fieldNames != null && i < fieldNames.Length; i++)
            {
                FieldInfo field = FindField(componentType, fieldNames[i]);
                if (field == null)
                    continue;

                object value = field.GetValue(obj);
                string serialized;
                if (TrySerialize(value, out serialized))
                    Set(placement, fieldNames[i], serialized);
            }
        }

        private static void ApplyFields(Obj_Base obj, ObjectPlacement placement, Type componentType, string[] fieldNames)
        {
            if (obj == null || placement == null || componentType == null || !componentType.IsInstanceOfType(obj))
                return;

            for (int i = 0; fieldNames != null && i < fieldNames.Length; i++)
            {
                FieldInfo field = FindField(componentType, fieldNames[i]);
                if (field == null)
                    continue;

                string value = Get(placement, fieldNames[i]);
                object parsed;
                if (TryParse(value, field.FieldType, out parsed))
                    field.SetValue(obj, parsed);
            }
        }

        private static void RefreshFoodBowl(Obj_Base obj)
        {
            if (obj == null || !typeof(Obj_FoodBowl).IsInstanceOfType(obj))
                return;

            bool filled = GetBoolField(obj, typeof(Obj_FoodBowl), "m_filled");
            FieldInfo rendererField = FindField(typeof(Obj_FoodBowl), "m_renderer");
            FieldInfo spriteField = FindField(typeof(Obj_FoodBowl), filled ? "m_fullSprite" : "m_emptySprite");
            SpriteRenderer renderer = rendererField != null ? rendererField.GetValue(obj) as SpriteRenderer : null;
            Sprite sprite = spriteField != null ? spriteField.GetValue(obj) as Sprite : null;
            if (renderer != null && sprite != null)
                renderer.sprite = sprite;
        }

        private static void RefreshTrapSprite(Obj_Base obj, Type type, string trappedField, string trappedSpriteField, string emptySpriteField)
        {
            if (obj == null || type == null || !type.IsInstanceOfType(obj))
                return;

            bool trapped = GetBoolField(obj, type, trappedField);
            FieldInfo rendererField = FindField(type, "m_SpriteRenderer");
            FieldInfo spriteField = FindField(type, trapped ? trappedSpriteField : emptySpriteField);
            SpriteRenderer renderer = rendererField != null ? rendererField.GetValue(obj) as SpriteRenderer : null;
            Sprite sprite = spriteField != null ? spriteField.GetValue(obj) as Sprite : null;
            if (renderer != null && sprite != null)
                renderer.sprite = sprite;
        }

        private static bool GetBoolField(object target, Type type, string fieldName)
        {
            FieldInfo field = FindField(type, fieldName);
            return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(target);
        }

        private static void InvokeIfPresent(object target, Type type, string methodName, object[] args)
        {
            if (target == null || type == null || !type.IsInstanceOfType(target))
                return;

            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
                method.Invoke(target, args);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field;
            }

            return null;
        }

        private static void Set(ObjectPlacement placement, string key, bool value)
        {
            Set(placement, key, value.ToString().ToLowerInvariant());
        }

        private static void Set(ObjectPlacement placement, string key, string value)
        {
            if (placement == null || string.IsNullOrEmpty(key) || value == null)
                return;

            ScenarioPropertyBag.Set(placement.CustomProperties, Key(key), value);
        }

        private static string Get(ObjectPlacement placement, string key)
        {
            return placement != null ? ScenarioPropertyBag.GetString(placement.CustomProperties, Key(key)) : null;
        }

        private static string Key(string key)
        {
            return Prefix + key;
        }

        private static bool TrySerialize(object value, out string serialized)
        {
            serialized = null;
            if (value == null)
                return false;

            if (value is bool)
            {
                serialized = ((bool)value).ToString().ToLowerInvariant();
                return true;
            }

            if (value is int)
            {
                serialized = ((int)value).ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (value is float)
            {
                serialized = ((float)value).ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (value is string)
            {
                serialized = (string)value;
                return true;
            }

            return false;
        }

        private static bool TryParse(string value, Type type, out object parsed)
        {
            parsed = null;
            if (string.IsNullOrEmpty(value) || type == null)
                return false;

            if (type == typeof(bool))
            {
                bool boolValue;
                if (!bool.TryParse(value, out boolValue))
                    return false;
                parsed = boolValue;
                return true;
            }

            if (type == typeof(int))
            {
                int intValue;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                    return false;
                parsed = intValue;
                return true;
            }

            if (type == typeof(float))
            {
                float floatValue;
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
                    return false;
                parsed = floatValue;
                return true;
            }

            if (type == typeof(string))
            {
                parsed = value;
                return true;
            }

            return false;
        }
    }
}
