using System.Collections.Generic;
using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;
using MedusaHorror.UI;

namespace MedusaHorror.Player
{
    /// <summary>
    /// 近战武器（长剑）：鼠标左键挥砍，扇形多目标判定，命中给闪白 + 轻微抖动 + 粒子 + 挥砍音。
    /// 与 PlayerWeapon（远程枪）通过 WeaponLoadout 互斥切换，共用同一套输入（FirePressed）。
    /// </summary>
    [DisallowMultipleComponent]
    public class MeleeWeapon : MonoBehaviour
    {
        [Header("攻击参数")]
        [Tooltip("水平判定半径（米），以角色为圆心")]
        public float attackRange = 2.2f;
        [Tooltip("扇形总张角（度），角色正前方为中心")]
        [Range(10f, 180f)] public float attackAngle = 100f;
        public float damage = 55f;
        [Tooltip("两次挥砍最小间隔（秒）")]
        public float attackInterval = 0.55f;
        [Tooltip("单次挥砍最多命中目标数，>1 即支持多目标")]
        public int maxTargets = 4;
        [Tooltip("目标与角色的高度差上限（米）")]
        public float maxHeightDiff = 2.0f;
        [Tooltip("玩家所在层，判定时排除")]
        public int playerLayer = 8;

        [Header("表现")]
        public float swingDuration = 0.40f;
        [Tooltip("旧参数，已由相位剑身朝向取代，保留仅避免序列化丢失")]
        public float swingArc = 72f;
        public float shakeDegrees = 2.2f;
        public float shakeSeconds = 0.16f;

        [Header("引用")]
        public InputReader input;
        public Transform bladeRoot;   // 剑（做挥砍摆动 + 显隐）
        public Transform modelRoot;   // 持剑整体（切换武器时显隐）
        public CameraShake shake;
        public WeaponHud hud;
        public PlayerWeapon firearm;  // 关联远程枪，仅用于互斥
        [Tooltip("上身程序化动作（与剑体摆动共用相位）")]
        public MeleeSwingBody swingBody;

        [Header("开关")]
        public bool inputEnabled = true;

        // ---------------- 诊断用只读状态 ----------------
        public bool Ready { get { return Time.time >= _nextSwing; } }
        public float CooldownRemaining { get { return Mathf.Max(0f, _nextSwing - Time.time); } }
        public float Cooldown01
        {
            get { return attackInterval <= 0.001f ? 0f : Mathf.Clamp01(CooldownRemaining / attackInterval); }
        }
        public int SwingCount { get; private set; }
        public int TotalHits { get; private set; }
        public int LastSwingTargets { get; private set; }
        public float LastSwingDamage { get; private set; }
        public string LastSwingResult { get; private set; }
        public float LastSwingTime { get; private set; }
        public string LastSwingDiag = "";

#if UNITY_EDITOR || MARVIS_DEBUG
        /// <summary>诊断用：下一次 Update 强制挥砍一次（不依赖键鼠）。仅在编辑器/调试构建中存在。</summary>
        public static bool debugSwingPulse;

        /// <summary>诊断用：>0 时按攻击间隔自动连续挥砍，剩余秒数递减到 0 自动停止（用于实测攻速）。</summary>
        public static float debugAutoSwingSeconds;

        /// <summary>诊断用：>=0 时把挥砍定格在该相位（0~1），便于截图核对动作。</summary>
        public static float debugHoldPhase = -1f;
#endif

        TrailRenderer _trail;
        float _nextSwing;
        float _swingElapsed = -1f;
        Quaternion _bladeBaseRot;
        Vector3 _bladeBasePos;
        bool _bladeCaptured;

        void Awake()
        {
            if (LastSwingResult == null) LastSwingResult = "无";
            if (input == null) input = GetComponent<InputReader>();
            if (firearm == null) firearm = GetComponent<PlayerWeapon>();
            if (hud == null) hud = GetComponent<WeaponHud>();
            if (shake == null)
            {
                var cam = Camera.main;
                if (cam != null) shake = cam.GetComponent<CameraShake>();
            }
            if (swingBody == null) swingBody = GetComponent<MeleeSwingBody>();
            if (swingBody == null) swingBody = gameObject.AddComponent<MeleeSwingBody>();
            CaptureBlade();
            BuildTrail();
        }

