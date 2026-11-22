using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// NPC 外观差异化：身高、体型、随机名字，外加随机小配饰（背包 / 帽子 / 围巾）。
    /// 注意：角色模型用 UnityChan 自定义 shader，直接改材质颜色会变黑色剪影，
    /// 所以这里绝不碰模型材质，只用几何配饰和比例来做区分。
    /// </summary>
    public class NpcVariation : MonoBehaviour
    {
        public int seed = 0;
        public bool applyScale = true;
        public bool addAccessory = true;

        static readonly Color[] Palette =
        {
            new Color(0.85f, 0.35f, 0.35f),
            new Color(0.35f, 0.55f, 0.85f),
            new Color(0.40f, 0.72f, 0.45f),
            new Color(0.85f, 0.72f, 0.32f),
            new Color(0.62f, 0.42f, 0.78f),
            new Color(0.85f, 0.55f, 0.30f),
            new Color(0.55f, 0.60f, 0.65f),
            new Color(0.90f, 0.85f, 0.80f),
        };

        static readonly string[] Names =
        {
            "阿岩", "老周", "小满", "铁柱", "阿岚", "苏禾",
            "瘸子老陈", "阿沁", "打更人", "拾荒客", "面摊老板", "阿木",
        };

        public string DisplayName { get; private set; }

        void Start()
        {
            Apply();
        }

        public void Apply()
        {
            var rnd = new System.Random(seed != 0 ? seed : GetInstanceID());

            if (applyScale)
            {
                float h = 0.90f + (float)rnd.NextDouble() * 0.22f;
                float w = 0.94f + (float)rnd.NextDouble() * 0.14f;
                var s = transform.localScale;
                transform.localScale = new Vector3(s.x * w, s.y * h, s.z * w);
            }

            DisplayName = Names[rnd.Next(Names.Length)];

            if (addAccessory) AddAccessory(rnd);
        }

        void AddAccessory(System.Random rnd)
        {
            if (transform.Find("NpcAcc") != null) return;

            var root = new GameObject("NpcAcc").transform;
            root.SetParent(transform, false);

            var color = Palette[rnd.Next(Palette.Length)];
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);

            int kind = rnd.Next(3);
            var head = FindBone("head");
            var chest = FindBone("spine");
            if (chest == null) chest = FindBone("chest");

            switch (kind)
            {
                case 0: // 背包
                {
                    var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    back.name = "Bag";
                    Object.Destroy(back.GetComponent<Collider>());
                    back.transform.SetParent(chest != null ? chest : root, false);
                    back.transform.localPosition = chest != null ? new Vector3(0f, 0.12f, -0.18f) : new Vector3(0f, 1.1f, -0.2f);
                    back.transform.localScale = new Vector3(0.34f, 0.4f, 0.18f);
                    back.GetComponent<Renderer>().sharedMaterial = mat;
                    break;
                }
                case 1: // 帽子
                {
                    var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    cap.name = "Cap";
                    Object.Destroy(cap.GetComponent<Collider>());
                    cap.transform.SetParent(head != null ? head : root, false);
                    cap.transform.localPosition = head != null ? new Vector3(0f, 0.13f, 0f) : new Vector3(0f, 1.66f, 0f);
                    cap.transform.localScale = new Vector3(0.22f, 0.05f, 0.22f);
                    cap.GetComponent<Renderer>().sharedMaterial = mat;
                    break;
                }
                default: // 围巾
                {
                    var scarf = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    scarf.name = "Scarf";
                    Object.Destroy(scarf.GetComponent<Collider>());
                    scarf.transform.SetParent(chest != null ? chest : root, false);
                    scarf.transform.localPosition = chest != null ? new Vector3(0f, 0.26f, 0f) : new Vector3(0f, 1.44f, 0f);
                    scarf.transform.localScale = new Vector3(0.24f, 0.04f, 0.24f);
                    scarf.GetComponent<Renderer>().sharedMaterial = mat;
                    break;
                }
            }
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
