using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Player;
using MedusaHorror.UI;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    /// <summary>只表现醒来的主观感受，不揭示试炼设定。</summary>
    public class TrialAwakening : MonoBehaviour
    {
        public static bool Playing { get; private set; }
        IEnumerator Start()
        {
            while (SceneLoader.IsLoading) yield return null;
            Playing = true;
            bool blocked = InputReader.blocked;
            InputReader.blocked = true;
            var go = new GameObject("AwakeningCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; go.GetComponent<Canvas>().sortingOrder = 2500;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            var upper = Lid(go.transform, true); var lower = Lid(go.transform, false);
            var text = new GameObject("InnerVoice", typeof(RectTransform), typeof(Text)); text.transform.SetParent(go.transform, false);
            var rt = text.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.2f); rt.sizeDelta = new Vector2(900, 90);
            var label = text.GetComponent<Text>(); label.font = CjkFont.Get(); label.fontSize = 26; label.alignment = TextAnchor.MiddleCenter; label.color = new Color(0.8f, 0.82f, 0.8f); label.raycastTarget = false;
            label.text = "……这里是哪？";
            AudioDirector.Instance?.Play(SfxId.Heartbeat, 0.5f, 0.8f);
            float elapsed = 0;
            while (elapsed < 4.5f)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                float opening = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - 0.6f) / 3.4f));
                float blink = elapsed > 1.4f && elapsed < 1.9f ? Mathf.Sin((elapsed - 1.4f) / 0.5f * Mathf.PI) * 0.18f : 0;
                upper.anchorMin = new Vector2(0, Mathf.Clamp01(0.5f + opening * 0.5f - blink));
                lower.anchorMax = new Vector2(1, Mathf.Clamp01(0.5f - opening * 0.5f + blink));
                if (elapsed > 2.4f) label.text = "头还有些昏沉……先看看周围。";
                yield return null;
            }
            Destroy(go); InputReader.blocked = blocked; Playing = false;
            TutorialDirector.MaybeShowOnW1("W1_Hospital");
        }
        static RectTransform Lid(Transform parent, bool top)
        {
            var go = new GameObject(top ? "UpperLid" : "LowerLid", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); rt.anchorMin = top ? new Vector2(0, 0.5f) : Vector2.zero;
            rt.anchorMax = top ? Vector2.one : new Vector2(1, 0.5f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Color.black; go.GetComponent<Image>().raycastTarget = false; return rt;
        }
        void OnDestroy() { Playing = false; }
    }
}
