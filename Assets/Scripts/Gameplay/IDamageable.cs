using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 可受击目标。Boss、野外怪物、可破坏物都实现它，武器判定统一走这个接口。
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        Transform Transform { get; }
        void TakeDamage(float amount);
    }
}
