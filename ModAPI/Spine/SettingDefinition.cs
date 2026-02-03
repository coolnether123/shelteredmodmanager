using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Spine
{
    /// <summary>
    /// Definition for a single setting entry in the UI.
    /// </summary>
    public class SettingDefinition
    {
        public string Id;
        public string FieldName;
        public string ParentId;
        public string Label;
        public string Tooltip;
        public int SortOrder;

        public SettingType Type;
        public SettingMode Mode = SettingMode.Both;

        // Architectural Flags
        public bool AllowExternalWrite = false;
        public Core.SyncMode SyncMode;
        public string DependsOnId; // ID of a Bool setting that enables this one

        // Runtime Visibility & Hierarchy
        public bool ShowInSimpleView = true;
        public bool ShowInAdvancedView = true;
        public Func<object, bool> VisibleWhen; // Runtime visibility predicate
        public bool ControlsChildVisibility = false; // Bool parents can hide children

        // Data
        public object DefaultValue;
        public float? MinValue;
        public float? MaxValue;
        public float? StepSize;
        public Type EnumType;

        // Presets (Simple Mode)
        public Dictionary<string, object> Presets = new Dictionary<string, object>();

        // Callbacks
        public Action<object> OnChanged;
        public Func<object, object, bool> Validate; // (newVal, data) => bool
        public Func<object, IEnumerable<string>> GetOptions; // (data) => strings
        public bool RequiresRestart;

        public string Category;
        
        // UI Visuals
        public bool EmphasizeAsHeader = false;
        public Color? HeaderColor;
    }
}