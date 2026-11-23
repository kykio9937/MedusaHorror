using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Gameplay;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 屏幕右下角主动技能条：两个技能格，显示按键、名称、冷却遮罩与剩余秒数。
    /// </summary>
    public class SkillHud : MonoBehaviour
    {
        static SkillHud _inst;

        ActiveSkill _skill;
        Image _slashMask;
        Image _shockMask;
        Text _slashText;
        Text _shockText;
        Text _slashCdText;
        Text _shockCdText;
        Canvas _canvas;

        public static SkillHud Instance { get { return _inst; } }

        public static SkillHud Ensure()
        {
            if (_inst != null) return _inst;
            var go = new GameObject("SkillHud");
            _inst = go.AddComponent<SkillHud>();
            return _inst;
        }

        void Awake()
        {
            if (_inst != null && _inst != this) { Destroy(gameObject); return; }
            _inst = this;
            BuildUi();
        }

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }

        void Update()
        {
            if (_skill == null)
            {
                _skill = FindObjectOfType<ActiveSkill>();
                if (_skill == null) return;
            }

            ApplySlot(_slashMask, _slashText, _slashCdText, _skill.SlashUnlocked, _skill.SlashReady,
                      _skill.SlashCd01, _skill.SlashRemain, "裂空斩", "F", new Color(0.32f, 0.92f, 1f));
            ApplySlot(_shockMask, _shockText, _shockCdText, _skill.ShockUnlocked, _skill.ShockReady,
                      _skill.ShockCd01, _skill.ShockRemain, "石化震荡", "V", new Color(0.85f, 0.4f, 1f));
        }

        void ApplySlot(Image mask, Text name, Text cd, bool unlocked, bool ready, float cd01, float remain, string label, string key, Color accent)
        {
            if (mask == null) return;
            mask.fillAmount = unlocked ? cd01 : 1f;
            mask.color = unlocked ? new Color(0f, 0f, 0f, 0.72f) : new Color(0f, 0f, 0f, 0.85f);

            if (name != null)
            {
                name.text = unlocked ? (key + " " + label) : (label + " · Lv" + (label == "裂空斩" ? "10" : "12"));
                name.color = unlocked ? accent : new Color(0.55f, 0.55f, 0.6f, 0.9f);
            }
            if (cd != null)
            {
                if (!unlocked) cd.text = "未解锁";
                else if (remain > 0.05f) cd.text = remain.ToString("F1");
                else cd.text = "就绪";
            }
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 56;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = new GameObject("Skills", typeof(RectTransform));
            root.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(340f, 96f);
            rt.anchoredPosition = new Vector2(-24f, 24f);

            BuildSlot(rt, 0, "裂空斩", "F", new Color(0.32f, 0.92f, 1f), out _slashMask, out _slashText, out _slashCdText);
            BuildSlot(rt, 1, "石化震荡", "V", new Color(0.85f, 0.4f, 1f), out _shockMask, out _shockText, out _shockCdText);
        }

        void BuildSlot(RectTransform parent, int index, string label, string key, Color accent,
                       out Image mask, out Text nameText, out Text cdText)
        {
            float w = 160f, h = 88f, gap = 20f;
            var go = new GameObject("Slot" + index, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(-index * (w + gap), 0f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.72f);
            bg.raycastTarget = false;

            var bar = new GameObject("Accent", typeof(RectTransform));
            bar.transform.SetParent(go.transform, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(4f, 0f);
            var bimg = bar.AddComponent<Image>();
            bimg.color = accent;
            bimg.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(go.transform, false);
            var nrt = (RectTransform)nameGo.transform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.sizeDelta = new Vector2(-16f, 30f);
            nrt.anchoredPosition = new Vector2(0f, -8f);
            nameText = nameGo.AddComponent<Text>();
            nameText.font = CjkFont.Get();
            nameText.fontSize = 19;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = accent;
            nameText.raycastTarget = false;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var cdGo = new GameObject("Cd", typeof(RectTransform));
            cdGo.transform.SetParent(go.transform, false);
            var crt = (RectTransform)cdGo.transform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(-16f, 34f);
            crt.anchoredPosition = new Vector2(0f, 6f);
            cdText = cdGo.AddComponent<Text>();
            cdText.font = CjkFont.Get();
            cdText.fontSize = 22;
            cdText.alignment = TextAnchor.MiddleLeft;
            cdText.color = new Color(0.9f, 0.9f, 0.95f);
            cdText.raycastTarget = false;

            var maskGo = new GameObject("Mask", typeof(RectTransform));
            maskGo.transform.SetParent(go.transform, false);
            var mrt = (RectTransform)maskGo.transform;
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            mask = maskGo.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.72f);
            mask.raycastTarget = false;
            mask.type = Image.Type.Filled;
            mask.fillMethod = Image.FillMethod.Vertical;
            mask.fillOrigin = (int)Image.OriginVertical.Bottom;
            mask.fillAmount = 0f;
        }

        public void SetShown(bool show)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(show);
        }
    }
}
