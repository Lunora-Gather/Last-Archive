using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LastArchive
{
    internal static class PortableSave
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        private const int MaxGameValue = 1_000_000;
        private const string Format = "last-archive-save";
        private const int Version = 1;
        private const string InvalidFile = "这不是可读取的最后档案城存档。";
        private const string FileTooLarge = "存档文件不能超过 2 MB。";
        private static readonly JsonSerializerOptions Options = new()
        {
            MaxDepth = 32,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
        };

        public static string Export(SaveData data)
        {
            var json = JsonSerializer.Serialize(new { format = Format, version = Version, data }, Options);
            return Encoding.UTF8.GetByteCount(json) <= MaxBytes ? json : null;
        }

        public static (SaveData Data, string Error) Read(Stream stream, long contentLength)
        {
            if (contentLength > MaxBytes) return (null, FileTooLarge);

            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int length;
            while ((length = stream.Read(chunk)) > 0)
            {
                if (buffer.Length + length > MaxBytes) return (null, FileTooLarge);
                buffer.Write(chunk, 0, length);
            }

            try
            {
                using var file = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length),
                    new JsonDocumentOptions { MaxDepth = Options.MaxDepth });
                var root = file.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String ||
                    format.GetString() != Format)
                    return (null, InvalidFile);
                if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number ||
                    !version.TryGetInt32(out int number) || number != Version)
                    return (null, "此存档文件版本暂不受支持。");
                if (!root.TryGetProperty("data", out var payload) || payload.ValueKind != JsonValueKind.Object ||
                    !payload.TryGetProperty("Version", out var saveVersion) || saveVersion.ValueKind != JsonValueKind.String ||
                    !payload.TryGetProperty("CurrentPhase", out _) || !payload.TryGetProperty("Items", out _) ||
                    !payload.TryGetProperty("LastSettledDay", out _))
                    return (null, InvalidFile);
                if (saveVersion.GetString() != GameConstants.SaveVersion)
                    return (null, "此游戏版本的存档暂不受支持。");

                var data = payload.Deserialize<SaveData>(Options);
                return IsValid(data) ? (data, null) : (null, InvalidFile);
            }
            catch (JsonException)
            {
                return (null, InvalidFile);
            }
        }

        public static bool MatchesWorld(SaveData data, GameManager game)
        {
            return ContainsSameEntries(data.NPCStates.Select(npc => npc.Id), game.NPCs.GetAllNPCs().Select(npc => npc.Id))
                && ContainsSameEntries(data.BuildingStates.Select(building => building.Id),
                    game.Buildings.GetAllBuildings().Select(building => building.Id))
                && ContainsSameEntries(data.QuestStates.Select(quest => (quest.QuestId, quest.ObjectiveProgress.Count)),
                    game.Quests.GetAllQuests().Select(quest => (quest.QuestId, quest.Objectives.Count)))
                && ContainsSameEntries(data.Rooms.Select(room => (room.MapId, room.RoomId)),
                    game.Exploration.GetAllMaps().SelectMany(map => map.Rooms.Select(room => (map.Id, room.Id))));
        }

        private static bool ContainsSameEntries<T>(IEnumerable<T> entries, IEnumerable<T> expected)
        {
            var keys = entries.ToHashSet();
            return keys.Count == entries.Count() && keys.SetEquals(expected);
        }

        private static bool IsValid(SaveData data)
        {
            return HasValidProgress(data) && HasValidWorld(data);
        }

        private static bool HasValidProgress(SaveData data) =>
            data.CurrentDay is > 0 and <= MaxGameValue && Enum.IsDefined(data.CurrentPhase)
                && data.LastSettledDay is >= 0 && data.LastSettledDay <= data.CurrentDay
                && IsGameValue(data.StarvationDays) && IsGameValue(data.TotalDaysSurvived)
                && IsGameValue(data.TotalExplorationsCompleted) && IsGameValue(data.TotalCombatsWon)
                && IsGameValue(data.MarketDay) && IsGameValue(data.MarketTrades)
                && data.LocationVisits != null && data.LocationVisits.Values.All(IsGameValue)
                && (!data.ChosenEnding.HasValue || Enum.IsDefined(data.ChosenEnding.Value));

        private static bool HasValidWorld(SaveData data) =>
            data.Resources?.Count == Enum.GetValues<ResourceType>().Length
                && data.Resources.All(resource => resource != null && Enum.IsDefined(resource.Type) &&
                    IsGameValue(resource.Amount))
                && data.Resources.Select(resource => resource.Type).Distinct().Count() == data.Resources.Count
                && data.NPCStates is { Count: > 0 } && data.NPCStates.All(IsValidResident)
                && data.BuildingStates is { Count: > 0 } && data.BuildingStates.All(building =>
                    building != null && !string.IsNullOrEmpty(building.Id) &&
                    building.Level is >= 0 and <= GameConfig.MaxBuildingLevel && building.Built == (building.Level > 0))
                && data.QuestStates is { Count: > 0 } && data.QuestStates.All(quest =>
                    quest != null && !string.IsNullOrEmpty(quest.QuestId) && Enum.IsDefined(quest.Status) &&
                    quest.ObjectiveProgress != null && quest.ObjectiveProgress.All(IsGameValue))
                && data.Items?.Count <= ItemSystem.MaxInventorySize && data.Items.All(IsValidItem)
                && data.Factions != null && data.Factions.All(faction =>
                    faction != null && Enum.IsDefined(faction.Type) && faction.Reputation is >= -100 and <= 100)
                && data.Crises != null && data.Crises.All(crisis =>
                    crisis != null && Enum.IsDefined(crisis.Type) && Enum.IsDefined(crisis.Severity) &&
                    IsGameValue(crisis.StartDay) && IsGameValue(crisis.Duration))
                && data.Rooms != null && data.Rooms.All(room => room != null &&
                    !string.IsNullOrEmpty(room.MapId) && !string.IsNullOrEmpty(room.RoomId))
                && data.UnlockedLocations is { Count: > 0 } && data.UnlockedLocations.All(id => !string.IsNullOrEmpty(id));

        private static bool IsValidResident(NPCSaveData npc) =>
            npc != null && !string.IsNullOrEmpty(npc.Id) && Enum.IsDefined(npc.Status) && Enum.IsDefined(npc.CurrentWork)
            && npc.Health is >= 0 and <= GameConfig.MaxNPCHealth && npc.Morale is >= 0 and <= GameConfig.MaxNPCMorale
            && npc.Hunger is >= 0 and <= GameConfig.MaxNPCHunger && npc.Fatigue is >= 0 and <= GameConfig.MaxNPCFatigue
            && npc.Loyalty is >= 0 and <= GameConfig.MaxNPCLoyalty
            && npc.Relationships != null && npc.Relationships.All(relation =>
                relation != null && !string.IsNullOrEmpty(relation.TargetId) &&
                relation.Value is >= GameConfig.MinRelationship and <= GameConfig.MaxRelationship)
            && npc.MemoryEntries is { Count: <= 200 } && npc.MemoryEntries.All(entry => entry != null &&
                Enum.IsDefined(entry.EventType) && IsGameValue(entry.Day) && entry.Tags != null);

        private static bool IsValidItem(ItemSaveData item) =>
            item != null && !string.IsNullOrEmpty(item.Id) && !string.IsNullOrEmpty(item.Name)
            && Enum.IsDefined(item.Type) && Enum.IsDefined(item.Rarity)
            && (!item.ResourceType.HasValue || Enum.IsDefined(item.ResourceType.Value))
            && IsGameValue(item.Value) && IsGameValue(item.AttackBonus) && IsGameValue(item.DefenseBonus)
            && IsGameValue(item.HealthBonus) && IsGameValue(item.HealAmount) && IsGameValue(item.MoraleBoost)
            && IsGameValue(item.ResourceAmount) && (!item.RequiredLevel.HasValue || IsGameValue(item.RequiredLevel.Value));

        private static bool IsGameValue(int value) => value is >= 0 and <= MaxGameValue;
    }
}
