// ============================================================
// Last Archive - 建筑系统
// ============================================================

using System;
using System.Collections.Generic;

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
            if (building == null || building.Built) return false;
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

        /// <summary>每日产出资源</summary>
        public void ProduceDailyResources()
        {
            foreach (var building in _buildings.Values)
            {
                if (!building.Built) continue;

                // 检查消耗是否足够
                if (building.DailyConsumption.Amounts.Count > 0)
                {
                    if (!_resourceSystem.HasEnoughBundle(building.DailyConsumption)) continue;
                    _resourceSystem.ConsumeBundle(building.DailyConsumption);
                }

                // 产出（根据等级缩放）
                foreach (var output in building.DailyOutput.Amounts)
                {
                    int amount = output.Amount * building.Level;
                    _resourceSystem.AddResource(output.Type, amount);
                }
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
