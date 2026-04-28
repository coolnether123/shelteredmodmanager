using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal static class ModUIHookRuntimeService
    {
        internal static void ProcessPanel(BasePanel panel)
        {
            TargetMenu? menuType = GetMenuType(panel);
            if (menuType == null)
                return;

            List<ModUIButtonHook> hooks = ModUIHookRegistry.Snapshot();
            for (int i = 0; i < hooks.Count; i++)
            {
                ModUIButtonHook hook = hooks[i];
                if (hook != null && hook.Menu == menuType.Value)
                    InjectButton(panel, hook);
            }
        }

        private static TargetMenu? GetMenuType(BasePanel panel)
        {
            if (panel is RadioDialogPanel)
                return TargetMenu.Radio;

            return null;
        }

        private static void InjectButton(BasePanel panel, ModUIButtonHook hook)
        {
            UIRuntimeServiceHelper.Run("ModUIHooks.InjectButton", delegate
            {
                GameObject parent = panel.gameObject;

                GameObject btnObj = new GameObject("ModButton_" + hook.Text);
                btnObj.transform.parent = parent.transform;
                btnObj.transform.localPosition = Vector3.zero;
                btnObj.layer = parent.layer;

                UISprite sprite = btnObj.AddComponent<UISprite>();
                sprite.type = UISprite.Type.Sliced;
                sprite.spriteName = "button_dark_thin_64";
                sprite.width = 200;
                sprite.height = 50;

                UIButton button = btnObj.AddComponent<UIButton>();
                button.tweenTarget = btnObj;

                GameObject labelObj = new GameObject("Label");
                labelObj.transform.parent = btnObj.transform;
                labelObj.transform.localPosition = Vector3.zero;
                labelObj.layer = btnObj.layer;

                UILabel label = labelObj.AddComponent<UILabel>();
                label.trueTypeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.text = hook.Text;
                label.fontSize = 25;
                label.alignment = NGUIText.Alignment.Center;

                EventDelegate.Add(button.onClick, delegate
                {
                    if (hook.OnClick != null)
                        hook.OnClick();
                });

                NGUIHelper.SetToTopDepth(sprite);
                label.depth = sprite.depth + 1;

                ModLog.Debug("Injected button '" + hook.Text + "' into " + panel.name);
            });
        }
    }
}
