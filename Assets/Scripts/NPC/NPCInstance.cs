// ============================================================
// Last Archive - NPC数据结构
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// NPC关系记录
    /// </summary>
    [Serializable]
    public class NPCRelationship
    {
        public string TargetId { get; set; }
        public int Value { get; set; }
    }

    [Serializable]
    public class NPCInstance
    {
        // ===== 基础信息 =====
        public string Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public NPCRole Role { get; set; }
        public string Description { get; set; }
        public string PortraitId { get; set; }

        // ===== 属性 =====
        public int Health { get; set; }
        public int Morale { get; set; }
        public int Hunger { get; set; }
        public int Fatigue { get; set; }
        public int Loyalty { get; set; }

        // ===== 能力 =====
        public int Medical { get; set; }
        public int Engineering { get; set; }
        public int Scavenging { get; set; }
        public int Combat { get; set; }
        public int Social { get; set; }

        // ===== 性格 =====
        public List<NPCTrait> Traits { get; set; } = new List<NPCTrait>();

        // ===== 状态 =====
        public NPCStatus Status { get; set; } = NPCStatus.Idle;
        public WorkType CurrentWork { get; set; } = WorkType.None;

        // ===== 记忆 =====
        public NPCMemory Memory { get; set; }

        // ===== 关系 =====
        public List<NPCRelationship> Relationships { get; set; } = new List<NPCRelationship>();

        public NPCInstance()
        {
            Memory = new NPCMemory();
        }

        /// <summary>获取与目标的关系值</summary>
        public int GetRelationship(string targetId)
        {
            foreach (var r in Relationships)
            {
                if (r.TargetId == targetId) return r.Value;
            }
            return 0;
        }

        /// <summary>修改与目标的关系值</summary>
        public void ChangeRelationship(string targetId, int delta)
        {
            for (int i = 0; i < Relationships.Count; i++)
            {
                if (Relationships[i].TargetId == targetId)
                {
                    int newVal = Relationships[i].Value + delta;
                    newVal = Math.Max(GameConfig.MinRelationship, Math.Min(GameConfig.MaxRelationship, newVal));
                    Relationships[i] = new NPCRelationship { TargetId = targetId, Value = newVal };
                    return;
                }
            }
            // 新关系
            int clamped = Math.Max(GameConfig.MinRelationship, Math.Min(GameConfig.MaxRelationship, delta));
            Relationships.Add(new NPCRelationship { TargetId = targetId, Value = clamped });
        }

        /// <summary>是否存活</summary>
        public bool IsAlive => Status != NPCStatus.Dead;

        /// <summary>是否可工作</summary>
        public bool CanWork => IsAlive && Status != NPCStatus.Injured && Status != NPCStatus.Missing;

        /// <summary>是否可探索</summary>
        public bool CanExplore => IsAlive && Status != NPCStatus.Injured && Status != NPCStatus.Missing && Status != NPCStatus.Working;
    }
}
