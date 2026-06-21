// ============================================================
// Last Archive - NPC系统
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// NPC系统 - 所有NPC状态修改必须经过此系统
    /// </summary>
    public class NPCSystem
    {
        private readonly Dictionary<string, NPCInstance> _npcs = new Dictionary<string, NPCInstance>();

        public NPCSystem() { }

        /// <summary>添加NPC</summary>
        public void AddNPC(NPCInstance npc)
        {
            npc.Memory = npc.Memory ?? new NPCMemory { NPCId = npc.Id };
            _npcs[npc.Id] = npc;
        }

        /// <summary>获取所有NPC</summary>
        public List<NPCInstance> GetAllNPCs()
        {
            return new List<NPCInstance>(_npcs.Values);
        }

        /// <summary>获取NPC</summary>
        public NPCInstance GetNPC(string id)
        {
            return _npcs.TryGetValue(id, out var npc) ? npc : null;
        }

        /// <summary>获取存活NPC数量</summary>
        public int GetAliveCount()
        {
            int count = 0;
            foreach (var npc in _npcs.Values)
            {
                if (npc.IsAlive) count++;
            }
            return count;
        }

        /// <summary>分配工作</summary>
        public bool AssignWork(string npcId, WorkType workType)
        {
            var npc = GetNPC(npcId);
            if (npc == null || !npc.CanWork) return false;

            var oldStatus = npc.Status;
            npc.CurrentWork = workType;
            if (workType == WorkType.None)
            {
                npc.Status = NPCStatus.Idle;
            }
            else
            {
                npc.Status = NPCStatus.Working;
            }
            EventBus.Publish(new OnNPCStatusChanged { NPCId = npcId, OldStatus = oldStatus, NewStatus = npc.Status });
            return true;
        }

        /// <summary>添加记忆</summary>
        public void AddMemory(string npcId, MemoryEntry entry)
        {
            var npc = GetNPC(npcId);
            if (npc == null) return;
            npc.Memory.Add(entry);
            EventBus.Publish(new OnNPCMemoryAdded { NPCId = npcId, Memory = entry });
        }

        /// <summary>修改关系</summary>
        public void UpdateRelationship(string npcId, string targetId, int amount)
        {
            var npc = GetNPC(npcId);
            if (npc == null) return;
            npc.ChangeRelationship(targetId, amount);
        }

        /// <summary>设置NPC状态</summary>
        public void SetNPCStatus(string npcId, NPCStatus status)
        {
            var npc = GetNPC(npcId);
            if (npc == null) return;
            var old = npc.Status;
            npc.Status = status;
            if (status != NPCStatus.Working)
            {
                npc.CurrentWork = WorkType.None;
            }
            EventBus.Publish(new OnNPCStatusChanged { NPCId = npcId, OldStatus = old, NewStatus = status });
        }

        /// <summary>NPC受伤</summary>
        public void InjureNPC(string npcId, int damage)
        {
            var npc = GetNPC(npcId);
            if (npc == null) return;
            npc.Health = Math.Max(0, npc.Health - damage);
            if (npc.Health <= 0)
            {
                SetNPCStatus(npcId, NPCStatus.Dead);
            }
            else if (npc.Health < 30)
            {
                SetNPCStatus(npcId, NPCStatus.Injured);
            }
        }

        /// <summary>治疗NPC</summary>
        public void HealNPC(string npcId, int amount)
        {
            var npc = GetNPC(npcId);
            if (npc == null) return;
            npc.Health = Math.Min(GameConfig.MaxNPCHealth, npc.Health + amount);
            if (npc.Health >= 30 && npc.Status == NPCStatus.Injured)
            {
                SetNPCStatus(npcId, NPCStatus.Idle);
            }
        }

        /// <summary>应用每日NPC状态变化</summary>
        public void ApplyDailyChanges()
        {
            foreach (var npc in _npcs.Values)
            {
                if (!npc.IsAlive) continue;

                // 饥饿增加
                npc.Hunger = Math.Min(GameConfig.MaxNPCHunger, npc.Hunger + GameConfig.DailyHungerIncrease);

                // 疲劳增加（工作中增加更多）
                int fatigueAdd = npc.Status == NPCStatus.Working
                    ? GameConfig.DailyFatigueIncrease * 2
                    : GameConfig.DailyFatigueIncrease;
                npc.Fatigue = Math.Min(GameConfig.MaxNPCFatigue, npc.Fatigue + fatigueAdd);

                // 饥饿惩罚士气
                if (npc.Hunger > 70)
                {
                    npc.Morale = Math.Max(0, npc.Morale - GameConfig.HungerMoralePenalty);
                }

                // 低士气惩罚忠诚
                if (npc.Morale < 30)
                {
                    npc.Loyalty = Math.Max(0, npc.Loyalty - GameConfig.LowMoraleLoyaltyPenalty);
                }

                // 受伤NPC恢复（如果有药品）
                if (npc.Status == NPCStatus.Injured && npc.Health > 0)
                {
                    npc.Health = Math.Min(GameConfig.MaxNPCHealth, npc.Health + 2);
                    if (npc.Health >= 30)
                    {
                        npc.Status = NPCStatus.Idle;
                        npc.CurrentWork = WorkType.None;
                    }
                }

                // 休息恢复疲劳和士气
                if (npc.Status == NPCStatus.Idle)
                {
                    npc.Fatigue = Math.Max(0, npc.Fatigue - 10);
                    npc.Morale = Math.Min(GameConfig.MaxNPCMorale, npc.Morale + 2);
                }
            }
        }

        /// <summary>获取可探索的NPC列表</summary>
        public List<NPCInstance> GetExplorableNPCs()
        {
            var result = new List<NPCInstance>();
            var psychology = ServiceLocator.Get<PsychologySystem>();
            foreach (var npc in _npcs.Values)
            {
                if (npc.CanExplore)
                {
                    if (psychology == null || psychology.CanExplore(npc.Id))
                    {
                        result.Add(npc);
                    }
                }
            }
            return result;
        }

        /// <summary>获取全局关系网矩阵</summary>
        public Dictionary<string, Dictionary<string, int>> GetRelationshipMatrix()
        {
            var matrix = new Dictionary<string, Dictionary<string, int>>();
            foreach (var npc in _npcs.Values)
            {
                if (!npc.IsAlive) continue;
                var dict = new Dictionary<string, int>();
                foreach (var rel in npc.Relationships)
                {
                    dict[rel.TargetId] = rel.Value;
                }
                matrix[npc.Id] = dict;
            }
            return matrix;
        }

        /// <summary>根据生存状态与工作分配，更新每日NPC关系</summary>
        public void UpdateNPCRelationships(bool hasFoodCrisis, bool hasWaterCrisis)
        {
            var aliveNPCs = new List<NPCInstance>();
            foreach (var npc in _npcs.Values)
            {
                if (npc.IsAlive) aliveNPCs.Add(npc);
            }

            if (aliveNPCs.Count < 2) return;

            var rng = new Random();

            for (int i = 0; i < aliveNPCs.Count; i++)
            {
                for (int j = i + 1; j < aliveNPCs.Count; j++)
                {
                    var npc1 = aliveNPCs[i];
                    var npc2 = aliveNPCs[j];

                    int delta = 0;
                    if (hasFoodCrisis || hasWaterCrisis)
                    {
                        // 存活危机压力：关系更容易变差
                        if (rng.Next(100) < 40)
                        {
                            delta = rng.Next(-3, 0); // 扣减 1 至 3 点
                        }
                    }
                    else
                    {
                        // 稳定状态下：关系有几率友好发展
                        if (rng.Next(100) < 20)
                        {
                            delta = 1;
                        }
                    }

                    // 协同效应：如果是同事，协同合作更容易变好
                    if (npc1.Status == NPCStatus.Working && npc2.Status == NPCStatus.Working && npc1.CurrentWork == npc2.CurrentWork && npc1.CurrentWork != WorkType.None)
                    {
                        if (rng.Next(100) < 30)
                        {
                            delta += rng.Next(1, 3);
                        }
                    }

                    if (delta != 0)
                    {
                        npc1.ChangeRelationship(npc2.Id, delta);
                        npc2.ChangeRelationship(npc1.Id, delta);
                    }
                }
            }
        }
    }
}
