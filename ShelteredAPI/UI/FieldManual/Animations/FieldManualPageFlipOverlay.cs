using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using UnityEngine;
using ModAPI.Core;

namespace ShelteredAPI.UI.FieldManual.Animations
{
    /// <summary>
    /// Plays an optional vanilla sprite flip overlay. If no vanilla animation exists,
    /// a short paper sweep gives the same hide/reveal cadence without new assets.
    /// </summary>
    internal sealed class FieldManualPageFlipOverlay
    {
        private readonly VanillaPageTurnAssets _assets;
        private readonly ITextureLibrary _textures;
        private readonly UIPrimitiveFactory _ui;
        private readonly float _defaultWidth;
        private readonly float _defaultHeight;

        public FieldManualPageFlipOverlay(VanillaPageTurnAssets assets, ITextureLibrary textures, UIPrimitiveFactory ui)
            : this(assets, textures, ui, 1240f, 680f)
        {
        }

        public FieldManualPageFlipOverlay(VanillaPageTurnAssets assets, ITextureLibrary textures, UIPrimitiveFactory ui, float defaultWidth, float defaultHeight)
        {
            _assets = assets;
            _textures = textures;
            _ui = ui;
            _defaultWidth = defaultWidth <= 0f ? 1240f : defaultWidth;
            _defaultHeight = defaultHeight <= 0f ? 680f : defaultHeight;
        }

        public GameObject Play(GameObject parent, float duration, int direction)
        {
            if (parent == null)
                return null;

            PageFlipBounds bounds = PageFlipBounds.FromParent(parent, _defaultWidth, _defaultHeight);
            GameObject overlay = TryCloneVanillaAnimation(parent, bounds, direction);
            if (overlay == null)
                overlay = BuildFallbackSweep(parent, duration, bounds, direction);

            if (overlay == null)
                return null;

            FieldManualTimedDestroy destroy = overlay.AddComponent<FieldManualTimedDestroy>();
            destroy.Lifetime = duration <= 0f ? 0.01f : duration;
            overlay.SetActive(true);
            return overlay;
        }

        private GameObject TryCloneVanillaAnimation(GameObject parent, PageFlipBounds bounds, int direction)
        {
            GameObject template = _assets != null ? _assets.FindFlipAnimationTemplate() : null;
            if (template == null)
                return null;

            GameObject clone = UnityEngine.Object.Instantiate(template) as GameObject;
            if (clone == null)
                return null;

            clone.name = "PageFlipAnimation";
            clone.transform.SetParent(parent.transform, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            clone.layer = parent.layer;
            NGUITools.SetLayer(clone, parent.layer);
            StripInteraction(clone);
            ConfigureWidgets(clone, bounds);
            NormalizeCloneToBounds(clone, bounds);
            PlaySpriteAnimations(clone, direction);
            MMLog.WriteInfo("[FieldManual] Using vanilla page flip animation template '" + template.name
                + "' in bounds " + bounds.Width + "x" + bounds.Height + " direction=" + DirectionName(direction) + ".");
            return clone;
        }

        private GameObject BuildFallbackSweep(GameObject parent, float duration, PageFlipBounds bounds, int direction)
        {
            if (_textures == null || _ui == null)
                return null;

            int sheetWidth = Mathf.Max(620, Mathf.RoundToInt(bounds.Width * 0.58f));
            int sheetHeight = Mathf.Max(640, Mathf.RoundToInt(bounds.Height));
            UITexture sweep = _ui.CreateQuad(parent, "PageFlipSweep",
                _textures.Paper(sheetWidth, sheetHeight), Vector3.zero, sheetWidth, sheetHeight,
                new Color(1f, 1f, 1f, 0.82f), _ui.NextDepth());
            sweep.pivot = UIWidget.Pivot.Center;
            FieldManualFallbackPageFlip fallback = sweep.gameObject.AddComponent<FieldManualFallbackPageFlip>();
            fallback.Duration = duration <= 0f ? FieldManualPageTurnProfile.VanillaClipboard.FlipDuration : duration;
            fallback.TravelDistance = bounds.Width * 0.29f;
            fallback.Direction = direction >= 0 ? 1 : -1;
            fallback.StartScaleX = 1f;
            fallback.MinimumScaleX = 0.12f;
            MMLog.WriteDebug("[FieldManual] Using fallback page flip sweep " + sheetWidth + "x" + sheetHeight
                + " in bounds " + bounds.Width + "x" + bounds.Height + " direction=" + DirectionName(direction) + ".");
            return sweep.gameObject;
        }

        private void ConfigureWidgets(GameObject clone, PageFlipBounds bounds)
        {
            UIWidget[] widgets = clone.GetComponentsInChildren<UIWidget>(true);
            int depth = _ui != null ? _ui.NextDepth() : 50150;
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null)
                    continue;

                widget.depth = depth;
                widget.alpha = 1f;
                widget.enabled = true;

                if (widget.width < bounds.Width * 0.5f)
                    widget.width = Mathf.RoundToInt(bounds.Width * 0.58f);
                if (widget.height < bounds.Height * 0.85f)
                    widget.height = Mathf.RoundToInt(bounds.Height);
            }
        }

