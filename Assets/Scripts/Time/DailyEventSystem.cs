// ============================================================
// Last Archive - 每日随机事件系统
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 每日随机事件 - 在结算时触发，增加游戏变数和乐趣
    /// </summary>
    public class DailyEventSystem
    {
        private Random _rng = new Random();

        /// <summary>触发一个随机事件，返回事件描述（空字符串=无事件）</summary>
        public string TriggerRandomEvent(GameManager game)
        {
            // 30%概率触发随机事件
            if (_rng.Next(100) >= 30) return "";

            var candidates = new List<Func<GameManager, string>>();

            // 正面事件（60%）
            candidates.Add(EventMerchantVisit);
            candidates.Add(EventFindStash);
            candidates.Add(EventGoodWeather);
            candidates.Add(EventSurvivorGift);
            candidates.Add(EventInspiringDream);

            // 负面事件（40%）
            candidates.Add(EventRatInfestation);
            candidates.Add(EventEquipmentBreak);
            candidates.Add(EventColdSnap);

            // 按权重选择
            int roll = _rng.Next(100);
            int idx = roll < 60 ? _rng.Next(4) : 4 + _rng.Next(3);
            if (idx >= candidates.Count) idx = 0;

            return candidates[idx](game);
        }

        // === 正面事件 ===

        private string EventMerchantVisit(GameManager game)
        {
            int parts = _rng.Next(3, 8);
            game.Resources.AddResource(ResourceType.Parts, parts);
            return $"🧳 流浪商人路过！赠送了 {parts} 个零件。";
        }

        private string EventFindStash(GameManager game)
        {
            int food = _rng.Next(5, 12);
            game.Resources.AddResource(ResourceType.Food, food);
            return $"📦 在基地角落发现了隐藏的物资储备！获得 {food} 食物。";
        }

        private string EventGoodWeather(GameManager game)
        {
            // 好天气：所有人士气+5
            foreach (var npc in game.NPCs.GetAllNPCs())
            {
                if (npc.IsAlive) npc.Morale = Math.Min(100, npc.Morale + 5);
            }
            return "☀️ 难得的好天气！居民们心情愉快，士气提升。";
        }

        private string EventSurvivorGift(GameManager game)
        {
            int medicine = _rng.Next(2, 5);
            game.Resources.AddResource(ResourceType.Medicine, medicine);
            return $"🎁 外面的幸存者送来了礼物！获得 {medicine} 药品。";
        }

        private string EventInspiringDream(GameManager game)
        {
            game.Resources.AddResource(ResourceType.MemoryShards, 1);
            return "💫 有人做了一个关于灾难前的梦，醒来后想起了一段珍贵的记忆。获得1记忆碎片。";
        }

        // === 负面事件 ===

        private string EventRatInfestation(GameManager game)
        {
            int lost = Math.Min(5, game.Resources.GetResourceAmount(ResourceType.Food));
            if (lost > 0) game.Resources.ConsumeResource(ResourceType.Food, lost);
            return lost > 0 ? $"🐀 鼠患！老鼠偷吃了 {lost} 份食物。" : "🐀 有老鼠出没的迹象，但食物已经安全。";
        }

        private string EventEquipmentBreak(GameManager game)
        {
            int lost = Math.Min(3, game.Resources.GetResourceAmount(ResourceType.Parts));
            if (lost > 0) game.Resources.ConsumeResource(ResourceType.Parts, lost);
            return lost > 0 ? $"🔧 设备故障！损失了 {lost} 个零件。" : "🔧 设备发出异响，但没有零件可损失。";
        }

        private string EventColdSnap(GameManager game)
        {
            int lost = Math.Min(3, game.Resources.GetResourceAmount(ResourceType.Power));
            if (lost > 0) game.Resources.ConsumeResource(ResourceType.Power, lost);
            return lost > 0 ? $"❄️ 寒潮来袭！额外消耗了 {lost} 电力取暖。" : "❄️ 气温骤降，但电力储备已空。";
        }
    }
}
