// ============================================================
// Last Archive - 建筑数据结构
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    [Serializable]
    public class BuildingInstance
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Level { get; set; }
        public int MaxLevel { get; set; }
        public bool Built { get; set; }
        public bool Unlocked { get; set; }

        /// <summary>建造消耗</summary>
        public ResourceBundle BuildCost { get; set; } = new ResourceBundle();

        /// <summary>升级消耗（每级）</summary>
        public ResourceBundle UpgradeCost { get; set; } = new ResourceBundle();

        /// <summary>每日产出</summary>
        public ResourceBundle DailyOutput { get; set; } = new ResourceBundle();

        /// <summary>每日消耗</summary>
        public ResourceBundle DailyConsumption { get; set; } = new ResourceBundle();

        /// <summary>建筑效果描述</summary>
        public string EffectDescription { get; set; }

        public bool IsMaxLevel => Level >= MaxLevel;
    }
}
