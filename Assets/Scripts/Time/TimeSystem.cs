// ============================================================
// Last Archive - 时间系统
// ============================================================

using System;

namespace LastArchive
{
    /// <summary>
    /// 时间系统 - 管理天数和阶段切换
    /// </summary>
    public class TimeSystem
    {
        public int CurrentDay { get; private set; } = 1;
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Day;
        public bool IsPaused { get; set; } = false;

        private ResourceSystem _resourceSystem;
        private NPCSystem _npcSystem;
        private BuildingSystem _buildingSystem;
        private QuestSystem _questSystem;
        private MemorySummarizer _memorySummarizer;
        private EventGenerator _eventGenerator;

        public TimeSystem(ResourceSystem resourceSystem, NPCSystem npcSystem,
            BuildingSystem buildingSystem, QuestSystem questSystem,
            MemorySummarizer memorySummarizer, EventGenerator eventGenerator)
        {
            _resourceSystem = resourceSystem;
            _npcSystem = npcSystem;
            _buildingSystem = buildingSystem;
            _questSystem = questSystem;
            _memorySummarizer = memorySummarizer;
            _eventGenerator = eventGenerator;
        }

        /// <summary>开始新游戏</summary>
        public void StartNewGame()
        {
            CurrentDay = 1;
            CurrentPhase = GamePhase.Day;
            IsPaused = false;
            EventBus.Publish(new OnDayStarted { Day = CurrentDay });
        }

        /// <summary>切换到夜晚</summary>
        public void AdvanceToNight()
        {
            if (CurrentPhase != GamePhase.Day || IsPaused) return;
            CurrentPhase = GamePhase.Night;
            EventBus.Publish(new OnNightStarted { Day = CurrentDay });
        }

        /// <summary>切换到结算</summary>
        public void AdvanceToSummary()
        {
            if (CurrentPhase != GamePhase.Night || IsPaused) return;
            CurrentPhase = GamePhase.Summary;
            TriggerDailySummary();
        }

        /// <summary>进入下一天</summary>
        public void AdvanceToNextDay()
        {
            if (CurrentPhase != GamePhase.Summary || IsPaused) return;

            CurrentDay++;
            CurrentPhase = GamePhase.Day;

            // 更新存活天数目标
            _questSystem.UpdateSurviveDaysObjectives(CurrentDay);

            EventBus.Publish(new OnDayStarted { Day = CurrentDay });
        }

        /// <summary>每日结算</summary>
        public void TriggerDailySummary()
        {
            // 1. 建筑产出
            _buildingSystem.ProduceDailyResources();

            // 2. 每日资源消耗
            int npcCount = _npcSystem.GetAliveCount();
            int powerCost = _buildingSystem.GetDailyPowerConsumption();
            _resourceSystem.ApplyDailyConsumption(npcCount, powerCost);

            // 3. NPC每日状态变化与关系演化
            _npcSystem.ApplyDailyChanges();
            bool hasFoodCrisis = _resourceSystem.GetResourceAmount(ResourceType.Food) <= 0;
            bool hasWaterCrisis = _resourceSystem.GetResourceAmount(ResourceType.Water) <= 0;
            _npcSystem.UpdateNPCRelationships(hasFoodCrisis, hasWaterCrisis);

            // 4. 生成每日事件
            var eventContext = new EventContext
            {
                CurrentDay = CurrentDay,
                NPCCount = npcCount,
                Resources = _resourceSystem.GetAllResources()
            };
            foreach (var q in _questSystem.GetActiveQuests())
            {
                eventContext.ActiveQuests.Add(q.QuestId);
            }
            string dailyEvent = _eventGenerator.Generate(eventContext);

            // 5. NPC记忆总结
            foreach (var npc in _npcSystem.GetAllNPCs())
            {
                if (!npc.IsAlive) continue;

                var todayEntries = new List<MemoryEntry>();
                foreach (var entry in npc.Memory.Entries)
                {
                    if (entry.Day == CurrentDay) todayEntries.Add(entry);
                }

                var memContext = new MemoryContext
                {
                    NPCId = npc.Id,
                    NPCName = npc.Name,
                    Day = CurrentDay,
                    TodayEntries = todayEntries,
                    PreviousSummary = npc.Memory.Summary
                };

                npc.Memory.Summary = _memorySummarizer.Summarize(memContext);
                npc.Memory.DiaryHistory.Add($"第{CurrentDay}天：{npc.Memory.Summary}");
                if (npc.Memory.DiaryHistory.Count > 10)
                {
                    npc.Memory.DiaryHistory.RemoveAt(0);
                }
            }

            // 6. 更新资源收集类任务
            foreach (ResourceType rt in Enum.GetValues(typeof(ResourceType)))
            {
                _questSystem.UpdateResourceObjectives(rt, _resourceSystem.GetResourceAmount(rt));
            }

            EventBus.Publish(new OnDayEnded { Day = CurrentDay });
        }

        /// <summary>从存档恢复</summary>
        public void Restore(int day, GamePhase phase)
        {
            CurrentDay = day;
            CurrentPhase = phase;
        }

        /// <summary>获取阶段名称</summary>
        public string GetPhaseName()
        {
            switch (CurrentPhase)
            {
                case GamePhase.Day: return "白天";
                case GamePhase.Night: return "夜晚";
                case GamePhase.Summary: return "结算";
                default: return "未知";
            }
        }

        /// <summary>更新AI生成器引用</summary>
        public void UpdateAI(MemorySummarizer memorySummarizer, EventGenerator eventGenerator)
        {
            _memorySummarizer = memorySummarizer;
            _eventGenerator = eventGenerator;
        }
    }
}
