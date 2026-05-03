using System;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Primitives;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    internal sealed class VanillaArrowButtonWidget
    {
        private readonly VanillaPageTurnAssets _assets;
        private readonly UIPrimitiveFactory _ui;

        public VanillaArrowButtonWidget(VanillaPageTurnAssets assets, UIPrimitiveFactory ui)
        {
            _assets = assets;
            _ui = ui;
        }

        public GameObject Build(GameObject parent, PageTurnArrowDirection direction, string name, Vector3 position, int width, int height, Action onClick)
        {
            GameObject template = _assets != null ? _assets.FindArrowTemplate(direction) : null;
            if (template == null || parent == null)
                return null;

            GameObject clone = UnityEngine.Object.Instantiate(template) as GameObject;
            if (clone == null)
                return null;

            clone.name = name;
            clone.transform.SetParent(parent.transform, false);
            clone.transform.localPosition = position;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            clone.layer = parent.layer;
            NGUITools.SetLayer(clone, parent.layer);
            clone.SetActive(true);

            StripTemplateBehaviour(clone);
            ConfigureVisuals(clone, width, height);
            ConfigureCollider(clone, width, height, onClick);
            return clone;
        }

        private static void StripTemplateBehaviour(GameObject clone)
        {
            UIButtonMessage[] messages = clone.GetComponentsInChildren<UIButtonMessage>(true);
            for (int i = 0; i < messages.Length; i++)
            {
                if (messages[i] != null)
                    UnityEngine.Object.Destroy(messages[i]);
            }

            UIPlaySound[] sounds = clone.GetComponentsInChildren<UIPlaySound>(true);
            for (int i = 0; i < sounds.Length; i++)
            {
                if (sounds[i] != null)
                    UnityEngine.Object.Destroy(sounds[i]);
            }

            UI_PlaySound[] legacySounds = clone.GetComponentsInChildren<UI_PlaySound>(true);
            for (int i = 0; i < legacySounds.Length; i++)
            {
                if (legacySounds[i] != null)
                    UnityEngine.Object.Destroy(legacySounds[i]);
            }
        }

        private void ConfigureVisuals(GameObject clone, int width, int height)
        {
            int depth = _ui != null ? _ui.NextDepth() : 50150;
            UIWidget[] widgets = clone.GetComponentsInChildren<UIWidget>(true);
            UIWidget largest = null;
            int largestArea = -1;
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null)
                    continue;

                widget.depth = depth;
                widget.alpha = 1f;
                widget.enabled = true;
                UILabel label = widget as UILabel;
                if (label != null)
                    label.text = string.Empty;

                int area = widget.width * widget.height;
                if (!(widget is UILabel) && area > largestArea)
                {
                    largest = widget;
                    largestArea = area;
                }
            }

            if (largest != null)
            {
                largest.width = width;
                largest.height = height;
            }

            UIButton[] buttons = clone.GetComponentsInChildren<UIButton>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton button = buttons[i];
                if (button == null)
                    continue;

                if (button.onClick != null)
                    button.onClick.Clear();
                button.isEnabled = true;
                button.SetState(UIButtonColor.State.Normal, true);
            }
        }

        private static void ConfigureCollider(GameObject clone, int width, int height, Action onClick)
        {
            Collider[] childColliders = clone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                if (childColliders[i] != null && childColliders[i].gameObject != clone)
                    UnityEngine.Object.Destroy(childColliders[i]);
            }

            BoxCollider collider = clone.GetComponent<BoxCollider>();
            if (collider == null)
                collider = clone.AddComponent<BoxCollider>();

            collider.center = Vector3.zero;
            collider.size = new Vector3(width, height, 1f);

            UIEventListener listener = UIEventListener.Get(clone);
            listener.onClick = delegate(GameObject clicked)
            {
                if (onClick != null)
                    onClick();
            };
        }
    }
}
