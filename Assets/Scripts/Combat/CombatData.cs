// ============================================================
// Last Archive - 战斗数据结构
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 战斗单位
    /// </summary>
    [Serializable]
    public class CombatUnit
    {
        public string Id;
        public string Name;
        public int Hp;
        public int MaxHp;
        public int Attack;
        public int Defense;
        public int Speed;
        public bool IsPlayerSide;
        public bool IsDefending;
        public bool IsEscaped;
        public string Role;
        public bool SkillUsed;
        public int BuffDefense;
        public bool IsStunned;
        public int BleedTurns;
        public bool IsDead => Hp <= 0;
        public bool IsAlive => Hp > 0 && !IsEscaped;
    }

    /// <summary>
    /// 敌人模板数据
    /// </summary>
    [Serializable]
    public class EnemyData
    {
        public string Id;
        public string Name;
        public int Hp;
        public int MaxHp;
        public int Attack;
        public int Defense;
        public int Speed;
        public List<ResourceAmount> Loot = new List<ResourceAmount>();
        public float MemoryShardDropChance;
    }

    /// <summary>
    /// 战斗结果
    /// </summary>
    [Serializable]
    public class CombatResult
    {
        public bool Victory;
        public bool Escaped;
        public List<ResourceAmount> Rewards = new List<ResourceAmount>();
        public List<string> InjuredNPCs = new List<string>();
        public string CombatLog = "";
    }
}
