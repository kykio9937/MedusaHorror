using UnityEngine;
using UnityEngine.UI;

namespace MedusaHorror.UI
{
    /// <summary>异步加载期间由虚拟网格解离到白光，再逐渐显露现实场景。</summary>
    public class RealityReturnEffect : MonoBehaviour
    {
        Image _wash;
        CanvasGroup _grid;
        Text _status;
        public void Build(Transform parent)
        {
            transform.SetParent(parent, false);
            _wash = Part("RealityWash", transform, new Color(0.025f, 0.08f, 0.11f));
            var root = new GameObject("DissolvingGrid", typeof(RectTransform), typeof(CanvasGroup)); root.transform.SetParent(transform, false);
            var rt = root.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            _grid = root.GetComponent<CanvasGroup>();
            for (int i = 1; i < 16; i++)
            {
                var line = Part("Vertical", rt, new Color(0.25f, 0.83f, 0.86f, 0.22f)).rectTransform;
                line.anchorMin = new Vector2(i / 16f, 0); line.anchorMax = new Vector2(i / 16f, 1); line.sizeDelta = new Vector2(1, 0);
            }
            for (int i = 1; i < 10; i++)
            {
                var line = Part("Horizontal", rt, new Color(0.25f, 0.83f, 0.86f, 0.18f)).rectTransform;
                line.anchorMin = new Vector2(0, i / 10f); line.anchorMax = new Vector2(1, i / 10f); line.sizeDelta = new Vector2(0, 1);
            }
            var label = new GameObject("ReturnStatus", typeof(RectTransform), typeof(Text)); label.transform.SetParent(transform, false);
            var tr = label.GetComponent<RectTransform>(); tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.sizeDelta = new Vector2(1000, 100);
            _status = label.GetComponent<Text>(); _status.font = CjkFont.Get(); _status.fontSize = 30; _status.alignment = TextAnchor.MiddleCenter; _status.raycastTarget = false;
            _status.text = "感知信号正在重组……";
        }
        static Image Part(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        public void Tick(float time)
        {
            float phase = Mathf.Clamp01(time / 3.8f);
            _wash.color = Color.Lerp(new Color(0.025f, 0.08f, 0.11f), new Color(0.94f, 0.95f, 0.9f), phase);
            _grid.alpha = 1 - phase;
            _grid.transform.localScale = Vector3.one * (1 + phase * 0.18f);
            _status.color = Color.Lerp(new Color(0.65f, 0.95f, 1), new Color(0.22f, 0.3f, 0.31f), phase);
            _status.text = time < 1.5f ? "感知信号正在重组……" : time < 3 ? "远处传来人的声音……" : "呼吸，慢慢睁开眼睛。";
        }
        public void Reveal(float progress)
        {
            _grid.alpha = 0;
            _status.color = new Color(0.2f, 0.25f, 0.26f, 1 - progress);
            _wash.color = new Color(0.94f, 0.95f, 0.9f, 1 - Mathf.SmoothStep(0, 1, progress));
        }
    }
}
