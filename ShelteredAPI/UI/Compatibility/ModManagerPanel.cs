using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using ShelteredAPI.UI.Internal;
using UnityEngine;

namespace ShelteredAPI.UI.Compatibility
{
    /// <summary>
    /// Mod Manager panel that displays installed mods in a book-style UI similar to Scenario Selection.
    /// Manages widget positioning, depth management, and mod details display.
    /// </summary>
    internal class ModManagerPanel : BasePanel
    {
        public static bool IsShowingModManager = false;
        private static ModManagerPanel _instance;
        public static bool IsShowingInstance => _instance != null && _instance.IsShowing();

        private UIPanel _uiPanel;
        private TweenAlpha _tween;
        private List<UIButton> _modButtons = new List<UIButton>();
        
        private UILabel _detailTitle;
        private UILabel _detailVersion;
        private UILabel _detailAuthors;
        private UILabel _detailDescription;
        
        private UIButton _backButton;
        private UIButton _settingsButton;
        private ModEntry _currentMod;
        private bool _bookFound;
        private bool _initialized = false;
        private NGUIScrollHelper _scrollHelper;
        private ModManagerDescriptionScroller _descriptionScroller;
        private List<GameObject> _modButtonObjects = new List<GameObject>();

        public static void ShowPanel()
        {
            if (_instance == null)
            {
                var go = new GameObject("ModAPI_ModManagerPanel");
                var uiRoot = UnityEngine.Object.FindObjectOfType<UIRoot>();
                if (uiRoot != null)
                {
                    go.transform.SetParent(uiRoot.transform, false);
                    go.layer = uiRoot.gameObject.layer;
                }

                _instance = go.AddComponent<ModManagerPanel>();
                _instance.Initialise();
                
                // CRITICAL: DontDestroyOnLoad only works for root objects.
                if (go.transform.parent != null)
                {
                    go.transform.SetParent(null);
                }
                DontDestroyOnLoad(go);
            }

            UIPanelManager.Instance().PushPanel(_instance);
        }

        public override void Initialise()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                _uiPanel = gameObject.GetComponent<UIPanel>() ?? gameObject.AddComponent<UIPanel>();
                _uiPanel.depth = 10000;
                _uiPanel.alpha = 0f;

                _tween = gameObject.GetComponent<TweenAlpha>() ?? gameObject.AddComponent<TweenAlpha>();
                _tween.from = 0f;
                _tween.to = 1f;
                _tween.duration = 0.4f;
                _tween.ignoreTimeScale = true;

                // --- CLONE BOOK VISUALS ---
                _bookFound = ModManagerPanelScaffolding.TryCloneBookVisuals(this);

                // --- CLICK BLOCKER (but it should NOT trigger back, just block) ---
                ModManagerPanelScaffolding.CreateClickBlocker(transform, gameObject.layer);

                // --- FIND BUTTON TEMPLATE ---
                UIButton buttonTemplate = ModManagerPanelScaffolding.FindScenarioButtonTemplate();
                if (buttonTemplate == null)
                {
                    MMLog.WriteError("[ModManagerPanel] Could not find button template!");
                    return;
                }

                // --- CREATE UI ELEMENTS ---
                Color textColor = _bookFound ? new Color(0.1f, 0.1f, 0.1f) : Color.white;

                // Title (left page, top-center)
                // Force single-line title rendering to prevent occasional trailing-character wrap.
                var installedModsHeader = CreateSimpleLabel("Installed Mods", -280f, 275f, 34, textColor, NGUIText.Alignment.Center, 380);
                if (installedModsHeader != null)
                {
                    installedModsHeader.multiLine = false;
                    installedModsHeader.overflowMethod = UILabel.Overflow.ShrinkContent;
                    installedModsHeader.alignment = NGUIText.Alignment.Center;
                    installedModsHeader.ProcessText();
                    installedModsHeader.MarkAsChanged();
                }

                // Mod list buttons (left page, centered vertically)
                CreateModButtons(buttonTemplate, textColor);

                // Details labels (right page)
                CreateDetailLabels(textColor);

                // Back button
                CreateBackButton(buttonTemplate);

                // Settings button
                CreateSettingsButton(buttonTemplate);

                // Show first mod by default
                var mods = ModRuntime.LoadedMods;
                if (mods.Count > 0) ShowDetails(mods[0]);
                
