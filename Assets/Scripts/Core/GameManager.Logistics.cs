using System;
using System.Collections.Generic;
using System.Linq;

namespace LastArchive
{
    public enum CampaignScenario { Standard, WaterShortage, MedicalShortage }
    public enum CampDuty { None, Pump, Triage }

    public partial class GameManager
    {
        public const int EmergencyWater = 16;
        public const int PumpPower = 3;
        public const int TriageMedicine = 1;
        public const int TriageHeal = 24;
        public const int TriagePatients = 2;
        public const int CriticalHealth = 30;
        public const int PumpSkill = 8;
        public const int TriageSkill = 7;

        private readonly HashSet<string> _fieldedToday = new(StringComparer.Ordinal);
        private int _rosterDay;
        private CampDuty _duty;
        private string _dutyWorker;
        private string _campReport;

        public CampDuty TodayDuty => _rosterDay == Time.CurrentDay ? _duty : CampDuty.None;
        public string TodayDutyWorker => TodayDuty != CampDuty.None ? _dutyWorker : null;
        public int CriticalResidents => NPCs.GetAllNPCs().Count(npc => npc.IsAlive && npc.Health < CriticalHealth);

        private void PrepareRoster()
        {
            if (_rosterDay == Time.CurrentDay) return;
            ClearRoster();
            _rosterDay = Time.CurrentDay;
        }

        private void ClearRoster()
        {
            _rosterDay = 0;
            _duty = CampDuty.None;
            _dutyWorker = null;
            _campReport = null;
            _fieldedToday.Clear();
        }

        internal void RecordDepartedTeam(IEnumerable<string> team)
        {
            PrepareRoster();
            _fieldedToday.UnionWith(team);
        }

        public bool CanExploreResident(NPCInstance resident) => resident?.CanExplore == true &&
            resident.Id != TodayDutyWorker && Psychology.CanExplore(resident.Id);

        private bool FieldedToday(string id) => _rosterDay == Time.CurrentDay && _fieldedToday.Contains(id);

        public bool OffersDuty(CampDuty duty) => duty switch
        {
            CampDuty.Pump => Scenario == CampaignScenario.WaterShortage,
            CampDuty.Triage => Scenario == CampaignScenario.MedicalShortage || Buildings.GetBuilding("med_room").Built,
            _ => false
        };

        public bool AffordsDuty(CampDuty duty) => duty switch
        {
            CampDuty.Pump => Resources.HasEnough(ResourceType.Power, PumpPower),
            CampDuty.Triage => Resources.HasEnough(ResourceType.Medicine, TriageMedicine),
            _ => false
        };

        internal static bool HasDutySkill(CampDuty duty, NPCInstance resident) => duty switch
            {
                CampDuty.Pump => resident?.Engineering >= PumpSkill,
                CampDuty.Triage => resident?.Medical >= TriageSkill,
                _ => false
            };

        public bool CanStaffDuty(CampDuty duty, NPCInstance resident) =>
            resident is { CanWork: true, Status: NPCStatus.Idle } && !FieldedToday(resident.Id) && HasDutySkill(duty, resident);

        public bool AssignCampDuty(CampDuty duty, string residentId)
        {
            if (!CanSave || IsGameOver || TodayDuty != CampDuty.None || !OffersDuty(duty) || !AffordsDuty(duty) ||
                string.IsNullOrEmpty(residentId) || !CanStaffDuty(duty, NPCs.GetNPC(residentId))) return false;
            PrepareRoster();
            if (duty == CampDuty.Pump) Resources.ConsumeResource(ResourceType.Power, PumpPower);
            else Resources.ConsumeResource(ResourceType.Medicine, TriageMedicine);
            _duty = duty;
            _dutyWorker = residentId;
            return true;
        }

        private IEnumerable<NPCInstance> TriageTargets() => NPCs.GetAllNPCs()
            .Where(npc => npc.IsAlive && npc.Health < GameConfig.MaxNPCHealth)
            .OrderBy(npc => npc.Health).Take(TriagePatients);

        public bool DutyCanRun => TodayDutyWorker is { } id && NPCs.GetNPC(id) is { CanWork: true };
        private int PlannedWater() => TodayDuty == CampDuty.Pump && DutyCanRun ? EmergencyWater : 0;

        public sealed record CareForecast(string Id, string Name, int Health, int AfterCare, bool ReceivesTriage);

