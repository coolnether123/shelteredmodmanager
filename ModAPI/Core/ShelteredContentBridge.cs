using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ModAPI.Core
{
    /// <summary>
    /// Reflection bridge used by ModAPI-owned runtime helpers to query ShelteredAPI's content module
    /// without introducing a project reference cycle back to ShelteredAPI.
    /// </summary>
    internal static class ShelteredContentBridge
    {
        private const string ContentInjectorTypeName = "ShelteredAPI.Content.ContentInjector, ShelteredAPI";
        private static readonly ItemManager.ItemType[] EmptyRegisteredTypes = new ItemManager.ItemType[0];

        /// <summary>
        /// Gets the currently registered custom item types from ShelteredAPI's content runtime.
        /// </summary>
        internal static IEnumerable<ItemManager.ItemType> GetRegisteredTypes()
        {
            Type injectorType = ResolveContentInjectorType();
            if (injectorType == null)
                return EmptyRegisteredTypes;

            try
            {
                PropertyInfo property = injectorType.GetProperty("RegisteredTypes", BindingFlags.Public | BindingFlags.Static);
                if (property == null)
                    return EmptyRegisteredTypes;

                object value = property.GetValue(null, null);
                IEnumerable<ItemManager.ItemType> typed = value as IEnumerable<ItemManager.ItemType>;
                if (typed != null)
                    return typed;

                IEnumerable untyped = value as IEnumerable;
                if (untyped == null)
                    return EmptyRegisteredTypes;

                List<ItemManager.ItemType> items = new List<ItemManager.ItemType>();
                foreach (object entry in untyped)
                {
                    if (entry is ItemManager.ItemType)
                        items.Add((ItemManager.ItemType)entry);
                }

                return items;
            }
            catch
            {
                return EmptyRegisteredTypes;
            }
        }

        /// <summary>
        /// Resolves a string item identifier to the game's runtime item type using ShelteredAPI content registration first.
        /// </summary>
        internal static bool ResolveItemType(string itemId, out ItemManager.ItemType type)
        {
            type = ItemManager.ItemType.Undefined;
            Type injectorType = ResolveContentInjectorType();
            if (injectorType == null)
                return false;

            try
            {
                MethodInfo method = injectorType.GetMethod("ResolveItemType", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                    return false;

                object[] args = new object[] { itemId, type };
                object result = method.Invoke(null, args);
                if (result is bool && (bool)result && args[1] is ItemManager.ItemType)
                {
                    type = (ItemManager.ItemType)args[1];
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static Type ResolveContentInjectorType()
        {
            return Type.GetType(ContentInjectorTypeName, false);
        }
    }
}
