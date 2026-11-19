using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Gameplay;

namespace MedusaHorror.UI
{
    /// <summary>
    /// Boss 血条 HUD：顶部居中。休眠时完全隐藏，苏醒后淡入，死亡后定格并淡出。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHud : MonoBehaviour
    {
        public MedusaBoss boss;
        public string bossName = "石化巫女 · 美杜莎";

        CanvasGroup _group;
        RectTransform _fill;
        Image _fillImage;
        Text _nameText;
        Text _stateText;
        float _shown;
        float _deadTimer = -1f;

        void Start()
        {
            if (boss == null) boss = Object.FindObjectOfType<MedusaBoss>();
            Build();
        }

        void Build()
        {
            var canvasGo = new GameObject("BossCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 65;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            _nameText = MakeText(canvasGo.transform, "BossName", bossName, 26,
                new Vector2(0.5f, 1f), new Vector2(760f, 34f), new Vector2(0f, -58f),
                new Color(0.92f, 0.80f, 0.78f, 0.95f));

            var back = MakeImage(canvasGo.transform, "BossBarBack",
                new Vector2(0.5f, 1f), new Vector2(900f, 18f), new Vector2(0f, -92f),
                new Color(0f, 0f, 0f, 0.55f));

            var fillGo = new GameObject("BossBarFill");
            fillGo.transform.SetParent(back.transform, false);
            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.color = new Color(0.78f, 0.16f, 0.14f, 1f);
            _fillImage.raycastTarget = false;
            _fill = _fillImage.rectTransform;
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(1f, 1f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = new Vector2(0f, 0f);
            _fill.offsetMax = new Vector2(0f, 0f);

            _stateText = MakeText(canvasGo.transform, "BossState", "", 20,
                new Vector2(0.5f, 1f), new Vector2(760f, 30f), new Vector2(0f, -116f),
                new Color(0.86f, 0.84f, 0.84f, 0.9f));
            _stateText.enabled = false;
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
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return img;
        }

        static Text MakeText(Transform parent, string name, string content, int size,
                             Vector2 anchor, Vector2 rect, Vector2 pos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var f = MedusaHorror.UI.CjkFont.Get();
            if (f != null) t.font = f;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = rect;
            rt.anchoredPosition = pos;
            return t;
        }

        void Update()
        {
            if (boss == null || _group == null) return;
            float dt = Time.deltaTime;

            bool visible = boss.IsAwake || boss.IsDead;
            _shown = Mathf.MoveTowards(_shown, visible ? 1f : 0f, dt * 2.2f);
            _group.alpha = _shown;

            if (_fill != null)
            {
                float k = boss.Health01;
                _fill.anchorMax = new Vector2(k, 1f);
                if (_fillImage != null)
                    _fillImage.color = k <= boss.rageThreshold
                        ? new Color(0.95f, 0.42f, 0.10f, 1f)
                        : new Color(0.78f, 0.16f, 0.14f, 1f);
            }

            if (boss.IsDead && _deadTimer < 0f) _deadTimer = 0f;
            if (_deadTimer >= 0f)
            {
                _deadTimer += dt;
                if (_stateText != null)
                {
                    _stateText.enabled = true;
                    _stateText.text = "美杜莎已被击碎";
                    _stateText.color = new Color(0.82f, 0.86f, 0.80f, 0.95f);
                }
                if (_deadTimer > 4f) _shown = Mathf.MoveTowards(_shown, 0f, dt * 1.5f);
            }
            else if (_stateText != null && boss.IsRaging)
            {
                _stateText.enabled = true;
                _stateText.text = "她的视线正在加速侵蚀……";
                _stateText.color = new Color(0.95f, 0.55f, 0.30f, 0.95f);
            }
            else if (_stateText != null)
            {
                _stateText.enabled = false;
            }
        }
    }
}
