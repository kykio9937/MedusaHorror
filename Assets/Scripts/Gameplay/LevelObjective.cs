using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Core;
using MedusaHorror.UI;

namespace MedusaHorror.Gameplay
{
    public enum GoalKind
    {
        Reach,      // 走到目标点
        Collect,    // 拾取，拾取后消失
        Activate    // 激活装置，激活后保留
    }

    /// <summary>
    /// 单个目标点：玩家进入半径即触发。
    /// </summary>
    public class LevelGoalPoint : MonoBehaviour
    {
        public GoalKind kind = GoalKind.Reach;
        public float radius = 2.2f;
        public string label = "";
        public bool consumed;
        /// <summary>必需目标：全部达成才算本关目标完成；非必需的点只计入探索进度。</summary>
        public bool required = true;
        public bool manualCompletion;
        public bool hideWorldMarker;

        [HideInInspector] public LevelObjective owner;

        LineRenderer _ring;
        LineRenderer _beam;
        float _pulse;

        void OnEnable()
        {
            if (owner != null) owner.Register(this);
            BuildMarker();
        }

        void Update()
        {
            if(hideWorldMarker){if(_ring!=null)_ring.enabled=false;if(_beam!=null)_beam.enabled=false;return;}
            if (consumed) return;
            _pulse += Time.deltaTime * 2.2f;
            float a = 0.30f + Mathf.Sin(_pulse) * 0.12f;
            Color c = required ? new Color(1f, 0.80f, 0.36f, a) : new Color(0.55f, 0.82f, 1f, a);
            if (_ring != null) { _ring.startColor = c; _ring.endColor = c; }
            if (_beam != null) { _beam.startColor = c; _beam.endColor = new Color(c.r, c.g, c.b, a * 0.4f); }
        }

        void BuildMarker()
        {
            if(hideWorldMarker)return;
            if (_ring != null) return;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.renderQueue = 3000;

            // 地面圆环
            _ring = MakeLine("GoalRing", mat, 0.10f);
            const int seg = 36;
            _ring.loop = true;
            _ring.positionCount = seg;
            float r = Mathf.Max(0.8f, radius * 0.85f);
            for (int i = 0; i < seg; i++)
            {
                float ang = i * Mathf.PI * 2f / seg;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, -0.85f, Mathf.Sin(ang) * r));
            }

