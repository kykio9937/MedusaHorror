using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;

namespace MedusaHorror.UI
{
    /// <summary>左下角等级与经验条：升级时闪一下并提示解锁内容。</summary>
    public class ExpHud : MonoBehaviour
    {
        static ExpHud _instance;

        Canvas _canvas;
        Text _levelText;
        Text _xpText;
        Image _fill;
        Image _flash;
        float _flashT;
        bool _shown = true;

        public static ExpHud Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ExpHud");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<ExpHud>();
            _instance.Build();
            return _instance;
        }

        public void SetShown(bool on)
        {
            _shown = on;
            if (_canvas != null) _canvas.gameObject.SetActive(on);
        }

        void OnEnable()
        {
            Progression.Changed += Refresh;
            Progression.LeveledUp += OnLevelUp;
        }

        void OnDisable()
        {
            Progression.Changed -= Refresh;
            Progression.LeveledUp -= OnLevelUp;
        }

        void OnLevelUp(int level)
        {
            _flashT = 0.9f;
            string extra = "";
            for (int i = 0; i < Progression.Unlocks.Length; i++)
            {
                var u = Progression.Unlocks[i];
                if (u.level == level) { extra = " · 解锁「" + u.name + "」" + u.desc; break; }
            }
            LootToast.Show("升级！ Lv " + level + extra);
            var a = AudioDirector.Instance;
            if (a != null) a.Play(SfxId.Pickup, 0.9f, 1.35f);
        }

        void Update()
        {
            if (!_shown) return;
            Refresh();

            if (_flashT > 0f)
            {
                _flashT = Mathf.Max(0f, _flashT - Time.deltaTime);
                if (_flash != null)
                {
                    var c = _flash.color;
                    c.a = _flashT * 0.5f;
                    _flash.color = c;
                }
            }
        }

        void Refresh()
        {
            if (_levelText == null || _fill == null) return;
            int lv = Progression.Level;
            float xp = Progression.Xp;
            float need = Progression.XpToNext;

            _levelText.text = "Lv " + lv;
            _xpText.text = lv >= Progression.MaxLevel
                ? "已满级"
                : Mathf.RoundToInt(xp) + " / " + Mathf.RoundToInt(need);
            _fill.fillAmount = lv >= Progression.MaxLevel ? 1f : Progression.Xp01;
        }

        void Build()
        {
            var go = new GameObject("ExpCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = CjkFont.Get();

            _levelText = MakeText(go.transform, font, 30, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 132f), new Vector2(140f, 40f),
                new Color(1f, 0.86f, 0.4f));

            // 经验条底
            var barBg = new GameObject("BarBg", typeof(RectTransform));
            barBg.transform.SetParent(go.transform, false);
            var brt = barBg.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(0f, 0f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.anchoredPosition = new Vector2(178f, 152f);
            brt.sizeDelta = new Vector2(300f, 14f);
            var bgImg = barBg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.12f, 0.16f, 0.85f);

            var barFill = new GameObject("BarFill", typeof(RectTransform));
            barFill.transform.SetParent(barBg.transform, false);
            var frt = barFill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _fill = barFill.AddComponent<Image>();
            _fill.color = new Color(0.3f, 0.85f, 1f, 0.95f);
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = 0;
            _fill.fillAmount = 0f;

            _xpText = MakeText(go.transform, font, 20, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(178f, 132f), new Vector2(300f, 26f),
                new Color(0.72f, 0.80f, 0.9f));

            var flashGo = new GameObject("Flash", typeof(RectTransform));
            flashGo.transform.SetParent(go.transform, false);
            var lrt = flashGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _flash = flashGo.AddComponent<Image>();
            _flash.color = new Color(1f, 0.9f, 0.4f, 0f);
            _flash.raycastTarget = false;

            Refresh();
        }

        static Text MakeText(Transform parent, Font font, int size, TextAnchor anchor,
            Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
