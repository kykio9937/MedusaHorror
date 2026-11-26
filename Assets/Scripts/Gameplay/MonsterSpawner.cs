using System.Collections.Generic;
using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.UI;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 野外刷怪点：维持固定数量的怪物，被打死后延迟补刷，怪物不会跑出活动半径。
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        public Monster.Kind kind = Monster.Kind.Stalker;
        public int targetCount = 3;
        public float spawnRadius = 8f;
        public float activeRadius = 22f;
        public float respawnDelay = 14f;
        public string displayName = "";
        public int maxTotalSpawns;
        public float activationDistance;
        int _totalSpawned;

        readonly List<Monster> _alive = new List<Monster>();
        float _nextCheck;

        void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 1f;

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null || _alive[i].IsDead) _alive.RemoveAt(i);
            }

            // 玩家走远就不补刷，省性能也避免怪追到别处
            var p = PlayerRef.Transform;
            if (p != null)
            {
                Vector3 d = p.position - transform.position; d.y = 0f;
                if (d.magnitude > (activationDistance > 0 ? activationDistance : activeRadius * 2.5f)) return;
            }

            if (_alive.Count >= targetCount) return;
            if (Time.time < _lastSpawn + respawnDelay) return;
            Spawn();
        }

        float _lastSpawn;

        public Monster Spawn()
        {
            if (maxTotalSpawns > 0 && _totalSpawned >= maxTotalSpawns) return null;
            _lastSpawn = Time.time;
            Vector2 rnd = Random.insideUnitCircle * spawnRadius;
            Vector3 position = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            if (maxTotalSpawns > 0)
            {
                if (!UnityEngine.AI.NavMesh.SamplePosition(position, out var nav, 2f, UnityEngine.AI.NavMesh.AllAreas)) return null;
                position = nav.position;
            }
            var go = new GameObject("Monster_" + (string.IsNullOrEmpty(displayName) ? kind.ToString() : displayName));
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var m = go.AddComponent<Monster>();
            m.kind = kind;
            if (!string.IsNullOrEmpty(displayName)) m.displayName = displayName;
            m.ApplyKindPreset();
            _alive.Add(m);
            _totalSpawned++;
            return m;
        }

        public int AliveCount { get { return _alive.Count; } }
    }
}
