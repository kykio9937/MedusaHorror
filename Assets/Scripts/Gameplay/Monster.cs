using UnityEngine;
using UnityEngine.AI;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 野外怪物：程序化外观 + 追击 / 攻击 / 受击 / 死亡给经验与掉落。
    /// 攻击方式是「凝视」——持续抬升玩家的石化值，与世界观一致。
    /// </summary>
    public class Monster : MonoBehaviour, IDamageable
    {
        public enum Kind { Stalker, Brute, Spitter }

        [Header("类型与数值")]
        public Kind kind = Kind.Stalker;
        public string displayName = "石行者";
        public float maxHealth = 60f;
        public float moveSpeed = 2.2f;
        public float chaseSpeed = 3.4f;
        public float damage = 9f;          // 每次凝视造成的石化值
        public float attackRange = 2.4f;
        public float attackInterval = 2.0f;
        public float detectRange = 18f;
        public float loseRange = 30f;
        public float heightOffset = 0f;

        [Header("奖励")]
        public float xpReward = 22f;
        public int coinReward = 8;
        public string dropItemId = "";
        public string dropItemName = "";
        public string dropItemDesc = "";
        public string dropItemKind = "材料";

        public bool IsDead { get { return _dead; } }
        public Transform Transform { get { return transform; } }
        public float Health { get; private set; }
        public float Health01 { get { return maxHealth <= 0.01f ? 0f : Mathf.Clamp01(Health / maxHealth); } }
        public string State { get; private set; }

        bool _dead;
        float _nextAttack;
        float _hitFlash;
        float _bobPhase;
        float _staggerUntil;
        Transform _body;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        Transform _eyeRoot;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        NavMeshAgent _agent;

        void Awake()
        {
            EnsureAgent();
            Health = maxHealth;
            _bobPhase = Random.Range(0f, 6.28f);
            State = "idle";
            if (transform.childCount == 0) BuildVisual();
            CacheRenderers();
            _mpb = new MaterialPropertyBlock();
        }


        void EnsureAgent()
        {
            if (_agent != null) return;
            _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
            _agent.radius = 0.35f;
            _agent.height = 1.7f;
            _agent.baseOffset = 0f;
            _agent.speed = chaseSpeed;
            _agent.angularSpeed = 540f;
            _agent.acceleration = 18f;
            _agent.stoppingDistance = 0.7f;
            _agent.updateRotation = false;   // we already face the player manually
            _agent.autoBraking = true;
        }

        void Start()
        {
            ApplyKindPreset();
        }

        /// <summary>按类型套用数值与配色（外部也可以只设 kind 然后让它自己配）。</summary>
        public void ApplyKindPreset()
        {
            switch (kind)
            {
                case Kind.Brute:
                    if (displayName == "石行者") displayName = "石像鬼";
                    maxHealth = Mathf.Max(maxHealth, 140f);
                    moveSpeed = 1.5f; chaseSpeed = 2.2f;
                    damage = 16f; attackRange = 2.8f; attackInterval = 2.6f;
                    xpReward = Mathf.Max(xpReward, 55f); coinReward = Mathf.Max(coinReward, 22);
                    Tint(new Color(0.30f, 0.26f, 0.34f), new Color(1f, 0.35f, 0.15f));
                    break;
                case Kind.Spitter:
                    if (displayName == "石行者") displayName = "酸蚀爬虫";
                    maxHealth = Mathf.Max(maxHealth, 45f);
                    moveSpeed = 2.6f; chaseSpeed = 4.2f;
                    damage = 7f; attackRange = 6.5f; attackInterval = 1.6f;
                    xpReward = Mathf.Max(xpReward, 26f); coinReward = Mathf.Max(coinReward, 10);
                    Tint(new Color(0.22f, 0.32f, 0.28f), new Color(0.45f, 1f, 0.35f));
                    break;
                default:
                    Tint(new Color(0.26f, 0.24f, 0.28f), new Color(0.35f, 0.85f, 1f));
                    break;
            }
            Health = maxHealth;
        }

        void BuildVisual()
        {
            var bodyMat = MakeMat(new Color(0.26f, 0.24f, 0.28f), Color.black);

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            _body.localPosition = new Vector3(0f, 0.85f, 0f);

            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Torso";
            torso.transform.SetParent(_body, false);
            torso.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
            torso.transform.localPosition = Vector3.zero;
            Object.Destroy(torso.GetComponent<Collider>());
            torso.GetComponent<Renderer>().sharedMaterial = bodyMat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(_body, false);
            head.transform.localScale = new Vector3(0.46f, 0.40f, 0.46f);
            head.transform.localPosition = new Vector3(0f, 0.72f, 0.10f);
            Object.Destroy(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = bodyMat;

            _eyeRoot = new GameObject("Eyes").transform;
            _eyeRoot.SetParent(head.transform, false);
            var eyeMat = MakeMat(new Color(0.35f, 0.85f, 1f), new Color(0.35f, 0.85f, 1f) * 2.2f);
            for (int i = 0; i < 2; i++)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = "Eye" + i;
                eye.transform.SetParent(_eyeRoot, false);
                eye.transform.localScale = Vector3.one * 0.16f;
                eye.transform.localPosition = new Vector3(i == 0 ? -0.13f : 0.13f, 0.10f, 0.40f);
                Object.Destroy(eye.GetComponent<Collider>());
                eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            }

            // 四肢：细长柱体，做出"石像"的骨感
            var limbMat = MakeMat(new Color(0.20f, 0.19f, 0.23f), Color.black);
            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = "Limb" + i;
                leg.transform.SetParent(_body, false);
                leg.transform.localScale = new Vector3(0.13f, 0.42f, 0.13f);
                float sx = (i % 2 == 0) ? -0.34f : 0.34f;
                float sz = (i < 2) ? 0.22f : -0.22f;
                leg.transform.localPosition = new Vector3(sx, -0.55f, sz);
                leg.transform.localRotation = Quaternion.Euler(sz > 0 ? 16f : -16f, 0f, sx > 0 ? -12f : 12f);
                Object.Destroy(leg.GetComponent<Collider>());
                leg.GetComponent<Renderer>().sharedMaterial = limbMat;
            }

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.9f;
            col.radius = 0.62f;
        }

        void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        static Material MakeMat(Color color, Color emission)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse"));
            m.color = color;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        void Tint(Color body, Color eye)
        {
            if (_renderers == null) CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                var m = r.sharedMaterial;
                if (m == null) continue;
                if (r.transform.name.StartsWith("Eye")) continue;
                if (m.HasProperty("_Color")) m.color = body;
            }
        }

        void Update()
        {
            if (_dead) return;
            var flow = GameFlow.Instance;
            if (flow != null && !flow.IsRunning) { State = "paused"; return; }

            _hitFlash = Mathf.Max(0f, _hitFlash - Time.deltaTime);
            ApplyFlash();

            var player = PlayerRef.Transform;
            float dt = Time.deltaTime;

            // 死亡 / 不存在玩家时待机
            if (player == null)
            {
                Idle(dt);
                State = "idle";
                return;
            }

            float dist = Vector3.Distance(Flat(transform.position), Flat(player.position));
            bool canSee = dist <= detectRange;
            bool tooFar = dist > loseRange;

            if (Time.time < _staggerUntil)
            {
                State = "stagger";
                Idle(dt);
                return;
            }

            if (canSee && !tooFar)
            {
                if (dist > attackRange)
                {
                    State = "chase";
                    Chase(player, dt);
                }
                else
                {
                    State = "attack";
                    FaceTo(player.position, dt, 8f);
                    Idle(dt);
                    if (Time.time >= _nextAttack) Attack(player);
                }
            }
            else
            {
                State = "idle";
                Idle(dt);
            }
        }

        void Idle(float dt)
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            if (_body == null) return;
            _bobPhase += dt * 2.2f;
            var p = _body.localPosition;
            p.y = 0.85f + Mathf.Sin(_bobPhase) * 0.04f;
            _body.localPosition = p;
        }

        void Chase(Transform player, float dt)
        {
            Vector3 to = Flat(player.position) - Flat(transform.position);
            if (to.sqrMagnitude < 0.0001f) return;
            EnsureAgent();
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.speed = chaseSpeed;
                _agent.isStopped = false;
                _agent.SetDestination(player.position);
                FaceTo(player.position, dt, 6f);
                if (_body != null)
                {
                    _bobPhase += dt * 9f;
                    var pb = _body.localPosition;
                    pb.y = 0.85f + Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.06f;
                    _body.localPosition = pb;
                }
                return;
            }

            Vector3 dir = AvoidWalls(to.normalized);
            transform.position += dir * chaseSpeed * dt;
            SnapToGround();
            FaceTo(player.position, dt, 6f);
            if (_body != null)
            {
                _bobPhase += dt * 9f;
                var p = _body.localPosition;
                p.y = 0.85f + Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.06f;
                _body.localPosition = p;
            }
        }


        /// <summary>Simple wall avoidance: if the direct path is blocked, slide along the
        /// first free side direction (the project has no NavMesh package).</summary>
        Vector3 AvoidWalls(Vector3 dir)
        {
            const float probe = 0.9f;
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            RaycastHit hit;
            if (!Physics.SphereCast(origin, 0.3f, dir, out hit, probe, ~0, QueryTriggerInteraction.Ignore))
                return dir;

            Vector3[] sides = { Quaternion.Euler(0, 55, 0) * dir, Quaternion.Euler(0, -55, 0) * dir, Quaternion.Euler(0, 90, 0) * dir, Quaternion.Euler(0, -90, 0) * dir };
            for (int i = 0; i < sides.Length; i++)
            {
                if (!Physics.SphereCast(origin, 0.3f, sides[i], out hit, probe, ~0, QueryTriggerInteraction.Ignore))
                    return sides[i];
            }
            return dir;
        }

        void FaceTo(Vector3 world, float dt, float speed)
        {
            Vector3 to = Flat(world) - Flat(transform.position);
            if (to.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(dt * speed));
        }

        void SnapToGround()
        {
            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                transform.position = new Vector3(transform.position.x, hit.point.y + heightOffset, transform.position.z);
        }

        void Attack(Transform player)
        {
            _nextAttack = Time.time + attackInterval;
            var pz = player.GetComponentInParent<PlayerPetrify>();
            if (pz != null && !pz.dead)
            {
                pz.ApplyImpact(damage);
                var audio = AudioDirector.Instance;
                if (audio != null) audio.PlayAt(SfxId.PetrifyLoop, transform.position, 0.35f, 1.1f);
                if (kind == Kind.Spitter) SpawnBolt(player.position);
            }
            var cam = Camera.main;
            if (cam != null)
            {
                var shake = cam.GetComponent<CameraShake>();
                if (shake != null) shake.Shake(1.2f, 0.12f);
            }
        }

        void SpawnBolt(Vector3 target)
        {
            var go = new GameObject("AcidBolt");
            go.transform.position = transform.position + Vector3.up * 1.2f;
            go.transform.LookAt(target + Vector3.up);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.forward * Vector3.Distance(go.transform.position, target));
            lr.startWidth = 0.08f; lr.endWidth = 0.02f;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) lr.material = new Material(sh);
            lr.startColor = new Color(0.5f, 1f, 0.4f, 0.9f);
            lr.endColor = new Color(0.5f, 1f, 0.4f, 0f);
            Object.Destroy(go, 0.18f);
        }

        public void TakeDamage(float amount)
        {
            if (_dead || amount <= 0f) return;
            MedusaHorror.UI.CombatNumbers.Show(transform.position+Vector3.up*1.6f,Mathf.Min(Health,amount));
            Health -= amount;
            _hitFlash = 0.14f;
            _staggerUntil = Time.time + 0.18f;
            if (Health <= 0f) Die();
        }

        void ApplyFlash()
        {
            if (_renderers == null || _mpb == null) return;
            var c = _hitFlash > 0f ? new Color(1f, 0.95f, 0.9f) : Color.white;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;
            State = "dead";
            SaveSystem.Data.killCount++;

            Progression.AddXp(xpReward);
            if (coinReward > 0) Wallet.Add(coinReward);
            if (!string.IsNullOrEmpty(dropItemId))
                Inventory.Add(dropItemId, dropItemName, 1, dropItemDesc, dropItemKind);

            LootToast.Show("击杀 " + displayName + " · +" + Mathf.RoundToInt(xpReward) + " 经验"
                + (coinReward > 0 ? " · +" + coinReward + " 信用点" : "")
                + (!string.IsNullOrEmpty(dropItemId) ? " · 掉落 " + dropItemName : ""));

            SpawnDeathFx();
            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.PetrifyBreak, transform.position, 0.7f, 0.9f);

            Destroy(gameObject, 0.05f);
        }

        void SpawnDeathFx()
        {
            var go = new GameObject("MonsterDeathFx");
            go.transform.position = transform.position + Vector3.up * 0.9f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.4f; main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.55f, 0.5f), new Color(0.2f, 0.18f, 0.2f));
            main.gravityModifier = 1.2f;
            main.maxParticles = 96;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
            var shape = ps.shape; shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.5f;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            if (rend != null) rend.material = new Material(Shader.Find("Sprites/Default"));
            ps.Play();
            Object.Destroy(go, 2f);
        }

        static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        /// <summary>让怪物也能被通用射线打到（保持接口一致）。</summary>
        public static Monster FindNearest(Vector3 point, float radius)
        {
            var all = Object.FindObjectsOfType<Monster>();
            Monster best = null; float bd = radius * radius;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i]._dead) continue;
                float d = (all[i].transform.position - point).sqrMagnitude;
                if (d < bd) { bd = d; best = all[i]; }
            }
            return best;
        }
    }

    /// <summary>玩家引用缓存，怪物不必每帧 Find。</summary>
    public static class PlayerRef
    {
        static Transform _t;
        public static Transform Transform
        {
            get
            {
                if (_t != null) return _t;
                var pz = Object.FindObjectOfType<PlayerPetrify>();
                if (pz != null) _t = pz.transform;
                return _t;
            }
        }
        public static void Clear() { _t = null; }
    }
}
