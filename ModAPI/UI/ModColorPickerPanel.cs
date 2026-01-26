using System;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Spine.UI;
using ModAPI.Core;

namespace ModAPI.UI
{
    // [LOCKED OFF] - This feature has been intentionally disabled.
    public class ModColorPickerPanel : MonoBehaviour
    {
        private static void Log(string msg) { MMLog.Write($"[ModColorPicker] {msg}"); }
        
        private static GameObject _instance;
        private Action<Color> _onChanged;
        private Color _originalColor;
        private Color _currentColor;
        
        // --- Dimensions (Compact) ---
        private const int SV_SIZE = 180;
        private const int BAR_WIDTH = 20;
        private const int BAR_HEIGHT = 180;
        private const int WINDOW_WIDTH = 550;
        private const int WINDOW_HEIGHT = 380;

        // --- Widgets ---
        private UITexture _svBox;
        private UITexture _svKnob;
        private Texture2D _svTexture;

        private UITexture _hueBar;
        private UITexture _hueKnob;
        private Texture2D _hueTexture;

        private UITexture _alphaBar;
        private UITexture _alphaKnob;
        private Texture2D _alphaTexture;

        private UITexture _previewCurrInfo;
        private UITexture _previewOrigInfo;

        // Inputs
        private UIInput _inputHex;
        private UIInput _inputR, _inputG, _inputB, _inputA; 
        private UIInput _inputH, _inputS, _inputV; 

        private GameObject _recentGridRoot;

        // --- Data ---
        private float _h, _s, _v, _a;
        private bool _ignoreInputEvents = false; 

        private static List<Color> _savedRecentColors = new List<Color>();

        public static void Show(Color initial, Action<Color> onChanged)
        {
            Log($"Show called. Initial Color: {initial}");
            if (_instance != null) Destroy(_instance);
            
            // Use a specific depth that is definitely above the settings panel (11000)
            var panel = UIUtil.EnsureOverlayPanel("ModAPI_ColorPicker", 13000);
            var go = new GameObject("ColorPicker_Root");
            go.transform.SetParent(panel.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            go.layer = panel.gameObject.layer;
            
            _instance = go;
            
            var script = go.AddComponent<ModColorPickerPanel>();
            script._originalColor = initial;
            script._currentColor = initial;
            script._onChanged = onChanged;
            
            Color.RGBToHSV(initial, out script._h, out script._s, out script._v);
            script._a = initial.a;
            
            script.Initialise();
        }

        private void Initialise()
        {
            Log("Initialising UI...");
            
            // 1. Blocker (Screen fill)
            var blocker = new GameObject("Blocker");
            blocker.transform.parent = transform;
            blocker.transform.localPosition = Vector3.zero;
            blocker.layer = gameObject.layer;
            
            var bTex = blocker.AddComponent<UITexture>();
            bTex.mainTexture = UIUtil.WhiteTexture;
            bTex.width = 5000; bTex.height = 5000;
            bTex.color = new Color(0, 0, 0, 0.5f);
            bTex.depth = 12900; // Explicit high depth, just below panel(13000) + local(0)
            
            // "Unlit/Transparent Colored" was rendering opaque white. Switching to Sprites/Default for reliable tinting.
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) bTex.shader = shader;
            else 
            {
                // Fallback if Sprites/Default is missing (rare in 5.x)
                shader = Shader.Find("Transparent/Diffuse");
                if (shader != null) bTex.shader = shader;
            }
            
            Log($"[Debug] Blocker created. Shader: {(shader ? shader.name : "NULL")}, Depth: {bTex.depth}");

            var bCol = blocker.AddComponent<BoxCollider>();
            bCol.size = new Vector3(5000, 5000, 1);
            UIEventListener.Get(blocker).onClick += (g) => Close();

            // 2. Window Background
            // We want this at depth ~13000
            var bgGO = UIUtil.CreatePanelBackground(gameObject, WINDOW_WIDTH, WINDOW_HEIGHT);
            bgGO.transform.localPosition = Vector3.zero;
            var bgTex = bgGO.GetComponent<UITexture>();
            if (bgTex != null) 
            {
                bgTex.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                bgTex.depth = 13000; 
                Log($"[Debug] Background created. Depth: {bgTex.depth}");
            }

            // 3. Title
            var titleLbl = UIUtil.CreateLabelQuick(gameObject, "COLOR PICKER", 16, new Vector3(0, (WINDOW_HEIGHT / 2) - 25, 0));
            titleLbl.alignment = NGUIText.Alignment.Center;
            titleLbl.depth = 13050;

            // 4. Layout Roots
            var pickerRoot = new GameObject("Pickers");
            pickerRoot.transform.parent = transform;
            pickerRoot.transform.localPosition = new Vector3(-130, 20, 0); 
            
            CreateSVBox(pickerRoot);
            CreateHueBar(pickerRoot);
            CreateAlphaBar(pickerRoot);

            var dataRoot = new GameObject("Data");
            dataRoot.transform.parent = transform;
            dataRoot.transform.localPosition = new Vector3(120, 0, 0); 
            
            CreatePreviewSection(dataRoot);
            CreateRecentSection(dataRoot);
            CreateInputSection(dataRoot);

            // 5. Buttons
            var btnDone = UIUtil.CreateButton(gameObject, SpineWidgetFactory.ButtonTemplate, "DONE", 90, 35, new Vector3(110, -(WINDOW_HEIGHT/2) + 40, 0), () => {
                CommitColor();
                Close();
            });
            if(btnDone) SetButtonDepth(btnDone, 13100);
            
            var btnCancel = UIUtil.CreateButton(gameObject, SpineWidgetFactory.ButtonTemplate, "CANCEL", 90, 35, new Vector3(210, -(WINDOW_HEIGHT/2) + 40, 0), () => {
                _onChanged?.Invoke(_originalColor);
                Close();
            });
            if(btnCancel) SetButtonDepth(btnCancel, 13100);

            UpdateVisuals();
            UpdateInputs(); 
        }

