// ============================================================
// Last Archive - 时间系统
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;

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
        public string LastNarrative { get; private set; }
        public int WaterFlush { get; internal set; }
        public int LastWaterShortfall { get; private set; }
        private int DailyWaterNeed(int population) => population * GameConstants.DailyWaterConsumptionPerNPC + WaterFlush;

        private ResourceSystem _resourceSystem;
        private NPCSystem _npcSystem;
        private BuildingSystem _buildingSystem;
        private QuestSystem _questSystem;
        private MemorySummarizer _memorySummarizer;
        private EventGenerator _eventGenerator;
        private readonly Action _settleCampDuty;
        private readonly Func<int> _plannedWater;
        internal int ClinicRecovery => _buildingSystem.GetBuilding("med_room") is { Built: true } clinic ? 5 * clinic.Level : 0;

        public TimeSystem(ResourceSystem resourceSystem, NPCSystem npcSystem,
            BuildingSystem buildingSystem, QuestSystem questSystem,
            MemorySummarizer memorySummarizer, EventGenerator eventGenerator,
            Action settleCampDuty = null, Func<int> plannedWater = null)
        {
            _resourceSystem = resourceSystem;
            _npcSystem = npcSystem;
            _buildingSystem = buildingSystem;
            _questSystem = questSystem;
            _memorySummarizer = memorySummarizer;
            _eventGenerator = eventGenerator;
            _settleCampDuty = settleCampDuty;
            _plannedWater = plannedWater;
        }

        /// <summary>开始新游戏</summary>
        public void StartNewGame()
        {
            CurrentDay = 1;
            CurrentPhase = GamePhase.Day;
            IsPaused = false;
            LastNarrative = null;
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

        public sealed record SupplyForecast(ResourceType Type, int Stock, int Produced, int Used, int Reserve, int Shortfall);

        public IReadOnlyList<SupplyForecast> GetSupplyForecast()
        {
            var operating = _buildingSystem.GetOperatingBuildings();
            int population = _npcSystem.GetAliveCount();
            var result = new List<SupplyForecast>();
            foreach (var type in Enum.GetValues<ResourceType>())
            {
                int stock = _resourceSystem.GetResourceAmount(type);
                int produced = operating.Sum(building => building.DailyOutput.Get(type) * building.Level);
                if (type == ResourceType.Water && CurrentPhase != GamePhase.Summary) produced += _plannedWater?.Invoke() ?? 0;
                int used = operating.Sum(building => building.DailyConsumption.Get(type)) + (type switch
                {
                    ResourceType.Food => population * GameConstants.DailyFoodConsumptionPerNPC,
                    ResourceType.Water => DailyWaterNeed(population),
                    _ => 0
                });
                int balance = stock + produced - used;
                result.Add(new SupplyForecast(type, stock, produced, used, Math.Max(0, balance), Math.Max(0, -balance)));
            }
            return result;
        }

        /// <summary>每日结算</summary>
        public void TriggerDailySummary()
        {
            // 1. 建筑产出
            _buildingSystem.ProduceDailyResources();
            _settleCampDuty?.Invoke();

            // 2. 每日资源消耗
            int npcCount = _npcSystem.GetAliveCount();
            int foodNeed = npcCount * GameConstants.DailyFoodConsumptionPerNPC;
            int waterNeed = DailyWaterNeed(npcCount);
            LastWaterShortfall = Math.Max(0, waterNeed - _resourceSystem.GetResourceAmount(ResourceType.Water));
            double foodCoverage = foodNeed == 0 ? 1 : Math.Min(1, (double)_resourceSystem.GetResourceAmount(ResourceType.Food) / foodNeed);
            double waterCoverage = waterNeed == 0 ? 1 : Math.Min(1, (double)_resourceSystem.GetResourceAmount(ResourceType.Water) / waterNeed);
            _resourceSystem.ApplyDailyConsumption(npcCount, buildingPowerCost: 0, extraWater: WaterFlush);

            // 3. NPC每日状态变化与关系演化
            _npcSystem.ApplyDailyChanges(ClinicRecovery, Math.Min(foodCoverage, waterCoverage));
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
            LastNarrative = _eventGenerator.Generate(eventContext);

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
