using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 顶层离场传送门特效：门洞内循环旋涡/流光 + 门框脉冲闪光，锁定偏暗、解锁变亮。
    /// 由 ExitPortalSetup.Install() 幂等装配（每次重建 ExitPortal 根节点时重新挂载并复原默认参数）。
    /// 仅表现层：不改 ExitPortal 位置、不改门禁逻辑；不含 Collider，粒子在本地空间模拟，不会穿墙或阻挡判定。
    /// </summary>
    [DisallowMultipleComponent]
    public class ExitPortalVfx : MonoBehaviour
    {
        public const string SwirlPrefabPath = "Assets/QFX/ProjectilesFX/VFX_Prefabs/Projectiles_Full/VFX_Magic_Projectile.prefab";
        public const string PulsePrefabPath = "Assets/QFX/ProjectilesFX/VFX_Prefabs/Flashes/VFX_Dark_Magic_Flash.prefab";

        [Header("特效预制体（由 ExitPortalSetup 赋值）")]
        public GameObject swirlPrefab;
        public GameObject pulsePrefab;

        [Header("布置（门根本地坐标，门面法线为 +X）")]
        public Vector3 swirlLocalPos = new Vector3(0.14f, 1.30f, 0f);
        public Vector3 pulseLocalPos = new Vector3(0.14f, 2.60f, 0f);
        public float swirlScale = 1.0f;
        public float pulseScale = 2.0f;

        [Header("明暗两档")]
        public float lockedRate = 0.35f;
        public float unlockedRate = 1.0f;
        public float lockedSpin = 16f;
        public float unlockedSpin = 52f;
        public float lockedGlow = 0.30f;
        public float unlockedGlow = 2.10f;

        class SysTone
        {
            public ParticleSystem ps;
            public ParticleSystem.MinMaxGradient baseColor;
            public float baseRate;
            public float baseSpeed;
        }

        ExitPortal _portal;
        Transform _swirlRoot;
        Transform _pulseRoot;
        SysTone[] _swirlTone = new SysTone[0];
        SysTone[] _pulseTone = new SysTone[0];
        Light _glow;
        Light _pulseLight;
        float _tone;
        float _appliedTone = -1f;
        float _clock;
        string _state = "未装配";

        public bool Built { get { return _swirlRoot != null; } }
        public bool Unlocked { get { return ResolveUnlocked(); } }

        void Awake()
        {
            _portal = GetComponent<ExitPortal>();
            EnsureGlow();
            RebuildEffects();
        }

        /// <summary>幂等重建：先清理既有特效节点，再按当前预制体引用重新生成。</summary>
        public void RebuildEffects()
        {
            ClearBuilt();

            if (!Application.isPlaying)
            {
                _state = "已挂载（编辑器态不落地粒子，Play 时装配）";
                return;
            }

            if (swirlPrefab != null)
            {
                _swirlRoot = Spawn(swirlPrefab, "ExitVFX_Swirl", swirlLocalPos, swirlScale, Quaternion.Euler(0f, 90f, 0f), true);
                _swirlTone = Configure(_swirlRoot.GetComponentsInChildren<ParticleSystem>(true), false);
            }
            if (pulsePrefab != null)
            {
                _pulseRoot = Spawn(pulsePrefab, "ExitVFX_Pulse", pulseLocalPos, pulseScale, Quaternion.Euler(0f, 90f, 0f), false);
                _pulseTone = Configure(_pulseRoot.GetComponentsInChildren<ParticleSystem>(true), true);
            }

            EnsureGlow();
            EnsurePulseLight();
            ApplyTone(ResolveUnlocked() ? 1f : 0f, true);
            _state = "装配完成";
            Debug.Log("[ExitVFX] " + StateText());
        }

        void ClearBuilt()
        {
            if (_swirlRoot != null) DestroyObj(_swirlRoot.gameObject);
            if (_pulseRoot != null) DestroyObj(_pulseRoot.gameObject);
            _swirlRoot = null;
            _pulseRoot = null;
            _swirlTone = new SysTone[0];
            _pulseTone = new SysTone[0];
        }

        Transform Spawn(GameObject prefab, string name, Vector3 localPos, float scale, Quaternion localRot, bool dropImpact)
        {
            var go = Instantiate(prefab, transform);
            go.name = name;
            var t = go.transform;
            t.localPosition = localPos;
            t.localRotation = localRot;
            t.localScale = Vector3.one * Mathf.Max(0.05f, scale);

            // 只做表现：剥掉一切碰撞与预制体自带脚本，避免参与物理或自行销毁
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) if (c != null) DestroyObj(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) DestroyObj(rb);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) mb.enabled = false;

            if (dropImpact)
            {
                // 投射物预制体自带 Impact 爆点，循环里会周期炸开，旋涡不需要
                foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                {
                    if (tr == t) continue;
                    if (tr.name.IndexOf("Impact", System.StringComparison.OrdinalIgnoreCase) >= 0) tr.gameObject.SetActive(false);
                }
            }
            return t;
        }

        SysTone[] Configure(ParticleSystem[] systems, bool pulse)
        {
            var list = new System.Collections.Generic.List<SysTone>(systems.Length);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null || !ps.gameObject.activeInHierarchy) continue;

                // 改 duration 前必须先完全停止，否则 Unity 报 "Setting the duration while system is still playing"
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                main.playOnAwake = true;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = Mathf.Clamp(main.maxParticles, 8, 36);
                // 速度压制：粒子只在门洞内打转，不奔涌出门框
                main.startSpeedMultiplier = Mathf.Clamp(main.startSpeedMultiplier, 0.05f, 0.55f);
                main.startSizeMultiplier = Mathf.Clamp(main.startSizeMultiplier, 0.05f, 0.85f);

                var em = ps.emission;
                em.enabled = true;
                if (pulse)
                {
                    // 脉冲：拉短循环时长，让爆点周期性重放
                    main.duration = Mathf.Clamp(main.duration, 0.45f, 0.9f);
                    em.rateOverTimeMultiplier = 0f;
                }
                else
                {
                    main.duration = Mathf.Max(main.duration, 2f);
                    em.SetBursts(new ParticleSystem.Burst[0]);
                    em.rateOverTimeMultiplier = Mathf.Clamp(em.rateOverTimeMultiplier, 2f, 8f);
                }

                // 绕门面法线（特效本地 Z）做轨道运动，形成旋涡/流光
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                // orbital X/Y/Z 三轴必须是同一种曲线模式，否则 Unity 报
                // "Particle Orbital Velocity curves must all be in the same mode"。
                // 这里统一改成 Constant 模式：X/Y 归零，只保留绕法线的 Z 向轨道速度。
                vel.orbitalX = new ParticleSystem.MinMaxCurve(0f);
                vel.orbitalY = new ParticleSystem.MinMaxCurve(0f);
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(pulse ? 0.4f : 1.6f);
                // orbital offset 同理，三轴模式也必须一致
                vel.orbitalOffsetX = new ParticleSystem.MinMaxCurve(0f);
                vel.orbitalOffsetY = new ParticleSystem.MinMaxCurve(0f);
                vel.orbitalOffsetZ = new ParticleSystem.MinMaxCurve(0f);

                ps.Play(true);

                list.Add(new SysTone
                {
                    ps = ps,
                    baseColor = main.startColor,
                    baseRate = pulse ? 1f : em.rateOverTimeMultiplier,
                    baseSpeed = main.startSpeedMultiplier
                });
            }
            return list.ToArray();
        }

        void EnsureGlow()
        {
            if (_glow != null) return;
            var go = new GameObject("ExitVFX_Glow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = swirlLocalPos;
            _glow = go.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = new Color(0.55f, 0.90f, 1f, 1f);
            _glow.range = 6.5f;
            _glow.intensity = 0f;
            _glow.shadows = LightShadows.None;
        }

        void EnsurePulseLight()
        {
            if (_pulseLight != null) return;
            var go = new GameObject("ExitVFX_FramePulse");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pulseLocalPos;
            _pulseLight = go.AddComponent<Light>();
            _pulseLight.type = LightType.Point;
            _pulseLight.color = new Color(0.95f, 0.82f, 1f, 1f);
            _pulseLight.range = 4.5f;
            _pulseLight.intensity = 0f;
            _pulseLight.shadows = LightShadows.None;
        }

        bool ResolveUnlocked()
        {
            if (_portal == null) _portal = GetComponent<ExitPortal>();
            if (_portal != null && (_portal.requireHospitalClues || _portal.requireBossDefeat)) return _portal.Activated;
            if (ExitPortal.KeyGranted) return true;
            return _portal != null && _portal.door != null && !_portal.door.Locked;
        }

        void Update()
        {
            float want = ResolveUnlocked() ? 1f : 0f;
            _tone = Mathf.MoveTowards(_tone, want, Time.deltaTime * 2.2f);
            ApplyTone(_tone, false);

            _clock += Time.deltaTime;

            if (_swirlRoot != null)
            {
                float spin = Mathf.Lerp(lockedSpin, unlockedSpin, _tone);
                _swirlRoot.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
            }
            if (_glow != null)
            {
                float g = Mathf.Lerp(lockedGlow, unlockedGlow, _tone);
                _glow.intensity = g * (0.86f + 0.14f * Mathf.Sin(_clock * 3.1f));
            }
            if (_pulseLight != null)
            {
                float p = Mathf.Lerp(lockedGlow * 0.6f, unlockedGlow * 0.85f, _tone);
                float wave = 0.5f + 0.5f * Mathf.Sin(_clock * Mathf.Lerp(2.2f, 5.4f, _tone));
                _pulseLight.intensity = p * wave;
            }
        }

        void ApplyTone(float tone, bool force)
        {
            if (!force && Mathf.Abs(tone - _appliedTone) < 0.01f) return;
            _appliedTone = tone;

            float rate = Mathf.Lerp(lockedRate, unlockedRate, tone);
            float speed = Mathf.Lerp(0.55f, 1.0f, tone);
            float bright = Mathf.Lerp(0.45f, 1.0f, tone);

            ApplyGroup(_swirlTone, rate, speed, bright);
            ApplyGroup(_pulseTone, rate, speed, bright);
        }

        void ApplyGroup(SysTone[] group, float rate, float speed, float bright)
        {
            if (group == null) return;
            for (int i = 0; i < group.Length; i++)
            {
                var t = group[i];
                if (t == null || t.ps == null) continue;
                var main = t.ps.main;
                main.startColor = ScaleColor(t.baseColor, bright);
                main.startSpeedMultiplier = t.baseSpeed * speed;
                var em = t.ps.emission;
                em.rateOverTimeMultiplier = t.baseRate * rate;
            }
        }

        static ParticleSystem.MinMaxGradient ScaleColor(ParticleSystem.MinMaxGradient g, float m)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Mul(g.color, m));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Mul(g.colorMin, m), Mul(g.colorMax, m));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(ScaleGradient(g.gradientMin, m), ScaleGradient(g.gradientMax, m));
                default:
                    return new ParticleSystem.MinMaxGradient(ScaleGradient(g.gradient, m));
            }
        }

        static Gradient ScaleGradient(Gradient src, float m)
        {
            var grad = new Gradient();
            var ck = src.colorKeys;
            for (int i = 0; i < ck.Length; i++) ck[i].color = Mul(ck[i].color, m);
            grad.SetKeys(ck, src.alphaKeys);
            return grad;
        }

        static Color Mul(Color c, float m)
        {
            return new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), c.a);
        }

        static void DestroyObj(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        public string StateText()
        {
            return "vfx=" + (string.IsNullOrEmpty(_state) ? "-" : _state) +
                   " swirlGo=" + (_swirlRoot != null ? _swirlRoot.gameObject.activeSelf.ToString() : "null") +
                   " swirlSys=" + _swirlTone.Length +
                   " pulseGo=" + (_pulseRoot != null ? _pulseRoot.gameObject.activeSelf.ToString() : "null") +
                   " pulseSys=" + _pulseTone.Length +
                   " unlocked=" + ResolveUnlocked() +
                   " tone=" + _tone.ToString("F2") +
                   " glow=" + (_glow != null ? _glow.intensity.ToString("F2") : "null") +
                   " light=" + (_pulseLight != null ? _pulseLight.intensity.ToString("F2") : "null") +
                   " pos=" + transform.position.ToString("F2");
        }
    }
}
