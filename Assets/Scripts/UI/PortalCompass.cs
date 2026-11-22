using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Gameplay;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 传送门导航罗盘：屏幕上方居中显示指向当前目标的箭头与距离。
    /// 目标取自 LevelObjective 最近的未达成点，箭头相对相机朝向旋转。
    /// </summary>
    public class PortalCompass : MonoBehaviour
    {
        static PortalCompass _inst;

        public static PortalCompass Ensure()
        {
            if (_inst != null) return _inst;
            var go = new GameObject("PortalCompassRuntime");
            DontDestroyOnLoad(go);
            return go.AddComponent<PortalCompass>();
        }

        const float NearHideDistance = 4.5f;

        RectTransform _root;
        RectTransform _arrow;
        Text _label;
        CanvasGroup _group;

        static Sprite _tri;

        void Awake()
        {
            if (_inst != null && _inst != this) { Destroy(gameObject); return; }
            _inst = this;
            BuildUi();
        }

        void OnDestroy() { if (_inst == this) _inst = null; }

        void BuildUi()
        {
            var canvasGo = new GameObject("CompassCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 122;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            var rootGo = new GameObject("Compass", typeof(RectTransform));
            _root = rootGo.GetComponent<RectTransform>();
            _root.SetParent(canvasGo.transform, false);
            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.sizeDelta = new Vector2(220f, 92f);
            _root.anchoredPosition = new Vector2(0f, -158f);

            var disc = new GameObject("Disc", typeof(RectTransform));
            var discRt = disc.GetComponent<RectTransform>();
            discRt.SetParent(_root, false);
            discRt.anchorMin = new Vector2(0.5f, 1f);
            discRt.anchorMax = new Vector2(0.5f, 1f);
            discRt.pivot = new Vector2(0.5f, 1f);
            discRt.sizeDelta = new Vector2(54f, 54f);
            discRt.anchoredPosition = new Vector2(0f, 0f);
            var discImg = disc.AddComponent<Image>();
            discImg.sprite = DotSprite();
            discImg.color = new Color(0.05f, 0.06f, 0.08f, 0.62f);
            discImg.raycastTarget = false;

            var arrowGo = new GameObject("Arrow", typeof(RectTransform));
            _arrow = arrowGo.GetComponent<RectTransform>();
            _arrow.SetParent(discRt, false);
            _arrow.anchorMin = new Vector2(0.5f, 0.5f);
            _arrow.anchorMax = new Vector2(0.5f, 0.5f);
            _arrow.pivot = new Vector2(0.5f, 0.5f);
            _arrow.sizeDelta = new Vector2(26f, 26f);
            _arrow.anchoredPosition = Vector2.zero;
            var arrowImg = arrowGo.AddComponent<Image>();
            arrowImg.sprite = TriSprite();
            arrowImg.color = new Color(0.40f, 0.95f, 0.65f, 1f);
            arrowImg.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(_root, false);
            labelRt.anchorMin = new Vector2(0.5f, 0f);
            labelRt.anchorMax = new Vector2(0.5f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.sizeDelta = new Vector2(220f, 26f);
            labelRt.anchoredPosition = new Vector2(0f, 0f);
            _label = labelGo.AddComponent<Text>();
            _label.font = CjkFont.Get();
            _label.fontSize = 17;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(0.92f, 0.96f, 1f, 1f);
            _label.raycastTarget = false;
            _label.text = "";
        }

        void Update()
        {
            var obj = LevelObjective.Active;
            if (obj == null || obj.IsDone)
            {
                if (_label != null && _label.text.Length > 0) _label.text = "";
                if (_group.alpha > 0f) _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.deltaTime * 4f);
                return;
            }

            var target = obj.NextTarget();
            var cam = Camera.main;
            if (target == null || cam == null)
            {
                if (_label != null && _label.text.Length > 0) _label.text = "";
                if (_group.alpha > 0f) _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.deltaTime * 4f);
                return;
            }

            Vector3 tp = target.transform.position;
            Vector3 local = cam.transform.InverseTransformPoint(tp);
            float ang = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -ang);

            Vector3 d = tp - cam.transform.position;
            d.y = 0f;
            float dist = d.magnitude;

            bool isPortal = !string.IsNullOrEmpty(target.label) && target.label.Contains("传送门");
            _label.text = (isPortal ? "传送门 · " : (string.IsNullOrEmpty(target.label) ? "目标 · " : target.label + " · "))
                          + Mathf.RoundToInt(dist) + " m";
            _label.color = isPortal ? new Color(0.55f, 0.98f, 0.72f, 1f) : new Color(0.92f, 0.96f, 1f, 1f);

            float want = dist <= NearHideDistance ? 0f : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, want, Time.deltaTime * 5f);
        }

        static Sprite DotSprite()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? Mathf.Clamp01(1.6f - d * 1.6f) : 0f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f));
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
