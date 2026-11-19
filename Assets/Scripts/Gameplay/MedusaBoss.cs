using System.Collections;
using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// W1 Boss：美杜莎。
    /// 状态机 Dormant -> Hunt -> Dead，纯追击型，无远程、无位移技能，压力全部来自凝视石化。
    /// Dormant 期间 MedusaGaze 关闭，玩家可以自由探索；苏醒后开始追猎并持续用凝视累积石化。
    /// </summary>
    [DisallowMultipleComponent]
    public class MedusaBoss : MonoBehaviour
    {
        public enum State { Dormant, Alert, Hunt, Dead }

        [Tooltip("难度配置资产（唯一数据源）：运行时用它覆盖下面的字段，改数值请改资产")]
        public DifficultyProfile profile;

        [Header("数值（由 profile 覆盖，仅作未挂资产时的兜底）")]
        public float maxHealth = 360f;
        public float huntSpeed = 2.6f;
        public float rageSpeed = 4.2f;
        [Range(0f, 1f)] public float rageThreshold = 0.4f;

        [Header("苏醒条件")]
        public float wakeRadius = 15f;
        public float wakeSightRange = 24f;
        [Tooltip("勾选后休眠状态不再按距离/视线唤醒，只由 BossFloorTrigger 唤醒（分层建筑用）")]
        public bool wakeByFloorTrigger = false;
        public float wakeDelay = 1.2f;
        public float stopDistance = 1.9f;

        [Header("表现")]
        public GameObject alertFxRoot;

    [Header("区域封锁与掉落")]
    public BossArenaGate arenaGate;
    public BossLoot loot;

        public State Current { get; private set; } = State.Dormant;
        public float Health { get; private set; }
        /// <summary>运行时调整血量与狂暴参数（供调参命令使用）</summary>
        public void ApplyStats(float hp, float rageSpd, float rageHp, float hunt = -1f, float stop = -1f)
        {
            maxHealth = Mathf.Max(1f, hp);
            Health = maxHealth;
            rageSpeed = rageSpd;
            rageThreshold = Mathf.Clamp01(rageHp);
            if (hunt > 0f) huntSpeed = hunt;
            if (stop > 0f) stopDistance = stop;
        }

        public float Health01 => maxHealth <= 0f ? 0f : Mathf.Clamp01(Health / maxHealth);
        public bool IsAwake => Current == State.Hunt || Current == State.Alert;
        public bool IsDead => Current == State.Dead;
        public bool IsRaging => Health01 <= rageThreshold && Current == State.Hunt;

        CharacterController _cc;
        MedusaGaze _gaze;
        Transform _player;
        PlayerPetrify _petrify;
        Renderer[] _renderers;
        Light _eye;
        float _verticalVel;
        float _wakeTimer;
        float _hitFlash;
        Vector3 _home;
        Quaternion _homeRot;
        Coroutine _deathRoutine;

        /// <summary>诊断：最近一次唤醒原因（dormant dist / floor-trigger / damage / -）</summary>
        public static string lastWake = "-";

        void Awake()
        {
            lastWake = "-";
            if (profile == null) profile = DifficultyProfile.Active;
            if (profile != null) profile.ApplyToBoss(this);

            Current = State.Dormant;   // 防止编辑器态残留 Dead 被序列化进场景
            Health = maxHealth;
            _home = transform.position;
            _homeRot = transform.rotation;
            _gaze = GetComponent<MedusaGaze>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (_gaze != null) _eye = _gaze.eyeLight;
        }

        void Start()
        {
            ResolvePlayer();
            if (_gaze != null) _gaze.enabled = false;   // 休眠期不产生凝视
            EnsureController();
            if (alertFxRoot != null) alertFxRoot.SetActive(false);
        }

        void ResolvePlayer()
        {
            if (_player != null) return;
            var go = GameObject.Find("Kazuko_W1_Player");
            if (go == null) return;
            _player = go.transform;
            _petrify = go.GetComponent<PlayerPetrify>();
        }

        void EnsureController()
        {
            if (_cc != null) return;
            var legacy = GetComponent<CapsuleCollider>();
            if (legacy != null) DestroyImmediate(legacy);   // 避免与 CharacterController 互相阻挡
            _cc = GetComponent<CharacterController>();
            if (_cc == null) _cc = gameObject.AddComponent<CharacterController>();
            _cc.radius = 0.36f;
            _cc.height = 1.78f;
            _cc.center = new Vector3(0f, 0.9f, 0f);
            _cc.slopeLimit = 50f;
            _cc.stepOffset = 0.35f;
            _cc.skinWidth = 0.04f;
        }

        void Update()
        {
            if (Current == State.Dead) return;

            // 三层战场保护：Boss 掉出三层（扶梯洞口/边缘）→ 传回刷新点继续战斗
            if (Current != State.Dormant && transform.position.y < 8.5f)
            {
                if (_cc != null) _cc.enabled = false;
                transform.position = _home;
                _verticalVel = 0f;
                if (_cc != null) _cc.enabled = true;
            }

            ResolvePlayer();
            if (_player == null) return;

            float dt = Time.deltaTime;
            float dist = FlatDistance(_player.position, transform.position);

            if (Current == State.Dormant)
            {
                if (!wakeByFloorTrigger && (dist <= wakeRadius || (dist <= wakeSightRange && HasSight())))
                {
                    _wakeTimer += dt;
                    if (_wakeTimer >= wakeDelay)
                    {
                        lastWake = "dormant dist=" + dist.ToString("F1");
                        Wake();
                    }
                }
                else
                {
                    _wakeTimer = 0f;
                }
                return;
            }

            float speed = IsRaging ? rageSpeed : huntSpeed;
            Vector3 dir = FlatDir(transform.position, _player.position);

            if (dir.sqrMagnitude > 0.0001f)
            {
                float flatDist = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(_player.position.x, 0f, _player.position.z));
                if (flatDist > stopDistance)
                {
                    if (_cc != null && _cc.enabled)
                    {
                        _verticalVel = _cc.isGrounded ? -2f : _verticalVel - 18f * dt;
                        _cc.Move((dir * speed + Vector3.up * _verticalVel) * dt);
                    }
                    else
                    {
                        transform.position += dir * speed * dt;
                    }
                }
                else if (_cc != null && _cc.enabled && !_cc.isGrounded)
                {
                    _verticalVel -= 18f * dt;
                    _cc.Move(Vector3.up * _verticalVel * dt);
                }
                Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 6f * dt);
            }

            HitFeedbackTick(dt);

            if (_hitFlash > 0f)
            {
                _hitFlash -= dt;
                ApplyTint(Color.Lerp(new Color(0.32f, 0.34f, 0.33f, 1f), new Color(0.95f, 0.86f, 0.80f, 1f), Mathf.Clamp01(_hitFlash / 0.12f)));
            }
        }

        void Wake()
        {
            if (Current != State.Dormant) return;
            Current = State.Hunt;
            _gaze = _gaze != null ? _gaze : GetComponent<MedusaGaze>();
            if (_gaze != null)
            {
                _gaze.enabled = true;
                if (_gaze.range < 26f) _gaze.range = 26f;
            }
            if (alertFxRoot != null) alertFxRoot.SetActive(true);
            Debug.Log("[Boss] 美杜莎苏醒，开始追猎");
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null)
            {
                audio.PlayAt(MedusaHorror.Core.SfxId.BossRoar, transform.position, 1f, 1f);
                audio.SetBattleMusic(true);
            }
        if (arenaGate != null) arenaGate.Seal();
        }

        /// <summary>外部触发苏醒（如玩家进入 Boss 所在楼层），跳过距离/视线判定。</summary>
        public void WakeNow()
        {
            if (Current == State.Dormant) { lastWake = "floor-trigger"; Wake(); }
        }

        public void TakeDamage(float amount)
        {
            if (Current == State.Dead || amount <= 0f) return;
            if (Current == State.Dormant) { lastWake = "damage"; Wake(); }

            Health = Mathf.Max(0f, Health - amount);
            _hitFlash = 0.12f;
            HitFeedbackFx();   // 受击反馈：白闪 / 命中粒子 / 短距击退抖动 / 音效叠层（不改变任何战斗数值）


            if (_eye != null) _eye.intensity = 4.2f;
            var hitAudio = MedusaHorror.Core.AudioDirector.Instance;
            if (hitAudio != null) hitAudio.PlayAt(MedusaHorror.Core.SfxId.BossHit, transform.position, 0.8f, Random.Range(0.94f, 1.08f));
            if (Health <= 0f) Die();
        }

        void LateUpdate()
        {
            if (_eye == null || Current == State.Dead) return;
            // 受击闪白结束后回落到凝视状态的亮度
            if (_hitFlash <= 0f)
            {
                float baseI = (_gaze != null && _gaze.enabled) ? 2.6f : 0.9f;
                if (IsRaging) baseI += 1.2f;
                _eye.intensity = Mathf.Lerp(_eye.intensity, baseI, Time.deltaTime * 8f);
            }
        }

        void Die()
        {
            if (_deathRoutine != null) return;
            Current = State.Dead;
            if (_gaze != null) _gaze.enabled = false;
            if (_cc != null) _cc.enabled = false;
            if (alertFxRoot != null) alertFxRoot.SetActive(false);
            Debug.Log("[Boss] 美杜莎被击碎");
            var dieAudio = MedusaHorror.Core.AudioDirector.Instance;
            if (dieAudio != null)
            {
                dieAudio.PlayAt(MedusaHorror.Core.SfxId.BossRoar, transform.position, 0.9f, 0.72f);
                dieAudio.SetBattleMusic(false);
            }
            _deathRoutine = StartCoroutine(DeathSequence());
        if (arenaGate != null) arenaGate.OnBossDefeated();
        ExitPortal.NotifyBossDefeated();
        if (loot != null) loot.Drop();
        // 击杀不再结束游戏：只解除本区域封锁，玩家取钥匙后可回医院继续探索，最终从顶层传送门离场
        LootToast.Show("美杜莎崩解 · 封锁解除 · 钥匙落地");
        }

        IEnumerator DeathSequence()
        {
            ApplyTint(new Color(0.58f, 0.58f, 0.60f, 1f));
            if (_eye != null) _eye.intensity = 0f;

            float t = 0f;
            Vector3 from = transform.position;
            while (t < 1.6f)
            {
                t += Time.deltaTime;
                float k = t / 1.6f;
                transform.position = from + Vector3.down * (k * 1.1f);
                transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
                ApplyTint(Color.Lerp(new Color(0.58f, 0.58f, 0.60f, 1f), new Color(0.22f, 0.22f, 0.23f, 1f), k));
                yield return null;
            }

            var caps = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < caps.Length; i++) caps[i].enabled = false;
            gameObject.SetActive(false);
        }

        void ApplyTint(Color c)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null || !mats[m].HasProperty("_Color")) continue;
                    mats[m].color = c;
                }
            }
        }

        bool HasSight()
        {
            Vector3 eye = transform.position + Vector3.up * 1.62f;
            Vector3 tgt = _player.position + Vector3.up * 1.1f;
            Vector3 d = tgt - eye;
            float len = d.magnitude;
            if (len <= 0.05f) return true;
            var hits = Physics.RaycastAll(eye, d / len, len, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return true;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.transform.IsChildOf(_player)) return true;
                if (h.distance >= len - 0.35f) continue;
                return false;
            }
            return true;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static Vector3 FlatDir(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
        }

        public void ResetBoss()
        {
            if (_deathRoutine != null) { StopCoroutine(_deathRoutine); _deathRoutine = null; }
            transform.position = _home;
            transform.rotation = _homeRot;
            Health = maxHealth;
            _wakeTimer = 0f;
            Current = State.Dormant;
            if (_gaze != null) _gaze.enabled = false;
            if (_cc != null) _cc.enabled = true;
            gameObject.SetActive(true);
        if (loot != null) loot.ResetLoot();
        }
    
        // ===== 受击反馈（白闪 / 命中点粒子 / 短距击退与抖动 / 音效叠层）=====
        // 纯表现层：不读写 Health、maxHealth、石化速率等战斗数值；连续命中做节流，粒子单系统复用不泄漏。
        ParticleSystem _hitBurst;
        Light _hitFlashLight;
        float _hitFlashPower;
        float _hitShakeTimer;
        Vector3 _hitShakeAxis = Vector3.up;
        float _hitFeedbackStamp = -1f;
        const float HitFeedbackMinInterval = 0.045f;
        // 只读遥测：记录峰值，用于实测「连续命中是否堆叠失控」
        float _fbPeakFlash;
        float _fbPeakLight;
        int _fbPeakAlive;
        int _fbHitCount;

        void EnsureHitFeedback()
        {
            if (_hitBurst == null)
            {
                var go = new GameObject("BossHitFx");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 1.15f, 0f);

                _hitBurst = go.AddComponent<ParticleSystem>();
                _hitBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = _hitBurst.main;
                main.duration = 0.5f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 6.0f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.90f, 1f), new Color(1f, 0.58f, 0.30f, 1f));
                main.gravityModifier = 0.3f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 48;

                var em = _hitBurst.emission;
                em.enabled = true;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

                var shape = _hitBurst.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.20f;

                var col = _hitBurst.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.62f, 0.32f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var pr = _hitBurst.GetComponent<ParticleSystemRenderer>();
                if (pr != null)
                {
                    pr.material = new Material(Shader.Find("Sprites/Default"));
                    pr.renderMode = ParticleSystemRenderMode.Billboard;
                }
            }

            if (_hitFlashLight == null)
            {
                var lg = new GameObject("BossHitFlashLight");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = new Vector3(0f, 1.30f, 0f);
                _hitFlashLight = lg.AddComponent<Light>();
                _hitFlashLight.type = LightType.Point;
                _hitFlashLight.color = Color.white;
                _hitFlashLight.range = 4.5f;
                _hitFlashLight.intensity = 0f;
                _hitFlashLight.shadows = LightShadows.None;
            }
        }

        Vector3 HitPointWorld()
        {
            Vector3 center = transform.position + Vector3.up * 1.15f;
            Vector3 dir = _player != null ? FlatDir(transform.position, _player.position) : Vector3.zero;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            return center + dir.normalized * 0.30f;
        }

        /// <summary>受击反馈入口：白闪 + 命中粒子 + 短距击退与抖动 + 音效叠层。</summary>
        void HitFeedbackFx()
        {
            if (_hitFeedbackStamp >= 0f && Time.time - _hitFeedbackStamp < HitFeedbackMinInterval) return;
            _hitFeedbackStamp = Time.time;
            _fbHitCount++;

            EnsureHitFeedback();

            // 1) 白闪：白色点光短促高亮，由 HitFeedbackTick 逐帧回落；不覆盖 ApplyTint 的暗红染色
            _hitFlashPower = Mathf.Max(_hitFlashPower, 1f);

            // 2) 命中点粒子：常驻单系统 Emit，避免每次命中新建对象造成泄漏
            Vector3 hp = HitPointWorld();
            if (_hitBurst != null)
            {
                _hitBurst.transform.position = hp;
                _hitBurst.Emit(8);
            }

            // 3) 短距离击退（CharacterController 位移，遇墙自动受阻）+ 抖动
            Vector3 away = _player != null ? -FlatDir(transform.position, _player.position) : -transform.forward;
            if (away.sqrMagnitude > 0.0001f)
            {
                away = away.normalized;
                if (_cc != null && _cc.enabled) _cc.Move(away * 0.085f);
                _hitShakeAxis = Vector3.Cross(Vector3.up, away);
                if (_hitShakeAxis.sqrMagnitude < 0.0001f) _hitShakeAxis = Vector3.up;
            }
            _hitShakeTimer = 0.16f;

            // 4) 音效叠层：与既有 BossHit 并行，加一层高频金属声
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null)
                audio.PlayAt(MedusaHorror.Core.SfxId.SlashHit, hp, 0.38f, Random.Range(1.06f, 1.24f));
        }

        void HitFeedbackTick(float dt)
        {
            // 白闪回落：0.12s 内从峰值降到 0
            if (_hitFlashPower > 0f || (_hitFlashLight != null && _hitFlashLight.intensity > 0.01f))
            {
                _hitFlashPower = Mathf.Max(0f, _hitFlashPower - dt / 0.12f);
                if (_hitFlashLight != null)
                {
                    const float peak = 5.5f;
                    _hitFlashLight.intensity = peak * _hitFlashPower;
                    _hitFlashLight.color = Color.Lerp(new Color(1f, 0.93f, 0.88f, 1f), Color.white, _hitFlashPower);
                }
            }

            // 遥测采样（只读，不参与表现逻辑）
            if (_hitFlashPower > _fbPeakFlash) _fbPeakFlash = _hitFlashPower;
            if (_hitFlashLight != null && _hitFlashLight.intensity > _fbPeakLight) _fbPeakLight = _hitFlashLight.intensity;
            if (_hitBurst != null && _hitBurst.particleCount > _fbPeakAlive) _fbPeakAlive = _hitBurst.particleCount;

            // 抖动：0.16s 内小角度摆动后归位，不介入追击朝向的长期逻辑
            if (_hitShakeTimer > 0f)
            {
                _hitShakeTimer -= dt;
                float u = Mathf.Clamp01(_hitShakeTimer / 0.16f);
                float angle = Mathf.Sin((1f - u) * Mathf.PI * 6f) * 2.6f * u;
                transform.rotation *= Quaternion.AngleAxis(angle, _hitShakeAxis.normalized);
                if (_hitShakeTimer < 0f) _hitShakeTimer = 0f;
            }
        }