        private void Close()
        {
            Log("Closing.");
            if (_svTexture) Destroy(_svTexture);
            if (_hueTexture) Destroy(_hueTexture);
            if (_alphaTexture) Destroy(_alphaTexture);
            
            Destroy(gameObject);
            _instance = null;
        }

        private void CommitColor()
        {
            Log($"CommitColor called. Value: {_currentColor}");
            bool exists = false;
            foreach(var c in _savedRecentColors) if(c == _currentColor) { exists = true; break; }
            if (!exists)
            {
                _savedRecentColors.Insert(0, _currentColor);
                if (_savedRecentColors.Count > 14) _savedRecentColors.RemoveAt(14);
            }
            _onChanged?.Invoke(_currentColor);
        }

        private void CreateSVBox(GameObject parent)
        {
            var root = new GameObject("SVBox");
            root.transform.parent = parent.transform;
            root.transform.localPosition = new Vector3(-40, 0, 0); 

            _svTexture = new Texture2D(32, 32, TextureFormat.RGB24, false);
            _svTexture.filterMode = FilterMode.Bilinear;
            _svTexture.wrapMode = TextureWrapMode.Clamp;
            
            _svBox = root.AddComponent<UITexture>();
            _svBox.mainTexture = _svTexture;
            _svBox.width = SV_SIZE; 
            _svBox.height = SV_SIZE;
            _svBox.depth = 13100;

            root.AddComponent<BoxCollider>().size = new Vector3(SV_SIZE, SV_SIZE, 1);
            var listener = UIEventListener.Get(root);
            listener.onDrag += (go, delta) => OnSVInput();
            listener.onPress += (go, pressed) => { if(pressed) OnSVInput(); };

            _svKnob = UIUtil.CreateFlatTexture(root, 10, 10, Color.white);
            _svKnob.depth = 13110;
            var outline = UIUtil.CreateFlatTexture(_svKnob.gameObject, 12, 12, Color.black);
            outline.depth = 13109;
        }

        private void CreateHueBar(GameObject parent)
        {
            var root = new GameObject("HueBar");
            root.transform.parent = parent.transform;
            root.transform.localPosition = new Vector3(80, 0, 0);

            _hueTexture = new Texture2D(1, 128, TextureFormat.RGB24, false);
            for (int y = 0; y < 128; y++)
                _hueTexture.SetPixel(0, y, Color.HSVToRGB((float)y/127f, 1, 1));
            _hueTexture.Apply();

            _hueBar = root.AddComponent<UITexture>();
            _hueBar.mainTexture = _hueTexture;
            _hueBar.width = BAR_WIDTH;
            _hueBar.height = BAR_HEIGHT;
            _hueBar.depth = 13100;

            root.AddComponent<BoxCollider>().size = new Vector3(BAR_WIDTH, BAR_HEIGHT, 1);
            var listener = UIEventListener.Get(root);
            listener.onDrag += (go, delta) => OnHueInput();
            listener.onPress += (go, pressed) => { if(pressed) OnHueInput(); };

            _hueKnob = UIUtil.CreateFlatTexture(root, BAR_WIDTH + 4, 4, Color.black);
            _hueKnob.depth = 13110;
        }

