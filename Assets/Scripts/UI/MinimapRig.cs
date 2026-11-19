using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MedusaHorror.Player;
using MedusaHorror.Gameplay;

namespace MedusaHorror.UI
{
    /// <summary>
    /// W1_Hospital 小地图。固定北向（地图不旋转，只旋转玩家箭头），按玩家当前楼层自动切换俯视高度。
    /// 楼层高度与 HospitalBuilder 的 H=4.5 对齐：楼层 = floor((y + 0.5) / 4.5)，y 越界自动夹在 0~2。
    /// 快捷键 M 折叠/展开。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public class MinimapRig : MonoBehaviour
    {
        [Header("引用（留空自动查找）")]
        public Camera cam;
        public RenderTexture rt;
        public RawImage view;
        public RectTransform marker;
        public Text floorText;

        [Header("参数")]
        public float floorHeight = 4.5f;
        public float camHeight = 4.25f;
        public float orthoSize = 16f;
        public int playerLayer = 8;
        public KeyCode toggleKey = KeyCode.M;

        [Header("运行态（只读）")]
        public int currentFloor;
        public bool hasPlayer;
        public Vector3 playerPos;

        [Header("敌人 / 出口标记")]
        public Color enemyColor = new Color(0.95f, 0.25f, 0.25f, 1f);
        public Color exitColor = new Color(0.35f, 0.95f, 0.55f, 1f);
        public float iconSize = 12f;

        class MapIcon { public Transform target; public Image img; public bool isExit; }
        readonly List<MapIcon> icons = new List<MapIcon>();
        float iconRefresh;
        static Sprite dotSprite;

        Transform player;
        bool visible = true;
        int lastFloor = -999;

        void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>(true);
            if (rt == null) { rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32); rt.name = "MinimapRT_Runtime"; }
            if (rt != null && !rt.IsCreated()) rt.Create();
            if (view != null) view.texture = rt;
            ApplyCamera(CameraClearFlags.SolidColor);
        }

        void OnEnable() { Camera.onPreRender += CamPre; Camera.onPostRender += CamPost; }
        void OnDisable() { Camera.onPreRender -= CamPre; Camera.onPostRender -= CamPost; }

        // 小地图渲染瞬间临时换成纯环境光，避免场景点光/聚光造成的大片死黑与过曝，渲染完立即还原。
        bool ambientSwapped;
        void CamPre(Camera c)
        {
            if (c != cam) return;
            savedMode = RenderSettings.ambientMode;
            savedLight = RenderSettings.ambientLight;
            savedIntensity = RenderSettings.ambientIntensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.85f, 0.87f, 0.90f);
            RenderSettings.ambientIntensity = 1f;
            ambientSwapped = true;
        }
        void CamPost(Camera c)
        {
            if (c != cam || !ambientSwapped) return;
            RenderSettings.ambientMode = savedMode;
            RenderSettings.ambientLight = savedLight;
            RenderSettings.ambientIntensity = savedIntensity;
            ambientSwapped = false;
        }
        UnityEngine.Rendering.AmbientMode savedMode;
        Color savedLight;
        float savedIntensity;

        void ApplyCamera(CameraClearFlags clear)
        {
            if (cam == null) return;
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 8f;
            cam.clearFlags = clear;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.depth = -20;
            cam.cullingMask = ~(1 << playerLayer);
        }

        void LateUpdate()
        {
            if (player == null) AcquirePlayer();
            if (player == null || cam == null) return;

            playerPos = player.position;
            int floor = Mathf.Clamp(Mathf.FloorToInt((playerPos.y + 0.5f) / floorHeight), 0, 2);
            if (floor != lastFloor)
            {
                lastFloor = floor;
                currentFloor = floor;
                if (floorText != null) floorText.text = "F" + (floor + 1);
            }
            cam.transform.position = new Vector3(playerPos.x, floor * floorHeight + camHeight, playerPos.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (marker != null) marker.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);

            UpdateIcons();

            if (toggleKey != KeyCode.None && TogglePressed())
            {
                visible = !visible;
                if (view != null && view.transform.parent != null) view.transform.parent.gameObject.SetActive(visible);
            }
            hasPlayer = true;
        }

        static System.Reflection.PropertyInfo _kbCurrent;
        static System.Reflection.PropertyInfo _wasPressed;
        static bool _inputDone;

        static bool TogglePressed()
        {
            if (!_inputDone)
            {
                _inputDone = true;
                var t = System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
                if (t != null)
                {
                    _kbCurrent = t.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                }
            }
            if (_kbCurrent != null)
            {
                object kb = _kbCurrent.GetValue(null);
                if (kb == null) return false;
                if (_wasPressed == null)
                {
                    var mk = kb.GetType().GetProperty("mKey");
                    object key = mk != null ? mk.GetValue(kb) : null;
                    if (key != null) _wasPressed = key.GetType().GetProperty("wasPressedThisFrame");
                }
                if (_wasPressed == null) return false;
                object k2 = kb.GetType().GetProperty("mKey").GetValue(kb);
                return k2 != null && (bool)_wasPressed.GetValue(k2);
            }
            return Input.GetKeyDown(KeyCode.M);
        }

        static Sprite DotSprite()
        {
            if (dotSprite != null) return dotSprite;
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8f, 8f)) / 8f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? 1f : 0f));
                }
            tex.Apply();
            dotSprite = Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            return dotSprite;
        }

        void UpdateIcons()
        {
            if (view == null || view.rectTransform == null) return;

            iconRefresh -= Time.deltaTime;
            if (iconRefresh <= 0f)
            {
                iconRefresh = 0.5f;
                RebuildIcons();
            }

            float width = view.rectTransform.rect.width;
            if (width <= 1f) width = 264f;
            float pxPerMeter = width / (orthoSize * 2f);
            float half = width * 0.5f - iconSize * 1.6f;

            for (int i = icons.Count - 1; i >= 0; i--)
            {
                var ic = icons[i];
                if (ic.img == null) { icons.RemoveAt(i); continue; }
                if (ic.target == null) { Destroy(ic.img.gameObject); icons.RemoveAt(i); continue; }

                Vector3 wp = ic.target.position;
                bool sameFloor = Mathf.Abs(wp.y - playerPos.y) < floorHeight * 0.75f;

                ic.img.enabled = true;
                Vector2 p = new Vector2((wp.x - playerPos.x) * pxPerMeter, (wp.z - playerPos.z) * pxPerMeter);
                bool inRange = Mathf.Abs(p.x) <= half && Mathf.Abs(p.y) <= half;
                p.x = Mathf.Clamp(p.x, -half, half);
                p.y = Mathf.Clamp(p.y, -half, half);
                ic.img.rectTransform.anchoredPosition = p;

                Color c = ic.isExit ? exitColor : enemyColor;
                if (!sameFloor) c.a *= 0.60f;
                if (!inRange) c.a *= 0.75f;
                ic.img.color = c;
            }
        }

        void RebuildIcons()
        {
            var gazes = FindObjectsOfType<MedusaGaze>(true);
            var portals = FindObjectsOfType<ExitPortal>(true);

            bool same = gazes.Length + portals.Length == icons.Count;
            if (same)
            {
                int k = 0;
                for (int i = 0; i < gazes.Length && same; i++, k++)
                    if (gazes[i] == null || icons[k].target != gazes[i].transform || icons[k].isExit) same = false;
                for (int i = 0; i < portals.Length && same; i++, k++)
                    if (portals[i] == null || icons[k].target != portals[i].transform || !icons[k].isExit) same = false;
            }
            if (same) return;

            for (int i = 0; i < icons.Count; i++)
                if (icons[i].img != null) Destroy(icons[i].img.gameObject);
            icons.Clear();

            for (int i = 0; i < gazes.Length; i++)
                if (gazes[i] != null) AddIcon(gazes[i].transform, false);
            for (int i = 0; i < portals.Length; i++)
                if (portals[i] != null) AddIcon(portals[i].transform, true);
        }

        void AddIcon(Transform t, bool isExit)
        {
            var go = new GameObject(isExit ? "ExitIcon" : "EnemyIcon");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(view.rectTransform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(isExit ? iconSize * 1.25f : iconSize, isExit ? iconSize * 1.25f : iconSize);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = DotSprite();
            img.color = isExit ? exitColor : enemyColor;
            img.raycastTarget = false;
            icons.Add(new MapIcon { target = t, img = img, isExit = isExit });
        }

        void AcquirePlayer()
        {
            var tp = FindObjectOfType<ThirdPersonController>();
            Transform t = tp != null ? tp.transform : null;
            if (t == null)
            {
                var go = GameObject.Find("Kazuko_W1_Player");
                if (go != null) t = go.transform;
            }
            if (t == null) return;
            player = t;
            SetLayerRecursive(t.gameObject, playerLayer);
        }

        public string IconReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("icons=").Append(icons.Count).Append(" player=").Append(playerPos.ToString("F1")).AppendLine();
            for (int i = 0; i < icons.Count; i++)
            {
                var ic = icons[i];
                if (ic.target == null) { sb.Append("[").Append(i).Append("] null").AppendLine(); continue; }
                sb.Append("[").Append(i).Append("] ").Append(ic.isExit ? "EXIT " : "ENEMY ")
                  .Append(ic.target.name).Append(" pos=").Append(ic.target.position.ToString("F1"))
                  .Append(" enabled=").Append(ic.img != null && ic.img.enabled)
                  .Append(" ui=").Append(ic.img != null ? ic.img.rectTransform.anchoredPosition.ToString("F1") : "-")
                  .AppendLine();
            }
            return sb.ToString();
        }

        public static void SetLayerRecursive(GameObject g, int layer)
        {
            if (g == null || g.layer == layer) { if (g != null) { foreach (Transform c in g.transform) SetLayerRecursive(c.gameObject, layer); } return; }
            g.layer = layer;
            foreach (Transform c in g.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }
}
