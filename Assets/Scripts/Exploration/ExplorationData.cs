// ============================================================
// Last Archive - 探索数据结构
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 房间数据
    /// </summary>
    [Serializable]
    public class RoomData
    {
        public string Id;
        public string Name;
        public string Description;
        public bool Visited;
        public int DangerLevel;        // 0-10
        public List<ResourceAmount> LootTable = new List<ResourceAmount>();
        public List<string> ConnectedRooms = new List<string>();
        public ExplorationEventType EventType;
        public string RequiredItem;     // 需要的物品ID（空=不需要）
        public bool Locked;
    }

    /// <summary>
    /// 探索地图数据
    /// </summary>
    [Serializable]
    public class ExplorationMapData
    {
        public string Id;
        public string Name;
        public string Description;
        public List<ResourceType> MainLoot = new List<ResourceType>();
        public List<string> MainEnemies = new List<string>();
        public List<RoomData> Rooms = new List<RoomData>();
        public bool Unlocked = true;
    }

    /// <summary>
    /// 探索事件
    /// </summary>
    [Serializable]
    public class ExplorationEvent
    {
        public string Id;
        public ExplorationEventType Type;
        public string Description;
        public List<ResourceAmount> Rewards = new List<ResourceAmount>();
        public string EnemyId;         // 如果是战斗事件
        public int EmotionalWeight;
    }

    /// <summary>
    /// 探索结果
    /// </summary>
    [Serializable]
    public class ExplorationResult
    {
        public List<ResourceAmount> CollectedResources = new List<ResourceAmount>();
        public List<string> CollectedMemoryShards = new List<string>();
        public List<string> InjuredNPCs = new List<string>();
        public List<ExplorationEvent> Events = new List<ExplorationEvent>();
        public bool HadCombat;
        public bool CombatVictory;
    }

    /// <summary>
    /// 技能判定事件
    /// </summary>
    [Serializable]
    public class SkillCheckEvent
    {
        public string Description;
        public string TargetSkill; // "medical", "engineering", "combat"
        public int Difficulty;     // 6-12
        public bool Resolved;
        public string ResultText;
    }
}
