using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Wrapper for a created interactive UI element, providing direct access to its components
    /// without needing to use GetComponent or Find.
    /// </summary>
    public class InteractiveUIElement
    {
        public GameObject GameObject { get; internal set; }
        public UISprite Sprite { get; internal set; }
        public UIButton Button { get; internal set; }
        public BoxCollider Collider { get; internal set; }
    }

    /// <summary>
    /// Configuration options for creating an interactive UI element.
    /// </summary>
    public class UIElementOptions
    {
        public int Depth = 0;
        public Vector3 Scale = Vector3.one;
        public Quaternion Rotation = Quaternion.identity;
        
        /// <summary>
        /// If null, the collider will auto-size to match the sprite's dimensions.
        /// </summary>
        public Vector3? ColliderSize = null;
        
        /// <summary>
        /// Whether the button should change colors on hover/press.
        /// </summary>
        public bool AddInteractionFeedback = true;
        
        public Color? HoverColor = null;
        public Color? PressColor = null;
        
        /// <summary>
        /// The name of the game object.
        /// </summary>
        public string Name = "ModdedUIElement";
    }

    public enum ArrowDirection { Left, Right, Up, Down }

    /// <summary>
    /// A tiered factory system for creating modded interactive UI elements.
    /// Provides simple one-liners for common cases and a flexible options pattern for complex ones.
    /// </summary>
    public static class UIFactory
    {
        private static readonly Color DefaultHover = new Color(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color DefaultPress = new Color(0.6f, 0.6f, 0.6f, 1f);

        // --- Tier 1: Simple Icon Button ---

        /// <summary>
        /// Create a clickable icon in one line. Best for simple toggles or static buttons.
        /// </summary>
        public static GameObject CreateIconButton(Transform parent, string sprite, Vector3 position, float scale = 1f, Action onClick = null)
        {
            var options = new UIElementOptions
            {
                Scale = Vector3.one * scale,
                Name = "IconButton_" + sprite
            };

            return CreateInteractiveElement(parent, sprite, position, onClick, options).GameObject;
        }

        // --- Tier 2: Arrow Button Helper ---

        /// <summary>
        /// Create directional arrows with automatic rotation and semantic alignment.
        /// </summary>
        public static GameObject CreateArrowButton(Transform parent, ArrowDirection direction, Vector3 position, Action onClick, string templateSprite = "arrow", float scale = 1f, int depth = 0)
        {
            float angle = 0f;
            switch (direction)
            {
                case ArrowDirection.Left: angle = 180f; break;
                case ArrowDirection.Right: angle = 0f; break;
                case ArrowDirection.Up: angle = 90f; break;
                case ArrowDirection.Down: angle = 270f; break;
            }

            var options = new UIElementOptions
            {
                Rotation = Quaternion.Euler(0, 0, angle),
                Scale = Vector3.one * scale,
                Depth = depth,
                Name = "ArrowButton_" + direction
            };

            return CreateInteractiveElement(parent, templateSprite, position, onClick, options).GameObject;
        }

        // --- Tier 3: Interactive UI Element Factory ---

        /// <summary>
        /// The master factory method for creating fully configured interactive UI elements.
        /// Returns a wrapper with direct component access for further tweaking.
        /// </summary>
        public static InteractiveUIElement CreateInteractiveElement(Transform parent, string sprite, Vector3 position, Action onClick, UIElementOptions options = null)
        {
            if (options == null) options = new UIElementOptions();

            // 1. Create GameObject
            var go = new GameObject(options.Name);
            go.layer = (parent != null) ? parent.gameObject.layer : LayerMask.NameToLayer("UI");
            go.transform.parent = parent;
            go.transform.localPosition = position;
            go.transform.localRotation = options.Rotation;
            go.transform.localScale = options.Scale;

            // 2. Setup Sprite
            var uiSprite = go.AddComponent<UISprite>();
            UIHelper.SetSpriteFromPath(uiSprite, sprite);
            uiSprite.depth = options.Depth;
            uiSprite.MakePixelPerfect(); // Foundation for sizing

            // 3. Setup Collider (for interaction)
            var collider = go.AddComponent<BoxCollider>();
            if (options.ColliderSize.HasValue)
            {
                collider.size = options.ColliderSize.Value;
            }
            else
            {
                // Auto-size to sprite
                collider.size = new Vector3(uiSprite.width, uiSprite.height, 1f);
            }

            // 4. Setup Button
            var button = go.AddComponent<UIButton>();
            button.tweenTarget = go;
            
            if (options.AddInteractionFeedback)
            {
                button.hover = options.HoverColor ?? DefaultHover;
                button.pressed = options.PressColor ?? DefaultPress;
            }

            if (onClick != null)
            {
                EventDelegate.Add(button.onClick, new EventDelegate(() => onClick()));
            }

            return new InteractiveUIElement
            {
                GameObject = go,
                Sprite = uiSprite,
                Button = button,
                Collider = collider
            };
        }
    }
}
