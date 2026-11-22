using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// W3 列车：一节可以自由走动的车厢，窗外是不断后退的废墟风景。
    /// 车厢建在场景远处（不受站台几何影响），玩家从站台车门按 E 上车。
    /// </summary>
    public class TrainRide : MonoBehaviour
    {
        public static TrainRide Instance { get; private set; }

        [Header("尺寸")]
        public float length = 24f;
        public float width = 5.2f;
        public float height = 3.2f;

        [Header("行驶")]
        public float scrollSpeed = 0.22f;      // 背景贴图每秒滚动比例
        public float shakeAmount = 0.012f;

        [Header("落点")]
        public Vector3 boardPoint = new Vector3(0f, 0.15f, 3f);   // 相对车厢中心
        public Vector3 leavePoint = new Vector3(0f, 0.15f, 10.6f);

        public bool Riding { get; private set; }
        public Vector3 StationReturn { get; set; }

        Transform _cabin;
        Transform _sceneryRoot;
        Renderer[] _bgRenderers;
        Material _bgMatL, _bgMatR;
        Transform[] _poles;
        Transform _player;
        float _time;
        bool _built;

        void Awake()
        {
            Instance = this;
            Build();
        }

        public static TrainRide Ensure(Vector3 stationReturn)
        {
            if (Instance != null) { Instance.StationReturn = stationReturn; return Instance; }
            var go = new GameObject("TrainRide");
            go.transform.position = new Vector3(0f, 0f, 300f);
            var t = go.AddComponent<TrainRide>();
            t.StationReturn = stationReturn;
            return t;
        }

        // ------------------------------------------------------------------ 构建

        void Build()
        {
            if (_built) return;
            _built = true;

            _cabin = new GameObject("Cabin").transform;
            _cabin.SetParent(transform, false);

            var floorMat = Mat(new Color(0.16f, 0.17f, 0.19f), 0f);
            var wallMat = Mat(new Color(0.30f, 0.31f, 0.33f), 0f);
            var trimMat = Mat(new Color(0.10f, 0.45f, 0.50f), 1.4f);
            var seatMat = Mat(new Color(0.22f, 0.20f, 0.26f), 0f);

            // 地板 + 天花板
            Slab("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(width, 0.1f, length), floorMat, _cabin);
            Slab("Ceiling", new Vector3(0f, height, 0f), new Vector3(width, 0.1f, length), wallMat, _cabin);

            // 两侧墙：窗洞上下分段
            float winBottom = 1.05f, winTop = 2.15f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (width * 0.5f);
                Slab("WallLow" + side, new Vector3(x, winBottom * 0.5f, 0f),
                    new Vector3(0.1f, winBottom, length), wallMat, _cabin);
                Slab("WallHigh" + side, new Vector3(x, (winTop + height) * 0.5f, 0f),
                    new Vector3(0.1f, height - winTop, length), wallMat, _cabin);

                // 窗间立柱
                int bays = 8;
                float seg = length / bays;
                for (int i = 0; i <= bays; i++)
                {
                    float z = -length * 0.5f + i * seg;
                    Slab("Mullion" + side + "_" + i, new Vector3(x, (winBottom + winTop) * 0.5f, z),
                        new Vector3(0.12f, winTop - winBottom, 0.16f), wallMat, _cabin);
                }

                // 窗玻璃（半透明）
                var glass = Slab("Glass" + side, new Vector3(x, (winBottom + winTop) * 0.5f, 0f),
                    new Vector3(0.04f, winTop - winBottom, length - 0.4f),
                    Mat(new Color(0.55f, 0.75f, 0.85f), 0f, 0.18f), _cabin, false);
                Object.Destroy(glass.GetComponent<Collider>());

                // 腰线灯带
                Slab("Trim" + side, new Vector3(x - side * 0.06f, winBottom - 0.12f, 0f),
                    new Vector3(0.05f, 0.06f, length - 0.6f), trimMat, _cabin);
            }

            // 前后端墙 + 车门
            for (int end = -1; end <= 1; end += 2)
            {
                Slab("EndWall" + end, new Vector3(0f, height * 0.5f, end * length * 0.5f),
                    new Vector3(width, height, 0.12f), wallMat, _cabin);
                // 门框发光
                Slab("EndDoor" + end, new Vector3(0f, 1.1f, end * (length * 0.5f - 0.07f)),
                    new Vector3(1.2f, 2.2f, 0.06f), trimMat, _cabin, false);
            }

            // 座椅：两侧各一排
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (width * 0.5f - 0.55f);
                for (float z = -length * 0.5f + 2f; z < length * 0.5f - 1.5f; z += 2.0f)
                {
                    Slab("SeatBase" + side + "_" + z, new Vector3(x, 0.45f, z),
                        new Vector3(0.9f, 0.12f, 1.4f), seatMat, _cabin);
                    Slab("SeatBack" + side + "_" + z, new Vector3(x + side * 0.38f, 0.85f, z),
                        new Vector3(0.12f, 0.9f, 1.4f), seatMat, _cabin);
                }
            }

            // 吊环扶手
            var barMat = Mat(new Color(0.55f, 0.58f, 0.60f), 0f, 1f, 0.35f);
            for (int side = -1; side <= 1; side += 2)
            {
                Slab("Rail" + side, new Vector3(side * 0.55f, height - 0.32f, 0f),
                    new Vector3(0.05f, 0.05f, length - 1f), barMat, _cabin);
                for (float z = -length * 0.5f + 1.5f; z < length * 0.5f; z += 2.2f)
                {
                    var strap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    strap.name = "Strap";
                    strap.transform.SetParent(_cabin, false);
                    strap.transform.localPosition = new Vector3(side * 0.55f, height - 0.62f, z);
                    strap.transform.localScale = new Vector3(0.04f, 0.14f, 0.04f);
                    Object.Destroy(strap.GetComponent<Collider>());
                    strap.GetComponent<Renderer>().sharedMaterial = barMat;
                }
            }

            // 顶灯
            for (float z = -length * 0.5f + 2f; z < length * 0.5f; z += 4f)
            {
                var lightGo = new GameObject("CeilLight");
                lightGo.transform.SetParent(_cabin, false);
                lightGo.transform.localPosition = new Vector3(0f, height - 0.12f, z);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 7f;
                l.intensity = 1.1f;
                l.color = new Color(0.85f, 0.93f, 1f);
                l.shadows = LightShadows.None;

                Slab("LightPanel", new Vector3(0f, height - 0.09f, z),
                    new Vector3(0.9f, 0.04f, 0.5f), Mat(new Color(0.9f, 0.95f, 1f), 2.2f), _cabin, false);
            }

            BuildScenery();

            // 车厢前端的下车交互点
            var leaveGo = new GameObject("TrainLeavePoint");
            leaveGo.transform.SetParent(_cabin, false);
            leaveGo.transform.localPosition = new Vector3(0f, 1.1f, length * 0.5f - 0.9f);
            var lc = leaveGo.AddComponent<BoxCollider>();
            lc.isTrigger = true;
            lc.size = new Vector3(1.8f, 2.4f, 1.0f);
            leaveGo.AddComponent<TrainLeavePoint>();
        }

        void BuildScenery()
        {
            _sceneryRoot = new GameObject("Scenery").transform;
            _sceneryRoot.SetParent(transform, false);

            var tex = MakeCityTexture();
            _bgMatL = new Material(Shader.Find("Unlit/Texture"));
            _bgMatL.mainTexture = tex;
            _bgMatR = new Material(Shader.Find("Unlit/Texture"));
            _bgMatR.mainTexture = tex;
            _bgMatL.mainTextureScale = new Vector2(3f, 1f);
            _bgMatR.mainTextureScale = new Vector2(3f, 1f);

            _bgRenderers = new Renderer[2];
            for (int side = -1; side <= 1; side += 2)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Bg" + side;
                q.transform.SetParent(_sceneryRoot, false);
                q.transform.localPosition = new Vector3(side * 16f, 3.2f, 0f);
                q.transform.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
                q.transform.localScale = new Vector3(90f, 26f, 1f);
                Object.Destroy(q.GetComponent<Collider>());
                q.GetComponent<Renderer>().sharedMaterial = side < 0 ? _bgMatL : _bgMatR;
                _bgRenderers[side < 0 ? 0 : 1] = q.GetComponent<Renderer>();
            }

            // 近处掠过的柱子，制造速度感
            int poleCount = 14;
            _poles = new Transform[poleCount];
            var poleMat = Mat(new Color(0.14f, 0.15f, 0.17f), 0f);
            for (int i = 0; i < poleCount; i++)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pole.name = "Pole" + i;
                pole.transform.SetParent(_sceneryRoot, false);
                int side = (i % 2 == 0) ? -1 : 1;
                pole.transform.localPosition = new Vector3(side * 5.2f, 2.6f, -length * 0.5f + i * 3.4f);
                pole.transform.localScale = new Vector3(0.5f, 5.2f, 0.5f);
                Object.Destroy(pole.GetComponent<Collider>());
                pole.GetComponent<Renderer>().sharedMaterial = poleMat;
                _poles[i] = pole.transform;
            }
        }

        static Texture2D MakeCityTexture()
        {
            const int W = 512, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;

            Color sky = new Color(0.05f, 0.07f, 0.13f);
            Color sky2 = new Color(0.14f, 0.10f, 0.20f);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float t = y / (float)H;
                Color c = Color.Lerp(sky, sky2, t * t);
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }

            // 远处建筑剪影
            var rnd = new System.Random(20261122);
            int bx = 0;
            while (bx < W)
            {
                int bw = 14 + rnd.Next(30);
                int bh = 40 + rnd.Next(110);
                Color body = new Color(0.03f, 0.035f, 0.05f);
                for (int x = bx; x < Mathf.Min(W, bx + bw); x++)
                    for (int y = 0; y < bh; y++)
                        px[y * W + x] = body;

                // 窗户光点
                for (int wy = 6; wy < bh - 4; wy += 7)
                    for (int wx = bx + 4; wx < bx + bw - 3; wx += 6)
                    {
                        if (rnd.Next(100) < 32) continue;
                        if (wx < 0 || wx >= W) continue;
                        Color light = rnd.Next(3) switch
                        {
                            0 => new Color(1f, 0.75f, 0.35f),
                            1 => new Color(0.35f, 0.85f, 1f),
                            _ => new Color(1f, 0.4f, 0.75f),
                        };
                        px[wy * W + wx] = light;
                        px[(wy + 1) * W + wx] = light * 0.7f;
                    }

                bx += bw + rnd.Next(4, 16);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Material Mat(Color c, float emission, float alpha = 1f, float smooth = 0.1f)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse"));
            m.color = new Color(c.r, c.g, c.b, alpha);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
            }
            if (alpha < 1f)
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = 3000;
            }
            return m;
        }

        static GameObject Slab(string name, Vector3 localPos, Vector3 scale, Material mat, Transform parent, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // ------------------------------------------------------------------ 上下车

        public void Board()
        {
            var pz = Object.FindObjectOfType<PlayerPetrify>();
            if (pz == null) return;
            _player = pz.transform;
            Riding = true;
            TeleportPlayer(transform.TransformPoint(boardPoint));
            AudioDirector.Instance?.Play(SfxId.DoorOpen, 0.7f, 0.9f);
            UI.StoryBanner.Show("列车启动", "车厢轻轻一震，窗外的灯开始向后流。", 3.2f);
        }

        public void Leave()
        {
            Riding = false;
            TeleportPlayer(StationReturn);
            AudioDirector.Instance?.Play(SfxId.DoorClose, 0.7f, 1f);
        }

        void TeleportPlayer(Vector3 world)
        {
            if (_player == null) return;
            var cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _player.position = world;
            if (cc != null) cc.enabled = true;
        }

        void Update()
        {
            if (!_built) return;
            _time += Time.deltaTime;

            // 背景滚动
            if (_bgMatL != null)
            {
                float off = _time * scrollSpeed;
                _bgMatL.mainTextureOffset = new Vector2(off, 0f);
                _bgMatR.mainTextureOffset = new Vector2(-off, 0f);
            }

            // 近景柱子循环后退
            if (_poles != null)
            {
                float span = _poles.Length * 3.4f;
                for (int i = 0; i < _poles.Length; i++)
                {
                    if (_poles[i] == null) continue;
                    var p = _poles[i].localPosition;
                    p.z -= 26f * Time.deltaTime;
                    if (p.z < -length * 0.5f - 6f) p.z += span;
                    _poles[i].localPosition = p;
                }
            }

            // 车厢轻微晃动
            if (_cabin != null && Riding)
            {
                float s = Mathf.Sin(_time * 9.3f) * shakeAmount + Mathf.Sin(_time * 3.1f) * shakeAmount * 0.5f;
                _cabin.localPosition = new Vector3(0f, s, 0f);
                _cabin.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_time * 5.7f) * 0.25f);
            }
        }
    }

    /// <summary>站台上的车门：按 E 上车。</summary>
    public class TrainBoardPoint : MonoBehaviour, IInteractable
    {
        public Vector3 stationReturn;

        public string Hint { get { return "列车 · 上车（E）"; } }
        public bool CanInteract { get { return true; } }

        public void Interact()
        {
            var t = TrainRide.Ensure(stationReturn);
            t.Board();
        }
    }

    /// <summary>车厢前端的车门：按 E 下车回到站台。</summary>
    public class TrainLeavePoint : MonoBehaviour, IInteractable
    {
        public string Hint { get { return "下车 · 回到站台（E）"; } }
        public bool CanInteract { get { return true; } }

        public void Interact()
        {
            if (TrainRide.Instance != null) TrainRide.Instance.Leave();
        }
    }
}
