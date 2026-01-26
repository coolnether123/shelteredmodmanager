using UnityEngine;
using ModAPI.Core;

namespace ModAPI.UI
{
    public class ModTooltip : MonoBehaviour
    {
        private static ModTooltip _instance;
        private GameObject _root;
        private UILabel _label;
        private UITexture _bg;
        private Camera _uiCamera;

        public static void Show(string text)
        {
            if (_instance == null) CreateInstance();
            _instance.SetText(text);
        }

        public static void Hide()
        {
            if (_instance != null) _instance.SetText(null);
        }

        private static void CreateInstance()
        {
            var go = new GameObject("ModAPI_Tooltip");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ModTooltip>();
        }

        private void Awake()
        {
            _uiCamera = NGUITools.FindCameraForLayer(gameObject.layer); // Usually finds UI Root camera

            // Create Visuals - High Depth
            var panel = gameObject.AddComponent<UIPanel>();
            panel.depth = 20000; // Above absolutely everything

            _bg = NGUITools.AddWidget<UITexture>(gameObject);
            _bg.material = new Material(Shader.Find("Unlit/Transparent Colored"));
            _bg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            _bg.depth = 1;

            _label = NGUITools.AddWidget<UILabel>(gameObject);
            _label.depth = 2;
            _label.fontSize = 16;
            _label.color = new Color(1f, 1f, 0.8f); // Slight yellow tint
            
            // Try to find a font
            var fonts = UIFontCache.GetFonts();
            if (fonts.Bitmap != null) _label.bitmapFont = fonts.Bitmap;
            else if (fonts.TTF != null) _label.trueTypeFont = fonts.TTF;

            _label.overflowMethod = UILabel.Overflow.ResizeFreely;
            
            gameObject.SetActive(false);
        }

        private void SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            _label.text = text;
            
            // Resize BG to fit text with padding
            int padding = 10;
            int width = _label.width + (padding * 2);
            int height = _label.height + (padding * 2);
            
            // Cap max width
            if (width > 400)
            {
                 _label.width = 400;
                 _label.overflowMethod = UILabel.Overflow.ResizeHeight;
                 width = 420;
                 height = _label.height + (padding * 2);
            }
            
            _bg.width = width;
            _bg.height = height;
            
            // Center label on BG could work, or just offset.
            // Let's assume Pivot Center for both simplifies things.
            _bg.pivot = UIWidget.Pivot.TopLeft;
            _label.pivot = UIWidget.Pivot.TopLeft;
            
             // Offset label slightly inside
            _label.transform.localPosition = new Vector3(padding, -padding, 0);

            UpdatePosition();
        }

        private void Update()
        {
            if (gameObject.activeSelf) UpdatePosition();
        }

        private void UpdatePosition()
        {
            // Follow mouse
            Vector3 mousePos = Input.mousePosition;
            
            // NGUI Coordinate conversion
            // Standard NGUI tooltips usually attach to UICamera logic, but we are doing a manual overlay.
            
            // Simple approach: Screen To World Point on the UI Plane
            if (_uiCamera == null) _uiCamera = NGUITools.FindCameraForLayer(gameObject.layer);
            if (_uiCamera == null) return;

            Vector3 worldPos = _uiCamera.ScreenToWorldPoint(mousePos);
            transform.position = worldPos;

            // Offset so cursor doesn't cover it
            transform.localPosition += new Vector3(15, -15, 0);
            
            // Screen edge clamping (simple version)
            // If x + width > ScreenWidth/2 ... (todo if needed)
        }
    }
}
