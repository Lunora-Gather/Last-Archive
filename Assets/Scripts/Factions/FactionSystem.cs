// ============================================================
// Last Archive - 派系系统
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 派系类型
    /// </summary>
    public enum FactionType
    {
        ArchiveOrder,     // 档案教团 - 致力于保存一切记忆
        Survivalists,     // 生存主义者 - 只关心活下去
        Wanderers         // 流浪者联盟 - 探索和自由
    }

    /// <summary>
    /// 派系声望等级
    /// </summary>
    public enum ReputationLevel
    {
        Hostile,     // 敌对 (-100 ~ -50)
        Unfriendly,  // 不友好 (-49 ~ -10)
        Neutral,     // 中立 (-9 ~ 9)
        Friendly,    // 友好 (10 ~ 49)
        Allied       // 同盟 (50 ~ 100)
    }

    /// <summary>
    /// 派系实例
    /// </summary>
    public class FactionInstance
    {
        public FactionType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Leader { get; set; }
        public int Reputation { get; set; }  // -100 ~ 100
        public bool Unlocked { get; set; }
        public List<string> MemberNPCIds { get; set; } = new List<string>();
        public Dictionary<string, bool> FactionFlags { get; set; } = new Dictionary<string, bool>();

        public ReputationLevel GetReputationLevel()
        {
            if (Reputation <= -50) return ReputationLevel.Hostile;
            if (Reputation <= -10) return ReputationLevel.Unfriendly;
            if (Reputation < 10) return ReputationLevel.Neutral;
            if (Reputation < 50) return ReputationLevel.Friendly;
            return ReputationLevel.Allied;
        }

        public string GetReputationName()
        {
            switch (GetReputationLevel())
            {
                case ReputationLevel.Hostile: return "敌对";
                case ReputationLevel.Unfriendly: return "不友好";
                case ReputationLevel.Neutral: return "中立";
                case ReputationLevel.Friendly: return "友好";
                case ReputationLevel.Allied: return "同盟";
                default: return "未知";
            }
        }
    }

    /// <summary>
    /// 派系事件
    /// </summary>
    public struct OnFactionReputationChanged
    {
        public FactionType Faction;
        public int OldReputation;
        public int NewReputation;
        public ReputationLevel OldLevel;
        public ReputationLevel NewLevel;
    }

    public struct OnFactionUnlocked { public FactionType Faction; }

    /// <summary>
    /// 派系系统 - 管理派系声望、解锁和交互
    /// </summary>
    public class FactionSystem
    {
        private readonly Dictionary<FactionType, FactionInstance> _factions = new Dictionary<FactionType, FactionInstance>();

        public FactionSystem() { }

        /// <summary>添加派系</summary>
        public void AddFaction(FactionInstance faction)
        {
            _factions[faction.Type] = faction;
        }

        /// <summary>获取派系</summary>
        public FactionInstance GetFaction(FactionType type)
        {
            return _factions.TryGetValue(type, out var f) ? f : null;
        }

        /// <summary>获取所有派系</summary>
        public List<FactionInstance> GetAllFactions()
        {
            return new List<FactionInstance>(_factions.Values);
        }

        /// <summary>获取已解锁派系</summary>
        public List<FactionInstance> GetUnlockedFactions()
        {
            var result = new List<FactionInstance>();
            foreach (var f in _factions.Values)
            {
                if (f.Unlocked) result.Add(f);
            }
            return result;
        }

        /// <summary>修改声望</summary>
        public void ChangeReputation(FactionType type, int amount)
        {
            var faction = GetFaction(type);
            if (faction == null) return;

            var oldRep = faction.Reputation;
            var oldLevel = faction.GetReputationLevel();

            faction.Reputation = Math.Max(-100, Math.Min(100, faction.Reputation + amount));

            var newLevel = faction.GetReputationLevel();
            if (oldRep != faction.Reputation)
            {
                EventBus.Publish(new OnFactionReputationChanged
                {
                    Faction = type,
                    OldReputation = oldRep,
                    NewReputation = faction.Reputation,
                    OldLevel = oldLevel,
                    NewLevel = newLevel
                });
            }
        }

        /// <summary>解锁派系</summary>
        public bool UnlockFaction(FactionType type)
        {
            var faction = GetFaction(type);
            if (faction == null || faction.Unlocked) return false;

            faction.Unlocked = true;
            EventBus.Publish(new OnFactionUnlocked { Faction = type });
            return true;
        }

        /// <summary>添加派系成员</summary>
        public void AddMember(FactionType type, string npcId)
        {
            var faction = GetFaction(type);
            if (faction == null) return;
            if (!faction.MemberNPCIds.Contains(npcId))
            {
                faction.MemberNPCIds.Add(npcId);
            }
        }

        /// <summary>获取NPC所属派系</summary>
        public FactionType? GetNPCFaction(string npcId)
        {
            foreach (var faction in _factions.Values)
            {
                if (faction.MemberNPCIds.Contains(npcId))
                    return faction.Type;
            }
            return null;
        }

        /// <summary>检查声望是否足够</summary>
        public bool HasReputation(FactionType type, int required)
        {
            var faction = GetFaction(type);
            if (faction == null) return false;
            return faction.Reputation >= required;
        }

        /// <summary>检查声望等级</summary>
        public bool HasReputationLevel(FactionType type, ReputationLevel required)
        {
            var faction = GetFaction(type);
            if (faction == null) return false;
            return (int)faction.GetReputationLevel() >= (int)required;
        }
    }
}
