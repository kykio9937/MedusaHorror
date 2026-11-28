using UnityEngine;
using MedusaHorror.UI;
namespace MedusaHorror.Gameplay
{
    /// <summary>可反复命中的训练靶，不给经验或信用点。</summary>
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        public bool IsDead => false;
        public Transform Transform => transform;
        public int HitCount { get; private set; }
        public float TotalDamage { get; private set; }
        public void TakeDamage(float amount)
        {
            if (amount <= 0) return;
            HitCount++; TotalDamage += amount;
            StoryBanner.Show("训练命中", "本次伤害 " + amount.ToString("F0") + " · 累计命中 " + HitCount + " 次", 2.5f);
        }
    }
}
