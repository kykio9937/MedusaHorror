using UnityEngine;
using UnityEngine.UI;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 赛博朋克 UI 皮肤：程序化生成霓虹描边（九宫格）与扫描线，统一套到各功能面板上。
    /// 不依赖外部美术资源，缩放任意分辨率都不会糊。
    /// </summary>
    public static class UiSkin
    {
        public static readonly Color NeonCyan = new Color(0.32f, 0.92f, 1f, 1f);
        public static readonly Color NeonMagenta = new Color(1f, 0.28f, 0.62f, 1f);

        static Sprite _frame;
        static Sprite _scan;

        /// <summary>给面板根节点套上描边 + 扫描线（幂等，重复调用不会叠加）。</summary>
        public static void Apply(RectTransform root, bool scanline = true)
        {
            if (root == null) return;

            var frame = EnsureChild(root, "SkinFrame");
            var img = frame.GetComponent<Image>();
            if (img == null) img = frame.gameObject.AddComponent<Image>();
            img.sprite = GetFrame();
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, 0.95f);
            img.raycastTarget = false;
            frame.SetAsLastSibling();

            if (!scanline) return;

            var scan = EnsureChild(root, "SkinScan");
            var simg = scan.GetComponent<Image>();
            if (simg == null) simg = scan.gameObject.AddComponent<Image>();
            simg.sprite = GetScan();
            simg.type = Image.Type.Tiled;
            simg.color = new Color(0.45f, 0.9f, 1f, 0.055f);
            simg.raycastTarget = false;
            scan.SetSiblingIndex(Mathf.Max(0, frame.GetSiblingIndex() - 1));
        }

        static RectTransform EnsureChild(RectTransform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Sprite GetFrame()
        {
            if (_frame != null) return _frame;

            const int S = 96;
            const int B = 22;
            const int Line = 3;

            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.name = "UiSkinFrame";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);

            // 四条边线：外侧淡、内侧亮
            for (int x = 0; x < S; x++)
            {
                for (int k = 0; k < Line; k++)
                {
                    float a = k == 0 ? 0.55f : (k == 1 ? 0.95f : 0.35f);
                    var c = NeonCyan; c.a = a;
                    px[k * S + x] = c;              // 下
                    px[(S - 1 - k) * S + x] = c;    // 上
                }
            }
            for (int y = 0; y < S; y++)
            {
                for (int k = 0; k < Line; k++)
                {
                    float a = k == 0 ? 0.55f : (k == 1 ? 0.95f : 0.35f);
                    var c = NeonCyan; c.a = a;
                    px[y * S + k] = c;              // 左
                    px[y * S + (S - 1 - k)] = c;    // 右
                }
            }

            // 四角 L 形角标（洋红），长度 34、厚度 5
            const int Corner = 34;
            const int Thick = 5;
            var mg = NeonMagenta;
            for (int i = 0; i < Corner; i++)
            {
                for (int t = 0; t < Thick; t++)
                {
                    var c = mg; c.a = 0.95f;
                    // 左下
                    px[t * S + i] = c;
                    px[i * S + t] = c;
                    // 右下
                    px[t * S + (S - 1 - i)] = c;
                    px[i * S + (S - 1 - t)] = c;
                    // 左上
                    px[(S - 1 - t) * S + i] = c;
                    px[(S - 1 - i) * S + t] = c;
                    // 右上
                    px[(S - 1 - t) * S + (S - 1 - i)] = c;
                    px[(S - 1 - i) * S + (S - 1 - t)] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply();

            _frame = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                                   SpriteMeshType.FullRect, new Vector4(B, B, B, B));
            _frame.name = "UiSkinFrameSprite";
            return _frame;
        }

        static Sprite GetScan()
        {
            if (_scan != null) return _scan;

            const int W = 4, H = 6;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "UiSkinScan";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;

            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float a = y == 0 ? 1f : 0f;
                for (int x = 0; x < W; x++) px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply();

            _scan = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _scan.name = "UiSkinScanSprite";
            return _scan;
        }
    }
}
