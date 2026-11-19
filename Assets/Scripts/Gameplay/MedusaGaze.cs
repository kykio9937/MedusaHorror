using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// W1 Medusa 凝视机制（原型）：在视线锥内且有直线视野时，持续对玩家累积石化值。
    /// 遮挡物（墙体、门板、立柱）会中断凝视，玩家可以靠躲墙规避。
    /// </summary>
    [DisallowMultipleComponent]
    public class MedusaGaze : MonoBehaviour
    {
        [Tooltip("难度配置资产（唯一数据源）：运行时覆盖下面的权重与距离参数")]
        public DifficultyProfile profile;

        [Header("视线参数")]
        [Tooltip("视锥半角（度）")]
        public float halfFov = 34f;

        [Tooltip("有效凝视距离")]
        public float range = 22f;

        [Tooltip("眼睛相对本体的高度")]
        public float eyeHeight = 1.62f;

        [Tooltip("慢速转向玩家的角速度（度/秒），设为 0 则固定朝向")]
        public float turnSpeed = 22f;

        [Header("强度")]
        [Tooltip("视野中心位置的累积倍率")]
        public float centerWeight = 1.55f;

        [Tooltip("视野边缘的累积倍率")]
        public float edgeWeight = 0.92f;

        [Tooltip("遮挡检测层（默认全部层）")]
        public LayerMask blockMask = ~0;

        [Header("近身减压（近战打法保护）")]
        [Tooltip("近身判定距离（米）：玩家贴近到该距离内，凝视权重大幅下调")]
        public float closeRangeStart = 2.0f;

        [Tooltip("近身判定的外沿距离（米）：超出该距离不再享受减压")]
        public float closeRangeEnd = 7.0f;

        [Tooltip("贴身时凝视权重下限倍率（1 = 不减；0.35 表示贴身只剩 35% 石化压力）")]
        [Range(0.05f, 1f)] public float closeWeightFactor = 0.55f;

        [Header("狂暴")]
        [Tooltip("狂暴期石化速率倍率（硬性钳制在 1~2 倍，即不超过基础速率的 2 倍）")]
        public float rageRateMultiplier = 1.35f;

        [Header("视觉反馈")]
        public Light eyeLight;
        public Color calmColor = new Color(0.55f, 0.10f, 0.10f, 1f);
        public Color alertColor = new Color(1f, 0.18f, 0.12f, 1f);

        PlayerPetrify target;
        MedusaBoss boss;
        bool gazing;

        // 诊断用：最近一次凝视角/距离/权重，便于编辑器内观测
        public float LastAngle { get; private set; }
        public float LastDist { get; private set; }
        public float LastWeight { get; private set; }
        /// <summary>最近一帧的近身减压倍率（1 = 未减压，诊断用）</summary>
        public float LastCloseFactor { get; private set; } = 1f;
        public float LastRate { get; private set; }
        public float LastRageMul { get; private set; }
        public string LastBlock { get; private set; }

        public bool Gazing { get { return gazing; } }

        void Awake()
        {
            if (profile == null) profile = DifficultyProfile.Active;
            if (profile != null) profile.ApplyToGaze(this);
        }

        void Start()
        {
            ResolveTarget();
            boss = GetComponentInParent<MedusaBoss>();
        }

        void ResolveTarget()
        {
            var p = FindObjectOfType<PlayerPetrify>();
            if (p != null) target = p;
        }

        void LateUpdate()
        {
            if (target == null)
            {
                ResolveTarget();
                gazing = false;
                UpdateLight();
                return;
            }

            float dt = Time.deltaTime;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 toPlayer = target.transform.position + Vector3.up * 1.1f - eye;
            float dist = toPlayer.magnitude;

            gazing = false;
            LastDist = dist;

            if (dist <= range && dist > 0.01f)
            {
                // 是否落在视锥内
                Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
                Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
                float ang = flat.sqrMagnitude > 0.0001f && fwd.sqrMagnitude > 0.0001f
                    ? Vector3.Angle(fwd, flat)
                    : 180f;

                LastAngle = ang;
                LastBlock = null;
                if (ang <= halfFov && HasLineOfSight(eye, target.transform.position + Vector3.up * 1.1f))
                {
                    // 视线定格：朝玩家缓慢转动（只转水平）
                    if (turnSpeed > 0.01f && flat.sqrMagnitude > 0.0001f)
                    {
                        Quaternion want = Quaternion.LookRotation(flat.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
                    }

                    float k = Mathf.Clamp01(1f - ang / Mathf.Max(1f, halfFov));
                    float weight = Mathf.Lerp(edgeWeight, centerWeight, k);
                    weight *= Mathf.Clamp01(1f - Mathf.InverseLerp(range * 0.72f, range, dist) * 0.55f);

                    // 近身减压：近战必须贴身，距离越近凝视权重越低，避免贴脸 3 秒被秒杀
                    float closeT = Mathf.InverseLerp(closeRangeStart, Mathf.Max(closeRangeStart + 0.01f, closeRangeEnd), dist);
                    weight *= Mathf.Lerp(closeWeightFactor, 1f, closeT);
                    LastCloseFactor = Mathf.Lerp(closeWeightFactor, 1f, closeT);

                    if (target.Petrified) weight *= 0.15f;
                    LastAngle = ang;
                    LastDist = dist;
                    LastWeight = weight;

                    float mul = RageMultiplier();
                    float rate = gazeBaseRate() * mul;
                    LastRate = rate;
                    LastRageMul = mul;

                    target.ApplyGaze(rate * weight, Mathf.Min(dt, 0.1f));
                    gazing = true;
                }
            }

            UpdateLight();
        }

        float gazeBaseRate()
        {
            // 以玩家石化组件上的速率为准，避免两处参数不一致
            var cfg = target != null ? target.gazeRate : 26f;
            return cfg;
        }

        /// <summary>
        /// 狂暴期速率倍率：硬性钳制在 [1, 2]，保证狂暴石化速度不超过基础值的 2 倍。
        /// </summary>
        float RageMultiplier()
        {
            if (boss == null) boss = GetComponentInParent<MedusaBoss>();
            bool raging = boss != null && boss.IsRaging;
            if (!raging) return 1f;
            return Mathf.Clamp(rageRateMultiplier, 1f, 2f);
        }

        bool HasLineOfSight(Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a;
            float d = dir.magnitude;
            if (d <= 0.01f) return true;
            // 玩家通常被放在 Ignore Raycast 层，射线会穿过玩家打到身后墙体，
            // 因此改为遍历全部命中并允许"贴近目标"的命中，避免误判为被遮挡。
            var hits = Physics.RaycastAll(a, dir / d, d, blockMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return true;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.transform.IsChildOf(target.transform)) return true;
                if (h.distance >= d - 0.3f) continue;
                LastBlock = h.collider.name + "@" + h.distance.ToString("F2") + " floor=" + h.collider.gameObject.layer;
                return false;
            }
            return true;
        }

        void UpdateLight()
        {
            if (eyeLight == null) return;
            eyeLight.color = gazing ? alertColor : calmColor;
            eyeLight.intensity = gazing ? 2.6f : 0.9f;
        }
    }
}
