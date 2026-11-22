using System.Collections.Generic;
using MedusaHorror.Player;
using UnityEngine;
using UnityEngine.UI;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// W1 玩家交互器：每帧在身前搜索最近的可交互物（当前只有门），显示「按 E 开门」提示，按 E 执行交互。
    /// 提示 UI 在运行时自建，不需要预先摆进场景。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("交互范围")]
        [Tooltip("以胸口为中心的搜索半径")]
        public float reach = 2.3f;

        [Tooltip("只接受视锥内的目标（前向点积阈值）")]
        public float facingDot = 0.10f;

        public float eyeHeight = 1.35f;

        /// <summary>交互提示的纵向位置：抬到武器 HUD（底部 60~170）之上，避免文字叠压。</summary>
        public const float HintY = 268f;

        InputReader input;
        IInteractable target;
        Text hintText;
        Image hintBg;
        readonly List<Collider> buffer = new List<Collider>(32);

        void Awake()
        {
            input = GetComponent<InputReader>();
            BuildUi();
        }

        /// <summary>交互提示在屏幕坐标系中的矩形（诊断叠压用）。</summary>
        public Rect HintScreenRect()
        {
            if (hintText == null) return new Rect(0f, 0f, 0f, 0f);
            var rt = hintText.rectTransform;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        void OnDisable()
        {
            if (hintText != null) hintText.enabled = false;
            if (hintBg != null) hintBg.enabled = false;
        }

        // 放在 LateUpdate：确保 InputReader.Update 已经算完当帧的 InteractPressed
        void LateUpdate()
        {
            target = FindTarget();

            bool show = target != null && hintText != null;
            if (hintText != null)
            {
                hintText.enabled = show;
                hintText.text = show ? ("[E] " + target.Hint) : string.Empty;
            }
            if (hintBg != null)
            {
                hintBg.enabled = show;
                hintBg.transform.position = hintText.transform.position;
            }

            if (show && input != null && input.InteractPressed)
                target.Interact();
        }

        IInteractable FindTarget()
        {
            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Collider[] hits = Physics.OverlapSphere(origin, reach, ~0, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestDist = float.MaxValue;
            Vector3 fwd = transform.forward;
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i];
                if (c == null) continue;
                var d = c.GetComponentInParent<IInteractable>();
                if (d == null || !d.CanInteract) continue;
                Vector3 to = ((MonoBehaviour)d).transform.position - origin;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) continue;
                if (Vector3.Dot(fwd, to.normalized) < facingDot) continue;
                float dist = to.magnitude;
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
            return best;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("InteractCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 背景条
            var bgGo = new GameObject("HintBg", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = new Vector2(0.5f, 0f);
            bgRt.anchorMax = new Vector2(0.5f, 0f);
            bgRt.pivot = new Vector2(0.5f, 0f);
            bgRt.sizeDelta = new Vector2(210f, 46f);
            bgRt.anchoredPosition = new Vector2(0f, HintY);
            hintBg = bgGo.AddComponent<Image>();
            hintBg.color = new Color(0f, 0f, 0f, 0.55f);
            hintBg.raycastTarget = false;
            hintBg.enabled = false;

            // 文字
            var txtGo = new GameObject("HintText", typeof(RectTransform));
            txtGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)txtGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(400f, 46f);
            rt.anchoredPosition = new Vector2(0f, HintY);
            hintText = txtGo.AddComponent<Text>();
            var f = MedusaHorror.UI.CjkFont.Get();
            hintText.font = f;
            hintText.fontSize = 26;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(1f, 1f, 1f, 0.95f);
            hintText.raycastTarget = false;
            hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            hintText.enabled = false;
        }
    }
}
