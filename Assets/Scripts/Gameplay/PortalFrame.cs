using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 星际传送门实体门框：从 Resources 加载 Teleport 模型，按门洞尺寸自动适配并贴上 PBR 贴图。
    /// 纯表现层，不含碰撞体，不参与门禁判定。
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalFrame : MonoBehaviour
    {
        const string ModelPath = "Portal/Teleport";

        [Header("适配")]
        [Tooltip("门框整体高度（米）")]
        public float targetHeight = 3.6f;
        [Tooltip("门框中心相对传送门根节点的本地坐标")]
        public Vector3 localCenter = new Vector3(0f, 1.35f, 0f);
        [Tooltip("模型朝向修正（度）")]
        public float yaw = 90f;
        public Vector3 modelEulerOffset;
        public bool modelAsFloorPad;

        [Header("动效")]
        public bool spin = true;
        public float spinSpeed = 6f;
        public float lockedEmission = 0.35f;
        public float unlockedEmission = 1.8f;

        Transform _root;
        Material _mat;
        Renderer[] _renderers;
        ExitPortal _portal;
        string _diag = "未装配";

        public bool Built { get { return _root != null; } }
        public string Diag { get { return _diag; } }

        public static PortalFrame Ensure(ExitPortal portal)
        {
            var pf = portal.GetComponent<PortalFrame>();
            if (pf == null) pf = portal.gameObject.AddComponent<PortalFrame>();
            pf.Build();
            return pf;
        }

        void Awake()
        {
            _portal = GetComponent<ExitPortal>();
            Build();
        }

        public void Build()
        {
            if (_root != null) return;

            var prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null)
            {
                _diag = "模型缺失：" + ModelPath;
                return;
            }

            var go = Instantiate(prefab);
            go.name = "PortalFrameModel";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(modelEulerOffset + new Vector3(0f, yaw, 0f));
            go.transform.localScale = Vector3.one;

            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);

            _mat = BuildMaterial();
            _renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].sharedMaterial = _mat;
                _renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 尺寸自适应：把模型整体高度缩到 targetHeight
            Bounds b = ComputeBounds(go);
            float h = Mathf.Max(0.001f, modelAsFloorPad ? Mathf.Max(b.size.x,b.size.z) : b.size.y);
            float s = targetHeight / h;
            go.transform.localScale = modelAsFloorPad ? new Vector3(s,s*0.06f,s) : Vector3.one*s;

            // 缩放后重新取包围盒，把几何中心对到门洞中心
            Bounds b2 = ComputeBounds(go);
            Vector3 centerLocal = transform.InverseTransformPoint(b2.center);
            if(modelAsFloorPad)centerLocal.y=transform.InverseTransformPoint(new Vector3(b2.center.x,b2.min.y,b2.center.z)).y;
            go.transform.localPosition += localCenter - centerLocal;

            _root = go.transform;
            _diag = "ok size=" + b.size.ToString("F2") + " scale=" + s.ToString("F2") + " renderers=" + _renderers.Length;
            ApplyEmission();

            // 门洞能量膜
            PortalEnergy.Ensure(this);
        }

        static Bounds ComputeBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null) continue;
                if (!has) { b = rs[i].bounds; has = true; }
                else b.Encapsulate(rs[i].bounds);
            }
            return b;
        }

        Material BuildMaterial()
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            var m = new Material(shader);
            m.name = "PortalFrameMat";

            var bc = Resources.Load<Texture2D>("Portal/textures/Teleport_Base_Color");
            var nm = Resources.Load<Texture2D>("Portal/textures/Teleport_Normal_DirectX");
            var em = Resources.Load<Texture2D>("Portal/textures/Teleport_Emissive");
            var ao = Resources.Load<Texture2D>("Portal/textures/Teleport_Mixed_AO");

            if (bc != null) m.SetTexture("_MainTex", bc);
            if (nm != null) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
            if (ao != null) m.SetTexture("_OcclusionMap", ao);
            if (em != null)
            {
                m.SetTexture("_EmissionMap", em);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.45f, 0.85f, 1f) * lockedEmission);
            }
            m.SetFloat("_Metallic", 0.65f);
            m.SetFloat("_Glossiness", 0.55f);
            return m;
        }

        void ApplyEmission()
        {
            if (_mat == null || _portal == null) return;
            bool unlocked = _portal.Activated;
            float e = unlocked ? unlockedEmission : lockedEmission;
            _mat.SetColor("_EmissionColor", new Color(0.45f, 0.85f, 1f) * e);
        }

        void Update()
        {
            if (_root == null) return;
            if (spin) _root.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            ApplyEmission();
        }
    }
}
