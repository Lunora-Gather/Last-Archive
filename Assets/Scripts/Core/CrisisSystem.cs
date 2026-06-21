// ============================================================
// Last Archive - 小镇危机系统
// 危机可以连锁触发：饥荒→疾病→叛乱
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>危机类型</summary>
    public enum CrisisType
    {
        Famine,       // 饥荒
        Plague,       // 瘟疫
        Riot,         // 叛乱
        PowerOutage,  // 停电
        MemoryStorm,  // 记忆风暴
        RaiderRaid    // 掠夺者袭击
    }

    /// <summary>危机状态</summary>
    public enum CrisisSeverity
    {
        Warning,   // 警告
        Active,    // 活跃
        Critical   // 严重
    }

    /// <summary>危机实例</summary>
    public class CrisisInstance
    {
        public CrisisType Type { get; set; }
        public CrisisSeverity Severity { get; set; }
        public int StartDay { get; set; }
        public int Duration { get; set; }
        public string Description { get; set; }
        public bool Resolved { get; set; }
    }

    /// <summary>危机事件</summary>
    public struct OnCrisisTriggered { public CrisisType Type; public CrisisSeverity Severity; public string Description; }
    public struct OnCrisisResolved { public CrisisType Type; public int Duration; }

    /// <summary>
    /// 危机系统 - 管理连锁危机事件
    /// </summary>
    public class CrisisSystem
    {
        private List<CrisisInstance> _activeCrises = new List<CrisisInstance>();
        private Random _rng = new Random();

        /// <summary>当前活跃危机</summary>
        public List<CrisisInstance> ActiveCrises => new List<CrisisInstance>(_activeCrises);

        /// <summary>是否有活跃危机</summary>
        public bool HasAnyCrisis => _activeCrises.Count > 0;

        /// <summary>检查并触发危机</summary>
        public string CheckAndTrigger(GameManager game)
        {
            var sb = new System.Text.StringBuilder();
            int day = game.Time.CurrentDay;

            // 饥荒危机：食物<人均3
            int foodPerCap = game.NPCs.GetAliveCount() > 0
                ? game.Resources.GetResourceAmount(ResourceType.Food) / game.NPCs.GetAliveCount()
                : 999;

            if (foodPerCap < 3 && !HasCrisisOfType(CrisisType.Famine))
            {
                var desc = TriggerCrisis(CrisisType.Famine, day, foodPerCap < 1 ? CrisisSeverity.Critical : CrisisSeverity.Active,
                    foodPerCap < 1 ? "严重饥荒！食物储备告急！" : "食物短缺，居民们开始挨饿。");
                sb.AppendLine(desc);

                // 饥荒可能连锁触发瘟疫（30%概率）
                if (_rng.Next(100) < 30 && !HasCrisisOfType(CrisisType.Plague))
                {
                    var plague = TriggerCrisis(CrisisType.Plague, day, CrisisSeverity.Warning,
                        "卫生条件恶化，瘟疫的阴影开始蔓延……");
                    sb.AppendLine(plague);
                }
            }

            // 停电危机：电力=0
            if (game.Resources.GetResourceAmount(ResourceType.Power) == 0 && !HasCrisisOfType(CrisisType.PowerOutage))
            {
                var desc = TriggerCrisis(CrisisType.PowerOutage, day, CrisisSeverity.Active,
                    "电力耗尽！档案馆陷入黑暗，设备停转。");
                sb.AppendLine(desc);
            }

            // 记忆风暴：记忆碎片>10时，20%概率触发
            if (game.Resources.GetResourceAmount(ResourceType.MemoryShards) > 10 && _rng.Next(100) < 20
                && !HasCrisisOfType(CrisisType.MemoryStorm))
            {
                var desc = TriggerCrisis(CrisisType.MemoryStorm, day, CrisisSeverity.Warning,
                    "大量记忆碎片聚集，引发了记忆风暴！NPC们开始做奇怪的梦。");
                sb.AppendLine(desc);
            }

            // 叛乱：平均忠诚<20时触发
            int totalLoyalty = 0, aliveCount = 0;
            foreach (var npc in game.NPCs.GetAllNPCs())
            {
                if (npc.IsAlive) { totalLoyalty += npc.Loyalty; aliveCount++; }
            }
            if (aliveCount > 0 && totalLoyalty / aliveCount < 20 && !HasCrisisOfType(CrisisType.Riot))
            {
                var desc = TriggerCrisis(CrisisType.Riot, day, CrisisSeverity.Critical,
                    "居民们的不满达到顶点，叛乱爆发了！");
                sb.AppendLine(desc);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>应用危机效果</summary>
        public void ApplyEffects(GameManager game)
        {
            foreach (var crisis in _activeCrises)
            {
                if (crisis.Resolved) continue;

                switch (crisis.Type)
                {
                    case CrisisType.Famine:
                        // 饥荒：每日士气-3
                        foreach (var npc in game.NPCs.GetAllNPCs())
                        {
                            if (npc.IsAlive) npc.Morale = Math.Max(0, npc.Morale - 3);
                        }
                        break;

                    case CrisisType.Plague:
                        // 瘟疫：随机让1个NPC受伤
                        var targets = new List<LastArchive.NPCInstance>();
                        foreach (var n in game.NPCs.GetAllNPCs())
                        {
                            if (n.IsAlive && n.Status != NPCStatus.Injured) targets.Add(n);
                        }
                        if (targets.Count > 0 && _rng.Next(100) < 40)
                        {
                            var victim = targets[_rng.Next(targets.Count)];
                            game.NPCs.InjureNPC(victim.Id, 15);
                            Console.WriteLine($"  [瘟疫] {victim.Name} 染病了！");
                        }
                        break;

                    case CrisisType.PowerOutage:
                        // 停电：记忆碎片每回合-1
                        if (game.Resources.GetResourceAmount(ResourceType.MemoryShards) > 0)
                        {
                            game.Resources.ConsumeResource(ResourceType.MemoryShards, 1);
                        }
                        break;

                    case CrisisType.Riot:
                        // 叛乱：忠诚持续下降
                        foreach (var npc in game.NPCs.GetAllNPCs())
                        {
                            if (npc.IsAlive) npc.Loyalty = Math.Max(0, npc.Loyalty - 2);
                        }
                        break;

                    case CrisisType.MemoryStorm:
                        // 记忆风暴：随机获得或失去碎片
                        if (_rng.Next(100) < 50)
                        {
                            game.Resources.AddResource(ResourceType.MemoryShards, 2);
                        }
                        else if (game.Resources.GetResourceAmount(ResourceType.MemoryShards) >= 2)
                        {
                            game.Resources.ConsumeResource(ResourceType.MemoryShards, 2);
                        }
                        break;

                    case CrisisType.RaiderRaid:
                        // 掠夺者袭击：损失资源
                        int lostParts = Math.Min(5, game.Resources.GetResourceAmount(ResourceType.Parts));
                        if (lostParts > 0) game.Resources.ConsumeResource(ResourceType.Parts, lostParts);
                        int lostFood = Math.Min(5, game.Resources.GetResourceAmount(ResourceType.Food));
                        if (lostFood > 0) game.Resources.ConsumeResource(ResourceType.Food, lostFood);
                        break;
                }
            }
        }

        /// <summary>尝试解决危机</summary>
        public string TryResolve(GameManager game)
        {
            var sb = new System.Text.StringBuilder();
            var resolved = new List<CrisisInstance>();

            foreach (var crisis in _activeCrises)
            {
                if (crisis.Resolved) continue;

                bool canResolve = false;
                string how = "";

                switch (crisis.Type)
                {
                    case CrisisType.Famine:
                        if (game.Resources.GetResourceAmount(ResourceType.Food) >= game.NPCs.GetAliveCount() * 3)
                        {
                            canResolve = true;
                            how = "食物储备恢复，饥荒结束。";
                        }
                        break;

                    case CrisisType.Plague:
                        if (game.Resources.GetResourceAmount(ResourceType.Medicine) >= 3)
                        {
                            game.Resources.ConsumeResource(ResourceType.Medicine, 3);
                            canResolve = true;
                            how = "消耗3药品，瘟疫得到控制。";
                        }
                        break;

                    case CrisisType.PowerOutage:
                        if (game.Resources.GetResourceAmount(ResourceType.Power) > 0)
                        {
                            canResolve = true;
                            how = "电力恢复，停电结束。";
                        }
                        break;

                    case CrisisType.Riot:
                        int avgLoyalty = 0, cnt = 0;
                        foreach (var npc in game.NPCs.GetAllNPCs())
                        {
                            if (npc.IsAlive) { avgLoyalty += npc.Loyalty; cnt++; }
                        }
                        if (cnt > 0 && avgLoyalty / cnt >= 40)
                        {
                            canResolve = true;
                            how = "民心回稳，叛乱平息。";
                        }
                        break;

                    case CrisisType.MemoryStorm:
                        if (game.Time.CurrentDay > crisis.StartDay + 2)
                        {
                            canResolve = true;
                            how = "记忆风暴自然消散。";
                        }
                        break;

                    case CrisisType.RaiderRaid:
                        if (game.Time.CurrentDay > crisis.StartDay)
                        {
                            canResolve = true;
                            how = "掠夺者撤退了。";
                        }
                        break;
                }

                if (canResolve)
                {
                    crisis.Resolved = true;
                    crisis.Duration = game.Time.CurrentDay - crisis.StartDay;
                    resolved.Add(crisis);
                    sb.AppendLine($"✅ [{crisis.Type}] {how}");
                    EventBus.Publish(new OnCrisisResolved { Type = crisis.Type, Duration = crisis.Duration });
                }
            }

            foreach (var c in resolved) _activeCrises.Remove(c);
            return sb.ToString().TrimEnd();
        }

        private string TriggerCrisis(CrisisType type, int day, CrisisSeverity severity, string description)
        {
            var crisis = new CrisisInstance
            {
                Type = type,
                Severity = severity,
                StartDay = day,
                Description = description,
                Resolved = false
            };
            _activeCrises.Add(crisis);
            EventBus.Publish(new OnCrisisTriggered { Type = type, Severity = severity, Description = description });
            return $"⚠️ [{type}] {description}";
        }

        private bool HasCrisisOfType(CrisisType type)
        {
            foreach (var c in _activeCrises)
            {
                if (c.Type == type && !c.Resolved) return true;
            }
            return false;
        }
    }
}
