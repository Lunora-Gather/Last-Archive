// ============================================================
// Last Archive - 存档系统
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LastArchive
{
    /// <summary>
    /// 存档数据
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public string Version { get; set; } = GameConstants.SaveVersion;
        public int CurrentDay { get; set; }
        public GamePhase CurrentPhase { get; set; }
        public List<ResourceSaveEntry> Resources { get; set; } = new List<ResourceSaveEntry>();
        public List<NPCSaveData> NPCStates { get; set; } = new List<NPCSaveData>();
        public List<BuildingSaveData> BuildingStates { get; set; } = new List<BuildingSaveData>();
        public List<QuestSaveData> QuestStates { get; set; } = new List<QuestSaveData>();
        public List<string> ExploredMaps { get; set; } = new List<string>();
        public List<string> UnlockedLocations { get; set; } = new List<string>();
        public Dictionary<string, bool> StoryFlags { get; set; } = new Dictionary<string, bool>();
        public List<ItemSaveData> Items { get; set; } = new List<ItemSaveData>();
        public List<FactionSaveData> Factions { get; set; } = new List<FactionSaveData>();
        public List<string> UnlockedAchievements { get; set; } = new List<string>();
        public string SaveTime { get; set; }
        public int? LastSettledDay { get; set; }
        public int StarvationDays { get; set; }
        public int TotalDaysSurvived { get; set; }
        public int TotalExplorationsCompleted { get; set; }
        public int TotalCombatsWon { get; set; }
        public bool IsGameOver { get; set; }
        public bool IsVictory { get; set; }
        public string LastDailyEvent { get; set; }
        public EndingType? ChosenEnding { get; set; }
        public int MarketDay { get; set; }
        public int MarketTrades { get; set; }
        public Dictionary<string, int> LocationVisits { get; set; } = new();
        public List<RoomSaveData> Rooms { get; set; } = new();
        public List<CrisisInstance> Crises { get; set; } = new();
        public Dictionary<string, int> AchievementDays { get; set; } = new();
        public SupplyRoute? SupplyRoute { get; set; }
        public SupplyRoute PlannedSupplyRoute { get; set; }
        public CampaignScenario Scenario { get; set; }
        public int RosterDay { get; set; }
        public List<string> FieldedResidents { get; set; } = new();
        public CampDuty CampDuty { get; set; }
        public string DutyWorker { get; set; }
        public bool ArchiveRecovered { get; set; }
        public bool TowerCalibrated { get; set; }
        public bool RelayInstalled { get; set; }
        public bool RelayCommissioned { get; set; }
        public int ArchiveReturnDay { get; set; }
        public int DepartureDay { get; set; }
        public int Departures { get; set; }
    }

    public class RoomSaveData
    {
        public string MapId { get; set; }
        public string RoomId { get; set; }
        public bool Visited { get; set; }
        public bool Locked { get; set; }
        public bool Claimed { get; set; }
        public bool Cleared { get; set; }
    }

    [Serializable]
    public class ResourceSaveEntry
    {
        public ResourceType Type { get; set; }
        public int Amount { get; set; }
    }

    [Serializable]
    public class NPCSaveData
    {
        public string Id { get; set; }
        public int Health { get; set; }
        public int Morale { get; set; }
        public int Hunger { get; set; }
        public int Fatigue { get; set; }
        public int Loyalty { get; set; }
        public NPCStatus Status { get; set; }
        public WorkType CurrentWork { get; set; }
        public string WeaponId { get; set; }
        public string ArmorId { get; set; }
        public MentalState? MentalState { get; set; }
        public MentalTrait? MentalTraits { get; set; }
        public List<NPCRelationship> Relationships { get; set; } = new List<NPCRelationship>();
        public List<MemoryEntry> MemoryEntries { get; set; } = new List<MemoryEntry>();
        public string MemorySummary { get; set; }
        public List<string> DiaryHistory { get; set; } = new List<string>();
    }

    [Serializable]
    public class BuildingSaveData
    {
        public string Id { get; set; }
        public int Level { get; set; }
        public bool Built { get; set; }
    }

    [Serializable]
    public class QuestSaveData
    {
        public string QuestId { get; set; }
        public QuestStatus Status { get; set; }
        public List<int> ObjectiveProgress { get; set; } = new List<int>();
    }

    [Serializable]
    public class ItemSaveData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ItemType Type { get; set; }
        public ItemRarity Rarity { get; set; }
        public int Value { get; set; }
        public int AttackBonus { get; set; }
        public int DefenseBonus { get; set; }
        public int HealthBonus { get; set; }
        public int HealAmount { get; set; }
        public int MoraleBoost { get; set; }
        public ResourceType? ResourceType { get; set; }
        public int ResourceAmount { get; set; }
        public string Source { get; set; }
        public int? RequiredLevel { get; set; }
    }

    [Serializable]
    public class FactionSaveData
    {
        public FactionType Type { get; set; }
        public int Reputation { get; set; }
        public bool Unlocked { get; set; }
    }

    /// <summary>
    /// 存档系统 - 所有存档读写必须经过此系统
    /// </summary>
    public class SaveSystem
    {
        private readonly string _savePath;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public SaveSystem(string savePath = null)
        {
            _savePath = savePath ?? "save.json";
        }

        /// <summary>创建新存档数据</summary>
        public SaveData CreateNewSaveData(int day, GamePhase phase, ResourceSystem resources,
            NPCSystem npcSystem, BuildingSystem buildingSystem, QuestSystem questSystem,
            ItemSystem itemSystem = null, FactionSystem factionSystem = null, AchievementSystem achievementSystem = null)
        {
            var data = new SaveData
            {
                CurrentDay = day,
                CurrentPhase = phase,
                SaveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // 资源
            var allRes = resources.GetAllResources();
            foreach (var kv in allRes)
            {
                data.Resources.Add(new ResourceSaveEntry { Type = kv.Key, Amount = kv.Value });
            }

            // NPC
            foreach (var npc in npcSystem.GetAllNPCs())
            {
                data.NPCStates.Add(new NPCSaveData
                {
                    Id = npc.Id,
                    Health = npc.Health,
                    Morale = npc.Morale,
                    Hunger = npc.Hunger,
                    Fatigue = npc.Fatigue,
                    Loyalty = npc.Loyalty,
                    Status = npc.Status,
                    CurrentWork = npc.CurrentWork,
                    Relationships = new List<NPCRelationship>(npc.Relationships),
                    MemoryEntries = new List<MemoryEntry>(npc.Memory.Entries),
                    MemorySummary = npc.Memory.Summary,
                    DiaryHistory = new List<string>(npc.Memory.DiaryHistory)
                });
            }

            // 建筑
            foreach (var building in buildingSystem.GetAllBuildings())
            {
                data.BuildingStates.Add(new BuildingSaveData
                {
                    Id = building.Id,
                    Level = building.Level,
                    Built = building.Built
                });
            }

            // 任务
            foreach (var quest in questSystem.GetAllQuests())
            {
                var qsd = new QuestSaveData
                {
                    QuestId = quest.QuestId,
                    Status = quest.Status
                };
                foreach (var obj in quest.Objectives)
                {
                    qsd.ObjectiveProgress.Add(obj.CurrentProgress);
                }
                data.QuestStates.Add(qsd);
            }

            // 物品
            if (itemSystem != null)
            {
                foreach (var item in itemSystem.GetInventory())
                {
                    data.Items.Add(new ItemSaveData
                    {
                        Id = item.Id, Name = item.Name, Description = item.Description,
                        Type = item.Type, Rarity = item.Rarity, Value = item.Value,
                        AttackBonus = item.AttackBonus, DefenseBonus = item.DefenseBonus,
                        HealthBonus = item.HealthBonus, HealAmount = item.HealAmount,
                        MoraleBoost = item.MoraleBoost, ResourceType = item.ResourceType,
                        ResourceAmount = item.ResourceAmount, Source = item.Source, RequiredLevel = item.RequiredLevel
                    });
                }
            }

            // 派系
            if (factionSystem != null)
            {
                foreach (var faction in factionSystem.GetAllFactions())
                {
                    data.Factions.Add(new FactionSaveData
                    {
                        Type = faction.Type,
                        Reputation = faction.Reputation,
                        Unlocked = faction.Unlocked
                    });
                }
            }

            // 成就
            if (achievementSystem != null)
            {
                foreach (var ach in achievementSystem.GetUnlockedAchievements())
                {
                    data.UnlockedAchievements.Add(ach.Id);
                    data.AchievementDays[ach.Id] = ach.UnlockedDay;
                }
            }

            return data;
        }

        /// <summary>保存游戏</summary>
        public bool SaveGame(SaveData data)
        {
            var path = Path.GetFullPath(_savePath);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew))
                {
                    JsonSerializer.Serialize(stream, data, JsonOptions);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporaryPath, path, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] 保存失败: {ex.Message}");
                return false;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (IOException ex) { Console.WriteLine($"[SaveSystem] 无法清理临时存档: {ex.Message}"); }
                    catch (UnauthorizedAccessException ex) { Console.WriteLine($"[SaveSystem] 无法清理临时存档: {ex.Message}"); }
                }
            }
        }

        /// <summary>加载游戏</summary>
        public SaveData LoadGame()
        {
            try
            {
                if (!HasSave()) return null;
                using var stream = File.OpenRead(_savePath);
                var data = JsonSerializer.Deserialize<SaveData>(stream, JsonOptions);
                if (data == null || data.CurrentDay < 1 || !Enum.IsDefined(data.CurrentPhase) ||
                    data.Resources == null || data.NPCStates == null ||
                    data.BuildingStates == null || data.QuestStates == null ||
                    data.Resources.Exists(entry => entry == null) ||
                    data.NPCStates.Exists(entry => string.IsNullOrEmpty(entry?.Id)) ||
                    data.BuildingStates.Exists(entry => string.IsNullOrEmpty(entry?.Id)) ||
                    data.QuestStates.Exists(entry => string.IsNullOrEmpty(entry?.QuestId)) ||
                    data.Items?.Exists(entry => string.IsNullOrEmpty(entry?.Id)) == true)
                {
                    return null;
                }
                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] 加载失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>是否有存档</summary>
        public bool HasSave()
        {
            return File.Exists(_savePath);
        }

        /// <summary>删除存档</summary>
        public bool DeleteSave()
        {
            try
            {
                if (HasSave())
                {
                    File.Delete(_savePath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
