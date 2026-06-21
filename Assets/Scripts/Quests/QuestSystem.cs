// ============================================================
// Last Archive - 任务系统
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 任务系统 - 所有任务状态修改必须经过此系统
    /// </summary>
    public class QuestSystem
    {
        private readonly Dictionary<string, QuestData> _quests = new Dictionary<string, QuestData>();
        private ResourceSystem _resourceSystem;
        private NPCSystem _npcSystem;

        public QuestSystem(ResourceSystem resourceSystem, NPCSystem npcSystem)
        {
            _resourceSystem = resourceSystem;
            _npcSystem = npcSystem;
        }

        /// <summary>添加任务</summary>
        public void AddQuest(QuestData quest)
        {
            _quests[quest.QuestId] = quest;
        }

        /// <summary>获取任务</summary>
        public QuestData GetQuest(string questId)
        {
            return _quests.TryGetValue(questId, out var q) ? q : null;
        }

        /// <summary>获取所有任务</summary>
        public List<QuestData> GetAllQuests()
        {
            return new List<QuestData>(_quests.Values);
        }

        /// <summary>获取活跃任务</summary>
        public List<QuestData> GetActiveQuests()
        {
            var result = new List<QuestData>();
            foreach (var q in _quests.Values)
            {
                if (q.Status == QuestStatus.Active) result.Add(q);
            }
            return result;
        }

        /// <summary>开始任务</summary>
        public bool StartQuest(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null || quest.Status != QuestStatus.NotStarted) return false;
            quest.Status = QuestStatus.Active;
            EventBus.Publish(new OnQuestStatusChanged { QuestId = questId, OldStatus = QuestStatus.NotStarted, NewStatus = QuestStatus.Active });
            return true;
        }

        /// <summary>更新目标进度</summary>
        public void UpdateObjectiveProgress(string questId, ObjectiveType type, string targetId, int progress)
        {
            if (string.IsNullOrEmpty(questId))
            {
                // 如果没有指定具体任务，更新所有活跃任务的该类型目标进度
                var activeQuests = GetActiveQuests();
                foreach (var q in activeQuests)
                {
                    bool updated = false;
                    foreach (var obj in q.Objectives)
                    {
                        if (obj.Type == type && (string.IsNullOrEmpty(targetId) || obj.TargetId == targetId))
                        {
                            obj.CurrentProgress = Math.Min(obj.RequiredAmount, obj.CurrentProgress + progress);
                            updated = true;
                        }
                    }
                    if (updated && q.AllObjectivesComplete)
                    {
                        CompleteQuest(q.QuestId);
                    }
                }
                return;
            }

            var quest = GetQuest(questId);
            if (quest == null || quest.Status != QuestStatus.Active) return;

            foreach (var obj in quest.Objectives)
            {
                if (obj.Type == type && obj.TargetId == targetId)
                {
                    obj.CurrentProgress = Math.Min(obj.RequiredAmount, obj.CurrentProgress + progress);
                }
            }

            // 自动检查完成
            if (quest.AllObjectivesComplete)
            {
                CompleteQuest(questId);
            }
        }

        /// <summary>更新资源收集类目标</summary>
        public void UpdateResourceObjectives(ResourceType type, int amount)
        {
            foreach (var quest in _quests.Values)
            {
                if (quest.Status != QuestStatus.Active) continue;
                foreach (var obj in quest.Objectives)
                {
                    if (obj.Type == ObjectiveType.CollectResource && obj.TargetId == type.ToString())
                    {
                        obj.CurrentProgress = _resourceSystem.GetResourceAmount(type);
                        if (obj.CurrentProgress > obj.RequiredAmount)
                            obj.CurrentProgress = obj.RequiredAmount;
                    }
                }
                if (quest.AllObjectivesComplete)
                {
                    CompleteQuest(quest.QuestId);
                }
            }
        }

        /// <summary>更新建筑类目标</summary>
        public void UpdateBuildingObjectives(string buildingId, bool isUpgrade, int level)
        {
            foreach (var quest in _quests.Values)
            {
                if (quest.Status != QuestStatus.Active) continue;
                foreach (var obj in quest.Objectives)
                {
                    if (obj.Type == ObjectiveType.UpgradeBuilding && obj.TargetId == buildingId)
                    {
                        obj.CurrentProgress = level;
                    }
                    if (obj.Type == ObjectiveType.BuildFacility && obj.TargetId == buildingId)
                    {
                        obj.CurrentProgress = 1;
                    }
                }
                if (quest.AllObjectivesComplete)
                {
                    CompleteQuest(quest.QuestId);
                }
            }
        }

        /// <summary>更新访问地点目标</summary>
        public void UpdateVisitLocationObjectives(string locationId)
        {
            foreach (var quest in _quests.Values)
            {
                if (quest.Status != QuestStatus.Active) continue;
                foreach (var obj in quest.Objectives)
                {
                    if (obj.Type == ObjectiveType.VisitLocation && obj.TargetId == locationId)
                    {
                        obj.CurrentProgress = Math.Min(obj.RequiredAmount, obj.CurrentProgress + 1);
                    }
                    if (obj.Type == ObjectiveType.ReachRoom && obj.TargetId == locationId)
                    {
                        obj.CurrentProgress = Math.Min(obj.RequiredAmount, obj.CurrentProgress + 1);
                    }
                }
                if (quest.AllObjectivesComplete)
                {
                    CompleteQuest(quest.QuestId);
                }
            }
        }

        /// <summary>更新存活天数目标</summary>
        public void UpdateSurviveDaysObjectives(int currentDay)
        {
            foreach (var quest in _quests.Values)
            {
                if (quest.Status != QuestStatus.Active) continue;
                foreach (var obj in quest.Objectives)
                {
                    if (obj.Type == ObjectiveType.SurviveDays)
                    {
                        obj.CurrentProgress = currentDay;
                    }
                }
                if (quest.AllObjectivesComplete)
                {
                    CompleteQuest(quest.QuestId);
                }
            }
        }

        /// <summary>完成任务</summary>
        public bool CompleteQuest(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null || quest.Status != QuestStatus.Active) return false;

            var oldStatus = quest.Status;
            quest.Status = QuestStatus.Completed;
            GiveRewards(questId);

            // 记录NPC记忆
            foreach (var npcId in quest.RelatedNPCs)
            {
                _npcSystem.AddMemory(npcId, new MemoryEntry
                {
                    MemoryId = Guid.NewGuid().ToString(),
                    Day = 0,
                    Actor = "player",
                    Target = npcId,
                    EventType = MemoryEventType.QuestCompleted,
                    Description = $"完成了任务：{quest.Title}",
                    EmotionalWeight = 3
                });
            }

            EventBus.Publish(new OnQuestStatusChanged { QuestId = questId, OldStatus = oldStatus, NewStatus = QuestStatus.Completed });
            return true;
        }

        /// <summary>失败任务</summary>
        public bool FailQuest(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null || quest.Status != QuestStatus.Active) return false;

            var oldStatus = quest.Status;
            quest.Status = QuestStatus.Failed;

            // 失败惩罚
            foreach (var consequence in quest.FailureConsequences)
            {
                if (consequence.ResourceType.HasValue && consequence.ResourceAmount < 0)
                {
                    _resourceSystem.ConsumeResource(consequence.ResourceType.Value, -consequence.ResourceAmount);
                }
                if (!string.IsNullOrEmpty(consequence.RelationshipTarget))
                {
                    _npcSystem.UpdateRelationship(consequence.RelationshipTarget, "player", consequence.RelationshipAmount);
                }
            }

            EventBus.Publish(new OnQuestStatusChanged { QuestId = questId, OldStatus = oldStatus, NewStatus = QuestStatus.Failed });
            return true;
        }

        /// <summary>发放奖励</summary>
        public void GiveRewards(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null) return;

            foreach (var reward in quest.Rewards)
            {
                if (reward.ResourceType.HasValue && reward.ResourceAmount > 0)
                {
                    _resourceSystem.AddResource(reward.ResourceType.Value, reward.ResourceAmount);
                }
                if (!string.IsNullOrEmpty(reward.RelationshipTarget))
                {
                    _npcSystem.UpdateRelationship(reward.RelationshipTarget, "player", reward.RelationshipAmount);
                }
            }
        }
    }
}
