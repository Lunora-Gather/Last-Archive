using System;
using System.Collections.Generic;
using System.Linq;

namespace LastArchive
{
    public enum SupplyRoute { Undecided, Homestead, Caravan }

    public partial class GameManager
    {
        public const int DeparturesPerDay = 2;
        public const int RelayPartsCost = 8;
        public const int RelayPowerCost = 5;
        public const int RelayTestPower = 3;
        public const int CaravanDeposit = 4;
        public const string SupplyMilestone = "建立补给路线";
        public const string ArchiveMilestone = "从旧档案城大厅带回档案";
        public const string TowerMilestone = "从天线平台带回校准数据";
        public const string RelayMilestone = "安装组件并在日终通电测试";

        public SupplyRoute Route { get; private set; }
        public bool ArchiveRecovered { get; private set; }
        public bool TowerCalibrated { get; private set; }
        public bool RelayInstalled { get; private set; }
        private bool _relayCommissioned;
        private int _archiveReturnDay;
        private int _departureDay;
        private int _departures;

        public int DeparturesLeft => DeparturesPerDay - (_departureDay == Time.CurrentDay ? _departures : 0);
        public int DepartureFood => Route == SupplyRoute.Caravan ? 2 : 5;
        public int DepartureWater => Route == SupplyRoute.Caravan ? 1 : 4;
        public bool CanAffordDeparture => Resources.HasEnough(ResourceType.Food, DepartureFood) &&
            Resources.HasEnough(ResourceType.Water, DepartureWater);

        private void ResetCampaign()
        {
            Route = SupplyRoute.Undecided;
            PlannedSupplyRoute = SupplyRoute.Undecided;
            ClearRoster();
            ArchiveRecovered = TowerCalibrated = RelayInstalled = _relayCommissioned = false;
            _archiveReturnDay = 0;
            _departureDay = _departures = 0;
        }

        public string ExpeditionLockReason(string mapId) => mapId switch
        {
            "archive_ruins" when Quests.GetQuest("main_2")?.Status != QuestStatus.Completed => "建立补给路线后开放",
            "broadcast_tower" when Quests.GetQuest("main_3")?.Status != QuestStatus.Completed => "带回旧城档案后开放",
            "broadcast_tower" when _archiveReturnDay == Time.CurrentDay => "旧城档案正在解码，次日开放",
            _ => ""
        };

        internal bool PayForDeparture(string mapId)
        {
            if (IsGameOver || ExpeditionLockReason(mapId).Length > 0 || DeparturesLeft <= 0 || !CanAffordDeparture)
                return false;
            Resources.ConsumeResource(ResourceType.Food, DepartureFood);
            Resources.ConsumeResource(ResourceType.Water, DepartureWater);
            if (_departureDay != Time.CurrentDay)
            {
                _departureDay = Time.CurrentDay;
                _departures = 0;
            }
            _departures++;
            return true;
        }

        public SupplyRoute PlannedSupplyRoute { get; private set; }
        public bool SupplyDecisionOpen => Route == SupplyRoute.Undecided &&
            Quests.GetQuest("main_2").Status == QuestStatus.Active;

        public bool SupplyRouteReady(SupplyRoute route) => route switch
        {
            SupplyRoute.Homestead => Buildings.GetBuilding("greenhouse").Built && Buildings.GetBuilding("water_station").Built,
            SupplyRoute.Caravan => Buildings.GetBuilding("market").Built && Resources.HasEnough(ResourceType.Parts, CaravanDeposit),
            _ => false
        };

        public bool PlanSupplyRoute(SupplyRoute route)
        {
            if (!CanSave || IsGameOver || !SupplyDecisionOpen || !SupplyRouteReady(route)) return false;
            PlannedSupplyRoute = route;
            return true;
        }

        private void ActivateSupplyRoute()
        {
            if (!SupplyDecisionOpen) return;
            var route = PlannedSupplyRoute;
            if (route == SupplyRoute.Undecided)
            {
                bool homestead = SupplyRouteReady(SupplyRoute.Homestead);
                bool caravan = SupplyRouteReady(SupplyRoute.Caravan);
                if (homestead == caravan) return;
                route = homestead ? SupplyRoute.Homestead : SupplyRoute.Caravan;
            }
            if (!SupplyRouteReady(route)) return;
            if (route == SupplyRoute.Caravan) Resources.ConsumeResource(ResourceType.Parts, CaravanDeposit);
            SetSupplyRoute(route);
            PlannedSupplyRoute = SupplyRoute.Undecided;
            ReconcileQuest("main_2");
        }

        private void SetSupplyRoute(SupplyRoute route)
        {
            Route = route;
            if (route != SupplyRoute.Homestead) return;
            Buildings.GetBuilding("greenhouse").DailyOutput.Add(ResourceType.Food, 3);
            Buildings.GetBuilding("water_station").DailyOutput.Add(ResourceType.Water, 3);
        }

        internal void RecordReturnedEvidence(ExplorationResult result)
        {
            if (result.ArchiveEvidence && !ArchiveRecovered)
            {
                ArchiveRecovered = true;
                _archiveReturnDay = Time.CurrentDay;
                ReconcileQuest("main_3");
            }
            if (result.TowerEvidence)
            {
                TowerCalibrated = true;
                ReconcileQuest("main_4");
            }
        }