            // 竖直光柱
            _beam = MakeLine("GoalBeam", mat, 0.16f);
            _beam.positionCount = 2;
            _beam.SetPosition(0, new Vector3(0f, -0.85f, 0f));
            _beam.SetPosition(1, new Vector3(0f, 1.30f, 0f));
        }

        LineRenderer MakeLine(string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = mat;
            lr.useWorldSpace = false;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            return lr;
        }

        public void Consume()
        {
            if (consumed) return;
            consumed = true;

            // 达成后标记变绿并收窄，给玩家一个明确的反馈
            var ok = new Color(0.45f, 0.95f, 0.55f, 0.55f);
            if (_ring != null) { _ring.startColor = ok; _ring.endColor = ok; }
            if (_beam != null) { _beam.startColor = ok; _beam.endColor = ok; }

            if (kind == GoalKind.Collect)
            {
                var rends = GetComponentsInChildren<Renderer>();
                for (int i = 0; i < rends.Length; i++) rends[i].enabled = false;
                var cols = GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 关卡目标：统计场景内目标点，玩家逐个达成后完成本关目标并写入存档。
    /// 每关一个实例，由关卡生成器放置。
    /// </summary>
    public class LevelObjective : MonoBehaviour
    {
        public string objectiveTitle = "探索区域";
        public bool autoFindPoints = true;

        readonly List<LevelGoalPoint> _points = new List<LevelGoalPoint>();
        int _progress;
        bool _done;
        float _startTime;
        Transform _player;

        public static LevelObjective Active { get; private set; }

        void Awake()
        {
            Active = this;
            _startTime = Time.time;
            if (autoFindPoints) RefreshPoints();
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        public void RefreshPoints()
        {
            _points.Clear();
            var found = GetComponentsInChildren<LevelGoalPoint>(true);
            for (int i = 0; i < found.Length; i++)
            {
                found[i].owner = this;
                _points.Add(found[i]);
            }
            // 场景里不在本对象下的目标点也纳入统计
            var all = Object.FindObjectsOfType<LevelGoalPoint>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].owner == null) all[i].owner = this;
                if (!_points.Contains(all[i])) _points.Add(all[i]);
            }
            Debug.Log("[LevelObjective] 目标点数量=" + _points.Count + " 标题=" + objectiveTitle);
        }

        public void Register(LevelGoalPoint p)
        {
            if (p != null && !_points.Contains(p)) { p.owner = this; _points.Add(p); }
        }

        void Start()
        {
            if (_points.Count == 0 && autoFindPoints) RefreshPoints();
            var hud = ObjectiveHud.Ensure();
            hud.Show(objectiveTitle, 0, _points.Count);

            var flow = GameFlow.Instance;
            if (flow != null) flow.BeginLevelTimer();

            // 一次性诊断：玩家出生点与最近目标点的距离，便于确认目标点没有落在不可达位置
            var pl = Player;
            if (pl != null && _points.Count > 0)
            {
                var nearest = _points[0];
                float best = float.MaxValue;
                for (int i = 0; i < _points.Count; i++)
                {
                    if (_points[i] == null) continue;
                    float d = Vector3.Distance(pl.position, _points[i].transform.position);
                    if (d < best) { best = d; nearest = _points[i]; }
                }
                Debug.Log("[LevelObjective] 玩家=" + pl.position.ToString("F1") +
                          " 最近点=" + (nearest != null ? nearest.label : "?") +
                          " 距离=" + best.ToString("F1"));
            }
        }

        Transform Player
        {
            get
            {
                if (_player != null) return _player;
                var ctrl = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
                if (ctrl != null) _player = ctrl.transform;
                return _player;
            }
        }

        void Update()
        {
            if (_done) return;
            var p = Player;
            if (p == null) return;

            for (int i = 0; i < _points.Count; i++)
            {
                var pt = _points[i];
                if (pt == null || pt.consumed || pt.manualCompletion) continue;
                if (Mathf.Abs(p.position.y - pt.transform.position.y) > 3f) continue;
                float d = Vector3.Distance(new Vector3(p.position.x, 0f, p.position.z),
                                           new Vector3(pt.transform.position.x, 0f, pt.transform.position.z));
                if (d <= pt.radius)
                {
                    CompletePoint(pt);
                }
            }

            if (_points.Count > 0 && AllRequiredReached()) Finish();
        }

        public void CompletePoint(LevelGoalPoint point)
        {
            if (point == null || point.consumed || !_points.Contains(point)) return;
            point.Consume();
            _progress++;
            ObjectiveHud.Instance?.SetProgress(_progress, _points.Count);
            AudioDirector.Instance?.Play(SfxId.Pickup, 0.8f, 1.05f);
            if (_points.Count > 0 && AllRequiredReached()) Finish();
        }

        bool AllRequiredReached()
        {
            for (int i = 0; i < _points.Count; i++)
            {
                var pt = _points[i];
                if (pt != null && pt.required && !pt.consumed) return false;
            }
            return true;
        }

        void Finish()
        {
            if (_done) return;
            _done = true;

            var hud = ObjectiveHud.Instance;
            if (hud != null) hud.Complete();

            float elapsed = Time.time - _startTime;
            var scene = SceneManager.GetActiveScene().name;
            SaveSystem.MarkCleared(scene, elapsed);
            Debug.Log("[LevelObjective] 本关目标完成，用时 " + elapsed.ToString("F1") + " 秒，已写入存档");

            var flow = GameFlow.Instance;
            if (flow != null) flow.EndLevelTimer();
        }

        /// <summary>本关是否已完成。</summary>
        public bool IsDone { get { return _done; } }

        /// <summary>下一个待达成目标：优先最近的必需点，没有则取最近的任意未达成点。</summary>
        public LevelGoalPoint NextTarget()
        {
            var p = Player;
            for (int pass = 0; pass < 2; pass++)
            {
                LevelGoalPoint best = null;
                float bestD = float.MaxValue;
                for (int i = 0; i < _points.Count; i++)
                {
                    var pt = _points[i];
                    if (pt == null || pt.consumed) continue;
                    if (pass == 0 && !pt.required) continue;
                    float d = p != null ? Vector3.Distance(p.position, pt.transform.position) : 0f;
                    if (d < bestD) { bestD = d; best = pt; }
                }
                if (best != null) return best;
            }
            return null;
        }

        /// <summary>诊断：目标状态。</summary>
        public string Diag()
        {
            return "title=" + objectiveTitle + " points=" + _points.Count + " progress=" + _progress + " done=" + _done;
        }
    }
}
