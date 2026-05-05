using System;
using ModAPI.Core;
using ShelteredAPI.UI.FieldManual.Animations;
using UnityEngine;

using ShelteredAPI.Content;
namespace ShelteredAPI.UI.FieldManual.Panels
{
    /// <summary>
    /// Shared page-turn runtime for book-style field manual panels.
    /// </summary>
    internal sealed class FieldManualBookPageTurn
    {
        private readonly FieldManualPageTurnController _controller;
        private bool _controllerAxisButtonDown;

        private FieldManualBookPageTurn(
            FieldManualPageTurnController controller,
            VanillaPageTurnAssets assets,
            IFieldManualTransition pageTransition)
        {
            _controller = controller;
            Assets = assets;
            PageTransition = pageTransition;
        }

        public VanillaPageTurnAssets Assets { get; private set; }
        public IFieldManualTransition PageTransition { get; private set; }

        public bool IsLocked
        {
            get { return _controller != null && _controller.IsLocked; }
        }

        public static FieldManualBookPageTurn Attach(GameObject root, FieldManualWindowChrome chrome)
        {
            return Attach(root, chrome, null);
        }

        public static FieldManualBookPageTurn Attach(
            GameObject root,
            FieldManualWindowChrome chrome,
            VanillaPageTurnAssets assets)
        {
            if (root == null)
                throw new ArgumentNullException("root");
            if (chrome == null)
                throw new ArgumentNullException("chrome");

            VanillaPageTurnAssets resolvedAssets = assets ?? new VanillaPageTurnAssets();
            IFieldManualTransition pageTransition = new FieldManualFadeTransition(FieldManualTransitionProfile.VanillaPageInfoFade);
            FieldManualPageTurnController controller = root.AddComponent<FieldManualPageTurnController>();
            controller.Configure(
                FieldManualPageTurnProfile.VanillaClipboard,
                new FieldManualFadeTransition(FieldManualTransitionProfile.FadeOut(0.06f, 0f, UITweener.Method.EaseOut)),
                pageTransition,
                new FieldManualFadeTransition(FieldManualTransitionProfile.Between(0.35f, 1f, 0.12f, 0f, UITweener.Method.EaseOut)),
                new FieldManualPageTurnAudio(resolvedAssets),
                new FieldManualPageFlipOverlay(
                    resolvedAssets,
                    chrome.Textures,
                    chrome.Ui,
                    chrome.Metrics.PanelWidth - 40f,
                    chrome.Metrics.PanelHeight - 140f));

            return new FieldManualBookPageTurn(controller, resolvedAssets, pageTransition);
        }

        public bool HandlePageInput(int pageCount, Func<bool> shouldBlockInput, Action<int> changePage)
        {
            if (pageCount <= 1)
                return false;
            if (shouldBlockInput != null && shouldBlockInput())
                return false;
            if (IsLocked)
                return false;
            if (changePage == null)
                return false;

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.PageUp))
            {
                changePage(-1);
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.PageDown))
            {
                changePage(1);
                return true;
            }

            float horizontal = PlatformInput.GetAxis(PlatformInput.MenuInputAxis.UIhorizontal);
            if (!_controllerAxisButtonDown)
            {
                if (horizontal > 0.5f)
                {
                    changePage(1);
                    _controllerAxisButtonDown = true;
                    return true;
                }

                if (horizontal < -0.5f)
                {
                    changePage(-1);
                    _controllerAxisButtonDown = true;
                    return true;
                }
            }
            else if (horizontal < 0.5f && horizontal > -0.5f)
            {
                _controllerAxisButtonDown = false;
            }

            return false;
        }

        public bool TryTurn(
            int delta,
            GameObject contentRoot,
            GameObject flipParent,
            GameObject labelRoot,
            Func<int, bool> canTurn,
            Action<int> commitPage,
            Action rebuildPage)
        {
            if (_controller == null)
                return false;

            return _controller.TryTurn(delta, contentRoot, flipParent, labelRoot, canTurn, commitPage, rebuildPage);
        }
    }
}