        public bool CanInstallRelay => CanSave && !IsGameOver && TowerCalibrated && !RelayInstalled &&
            Quests.GetQuest("main_4").Status == QuestStatus.Active;
        public bool CanAffordRelay => Resources.HasEnough(ResourceType.Parts, RelayPartsCost) &&
            Resources.HasEnough(ResourceType.Power, RelayPowerCost);

        public bool InstallRelay()
        {
            if (!CanInstallRelay || !CanAffordRelay) return false;
            Resources.ConsumeResource(ResourceType.Parts, RelayPartsCost);
            Resources.ConsumeResource(ResourceType.Power, RelayPowerCost);
            RelayInstalled = true;
            return true;
        }

        public bool RelayAwaitingTest => RelayInstalled && !_relayCommissioned && !IsGameOver;

        private string RelayTestProblem(int availablePower, int waterShortfall, int criticalResidents)
        {
            var blockers = new List<string>();
            if (availablePower < RelayTestPower) blockers.Add($"试送电缺 {RelayTestPower - availablePower} 电力");
            if (Scenario == CampaignScenario.WaterShortage && waterShortfall > 0)
                blockers.Add($"配水缺 {waterShortfall}，优先为营地供水");
            if (Scenario == CampaignScenario.MedicalShortage && criticalResidents > 0)
                blockers.Add($"{criticalResidents} 位重伤员需要先救治");
            return string.Join("；", blockers);
        }

        public string RelayTestForecast
        {
            get
            {
                if (!RelayAwaitingTest) return "";
                var forecast = Time.GetSupplyForecast();
                return RelayTestProblem(forecast.First(item => item.Type == ResourceType.Power).Reserve,
                    forecast.First(item => item.Type == ResourceType.Water).Shortfall,
                    GetCareForecast().Count(patient => patient.AfterCare < CriticalHealth));
            }
        }

        private string CommissionRelay()
        {
            if (!RelayAwaitingTest) return null;
            string problem = RelayTestProblem(Resources.GetResourceAmount(ResourceType.Power), Time.LastWaterShortfall, CriticalResidents);
            if (problem.Length > 0) return $"广播试送电暂缓：{problem}。";
            Resources.ConsumeResource(ResourceType.Power, RelayTestPower);
            _relayCommissioned = true;
            ReconcileQuest("main_4");
            return "广播组件通过试送电。档案城已能决定记忆的去向。";
        }

        private int MilestoneProgress(string id) => id switch
        {
            SupplyMilestone => Route != SupplyRoute.Undecided ? 1 : 0,
            ArchiveMilestone => ArchiveRecovered ? 1 : 0,
            TowerMilestone => TowerCalibrated ? 1 : 0,
            RelayMilestone => _relayCommissioned ? 1 : 0,
            _ => 0
        };

        public ExplorationEvent SearchExpeditionRoom()
        {
            if (Combat.InCombat) return null;
            var result = Exploration.SearchRoom();
            if (result == null) return null;
            if (result.Type == ExplorationEventType.EnemyEncounter &&
                !Combat.StartCombat(Exploration.TeamNPCIds, result.EnemyIds))
                Exploration.ResolveEncounter(false);
            SyncResourceObjectives();
            return result;
        }

        internal void SyncResourceObjectives()
        {
            foreach (var type in Enum.GetValues<ResourceType>())
                Quests.UpdateResourceObjectives(type, Resources.GetResourceAmount(type));
        }

        private void CaptureCampaign(SaveData data)
        {
            data.SupplyRoute = Route;
            data.PlannedSupplyRoute = PlannedSupplyRoute;
            data.Scenario = Scenario;
            CaptureRoster(data);
            data.ArchiveRecovered = ArchiveRecovered;
            data.TowerCalibrated = TowerCalibrated;
            data.RelayInstalled = RelayInstalled;
            data.RelayCommissioned = _relayCommissioned;
            data.ArchiveReturnDay = _archiveReturnDay;
            data.DepartureDay = _departureDay;
            data.Departures = _departures;
        }

        private void RestoreCampaign(SaveData data)
        {
            var archiveFinished = Quests.GetQuest("main_3").Status == QuestStatus.Completed;
            var towerFinished = Quests.GetQuest("main_4").Status == QuestStatus.Completed;
            var previousRoute = data.SupplyRoute ??
                (Quests.GetQuest("main_2").Status == QuestStatus.Completed ? SupplyRoute.Homestead : SupplyRoute.Undecided);
            SetSupplyRoute(previousRoute);
            PlannedSupplyRoute = Route == SupplyRoute.Undecided ? data.PlannedSupplyRoute : SupplyRoute.Undecided;
            ArchiveRecovered = data.ArchiveRecovered || archiveFinished || towerFinished;
            TowerCalibrated = data.TowerCalibrated || towerFinished;
            RelayInstalled = data.RelayInstalled || towerFinished;
            _relayCommissioned = data.RelayCommissioned || towerFinished;
            _archiveReturnDay = data.ArchiveReturnDay;
            _departureDay = data.DepartureDay;
            _departures = Math.Clamp(data.Departures, 0, DeparturesPerDay);
            RestoreRoster(data);
            // Recompute changed active objectives. Completed chapters are preserved.
            foreach (var quest in Quests.GetActiveQuests())
                if (quest.Type == QuestType.MainQuest || !data.LastSettledDay.HasValue)
                    ReconcileQuest(quest.QuestId);
        }
    }
}
