// ============================================================
// Last Archive - 资源数据结构
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    [Serializable]
    public struct ResourceAmount
    {
        public ResourceType Type { get; set; }
        public int Amount { get; set; }

        public ResourceAmount(ResourceType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    /// <summary>
    /// 资源数量集合（用于建筑消耗/产出、任务奖励等）
    /// </summary>
    [Serializable]
    public class ResourceBundle
    {
        public List<ResourceAmount> Amounts { get; set; } = new List<ResourceAmount>();

        public void Add(ResourceType type, int amount)
        {
            for (int i = 0; i < Amounts.Count; i++)
            {
                if (Amounts[i].Type == type)
                {
                    Amounts[i] = new ResourceAmount(type, Amounts[i].Amount + amount);
                    return;
                }
            }
            Amounts.Add(new ResourceAmount(type, amount));
        }

        public int Get(ResourceType type)
        {
            foreach (var a in Amounts)
            {
                if (a.Type == type) return a.Amount;
            }
            return 0;
        }
    }
}
