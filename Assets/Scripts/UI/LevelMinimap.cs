using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MedusaHorror.Player;
using MedusaHorror.Gameplay;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 通用关卡小地图：右下角俯视跟随，固定北向。
    /// 显示玩家朝向箭头、传送门（绿）与怪物（红）图标，建筑由俯视渲染自然呈现。
    /// M 键折叠。W1 医院继续用专用的 MinimapRig，本组件不接管。
    /// </summary>
    public class LevelMinimap : MonoBehaviour
    {
        static LevelMinimap _inst;
        public static LevelMinimap Instance { get { return _inst; } }

        public static LevelMinimap Ensure(string sceneName)
        {
            bool explore = sceneName == "W2_Wasteland" || sceneName == "W3_Subway"
                        || sceneName == "W4_NeonStreet" || sceneName == "W5_AlienGate";
            if (!explore)
            {
                // 主菜单/基地/医院：收起小地图，避免残留面板
                if (_inst != null) { _inst._available = false; _inst.SetShown(false); }
                return null;
            }
            if (_inst != null) { _inst._available = true; _inst.SetShown(true); _inst.SetScene(sceneName); return _inst; }
            var go = new GameObject("LevelMinimapRuntime");
            DontDestroyOnLoad(go);
            var mm = go.AddComponent<LevelMinimap>();
            mm.SetScene(sceneName);
            return mm;
        }

        public void SetShown(bool on)
        {
            if (on && !_visible) _renderTimer = 0f;
            _visible = on;
            if (_panel != null) _panel.gameObject.SetActive(on);
        }

        const float ViewSize = 208f;
        const float PanelPad = 10f;
        const float IconSize = 11f;

        string _scene = "";
        Camera _cam;
        RenderTexture _rt;
        RectTransform _panel;
        RectTransform _viewRt;
        RectTransform _iconRoot;
        RectTransform _arrow;
        RawImage _view;
        Text _title;

        Transform _player;
        Vector3 _playerPos;
        Vector3 _mapCenter;
        float _renderTimer;
        const float MapRefreshInterval = 0.125f;

        float _ortho = 45f;
        float _camY = 45f;
        bool _absCamY;
        bool _visible = true;
        bool _available = true;

        float _iconTimer;
        float _scanTimer;

        class MapIcon { public Transform target; public Image img; public bool isExit; }
        readonly List<MapIcon> _icons = new List<MapIcon>();
        readonly List<ExitPortal> _portals = new List<ExitPortal>();
        readonly List<MedusaGaze> _gazes = new List<MedusaGaze>();

        static Sprite _dot;
        static Sprite _tri;

        bool _fogSaved;
        bool _savedFog;
        UnityEngine.Rendering.AmbientMode _savedAmbMode;
        Color _savedAmb;

        void Awake()
        {
            if (_inst != null && _inst != this) { Destroy(gameObject); return; }
            _inst = this;
            BuildUi();
            BuildCamera();
        }

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }

        void OnEnable() { Camera.onPreRender += PreRender; Camera.onPostRender += PostRender; }
        void OnDisable()
        {
            PostRender(_cam);
            Camera.onPreRender -= PreRender;
            Camera.onPostRender -= PostRender;
        }

        void SetScene(string s)
        {
            _scene = s;
            _player = null;
            _renderTimer = 0f;
            switch (s)
            {
                case "W3_Subway": _ortho = 19f; _absCamY = true; _camY = 3.75f; break;
                case "W4_NeonStreet": _ortho = 48f; _absCamY = false; _camY = 45f; break;
                case "W5_AlienGate": _ortho = 50f; _absCamY = false; _camY = 45f; break;
                case "W2_Wasteland": _ortho = 42f; _absCamY = false; _camY = 42f; break;
                default: _ortho = 45f; _absCamY = false; _camY = 45f; break;
            }
            if (_cam != null) _cam.orthographicSize = _ortho;
            _iconTimer = 0f;
            _scanTimer = 0f;
            if (_title != null) _title.text = SceneLabel(s);
        }

        static string SceneLabel(string s)
        {
            switch (s)
            {
                case "W2_Wasteland": return "荒废村庄";
                case "W3_Subway": return "地下铁";
                case "W4_NeonStreet": return "霓虹街区";
                case "W5_AlienGate": return "外星星门";
                default: return "地图";
            }
        }

        // ------------------------------------------------------------------ UI
        void BuildUi()
        {
            var canvasGo = new GameObject("MinimapCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 118;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelGo = new GameObject("MinimapPanel", typeof(RectTransform));
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.SetParent(canvasGo.transform, false);
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.sizeDelta = new Vector2(ViewSize + PanelPad * 2f, ViewSize + PanelPad * 2f + 26f);
            _panel.anchoredPosition = new Vector2(24f, -24f);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);
            bg.raycastTarget = false;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.SetParent(_panel, false);
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 24f);
            titleRt.anchoredPosition = new Vector2(0f, -3f);
            _title = titleGo.AddComponent<Text>();
            _title.font = CjkFont.Get();
            _title.fontSize = 15;
            _title.alignment = TextAnchor.MiddleCenter;
            _title.color = new Color(0.88f, 0.92f, 0.98f, 1f);
            _title.text = "地图";
            _title.raycastTarget = false;

            var viewGo = new GameObject("View", typeof(RectTransform));
            _viewRt = viewGo.GetComponent<RectTransform>();
            _viewRt.SetParent(_panel, false);
            _viewRt.anchorMin = new Vector2(0.5f, 0f);
            _viewRt.anchorMax = new Vector2(0.5f, 0f);
            _viewRt.pivot = new Vector2(0.5f, 0f);
            _viewRt.sizeDelta = new Vector2(ViewSize, ViewSize);
            _viewRt.anchoredPosition = new Vector2(0f, PanelPad);
            _view = viewGo.AddComponent<RawImage>();
            _view.raycastTarget = false;
            _view.color = Color.white;

            var iconGo = new GameObject("Icons", typeof(RectTransform));
            _iconRoot = iconGo.GetComponent<RectTransform>();
            _iconRoot.SetParent(_viewRt, false);
            _iconRoot.anchorMin = Vector2.zero;
            _iconRoot.anchorMax = Vector2.one;
            _iconRoot.offsetMin = Vector2.zero;
            _iconRoot.offsetMax = Vector2.zero;

            var arrowGo = new GameObject("PlayerArrow", typeof(RectTransform));
            _arrow = arrowGo.GetComponent<RectTransform>();
            _arrow.SetParent(_viewRt, false);
            _arrow.anchorMin = new Vector2(0.5f, 0.5f);
            _arrow.anchorMax = new Vector2(0.5f, 0.5f);
            _arrow.pivot = new Vector2(0.5f, 0.5f);
            _arrow.sizeDelta = new Vector2(16f, 16f);
            _arrow.anchoredPosition = Vector2.zero;
            var arrowImg = arrowGo.AddComponent<Image>();
            arrowImg.sprite = TriSprite();
            arrowImg.color = new Color(0.98f, 0.95f, 0.45f, 1f);
            arrowImg.raycastTarget = false;
        }

        void BuildCamera()
        {
            var camGo = new GameObject("MinimapCam");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            // Render the cached map explicitly, rather than drawing the city every frame.
            _cam.enabled = false;
            _cam.orthographic = true;
            _cam.orthographicSize = _ortho;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.depth = -50f;
            _cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _rt = new RenderTexture(288, 288, 16, RenderTextureFormat.ARGB32);
            _rt.name = "LevelMinimapRT";
            _rt.Create();
            _cam.targetTexture = _rt;
            _view.texture = _rt;
        }

        void PreRender(Camera c)
        {
            if (c != _cam || _fogSaved) return;
            _savedFog = RenderSettings.fog;
            _savedAmbMode = RenderSettings.ambientMode;
            _savedAmb = RenderSettings.ambientLight;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.78f, 0.81f, 0.86f);
            _fogSaved = true;
        }

        void PostRender(Camera c)
        {
            if (c != _cam || !_fogSaved) return;
            RenderSettings.fog = _savedFog;
            RenderSettings.ambientMode = _savedAmbMode;
            RenderSettings.ambientLight = _savedAmb;
            _fogSaved = false;
        }

        void LateUpdate()
        {
            if (!_available) return;
            if (InteractionPanelInput.Pressed(KeyCode.M)) SetShown(!_visible);
            if (!_visible) return;
            if (_player == null) AcquirePlayer();
            if (_player != null)
            {
                _playerPos = _player.position;
                _arrow.localRotation = Quaternion.Euler(0f, 0f, -_player.eulerAngles.y);
                _renderTimer -= Time.unscaledDeltaTime;
                if (_renderTimer <= 0f)
                {
                    _renderTimer = MapRefreshInterval;
                    _mapCenter = _playerPos;
                    float y = _absCamY ? _camY : _playerPos.y + _camY;
                    _cam.transform.position = new Vector3(_mapCenter.x, y, _mapCenter.z);
                    try { _cam.Render(); }
                    finally { PostRender(_cam); }
                }
                // Icons and the player arrow follow every frame over the cached image.
                float ppm = ViewSize / (_ortho * 2f);
                _arrow.anchoredPosition = new Vector2((_playerPos.x - _mapCenter.x) * ppm,
                    (_playerPos.z - _mapCenter.z) * ppm);
            }

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f) { _scanTimer = 1.5f; ScanTargets(); }

            _iconTimer -= Time.deltaTime;
            if (_iconTimer <= 0f) { _iconTimer = 0.4f; SyncIcons(); }

            UpdateIconPositions();

        }

        void AcquirePlayer()
        {
            var tp = FindObjectOfType<ThirdPersonController>();
            if (tp != null) _player = tp.transform;
        }

        void ScanTargets()
        {
            _portals.Clear();
            _gazes.Clear();
            var ps = FindObjectsOfType<ExitPortal>(true);
            for (int i = 0; i < ps.Length; i++) if (ps[i] != null && ps[i].gameObject.activeInHierarchy) _portals.Add(ps[i]);
            var gs = FindObjectsOfType<MedusaGaze>(true);
            for (int i = 0; i < gs.Length; i++) if (gs[i] != null && gs[i].gameObject.activeInHierarchy) _gazes.Add(gs[i]);
        }

        void SyncIcons()
        {
            int want = _portals.Count + _gazes.Count;
            if (want == _icons.Count)
            {
                bool same = true;
                int k = 0;
                for (int i = 0; i < _gazes.Count && same; i++, k++)
                    if (_icons[k].target != _gazes[i].transform || _icons[k].isExit) same = false;
                for (int i = 0; i < _portals.Count && same; i++, k++)
                    if (_icons[k].target != _portals[i].transform || !_icons[k].isExit) same = false;
                if (same) return;
            }

            for (int i = 0; i < _icons.Count; i++)
                if (_icons[i].img != null) Destroy(_icons[i].img.gameObject);
            _icons.Clear();

            for (int i = 0; i < _gazes.Count; i++) AddIcon(_gazes[i].transform, false);
            for (int i = 0; i < _portals.Count; i++) AddIcon(_portals[i].transform, true);
        }

        void AddIcon(Transform t, bool isExit)
        {
            var go = new GameObject(isExit ? "PortalIcon" : "EnemyIcon", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_iconRoot, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float sz = isExit ? IconSize * 1.5f : IconSize;
            rt.sizeDelta = new Vector2(sz, sz);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = DotSprite();
            img.color = isExit ? new Color(0.35f, 0.98f, 0.60f, 1f) : new Color(0.98f, 0.28f, 0.26f, 1f);
            img.raycastTarget = false;
            _icons.Add(new MapIcon { target = t, img = img, isExit = isExit });
        }

        void UpdateIconPositions()
        {
            if (_icons.Count == 0) return;
            float width = _viewRt.rect.width;
            if (width <= 1f) width = ViewSize;
            float ppm = width / (_ortho * 2f);
            float half = width * 0.5f - IconSize * 1.3f;

            for (int i = 0; i < _icons.Count; i++)
            {
                var ic = _icons[i];
                if (ic.img == null) continue;
                if (ic.target == null) { ic.img.enabled = false; continue; }
                Vector3 wp = ic.target.position;
                Vector2 p = new Vector2((wp.x - _mapCenter.x) * ppm, (wp.z - _mapCenter.z) * ppm);
                bool inRange = Mathf.Abs(p.x) <= half && Mathf.Abs(p.y) <= half;
                p.x = Mathf.Clamp(p.x, -half, half);
                p.y = Mathf.Clamp(p.y, -half, half);
                ic.img.rectTransform.anchoredPosition = p;
                ic.img.enabled = true;
                Color c = ic.img.color;
                c.a = inRange ? 1f : 0.55f;
                ic.img.color = c;
            }
        }

        public string IconReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("scene=").Append(_scene).Append(" portals=").Append(_portals.Count)
              .Append(" enemies=").Append(_gazes.Count).Append(" icons=").Append(_icons.Count)
              .Append(" player=").Append(_playerPos.ToString("F1")).AppendLine();
            for (int i = 0; i < _icons.Count; i++)
            {
                var ic = _icons[i];
                if (ic.target == null) { sb.Append("[").Append(i).Append("] null").AppendLine(); continue; }
                sb.Append("[").Append(i).Append("] ").Append(ic.isExit ? "PORTAL " : "ENEMY ")
                  .Append(ic.target.name).Append(" pos=").Append(ic.target.position.ToString("F1"))
                  .Append(" ui=").Append(ic.img != null ? ic.img.rectTransform.anchoredPosition.ToString("F1") : "-")
                  .AppendLine();
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 贴图
        static Sprite DotSprite()
        {
            if (_dot != null) return _dot;
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8f, 8f)) / 8f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? 1f : 0f));
                }
            tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            return _dot;
        }

        static Sprite TriSprite()
        {
            if (_tri != null) return _tri;
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float fx = (x + 0.5f) / 32f * 2f - 1f;
                    float fy = (y + 0.5f) / 32f;
                    bool inside = Mathf.Abs(fx) <= (1f - fy) * 0.95f && fy > 0.08f;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            tex.Apply();
            _tri = Sprite.Create(tex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f));
            return _tri;
        }
    }
}
