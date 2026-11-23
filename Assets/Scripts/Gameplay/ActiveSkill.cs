using System.Collections;
using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 主动技能：F 裂空斩（前方扇形）、V 石化震荡（自身圆环）。
    /// 解锁条件复用等级表：blade（10 级）、resist2（12 级）。
    /// </summary>
    [DisallowMultipleComponent]
    public class ActiveSkill : MonoBehaviour
    {
        [Header("技能 1 · 裂空斩")]
        public float slashCooldown = 7f;
        public float slashDamage = 45f;
        public float slashRadius = 4.5f;
        public float slashRange = 6.5f;
        [Range(0f, 1f)] public float slashHalfAngleCos = 0.2f;

        [Header("技能 2 · 石化震荡")]
        public float shockCooldown = 12f;
        public float shockDamage = 28f;
        public float shockRadius = 8f;

        InputReader _input;
        float _slashCd;
        float _shockCd;
        float _lastCastTime = -10f;

        public bool SlashUnlocked { get { return Progression.Has("blade"); } }
        public bool ShockUnlocked { get { return Progression.Has("resist2"); } }
        public bool SlashReady { get { return SlashUnlocked && _slashCd <= 0f; } }
        public bool ShockReady { get { return ShockUnlocked && _shockCd <= 0f; } }
        public float SlashCd01 { get { return slashCooldown <= 0.01f ? 0f : Mathf.Clamp01(_slashCd / slashCooldown); } }
        public float ShockCd01 { get { return shockCooldown <= 0.01f ? 0f : Mathf.Clamp01(_shockCd / shockCooldown); } }
        public float SlashRemain { get { return Mathf.Max(0f, _slashCd); } }
        public float ShockRemain { get { return Mathf.Max(0f, _shockCd); } }
        public int CastCount { get; private set; }
        public string LastCastName { get; private set; }

        void Awake()
        {
            _input = GetComponent<InputReader>();
            if (_input == null) _input = GetComponentInParent<InputReader>();
        }

        void Update()
        {
            if (_slashCd > 0f) _slashCd -= Time.deltaTime;
            if (_shockCd > 0f) _shockCd -= Time.deltaTime;

            if (_input == null) return;
            if (_input.Skill1Pressed) TrySlash();
            if (_input.Skill2Pressed) TryShock();
        }

        public bool TrySlash()
        {
            if (!SlashUnlocked)
            {
                Toast("裂空斩 · 等级 10 解锁");
                return false;
            }
            if (_slashCd > 0f)
            {
                Toast("裂空斩冷却中 " + _slashCd.ToString("F1") + "s");
                return false;
            }

            _slashCd = slashCooldown;
            CastCount++;
            LastCastName = "裂空斩";

            Vector3 origin = transform.position;
            Vector3 fwd = transform.forward;
            float dmg = slashDamage * PlayerStats.DamageMultiplier;
            int hits = DamageArc(origin + fwd * 2f, slashRadius, fwd, slashHalfAngleCos, dmg);
            SpawnRing(origin, 0.8f, slashRadius + 1.5f, new Color(0.55f, 0.95f, 1f, 0.85f), 0.32f);
            SpawnRing(origin, 0.5f, slashRadius, new Color(1f, 1f, 1f, 0.6f), 0.22f);

            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.SlashSwing, origin, 0.9f, 0.85f);
            if (hits > 0 && audio != null) audio.PlayAt(SfxId.SlashHit, origin, 0.8f, 1f);
            if (hits > 0) Shake(0.12f, 0.09f);
            return true;
        }

        public bool TryShock()
        {
            if (!ShockUnlocked)
            {
                Toast("石化震荡 · 等级 12 解锁");
                return false;
            }
            if (_shockCd > 0f)
            {
                Toast("石化震荡冷却中 " + _shockCd.ToString("F1") + "s");
                return false;
            }

            _shockCd = shockCooldown;
            CastCount++;
            LastCastName = "石化震荡";

            Vector3 origin = transform.position;
            float dmg = shockDamage * PlayerStats.DamageMultiplier;
            int hits = DamageSphere(origin, shockRadius, dmg);
            SpawnRing(origin, 0.6f, shockRadius, new Color(0.85f, 0.4f, 1f, 0.9f), 0.45f);
            SpawnRing(origin, 0.4f, shockRadius * 0.65f, new Color(1f, 0.75f, 0.35f, 0.7f), 0.35f);

            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.BossRoar, origin, 0.45f, 1.35f);
            if (hits > 0) Shake(0.2f, 0.14f);
            return true;
        }

        int DamageArc(Vector3 center, float radius, Vector3 forward, float halfCos, float dmg)
        {
            var cols = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;
                if (c.transform.IsChildOf(transform)) continue;
                Vector3 to = c.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Dot(to.normalized, forward) < halfCos) continue;
                var d = c.GetComponentInParent<IDamageable>();
                if (d == null || d.IsDead) continue;
                d.TakeDamage(dmg);
                hits++;
            }
            return hits;
        }

        int DamageSphere(Vector3 center, float radius, float dmg)
        {
            var cols = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;
                if (c.transform.IsChildOf(transform)) continue;
                var d = c.GetComponentInParent<IDamageable>();
                if (d == null || d.IsDead) continue;
                d.TakeDamage(dmg);
                hits++;
            }
            return hits;
        }

        void Shake(float degrees, float seconds)
        {
            var cs = FindObjectOfType<CameraShake>();
            if (cs != null) cs.Shake(degrees, seconds);
        }

        void Toast(string msg)
        {
            if (Time.time - _lastCastTime < 1.2f) return;
            _lastCastTime = Time.time;
            LootToast.Show(msg);
        }

        /// <summary>技能范围提示：程序化圆环，0.4 秒内扩散淡出。</summary>
        void SpawnRing(Vector3 pos, float fromRadius, float toRadius, Color color, float duration)
        {
            var go = new GameObject("SkillRing");
            go.transform.position = pos + Vector3.up * 0.25f;
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 48;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = 0.16f;
            lr.endWidth = 0.06f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            StartCoroutine(AnimateRing(go, lr, fromRadius, toRadius, color, duration));
        }

        IEnumerator AnimateRing(GameObject go, LineRenderer lr, float from, float to, Color color, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float r = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                for (int i = 0; i < 48; i++)
                {
                    float a = i / 48f * Mathf.PI * 2f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
                var c = color;
                c.a *= (1f - k);
                lr.startColor = c;
                lr.endColor = c;
                yield return null;
            }
            Destroy(go);
        }
    }
}