        private static void NormalizeCloneToBounds(GameObject clone, PageFlipBounds bounds)
        {
            Bounds localBounds;
            if (!TryCalculateWidgetBounds(clone, out localBounds))
                return;

            Vector3 center = localBounds.center;
            float width = Mathf.Max(1f, localBounds.size.x);
            float height = Mathf.Max(1f, localBounds.size.y);
            float targetWidth = Mathf.Max(1f, bounds.Width * 0.58f);
            float targetHeight = Mathf.Max(1f, bounds.Height);
            float scale = Mathf.Min(targetWidth / width, targetHeight / height);
            scale = Mathf.Clamp(scale, 0.25f, 8f);

            clone.transform.localScale = new Vector3(scale, scale, 1f);
            clone.transform.localPosition = new Vector3(-center.x * scale, -center.y * scale, 0f);
        }

        private static bool TryCalculateWidgetBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            UIWidget[] widgets = root.GetComponentsInChildren<UIWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null)
                    continue;

                Vector3 localPosition = root.transform.InverseTransformPoint(widget.transform.position);
                Vector3 size = new Vector3(Mathf.Max(1, widget.width), Mathf.Max(1, widget.height), 0f);
                Bounds widgetBounds = new Bounds(localPosition, size);
                if (!hasBounds)
                {
                    bounds = widgetBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(widgetBounds);
                }
            }

            return hasBounds;
        }

        private static void StripInteraction(GameObject clone)
        {
            Collider[] colliders = clone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    UnityEngine.Object.Destroy(colliders[i]);
            }

            UIButton[] buttons = clone.GetComponentsInChildren<UIButton>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].onClick != null)
                    buttons[i].onClick.Clear();
            }
        }

        private static void PlaySpriteAnimations(GameObject clone, int direction)
        {
            UISpriteAnimationEx[] exAnimations = clone.GetComponentsInChildren<UISpriteAnimationEx>(true);
            for (int i = 0; i < exAnimations.Length; i++)
            {
                UISpriteAnimationEx animation = exAnimations[i];
                if (animation == null)
                    continue;

                animation.m_looping = false;
                animation.loop = false;
                if (direction >= 0)
                    animation.Play();
                else
                    animation.PlayReversed();
            }

            UISpriteAnimation[] animations = clone.GetComponentsInChildren<UISpriteAnimation>(true);
            for (int i = 0; i < animations.Length; i++)
            {
                UISpriteAnimation animation = animations[i];
                if (animation == null || animation is UISpriteAnimationEx)
                    continue;

                animation.loop = false;
                animation.Reset();
            }
        }

        private static string DirectionName(int direction)
        {
            return direction >= 0 ? "next-right-to-left" : "previous-left-to-right";
        }

        private sealed class PageFlipBounds
        {
            public readonly float Width;
            public readonly float Height;

            private PageFlipBounds(float width, float height)
            {
                Width = width;
                Height = height;
            }

            public static PageFlipBounds FromParent(GameObject parent, float defaultWidth, float defaultHeight)
            {
                UIPanel panel = parent != null ? parent.GetComponent<UIPanel>() : null;
                if (panel != null)
                {
                    float width = Mathf.Abs(panel.baseClipRegion.z);
                    float height = Mathf.Abs(panel.baseClipRegion.w);
                    if (width > 1f && height > 1f)
                        return new PageFlipBounds(width, Mathf.Max(height, 520f));
                }

                return new PageFlipBounds(defaultWidth, defaultHeight);
            }
        }
    }
}
