// ============================================================
// Last Archive - 建筑系统
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace LastArchive
{
    /// <summary>
    /// 建筑系统 - 管理建筑建造、升级和产出
    /// </summary>
    public class BuildingSystem
    {
        private readonly Dictionary<string, BuildingInstance> _buildings = new Dictionary<string, BuildingInstance>();
        private ResourceSystem _resourceSystem;

        public BuildingSystem(ResourceSystem resourceSystem)
        {
            _resourceSystem = resourceSystem;
        }

        /// <summary>添加建筑</summary>
        public void AddBuilding(BuildingInstance building)
        {
            _buildings[building.Id] = building;
        }

        /// <summary>获取建筑</summary>
        public BuildingInstance GetBuilding(string id)
        {
            return _buildings.TryGetValue(id, out var b) ? b : null;
        }

        /// <summary>获取所有建筑</summary>
        public List<BuildingInstance> GetAllBuildings()
        {
            return new List<BuildingInstance>(_buildings.Values);
        }

        /// <summary>建造建筑</summary>
        public bool Build(string buildingId)
        {
            var building = GetBuilding(buildingId);
            if (building == null || !building.Unlocked || building.Built) return false;
            if (!_resourceSystem.HasEnoughBundle(building.BuildCost)) return false;

            _resourceSystem.ConsumeBundle(building.BuildCost);
            building.Built = true;
            building.Level = 1;
            EventBus.Publish(new OnBuildingBuilt { BuildingId = buildingId });
            return true;
        }

        /// <summary>升级建筑</summary>
        public bool Upgrade(string buildingId)
        {
            var building = GetBuilding(buildingId);
            if (building == null || !building.Built || building.IsMaxLevel) return false;
            if (!_resourceSystem.HasEnoughBundle(building.UpgradeCost)) return false;

            _resourceSystem.ConsumeBundle(building.UpgradeCost);
            building.Level++;
            EventBus.Publish(new OnBuildingUpgraded { BuildingId = buildingId, NewLevel = building.Level });
            return true;
        }

        internal IReadOnlyList<BuildingInstance> GetOperatingBuildings()
        {
            var available = _resourceSystem.GetAllResources();
            var operating = new List<BuildingInstance>();
            foreach (var building in _buildings.Values)
            {
                if (!building.Built || building.DailyConsumption.Amounts.Any(cost => available[cost.Type] < cost.Amount))
                    continue;
                foreach (var cost in building.DailyConsumption.Amounts) available[cost.Type] -= cost.Amount;
                foreach (var output in building.DailyOutput.Amounts) available[output.Type] += output.Amount * building.Level;
                operating.Add(building);
            }
            return operating;
        }

        /// <summary>每日产出资源</summary>
        public void ProduceDailyResources()
        {
            foreach (var building in GetOperatingBuildings())
            {
                _resourceSystem.ConsumeBundle(building.DailyConsumption);
                foreach (var output in building.DailyOutput.Amounts)
                    _resourceSystem.AddResource(output.Type, output.Amount * building.Level);
            }
        }

        /// <summary>获取建筑每日电力消耗</summary>
        public int GetDailyPowerConsumption()
        {
            int total = 0;
            foreach (var building in _buildings.Values)
            {
                if (!building.Built) continue;
                total += building.DailyConsumption.Get(ResourceType.Power);
            }
            return total;
        }

        /// <summary>获取温室食物产出</summary>
        public int GetGreenhouseFoodProduction()
        {
            var gh = GetBuilding("greenhouse");
            if (gh == null || !gh.Built) return 0;
            return gh.DailyOutput.Get(ResourceType.Food) * gh.Level;
        }
    }
}