        private void CreateAlphaBar(GameObject parent)
        {
            var root = new GameObject("AlphaBar");
            root.transform.parent = parent.transform;
            root.transform.localPosition = new Vector3(115, 0, 0);

            _alphaTexture = new Texture2D(1, 32, TextureFormat.RGBA32, false);
            _alphaBar = root.AddComponent<UITexture>();
            _alphaBar.mainTexture = _alphaTexture;
            _alphaBar.width = BAR_WIDTH;
            _alphaBar.height = BAR_HEIGHT;
            _alphaBar.depth = 13100;

            var bgCheck = UIUtil.CreateFlatTexture(root, BAR_WIDTH, BAR_HEIGHT, Color.gray);
            bgCheck.depth = 13099;

            root.AddComponent<BoxCollider>().size = new Vector3(BAR_WIDTH, BAR_HEIGHT, 1);
            var listener = UIEventListener.Get(root);
            listener.onDrag += (go, delta) => OnAlphaInput();
            listener.onPress += (go, pressed) => { if(pressed) OnAlphaInput(); };

            _alphaKnob = UIUtil.CreateFlatTexture(root, BAR_WIDTH + 4, 4, Color.white);
            _alphaKnob.depth = 13110;
        }

        private void CreatePreviewSection(GameObject parent)
        {
            var root = new GameObject("Preview");
            root.transform.parent = parent.transform;
            root.transform.localPosition = new Vector3(0, 95, 0);
            
            _previewCurrInfo = UIUtil.CreateFlatTexture(root, 70, 50, _currentColor);
            _previewCurrInfo.transform.localPosition = new Vector3(-40, 0, 0);
            _previewCurrInfo.depth = 13100;
            var l1 = UIUtil.CreateLabelQuick(_previewCurrInfo.gameObject, "NEW", 10, new Vector3(0, 30, 0));
            l1.alignment = NGUIText.Alignment.Center; l1.depth = 13105;

            _previewOrigInfo = UIUtil.CreateFlatTexture(root, 70, 50, _originalColor);
            _previewOrigInfo.transform.localPosition = new Vector3(40, 0, 0);
            _previewOrigInfo.depth = 13100;
            var l2 = UIUtil.CreateLabelQuick(_previewOrigInfo.gameObject, "OLD", 10, new Vector3(0, 30, 0));
            l2.alignment = NGUIText.Alignment.Center; l2.depth = 13105;

            var box = _previewOrigInfo.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(70, 50, 1);
            UIEventListener.Get(_previewOrigInfo.gameObject).onClick += (g) => {
                _currentColor = _originalColor;
                Color.RGBToHSV(_currentColor, out _h, out _s, out _v);
                _a = _currentColor.a;
                _ignoreInputEvents = true; UpdateVisuals(); UpdateInputs(); _ignoreInputEvents = false;
            };
        }

        private void CreateRecentSection(GameObject parent)
        {
            _recentGridRoot = new GameObject("RecentGrid");
            _recentGridRoot.transform.parent = parent.transform;
            _recentGridRoot.transform.localPosition = new Vector3(0, 40, 0);
            RefreshRecentGrid();
        }

        private void RefreshRecentGrid()
        {
            foreach (Transform t in _recentGridRoot.transform) Destroy(t.gameObject);
            
            float startX = -75;
            float startY = 0;
            float size = 18;
            float pad = 4;
            int cols = 7;

            for (int i = 0; i < _savedRecentColors.Count && i < 14; i++)
            {
                int r = i / cols;
                int c = i % cols;
                
                Color col = _savedRecentColors[i];
                var swatch = UIUtil.CreateFlatTexture(_recentGridRoot, (int)size, (int)size, col);
                swatch.transform.localPosition = new Vector3(startX + (c * (size+pad)), startY - (r * (size+pad)), 0);
                swatch.depth = 13100;
                
                swatch.gameObject.AddComponent<BoxCollider>().size = new Vector3(size, size, 1);
                UIEventListener.Get(swatch.gameObject).onClick += (g) => {
                    _currentColor = col;
                    Color.RGBToHSV(_currentColor, out _h, out _s, out _v);
                    _a = _currentColor.a;
                    _ignoreInputEvents = true; UpdateVisuals(); UpdateInputs(); _ignoreInputEvents = false;
                };
            }
        }

