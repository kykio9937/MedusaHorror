namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 通用可交互物：玩家靠近并面向时显示 Hint，按 E 触发 Interact。
    /// 门、NPC、传送门、可购买房屋都实现这一接口。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>提示文本，例如「开门」「交谈」「点亮传送门」「花费 500 购买」。</summary>
        string Hint { get; }

        /// <summary>当前是否可交互（false 时不显示提示）。</summary>
        bool CanInteract { get; }

        void Interact();
    }
}