public string HitFeedbackTelemetry()
    {
        return "hits=" + _fbHitCount +
               " peakFlash=" + _fbPeakFlash.ToString("0.00") +
               " peakLight=" + _fbPeakLight.ToString("0.00") +
               " peakAlive=" + _fbPeakAlive +
               " shakeActive=" + (_hitShakeTimer > 0f ? "1" : "0");
    }

    public void ResetFeedbackTelemetry()
    {
        _fbPeakFlash = 0f; _fbPeakLight = 0f; _fbPeakAlive = 0; _fbHitCount = 0;
    }

    // ---- 受击反馈只读诊断（供编辑器命令通道实测，不改变任何数值） ----
    public string HitFeedbackState()
    {
        int alive = 0, peak = 0;
        if (_hitBurst != null)
        {
            var ps = _hitBurst;
            alive = ps.particleCount;
            var m = ps.main;
            peak = m.maxParticles;
        }
        float lightI = _hitFlashLight != null ? _hitFlashLight.intensity : -1f;
        return "flash=" + _hitFlashPower.ToString("0.00") +
               " tint=" + _hitFlash.ToString("0.00") +
               " shake=" + _hitShakeTimer.ToString("0.00") +
               " burstAlive=" + alive + "/" + peak +
               " lightI=" + lightI.ToString("0.00") +
               " sound=" + MedusaHorror.Core.SfxId.BossHit;
    }
}
}
