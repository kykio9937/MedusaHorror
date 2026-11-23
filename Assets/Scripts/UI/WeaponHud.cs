using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Player;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 武器 HUD：左下角常驻面板，显示当前武器、近战冷却或枪械弹药，以及切换提示；
    /// 命中时全屏白闪 + 短暂命中文字。剑与枪共用这一块 HUD。
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponHud : MonoBehaviour
    {
        public MeleeWeapon sword;
        public PlayerWeapon gun;

        CanvasGroup _group;
        Text _weaponText;
        Text _stateText;
        Text _hintText;
        Text _resultText;
        Image _barBack;
        Image _barFill;
        Image _flash;

        float _flashAlpha;
        float _resultTimer = -1f;

        public WeaponKind CurrentKind { get; private set; }
        public string WeaponLabel { get; private set; }
        public string StateLabel { get; private set; }
        public float BarFill01 { get; private set; }
        public float FlashAlpha { get { return _flashAlpha; } }

        void Awake()
        {
            if (sword == null) sword = GetComponent<MeleeWeapon>();
            if (gun == null) gun = GetComponent<PlayerWeapon>();
            Build();
        }

        void Build()
        {
            var canvasGo = new GameObject("WeaponCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.blocksRaycasts = false;

            // 武器名（左下）
            _weaponText = MakeText(canvasGo.transform, "WeaponName", "长剑", 28,
                new Vector2(0f, 0f), new Vector2(520f, 38f), new Vector2(46f, 132f),
                new Color(0.95f, 0.92f, 0.86f, 0.96f), TextAnchor.MiddleLeft);

            // 状态行（冷却 / 弹药）
            _stateText = MakeText(canvasGo.transform, "WeaponState", "", 22,
                new Vector2(0f, 0f), new Vector2(520f, 30f), new Vector2(46f, 104f),
                new Color(0.84f, 0.82f, 0.80f, 0.92f), TextAnchor.MiddleLeft);

            // 条背景
            _barBack = MakeImage(canvasGo.transform, "WeaponBarBack",
                new Vector2(0f, 0f), new Vector2(300f, 8f), new Vector2(46f, 88f),
                new Color(0f, 0f, 0f, 0.5f));

            var fillGo = new GameObject("WeaponBarFill");
            fillGo.transform.SetParent(_barBack.transform, false);
            _barFill = fillGo.AddComponent<Image>();
            _barFill.color = new Color(0.82f, 0.78f, 0.45f, 1f);
            _barFill.raycastTarget = false;
            var frt = _barFill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            // 切换提示
            _hintText = MakeText(canvasGo.transform, "WeaponHint", "1 剑 · 2 枪 · Q 切换", 20,
                new Vector2(0f, 0f), new Vector2(520f, 28f), new Vector2(46f, 60f),
                new Color(0.72f, 0.72f, 0.74f, 0.8f), TextAnchor.MiddleLeft);

            // 命中结果（屏幕中下方）
            _resultText = MakeText(canvasGo.transform, "WeaponResult", "", 26,
                new Vector2(0.5f, 0f), new Vector2(700f, 40f), new Vector2(0f, 210f),
                new Color(1f, 0.88f, 0.62f, 0.95f), TextAnchor.MiddleCenter);
            _resultText.enabled = false;

            // 命中白闪（全屏）
            _flash = MakeImage(canvasGo.transform, "HitFlash",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), Vector2.zero,
                new Color(1f, 1f, 1f, 0f));
            var frt2 = _flash.rectTransform;
            frt2.anchorMin = Vector2.zero;
            frt2.anchorMax = Vector2.one;
            frt2.offsetMin = Vector2.zero;
            frt2.offsetMax = Vector2.zero;
            frt2.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>左下武器信息块在屏幕坐标系中的矩形（诊断叠压用）。</summary>
        public Rect HudScreenRect()
        {
            var r = new Rect(0f, 0f, 0f, 0f);
            bool has = false;
            CollectRect(_weaponText != null ? _weaponText.rectTransform : null, ref r, ref has);
            CollectRect(_stateText != null ? _stateText.rectTransform : null, ref r, ref has);
            CollectRect(_barBack != null ? _barBack.rectTransform : null, ref r, ref has);
            CollectRect(_hintText != null ? _hintText.rectTransform : null, ref r, ref has);
            return r;
        }

        static void CollectRect(RectTransform rt, ref Rect acc, ref bool has)
        {
            if (rt == null) return;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            if (!has) { acc = r; has = true; }
            else acc = Rect.MinMaxRect(Mathf.Min(acc.xMin, r.xMin), Mathf.Min(acc.yMin, r.yMin),
                                       Mathf.Max(acc.xMax, r.xMax), Mathf.Max(acc.yMax, r.yMax));
        }

        static Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 size,
                               Vector2 pos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return img;
        }

        static Text MakeText(Transform parent, string name, string content, int size,
                             Vector2 anchor, Vector2 rect, Vector2 pos, Color color,
                             TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var f = CjkFont.Get();
            if (f != null) t.font = f;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = rect;
            rt.anchoredPosition = pos;
            return t;
        }

        /// <summary>由 WeaponLoadout 在切换时调用。</summary>
        public void SetWeapon(WeaponKind kind, WeaponLoadout loadout, MeleeWeapon melee, PlayerWeapon firearm)
        {
            CurrentKind = kind;
            if (melee != null) sword = melee;
            if (firearm != null) gun = firearm;

            if (kind == WeaponKind.Sword)
            {
                WeaponLabel = "长剑 · Claymore";
                if (_weaponText != null)
                {
                    _weaponText.text = WeaponLabel;
                    _weaponText.color = new Color(0.95f, 0.92f, 0.86f, 0.96f);
                }
                if (_barFill != null) _barFill.color = new Color(0.82f, 0.78f, 0.45f, 1f);
            }
            else if (kind == WeaponKind.Rifle)
            {
                WeaponLabel = "脉冲步枪 · MK-II";
                if (_weaponText != null)
                {
                    _weaponText.text = WeaponLabel;
                    _weaponText.color = new Color(0.72f, 0.96f, 1f, 0.98f);
                }
                if (_barFill != null) _barFill.color = new Color(0.30f, 0.92f, 1f, 1f);
            }
            else
            {
                WeaponLabel = "手枪 · M1911";
                if (_weaponText != null)
                {
                    _weaponText.text = WeaponLabel;
                    _weaponText.color = new Color(0.86f, 0.90f, 0.96f, 0.96f);
                }
                if (_barFill != null) _barFill.color = new Color(0.55f, 0.75f, 0.92f, 1f);
            }
        }

        /// <summary>命中时刻的全屏白闪。</summary>
        public void FlashWhite(float alpha)
        {
            _flashAlpha = Mathf.Clamp01(alpha);
        }

        /// <summary>显示一次挥砍结果（命中 / 挥空），1 秒后淡出。</summary>
        public void ShowSwingResult(string text, bool hit)
        {
            if (_resultText == null) return;
            _resultText.enabled = true;
            _resultText.text = text;
            _resultText.color = hit
                ? new Color(1f, 0.86f, 0.56f, 0.96f)
                : new Color(0.78f, 0.80f, 0.82f, 0.85f);
            _resultTimer = 1.0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (_flashAlpha > 0.0001f && _flash != null)
            {
                _flashAlpha = Mathf.Max(0f, _flashAlpha - dt * 2.6f);
                _flash.color = new Color(1f, 1f, 1f, _flashAlpha);
            }

            if (_resultTimer > 0f)
            {
                _resultTimer -= dt;
                if (_resultTimer <= 0f && _resultText != null) _resultText.enabled = false;
            }

            if (CurrentKind == WeaponKind.Sword)
            {
                if (sword == null) return;
                float cd = sword.CooldownRemaining;
                StateLabel = cd <= 0.001f
                    ? "近战：就绪（伤害 " + sword.damage.ToString("0") + " · 范围 " + sword.attackRange.ToString("0.0") + "m）"
                    : "近战冷却 " + cd.ToString("0.00") + "s";
                BarFill01 = 1f - sword.Cooldown01;
            }
            else
            {
                if (gun == null) return;
                StateLabel = gun.IsReloading
                    ? "换弹中……"
                    : ("弹药 " + gun.AmmoInMagazine + " / " + gun.ReserveAmmo);
                BarFill01 = gun.MagazineSize > 0 ? (float)gun.AmmoInMagazine / gun.MagazineSize : 0f;
            }

            if (_stateText != null) _stateText.text = StateLabel;
            if (_barFill != null)
            {
                var rt = _barFill.rectTransform;
                rt.anchorMax = new Vector2(Mathf.Clamp01(BarFill01), 1f);
            }
        }
    
        string _defaultHint;

        /// <summary>诊断：当前武器名文本。</summary>
        public string CurrentWeaponLabel { get { return _weaponText != null ? _weaponText.text : ""; } }

        /// <summary>诊断：当前底部提示文本。</summary>
        public string HintLabel { get { return _hintText != null ? _hintText.text : ""; } }

        /// <summary>底部提示：传 null / 空串恢复默认提示（1 剑 · 2 枪 · Q 切换）。</summary>
        public void SetHintText(string text)
        {
            if (_hintText == null) return;
            if (_defaultHint == null) _defaultHint = _hintText.text;
            _hintText.text = string.IsNullOrEmpty(text) ? _defaultHint : text;
        }

}
}
