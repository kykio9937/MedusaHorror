using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>Boss 掉落的钥匙道具：悬浮旋转，靠近自动拾取，作为通往下一区域的凭证。</summary>
    public class BossKey : MonoBehaviour
    {
        public string displayName = "锈蚀钥匙";
        public BossArenaGate gate;
        public float pickupRadius = 1.9f;
        [Tooltip("生成后延迟可拾取的时间，避免玩家刚好站在掉落点导致钥匙瞬间消失")]
        public float armDelay = 1.2f;

        Transform _player;
        float _baseY;
        float _t;
        bool _taken;

        public static BossKey Spawn(Vector3 pos, string itemName, Color color, BossArenaGate gate)
        {
            var go = new GameObject("BossKey_" + (string.IsNullOrEmpty(itemName) ? "Key" : itemName));
            go.transform.position = pos;
            var key = go.AddComponent<BossKey>();
            key.displayName = string.IsNullOrEmpty(itemName) ? "锈蚀钥匙" : itemName;
            key.gate = gate;
            key.Build(color);
            return key;
        }

        void Build(Color color)
        {
            _baseY = transform.position.y;

            var mat = new Material(Shader.Find("Standard") != null ? Shader.Find("Standard") : Shader.Find("Sprites/Default"));
            mat.name = "BossKeyMat";
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * 1.6f);

            Part("Shaft", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.34f, 0.05f), mat);
            Part("Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.26f, 0f), new Vector3(0.17f, 0.02f, 0.17f), mat);
            Part("Tooth", PrimitiveType.Cube, new Vector3(0.08f, -0.08f, 0f), new Vector3(0.13f, 0.05f, 0.05f), mat);

            var lightGo = new GameObject("KeyLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = 2.2f;
            l.range = 4.5f;
            l.shadows = LightShadows.None;

            var col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = pickupRadius * 0.6f;
        }

        void Part(string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        void Update()
        {
            _t += Time.deltaTime;
            transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
            var p = transform.position;
            p.y = _baseY + Mathf.Sin(_t * 1.8f) * 0.08f;
            transform.position = p;

            if (_taken || _t < armDelay) return;
            if (_player == null)
            {
                var pet = Object.FindObjectOfType<PlayerPetrify>();
                if (pet != null) _player = pet.transform;
                if (_player == null) return;
            }
            Vector3 a = new Vector3(_player.position.x, 0f, _player.position.z);
            Vector3 b = new Vector3(p.x, 0f, p.z);
            if (Vector3.Distance(a, b) <= pickupRadius && Mathf.Abs(_player.position.y - p.y) < 2.4f) Collect();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            if (other.GetComponentInParent<PlayerPetrify>() != null) Collect();
        }

        public void Collect()
        {
            if (_taken) return;
            _taken = true;
            if (gate != null) gate.OnKeyCollected();
            // 同一把 Boss 钥匙同时解封顶层传送门（独立于其它钥匙，不做复用）
            ExitPortal.GrantKey();
            Core.Inventory.Add("key_" + displayName, displayName, 1, "锈迹斑斑的钥匙，能解开封锁的门", "钥匙");
            LootToast.Show("获得「" + displayName + "」 · 前路开启，顶层传送门可解封");
            var audio = Core.AudioDirector.Instance;
            if (audio != null) audio.PlayAt(Core.SfxId.Pickup, transform.position, 0.95f, 1f);
            string who = _player != null ? _player.position.ToString("F2") : "null";
            Debug.Log("[Loot] 拾取 " + displayName + " 钥匙坐标=" + transform.position.ToString("F2") + " 玩家坐标=" + who);
            Destroy(gameObject);
        }
    }
}