        private void CreateInputSection(GameObject parent)
        {
            var root = new GameObject("Inputs");
            root.transform.parent = parent.transform;
            root.transform.localPosition = new Vector3(0, -65, 0);
            
            var lR = UIUtil.CreateLabelQuick(root, "R", 12, new Vector3(-80, 50, 0)); lR.depth = 13100;
            var lG = UIUtil.CreateLabelQuick(root, "G", 12, new Vector3(-30, 50, 0)); lG.depth = 13100;
            var lB = UIUtil.CreateLabelQuick(root, "B", 12, new Vector3(20, 50, 0));  lB.depth = 13100;
            var lA = UIUtil.CreateLabelQuick(root, "A", 12, new Vector3(70, 50, 0));  lA.depth = 13100;

            var lH = UIUtil.CreateLabelQuick(root, "H", 12, new Vector3(-80, 5, 0));  lH.depth = 13100;
            var lS = UIUtil.CreateLabelQuick(root, "S", 12, new Vector3(-30, 5, 0));  lS.depth = 13100;
            var lV = UIUtil.CreateLabelQuick(root, "V", 12, new Vector3(20, 5, 0));   lV.depth = 13100;

            var lHex = UIUtil.CreateLabelQuick(root, "HEX", 12, new Vector3(-40, -40, 0)); lHex.depth = 13100;

            _inputR = CreateNumInput(root, -80, 30, (v) => SetRGB(v, -1,-1));
            _inputG = CreateNumInput(root, -30, 30, (v) => SetRGB(-1, v,-1));
            _inputB = CreateNumInput(root, 20, 30, (v) => SetRGB(-1,-1, v));
            _inputA = CreateNumInput(root, 70, 30, (v) => { _a = Mathf.Clamp01(v/255f); SyncAndRefresh(); });

            _inputH = CreateNumInput(root, -80, -15, (v) => SetHSV(v, -1,-1));
            _inputS = CreateNumInput(root, -30, -15, (v) => SetHSV(-1, v,-1));
            _inputV = CreateNumInput(root, 20, -15, (v) => SetHSV(-1,-1, v));

            _inputHex = CreateTextInput(root, 40, -40, 100, (s) => {
                if (ColorUtility.TryParseHtmlString("#" + s.TrimStart('#'), out Color c))
                {
                    _currentColor = c;
                    Color.RGBToHSV(c, out _h, out _s, out _v);
                    _a = c.a;
                    SyncAndRefresh();
                }
            });
        }

        private UIInput CreateNumInput(GameObject parent, float x, float y, Action<float> onSubmit)
        {
            return CreateTextInput(parent, x, y, 40, (str) => {
                if (float.TryParse(str, out float res)) onSubmit(res);
            });
        }

        private UIInput CreateTextInput(GameObject parent, float x, float y, int w, Action<string> onSubmit)
        {
            var go = new GameObject("Input" + x + y);
            go.transform.parent = parent.transform;
            go.transform.localPosition = new Vector3(x, y, 0);
            
            var bg = UIUtil.CreateFlatTexture(go, w, 22, new Color(0.2f, 0.2f, 0.2f));
            bg.depth = 13100;

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(w, 22, 1);

            var lbl = UIUtil.CreateLabelQuick(go, "0", 14, Vector3.zero);
            lbl.pivot = UIWidget.Pivot.Center;
            lbl.depth = 13105;
            lbl.effectStyle = UILabel.Effect.None; // Ensure clarity

            var inp = go.AddComponent<UIInput>();
            inp.label = lbl;
            EventDelegate.Add(inp.onChange, () => {
                if (!_ignoreInputEvents) onSubmit(inp.value);
            });
            
            return inp;
        }

        private void SetRGB(float r, float g, float b)
        {
            if (r >= 0) _currentColor.r = Mathf.Clamp01(r/255f);
            if (g >= 0) _currentColor.g = Mathf.Clamp01(g/255f);
            if (b >= 0) _currentColor.b = Mathf.Clamp01(b/255f);
            Color.RGBToHSV(_currentColor, out _h, out _s, out _v);
            _currentColor.a = _a;
            SyncAndRefresh();
        }

        private void SetHSV(float h, float s, float v)
        {
            if (h >= 0) _h = Mathf.Clamp01(h/360f);
            if (s >= 0) _s = Mathf.Clamp01(s/100f);
            if (v >= 0) _v = Mathf.Clamp01(v/100f);
            _currentColor = Color.HSVToRGB(_h, _s, _v);
            _currentColor.a = _a;
            SyncAndRefresh();
        }

