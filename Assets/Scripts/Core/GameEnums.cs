// ============================================================
// Last Archive - 核心枚举与常量
// ============================================================

namespace LastArchive
{
    /// <summary>
    /// 游戏阶段
    /// </summary>
    public enum GamePhase
    {
        Day,      // 白天经营
        Night,    // 夜晚探索
        Summary   // 每日结算
    }

    /// <summary>
    /// 资源类型
    /// </summary>
    public enum ResourceType
    {
        Food,          // 食物
        Water,         // 水
        Power,         // 电力
        Medicine,      // 药品
        Parts,         // 零件
        MemoryShards   // 记忆碎片
    }

    /// <summary>
    /// NPC状态
    /// </summary>
    public enum NPCStatus
    {
        Idle,      // 空闲
        Working,   // 工作中
        Injured,   // 受伤
        Missing,   // 失踪
        Dead       // 死亡
    }

    /// <summary>
    /// NPC角色
    /// </summary>
    public enum NPCRole
    {
        Doctor,    // 医生
        Engineer,  // 工程师
        Scout,     // 侦察兵
        Child,     // 儿童
        Stranger   // 陌生人
    }

    /// <summary>
    /// NPC性格标签
    /// </summary>
    public enum NPCTrait
    {
        Cautious,   // 谨慎
        Brave,      // 勇敢
        Selfish,    // 自私
        Kind,       // 善良
        Suspicious, // 多疑
        Loyal,      // 忠诚
        Practical,  // 务实
        Mysterious, // 神秘
        Fearful,    // 胆怯
        Calm        // 冷静
    }

    /// <summary>
    /// NPC工作类型
    /// </summary>
    public enum WorkType
    {
        None,
        Doctor,      // 医疗工作
        Engineer,    // 工程工作
        Scavenging,  // 搜集工作
        Guarding,    // 守卫工作
        Cooking,     // 烹饪工作
        Farming      // 种植工作
    }

    /// <summary>
    /// 任务类型
    /// </summary>
    public enum QuestType
    {
        MainQuest,        // 主线任务
        SideQuest,        // 支线任务
        ExplorationQuest, // 探索任务
        NPCQuest          // 角色任务
    }

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum QuestStatus
    {
        NotStarted, // 未开始
        Active,     // 进行中
        Completed,  // 已完成
        Failed      // 已失败
    }

    /// <summary>
    /// 任务目标类型
    /// </summary>
    public enum ObjectiveType
    {
        CollectItem,      // 收集物品
        CollectResource,  // 收集资源
        VisitLocation,    // 访问地点
        RescueNPC,        // 救援NPC
        BuildFacility,    // 建造设施
        UpgradeBuilding,  // 升级建筑
        SurviveDays,      // 存活天数
        TalkToNPC,        // 与NPC对话
        DefeatEnemy,      // 击败敌人
        ReachRoom         // 到达房间
    }

    /// <summary>
    /// 战斗动作
    /// </summary>
    public enum CombatAction
    {
        Attack,       // 攻击
        Defend,       // 防御
        UseMedicine,  // 使用药品
        Escape,       // 逃跑
        UseSkill,     // 使用技能
        UseItem       // 使用物品
    }

    /// <summary>
    /// 记忆事件类型
    /// </summary>
    public enum MemoryEventType
    {
        PlayerHelpedNPC,       // 玩家帮助NPC
        PlayerIgnoredNPC,      // 玩家忽视NPC
        PlayerSavedSomeone,    // 玩家拯救某人
        PlayerAbandonedSomeone,// 玩家抛弃某人
        NPCJoinedExploration,  // NPC参与探索
        NPCInjured,            // NPC受伤
        ResourceCrisis,        // 资源危机
        QuestCompleted,        // 任务完成
        QuestFailed,           // 任务失败
        ImportantDialogue,     // 重要对话
        MainStoryDiscovery     // 主线发现
    }

    /// <summary>
    /// 探索事件类型
    /// </summary>
    public enum ExplorationEventType
    {
        FindResources,    // 发现物资
        FindMemoryShard,  // 发现记忆碎片
        EnemyEncounter,   // 遭遇敌人
        FindSurvivor,     // 发现幸存者
        TrapEvent,        // 陷阱事件
        StoryEvent        // 剧情事件
    }

    /// <summary>
    /// 游戏常量
    /// </summary>
    public static class GameConstants
    {
        // 初始资源
        public const int InitialFood = 50;
        public const int InitialWater = 50;
        public const int InitialPower = 15;
        public const int InitialMedicine = 8;
        public const int InitialParts = 15;
        public const int InitialMemoryShards = 0;

        // 每日消耗（每NPC）
        public const int DailyFoodConsumptionPerNPC = 1;
        public const int DailyWaterConsumptionPerNPC = 1;

        // 温室每日产出
        public const int GreenhouseFoodProduction = 5;

        // 温室建造消耗
        public const int GreenhouseBuildPartsCost = 5;
        public const int GreenhouseBuildPowerCost = 2;

        // 建筑升级基础消耗
        public const int BuildingUpgradePartsBase = 10;
        public const int BuildingUpgradePowerBase = 3;

        // 存档版本
        public const string SaveVersion = "0.1.0";

        // 战斗逃跑成功率
        public const float EscapeSuccessRate = 0.5f;

        // 防御减伤比例
        public const float DefenseDamageReduction = 0.5f;
    }
}
