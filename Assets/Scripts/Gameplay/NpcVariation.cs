using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// NPC 外观差异化：同一个模型也能看出是不同的人——身高、体型、服装配色、随机发型配件。
    /// 运行时给每个 NPC 单独实例化材质，互不影响。
    /// </summary>
    public class NpcVariation : MonoBehaviour
    {
        public int seed = 0;
        public bool applyScale = true;
        public bool applyTint = true;

        static readonly Color[] Palette =
        {
            new Color(0.85f, 0.35f, 0.35f),   // 红
            new Color(0.35f, 0.55f, 0.85f),   // 蓝
            new Color(0.40f, 0.72f, 0.45f),   // 绿
            new Color(0.85f, 0.72f, 0.32f),   // 黄
            new Color(0.62f, 0.42f, 0.78f),   // 紫
            new Color(0.85f, 0.55f, 0.30f),   // 橙
            new Color(0.55f, 0.60f, 0.65f),   // 灰蓝
            new Color(0.90f, 0.85f, 0.80f),   // 米白
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
                float h = 0.92f + (float)rnd.NextDouble() * 0.18f;
                float w = 0.95f + (float)rnd.NextDouble() * 0.12f;
                var s = transform.localScale;
                transform.localScale = new Vector3(s.x * w, s.y * h, s.z * w);
            }

            DisplayName = Names[rnd.Next(Names.Length)];

            if (!applyTint) return;

            var tint = Palette[rnd.Next(Palette.Length)];
            float strength = 0.45f + (float)rnd.NextDouble() * 0.25f;

            var rends = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null) continue;
                var mats = r.materials;    // 每个 NPC 独立材质实例
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;
                    if (!mat.HasProperty("_Color")) continue;
                    // 皮肤色保留，只染衣服和头发
                    if (mat.name.ToLowerInvariant().Contains("skin")) continue;
                    mat.color = Color.Lerp(mat.color, tint, strength);
                    changed = true;
                }
                if (changed) r.materials = mats;
            }
        }
    }
}
