// ============================================================
// Last Archive - 游戏配置
// ============================================================

namespace LastArchive
{
    /// <summary>
    /// 游戏配置，集中管理可调参数
    /// </summary>
    public static class GameConfig
    {
        // ===== 资源配置 =====
        public static readonly int[] InitialResources = new int[]
        {
            GameConstants.InitialFood,       // Food
            GameConstants.InitialWater,      // Water
            GameConstants.InitialPower,      // Power
            GameConstants.InitialMedicine,   // Medicine
            GameConstants.InitialParts,      // Parts
            GameConstants.InitialMemoryShards // MemoryShards
        };

        public static readonly string[] ResourceNames = new string[]
        {
            "食物", "水", "电力", "药品", "零件", "记忆碎片"
        };

        public static readonly string[] ResourceNamesEN = new string[]
        {
            "Food", "Water", "Power", "Medicine", "Parts", "MemoryShards"
        };

        // ===== NPC配置 =====
        public const int MaxNPCHealth = 100;
        public const int MaxNPCMorale = 100;
        public const int MaxNPCHunger = 100;
        public const int MaxNPCFatigue = 100;
        public const int MaxNPCLoyalty = 100;
        public const int MinRelationship = -100;
        public const int MaxRelationship = 100;

        // ===== 每日变化 =====
        public const int DailyHungerIncrease = 10;     // 饥饿每日增加
        public const int DailyFatigueIncrease = 5;      // 疲劳每日增加
        public const int HungerMoralePenalty = 5;        // 饥饿时士气惩罚
        public const int LowMoraleLoyaltyPenalty = 2;    // 低士气时忠诚惩罚

        // ===== 建筑配置 =====
        public const int MaxBuildingLevel = 3;

        // ===== 探索配置 =====
        public const int MaxExplorationTeamSize = 3;
        public const int BaseSearchSuccessRate = 70;     // 基础搜索成功率%
        public const int DangerInjuryChance = 30;        // 危险房间受伤概率%

        // ===== 战斗配置 =====
        public const int MedicineHealAmount = 15;        // 药品治疗量
        public const int MedicineCostPerUse = 1;         // 每次用药消耗药品数

        // ===== 日志 =====
        public const bool EnableDebugLog = true;
    }
}