        /// <summary>在剑尖挂一条拖尾，挥砍时显示剑光弧。</summary>
        void BuildTrail()
        {
            if (bladeRoot == null || _trail != null) return;
            var rends = bladeRoot.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Vector3 origin = bladeRoot.position;
            Vector3 tip = origin;
            float bestD = -1f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                float d = (corner - origin).sqrMagnitude;
                if (d > bestD) { bestD = d; tip = corner; }
            }

            var go = new GameObject("BladeTrail");
            go.transform.SetParent(bladeRoot, false);
            go.transform.position = tip;

            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.16f;
            tr.minVertexDistance = 0.02f;
            tr.startWidth = 0.11f;
            tr.endWidth = 0f;
            tr.numCapVertices = 2;
            tr.numCornerVertices = 2;
            tr.alignment = LineAlignment.View;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.62f, 0.88f, 1f), 0.55f),
                    new GradientColorKey(new Color(0.30f, 0.55f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            tr.colorGradient = grad;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) tr.material = new Material(sh);
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            _trail = tr;
        }

        void CaptureBlade()
        {
            if (bladeRoot == null || _bladeCaptured) return;
            _bladeBaseRot = bladeRoot.localRotation;
            _bladeBasePos = bladeRoot.localPosition;
            _bladeCaptured = true;
        }

        void Update()
        {
            UpdateSwingAnimation();

            var flow = GameFlow.Instance;
            bool flowRunning = flow == null || flow.IsRunning;

            bool fire = false;
            bool forced = false;
#if UNITY_EDITOR || MARVIS_DEBUG
            if (debugSwingPulse)
            {
                forced = true;
                fire = true;
                debugSwingPulse = false;
            }
            else if (debugAutoSwingSeconds > 0f && flowRunning)
            {
                debugAutoSwingSeconds -= Time.deltaTime;
                forced = true;
                fire = true;
            }
            else
#endif
            if (inputEnabled && input != null)
            {
                fire = flowRunning && input.FirePressed;
            }

            if (!fire) return;
            if (!forced && Cursor.lockState != CursorLockMode.Locked) return;
            if (Time.time < _nextSwing) return;
            Swing();
        }

        /// <summary>执行一次挥砍：扇形判定 + 命中/挥空反馈。</summary>
        public void Swing()
        {
            _nextSwing = Time.time + Mathf.Max(0.05f, attackInterval);
            SwingCount++;
            LastSwingTime = Time.time;
            _swingElapsed = 0f;
            CaptureBlade();
            if (_trail != null) { _trail.Clear(); _trail.emitting = true; }

            var targets = CollectTargets();
            LastSwingTargets = targets.Count;

            var audio = AudioDirector.Instance;
            Vector3 pivot = transform.position + Vector3.up * 1.05f;

            if (targets.Count == 0)
            {
                LastSwingDamage = 0f;
                LastSwingResult = "挥空";
                if (swingBody != null) swingBody.BeginSwing(swingDuration, false);
                if (audio != null) audio.Play(SfxId.SlashSwing, 0.62f, Random.Range(0.94f, 1.06f));
                if (hud != null) hud.ShowSwingResult("挥空", false);
                return;
            }

            float dealt = 0f;
            int hitCount = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var boss = targets[i];
                if (boss == null || boss.IsDead) continue;

                Vector3 point = boss.transform.position + Vector3.up * 1.0f;
                Vector3 hitPoint = Vector3.Lerp(pivot, point, 0.75f);

                float dmg = damage * MedusaHorror.Core.PlayerStats.DamageMultiplier;
                boss.TakeDamage(dmg);
                dealt += dmg;
                hitCount++;
                TotalHits++;

                SpawnHitParticles(hitPoint);
            }

            LastSwingDamage = dealt;
            LastSwingResult = hitCount > 0 ? ("命中 " + hitCount + " 个目标 · " + dealt.ToString("0") + " 伤害") : "挥空";
            if (swingBody != null) swingBody.BeginSwing(swingDuration, hitCount > 0);

            if (audio != null)
            {
                audio.Play(SfxId.SlashHit, 0.88f, Random.Range(0.95f, 1.08f));
                if (hitCount == 0) audio.Play(SfxId.SlashSwing, 0.5f, 1f);
            }

            if (hitCount > 0)
            {
                if (shake != null) shake.Shake(shakeDegrees, shakeSeconds);
                if (hud != null) hud.FlashWhite(hitCount > 0 ? 0.22f : 0f);
                if (hud != null) hud.ShowSwingResult(LastSwingResult, true);
            }
            else
            {
                if (hud != null) hud.ShowSwingResult("挥空", false);
            }
        }

        /// <summary>扇形范围内的所有 Boss（去重、按距离排序、受 maxTargets 限制）。</summary>
        List<MedusaBoss> CollectTargets()
        {
            var result = new List<MedusaBoss>();
            Vector3 origin = transform.position;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            var cols = Physics.OverlapSphere(origin + Vector3.up * 1.05f, attackRange + 0.8f,
                ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<MedusaBoss>();
            var scored = new List<KeyValuePair<float, MedusaBoss>>();
            string diag = "cols=" + cols.Length + " pos=" + origin.ToString("F2") + " fwd=" + fwd.ToString("F2")
                + " r=" + (attackRange + 0.8f).ToString("F2");

            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;
                // 玩家与 Boss 同处 playerLayer，不能按 layer 过滤，改为排除玩家自身层级
                if (c.transform.root == transform.root) continue;

                var boss = c.GetComponentInParent<MedusaBoss>();
                if (boss == null || boss.IsDead) continue;
                if (seen.Contains(boss)) continue;

                Vector3 to = boss.transform.position - origin;
                if (Mathf.Abs(to.y) > maxHeightDiff) continue;
                to.y = 0f;
                float dist = to.magnitude;
                diag += " |" + boss.name + " d=" + dist.ToString("F2");
                if (dist > attackRange) { diag += ">R"; continue; }
                if (dist < 0.05f) { seen.Add(boss); scored.Add(new KeyValuePair<float, MedusaBoss>(0f, boss)); continue; }

                float ang = Vector3.Angle(fwd, to);
                diag += " a=" + ang.ToString("F1");
                if (ang > attackAngle * 0.5f) { diag += ">A"; continue; }

                seen.Add(boss);
                scored.Add(new KeyValuePair<float, MedusaBoss>(dist, boss));
            }

            scored.Sort((a, b) => a.Key.CompareTo(b.Key));
            int limit = Mathf.Max(1, maxTargets);
            for (int i = 0; i < scored.Count && i < limit; i++) result.Add(scored[i].Value);
            LastSwingDiag = diag + " picked=" + result.Count;
            return result;
        }

        /// <summary>命中粒子：程序化粒子系统，播放一次后销毁。</summary>
        void SpawnHitParticles(Vector3 position)
        {
            var go = new GameObject("MeleeHitFx");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.22f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.92f, 0.72f, 1f), new Color(1f, 0.42f, 0.28f, 1f));
            main.gravityModifier = 0.55f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            ps.Play();
            Object.Destroy(go, 1.6f);
        }

        /// <summary>剑体挥砍摆动：沿本地 Z 轴（横向扫）在挥砍时间内完成一次弧线。</summary>
        void UpdateSwingAnimation()
        {
            if (_swingElapsed < 0f) return;
            if (bladeRoot == null) { _swingElapsed = -1f; return; }

            if (!_bladeCaptured) CaptureBlade();

            _swingElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(_swingElapsed / Mathf.Max(0.05f, swingDuration));
            bool hold = false;
#if UNITY_EDITOR || MARVIS_DEBUG
            if (debugHoldPhase >= 0f) { u = Mathf.Clamp01(debugHoldPhase); hold = true; }
#endif

            // 与 MeleeSwingBody 同相位的三段式：0~0.30 蓄力举剑，0.30~0.58 斜劈，0.58~1 收招
            float q;
            if (u < 0.30f) q = -EaseInOut(u / 0.30f);
            else if (u < 0.58f) q = Mathf.Lerp(-1f, 1f, EaseInOut((u - 0.30f) / 0.28f));
            else q = Mathf.Lerp(1f, 0f, EaseInOut((u - 0.58f) / 0.42f));

            // 剑身朝向按相位在世界空间直接给定（角色本地空间：x=右, y=上, z=前）
            // 起止点用「手骨当前姿态下的原始剑向」，保证起手/收招与待机姿势无缝衔接
            Vector3 restW = bladeRoot.parent != null
                ? bladeRoot.parent.rotation * (_bladeBaseRot * Vector3.forward)
                : transform.TransformDirection(RestDir);
            Vector3 windW = transform.TransformDirection(WindDir).normalized;
            Vector3 hitW = transform.TransformDirection(HitDir).normalized;

            Vector3 dir;
            if (u < 0.30f) dir = Vector3.Slerp(restW, windW, EaseInOut(u / 0.30f));
            else if (u < 0.58f) dir = Vector3.Slerp(windW, hitW, EaseInOut((u - 0.30f) / 0.28f));
            else dir = Vector3.Slerp(hitW, restW, EaseInOut((u - 0.58f) / 0.42f));
            bladeRoot.rotation = LookDir(dir, transform.up);

            // 上身动作使用同一相位 u，保证与剑体摆动严格同步
            if (swingBody != null) swingBody.SetSwingPhase(u);

            if (u >= 1f && !hold)
            {
                bladeRoot.localRotation = _bladeBaseRot;
                bladeRoot.localPosition = _bladeBasePos;
                if (_trail != null) _trail.emitting = false;
                _swingElapsed = -1f;
            }
        }