                // --- SETUP SCROLLING for mod list ---
                // Available space: from startY (160) to just above back button (-300)
                // This gives us room for ~5-6 buttons before needing to scroll.
                // We restrict scrolling input to the left page bounds (X: -600 to 0).
                if (_modButtonObjects.Count > 0)
                {
                    _scrollHelper = gameObject.AddComponent<NGUIScrollHelper>();
                    _scrollHelper.Initialize(
                        items: _modButtonObjects,
                        startY: 160f,
                        itemSpacing: 90f,
                        minY: -300f, 
                        maxY: 160f,  
                        minX: -600f, 
                        maxX: 0f     
                    );
                }
            }
            catch (Exception ex) 
            { 
                MMLog.WriteError("[ModManagerPanel] Initialisation failed: " + ex.ToString()); 
            }
            
            base.Initialise();
        }

        private void CreateModButtons(UIButton template, Color textColor)
        {
            var mods = ModRuntime.LoadedMods;
            
            // Position on left page - need to center the buttons
            float startY = 160f;
            float spacing = 90f;
            float xPos = -280f; // Left page center
            
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                
                // Hook up click event with a closure-friendly copy of the mod entry
                var capture = mod;
                Vector3 pos = new Vector3(xPos, startY - (i * spacing), 0);
                UIButton btn = CreatePanelButton(template, "ModBtn_" + mod.Id, mod.Name, pos, 300, 70, 24, textColor, delegate
                {
                    ShowDetails(capture);
                });
                if (btn == null)
                    continue;
                GameObject btnGO = btn.gameObject;
                
                _modButtons.Add(btn);
                _modButtonObjects.Add(btnGO); // For the scroll helper to track
            }
        }

        private void CreateDetailLabels(Color textColor)
        {
            // Right page center X position
            float rightX = 300f;
            
            // Title - centered on right page
            _detailTitle = CreateSimpleLabel("Select a mod", rightX, 275f, 32, textColor, NGUIText.Alignment.Center, 420);
            _detailTitle.overflowMethod = UILabel.Overflow.ShrinkContent;
            
            // Version - centered
            _detailVersion = CreateSimpleLabel("", rightX, 230f, 20, new Color(0.5f, 0.5f, 0.5f), NGUIText.Alignment.Center, 420);
            
            // Authors - centered
            _detailAuthors = CreateSimpleLabel("", rightX, 195f, 20, new Color(0.3f, 0.3f, 0.45f), NGUIText.Alignment.Center, 420);
            
            // Description - scrollable on right page
            // Create a clipping panel for the description area
            var descContainer = new GameObject("DescriptionContainer");
            descContainer.transform.parent = transform;
            descContainer.transform.localPosition = new Vector3(rightX, -90f, 0f); // Below authors line
            descContainer.transform.localRotation = Quaternion.identity;
            descContainer.transform.localScale = Vector3.one;
            descContainer.layer = gameObject.layer;
            
            // Add BoxCollider so UIScrollView can detect scroll wheel and drag events.
            var descCollider = descContainer.AddComponent<BoxCollider>();
            descCollider.center = Vector3.zero;
            descCollider.size = new Vector3(460f, 360f, 1f); 
            descCollider.isTrigger = true;
            
            // Add UIPanel for clipping.
            var descPanel = descContainer.AddComponent<UIPanel>();
            descPanel.depth = 10019; 
            descPanel.clipping = UIDrawCall.Clipping.SoftClip;
            descPanel.baseClipRegion = new Vector4(0, 0, 460, 360); // Width x Height area.
            
            // Add UIScrollView for basic momentum logic.
            // Note: Manual scrolling is handled in Update() to bypass NGUI coordinate quirks.
            var scrollView = descContainer.AddComponent<UIScrollView>();
            scrollView.movement = UIScrollView.Movement.Vertical;
            scrollView.dragEffect = UIScrollView.DragEffect.MomentumAndSpring;
            scrollView.scrollWheelFactor = 0.5f; 
            scrollView.momentumAmount = 10f;
            scrollView.restrictWithinPanel = true;
            scrollView.disableDragIfFits = true;
            
            // Create the description label inside the scroll view
            // IMPORTANT: Create without parent first to avoid position conflicts
            var descLabelGO = new GameObject("DescriptionLabel");
            descLabelGO.transform.parent = descContainer.transform;
            descLabelGO.transform.localPosition = new Vector3(0f, 150f, 0f); // Start below authors
            descLabelGO.transform.localRotation = Quaternion.identity;
            descLabelGO.transform.localScale = Vector3.one;
            descLabelGO.layer = gameObject.layer;
            
            // Add UIWidget first (required for UIScrollView bounds calculation)
            var descWidget = descLabelGO.AddComponent<UIWidget>();
            descWidget.depth = 10020;
            descWidget.pivot = UIWidget.Pivot.Top;
            
            _detailDescription = descLabelGO.AddComponent<UILabel>();
            _detailDescription.text = "";
            _detailDescription.fontSize = 32;
            _detailDescription.color = textColor;
            _detailDescription.alignment = NGUIText.Alignment.Left;
            _detailDescription.width = 440;
            _detailDescription.depth = 10020;
            _detailDescription.overflowMethod = UILabel.Overflow.ResizeHeight;
            _detailDescription.maxLineCount = 0;
            _detailDescription.multiLine = true;
            _detailDescription.spacingX = 0;
            _detailDescription.spacingY = 0;
            _detailDescription.pivot = UIWidget.Pivot.Top;
            
            // Font choice: TrueType Arial is used for descriptions to ensure high readability 
            // and consistent scaling compared to some fixed-resolution bitmap fonts in-game.
            Font arialFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (arialFont != null)
            {
                _detailDescription.trueTypeFont = arialFont;
                _detailDescription.bitmapFont = null;
            }

            _descriptionScroller = descContainer.AddComponent<ModManagerDescriptionScroller>();
            _descriptionScroller.Initialize(_detailDescription, 360f, 50f, 600f, 150f, 50f);
            
            scrollView.ResetPosition();
        }

        private void CreateBackButton(UIButton template)
        {
            // Back button positioned at bottom-left, moved down 40px from original
            Vector3 backPos = new Vector3(-460f, -410f, 0);
            
            _backButton = CreatePanelButton(
                template,
                "BackButton",
                "Back",
                backPos,
                200,
                60,
                24,
                _bookFound ? new Color(0.1f, 0.1f, 0.1f) : Color.white,
                OnCancel);
        }

        private void CreateSettingsButton(UIButton template)
        {
            // Positioned under description, above back button area's counterpart on right page
            Vector3 pos = new Vector3(300f, -305f, 0); // Centered on right page, closer to description area

            _settingsButton = CreatePanelButton(template, "SettingsButton", "SETTINGS", pos, 200, 50, 22, new Color(0.1f, 0.1f, 0.1f), delegate
            {
                if (_currentMod != null)
                    ModSettingsPanel.Show(_currentMod);
            });
            
            // Only hide initially - will be shown by ShowDetails if applicable
            if (_settingsButton != null)
                _settingsButton.gameObject.SetActive(false);
        }

        private UIButton CreatePanelButton(UIButton template, string name, string text, Vector3 localPosition, int width, int height, int fontSize, Color labelColor, Action onClick)
        {
            UIButton button = UIUtil.CloneButton(template, transform, text);
            if (button == null)
                return null;

            GameObject buttonObject = button.gameObject;
            buttonObject.name = name;
            buttonObject.layer = gameObject.layer;
            NGUITools.SetLayer(buttonObject, gameObject.layer);
            buttonObject.SetActive(true);
            buttonObject.transform.localPosition = localPosition;
            buttonObject.transform.localRotation = Quaternion.identity;
            buttonObject.transform.localScale = Vector3.one;

            ApplyPanelButtonLayout(buttonObject, width, height, fontSize, labelColor);
            ConfigurePanelButtonClick(button, onClick);
            return button;
        }

        private static void ApplyPanelButtonLayout(GameObject buttonObject, int width, int height, int fontSize, Color labelColor)
        {
            UIWidget rootWidget = buttonObject.GetComponent<UIWidget>();
            if (rootWidget != null)
            {
                rootWidget.width = width;
                rootWidget.height = height;
                rootWidget.depth = 10015;
            }

            BoxCollider collider = buttonObject.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.center = Vector3.zero;
                collider.size = new Vector3(width, height, 1f);
            }

            UISprite[] sprites = buttonObject.GetComponentsInChildren<UISprite>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                    sprites[i].depth = 10015;
            }

            UILabel[] labels = buttonObject.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label == null)
                    continue;

                label.text = label.text ?? string.Empty;
                label.fontSize = fontSize;
                label.color = labelColor;
                label.alignment = NGUIText.Alignment.Center;
                label.overflowMethod = UILabel.Overflow.ShrinkContent;
                label.width = width - 20;
                label.depth = 10020;
                label.ProcessText();
                label.MarkAsChanged();
            }
        }

        private static void ConfigurePanelButtonClick(UIButton button, Action onClick)
        {
            if (button == null)
                return;

            if (button.onClick != null)
                button.onClick.Clear();
            button.isEnabled = true;

            UIEventListener[] inheritedListeners = button.gameObject.GetComponents<UIEventListener>();
            for (int i = 0; i < inheritedListeners.Length; i++)
            {
                if (inheritedListeners[i] != null)
                    UnityEngine.Object.DestroyImmediate(inheritedListeners[i]);
            }

            UIEventListener listener = button.gameObject.AddComponent<UIEventListener>();
            listener.onClick = delegate(GameObject clicked)
            {
                if (onClick != null)
                    onClick();
            };
        }

        private UILabel CreateSimpleLabel(string text, float x, float y, int fontSize, Color color, NGUIText.Alignment alignment, int width)
        {
            var go = new GameObject("Label_" + text.Replace(" ", "_"));
            go.transform.parent = transform;
            go.transform.localPosition = new Vector3(x, y, 0);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = gameObject.layer;
            
            var label = go.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.width = width;
            label.depth = 10020; // Text on top
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            
            // Set pivot based on alignment for proper centering
            if (alignment == NGUIText.Alignment.Center)
                label.pivot = UIWidget.Pivot.Center;
            else if (alignment == NGUIText.Alignment.Left)
                label.pivot = UIWidget.Pivot.Center; // Still center pivot, text flows left
            
            // Font assignment with fallback to ensure text is visible
            bool fontSet = false;
            
            // Try bitmap font from existing labels
            var allLabels = UnityEngine.Object.FindObjectsOfType<UILabel>();
            foreach (var sampleLabel in allLabels)
            {
                if (sampleLabel != null && sampleLabel.bitmapFont != null)
                {
                    label.bitmapFont = sampleLabel.bitmapFont;
                    label.trueTypeFont = null;
                    fontSet = true;
                    break;
                }
            }
            
            // Fallback to TTF if no bitmap font found
            if (!fontSet)
            {
                Font arialFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (arialFont != null)
                {
                    label.trueTypeFont = arialFont;
                    label.bitmapFont = null;
                    label.fontSize = fontSize;
                    fontSet = true;
                }
            }
            
            if (!fontSet) MMLog.WriteError("[ModManagerPanel] CRITICAL: Label '" + text + "' has NO FONT!");
            
            return label;
        }

        private void ShowDetails(ModEntry mod)
        {
            if (mod == null) return;
            
            // Update title (Upper case for book aesthetic)
            string titleText = mod.Name.ToUpper();
            _detailTitle.text = titleText;
            
            // Update version
            string versionText = "Version " + mod.Version;
            _detailVersion.text = versionText;
            
            // Update authors (Join multiple authors if present)
            string authors = "Unknown";
            if (mod.About?.authors != null && mod.About.authors.Length > 0)
                authors = string.Join(", ", mod.About.authors);
            string authorsText = "By " + authors;
            _detailAuthors.text = authorsText;
            
            // Update description
            string desc = "No description available.";
            if (mod.About != null && !string.IsNullOrEmpty(mod.About.description))
                desc = mod.About.description;
            _detailDescription.text = desc;
            
            // Reset description position to start (Y=150) when switching mods
            // to prevent scrolled state from carrying over
            if (_descriptionScroller != null)
                _descriptionScroller.ResetToTop();
            else
                _detailDescription.transform.localPosition = new Vector3(0f, 150f, 0f);
            
            // Force NGUI to update text geometry
            _detailTitle.ProcessText();
            _detailTitle.MarkAsChanged();
            _detailVersion.ProcessText();
            _detailVersion.MarkAsChanged();
            _detailAuthors.ProcessText();
            _detailAuthors.MarkAsChanged();
            _detailDescription.ProcessText();
            _detailDescription.MarkAsChanged();
            
            // Re-sync UIScrollView bounds now that content text has changed height.
            // SoftClip depends on proper bounds to fade out text correctly.
            var scrollView = _detailDescription.transform.parent.GetComponent<UIScrollView>();
            if (scrollView != null)
            {
                scrollView.ResetPosition();
                scrollView.UpdateScrollbars(true); 
            }

            // Update Settings Button
            _currentMod = mod;
            if (_settingsButton != null)
            {
                bool hasSettings = mod.SettingsProvider != null;
                _settingsButton.gameObject.SetActive(hasSettings);
            }
        }
        
        public override void OnShow()
        {
            IsShowingModManager = true;
            base.OnShow();
            if (_tween != null) _tween.PlayForward();
        }

        public override void OnHide(bool hiddenForPopup)
        {
            IsShowingModManager = false;
            base.OnHide(hiddenForPopup);
        }

        public override void OnClose()
        {
            IsShowingModManager = false;
            base.OnClose();
        }

        public override void OnCancel()
        {
            if (_tween != null)
            {
                _tween.PlayReverse();
                EventDelegate.Add(_tween.onFinished, () => {
                    Close();
                    var mainMenu = UnityEngine.Object.FindObjectOfType<MainMenu>();
                    if (mainMenu != null) mainMenu.OnResume();
                }, true);
            }
            else
            {
                Close();
            }
        }

        public override bool AlwaysShow() => false;
        public override bool PausesGameInput() => true;
        public override bool PausesGameTime() => true;
        public override bool Popup() => false;
    }
}
