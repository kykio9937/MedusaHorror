using UnityEngine;

namespace MedusaHorror.Core
{
    /// <summary>统一价格计算：村庄支线完成后所有消费打 8.5 折。</summary>
    public static class Pricing
    {
        public static float Discount { get { return SaveSystem.Data.villageDiscount; } }

        public static int Apply(int basePrice)
        {
            if (basePrice <= 0) return basePrice;
            float d = Discount;
            if (d <= 0f) return basePrice;
            return Mathf.Max(1, Mathf.RoundToInt(basePrice * (1f - d)));
        }

        public static string Tag(int basePrice)
        {
            return Discount > 0f ? "（村庄折扣）" : "";
        }
    }
}