        private void SyncAndRefresh()
        {
             _ignoreInputEvents = true;
             UpdateVisuals();
             UpdateInputs();
             _ignoreInputEvents = false;
        }

        private void OnSVInput()
        {
            Vector3 pos = _svBox.transform.InverseTransformPoint(UICamera.lastHit.point);
            float halfW = _svBox.width / 2f;
            float halfH = _svBox.height / 2f;
            
            _s = Mathf.Clamp01((pos.x + halfW) / _svBox.width);
            _v = Mathf.Clamp01((pos.y + halfH) / _svBox.height);
            _currentColor = Color.HSVToRGB(_h, _s, _v);
            _currentColor.a = _a;
            SyncAndRefresh();
        }

        private void OnHueInput()
        {
            Vector3 pos = _hueBar.transform.InverseTransformPoint(UICamera.lastHit.point);
            float halfH = _hueBar.height / 2f;
            _h = Mathf.Clamp01((pos.y + halfH) / _hueBar.height);
            _currentColor = Color.HSVToRGB(_h, _s, _v);
            _currentColor.a = _a;
            SyncAndRefresh();
        }

        private void OnAlphaInput()
        {
            Vector3 pos = _alphaBar.transform.InverseTransformPoint(UICamera.lastHit.point);
            float halfH = _alphaBar.height / 2f;
            _a = Mathf.Clamp01((pos.y + halfH) / _alphaBar.height);
            _currentColor.a = _a;
            SyncAndRefresh();
        }

        private Color _lastHue = Color.clear;
        
        private void UpdateVisuals()
        {
            _currentColor = Color.HSVToRGB(_h, _s, _v);
            _currentColor.a = _a;
            Color hueColor = Color.HSVToRGB(_h, 1, 1);

            if (hueColor != _lastHue)
            {
                _lastHue = hueColor;
                int s = 32;
                Color[] cols = new Color[s*s];
                for(int y=0; y<s; y++) {
                    float v = (float)y/(s-1);
                    for(int x=0; x<s; x++) {
                        float sat = (float)x/(s-1);
                        Color c = Color.Lerp(Color.white, hueColor, sat) * v;
                        c.a = 1f;
                        cols[y*s + x] = c;
                    }
                }
                _svTexture.SetPixels(cols);
                _svTexture.Apply();
            }

            int ah = 32;
            Color baseCol = _currentColor; baseCol.a = 1f;
            for(int i=0; i<ah; i++) {
                Color c = baseCol;
                c.a = (float)i/(ah-1);
                _alphaTexture.SetPixel(0, i, c);
            }
            _alphaTexture.Apply();

            _svKnob.transform.localPosition = new Vector3((_s * SV_SIZE) - (SV_SIZE/2), (_v * SV_SIZE) - (SV_SIZE/2), 0);
            _hueKnob.transform.localPosition = new Vector3(0, (_h * BAR_HEIGHT) - (BAR_HEIGHT/2), 0);
            _alphaKnob.transform.localPosition = new Vector3(0, (_a * BAR_HEIGHT) - (BAR_HEIGHT/2), 0);

            if (_previewCurrInfo) _previewCurrInfo.color = _currentColor;
            
            _onChanged?.Invoke(_currentColor);
        }

        private void UpdateInputs()
        {
            if (_inputR) _inputR.value = Mathf.RoundToInt(_currentColor.r * 255).ToString();
            if (_inputG) _inputG.value = Mathf.RoundToInt(_currentColor.g * 255).ToString();
            if (_inputB) _inputB.value = Mathf.RoundToInt(_currentColor.b * 255).ToString();
            if (_inputA) _inputA.value = Mathf.RoundToInt(_currentColor.a * 255).ToString();

            if (_inputH) _inputH.value = Mathf.RoundToInt(_h * 360).ToString();
            if (_inputS) _inputS.value = Mathf.RoundToInt(_s * 100).ToString();
            if (_inputV) _inputV.value = Mathf.RoundToInt(_v * 100).ToString();

            if (_inputHex) _inputHex.value = ColorUtility.ToHtmlStringRGBA(_currentColor);
        }
        private void SetButtonDepth(UIButton btn, int depth)
        {
            if (btn == null) return;
            foreach (var w in btn.GetComponentsInChildren<UIWidget>(true))
            {
                w.depth = depth + (w.depth % 10);
            }
        }
    }
}
