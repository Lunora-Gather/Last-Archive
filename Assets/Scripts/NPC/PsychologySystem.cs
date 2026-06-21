// ============================================================
// Last Archive - NPC心理系统
// 创伤/希望/恐惧/信任/绝望 影响行为和对话
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>心理状态</summary>
    public enum MentalState
    {
        Hopeful,    // 充满希望
        Stable,     // 稳定
        Anxious,    // 焦虑
        Despair,    // 绝望
        Traumatized // 创伤
    }

    /// <summary>心理特征</summary>
    [Flags]
    public enum MentalTrait
    {
        None = 0,
        Trauma = 1,       // 创伤（灾难相关）
        Hope = 2,          // 希望（对未来的期待）
        Fear = 4,          // 恐惧（对危险的害怕）
        Trust = 8,         // 信任（对玩家/同伴的信任）
        Despair = 16,      // 绝望（对生存的放弃）
        Paranoia = 32,     // 偏执（对他人的怀疑）
        Resolve = 64       // 决心（坚定意志）
    }

    /// <summary>心理事件</summary>
    public struct OnMentalStateChanged { public string NPCId; public MentalState OldState; public MentalState NewState; public string Reason; }

    /// <summary>
    /// NPC心理系统 - 管理NPC的心理状态变化
    /// </summary>
    public class PsychologySystem
    {
        private Dictionary<string, MentalTrait> _traits = new Dictionary<string, MentalTrait>();
        private Dictionary<string, MentalState> _states = new Dictionary<string, MentalState>();

        /// <summary>获取NPC心理状态</summary>
        public MentalState GetState(string npcId)
        {
            return _states.TryGetValue(npcId, out var s) ? s : MentalState.Stable;
        }

        /// <summary>获取NPC心理特征</summary>
        public MentalTrait GetTraits(string npcId)
        {
            return _traits.TryGetValue(npcId, out var t) ? t : MentalTrait.None;
        }

        /// <summary>获取状态中文名</summary>
        public string GetStateName(MentalState state)
        {
            switch (state)
            {
                case MentalState.Hopeful: return "希望";
                case MentalState.Stable: return "稳定";
                case MentalState.Anxious: return "焦虑";
                case MentalState.Despair: return "绝望";
                case MentalState.Traumatized: return "创伤";
                default: return "未知";
            }
        }

        /// <summary>初始化NPC心理</summary>
        public void Initialize(string npcId, int morale, int loyalty)
        {
            var traits = MentalTrait.None;

            // 基于士气和忠诚度初始化
            if (morale >= 70) traits |= MentalTrait.Hope;
            if (morale <= 30) traits |= MentalTrait.Fear;
            if (loyalty >= 60) traits |= MentalTrait.Trust;
            if (loyalty <= 20) traits |= MentalTrait.Paranoia;
            if (morale >= 50 && loyalty >= 40) traits |= MentalTrait.Resolve;

            _traits[npcId] = traits;
            _states[npcId] = DeriveState(morale, loyalty, traits);
        }

        /// <summary>每日更新心理状态</summary>
        public void DailyUpdate(NPCInstance npc)
        {
            if (!_traits.ContainsKey(npc.Id)) Initialize(npc.Id, npc.Morale, npc.Loyalty);

            var traits = _traits[npc.Id];

            // 基于NPC状态调整
            if (npc.Status == NPCStatus.Injured)
            {
                traits |= MentalTrait.Fear;
                traits &= ~MentalTrait.Hope;
            }

            // 基于士气
            if (npc.Morale >= 70) traits |= MentalTrait.Hope;
            else if (npc.Morale <= 20) traits |= MentalTrait.Despair;

            // 基于忠诚
            if (npc.Loyalty >= 60) traits |= MentalTrait.Trust;
            else if (npc.Loyalty <= 15) traits |= MentalTrait.Paranoia;

            // 绝望蔓延：如果有绝望特征，小概率传染给别人
            // (由外部调用 ApplyDespairSpread 处理)

            _traits[npc.Id] = traits;

            var oldState = _states[npc.Id];
            var newState = DeriveState(npc.Morale, npc.Loyalty, traits);
            if (oldState != newState)
            {
                _states[npc.Id] = newState;
                EventBus.Publish(new OnMentalStateChanged
                {
                    NPCId = npc.Id,
                    OldState = oldState,
                    NewState = newState,
                    Reason = DeriveReason(npc, newState)
                });
            }
        }

        /// <summary>施加创伤</summary>
        public void ApplyTrauma(string npcId, string reason)
        {
            if (!_traits.ContainsKey(npcId)) return;
            var traits = _traits[npcId];
            traits |= MentalTrait.Trauma;
            traits |= MentalTrait.Fear;
            traits &= ~MentalTrait.Hope;
            _traits[npcId] = traits;

            var oldState = _states[npcId];
            _states[npcId] = MentalState.Traumatized;
            if (oldState != MentalState.Traumatized)
            {
                EventBus.Publish(new OnMentalStateChanged
                {
                    NPCId = npcId, OldState = oldState, NewState = MentalState.Traumatized, Reason = reason
                });
            }
        }

        /// <summary>施加希望</summary>
        public void ApplyHope(string npcId, string reason)
        {
            if (!_traits.ContainsKey(npcId)) return;
            var traits = _traits[npcId];
            traits |= MentalTrait.Hope;
            traits |= MentalTrait.Trust;
            traits &= ~MentalTrait.Despair;
            _traits[npcId] = traits;
        }

        /// <summary>绝望蔓延</summary>
        public void ApplyDespairSpread(NPCSystem npcSystem, Random rng)
        {
            var despairIds = new List<string>();
            foreach (var kv in _states)
            {
                if (kv.Value == MentalState.Despair) despairIds.Add(kv.Key);
            }

            if (despairIds.Count == 0) return;

            foreach (var npc in npcSystem.GetAllNPCs())
            {
                if (!npc.IsAlive) continue;
                var state = GetState(npc.Id);
                if (state == MentalState.Despair || state == MentalState.Traumatized) continue;

                // 检查是否认识绝望者
                foreach (var despairId in despairIds)
                {
                    int rel = npc.GetRelationship(despairId);
                    if (rel > 0 && rng.Next(100) < 15)
                    {
                        var traits = _traits.ContainsKey(npc.Id) ? _traits[npc.Id] : MentalTrait.None;
                        traits |= MentalTrait.Fear;
                        _traits[npc.Id] = traits;
                        break;
                    }
                }
            }
        }

        /// <summary>心理状态是否允许探索</summary>
        public bool CanExplore(string npcId)
        {
            var state = GetState(npcId);
            return state != MentalState.Despair && state != MentalState.Traumatized;
        }

        /// <summary>心理状态对战斗属性的影响</summary>
        public int GetCombatModifier(string npcId)
        {
            var state = GetState(npcId);
            switch (state)
            {
                case MentalState.Hopeful: return 2;
                case MentalState.Stable: return 0;
                case MentalState.Anxious: return -1;
                case MentalState.Despair: return -3;
                case MentalState.Traumatized: return -2;
                default: return 0;
            }
        }

        private MentalState DeriveState(int morale, int loyalty, MentalTrait traits)
        {
            if ((traits & MentalTrait.Trauma) != 0 && morale < 30) return MentalState.Traumatized;
            if ((traits & MentalTrait.Despair) != 0 || morale <= 10) return MentalState.Despair;
            if (morale >= 60 && loyalty >= 40 && (traits & MentalTrait.Hope) != 0) return MentalState.Hopeful;
            if (morale <= 40 || (traits & MentalTrait.Fear) != 0) return MentalState.Anxious;
            return MentalState.Stable;
        }

        private string DeriveReason(NPCInstance npc, MentalState newState)
        {
            switch (newState)
            {
                case MentalState.Hopeful: return $"{npc.Name}对未来充满希望";
                case MentalState.Despair: return $"{npc.Name}陷入了绝望";
                case MentalState.Traumatized: return $"{npc.Name}受到了心理创伤";
                case MentalState.Anxious: return $"{npc.Name}变得焦虑不安";
                default: return $"{npc.Name}心理状态恢复稳定";
            }
        }
    }
}
