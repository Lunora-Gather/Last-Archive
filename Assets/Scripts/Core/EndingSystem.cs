// ============================================================
// Last Archive - 多结局系统
// 6种结局由玩家选择和声望决定
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>结局类型</summary>
    public enum EndingType
    {
        None,
        Broadcast,     // 广播 - 联系外界，重建文明
        Silence,       // 沉默 - 保持隐秘，独自生存
        Upload,        // 上传 - 将记忆上传到数字永恒
        Destroy,       // 摧毁 - 摧毁档案城，终结一切
        Share,         // 公开 - 公开真相，让世界面对过去
        Vanish         // 消失 - 档案城神秘消失
    }

    /// <summary>结局数据</summary>
    public class EndingData
    {
        public EndingType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Epilogue { get; set; }
        public bool IsGood { get; set; }
    }

    /// <summary>
    /// 多结局系统 - 根据玩家行为决定最终结局
    /// </summary>
    public class EndingSystem
    {
        /// <summary>计算最终结局</summary>
        public EndingData CalculateEnding(GameManager game)
        {
            var endings = new List<(EndingType type, int score, EndingData data)>();

            int shards = game.Resources.GetResourceAmount(ResourceType.MemoryShards);
            int day = game.Time.CurrentDay;
            int aliveNPCs = game.NPCs.GetAliveCount();
            int totalNPCs = 0;
            foreach (var n in game.NPCs.GetAllNPCs()) totalNPCs++;

            // 派系声望
            var archiveFaction = game.Factions.GetFaction(FactionType.ArchiveOrder);
            var survivorFaction = game.Factions.GetFaction(FactionType.Survivalists);
            var wandererFaction = game.Factions.GetFaction(FactionType.Wanderers);
            int archiveRep = archiveFaction?.Reputation ?? 0;
            int survivorRep = survivorFaction?.Reputation ?? 0;
            int wandererRep = wandererFaction?.Reputation ?? 0;

            // 广播结局：完成主线4 + 档案教团声望高
            int broadcastScore = 0;
            var mainQuest4 = game.Quests.GetQuest("main_4");
            if (mainQuest4 != null && mainQuest4.Status == QuestStatus.Completed) broadcastScore += 50;
            broadcastScore += Math.Max(0, archiveRep);
            broadcastScore += shards > 20 ? 10 : 0;
            endings.Add((EndingType.Broadcast, broadcastScore, new EndingData
            {
                Type = EndingType.Broadcast,
                Title = "📡 广播重启",
                Description = "你修复了广播塔，联系到了外界的幸存者。",
                Epilogue = "无线电波穿越废墟，带着档案城的坐标飞向远方。几周后，第一批探访者抵达。这不是结束，而是新世界的开始。",
                IsGood = true
            }));

            // 沉默结局：生存主义者声望高 + 存活天数长
            int silenceScore = Math.Max(0, survivorRep) + day + (aliveNPCs >= totalNPCs ? 20 : 0);
            endings.Add((EndingType.Silence, silenceScore, new EndingData
            {
                Type = EndingType.Silence,
                Title = "🤫 永恒沉默",
                Description = "你选择了隐秘生存，档案城成为传说中的避难所。",
                Epilogue = "档案城的大门缓缓关闭，外面的世界永远不知道这里的存在。但在这里，记忆被安全地保存着，一代又一代。",
                IsGood = true
            }));

            // 上传结局：记忆碎片多 + 档案教团声望高
            int uploadScore = shards * 2 + Math.Max(0, archiveRep);
            endings.Add((EndingType.Upload, uploadScore, new EndingData
            {
                Type = EndingType.Upload,
                Title = "💾 意识上传",
                Description = "你将所有记忆碎片上传到了永恒的数字空间。",
                Epilogue = "肉体终将消亡，但记忆获得了永生。在数字的海洋中，每一个曾经的灵魂继续着它们的对话。",
                IsGood = true
            }));

            // 摧毁结局：档案教团声望极低 或 忠诚度极低
            int avgLoyalty = 0, loyaltyCount = 0;
            foreach (var npc in game.NPCs.GetAllNPCs())
            {
                if (npc.IsAlive) { avgLoyalty += npc.Loyalty; loyaltyCount++; }
            }
            if (loyaltyCount > 0) avgLoyalty /= loyaltyCount;

            int destroyScore = Math.Max(0, -archiveRep) + (avgLoyalty < 20 ? 30 : 0) + (aliveNPCs <= 3 ? 20 : 0);
            endings.Add((EndingType.Destroy, destroyScore, new EndingData
            {
                Type = EndingType.Destroy,
                Title = "🔥 焚毁一切",
                Description = "档案城在火焰中化为灰烬，所有记忆随之消散。",
                Epilogue = "也许这是正确的选择。有些记忆，注定不应存在。火焰吞噬了一切，只剩下沉默的废墟。",
                IsGood = false
            }));

            // 公开结局：流浪者声望高 + 探索次数多
            int shareScore = Math.Max(0, wandererRep) + game.TotalExplorationsCompleted * 5 + (shards > 15 ? 15 : 0);
            endings.Add((EndingType.Share, shareScore, new EndingData
            {
                Type = EndingType.Share,
                Title = "🌍 真相公开",
                Description = "你决定向全世界公开档案城的存在和记忆。",
                Epilogue = "消息传开后，各方势力涌入。混乱、争论、希望……但至少，真相不再被掩埋。",
                IsGood = true
            }));

            // 消失结局：记忆风暴活跃 + 特殊条件
            int vanishScore = shards > 30 ? 40 : 0;
            endings.Add((EndingType.Vanish, vanishScore, new EndingData
            {
                Type = EndingType.Vanish,
                Title = "👻 量子消失",
                Description = "档案城在记忆风暴中神秘消失，仿佛从未存在。",
                Epilogue = "有人说它去往了另一个维度。有人说它只是被遗忘。但那些曾经住在那里的人，心中永远记得。",
                IsGood = false
            }));

            // 选得分最高的结局
            endings.Sort((a, b) => b.score.CompareTo(a.score));
            return endings[0].data;
        }

        /// <summary>获取所有结局列表（供UI展示）</summary>
        public List<EndingData> GetAllEndings()
        {
            return new List<EndingData>
            {
                new EndingData { Type = EndingType.Broadcast, Title = "📡 广播重启", Description = "联系外界，重建文明", Epilogue = "无线电波穿越废墟，带着档案城的坐标飞向远方。几周后，第一批探访者抵达。这不是结束，而是新世界的开始。", IsGood = true },
                new EndingData { Type = EndingType.Silence, Title = "🤫 永恒沉默", Description = "保持隐秘，独自生存", Epilogue = "档案城的大门缓缓关闭，外面的世界永远不知道这里的存在。但在这里，记忆被安全地保存着，一代又一代。", IsGood = true },
                new EndingData { Type = EndingType.Upload, Title = "💾 意识上传", Description = "记忆上传数字永恒", Epilogue = "肉体终将消亡，但记忆获得了永生。在数字的海洋中，每一个曾经的灵魂继续着它们的对话。", IsGood = true },
                new EndingData { Type = EndingType.Destroy, Title = "🔥 焚毁一切", Description = "摧毁档案城，终结一切", Epilogue = "也许这是正确的选择。有些记忆，注定不应存在。火焰吞噬了一切，只剩下沉默的废墟。", IsGood = false },
                new EndingData { Type = EndingType.Share, Title = "🌍 真相公开", Description = "公开真相，面对过去", Epilogue = "消息传开后，各方势力涌入。混乱、争论、希望……但至少，真相不再被掩埋。", IsGood = true },
                new EndingData { Type = EndingType.Vanish, Title = "👻 量子消失", Description = "档案城神秘消失", Epilogue = "有人说它去往了另一个维度。有人说它只是被遗忘。但那些曾经住在那里的人，心中永远记得。", IsGood = false }
            };
        }
    }
}
