// ============================================================
// Last Archive - 成就系统
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 成就定义
    /// </summary>
    public class Achievement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Unlocked { get; set; }
        public int UnlockedDay { get; set; }  // 解锁时的天数
        public string Category { get; set; }  // 分类

        public string GetStatus()
        {
            return Unlocked ? $"✓ 已解锁 (第{UnlockedDay}天)" : "✗ 未解锁";
        }
    }

    /// <summary>
    /// 成就事件
    /// </summary>
    public struct OnAchievementUnlocked { public string AchievementId; public string AchievementName; }

    /// <summary>
    /// 成就系统 - 跟踪游戏里程碑
    /// </summary>
    public class AchievementSystem
    {
        private readonly Dictionary<string, Achievement> _achievements = new Dictionary<string, Achievement>();
        private int _currentDay = 1;

        public AchievementSystem() { }

        /// <summary>注册成就</summary>
        public void Register(Achievement achievement)
        {
            _achievements[achievement.Id] = achievement;
        }

        /// <summary>更新当前天数</summary>
        public void UpdateDay(int day)
        {
            _currentDay = day;
        }

        /// <summary>解锁成就</summary>
        public bool Unlock(string achievementId)
        {
            var ach = GetAchievement(achievementId);
            if (ach == null || ach.Unlocked) return false;

            ach.Unlocked = true;
            ach.UnlockedDay = _currentDay;
            EventBus.Publish(new OnAchievementUnlocked { AchievementId = achievementId, AchievementName = ach.Name });
            return true;
        }

        /// <summary>获取成就</summary>
        public Achievement GetAchievement(string id)
        {
            return _achievements.TryGetValue(id, out var a) ? a : null;
        }

        /// <summary>获取所有成就</summary>
        public List<Achievement> GetAllAchievements()
        {
            return new List<Achievement>(_achievements.Values);
        }

        /// <summary>获取已解锁成就</summary>
        public List<Achievement> GetUnlockedAchievements()
        {
            var result = new List<Achievement>();
            foreach (var a in _achievements.Values)
            {
                if (a.Unlocked) result.Add(a);
            }
            return result;
        }

        /// <summary>检查条件并自动解锁</summary>
        public void CheckAchievements(GameManager game)
        {
            // 生存成就
            if (game.Time.CurrentDay >= 3) Unlock("survive_3days");
            if (game.Time.CurrentDay >= 7) Unlock("survive_7days");
            if (game.Time.CurrentDay >= 14) Unlock("survive_14days");
            if (game.Time.CurrentDay >= 30) Unlock("survive_30days");

            // 资源成就
            if (game.Resources.GetResourceAmount(ResourceType.Food) >= 50) Unlock("food_hoarder");
            if (game.Resources.GetResourceAmount(ResourceType.MemoryShards) >= 10) Unlock("memory_collector");
            if (game.Resources.GetResourceAmount(ResourceType.MemoryShards) >= 30) Unlock("archive_master");

            // NPC成就
            if (game.NPCs.GetAliveCount() >= 5) Unlock("community_builder");
            foreach (var npc in game.NPCs.GetAllNPCs())
            {
                if (npc.GetRelationship("player") >= 80) Unlock("trusted_leader");
                break;
            }

            // 建筑成就
            bool allBuilt = true;
            foreach (var b in game.Buildings.GetAllBuildings())
            {
                if (!b.Built) allBuilt = false;
            }
            if (allBuilt) Unlock("builder");

            bool anyMaxLevel = false;
            foreach (var b in game.Buildings.GetAllBuildings())
            {
                if (b.Built && b.Level >= GameConfig.MaxBuildingLevel) anyMaxLevel = true;
            }
            if (anyMaxLevel) Unlock("master_architect");

            // 任务成就
            int completedCount = 0;
            foreach (var q in game.Quests.GetAllQuests())
            {
                if (q.Status == QuestStatus.Completed) completedCount++;
            }
            if (completedCount >= 1) Unlock("first_quest");
            if (completedCount >= 5) Unlock("quest_hunter");

            // 探索成就 - 检查是否有存档数据
            // (简化处理，通过事件触发)

            // 派系成就
            if (game.Factions != null)
            {
                foreach (var faction in game.Factions.GetAllFactions())
                {
                    if (faction.Unlocked && faction.GetReputationLevel() == ReputationLevel.Allied)
                    {
                        Unlock("faction_allied");
                        break;
                    }
                }
            }
        }

        /// <summary>获取解锁统计</summary>
        public string GetStats()
        {
            int total = _achievements.Count;
            int unlocked = 0;
            foreach (var a in _achievements.Values)
            {
                if (a.Unlocked) unlocked++;
            }
            return $"成就: {unlocked}/{total}";
        }
    }
}
