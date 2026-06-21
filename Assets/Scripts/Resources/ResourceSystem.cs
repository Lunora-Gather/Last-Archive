// ============================================================
// Last Archive - 资源系统
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 资源系统 - 所有资源修改必须经过此系统
    /// </summary>
    public class ResourceSystem
    {
        private readonly Dictionary<ResourceType, int> _resources = new Dictionary<ResourceType, int>();

        public ResourceSystem()
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                _resources[type] = 0;
            }
        }

        /// <summary>初始化资源</summary>
        public void Initialize(int[] initial)
        {
            var types = (ResourceType[])Enum.GetValues(typeof(ResourceType));
            for (int i = 0; i < types.Length && i < initial.Length; i++)
            {
                _resources[types[i]] = initial[i];
            }
        }

        /// <summary>增加资源</summary>
        public void AddResource(ResourceType type, int amount)
        {
            if (amount <= 0) return;
            _resources[type] += amount;
            EventBus.Publish(new OnResourceChanged { Type = type, Amount = amount, Total = _resources[type] });
        }

        /// <summary>消耗资源（返回是否成功）</summary>
        public bool ConsumeResource(ResourceType type, int amount)
        {
            if (amount <= 0) return true;
            if (!HasEnough(type, amount)) return false;
            _resources[type] -= amount;
            EventBus.Publish(new OnResourceChanged { Type = type, Amount = -amount, Total = _resources[type] });
            return true;
        }

        /// <summary>消耗一组资源</summary>
        public bool ConsumeBundle(ResourceBundle bundle)
        {
            // 先检查全部够不够
            foreach (var a in bundle.Amounts)
            {
                if (!HasEnough(a.Type, a.Amount)) return false;
            }
            // 再实际扣
            foreach (var a in bundle.Amounts)
            {
                ConsumeResource(a.Type, a.Amount);
            }
            return true;
        }

        /// <summary>判断资源是否足够</summary>
        public bool HasEnough(ResourceType type, int amount)
        {
            return _resources[type] >= amount;
        }

        /// <summary>判断一组资源是否全部足够</summary>
        public bool HasEnoughBundle(ResourceBundle bundle)
        {
            foreach (var a in bundle.Amounts)
            {
                if (!HasEnough(a.Type, a.Amount)) return false;
            }
            return true;
        }

        /// <summary>获取资源数量</summary>
        public int GetResourceAmount(ResourceType type)
        {
            return _resources[type];
        }

        /// <summary>应用每日消耗</summary>
        public void ApplyDailyConsumption(int npcCount, int buildingPowerCost)
        {
            int foodCost = npcCount * GameConstants.DailyFoodConsumptionPerNPC;
            int waterCost = npcCount * GameConstants.DailyWaterConsumptionPerNPC;
            int powerCost = buildingPowerCost;

            // 食物不足触发危机
            int actualFood = Math.Min(foodCost, _resources[ResourceType.Food]);
            int actualWater = Math.Min(waterCost, _resources[ResourceType.Water]);
            int actualPower = Math.Min(powerCost, _resources[ResourceType.Power]);

            _resources[ResourceType.Food] -= actualFood;
            _resources[ResourceType.Water] -= actualWater;
            _resources[ResourceType.Power] -= actualPower;

            // 触发危机事件
            if (actualFood < foodCost)
            {
                EventBus.Publish(new OnResourceCrisis { Type = ResourceType.Food, Shortage = foodCost - actualFood });
            }
            if (actualWater < waterCost)
            {
                EventBus.Publish(new OnResourceCrisis { Type = ResourceType.Water, Shortage = waterCost - actualWater });
            }
            if (actualPower < powerCost)
            {
                EventBus.Publish(new OnResourceCrisis { Type = ResourceType.Power, Shortage = powerCost - actualPower });
            }
        }

        /// <summary>获取所有资源快照</summary>
        public Dictionary<ResourceType, int> GetAllResources()
        {
            return new Dictionary<ResourceType, int>(_resources);
        }

        /// <summary>从快照恢复</summary>
        public void RestoreFromSnapshot(Dictionary<ResourceType, int> snapshot)
        {
            foreach (var kv in snapshot)
            {
                _resources[kv.Key] = kv.Value;
            }
        }
    }
}
