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
        RectTransform itemCard;
        Text itemName, itemAction;
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
            if (itemCard != null) itemCard.gameObject.SetActive(false);
        }

        // 放在 LateUpdate：确保 InputReader.Update 已经算完当帧的 InteractPressed
        void LateUpdate()
        {
            target = InputReader.blocked ? null : FindTarget();

            bool item = target is W1ObjectivePickup;
            bool show = target != null && hintText != null;
            if (hintText != null)
            {
                hintText.enabled = show && !item;
                hintText.text = show ? ("[E] " + target.Hint) : string.Empty;
            }
            if (hintBg != null)
            {
                hintBg.enabled = show && !item;
                hintBg.transform.position = hintText.transform.position;
            }
            itemCard.gameObject.SetActive(show && item);
            if (show && item)
            {
                var pickup=(W1ObjectivePickup)target;
                itemName.text=pickup.label;itemAction.text="[E] 拾取";
                var screen=Camera.main.WorldToScreenPoint(pickup.transform.position+Vector3.up*0.1f);
                var parent=(RectTransform)itemCard.parent;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,null,out var local);
                itemCard.anchoredPosition=new Vector2(Mathf.Clamp(local.x+155,-parent.rect.width/2+150,parent.rect.width/2-150),Mathf.Clamp(local.y,-parent.rect.height/2+55,parent.rect.height/2-55));
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
                if (d is W1ObjectivePickup)
                {
                    var camera=Camera.main;if(camera==null)continue;
                    var ray=camera.ViewportPointToRay(new Vector3(0.5f,0.5f,0));
                    // A small angular tolerance keeps tiny models usable with the third-person reticle.
                    var aimBounds=c.bounds;aimBounds.Expand(0.12f);
                    if(!aimBounds.IntersectRay(ray))continue;
                    if(!HasLineOfSight(camera.transform.position,c,d))continue;
                }
                Vector3 to = ((MonoBehaviour)d).transform.position - origin;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) continue;
                if (Vector3.Dot(fwd, to.normalized) < facingDot) continue;
                if (!HasLineOfSight(origin, c, d)) continue;
                float dist = to.magnitude;
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
            return best;
        }

        bool HasLineOfSight(Vector3 origin, Collider candidate, IInteractable interactable)
        {
            Vector3 end = candidate.ClosestPoint(origin);
            Vector3 direction = end - origin;
            if (direction.sqrMagnitude < 0.0001f) return true;
            foreach (var hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<IInteractable>() == interactable) continue;
                return false;
            }
            return true;
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
            var card=new GameObject("FocusedItemCard",typeof(RectTransform),typeof(Image));
            itemCard=card.GetComponent<RectTransform>();itemCard.SetParent(canvasGo.transform,false);
            itemCard.sizeDelta=new Vector2(280,88);card.GetComponent<Image>().color=new Color(0.025f,0.045f,0.055f,0.94f);card.GetComponent<Image>().raycastTarget=false;
            var outline=card.AddComponent<Outline>();outline.effectColor=new Color(0.4f,0.65f,0.68f,0.9f);outline.effectDistance=Vector2.one;
            itemName=CardText("ItemName",itemCard,23,new Vector2(0,16),new Color(0.94f,0.96f,0.96f));
            itemAction=CardText("PickupAction",itemCard,19,new Vector2(0,-18),new Color(0.65f,0.88f,0.84f));card.SetActive(false);

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

        static Text CardText(string name,Transform parent,int size,Vector2 position,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(260,34);rt.anchoredPosition=position;
            var t=go.GetComponent<Text>();t.font=MedusaHorror.UI.CjkFont.Get();t.fontSize=size;t.alignment=TextAnchor.MiddleLeft;t.color=color;t.raycastTarget=false;return t;
        }
    }
}
