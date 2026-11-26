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

        // Targets in the portable format before the campaign changed. Progress was positional.
        private static readonly Dictionary<string, int[]> PreCampaignTargets = new()
        {
            ["main_1"] = [5], ["main_2"] = [1], ["main_3"] = [1], ["main_4"] = [1, 20]
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

                // The old writer omitted all campaign fields. A modern roster cannot lose its route discriminator.
                if (payload.TryGetProperty(nameof(SaveData.SupplyRoute), out var route)
                    ? route.ValueKind != JsonValueKind.String
                    : payload.TryGetProperty(nameof(SaveData.Scenario), out _) || payload.TryGetProperty(nameof(SaveData.RosterDay), out _))
                    return (null, InvalidFile);

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
            var maps = game.Exploration.GetAllMaps();
            var mapIds = maps.Select(map => map.Id).ToHashSet();
            var visitIds = mapIds.Concat(maps.SelectMany(map => map.Rooms.Select(room => room.Id))).ToHashSet();
            return ContainsSameEntries(data.NPCStates.Select(npc => npc.Id), game.NPCs.GetAllNPCs().Select(npc => npc.Id))
                && ContainsSameEntries(data.BuildingStates.Select(building => building.Id),
                    game.Buildings.GetAllBuildings().Select(building => building.Id))
                && ContainsSameEntries(data.QuestStates.Select(quest => quest.QuestId),
                    game.Quests.GetAllQuests().Select(quest => quest.QuestId))
                && ContainsSameEntries(data.Rooms.Select(room => (room.MapId, room.RoomId)),
                    maps.SelectMany(map => map.Rooms.Select(room => (map.Id, room.Id))))
                && data.UnlockedLocations.All(mapIds.Contains)
                && data.UnlockedLocations.Distinct().Count() == data.UnlockedLocations.Count
                && data.LocationVisits.Keys.All(visitIds.Contains)
                && HasValidCampaign(data) && HasValidRoster(data, game)
                && data.QuestStates.All(state => PrepareObjectives(state, game.Quests.GetQuest(state.QuestId), !data.SupplyRoute.HasValue));
        }

        private static bool PrepareObjectives(QuestSaveData state, QuestData quest, bool preCampaign)
        {
            var counts = state.ObjectiveProgress;
            bool completed = state.Status == QuestStatus.Completed;
            bool hasOldTargets = PreCampaignTargets.TryGetValue(state.QuestId, out var oldTargets);
            // Loading a legacy local checkpoint can leave old completed counts in a modern export.
            bool inheritedCounts = hasOldTargets && completed && counts.Count == quest.Objectives.Count &&
                counts.SequenceEqual(oldTargets);
            bool migrate = hasOldTargets && (preCampaign || inheritedCounts);
            if (counts.Count != (migrate ? oldTargets.Length : quest.Objectives.Count)) return false;
            for (int i = 0; i < counts.Count; i++)
            {
                int required = migrate ? oldTargets[i] : quest.Objectives[i].RequiredAmount;
                // SurviveDays records the day reached, which can exceed the target.
                bool recordsDay = !migrate && quest.Objectives[i].Type == ObjectiveType.SurviveDays;
                if ((counts[i] > required && !recordsDay) || (completed && counts[i] < required) ||
                    (state.Status == QuestStatus.NotStarted && counts[i] != 0)) return false;
            }
            // Only rewrite the detached import. RestoreCampaign recomputes active chapters from world evidence.
            if (migrate)
                state.ObjectiveProgress = quest.Objectives.Select(obj => completed ? obj.RequiredAmount : 0).ToList();
            return true;
        }

        private static bool HasValidCampaign(SaveData data)
        {
            var chapters = data.QuestStates.ToDictionary(quest => quest.QuestId, quest => quest.Status);
            bool unlocked = true;
            for (int chapter = 1; chapter <= 4; chapter++)
            {
                var status = chapters[$"main_{chapter}"];
                if (unlocked != (status != QuestStatus.NotStarted)) return false;
                unlocked = status == QuestStatus.Completed;
            }
            if ((data.IsVictory && (!data.IsGameOver || !unlocked)) ||
                (data.ChosenEnding.HasValue && (!data.IsVictory || !GameManager.EndingChoices.Contains(data.ChosenEnding.Value))))
                return false;

            if (!data.SupplyRoute.HasValue)
                return data.PlannedSupplyRoute == SupplyRoute.Undecided && data.Scenario == CampaignScenario.Standard &&
                    !data.ArchiveRecovered && !data.TowerCalibrated && !data.RelayInstalled && !data.RelayCommissioned &&
                    data.ArchiveReturnDay == 0 && data.DepartureDay == 0 && data.RosterDay == 0;

            bool hasRoute = data.SupplyRoute != SupplyRoute.Undecided;
            return hasRoute == (chapters["main_2"] == QuestStatus.Completed)
                && (data.PlannedSupplyRoute == SupplyRoute.Undecided || chapters["main_2"] == QuestStatus.Active)
                && data.ArchiveRecovered == (chapters["main_3"] == QuestStatus.Completed)
                && (!data.ArchiveRecovered || hasRoute) && (data.ArchiveReturnDay == 0 || data.ArchiveRecovered)
                && (!data.TowerCalibrated || data.ArchiveRecovered) && (!data.RelayInstalled || data.TowerCalibrated)
                && (!data.RelayCommissioned || data.RelayInstalled)
                && data.RelayCommissioned == (chapters["main_4"] == QuestStatus.Completed);
        }

        private static bool HasValidRoster(SaveData data, GameManager game)
        {
            var fielded = data.FieldedResidents;
            int departuresToday = data.DepartureDay == data.CurrentDay ? data.Departures : 0;
            bool hasAssignments = data.CampDuty != CampDuty.None || fielded.Count > 0;
            if ((data.RosterDay != data.CurrentDay && (hasAssignments || data.RosterDay != 0)) ||
                (departuresToday > 0) != (fielded.Count > 0) ||
                fielded.Count > departuresToday * GameConfig.MaxExplorationTeamSize ||
                fielded.Count != fielded.Distinct().Count() || fielded.Any(id => game.NPCs.GetNPC(id) == null)) return false;

            if (data.CampDuty == CampDuty.None) return data.DutyWorker == null;
            if (string.IsNullOrEmpty(data.DutyWorker) || fielded.Contains(data.DutyWorker)) return false;
            // Check the saved opening and clinic. Only fixed resident skills come from the destination.
            // Supplies are prepaid and the worker may have been injured since assignment.
            if (!GameManager.HasDutySkill(data.CampDuty, game.NPCs.GetNPC(data.DutyWorker))) return false;
            return data.CampDuty switch
            {
                CampDuty.Pump => data.Scenario == CampaignScenario.WaterShortage,
                CampDuty.Triage => data.Scenario == CampaignScenario.MedicalShortage ||
                    data.BuildingStates.Any(building => building.Id == "med_room" && building.Built),
                _ => false
            };
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
                && data.DepartureDay >= 0 && data.DepartureDay <= data.CurrentDay
                && data.Departures is >= 0 and <= GameManager.DeparturesPerDay && (data.DepartureDay == 0) == (data.Departures == 0)
                && data.ArchiveReturnDay >= 0 && data.ArchiveReturnDay <= data.CurrentDay
                && Enum.IsDefined(data.Scenario) && Enum.IsDefined(data.PlannedSupplyRoute)
                && Enum.IsDefined(data.CampDuty)
                && data.FieldedResidents != null
                && data.FieldedResidents.All(id => !string.IsNullOrEmpty(id))
                && (!data.SupplyRoute.HasValue || Enum.IsDefined(data.SupplyRoute.Value))
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
                    !string.IsNullOrEmpty(room.MapId) && !string.IsNullOrEmpty(room.RoomId) &&
                    (room.Visited || !room.Claimed && !room.Cleared))
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