        public IReadOnlyList<CareForecast> GetCareForecast()
        {
            var patients = TodayDuty == CampDuty.Triage && DutyCanRun ? TriageTargets().Select(npc => npc.Id).ToHashSet() : new();
            return NPCs.GetAllNPCs().Where(npc => npc.IsAlive && (npc.Health < CriticalHealth || patients.Contains(npc.Id)))
                .OrderBy(npc => npc.Health).Select(npc =>
                {
                    bool treated = patients.Contains(npc.Id);
                    int hp = Math.Min(GameConfig.MaxNPCHealth, npc.Health + (treated ? TriageHeal : 0));
                    if (npc.Status == NPCStatus.Injured && hp < CriticalHealth) hp += 2 + Time.ClinicRecovery;
                    return new CareForecast(npc.Id, npc.Name, npc.Health, hp, treated);
                }).ToArray();
        }

        private void SettleCampDuty()
        {
            _campReport = null;
            if (TodayDuty == CampDuty.None) return;
            string workerName = NPCs.GetNPC(TodayDutyWorker).Name;
            if (!DutyCanRun)
            {
                _campReport = $"{workerName} 无法完成今天的值班。";
                return;
            }
            if (TodayDuty == CampDuty.Pump)
            {
                Resources.AddResource(ResourceType.Water, EmergencyWater);
                _campReport = $"{workerName} 完成净水值班，供水 +{EmergencyWater}。";
            }
            else if (TodayDuty == CampDuty.Triage)
            {
                var patients = TriageTargets().ToArray();
                foreach (var patient in patients) NPCs.HealNPC(patient.Id, TriageHeal);
                _campReport = patients.Length == 0 ? "医疗值班结束，营地没有伤员。" :
                    $"{workerName} 治疗了 {string.Join('、', patients.Select(npc => npc.Name))}，每人最多恢复 {TriageHeal} 生命。";
            }
        }

        private void CaptureRoster(SaveData data)
        {
            if (_rosterDay != Time.CurrentDay) return;
            data.RosterDay = _rosterDay;
            data.FieldedResidents = _fieldedToday.ToList();
            data.CampDuty = TodayDuty;
            data.DutyWorker = TodayDutyWorker;
        }

        private void RestoreRoster(SaveData data)
        {
            if (data.RosterDay != Time.CurrentDay) return;
            PrepareRoster();
            _fieldedToday.UnionWith((data.FieldedResidents ?? new()).Where(id => !string.IsNullOrEmpty(id) && NPCs.GetNPC(id) != null));
            // Preserve the prepaid shift even if the resident fell ill or died after being assigned.
            if (OffersDuty(data.CampDuty) && !string.IsNullOrEmpty(data.DutyWorker) && !FieldedToday(data.DutyWorker) &&
                HasDutySkill(data.CampDuty, NPCs.GetNPC(data.DutyWorker)))
            {
                _duty = data.CampDuty;
                _dutyWorker = data.DutyWorker;
            }
        }

        public sealed record Opening(CampaignScenario Scenario, string Title, string Description);

        public static IReadOnlyList<Opening> Openings { get; } = Array.AsReadOnly(new[]
        {
            new Opening(CampaignScenario.Standard, "常规开局", "储备均衡，适合第一次接手档案城。"),
            new Opening(CampaignScenario.WaterShortage, "净水故障", "32 饮水、8 电力。每日多用 4 饮水冲洗管道，配水不足时暂停广播试送电。"),
            new Opening(CampaignScenario.MedicalShortage, "药品告急", "开局只有 1 药品，铁柱与莉莉重伤。日终仍有生命低于 30 的居民时，广播试送电暂缓。")
        });

        public CampaignScenario Scenario { get; private set; }

        private void ApplyOpening(CampaignScenario scenario)
        {
            Scenario = scenario;
            Time.WaterFlush = scenario == CampaignScenario.WaterShortage ? 4 : 0;
            if (scenario == CampaignScenario.WaterShortage)
            {
                Resources.ConsumeResource(ResourceType.Water, GameConstants.InitialWater - 32);
                Resources.ConsumeResource(ResourceType.Power, GameConstants.InitialPower - 8);
            }
            else if (scenario == CampaignScenario.MedicalShortage)
            {
                Resources.ConsumeResource(ResourceType.Medicine, GameConstants.InitialMedicine - 1);
                NPCs.InjureNPC("lin_doctor", 32);
                NPCs.InjureNPC("iron_guard", NPCs.GetNPC("iron_guard").Health - 25);
                NPCs.InjureNPC("big_hu", 35);
                NPCs.InjureNPC("lily_nurse", NPCs.GetNPC("lily_nurse").Health - 19);
            }
        }
    }
}
