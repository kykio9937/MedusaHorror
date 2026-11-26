using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// W3 列车：一节可以自由走动的科幻车厢，窗外是不断后退的霓虹城景与光轨。
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
        public float rideDuration = 12f;       // 上车后多久到站
        public string nextScene = "W4_NeonStreet";

        [Header("落点")]
        public Vector3 boardPoint = new Vector3(0f, 0.15f, 3f);   // 相对车厢中心
        public Vector3 leavePoint = new Vector3(0f, 0.15f, 10.6f);

        public bool Riding { get; private set; }
        public Vector3 StationReturn { get; set; }

        Transform _cabin;
        Transform _sceneryRoot;
        Transform[] _poles;
        Transform[] _city;
        Transform[] _arches;
        Transform _player;
        Transform _holoRoot;
        Transform _passTrain;
        float _passZ;
        float _time;
        bool _built;
        float _rideTime;
        bool _arriving;

        static readonly Color HullDark = new Color(0.075f, 0.085f, 0.105f);
        static readonly Color HullMid = new Color(0.145f, 0.165f, 0.205f);
        static readonly Color HullLight = new Color(0.30f, 0.34f, 0.40f);
        static readonly Color NeonCyan = new Color(0.20f, 0.92f, 1.00f);
        static readonly Color NeonBlue = new Color(0.22f, 0.55f, 1.00f);
        static readonly Color NeonSoft = new Color(0.72f, 0.90f, 1.00f);
        static readonly Color SeatColor = new Color(0.12f, 0.135f, 0.175f);

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

            var hull = Mat(HullDark, 0f, 1f, 0.72f, 0.55f);
            var panel = Mat(HullMid, 0f, 1f, 0.62f, 0.40f);
            var rib = Mat(HullLight, 0f, 1f, 0.68f, 0.65f);
            var cyan = Mat(NeonCyan, 2.8f, 1f, 0.5f, 0.2f);
            var blue = Mat(NeonBlue, 2.0f, 1f, 0.5f, 0.2f);
            var soft = Mat(NeonSoft, 1.6f, 1f, 0.5f, 0.2f);
            var seat = Mat(SeatColor, 0f, 1f, 0.45f, 0.15f);

            // 地板 / 天花板
            Slab("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(width, 0.1f, length), hull, _cabin);
            Slab("Ceiling", new Vector3(0f, height, 0f), new Vector3(width, 0.1f, length), hull, _cabin);

            // 地板两条贯穿灯带
            for (int side = -1; side <= 1; side += 2)
                Slab("FloorGlow" + side, new Vector3(side * (width * 0.5f - 0.42f), 0.004f, 0f),
                    new Vector3(0.09f, 0.01f, length - 0.8f), cyan, _cabin, false);

            // 中央顶灯长条 + 两侧暖色副灯
            Slab("CeilStrip", new Vector3(0f, height - 0.055f, 0f),
                new Vector3(0.42f, 0.03f, length - 1.2f), cyan, _cabin, false);
            for (int side = -1; side <= 1; side += 2)
                Slab("CeilStrip2" + side, new Vector3(side * 1.55f, height - 0.055f, 0f),
                    new Vector3(0.10f, 0.03f, length - 1.6f), soft, _cabin, false);

            // 顶部横向加强肋
            for (float z = -length * 0.5f + 1.5f; z < length * 0.5f - 1f; z += 3f)
                Slab("CeilRib_" + Mathf.RoundToInt(z), new Vector3(0f, height - 0.09f, z),
                    new Vector3(width - 0.5f, 0.10f, 0.14f), rib, _cabin, false);

            // 两侧：下部装甲 + 上部大窗 + 窗框灯带
            float winBottom = 0.95f, winTop = 2.42f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (width * 0.5f);

                Slab("HullLow" + side, new Vector3(x, winBottom * 0.5f, 0f),
                    new Vector3(0.12f, winBottom, length), hull, _cabin);
                Slab("HullHigh" + side, new Vector3(x, (winTop + height) * 0.5f, 0f),
                    new Vector3(0.12f, height - winTop, length), hull, _cabin);

                Slab("Skirt" + side, new Vector3(x + side * 0.09f, 0.42f, 0f),
                    new Vector3(0.06f, 0.72f, length - 0.4f), panel, _cabin, false);

                Slab("TrimLow" + side, new Vector3(x - side * 0.08f, winBottom - 0.06f, 0f),
                    new Vector3(0.06f, 0.07f, length - 0.5f), cyan, _cabin, false);
                Slab("TrimHigh" + side, new Vector3(x - side * 0.08f, winTop + 0.06f, 0f),
                    new Vector3(0.05f, 0.05f, length - 0.5f), blue, _cabin, false);

                int bays = 8;
                float seg = length / bays;
                for (int i = 0; i <= bays; i++)
                {
                    float z = -length * 0.5f + i * seg;
                    Slab("Mullion" + side + "_" + i, new Vector3(x - side * 0.04f, (winBottom + winTop) * 0.5f, z),
                        new Vector3(0.16f, winTop - winBottom + 0.1f, 0.18f), rib, _cabin);
                }

                var glass = Slab("Glass" + side, new Vector3(x, (winBottom + winTop) * 0.5f, 0f),
                    new Vector3(0.04f, winTop - winBottom, length - 0.4f),
                    Mat(new Color(0.62f, 0.88f, 1.00f), 0f, 0.09f, 0.9f), _cabin, false);
                Object.Destroy(glass.GetComponent<Collider>());
            }

            // 前后端墙 + 气密门
            for (int end = -1; end <= 1; end += 2)
            {
                float z = end * length * 0.5f;
                Slab("EndWall" + end, new Vector3(0f, height * 0.5f, z),
                    new Vector3(width, height, 0.14f), hull, _cabin);
                Slab("EndDoorFrame" + end, new Vector3(0f, 1.15f, z - end * 0.09f),
                    new Vector3(1.5f, 2.3f, 0.06f), rib, _cabin, false);
                Slab("EndDoorGlow" + end, new Vector3(0f, 1.15f, z - end * 0.13f),
                    new Vector3(1.24f, 2.14f, 0.03f), cyan, _cabin, false);
                for (int leaf = -1; leaf <= 1; leaf += 2)
                    Slab("EndDoorLeaf" + end + "_" + leaf,
                        new Vector3(leaf * 0.33f, 1.15f, z - end * 0.16f),
                        new Vector3(0.62f, 2.10f, 0.05f), panel, _cabin, false);
                Slab("EndScreen" + end, new Vector3(0f, 2.72f, z - end * 0.12f),
                    new Vector3(1.05f, 0.30f, 0.03f), blue, _cabin, false);
            }

            // 座椅：两侧悬浮式座椅
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (width * 0.5f - 0.58f);
                for (float z = -length * 0.5f + 2.2f; z < length * 0.5f - 1.6f; z += 2.1f)
                {
                    Slab("SeatBase" + side + "_" + z, new Vector3(x, 0.44f, z),
                        new Vector3(0.92f, 0.14f, 1.5f), seat, _cabin);
                    Slab("SeatBack" + side + "_" + z, new Vector3(x + side * 0.40f, 0.86f, z),
                        new Vector3(0.14f, 0.92f, 1.5f), seat, _cabin);
                    Slab("SeatGlow" + side + "_" + z, new Vector3(x - side * 0.42f, 0.40f, z),
                        new Vector3(0.05f, 0.05f, 1.42f), cyan, _cabin, false);
                    Slab("SeatLeg" + side + "_" + z, new Vector3(x + side * 0.2f, 0.20f, z),
                        new Vector3(0.14f, 0.40f, 0.9f), rib, _cabin, false);
                }
            }

            // 中央扶手立杆 + 顶部滑轨
            var barMat = Mat(HullLight, 0f, 1f, 0.85f, 0.9f);
            for (int side = -1; side <= 1; side += 2)
            {
                Slab("Rail" + side, new Vector3(side * 1.12f, height - 0.42f, 0f),
                    new Vector3(0.06f, 0.06f, length - 1.2f), barMat, _cabin, false);
                Slab("RailGlow" + side, new Vector3(side * 1.12f, height - 0.50f, 0f),
                    new Vector3(0.03f, 0.02f, length - 1.4f), blue, _cabin, false);
                for (float z = -length * 0.5f + 2.5f; z < length * 0.5f - 1f; z += 4.5f)
                    Slab("RailPost" + side + "_" + Mathf.RoundToInt(z),
                        new Vector3(side * 1.12f, height - 0.20f, z),
                        new Vector3(0.06f, 0.42f, 0.06f), barMat, _cabin, false);
            }

            // 顶灯点光源
            for (float z = -length * 0.5f + 3f; z < length * 0.5f; z += 6f)
            {
                var lightGo = new GameObject("CeilLight");
                lightGo.transform.SetParent(_cabin, false);
                lightGo.transform.localPosition = new Vector3(0f, height - 0.2f, z);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.intensity = 1.15f;
                l.color = new Color(0.72f, 0.94f, 1f);
                l.shadows = LightShadows.None;
            }

            // 车厢中央的全息环
            _holoRoot = new GameObject("HoloRing").transform;
            _holoRoot.SetParent(_cabin, false);
            _holoRoot.localPosition = new Vector3(0f, 2.45f, 0f);
            int segs = 20;
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                var seg = Slab("HoloSeg" + i,
                    new Vector3(Mathf.Cos(a) * 1.05f, 0f, Mathf.Sin(a) * 1.05f),
                    new Vector3(0.06f, 0.02f, 0.34f), cyan, _holoRoot, false);
                seg.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            Slab("HoloCore", new Vector3(0f, 0.55f, 0f), new Vector3(0.16f, 0.5f, 0.16f), blue, _holoRoot, false);
            Slab("HoloBeam", new Vector3(0f, -0.9f, 0f), new Vector3(0.05f, 1.8f, 0.05f),
                Mat(NeonCyan, 1.2f, 0.35f), _holoRoot, false);

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

            // 窗外：霓虹楼块（几何体，透过窗户一定看得见）
            var bldMat = Mat(new Color(0.045f, 0.05f, 0.075f), 0f, 1f, 0.45f, 0.35f);
            var winCyan = Mat(NeonCyan, 2.4f);
            var winBlue = Mat(NeonBlue, 2.2f);
            var winSoft = Mat(NeonSoft, 2.0f);
            var beaconMat = Mat(new Color(1f, 0.30f, 0.28f), 2.6f);

            _city = new Transform[24];
            var cityRnd = new System.Random(90210);
            for (int i = 0; i < _city.Length; i++)
            {
                var b = new GameObject("Bld" + i).transform;
                b.SetParent(_sceneryRoot, false);
                int side = (i % 2 == 0) ? -1 : 1;
                float h = 6f + (float)cityRnd.NextDouble() * 12f;
                float w = 5f + (float)cityRnd.NextDouble() * 5f;
                b.localPosition = new Vector3(side * (11.5f + (float)cityRnd.NextDouble() * 6f), h * 0.5f, -168f + i * 14f);

                Slab("Body", Vector3.zero, new Vector3(w, h, w), bldMat, b, false);

                var stripMat = cityRnd.Next(3) == 0 ? winSoft : (cityRnd.Next(2) == 0 ? winCyan : winBlue);
                for (int k = 0; k < 2; k++)
                {
                    float sx = (k == 0 ? -1f : 1f) * (w * 0.5f + 0.02f) * (side < 0 ? 1f : -1f);
                    Slab("Strip" + k, new Vector3(sx, 0f, (float)(cityRnd.NextDouble() - 0.5) * w * 0.6f),
                        new Vector3(0.06f, h * 0.85f, 0.35f), stripMat, b, false);
                }
                Slab("Beacon", new Vector3(0f, h * 0.5f + 0.15f, 0f),
                    new Vector3(0.5f, 0.3f, 0.5f), beaconMat, b, false);

                _city[i] = b;
            }

            // 隧道光环：整圈发光框，从头顶掠过，速度感主要靠它
            _arches = new Transform[7];
            var archMat = Mat(NeonBlue, 2.6f);
            for (int i = 0; i < _arches.Length; i++)
            {
                var a = new GameObject("Arch" + i).transform;
                a.SetParent(_sceneryRoot, false);
                a.localPosition = new Vector3(0f, 0f, -56f + i * 16f);
                Slab("Top", new Vector3(0f, 7.2f, 0f), new Vector3(15f, 0.22f, 0.35f), archMat, a, false);
                Slab("L", new Vector3(-7.4f, 3.6f, 0f), new Vector3(0.22f, 7.2f, 0.35f), archMat, a, false);
                Slab("R", new Vector3(7.4f, 3.6f, 0f), new Vector3(0.22f, 7.2f, 0.35f), archMat, a, false);
                _arches[i] = a;
            }

            // 近景：发光支撑塔柱，循环后退
            int poleCount = 14;
            _poles = new Transform[poleCount];
            var poleMat = Mat(new Color(0.10f, 0.11f, 0.14f), 0f, 1f, 0.8f, 0.6f);
            var poleGlow = Mat(NeonBlue, 2.2f);
            for (int i = 0; i < poleCount; i++)
            {
                var root = new GameObject("Pylon" + i).transform;
                root.SetParent(_sceneryRoot, false);
                int side = (i % 2 == 0) ? -1 : 1;
                root.localPosition = new Vector3(side * 7.4f, 0f, -length * 0.5f + i * 3.4f);

                Slab("Body", new Vector3(0f, 2.6f, 0f), new Vector3(0.55f, 5.2f, 0.55f), poleMat, root, false);
                Slab("Glow", new Vector3(0f, 2.6f, -0.30f), new Vector3(0.10f, 4.6f, 0.06f), poleGlow, root, false);
                Slab("Cap", new Vector3(0f, 5.3f, 0f), new Vector3(0.9f, 0.12f, 0.9f), poleGlow, root, false);
                _poles[i] = root;
            }

            // 对面轨道上驶过的第二辆列车
            BuildPassingTrain();
        }

        /// <summary>窗外掠过的另一辆列车：对面轨道，速度更快。</summary>
        void BuildPassingTrain()
        {
            var root = new GameObject("PassingTrain").transform;
            root.SetParent(_sceneryRoot, false);

            var hull = Mat(new Color(0.09f, 0.10f, 0.13f), 0f, 1f, 0.8f, 0.6f);
            var win = Mat(new Color(0.35f, 0.85f, 1f), 2.2f);

            float len = 26f, w = 3.0f, h = 3.0f;
            Slab("Hull", new Vector3(0f, 1.6f, 0f), new Vector3(w, h, len), hull, root, false);
            Slab("Nose", new Vector3(0f, 1.6f, len * 0.5f + 0.4f), new Vector3(w * 0.8f, h * 0.8f, 0.9f), hull, root, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Slab("WinBand" + side, new Vector3(side * (w * 0.5f + 0.01f), 2.05f, 0f),
                    new Vector3(0.03f, 0.42f, len - 2.5f), win, root, false);
                Slab("UnderGlow" + side, new Vector3(side * (w * 0.5f + 0.01f), 0.45f, 0f),
                    new Vector3(0.03f, 0.08f, len - 1f), Mat(NeonCyan, 2.4f), root, false);
            }
            Slab("HeadLight", new Vector3(0f, 1.4f, len * 0.5f + 0.86f), new Vector3(1.4f, 0.22f, 0.06f),
                Mat(new Color(1f, 0.95f, 0.85f), 3.2f), root, false);

            root.localPosition = new Vector3(10.5f, 0f, 40f);
            _passTrain = root;
            _passZ = 40f;
        }

        static Material Mat(Color c, float emission, float alpha = 1f, float smooth = 0.1f, float metallic = 0f)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse"));
            m.color = new Color(c.r, c.g, c.b, alpha);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (metallic > 0f && m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
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
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) { c.enabled = false; Object.Destroy(c); }
            }
            return go;
        }

        // ------------------------------------------------------------------ 上下车

        public void Board()
        {
            var pz = Object.FindObjectOfType<PlayerPetrify>();
            if (pz == null) return;
            _player = pz.transform;
            Riding = true;
            _rideTime = 0f;
            _arriving = false;
            TeleportPlayer(transform.TransformPoint(boardPoint));
            AudioDirector.Instance?.Play(SfxId.DoorOpen, 0.7f, 0.9f);
            UI.StoryBanner.Show("列车启动", "气密门合拢，窗外的霓虹被拉成一道道流光。", 3.2f);
        }

        public void Leave()
        {
            Riding = false;
            _rideTime = 0f;
            _arriving = false;
            TeleportPlayer(StationReturn);
            AudioDirector.Instance?.Play(SfxId.DoorClose, 0.7f, 1f);
        }

        /// <summary>到站：淡出后切到下一关（霓虹街区）。</summary>
        System.Collections.IEnumerator ArriveRoutine()
        {
            UI.StoryBanner.Show("到站", "列车减速，霓虹街区的灯牌在车窗外铺开。", 2.6f);
            yield return new WaitForSeconds(2.6f);
            if (Riding) SceneLoader.Load(nextScene, "霓虹街区");
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

            // 窗外楼群后退
            if (_city != null)
            {
                float span = _city.Length * 14f;
                for (int i = 0; i < _city.Length; i++)
                {
                    if (_city[i] == null) continue;
                    var p = _city[i].localPosition;
                    p.z -= 26f * Time.deltaTime;
                    if (p.z < -span * 0.5f) p.z += span;
                    _city[i].localPosition = p;
                }
            }
            // 隧道光环掠过
            if (_arches != null)
            {
                float span = _arches.Length * 16f;
                for (int i = 0; i < _arches.Length; i++)
                {
                    if (_arches[i] == null) continue;
                    var p = _arches[i].localPosition;
                    p.z -= 40f * Time.deltaTime;
                    if (p.z < -span * 0.5f) p.z += span;
                    _arches[i].localPosition = p;
                }
            }

            // 近景塔柱循环后退
            if (_poles != null)
            {
                float span = _poles.Length * 3.4f;
                for (int i = 0; i < _poles.Length; i++)
                {
                    if (_poles[i] == null) continue;
                    var p = _poles[i].localPosition;
                    p.z -= 30f * Time.deltaTime;
                    if (p.z < -length * 0.5f - 8f) p.z += span;
                    _poles[i].localPosition = p;
                }
            }

            // 对面轨道列车掠过
            if (_passTrain != null)
            {
                _passZ -= 46f * Time.deltaTime;
                if (_passZ < -70f) _passZ = 70f;
                _passTrain.localPosition = new Vector3(10.5f, 0f, _passZ);
            }

            // 全息环旋转
            if (_holoRoot != null)
                _holoRoot.localRotation = Quaternion.Euler(0f, _time * 42f, 0f);

            // 车厢轻微晃动
            if (_cabin != null && Riding)
            {
                float s = Mathf.Sin(_time * 9.3f) * shakeAmount + Mathf.Sin(_time * 3.1f) * shakeAmount * 0.5f;
                _cabin.localPosition = new Vector3(0f, s, 0f);
                _cabin.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_time * 5.7f) * 0.25f);
            }

            // 到站判定
            if (Riding && !_arriving)
            {
                _rideTime += Time.deltaTime;
                if (_rideTime >= rideDuration)
                {
                    _arriving = true;
                    StartCoroutine(ArriveRoutine());
                }
            }
        }
    }

    /// <summary>站台上的车门：按 E 上车。</summary>
    public class TrainBoardPoint : MonoBehaviour, IInteractable
    {
        public Vector3 stationReturn;
        public int fare = 200;

        public string Hint
        {
            get
            {
                if (TrainRide.Instance != null && TrainRide.Instance.Riding) return "列车 · 已在车上";
                  if (fare <= 0) return "列车 · 免费前往霓虹街区（E）";
                  if (Wallet.CanAfford(fare)) return "列车 · 上车（" + fare + " 信用点，E）";
                  return "列车 · 车票 " + fare + " 信用点，还差 " + Mathf.Max(0, fare - Wallet.Coins) + " · 可探索站台赚取";
            }
        }

        public bool CanInteract { get { return TrainRide.Instance == null || !TrainRide.Instance.Riding; } }

        public void Interact()
        {
            if (TrainRide.Instance != null && TrainRide.Instance.Riding) return;
            if (!Wallet.Spend(fare))
            {
                UI.StoryBanner.Show("信用点不足", "车票 " + fare + " 信用点，还差 " + Mathf.Max(0, fare - Wallet.Coins) + "。可先探索站台遭遇，赚取车费。", 3.5f);
                return;
            }
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
