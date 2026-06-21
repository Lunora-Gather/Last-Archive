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
                        ResourceAmount = item.ResourceAmount, Source = item.Source
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
                }
            }

            return data;
        }

        /// <summary>保存游戏</summary>
        public bool SaveGame(SaveData data)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_savePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] 保存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>加载游戏</summary>
        public SaveData LoadGame()
        {
            try
            {
                if (!HasSave()) return null;
                string json = File.ReadAllText(_savePath);
                var options = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                };
                return JsonSerializer.Deserialize<SaveData>(json, options);
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
