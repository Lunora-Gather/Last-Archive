// ============================================================
// Last Archive - 战斗系统
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace LastArchive
{
    /// <summary>
    /// 战斗系统 - 简单回合制
    /// </summary>
    public class CombatSystem
    {
        private ResourceSystem _resourceSystem;
        private NPCSystem _npcSystem;
        private ItemSystem _itemSystem;
        private Dictionary<string, EnemyData> _enemyTemplates = new Dictionary<string, EnemyData>();
        private Random _rng = new Random();

        // 当前战斗状态
        public bool InCombat { get; private set; }
        public List<CombatUnit> PlayerUnits { get; private set; } = new List<CombatUnit>();
        public List<CombatUnit> EnemyUnits { get; private set; } = new List<CombatUnit>();
        public List<CombatUnit> ActionTimeline { get; private set; } = new List<CombatUnit>();
        public int CurrentTimelineIndex { get; private set; } = 0;
        public StringBuilder CombatLog { get; private set; } = new StringBuilder();
        public bool IsPlayerTurn { get; private set; }

        public CombatUnit ActiveUnit => (ActionTimeline != null && CurrentTimelineIndex < ActionTimeline.Count) ? ActionTimeline[CurrentTimelineIndex] : null;

        public CombatSystem(ResourceSystem resourceSystem, NPCSystem npcSystem, ItemSystem itemSystem = null)
        {
            _resourceSystem = resourceSystem;
            _npcSystem = npcSystem;
            _itemSystem = itemSystem;
        }

        /// <summary>注册敌人模板</summary>
        public void RegisterEnemy(EnemyData enemy)
        {
            _enemyTemplates[enemy.Id] = enemy;
        }

        /// <summary>获取敌人模板</summary>
        public EnemyData GetEnemyTemplate(string id)
        {
            return _enemyTemplates.TryGetValue(id, out var e) ? e : null;
        }

        /// <summary>开始战斗</summary>
        public bool StartCombat(List<string> playerNPCIds, List<string> enemyIds)
        {
            PlayerUnits.Clear();
            EnemyUnits.Clear();
            CombatLog.Clear();
            ActionTimeline.Clear();
            CurrentTimelineIndex = 0;

            // 创建玩家单位
            var psychology = ServiceLocator.Get<PsychologySystem>();
            foreach (var npcId in playerNPCIds)
            {
                var npc = _npcSystem.GetNPC(npcId);
                if (npc == null || !npc.IsAlive) continue;

                int psychologyMod = psychology?.GetCombatModifier(npcId) ?? 0;

                PlayerUnits.Add(new CombatUnit
                {
                    Id = npcId,
                    Name = npc.Name,
                    Hp = npc.Health,
                    MaxHp = GameConfig.MaxNPCHealth,
                    Attack = Math.Max(1, npc.Combat * 2 + 3 + (_itemSystem?.GetNPCAttackBonus(npcId) ?? 0) + psychologyMod),
                    Defense = Math.Max(0, npc.Combat + 1 + (_itemSystem?.GetNPCDefenseBonus(npcId) ?? 0) + psychologyMod),
                    Speed = 3 + npc.Scavenging / 3,
                    IsPlayerSide = true,
                    Role = npc.Role.ToString(),
                    SkillUsed = false,
                    BuffDefense = 0
                });
            }

            if (PlayerUnits.Count == 0) return false;

            // 创建敌人单位
            foreach (var enemyId in enemyIds)
            {
                var template = GetEnemyTemplate(enemyId);
                if (template == null) continue;

                EnemyUnits.Add(new CombatUnit
                {
                    Id = template.Id,
                    Name = template.Name,
                    Hp = template.Hp,
                    MaxHp = template.MaxHp,
                    Attack = template.Attack,
                    Defense = template.Defense,
                    Speed = template.Speed,
                    IsPlayerSide = false,
                    Role = "Enemy",
                    SkillUsed = false,
                    BuffDefense = 0
                });
            }

            if (EnemyUnits.Count == 0) return false;

            // 建立时间轴
            ActionTimeline.AddRange(PlayerUnits);
            ActionTimeline.AddRange(EnemyUnits);
            // 按速度降序排序，速度相同则玩家优先
            ActionTimeline.Sort((a, b) =>
            {
                if (b.Speed != a.Speed) return b.Speed.CompareTo(a.Speed);
                return b.IsPlayerSide.CompareTo(a.IsPlayerSide);
            });

            InCombat = true;
            CurrentTimelineIndex = 0;
            IsPlayerTurn = ActiveUnit.IsPlayerSide;

            CombatLog.AppendLine("=== 战斗开始 ===");
            foreach (var e in EnemyUnits)
            {
                CombatLog.AppendLine($"遭遇了 {e.Name}！(HP:{e.Hp})");
            }

            EventBus.Publish(new OnCombatStarted { MapId = "", RoomId = "" });

            // 如果首先行动的是敌人，自动处理敌人回合
            if (!IsPlayerTurn)
            {
                var tempResult = new CombatResult();
                AutoRunEnemyTurns(tempResult);
            }

            return true;
        }

        /// <summary>执行玩家行动</summary>
        public CombatResult ExecutePlayerAction(CombatAction action, int targetIndex = 0, string itemId = "", string skillName = "")
        {
            if (!InCombat || !IsPlayerTurn) return null;

            var result = new CombatResult();
            var attacker = ActiveUnit;
            if (attacker == null || attacker.IsDead)
            {
                AdvanceTimeline();
                if (InCombat && !IsPlayerTurn) AutoRunEnemyTurns(result);
                result.CombatLog = CombatLog.ToString();
                return InCombat ? null : result;
            }

            // 流血结算
            if (attacker.BleedTurns > 0)
            {
                attacker.BleedTurns--;
                attacker.Hp = Math.Max(0, attacker.Hp - 5);
                CombatLog.AppendLine($"{attacker.Name} 因流血受到 5 伤害！(HP: {attacker.Hp})");
                if (attacker.Hp <= 0)
                {
                    CombatLog.AppendLine($"{attacker.Name} 因流血倒下了！");
                    AdvanceTimeline();
                    if (InCombat && !IsPlayerTurn) AutoRunEnemyTurns(result);
                    result.CombatLog = CombatLog.ToString();
                    return InCombat ? null : result;
                }
            }

            // 眩晕结算
            if (attacker.IsStunned)
            {
                attacker.IsStunned = false;
                CombatLog.AppendLine($"{attacker.Name} 处于眩晕状态，跳过了本回合行动！");
                AdvanceTimeline();
                if (InCombat && !IsPlayerTurn) AutoRunEnemyTurns(result);
                result.CombatLog = CombatLog.ToString();
                return InCombat ? null : result;
            }

            switch (action)
            {
                case CombatAction.Attack:
                    {
                        var enemies = GetAliveUnits(EnemyUnits);
                        if (enemies.Count == 0) return EndCombat(true, false);
                        var target = enemies[Math.Min(targetIndex, enemies.Count - 1)];
                        int damage = Math.Max(1, attacker.Attack - target.Defense / 2);
                        target.Hp = Math.Max(0, target.Hp - damage);
                        CombatLog.AppendLine($"{attacker.Name} 攻击 {target.Name}，造成 {damage} 伤害！");
                        if (target.IsDead)
                        {
                            CombatLog.AppendLine($"{target.Name} 被击败了！");
                        }
                    }
                    break;

                case CombatAction.Defend:
                    attacker.IsDefending = true;
                    CombatLog.AppendLine($"{attacker.Name} 进入防御姿态！");
                    break;

                case CombatAction.UseMedicine:
                    if (_resourceSystem.ConsumeResource(ResourceType.Medicine, GameConfig.MedicineCostPerUse))
                    {
                        attacker.Hp = Math.Min(attacker.MaxHp, attacker.Hp + GameConfig.MedicineHealAmount);
                        CombatLog.AppendLine($"{attacker.Name} 使用了药品，恢复 {GameConfig.MedicineHealAmount} HP！");
                    }
                    else
                    {
                        CombatLog.AppendLine("没有药品可用！");
                    }
                    break;

                case CombatAction.Escape:
                    if (_rng.NextDouble() < GameConstants.EscapeSuccessRate)
                    {
                        CombatLog.AppendLine($"{attacker.Name} 成功逃脱！");
                        return EndCombat(false, true);
                    }
                    else
                    {
                        CombatLog.AppendLine("逃跑失败！");
                    }
                    break;

                case CombatAction.UseSkill:
                    ExecuteSkill(attacker, targetIndex);
                    break;

                case CombatAction.UseItem:
                    if (!string.IsNullOrEmpty(itemId) && _itemSystem != null)
                    {
                        var inv = _itemSystem.GetInventory();
                        var item = inv.Find(i => i.Id == itemId);
                        if (item != null && item.IsConsumable)
                        {
                            if (_itemSystem.UseConsumable(itemId, attacker.Id))
                            {
                                CombatLog.AppendLine($"{attacker.Name} 使用了 [{item.Name}]！");
                                // 刷新生命值
                                var npc = _npcSystem.GetNPC(attacker.Id);
                                if (npc != null) attacker.Hp = npc.Health;
                            }
                            else
                            {
                                CombatLog.AppendLine($"使用 [{item.Name}] 失败！");
                            }
                        }
                        else
                        {
                            CombatLog.AppendLine("物品不可用或已消耗！");
                        }
                    }
                    else
                    {
                        CombatLog.AppendLine("未选择有效物品！");
                    }
                    break;
            }

            // 检查敌人是否全灭
            if (GetAliveUnits(EnemyUnits).Count == 0)
            {
                return EndCombat(true, false);
            }

            // 推进时间轴
            AdvanceTimeline();

            // 自动运行敌人回合
            if (InCombat && !IsPlayerTurn)
            {
                AutoRunEnemyTurns(result);
            }

            result.CombatLog = CombatLog.ToString();
            return InCombat ? null : result;
        }

        /// <summary>自动运行连续的敌人回合</summary>
        private void AutoRunEnemyTurns(CombatResult result)
        {
            while (InCombat && !IsPlayerTurn)
            {
                var attacker = ActiveUnit;
                if (attacker == null || attacker.IsDead)
                {
                    AdvanceTimeline();
                    continue;
                }

                // 流血结算
                if (attacker.BleedTurns > 0)
                {
                    attacker.BleedTurns--;
                    attacker.Hp = Math.Max(0, attacker.Hp - 5);
                    CombatLog.AppendLine($"{attacker.Name} 因流血受到 5 伤害！(HP: {attacker.Hp})");
                    if (attacker.Hp <= 0)
                    {
                        CombatLog.AppendLine($"{attacker.Name} 因流血被击败了！");
                        if (GetAliveUnits(EnemyUnits).Count == 0)
                        {
                            var endRes = EndCombat(true, false);
                            result.Victory = endRes.Victory;
                            result.Escaped = endRes.Escaped;
                            result.Rewards = endRes.Rewards;
                            result.InjuredNPCs.AddRange(endRes.InjuredNPCs);
                            result.CombatLog = endRes.CombatLog;
                            return;
                        }
                        AdvanceTimeline();
                        continue;
                    }
                }

                // 眩晕结算
                if (attacker.IsStunned)
                {
                    attacker.IsStunned = false;
                    CombatLog.AppendLine($"{attacker.Name} 处于眩晕状态，跳过了本回合行动！");
                    AdvanceTimeline();
                    continue;
                }

                var alivePlayers = GetAliveUnits(PlayerUnits);
                if (alivePlayers.Count == 0)
                {
                    var endRes = EndCombat(false, false);
                    result.Victory = endRes.Victory;
                    result.Escaped = endRes.Escaped;
                    result.Rewards = endRes.Rewards;
                    result.InjuredNPCs.AddRange(endRes.InjuredNPCs);
                    result.CombatLog = endRes.CombatLog;
                    return;
                }

                // 随机攻击一个玩家队员
                var target = alivePlayers[_rng.Next(alivePlayers.Count)];
                int totalDefense = target.Defense + target.BuffDefense;
                int damage = Math.Max(1, attacker.Attack - totalDefense / 2);
                if (target.IsDefending)
                {
                    damage = Math.Max(1, (int)(damage * GameConstants.DefenseDamageReduction));
                }
                target.Hp = Math.Max(0, target.Hp - damage);
                CombatLog.AppendLine($"{attacker.Name} 攻击 {target.Name}，造成 {damage} 伤害！");

                if (target.IsDead)
                {
                    CombatLog.AppendLine($"{target.Name} 倒下了！");
                    result.InjuredNPCs.Add(target.Id);
                }

                // 检查我方是否全灭
                if (GetAliveUnits(PlayerUnits).Count == 0)
                {
                    var endRes = EndCombat(false, false);
                    result.Victory = endRes.Victory;
                    result.Escaped = endRes.Escaped;
                    result.Rewards = endRes.Rewards;
                    result.InjuredNPCs.AddRange(endRes.InjuredNPCs);
                    result.CombatLog = endRes.CombatLog;
                    return;
                }

                AdvanceTimeline();
            }
        }

        /// <summary>执行职业特色技能</summary>
        private void ExecuteSkill(CombatUnit attacker, int targetIndex)
        {
            if (attacker.SkillUsed)
            {
                CombatLog.AppendLine($"{attacker.Name} 的技能已经使用过了！");
                return;
            }

            attacker.SkillUsed = true;

            switch (attacker.Role)
            {
                case "Doctor":
                    {
                        CombatLog.AppendLine($"{attacker.Name} 释放技能【群体治疗】！");
                        var alivePlayers = GetAliveUnits(PlayerUnits);
                        foreach (var p in alivePlayers)
                        {
                            p.Hp = Math.Min(p.MaxHp, p.Hp + 15);
                            CombatLog.AppendLine($"  {p.Name} 恢复了 15 HP！");
                        }
                    }
                    break;

                case "Child":
                    {
                        var enemies = GetAliveUnits(EnemyUnits);
                        if (enemies.Count > 0)
                        {
                            var target = enemies[Math.Min(targetIndex, enemies.Count - 1)];
                            CombatLog.AppendLine($"{attacker.Name} 释放技能【破甲射击】！");
                            int damage = attacker.Attack + 5;
                            target.Hp = Math.Max(0, target.Hp - damage);
                            CombatLog.AppendLine($"  对 {target.Name} 造成 {damage} 伤害！(无视防御)");
                            if (_rng.Next(100) < 40)
                            {
                                target.IsStunned = true;
                                CombatLog.AppendLine($"  成功击晕了 {target.Name}！");
                            }
                            if (target.IsDead)
                            {
                                CombatLog.AppendLine($"  {target.Name} 被击败了！");
                            }
                        }
                    }
                    break;

                case "Scout":
                    {
                        CombatLog.AppendLine($"{attacker.Name} 释放技能【搭建护盾挡板】！");
                        var alivePlayers = GetAliveUnits(PlayerUnits);
                        foreach (var p in alivePlayers)
                        {
                            p.BuffDefense += 5;
                            CombatLog.AppendLine($"  {p.Name} 获得护盾防壁 (+5 防御)！");
                        }
                    }
                    break;

                case "Engineer":
                    {
                        var enemies = GetAliveUnits(EnemyUnits);
                        if (enemies.Count > 0)
                        {
                            var target = enemies[Math.Min(targetIndex, enemies.Count - 1)];
                            CombatLog.AppendLine($"{attacker.Name} 释放技能【机械重载】！");
                            int damage = Math.Max(1, (int)(attacker.Attack * 1.5) - target.Defense / 2);
                            target.Hp = Math.Max(0, target.Hp - damage);
                            CombatLog.AppendLine($"  对 {target.Name} 造成 {damage} 机械重击伤害！");
                            if (target.IsDead)
                            {
                                CombatLog.AppendLine($"  {target.Name} 被击败了！");
                            }
                        }
                    }
                    break;

                case "Stranger":
                    {
                        var enemies = GetAliveUnits(EnemyUnits);
                        if (enemies.Count > 0)
                        {
                            var target = enemies[Math.Min(targetIndex, enemies.Count - 1)];
                            CombatLog.AppendLine($"{attacker.Name} 释放技能【洞察破绽】！");
                            int damage = Math.Max(1, attacker.Attack - target.Defense / 2);
                            target.Hp = Math.Max(0, target.Hp - damage);
                            target.Defense = Math.Max(0, target.Defense - 3);
                            target.BleedTurns = 2;
                            CombatLog.AppendLine($"  对 {target.Name} 造成 {damage} 伤害，使其防御降低 3 并使其流血！");
                            if (target.IsDead)
                            {
                                CombatLog.AppendLine($"  {target.Name} 被击败了！");
                            }
                        }
                    }
                    break;

                default:
                    CombatLog.AppendLine($"{attacker.Name} 释放了技能，但没有产生任何战术效果。");
                    break;
            }
        }

        /// <summary>推进时间轴</summary>
        private void AdvanceTimeline()
        {
            if (!InCombat) return;
            if (GetAliveUnits(PlayerUnits).Count == 0 || GetAliveUnits(EnemyUnits).Count == 0)
            {
                return;
            }

            CurrentTimelineIndex++;
            if (CurrentTimelineIndex >= ActionTimeline.Count)
            {
                CurrentTimelineIndex = 0;
                // 新回合开始，减少防御盾Buff
                foreach (var unit in ActionTimeline)
                {
                    unit.IsDefending = false;
                    unit.BuffDefense = 0;
                }
            }

            var nextUnit = ActiveUnit;
            if (nextUnit == null || nextUnit.IsDead)
            {
                AdvanceTimeline();
                return;
            }

            IsPlayerTurn = nextUnit.IsPlayerSide;
        }

        /// <summary>结束战斗</summary>
        private CombatResult EndCombat(bool victory, bool escaped)
        {
            var result = new CombatResult
            {
                Victory = victory,
                Escaped = escaped
            };

            if (victory)
            {
                CombatLog.AppendLine("=== 战斗胜利！ ===");
                foreach (var enemy in EnemyUnits)
                {
                    var template = GetEnemyTemplate(enemy.Id);
                    if (template != null)
                    {
                        foreach (var loot in template.Loot)
                        {
                            _resourceSystem.AddResource(loot.Type, loot.Amount);
                            result.Rewards.Add(loot);
                        }
                        if (template.MemoryShardDropChance > 0 && _rng.NextDouble() < template.MemoryShardDropChance)
                        {
                            _resourceSystem.AddResource(ResourceType.MemoryShards, 1);
                            result.Rewards.Add(new ResourceAmount(ResourceType.MemoryShards, 1));
                            CombatLog.AppendLine("获得了记忆碎片！");
                        }
                    }
                }
            }
            else if (!escaped)
            {
                CombatLog.AppendLine("=== 战斗失败... ===");
            }

            // 结算并更新所有参战玩家单位的状态和生命值
            foreach (var unit in PlayerUnits)
            {
                var npc = _npcSystem.GetNPC(unit.Id);
                if (npc == null) continue;

                if (unit.IsDead)
                {
                    // 战损：生命值扣尽，状态设为Dead
                    _npcSystem.InjureNPC(unit.Id, npc.Health);
                    if (!result.InjuredNPCs.Contains(unit.Id))
                    {
                        result.InjuredNPCs.Add(unit.Id);
                    }
                }
                else
                {
                    // 存活：同步生命值，并根据当前生命值判定是否受伤
                    npc.Health = unit.Hp;
                    if (unit.Hp < unit.MaxHp && !escaped)
                    {
                        // 非撤退情况下的扣血记为受伤
                        if (!result.InjuredNPCs.Contains(unit.Id))
                        {
                            result.InjuredNPCs.Add(unit.Id);
                        }
                    }
                    if (unit.Hp < 30 && npc.Status != NPCStatus.Injured)
                    {
                        _npcSystem.SetNPCStatus(unit.Id, NPCStatus.Injured);
                    }
                }
            }

            InCombat = false;
            result.CombatLog = CombatLog.ToString();
            EventBus.Publish(new OnCombatEnded { Victory = victory });
            return result;
        }

        /// <summary>获取活着的单位</summary>
        private List<CombatUnit> GetAliveUnits(List<CombatUnit> units)
        {
            var result = new List<CombatUnit>();
            foreach (var u in units)
            {
                if (!u.IsDead && !u.IsEscaped) result.Add(u);
            }
            return result;
        }

        /// <summary>获取战斗状态描述</summary>
        public string GetStatusText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("【我方】");
            var active = ActiveUnit;
            foreach (var u in PlayerUnits)
            {
                string activeMarker = (active != null && active.Id == u.Id && IsPlayerTurn) ? "▶ " : "  ";
                string status = u.IsDead ? "(倒下)" : u.BuffDefense > 0 ? $"(护盾 +{u.BuffDefense})" : "";
                sb.AppendLine($"{activeMarker}{u.Name} HP:{u.Hp}/{u.MaxHp} {status}");
            }
            sb.AppendLine("【敌方】");
            foreach (var u in EnemyUnits)
            {
                string activeMarker = (active != null && active.Id == u.Id && !IsPlayerTurn) ? "▶ " : "  ";
                string status = u.IsDead ? "(击败)" : "";
                sb.AppendLine($"{activeMarker}{u.Name} HP:{u.Hp}/{u.MaxHp} {status}");
            }
            return sb.ToString();
        }
    }
}
