using UnityEngine;

namespace MedusaHorror.Player
{
    /// <summary>
    /// 玩家外观：V 键在三套配色间切换。
    /// 角色模型用的是 UnityChan 自定义 shader，直接改材质颜色会渲染成黑色剪影，
    /// 所以这里只改脚下的能量光环颜色，模型本体完全不动。
    /// </summary>
    public class PlayerSkin : MonoBehaviour
    {
        public static readonly string[] Names = { "默认", "暗夜", "赤红" };

        static readonly Color[] Colors =
        {
            new Color(0.35f, 0.85f, 1f),
            new Color(0.55f, 0.45f, 1f),
            new Color(1f, 0.35f, 0.32f),
        };

        const string PrefKey = "medusa_player_skin";

        int _index;
        Renderer _ringRenderer;
        Material _ringMat;

        public int Index { get { return _index; } }
        public string SkinName { get { return Names[Mathf.Clamp(_index, 0, Names.Length - 1)]; } }

        void Start()
        {
            _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Names.Length - 1);
            BuildRing();
            Apply();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                _index = (_index + 1) % Names.Length;
                PlayerPrefs.SetInt(PrefKey, _index);
                PlayerPrefs.Save();
                Apply();
                Gameplay.LootToast.Show("光环：" + SkinName);
            }
        }

        void BuildRing()
        {
            if (_ringRenderer != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "PlayerRing";
            var col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            _ringMat = new Material(Shader.Find("Sprites/Default"));
            _ringMat.mainTexture = MakeRingTexture();
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _ringMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _ringRenderer = r;
        }

        static Texture2D MakeRingTexture()
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[S * S];
            Vector2 c = new Vector2(S * 0.5f, S * 0.5f);
            float outer = S * 0.48f;
            float inner = S * 0.34f;
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float a = 0f;
                    if (d <= outer && d >= inner)
                    {
                        float t = Mathf.InverseLerp(inner, outer, d);
                        a = Mathf.Sin(t * Mathf.PI) * 0.85f;
                    }
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public void Apply()
        {
            if (_ringMat == null) return;
            var c = Colors[Mathf.Clamp(_index, 0, Colors.Length - 1)];
            _ringMat.color = c;
        }
    }
}