#if UNITY_EDITOR || MARVIS_DEBUG
        /// <summary>诊断用：把挥砍定格在相位 u。</summary>
        public void HoldSwing(float u)
        {
            if (bladeRoot == null) return;
            CaptureBlade();
            debugHoldPhase = Mathf.Clamp01(u);
            _swingElapsed = debugHoldPhase * Mathf.Max(0.05f, swingDuration);
            if (_trail != null) _trail.emitting = false;
        }

        /// <summary>诊断用：解除定格。</summary>
        public void ReleaseHold()
        {
            debugHoldPhase = -1f;
            _swingElapsed = -1f;
            if (bladeRoot != null)
            {
                bladeRoot.localRotation = _bladeBaseRot;
                bladeRoot.localPosition = _bladeBasePos;
            }
        }
#endif

        static readonly Vector3 RestDir = new Vector3(0.16f, 0.96f, 0.22f).normalized;
        static readonly Vector3 WindDir = new Vector3(0.44f, 0.80f, -0.42f).normalized;
        static readonly Vector3 HitDir = new Vector3(-0.26f, -0.34f, 0.90f).normalized;

        /// <summary>让物体的本地 +Z 指向 dir，roll 由 upHint 决定。</summary>
        static Quaternion LookDir(Vector3 dir, Vector3 upHint)
        {
            Vector3 z = dir.normalized;
            Vector3 x = Vector3.Cross(upHint, z);
            if (x.sqrMagnitude < 1e-4f) x = Vector3.Cross(Vector3.forward, z);
            x.Normalize();
            Vector3 y = Vector3.Cross(z, x);
            return Quaternion.LookRotation(z, y);
        }

        static float EaseInOut(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>切换武器时由 WeaponLoadout 调用。</summary>
        public void SetActiveState(bool active)
        {
            inputEnabled = active;
            if (modelRoot != null) modelRoot.gameObject.SetActive(active);
            else if (bladeRoot != null) bladeRoot.gameObject.SetActive(active);
            if (!active)
            {
                if (_trail != null) _trail.emitting = false;
                _swingElapsed = -1f;
                if (swingBody != null) swingBody.Cancel();
            }
        }
    }
}
