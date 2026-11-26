using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 传送门能量膜：程序生成的径向渐变 + 同心环贴图，贴在门洞平面上做脉动与旋转。
    /// 纯表现层，无碰撞体。
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalEnergy : MonoBehaviour
    {
        [Header("尺寸（门洞）")]
        public float width = 2.50f;
        public float height = 2.60f;
        [Tooltip("相对传送门根节点的中心")]
        public Vector3 localCenter = new Vector3(0f, 1.30f, 0f);
        [Tooltip("沿门法线方向的偏移，避免与门框共面闪烁")]
        public float normalOffset = -0.75f;

        [Header("动效")]
        public float spinSpeed = 14f;
        public float pulseSpeed = 1.7f;
        public Color tint = new Color(0.45f, 0.90f, 1f, 0.95f);

        Transform _quad;
        Light _light;
        Material _mat;
        ExitPortal _portal;
        float _t;
        static Texture2D _sharedTex;

        public static PortalEnergy Ensure(PortalFrame frame)
        {
            var pe = frame.GetComponent<PortalEnergy>();
            if (pe == null) pe = frame.gameObject.AddComponent<PortalEnergy>();
            pe.Build();
            return pe;
        }

        void Awake()
        {
            _portal = GetComponent<ExitPortal>();
            Build();
        }

        public void Build()
        {
            if (_quad != null) return;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PortalEnergyFilm";
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            quad.transform.SetParent(transform, false);
            // 门扇平面是本地 YZ 平面（法线本地 +X），宽度沿本地 Z
            quad.transform.localPosition = localCenter + Vector3.right * normalOffset;
            quad.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            quad.transform.localScale = new Vector3(width, height, 1f);

            if (_sharedTex == null) _sharedTex = BuildTexture();

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            _mat = new Material(shader);
            _mat.name = "PortalEnergyMat";
            _mat.mainTexture = _sharedTex;
            _mat.color = tint;
            quad.GetComponent<Renderer>().sharedMaterial = _mat;
            quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _quad = quad.transform;

            // 门洞中心补一盏青色点光，让能量膜在暗场景里有实体感
            var lightGo = new GameObject("PortalEnergyLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = localCenter + Vector3.right * 0.9f;
            var lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(0.42f, 0.88f, 1f);
            lt.range = 7f;
            lt.intensity = 2.6f;
            lt.shadows = LightShadows.None;
            _light = lt;
        }

        static Texture2D BuildTexture()
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.name = "PortalEnergyTex";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2f - 1f;
                    float dy = (y + 0.5f) / S * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float falloff = Mathf.Clamp01(1f - r);
                    falloff = Mathf.Pow(falloff, 0.85f);

                    float ring = 0.5f + 0.5f * Mathf.Sin(r * 26f);
                    float swirl = 0.5f + 0.5f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + r * 9f);
                    float v = falloff * (0.45f + 0.35f * ring + 0.20f * swirl);

                    byte a = (byte)Mathf.Clamp(v * 380f, 0f, 255f);
                    px[y * S + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        void Update()
        {
            if (_quad == null) return;

            _t += Time.deltaTime;
            _quad.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);

            float pulse = 0.85f + 0.15f * Mathf.Sin(_t * pulseSpeed);
            _quad.localScale = new Vector3(width * pulse, height * pulse, 1f);

            if (_light != null)
            {
                bool unl = _portal == null || _portal.Activated;
                _light.intensity = (unl ? 2.6f : 0.9f) * (0.88f + 0.12f * Mathf.Sin(_t * pulseSpeed * 1.3f));
            }

            if (_mat != null)
            {
                bool unlocked = _portal == null || _portal.Activated;
                float a = unlocked ? 0.95f : 0.40f;
                var c = tint;
                c.a = a * (0.85f + 0.15f * Mathf.Sin(_t * pulseSpeed * 1.7f));
                _mat.color = c;
            }
        }
    }
}
