using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// NPC 外观差异化。素材包里只有玩家用的 Kazuko 一套人形，所以这里从它派生：
    /// 换肤色 / 发色 / 服装配色，改体型，再加头盔、防毒面具、斗篷、背包等配件，
    /// 让每个 NPC 一眼就能和玩家区分开。
    /// 每个 NPC 的外观由 seed 决定，同一次运行里保持稳定。
    /// </summary>
    public class NpcVariation : MonoBehaviour
    {
        public int seed = 0;
        public bool applyScale = true;
        public bool addAccessory = true;

        static readonly string[] Names =
        {
            "阿岩", "老周", "小满", "铁柱", "阿岚", "苏禾",
            "瘸子老陈", "阿沁", "打更人", "拾荒客", "面摊老板", "阿木",
            "戴面具的", "背货的", "守夜的", "瘦高个", "矮胖子",
        };

        /// <summary>一套外观：三种配色 + 体型 + 配件类型。</summary>
        struct Look
        {
            public Color skin, hair, cloth, acc;
            public float height, width;
            public int gear;   // 0 斗篷 1 头盔 2 面具 3 宽檐帽 4 兜帽
        }

        static readonly Look[] Looks = new Look[]
        {
            // 拾荒者：土褐斗篷 + 背包，精瘦
            new Look { skin = new Color(0.95f, 0.80f, 0.64f), hair = new Color(0.30f, 0.22f, 0.16f),
                       cloth = new Color(0.62f, 0.55f, 0.44f), acc = new Color(0.42f, 0.36f, 0.26f),
                       height = 0.96f, width = 0.96f, gear = 0 },
            // 守卫：深蓝制服 + 头盔肩甲，高大壮
            new Look { skin = new Color(1.00f, 0.88f, 0.74f), hair = new Color(0.16f, 0.14f, 0.14f),
                       cloth = new Color(0.34f, 0.42f, 0.58f), acc = new Color(0.28f, 0.31f, 0.36f),
                       height = 1.12f, width = 1.14f, gear = 1 },
            // 老人：白发驼背灰衣
            new Look { skin = new Color(0.96f, 0.84f, 0.72f), hair = new Color(0.92f, 0.92f, 0.90f),
                       cloth = new Color(0.70f, 0.70f, 0.66f), acc = new Color(0.50f, 0.48f, 0.44f),
                       height = 0.86f, width = 0.94f, gear = 4 },
            // 商贩：暗红衣 + 宽檐帽，矮胖
            new Look { skin = new Color(0.98f, 0.80f, 0.64f), hair = new Color(0.42f, 0.26f, 0.18f),
                       cloth = new Color(0.66f, 0.30f, 0.26f), acc = new Color(0.30f, 0.24f, 0.18f),
                       height = 0.92f, width = 1.16f, gear = 3 },
            // 面具人：军绿工装 + 防毒面具
            new Look { skin = new Color(0.92f, 0.78f, 0.64f), hair = new Color(0.20f, 0.18f, 0.16f),
                       cloth = new Color(0.40f, 0.48f, 0.34f), acc = new Color(0.18f, 0.18f, 0.16f),
                       height = 1.02f, width = 1.02f, gear = 2 },
            // 夜行者：黑灰衣 + 毛线帽，瘦高
            new Look { skin = new Color(0.88f, 0.74f, 0.62f), hair = new Color(0.12f, 0.11f, 0.11f),
                       cloth = new Color(0.26f, 0.26f, 0.30f), acc = new Color(0.22f, 0.22f, 0.24f),
                       height = 1.06f, width = 0.90f, gear = 4 },
        };

        public string DisplayName { get; private set; }

        void Start()
        {
            Apply();
        }

        public void Apply()
        {
            var rnd = new System.Random(seed != 0 ? seed : GetInstanceID());
            var look = Looks[Mathf.Abs(rnd.Next()) % Looks.Length];

            DisplayName = Names[Mathf.Abs(rnd.Next()) % Names.Length];

            if (applyScale)
            {
                float h = look.height * (0.98f + (float)rnd.NextDouble() * 0.06f);
                float w = look.width * (0.97f + (float)rnd.NextDouble() * 0.06f);
                var s = transform.localScale;
                transform.localScale = new Vector3(s.x * w, s.y * h, s.z * w);
            }

            Tint(look);
            if (addAccessory) AddGear(look, rnd);
        }

        // ---------------- 配色 ----------------

        /// <summary>
        /// UnityChan 卡通 shader 里 _Color 是乘算（combinedColor *= _Color.rgb），
        /// 所以贴图细节会保留，但整体会变暗 —— 取值要偏亮。
        /// </summary>
        void Tint(Look look)
        {
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
            {
                var smr = smrs[i];
                var mats = smr.materials;   // 实例化，避免改到共享材质
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;
                    string n = mat.name.ToLowerInvariant();
                    // 眼睛/脸/眉毛保留原色，否则表情会糊掉
                    if (n.Contains("eye") || n.Contains("face") || n.Contains("brow") || n.Contains("mouth")) continue;

                    Color c;
                    if (n.Contains("hair")) c = look.hair;
                    else if (n.Contains("skin")) c = look.skin;
                    else c = look.cloth;

                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", c * 0.62f);
                    changed = true;
                }
                if (changed) smr.materials = mats;
            }
        }

        // ---------------- 配件 ----------------

        void AddGear(Look look, System.Random rnd)
        {
            if (transform.Find("NpcGear") != null) return;

            var root = new GameObject("NpcGear").transform;
            root.SetParent(transform, false);

            var mat = new Material(Shader.Find("Standard"));
            mat.color = look.acc;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.15f);

            var head = FindBone("head");
            var chest = FindBone("chest");
            if (chest == null) chest = FindBone("spine");
            var hand = FindBone("hand_l");
            if (hand == null) hand = FindBone("hand");

            switch (look.gear)
            {
                case 0: // 斗篷 + 背包
                    AddCloak(root, chest, mat);
                    AddBox(root, chest, "Bag", new Vector3(0f, 0.10f, -0.20f), new Vector3(0.36f, 0.42f, 0.20f), mat);
                    break;
                case 1: // 头盔 + 肩甲
                    AddSphere(root, head, "Helmet", new Vector3(0f, 0.13f, 0f), new Vector3(0.25f, 0.17f, 0.27f), mat);
                    AddSphere(root, chest, "PauldronL", new Vector3(-0.20f, 0.20f, 0f), new Vector3(0.15f, 0.10f, 0.17f), mat);
                    AddSphere(root, chest, "PauldronR", new Vector3(0.20f, 0.20f, 0f), new Vector3(0.15f, 0.10f, 0.17f), mat);
                    break;
                case 2: // 防毒面具：面罩 + 两个滤罐
                    AddSphere(root, head, "Mask", new Vector3(0f, 0.02f, 0.11f), new Vector3(0.18f, 0.15f, 0.13f), mat);
                    AddCylinder(root, head, "FilterL", new Vector3(-0.08f, -0.02f, 0.15f), new Vector3(0.05f, 0.045f, 0.05f), mat);
                    AddCylinder(root, head, "FilterR", new Vector3(0.08f, -0.02f, 0.15f), new Vector3(0.05f, 0.045f, 0.05f), mat);
                    break;
                case 3: // 宽檐帽
                    AddCylinder(root, head, "HatBrim", new Vector3(0f, 0.15f, 0f), new Vector3(0.36f, 0.012f, 0.36f), mat);
                    AddCylinder(root, head, "HatTop", new Vector3(0f, 0.21f, 0f), new Vector3(0.18f, 0.08f, 0.18f), mat);
                    break;
                default: // 毛线帽 + 围巾
                    AddCylinder(root, head, "Beanie", new Vector3(0f, 0.16f, 0f), new Vector3(0.21f, 0.075f, 0.21f), mat);
                    AddCylinder(root, chest, "Scarf", new Vector3(0f, 0.26f, 0f), new Vector3(0.26f, 0.05f, 0.26f), mat);
                    if (hand != null)
                        AddCylinder(root, hand, "Stick", new Vector3(0f, -0.45f, 0.02f), new Vector3(0.025f, 0.55f, 0.025f), mat);
                    break;
            }
        }

        void AddCloak(Transform root, Transform chest, Material mat)
        {
            var cloak = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloak.name = "Cloak";
            Object.Destroy(cloak.GetComponent<Collider>());
            cloak.transform.SetParent(chest != null ? chest : root, false);
            cloak.transform.localPosition = chest != null ? new Vector3(0f, 0.02f, -0.16f) : new Vector3(0f, 1.05f, -0.16f);
            cloak.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            cloak.transform.localScale = new Vector3(0.46f, 0.85f, 0.05f);
            cloak.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void AddBox(Transform root, Transform bone, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(bone != null ? bone : root, false);
            go.transform.localPosition = bone != null ? pos : pos + new Vector3(0f, 1.1f, 0f);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void AddSphere(Transform root, Transform bone, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(bone != null ? bone : root, false);
            go.transform.localPosition = bone != null ? pos : pos + new Vector3(0f, 1.55f, 0f);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void AddCylinder(Transform root, Transform bone, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(bone != null ? bone : root, false);
            go.transform.localPosition = bone != null ? pos : pos + new Vector3(0f, 1.4f, 0f);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        Transform FindBone(string keyword)
        {
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
            {
                var bones = smrs[i].bones;
                if (bones == null) continue;
                for (int b = 0; b < bones.Length; b++)
                {
                    if (bones[b] == null) continue;
                    if (bones[b].name.ToLowerInvariant().Contains(keyword)) return bones[b];
                }
            }
            return null;
        }
    }
}
