// ============================================================
// Last Archive - 任务数据结构
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    [Serializable]
    public class QuestObjective
    {
        public ObjectiveType Type { get; set; }
        public string TargetId { get; set; }
        public int RequiredAmount { get; set; }
        public int CurrentProgress { get; set; }
        public bool IsComplete => CurrentProgress >= RequiredAmount;
    }

    /// <summary>
    /// 任务奖励
    /// </summary>
    [Serializable]
    public class QuestReward
    {
        public ResourceType? ResourceType { get; set; }
        public int ResourceAmount { get; set; }
        public string RelationshipTarget { get; set; }
        public int RelationshipAmount { get; set; }
    }

    /// <summary>
    /// 任务数据
    /// </summary>
    [Serializable]
    public class QuestData
    {
        public string QuestId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public QuestType Type { get; set; }
        public QuestStatus Status { get; set; } = QuestStatus.NotStarted;
        public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
        public List<QuestReward> Rewards { get; set; } = new List<QuestReward>();
        public List<QuestReward> FailureConsequences { get; set; } = new List<QuestReward>();
        public List<string> RelatedNPCs { get; set; } = new List<string>();
        public string RelatedLocation { get; set; }

        /// <summary>是否所有目标完成</summary>
        public bool AllObjectivesComplete
        {
            get
            {
                foreach (var obj in Objectives)
                {
                    if (!obj.IsComplete) return false;
                }
                return true;
            }
        }
    }
}
