using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.UI;

namespace ModAPI.Internal.UI
{
    internal sealed class ModUIButtonHook
    {
        public TargetMenu Menu;
        public string Text;
        public Action OnClick;
    }

    internal static class ModUIHookRegistry
    {
        private static readonly List<ModUIButtonHook> Hooks = new List<ModUIButtonHook>();

        internal static void Register(TargetMenu menu, string buttonText, Action onClick)
        {
            Hooks.Add(new ModUIButtonHook
            {
                Menu = menu,
                Text = buttonText,
                OnClick = onClick
            });

            ModLog.Debug("Registered UI hook for " + menu + ": " + buttonText);
        }

        internal static List<ModUIButtonHook> Snapshot()
        {
            return new List<ModUIButtonHook>(Hooks);
        }
    }
}
