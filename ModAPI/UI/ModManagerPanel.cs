using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Internal.UI;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Mod Manager panel that displays installed mods in a book-style UI similar to Scenario Selection.
    /// Manages widget positioning, depth management, and mod details display.
    /// </summary>
    public class ModManagerPanel : BasePanel
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
                var mods = PluginManager.LoadedMods;
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
            var mods = PluginManager.LoadedMods;
            
            // Position on left page - need to center the buttons
            float startY = 160f;
            float spacing = 90f;
            float xPos = -280f; // Left page center
            
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                
                // Clone button
                var btnGO = (GameObject)UnityEngine.Object.Instantiate(template.gameObject);
                btnGO.transform.parent = transform;
                btnGO.name = "ModBtn_" + mod.Id;
                btnGO.layer = gameObject.layer;
                
                // Remove localization components that override text
                var localize = btnGO.GetComponentsInChildren<UILocalize>(true);
                foreach (var loc in localize) UnityEngine.Object.DestroyImmediate(loc);
                
                var buttonMsgs = btnGO.GetComponentsInChildren<UIButtonMessage>(true);
                foreach (var msg in buttonMsgs) UnityEngine.Object.DestroyImmediate(msg);
                
                btnGO.SetActive(true);
                
                // Position - NGUI uses center pivot by default for positioned elements
                Vector3 pos = new Vector3(xPos, startY - (i * spacing), 0);
                btnGO.transform.localPosition = pos;
                btnGO.transform.localRotation = Quaternion.identity;
                btnGO.transform.localScale = Vector3.one;
                
                // Set size
                var widget = btnGO.GetComponent<UIWidget>();
                if (widget != null) 
                {
                    widget.width = 300;
                    widget.height = 70;
                    widget.depth = 10015; // Above book, below text
                }
                
                // Update label - CRITICAL FIX
                var label = btnGO.GetComponentInChildren<UILabel>();
                if (label != null)
                {
                    // Destroy any localization on the label itself
                    var labelLocalize = label.GetComponent<UILocalize>();
                    if (labelLocalize != null) UnityEngine.Object.DestroyImmediate(labelLocalize);
                    
                    label.text = mod.Name;
                    label.fontSize = 24;
                    label.color = textColor;
                    label.alignment = NGUIText.Alignment.Center;
                    label.overflowMethod = UILabel.Overflow.ShrinkContent;
                    label.width = 280;
                    label.depth = 10020; // Text on top
                    
                    // Force immediate update
                    label.ProcessText();
                    label.MarkAsChanged();
                }
                
                // Set background sprite depths
                var sprites = btnGO.GetComponentsInChildren<UISprite>(true);
                foreach (var s in sprites)
                {
                    if (s != widget) s.depth = 10015; // Background sprites
                }
                
                // Get the UIButton component
                var btn = btnGO.GetComponent<UIButton>();
                if (btn != null)
                {
                    if (btn.onClick != null) btn.onClick.Clear();
                    btn.isEnabled = true;
                }
                
                // Hook up click event with a closure-friendly copy of the mod entry
                var capture = mod;
                UIEventListener.Get(btnGO).onClick = (g) => ShowDetails(capture);
                
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
            
            // Clone back button
            var btnGO = (GameObject)UnityEngine.Object.Instantiate(template.gameObject);
            btnGO.transform.parent = transform;
            btnGO.name = "BackButton";
            btnGO.layer = gameObject.layer;
            
            // Remove localization components
            var localize = btnGO.GetComponentsInChildren<UILocalize>(true);
            foreach (var loc in localize) UnityEngine.Object.DestroyImmediate(loc);
            
            var buttonMsgs = btnGO.GetComponentsInChildren<UIButtonMessage>(true);
            foreach (var msg in buttonMsgs) UnityEngine.Object.DestroyImmediate(msg);
            
            btnGO.SetActive(true);
            btnGO.transform.localPosition = backPos;
            btnGO.transform.localRotation = Quaternion.identity;
            btnGO.transform.localScale = Vector3.one;
            
            // Set size
            var widget = btnGO.GetComponent<UIWidget>();
            if (widget != null)
            {
                widget.width = 200;
                widget.height = 60;
                widget.depth = 10015;
            }
            
            // Update label - CRITICAL: Make sure text is visible
            var label = btnGO.GetComponentInChildren<UILabel>();
            if (label != null)
            {
                var labelLocalize = label.GetComponent<UILocalize>();
                if (labelLocalize != null) UnityEngine.Object.DestroyImmediate(labelLocalize);
                
                label.text = "Back";
                label.fontSize = 24;
                label.color = _bookFound ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
                label.alignment = NGUIText.Alignment.Center;
                label.depth = 10020;
                label.ProcessText();
                label.MarkAsChanged();
            }
            
            // Set background depths
            var sprites = btnGO.GetComponentsInChildren<UISprite>(true);
            foreach (var s in sprites)
            {
                s.depth = 10015;
            }
            
            // Get button and ensure it's enabled
            var btn = btnGO.GetComponent<UIButton>();
            if (btn != null)
            {
                if (btn.onClick != null) btn.onClick.Clear();
                btn.isEnabled = true;
            }
            
            UIEventListener.Get(btnGO).onClick = (g) => OnCancel();
            _backButton = btn;
        }

        private void CreateSettingsButton(UIButton template)
        {
            // Positioned under description, above back button area's counterpart on right page
            Vector3 pos = new Vector3(300f, -305f, 0); // Centered on right page, closer to description area
            
            var btnGO = (GameObject)UnityEngine.Object.Instantiate(template.gameObject);
            btnGO.transform.parent = transform;
            btnGO.name = "SettingsButton";
            btnGO.layer = gameObject.layer;
            
            // Cleanup
            var localize = btnGO.GetComponentsInChildren<UILocalize>(true);
            foreach (var loc in localize) UnityEngine.Object.DestroyImmediate(loc);
            
            var buttonMsgs = btnGO.GetComponentsInChildren<UIButtonMessage>(true);
            foreach (var msg in buttonMsgs) UnityEngine.Object.DestroyImmediate(msg);
            
            btnGO.SetActive(true);
            btnGO.transform.localPosition = pos;
            btnGO.transform.localRotation = Quaternion.identity;
            btnGO.transform.localScale = Vector3.one;
            
            // Size
            var widget = btnGO.GetComponent<UIWidget>();
            if (widget != null)
            {
                widget.width = 200;
                widget.height = 50;
                widget.depth = 10015;
            }
            
            // Label
            var label = btnGO.GetComponentInChildren<UILabel>();
            if (label != null)
            {
                var labelLocalize = label.GetComponent<UILocalize>();
                if (labelLocalize != null) UnityEngine.Object.DestroyImmediate(labelLocalize);
                
                label.text = "SETTINGS";
                label.fontSize = 22;
                label.color = new Color(0.1f, 0.1f, 0.1f);
                label.alignment = NGUIText.Alignment.Center;
                label.depth = 10020;
                label.ProcessText();
                label.MarkAsChanged();
            }
            
            // Depths
            var sprites = btnGO.GetComponentsInChildren<UISprite>(true);
            foreach (var s in sprites) s.depth = 10015;
            
            var btn = btnGO.GetComponent<UIButton>();
            if (btn != null)
            {
                if (btn.onClick != null) btn.onClick.Clear();
                btn.isEnabled = true;
            }
            
            UIEventListener.Get(btnGO).onClick = (g) => 
            {
                // Capture current mod
                // We need to store current mod in a field since ShowDetails sets it
                if (_currentMod != null)
                {
                    ModSettingsPanel.Show(_currentMod);
                }
            };
            
            _settingsButton = btn;
            
            // Only hide initially - will be shown by ShowDetails if applicable
            btnGO.SetActive(false); 
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
        
        private ModEntry _currentMod;

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
