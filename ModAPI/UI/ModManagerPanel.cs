using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Mod Manager panel that displays installed mods in a book-style UI similar to Scenario Selection.
    /// Uses proper NGUI widget positioning and depth management.
    /// </summary>
    public class ModManagerPanel : BasePanel
    {
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
        private bool _bookFound;
        private bool _initialized = false;
        private NGUIScrollHelper _scrollHelper;
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
                _bookFound = CloneBookVisuals();

                // --- CLICK BLOCKER (but it should NOT trigger back, just block) ---
                CreateClickBlocker();

                // --- FIND BUTTON TEMPLATE ---
                UIButton buttonTemplate = FindScenarioButtonTemplate();
                if (buttonTemplate == null)
                {
                    MMLog.WriteError("[ModManagerPanel] Could not find button template!");
                    return;
                }

                // --- CREATE UI ELEMENTS ---
                Color textColor = _bookFound ? new Color(0.1f, 0.1f, 0.1f) : Color.white;

                // Title (left page, top-center)
                CreateSimpleLabel("Installed Mods", -280f, 275f, 34, textColor, NGUIText.Alignment.Center, 300);

                // Mod list buttons (left page, centered vertically)
                CreateModButtons(buttonTemplate, textColor);

                // Details labels (right page)
                CreateDetailLabels(textColor);

                // Back button
                CreateBackButton(buttonTemplate);

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

        private bool CloneBookVisuals()
        {
            try
            {
                BasePanel scenarioPanel = FindScenarioPanel();
                if (scenarioPanel == null) return false;

                // We clone the book visuals from the ScenarioSelectionPanel to maintain game's aesthetic.
                MMLog.Write("[ModAPI] Loading Mod Manager UI...");
                
                foreach (Transform child in scenarioPanel.transform)
                {
                    // Skip UI panels and buttons
                    if (child.GetComponent<UIPanel>() != null) continue;
                    if (child.GetComponent<UIButton>() != null) continue;
                    
                    // Clone visual elements (book background, etc.)
                    string name = child.name.ToLower();
                    bool isVisual = name.Contains("background") || name.Contains("book") || 
                                   name.Contains("visual") || name.Contains("root") || name.Contains("tween");
                    
                    if (isVisual)
                    {
                        var clone = (GameObject)UnityEngine.Object.Instantiate(child.gameObject);
                        clone.transform.parent = transform;
                        clone.name = "Cloned_" + child.name;
                        clone.transform.localPosition = child.localPosition;
                        clone.transform.localScale = child.localScale;
                        clone.transform.localRotation = child.localRotation;
                        clone.layer = gameObject.layer;

                        // Remove ALL interactive components and text from cloned visuals
                        var buttons = clone.GetComponentsInChildren<UIButton>(true);
                        foreach (var b in buttons) UnityEngine.Object.Destroy(b.gameObject);
                        
                        var labels = clone.GetComponentsInChildren<UILabel>(true);
                        foreach (var l in labels) UnityEngine.Object.Destroy(l.gameObject);
                        
                        // CRITICAL: Destroy all colliders to prevent click-through to scenario buttons.
                        // Since we clone the entire background tree, we must ensure old button targets are dead.
                        var colliders = clone.GetComponentsInChildren<Collider>(true);
                        foreach (var c in colliders) UnityEngine.Object.Destroy(c);
                        
                        // Set depth for background elements
                        var widgets = clone.GetComponentsInChildren<UIWidget>(true);
                        foreach (var w in widgets)
                        {
                            w.gameObject.layer = gameObject.layer;
                            w.depth = 10005; // Behind our content but above main menu
                        }
                        
                        clone.SetActive(true);
                        
                        // Found and cloned book visuals successfully.
                        return true; 
                    }
                }
            }
            catch (Exception ex) 
            { 
                MMLog.WriteError("[ModManagerPanel] Book clone error: " + ex.Message); 
            }
            return false;
        }

        private void CreateClickBlocker()
        {
            var blocker = new GameObject("ClickBlocker");
            blocker.transform.parent = transform;
            blocker.transform.localPosition = Vector3.zero;
            blocker.layer = gameObject.layer;
            
            var sprite = blocker.AddComponent<UISprite>();
            sprite.color = new Color(0, 0, 0, 0.75f);
            sprite.width = 10000;
            sprite.height = 10000;
            sprite.depth = 9999; // Behind book
            
            var col = blocker.AddComponent<BoxCollider>();
            col.size = new Vector3(10000, 10000, 1);
            
            // IMPORTANT: Just block clicks, don't trigger any action
            UIEventListener.Get(blocker).onClick = (g) => { /* Do nothing, just block */ };
        }

        private UIButton FindScenarioButtonTemplate()
        {
            try
            {
                BasePanel scenarioPanel = FindScenarioPanel();
                if (scenarioPanel != null)
                {
                    var buttons = scenarioPanel.GetComponentsInChildren<UIButton>(true);
                    
                    if (buttons != null && buttons.Length > 0)
                    {
                        // Find a non-back button (scenario selection button)
                        foreach (var btn in buttons)
                        {
                            string name = btn.name.ToLower();
                            if (!name.Contains("back") && !name.Contains("cancel"))
                            {
                                return btn;
                            }
                        }
                        return buttons[0]; // Fallback to first button
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModManagerPanel] Error finding button template: " + ex.Message);
            }

            // Ultimate fallback
            return UIUtil.FindAnyButtonTemplate();
        }
        
        private Vector3 FindBackButtonPosition()
        {
            Vector3 defaultPos = new Vector3(-460f, -370f, 0); // Lower default position
            
            try
            {
                BasePanel scenarioPanel = FindScenarioPanel();
                if (scenarioPanel != null)
                {
                    var buttons = scenarioPanel.GetComponentsInChildren<UIButton>(true);
                    foreach (var btn in buttons)
                    {
                        string name = btn.name.ToLower();
                        if (name.Contains("back") || name.Contains("cancel"))
                        {
                            defaultPos = btn.transform.localPosition;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModManagerPanel] Error finding back button position: " + ex.Message);
            }
            
            return defaultPos;
        }

        private BasePanel FindScenarioPanel()
        {
            // Try hierarchy traversal first
            var fe = FrontEndController.instance;
            if (fe != null && fe.mainMenu != null)
            {
                var mm = fe.mainMenu as MainMenu;
                var modeField = typeof(MainMenu).GetField("m_gameModeSelectionPanel", BindingFlags.NonPublic | BindingFlags.Instance);
                var modePanel = modeField?.GetValue(mm) as GameModeSelectionPanel;
                if (modePanel != null)
                {
                    var scenarioField = typeof(GameModeSelectionPanel).GetField("m_scenarioSelectionPanel", BindingFlags.NonPublic | BindingFlags.Instance);
                    var panel = scenarioField?.GetValue(modePanel) as BasePanel;
                    if (panel != null) return panel;
                }
            }

            // Fallback to searching all panels
            var allPanels = Resources.FindObjectsOfTypeAll<BasePanel>();
            foreach (var p in allPanels)
            {
                if (p.name.Contains("ScenarioSelectionPanel") || p.GetType().Name.Contains("ScenarioSelection"))
                    return p;
            }
            
            return null;
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
                
                // CRITICAL: Remove localization components that override text
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
            descCollider.size = new Vector3(460f, 460f, 1f); 
            descCollider.isTrigger = true;
            
            // Add UIPanel for clipping.
            var descPanel = descContainer.AddComponent<UIPanel>();
            descPanel.depth = 10019; 
            descPanel.clipping = UIDrawCall.Clipping.SoftClip;
            descPanel.baseClipRegion = new Vector4(0, 0, 460, 460); // Width x Height area.
            
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
            
            scrollView.ResetPosition();
        }

        private void CreateBackButton(UIButton template)
        {
            Vector3 backPos = FindBackButtonPosition();
            
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
            
            // CRITICAL: Font assignment with fallback
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
            
            // CRITICAL: Reset description position to start (Y=150) when switching mods.
            // This ensures long descriptions from previous mods don't leave the view scrolled down.
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
        }

        public override void OnShow()
        {
            base.OnShow();
            if (_tween != null) _tween.PlayForward();
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
        
        // Manual description scrolling
        void Update()
        {
            if (_detailDescription == null) return;
            
            // Check if description needs scrolling (height > clip area)
            if (_detailDescription.height <= 460) return;
            
            // Check for scroll input
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll == 0f)
                scroll = Input.mouseScrollDelta.y;
            
            if (scroll == 0f) return;
            
            // Check if mouse is over right page (description area)
            Vector3 mousePos = Input.mousePosition;
            float uiX = (mousePos.x - Screen.width / 2f);
            
            // Right page is roughly X: 50 to 550
            if (uiX < 50f || uiX > 600f) return;
            
            // Adjust description Y position
            // Scroll DOWN (negative scroll) = text moves UP (increase Y) to show more below
            // Scroll UP (positive scroll) = text moves DOWN (decrease Y) to show content above
            Vector3 pos = _detailDescription.transform.localPosition;
            float scrollSpeed = 50f; // pixels per scroll unit
            
            // INVERT: negative scroll (down) should increase Y (text moves up)
            pos.y -= scroll * scrollSpeed;
            
            // Clamp: Start at 150 (top), can go UP to show more content (higher Y values)
            float minY = 150f; // Starting/top position - text at top of clip area
            float maxY = 150f + (_detailDescription.height - 460f); // Fully scrolled - shows bottom of text
            
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            
            _detailDescription.transform.localPosition = pos;
            
            MMLog.Write(string.Format("[ModManagerPanel] Description scrolled to Y={0:F1} (range: {1:F1} to {2:F1})", 
                pos.y, minY, maxY));
        }
    }
}
