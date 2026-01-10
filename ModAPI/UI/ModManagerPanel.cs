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

                MMLog.Write("[ModManagerPanel] Found scenario panel: " + scenarioPanel.name);
                
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
                        
                        // Set depth for background elements
                        var widgets = clone.GetComponentsInChildren<UIWidget>(true);
                        foreach (var w in widgets)
                        {
                            w.gameObject.layer = gameObject.layer;
                            w.depth = 10005; // Behind our content but above main menu
                        }
                        
                        clone.SetActive(true);
                        
                        // Log book bounds
                        var bookSprites = clone.GetComponentsInChildren<UISprite>(true);
                        if (bookSprites.Length > 0)
                        {
                            // Find largest sprite (the book background)
                            UISprite largestSprite = bookSprites[0];
                            int maxArea = largestSprite.width * largestSprite.height;
                            foreach (var s in bookSprites)
                            {
                                int area = s.width * s.height;
                                if (area > maxArea)
                                {
                                    maxArea = area;
                                    largestSprite = s;
                                }
                            }
                            
                            Vector3 pos = largestSprite.transform.localPosition;
                            float halfW = largestSprite.width * 0.5f;
                            float halfH = largestSprite.height * 0.5f;
                            
                            MMLog.Write("[ModManagerPanel] === BOOK BOUNDS ===");
                            MMLog.Write("[ModManagerPanel] Center: (" + pos.x + ", " + pos.y + ")");
                            MMLog.Write("[ModManagerPanel] Size: " + largestSprite.width + " x " + largestSprite.height);
                            MMLog.Write("[ModManagerPanel] Top-Left: (" + (pos.x - halfW) + ", " + (pos.y + halfH) + ")");
                            MMLog.Write("[ModManagerPanel] Top-Right: (" + (pos.x + halfW) + ", " + (pos.y + halfH) + ")");
                            MMLog.Write("[ModManagerPanel] Bottom-Left: (" + (pos.x - halfW) + ", " + (pos.y - halfH) + ")");
                            MMLog.Write("[ModManagerPanel] Bottom-Right: (" + (pos.x + halfW) + ", " + (pos.y - halfH) + ")");
                            MMLog.Write("[ModManagerPanel] Left Page Center: (" + (pos.x - halfW/2) + ", " + pos.y + ")");
                            MMLog.Write("[ModManagerPanel] Right Page Center: (" + (pos.x + halfW/2) + ", " + pos.y + ")");
                            MMLog.Write("[ModManagerPanel] ==================");
                        }
                        
                        return true; // Found and cloned book
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
                            MMLog.Write("[ModManagerPanel] Found back button at: " + defaultPos);
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
                
                // Hook up click event
                var capture = mod;
                UIEventListener.Get(btnGO).onClick = (g) => ShowDetails(capture);
                
                _modButtons.Add(btn);
                
                MMLog.Write("[ModManagerPanel] Mod button " + (i+1) + ": '" + mod.Name + "' at " + pos);
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
            
            // Description - on right page with proper wrapping
            // Note: TTF fonts need ClampContent to respect width boundaries
            _detailDescription = CreateSimpleLabel("", rightX, 80f, 32, textColor, NGUIText.Alignment.Left, 450);
            _detailDescription.overflowMethod = UILabel.Overflow.ClampContent;
            _detailDescription.maxLineCount = 0; // No line limit
            _detailDescription.multiLine = true; // Enable multi-line
            
            // CRITICAL: For TTF fonts, must set spacingX and spacingY explicitly
            _detailDescription.spacingX = 0;
            _detailDescription.spacingY = 0;
            
            // Re-set dimensions after font is assigned to ensure they stick
            _detailDescription.width = 450;
            _detailDescription.height = 350;
            
            MMLog.Write("[ModManagerPanel] Detail labels: Title(300,275) Version(300,230) Authors(300,195) Desc(300,80)");
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
            
            MMLog.Write("[ModManagerPanel] Back button at: " + backPos);
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
            if (mod == null) 
            {
                MMLog.WriteError("[ModManagerPanel] ShowDetails called with null mod!");
                return;
            }
            
            MMLog.Write("[ModManagerPanel] === ShowDetails START for: " + mod.Name + " ===");
            
            // Update title
            string titleText = mod.Name.ToUpper();
            _detailTitle.text = titleText;
            MMLog.Write("[ModManagerPanel] Title updated to: '" + titleText + "' (position: " + _detailTitle.transform.localPosition + ")");
            
            // Update version
            string versionText = "Version " + mod.Version;
            _detailVersion.text = versionText;
            MMLog.Write("[ModManagerPanel] Version updated to: '" + versionText + "' (position: " + _detailVersion.transform.localPosition + ")");
            
            // Update authors
            string authors = "Unknown";
            if (mod.About?.authors != null && mod.About.authors.Length > 0)
                authors = string.Join(", ", mod.About.authors);
            string authorsText = "By " + authors;
            _detailAuthors.text = authorsText;
            MMLog.Write("[ModManagerPanel] Authors updated to: '" + authorsText + "' (position: " + _detailAuthors.transform.localPosition + ")");
            
            // Update description
            string desc = "No description available.";
            if (mod.About != null && !string.IsNullOrEmpty(mod.About.description))
                desc = mod.About.description;
            _detailDescription.text = desc;
            MMLog.Write("[ModManagerPanel] Description updated to: '" + desc.Substring(0, Math.Min(50, desc.Length)) + "...' (position: " + _detailDescription.transform.localPosition + ")");
            
            // Force NGUI to update
            _detailTitle.ProcessText();
            _detailTitle.MarkAsChanged();
            MMLog.Write("[ModManagerPanel] Title ProcessText and MarkAsChanged called. Visible: " + _detailTitle.isVisible + ", Alpha: " + _detailTitle.alpha);
            
            _detailVersion.ProcessText();
            _detailVersion.MarkAsChanged();
            MMLog.Write("[ModManagerPanel] Version ProcessText and MarkAsChanged called. Visible: " + _detailVersion.isVisible + ", Alpha: " + _detailVersion.alpha);
            
            _detailAuthors.ProcessText();
            _detailAuthors.MarkAsChanged();
            MMLog.Write("[ModManagerPanel] Authors ProcessText and MarkAsChanged called. Visible: " + _detailAuthors.isVisible + ", Alpha: " + _detailAuthors.alpha);
            
            _detailDescription.ProcessText();
            _detailDescription.MarkAsChanged();
            
            // DETAILED DEBUGGING FOR DESCRIPTION
            MMLog.Write("[ModManagerPanel] === DESCRIPTION DEBUG ===");
            MMLog.Write("[ModManagerPanel] Text: '" + desc.Substring(0, Math.Min(50, desc.Length)) + "...'");
            MMLog.Write("[ModManagerPanel] Position: " + _detailDescription.transform.localPosition);
            MMLog.Write("[ModManagerPanel] Width: " + _detailDescription.width + " Height: " + _detailDescription.height);
            MMLog.Write("[ModManagerPanel] Line Width: " + _detailDescription.lineWidth + " Line Height: " + _detailDescription.lineHeight);
            MMLog.Write("[ModManagerPanel] Pivot: " + _detailDescription.pivot);
            MMLog.Write("[ModManagerPanel] FontSize: " + _detailDescription.fontSize + " DefaultFontSize: " + _detailDescription.defaultFontSize);
            MMLog.Write("[ModManagerPanel] Font: " + (_detailDescription.bitmapFont != null ? "Bitmap:" + _detailDescription.bitmapFont.name : "TTF:" + (_detailDescription.trueTypeFont != null ? _detailDescription.trueTypeFont.name : "NULL")));
            MMLog.Write("[ModManagerPanel] Overflow: " + _detailDescription.overflowMethod);
            MMLog.Write("[ModManagerPanel] ProcessedText Length: " + _detailDescription.processedText.Length);
            MMLog.Write("[ModManagerPanel] Visible: " + _detailDescription.isVisible + " Alpha: " + _detailDescription.alpha + " Depth: " + _detailDescription.depth);
            MMLog.Write("[ModManagerPanel] LocalSize: " + _detailDescription.localSize);
            MMLog.Write("[ModManagerPanel] ===========================");
            
            MMLog.Write("[ModManagerPanel] Description ProcessText and MarkAsChanged called. Visible: " + _detailDescription.isVisible + ", Alpha: " + _detailDescription.alpha);
            
            MMLog.Write("[ModManagerPanel] === ShowDetails END ===");
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
    }
}
