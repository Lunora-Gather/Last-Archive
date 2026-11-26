// ============================================================
// Last Archive - 控制台Demo (可运行的文字版游戏)
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LastArchive
{
    /// <summary>
    /// 控制台UI - 文字按钮版Demo，系统闭环可运行
    /// </summary>
    public class ConsoleGame
    {
        private GameManager _game;
        private bool _running = true;

        public void Run()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "最后档案城 - Last Archive";

            ShowMainMenu();

            while (_running)
            {
                if (_game != null && _game.IsInitialized && !_game.IsGameOver)
                {
                    ShowCurrentPhase();
                }
                else if (_game != null && _game.IsGameOver)
                {
                    ShowGameOver();
                }
                else
                {
                    ShowMainMenu();
                }
            }
        }

        // ========== 主菜单 ==========

        private void ShowMainMenu()
        {
            Console.Clear();
            PrintBanner();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════╗");
            Console.WriteLine("  ║     最 后 档 案 城            ║");
            Console.WriteLine("  ║     Last Archive              ║");
            Console.WriteLine("  ╠══════════════════════════════╣");
            Console.WriteLine("  ║  1. 新游戏                    ║");
            Console.WriteLine("  ║  2. 继续游戏                  ║");
            Console.WriteLine("  ║  3. 退出                      ║");
            Console.WriteLine("  ║  4. 选择战役开局              ║");
            Console.WriteLine("  ╚══════════════════════════════╝");
            Console.WriteLine();

            var save = new SaveSystem();
            if (!save.HasSave())
            {
                Console.WriteLine("  (没有存档)");
            }

            Console.Write("  选择: ");
            var input = Console.ReadLine()?.Trim();

            switch (input)
            {
                case "1":
                case "4":
                    var scenario = CampaignScenario.Standard;
                    if (input == "4")
                    {
                        var openings = GameManager.Openings;
                        for (int i = 0; i < openings.Count; i++)
                            Console.WriteLine($"  {i + 1}. {openings[i].Title} · {openings[i].Description}");
                        Console.Write("  选择开局 (0=返回): ");
                        if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 1 || choice > openings.Count) return;
                        scenario = openings[choice - 1].Scenario;
                    }
                    _game = new GameManager();
                    var ai = AIProviderFactory.Interactive();
                    _game.Initialize(ai);
                    _game.StartNewGame(scenario);
                    Console.WriteLine("\n  新游戏开始！欢迎来到档案城。");
                    Console.WriteLine("  按任意键继续...");
                    Console.ReadKey();
                    break;
                case "2":
                    _game = new GameManager();
                    _game.Initialize(new MockAIProvider());
                    _game.StartNewGame(); // 先初始化数据
                    if (_game.LoadGame())
                    {
                        Console.WriteLine("\n  存档加载成功！");
                    }
                    else
                    {
                        Console.WriteLine("\n  没有存档或加载失败。");
                        _game = null;
                    }
                    Console.WriteLine("  按任意键继续...");
                    Console.ReadKey();
                    break;
                case "3":
                    _running = false;
                    break;
            }
        }

        // ========== 游戏阶段 ==========

        private void ShowCurrentPhase()
        {
            if (_game.Combat.InCombat) { RunCombatLoop(); return; }
            if (_game.Exploration.IsExploring) { ShowExplorationMenu(); return; }
            switch (_game.Time.CurrentPhase)
            {
                case GamePhase.Day:
                    ShowDayPhase();
                    break;
                case GamePhase.Night:
                    ShowNightPhase();
                    break;
                case GamePhase.Summary:
                    ShowSummaryPhase();
                    break;
            }
        }

        // ========== 白天阶段 ==========

        private void ShowDayPhase()
        {
            Console.Clear();
            PrintHeader("白 天 - 基 地 经 营");

            ShowCampStatus();
            var active = _game.Quests.GetActiveQuests().Find(quest => quest.Type == QuestType.MainQuest);
            if (active != null) Console.WriteLine($"  当前主线: {active.Title} · {active.Description}");
            if (_game.MarketCapacity > 0) Console.WriteLine($"  M. 集市交换 · 今日剩余 {_game.MarketTradesLeft} 次");
            if (_game.CanChooseEnding) Console.WriteLine("  E. 决定记忆的去向");
            Console.WriteLine();

            Console.WriteLine("  ╔══════════════════════════════╗");
            Console.WriteLine("  ║  1. 查看居民                  ║");
            Console.WriteLine("  ║  2. 管理建筑                  ║");
            Console.WriteLine("  ║  3. 查看任务                  ║");
            Console.WriteLine("  ║  4. 与NPC对话                 ║");
            Console.WriteLine("  ║  5. 分配工作                  ║");
            Console.WriteLine("  ║  6. 背包物品                  ║");
            Console.WriteLine("  ║  7. 派系声望                  ║");
            Console.WriteLine("  ║  8. 成就                      ║");
            Console.WriteLine("  ║  9. 危机/心理                  ║");
            Console.WriteLine("  ║  X. 选择探索地点              ║");
            Console.WriteLine("  ║  S. 保存游戏                  ║");
            Console.WriteLine("  ║  0. 结束白天，进入夜晚        ║");
            Console.WriteLine("  ╚══════════════════════════════╝");
            Console.Write("  选择: ");

            var input = Console.ReadLine()?.Trim();
            if (HandleCampChoice(input)) return;
            switch (input)
            {
                case "1": ShowNPCList(); break;
                case "2": ShowBuildingMenu(); break;
                case "3": ShowQuestList(); break;
                case "4": ShowDialogueMenu(); break;
                case "5": ShowWorkAssignment(); break;
                case "6": ShowInventory(); break;
                case "7": ShowFactions(); break;
                case "8": ShowAchievements(); break;
                case "9": ShowCrisisAndPsychology(); break;
                case "X":
                case "x": ShowExplorationSelect(); break;
                case "S":
                case "s":
                    if (_game.SaveGame()) Console.WriteLine("  保存成功！");
                    else Console.WriteLine("  保存失败！");
                    Pause();
                    break;
                case "M":
                case "m": ShowMarket(); break;
                case "E":
                case "e": ShowEndingChoice(); break;
                case "0": _game.Time.AdvanceToNight(); break;
            }
        }

        private void ShowNightPhase()
        {
            Console.Clear();
            PrintHeader("夜 晚 - 远 征");
            ShowCampStatus();
            Console.WriteLine();

            Console.WriteLine("  ╔══════════════════════════════╗");
            Console.WriteLine("  ║  1. 选择探索地点              ║");
            Console.WriteLine("  ║  2. 结束夜晚，进入结算        ║");
            Console.WriteLine("  ╚══════════════════════════════╝");
            Console.Write("  选择: ");

            var nightInput = Console.ReadLine()?.Trim();
            if (HandleCampChoice(nightInput)) return;
            switch (nightInput)
            {
                case "1": ShowExplorationSelect(); break;
                case "2": _game.Time.AdvanceToSummary(); break;
            }
        }

        private void ShowCampStatus()
        {
            ShowResources();
            var supply = _game.Time.GetSupplyForecast().Where(item => item.Used > 0);
            Console.WriteLine("  结算预计: " + string.Join(" · ", supply.Select(item =>
                $"{GameConfig.ResourceNames[(int)item.Type]}{(item.Shortfall > 0 ? $"短缺 {item.Shortfall}" : $"余 {item.Reserve}")}")));
            Console.WriteLine("  " + CampaignUI.CampStatus(_game));
            foreach (var patient in _game.GetCareForecast()) Console.WriteLine("  " + CampaignUI.CareStatus(patient));
            Console.WriteLine("  " + CampaignUI.SupplyStatus(_game));
            if (_game.SupplyDecisionOpen)
            {
                Console.WriteLine("  H. " + CampaignUI.RouteOption(_game, SupplyRoute.Homestead));
                Console.WriteLine("  C. " + CampaignUI.RouteOption(_game, SupplyRoute.Caravan));
            }
            if (_game.TodayDuty != CampDuty.None)
                Console.WriteLine("  " + CampaignUI.AssignedDuty(_game));
            else
                foreach (var duty in CampaignUI.Duties.Where(_game.OffersDuty))
                    Console.WriteLine($"  {(duty == CampDuty.Pump ? "P" : "T")}. {CampaignUI.DutyOption(_game, duty)}");
            if (_game.CanInstallRelay) Console.WriteLine("  R. " + CampaignUI.InstallOption(_game));
            if (_game.RelayAwaitingTest) Console.WriteLine("  " + CampaignUI.RelayForecast(_game));
        }

        private bool HandleCampChoice(string input)
        {
            switch (input?.ToUpperInvariant())
            {
                case "H" or "C" when _game.SupplyDecisionOpen:
                    var route = input.Equals("H", StringComparison.OrdinalIgnoreCase) ? SupplyRoute.Homestead : SupplyRoute.Caravan;
                    Console.WriteLine(_game.PlanSupplyRoute(route) ? CampaignUI.SupplyStatus(_game) : CampaignUI.RouteOption(_game, route));
                    break;
                case "P" or "T" when _game.TodayDuty == CampDuty.None:
                    var duty = input.Equals("P", StringComparison.OrdinalIgnoreCase) ? CampDuty.Pump : CampDuty.Triage;
                    if (!_game.OffersDuty(duty)) return false;
                    var workers = CampaignUI.DutyCandidates(_game, duty);
                    if (!_game.AffordsDuty(duty) || workers.Count == 0)
                    {
                        Console.WriteLine(CampaignUI.DutyOption(_game, duty));
                        break;
                    }
                    for (int i = 0; i < workers.Count; i++)
                        Console.WriteLine($"  {i + 1}. {workers[i].Name}");
                    Console.Write("  选择值班居民（当天不能远征，0=返回）: ");
                    if (!int.TryParse(Console.ReadLine(), out int worker) || worker < 1 || worker > workers.Count) return true;
                    Console.WriteLine(_game.AssignCampDuty(duty, workers[worker - 1].Id)
                        ? CampaignUI.AssignedDuty(_game) : "未能排班，请检查物资和居民状态。");
                    break;
                case "R" when _game.CanInstallRelay:
                    Console.WriteLine(_game.InstallRelay() ? CampaignUI.RelayForecast(_game) : CampaignUI.InstallOption(_game));
                    break;
                default: return false;
            }
            Pause();
            return true;
        }

        // ========== 结算阶段 ==========

        private void ShowSummaryPhase()
        {
            Console.Clear();
            PrintHeader("每 日 结 算");

            Console.WriteLine("  === 资源变化 ===");
            ShowResources();
            Console.WriteLine();

            Console.WriteLine("  === 居民状态 ===");
            foreach (var npc in _game.NPCs.GetAllNPCs())
            {
                if (!npc.IsAlive) continue;
                Console.WriteLine($"  {npc.Name}: HP={npc.Health} 士气={npc.Morale} 饥饿={npc.Hunger} 疲劳={npc.Fatigue}");
                if (!string.IsNullOrEmpty(npc.Memory.Summary))
                {
                    Console.WriteLine($"    记忆: {npc.Memory.Summary}");
                }
            }
            Console.WriteLine();

            // 检查游戏结束
            _game.OnDayEnd();

            if (!_game.IsGameOver)
            {
                Console.WriteLine("  按任意键进入新的一天...");
                Console.ReadKey();
                _game.AdvanceDay();
            }
        }

        // ========== 探索选择 ==========

        private void ShowExplorationSelect()
        {
            Console.Clear();
            PrintHeader("选 择 探 索 地 点");

            var maps = _game.Exploration.GetAllMaps();
            Console.WriteLine($"  今日剩余出发 {_game.DeparturesLeft} 次 · 每次 {_game.DepartureFood} 食物 / {_game.DepartureWater} 饮水");
            var available = _game.NPCs.GetAllNPCs().Where(_game.CanExploreResident).ToList();
            var departureIssue = CampaignUI.DepartureIssue(_game, available.Count);
            if (departureIssue.Length > 0)
            {
                Console.WriteLine("  " + departureIssue);
                Pause();
                return;
            }
            for (int i = 0; i < maps.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {maps[i].Name}");
                Console.WriteLine($"     {maps[i].Description}");
                string locked = _game.ExpeditionLockReason(maps[i].Id);
                if (locked.Length > 0) Console.WriteLine($"     {locked}");
                Console.WriteLine($"     主要战利品: {string.Join(", ", maps[i].MainLoot)}");
                Console.WriteLine();
            }

            Console.Write("  选择地图 (0=返回): ");
            var input = Console.ReadLine()?.Trim();
            if (input == "0") return;

            if (int.TryParse(input, out int mapIdx) && mapIdx >= 1 && mapIdx <= maps.Count)
            {
                var map = maps[mapIdx - 1];
                string locked = _game.ExpeditionLockReason(map.Id);
                if (locked.Length > 0) { Console.WriteLine("  " + locked); Pause(); return; }

                // 选择队伍
                Console.WriteLine($"\n  选择探索队伍 (最多{GameConfig.MaxExplorationTeamSize}人):");
                for (int i = 0; i < available.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {available[i].Name} (工程:{available[i].Engineering} 战斗:{available[i].Combat} 搜集:{available[i].Scavenging} 疲劳:{available[i].Fatigue})");
                }

                Console.Write("  输入队员编号 (用逗号分隔): ");
                var teamInput = Console.ReadLine()?.Trim();
                var teamIds = new List<string>();

                if (!string.IsNullOrEmpty(teamInput))
                {
                    var parts = teamInput.Split(',');
                    foreach (var p in parts)
                    {
                        if (int.TryParse(p.Trim(), out int idx) && idx >= 1 && idx <= available.Count)
                        {
                            if (!teamIds.Contains(available[idx - 1].Id))
                            {
                                teamIds.Add(available[idx - 1].Id);
                            }
                        }
                    }
                }

                if (teamIds.Count == 0 || teamIds.Count > GameConfig.MaxExplorationTeamSize)
                {
                    Console.WriteLine($"  请选择 1 至 {GameConfig.MaxExplorationTeamSize} 名队员。");
                    Pause();
                    return;
                }

                if (_game.Exploration.StartExploration(map.Id, teamIds))
                {
                    Console.WriteLine($"\n  探索队出发！前往{map.Name}...");
                    // 更新访问地点目标
                    Pause();
                }
                else
                {
                    Console.WriteLine("  无法开始探索！");
                    Pause();
                }
            }
        }

        // ========== 探索菜单 ==========

        private void ShowExplorationMenu()
        {
            Console.Clear();
            PrintHeader("探 索 中");

            Console.WriteLine($"  地图: {_game.Exploration.CurrentMap.Name}");
            Console.WriteLine($"  当前房间: {_game.Exploration.CurrentRoom.Name}");
            Console.WriteLine($"  {_game.Exploration.CurrentRoom.Description}");
            Console.WriteLine($"  危险等级: {_game.Exploration.CurrentRoom.DangerLevel}/10");
            Console.WriteLine();

            ShowResources();
            Console.WriteLine();

            Console.WriteLine("  ╔══════════════════════════════╗");
            var searchIssue = CampaignUI.SearchIssue(_game.Exploration);
            Console.WriteLine($"  1. 搜索当前房间  {searchIssue}");
            Console.WriteLine("  ║  2. 移动到其他房间            ║");
            Console.WriteLine("  ║  3. 返回基地                  ║");
            if (_game.Exploration.CanOfferTowerBypass)
                Console.WriteLine($"  4. 现场接线（{ExplorationSystem.BypassRequirements}） {_game.Exploration.TowerBypassIssue}");
            if (_game.Exploration.ActiveCheck != null) Console.WriteLine("  5. 指派队员执行检定");
            Console.WriteLine("  ╚══════════════════════════════╝");
            Console.Write("  选择: ");

            var input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1":
                    if (searchIssue.Length > 0) { Console.WriteLine(searchIssue); Pause(); break; }
                    var evt = _game.SearchExpeditionRoom();
                    if (evt != null)
                    {
                        Console.WriteLine($"\n  {evt.Description}");
                        if (evt.Rewards.Count > 0)
                        {
                            Console.Write("  获得: ");
                            foreach (var r in evt.Rewards)
                            {
                                Console.Write($"{GameConfig.ResourceNames[(int)r.Type]}x{r.Amount} ");
                            }
                            Console.WriteLine();
                        }

                        if (_game.Combat.InCombat)
                        {
                            Console.WriteLine("\n  遭遇敌人！准备战斗！");
                            Pause();

                            RunCombatLoop();
                        }
                    }
                    Pause();
                    break;

                case "2":
                    var connected = _game.Exploration.GetConnectedRooms();
                    if (connected.Count == 0)
                    {
                        Console.WriteLine("  没有可前往的房间。");
                    }
                    else
                    {
                        Console.WriteLine("  可前往的房间:");
                        for (int i = 0; i < connected.Count; i++)
                        {
                            string visited = connected[i].Visited ? "(已访问)" : "(未访问)";
                            Console.WriteLine($"  {i + 1}. {connected[i].Name} {visited} [危险:{connected[i].DangerLevel}] {CampaignUI.EntryIssue(_game, connected[i])}");
                        }
                        Console.Write("  选择: ");
                        var roomInput = Console.ReadLine()?.Trim();
                        if (int.TryParse(roomInput, out int roomIdx) && roomIdx >= 1 && roomIdx <= connected.Count)
                        {
                            if (_game.Exploration.MoveToRoom(connected[roomIdx - 1].Id))
                            {
                                Console.WriteLine($"  移动到 {_game.Exploration.CurrentRoom.Name}");
                            }
                            else
                            {
                                Console.WriteLine("  " + CampaignUI.EntryIssue(_game, connected[roomIdx - 1]));
                            }
                        }
                    }
                    Pause();
                    break;

                case "3":
                    var result = _game.Exploration.ReturnToBase();
                    if (result != null)
                    {
                        Console.WriteLine("\n  === 探索结算 ===");
                        Console.WriteLine($"  收集资源: {result.CollectedResources.Count}项");
                        foreach (var r in result.CollectedResources)
                        {
                            Console.WriteLine($"    {GameConfig.ResourceNames[(int)r.Type]}x{r.Amount}");
                        }
                        if (result.InjuredNPCs.Count > 0)
                        {
                            Console.WriteLine($"  受伤NPC: {string.Join(", ", result.InjuredNPCs)}");
                        }
                    }
                    Pause();
                    break;
                case "4":
                    Console.WriteLine(_game.Exploration.BypassTower() ? "  已取得校准数据，返回基地后可安装组件。" : _game.Exploration.TowerBypassIssue);
                    Pause();
                    break;
                case "5":
                    var check = _game.Exploration.ActiveCheck;
                    if (check == null) break;
                    Console.WriteLine(check.Description);
                    var crew = _game.Exploration.TeamNPCIds.Select(_game.NPCs.GetNPC).Where(n => n.IsAlive).ToList();
                    for (int index = 0; index < crew.Count; index++)
                        Console.WriteLine($"  {index + 1}. {crew[index].Name} · 工程 {crew[index].Engineering} / 医疗 {crew[index].Medical} / 战斗 {crew[index].Combat}");
                    Console.Write("  输入队员编号: ");
                    if (int.TryParse(Console.ReadLine(), out int member) && member >= 1 && member <= crew.Count &&
                        _game.Exploration.ResolveSkillCheck(crew[member - 1].Id, out string outcome, out _))
                        Console.WriteLine(outcome);
                    Pause();
                    break;
            }
        }

        // ========== 战斗循环 ==========

        private void RunCombatLoop()
        {
            while (_game.Combat.InCombat)
            {
                ShowCombatMenu();

                Console.Write("  选择行动: ");
                var input = Console.ReadLine()?.Trim();
                CombatAction action = CombatAction.Attack;

                switch (input)
                {
                    case "1": action = CombatAction.Attack; break;
                    case "2": action = CombatAction.Defend; break;
                    case "3": action = CombatAction.UseMedicine; break;
                    case "4": action = CombatAction.Escape; break;
                    case "5": action = CombatAction.UseSkill; break;
                    default: continue;
                }

                int targetIndex = 0;
                var enemies = _game.Combat.EnemyUnits.Where(enemy => enemy.IsAlive).ToList();
                if (enemies.Count > 1 && action is CombatAction.Attack or CombatAction.UseSkill)
                {
                    for (int index = 0; index < enemies.Count; index++)
                        Console.WriteLine($"  {index + 1}. {enemies[index].Name}");
                    Console.Write("  选择目标（回车默认 1）: ");
                    if (int.TryParse(Console.ReadLine(), out int selected) && selected >= 1 && selected <= enemies.Count)
                        targetIndex = selected - 1;
                }
                var result = _game.Combat.ExecutePlayerAction(action, targetIndex);
                if (result != null)
                {
                    Console.WriteLine("\n  === 战斗结束 ===");
                    if (result.Victory)
                    {
                        Console.WriteLine("  胜利！");
                        if (result.Rewards.Count > 0)
                        {
                            Console.Write("  获得: ");
                            foreach (var r in result.Rewards)
                            {
                                Console.Write($"{GameConfig.ResourceNames[(int)r.Type]}x{r.Amount} ");
                            }
                            Console.WriteLine();
                        }
                        // 更新击败敌人目标
                        _game.Quests.UpdateObjectiveProgress("", ObjectiveType.DefeatEnemy, "", 1);
                    }
                    else if (result.Escaped)
                    {
                        Console.WriteLine("  成功逃脱！");
                    }
                    else
                    {
                        Console.WriteLine("  战斗失败...");
                    }

                    if (result.InjuredNPCs.Count > 0)
                    {
                        Console.WriteLine($"  受伤: {string.Join(", ", result.InjuredNPCs)}");
                    }

                    // 更新资源
                    foreach (ResourceType rt in Enum.GetValues(typeof(ResourceType)))
                    {
                        _game.Quests.UpdateResourceObjectives(rt, _game.Resources.GetResourceAmount(rt));
                    }

                    Pause();
                }
            }
        }

        // ========== 战斗菜单 ==========

        private void ShowCombatMenu()
        {
            Console.Clear();
            PrintHeader("战 斗");

            Console.WriteLine(_game.Combat.GetStatusText());
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════╗");
            Console.WriteLine("  ║  1. 攻击                      ║");
            Console.WriteLine("  ║  2. 防御                      ║");
            Console.WriteLine("  ║  3. 使用药品                  ║");
            Console.WriteLine("  ║  4. 逃跑                      ║");
            Console.WriteLine("  ║  5. 使用职业技能              ║");
            Console.WriteLine("  ╚══════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  [战斗日志]");
            var lines = _game.Combat.CombatLog.ToString().Split('\n');
            int start = Math.Max(0, lines.Length - 6);
            for (int i = start; i < lines.Length; i++)
            {
                Console.WriteLine($"  {lines[i].TrimEnd()}");
            }
        }

        // ========== NPC列表 ==========

        private void ShowNPCList()
        {
            Console.Clear();
            PrintHeader("居 民 列 表");

            foreach (var npc in _game.NPCs.GetAllNPCs())
            {
                string statusIcon = npc.Status == NPCStatus.Dead ? "X"
                    : npc.Status == NPCStatus.Injured ? "!"
                    : npc.Status == NPCStatus.Working ? "W"
                    : "O";

                Console.WriteLine($"  {statusIcon} {npc.Name} ({npc.Role})");
                Console.WriteLine($"    HP:{npc.Health} 士气:{npc.Morale} 饥饿:{npc.Hunger} 疲劳:{npc.Fatigue}");
                Console.WriteLine($"    医疗:{npc.Medical} 工程:{npc.Engineering} 搜集:{npc.Scavenging} 战斗:{npc.Combat} 社交:{npc.Social}");
                Console.WriteLine($"    状态:{npc.Status} 工作:{npc.CurrentWork}");
                Console.WriteLine($"    对玩家关系:{npc.GetRelationship("player")}");
                if (!string.IsNullOrEmpty(npc.Memory.Summary))
                {
                    Console.WriteLine($"    记忆: {npc.Memory.Summary}");
                }
                Console.WriteLine();
            }

            Pause();
        }

        // ========== 建筑菜单 ==========

        private void ShowBuildingMenu()
        {
            Console.Clear();
            PrintHeader("建 筑 管 理");

            foreach (var b in _game.Buildings.GetAllBuildings())
            {
                string status = b.Built ? $"Lv.{b.Level}" : "未建造";
                Console.WriteLine($"  [{status}] {b.Name}");
                Console.WriteLine($"    {b.Description}");
                Console.WriteLine($"    {b.EffectDescription}");
                if (b.Id == "med_room" && !_game.OffersDuty(CampDuty.Triage))
                    Console.WriteLine("    建成后可安排医疗值班。");

                if (!b.Built)
                {
                    Console.Write("    建造消耗: ");
                    foreach (var cost in b.BuildCost.Amounts)
                    {
                        Console.Write($"{GameConfig.ResourceNames[(int)cost.Type]}x{cost.Amount} ");
                    }
                    Console.WriteLine();
                }
                else if (!b.IsMaxLevel)
                {
                    Console.Write("    升级消耗: ");
                    foreach (var cost in b.UpgradeCost.Amounts)
                    {
                        Console.Write($"{GameConfig.ResourceNames[(int)cost.Type]}x{cost.Amount} ");
                    }
                    Console.WriteLine();
                }

                if (b.Built && b.DailyOutput.Amounts.Count > 0)
                {
                    Console.Write("    每日产出: ");
                    foreach (var output in b.DailyOutput.Amounts)
                    {
                        Console.Write($"{GameConfig.ResourceNames[(int)output.Type]}x{output.Amount * b.Level} ");
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();
            }

            // 保留原有四个快捷选项，其余设施由建筑列表提供。
            var actions = new List<(BuildingInstance Building, bool Upgrade)>
            {
                (_game.Buildings.GetBuilding("greenhouse"), false),
                (_game.Buildings.GetBuilding("archive_hall"), true),
                (_game.Buildings.GetBuilding("workshop"), true),
                (_game.Buildings.GetBuilding("greenhouse"), true)
            };
            actions.AddRange(_game.Buildings.GetAllBuildings()
                .Where(b => b.Unlocked && b.Id is not ("greenhouse" or "archive_hall" or "workshop"))
                .Select(b => (b, b.Built)));
            for (int i = 0; i < actions.Count; i++)
                Console.WriteLine($"  {i + 1}. {(actions[i].Upgrade ? "升级" : "建造")}{actions[i].Building.Name}");
            Console.Write("  选择建筑操作 (0=返回): ");
            if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 1 || choice > actions.Count) return;
            var (building, upgrade) = actions[choice - 1];
            bool success = upgrade ? _game.Buildings.Upgrade(building.Id) : _game.Buildings.Build(building.Id);
            Console.WriteLine(success ? $"  {building.Name}已{(upgrade ? "升级" : "建成")}。" : "  请检查建筑等级与所需物资。");
            Pause();
        }

        // ========== 任务列表 ==========

        private void ShowQuestList()
        {
            Console.Clear();
            PrintHeader("任 务 列 表");

            var allQuests = _game.Quests.GetAllQuests();
            var activeQuests = _game.Quests.GetActiveQuests();

            Console.WriteLine("  === 活跃任务 ===");
            if (activeQuests.Count == 0) Console.WriteLine("  (无)");
            foreach (var q in activeQuests)
            {
                Console.WriteLine($"  [{q.Type}] {q.Title}");
                Console.WriteLine($"    {q.Description}");
                foreach (var obj in q.Objectives)
                {
                    string done = obj.IsComplete ? "V" : "O";
                    Console.WriteLine($"    {done} {obj.Type}: {obj.TargetId} ({obj.CurrentProgress}/{obj.RequiredAmount})");
                }
                Console.WriteLine();
            }

            Console.WriteLine("  === 未开始任务 ===");
            foreach (var q in allQuests.Where(q => q.Status == QuestStatus.NotStarted))
            {
                Console.WriteLine($"  [{q.Type}] {q.Title}");
                Console.WriteLine($"    {q.Description}");
                Console.WriteLine();
            }

            Console.WriteLine("  === 已完成任务 ===");
            foreach (var q in allQuests.Where(q => q.Status == QuestStatus.Completed))
            {
                Console.WriteLine($"  V [{q.Type}] {q.Title}");
            }

            Pause();
        }

        // ========== 对话菜单 ==========

        private void ShowDialogueMenu()
        {
            Console.Clear();
            PrintHeader("与 N P C 对 话");

            var npcs = _game.NPCs.GetAllNPCs().Where(n => n.IsAlive).ToList();
            for (int i = 0; i < npcs.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {npcs[i].Name} ({npcs[i].Role})");
            }

            Console.Write("  选择对话对象 (0=返回): ");
            var input = Console.ReadLine()?.Trim();
            if (input == "0") return;

            if (int.TryParse(input, out int idx) && idx >= 1 && idx <= npcs.Count)
            {
                string dialogue = _game.TalkToNPC(npcs[idx - 1].Id);
                Console.WriteLine();
                Console.WriteLine(dialogue);
            }

            Pause();
        }

        // ========== 工作分配 ==========

        private void ShowWorkAssignment()
        {
            Console.Clear();
            PrintHeader("工 作 分 配");

            var npcs = _game.NPCs.GetAllNPCs().Where(n => n.CanWork && n.Id != _game.TodayDutyWorker).ToList();
            for (int i = 0; i < npcs.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {npcs[i].Name} - 当前工作: {npcs[i].CurrentWork}");
            }

            Console.Write("  选择NPC (0=返回): ");
            var input = Console.ReadLine()?.Trim();
            if (input == "0") return;

            if (int.TryParse(input, out int idx) && idx >= 1 && idx <= npcs.Count)
            {
                var npc = npcs[idx - 1];
                Console.WriteLine($"  为 {npc.Name} 选择工作:");
                Console.WriteLine("  1. 医疗  2. 工程  3. 搜集  4. 守卫  5. 种植  0. 无");
                Console.Write("  选择: ");
                var workInput = Console.ReadLine()?.Trim();

                WorkType work = WorkType.None;
                switch (workInput)
                {
                    case "1": work = WorkType.Doctor; break;
                    case "2": work = WorkType.Engineer; break;
                    case "3": work = WorkType.Scavenging; break;
                    case "4": work = WorkType.Guarding; break;
                    case "5": work = WorkType.Farming; break;
                    case "0": work = WorkType.None; break;
                }

                if (_game.NPCs.AssignWork(npc.Id, work))
                {
                    Console.WriteLine($"  {npc.Name} 已分配到 {work} 工作。");
                }
                else
                {
                    Console.WriteLine("  分配失败！");
                }
            }

            Pause();
        }

        // ========== 游戏结束 ==========

        private void ShowGameOver()
        {
            Console.Clear();
            PrintBanner();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════╗");
            Console.WriteLine("  ║       游 戏 结 束             ║");
            Console.WriteLine("  ╚══════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"  你在档案城生存了 {_game.Time.CurrentDay} 天。");
            Console.WriteLine();
            Console.WriteLine("  1. 返回主菜单");
            Console.WriteLine("  2. 退出");
            Console.Write("  选择: ");

            var input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": _game = null; break;
                case "2": _running = false; break;
            }
        }

        // ========== 物品背包 ==========

        private void ShowInventory()
        {
            Console.Clear();
            PrintHeader("背 包 物 品");

            var items = _game.Items.GetInventory();
            if (items.Count == 0)
            {
                Console.WriteLine("  (背包为空)");
                Pause();
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Console.WriteLine($"  {i + 1}. [{item.GetRarityName()}] {item.Name}");
                Console.WriteLine($"     {item.Description}");
                if (item.AttackBonus > 0) Console.WriteLine($"     攻击+{item.AttackBonus}");
                if (item.DefenseBonus > 0) Console.WriteLine($"     防御+{item.DefenseBonus}");
                if (item.HealAmount > 0) Console.WriteLine($"     治疗{item.HealAmount}HP");
                if (item.MoraleBoost > 0) Console.WriteLine($"     士气+{item.MoraleBoost}");
            }

            Console.WriteLine($"\n  共 {items.Count} 件物品");
            Pause();
        }

        // ========== 派系声望 ==========

        private void ShowFactions()
        {
            Console.Clear();
            PrintHeader("派 系 声 望");

            var factions = _game.Factions.GetAllFactions();
            foreach (var f in factions)
            {
                string status = f.Unlocked ? "" : " (未解锁)";
                Console.WriteLine($"  【{f.Name}】{status}");
                Console.WriteLine($"     {f.Description}");
                Console.WriteLine($"     声望: {f.Reputation} ({f.GetReputationName()})");
                Console.WriteLine($"     领袖: {f.Leader}");
                if (f.MemberNPCIds.Count > 0)
                {
                    Console.WriteLine($"     成员: {string.Join(", ", f.MemberNPCIds)}");
                }
                Console.WriteLine();
            }

            Pause();
        }

        // ========== 成就 ==========

        private void ShowAchievements()
        {
            Console.Clear();
            PrintHeader("成 就");

            var all = _game.Achievements.GetAllAchievements();
            // 按分类分组
            var groups = new Dictionary<string, List<Achievement>>();
            foreach (var ach in all)
            {
                string cat = ach.Category ?? "其他";
                if (!groups.ContainsKey(cat)) groups[cat] = new List<Achievement>();
                groups[cat].Add(ach);
            }

            foreach (var kv in groups)
            {
                Console.WriteLine($"  [{kv.Key}]");
                foreach (var ach in kv.Value)
                {
                    Console.WriteLine($"    {ach.GetStatus()} - {ach.Name}: {ach.Description}");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"  {_game.Achievements.GetStats()}");
            Pause();
        }

        private void ShowMarket()
        {
            if (_game.MarketCapacity == 0)
            {
                Console.WriteLine("  建造集市后可以交换物资。");
                Pause();
                return;
            }
            Console.WriteLine($"  今日剩余 {_game.MarketTradesLeft} 次交换");
            for (int index = 0; index < GameManager.MarketOffers.Count; index++)
            {
                var offer = GameManager.MarketOffers[index];
                Console.WriteLine($"  {index + 1}. {offer.Cost.Amount} {GameConfig.ResourceNames[(int)offer.Cost.Type]} → {offer.Reward.Amount} {GameConfig.ResourceNames[(int)offer.Reward.Type]}");
            }
            Console.Write("  选择兑换项，回车返回: ");
            if (int.TryParse(Console.ReadLine(), out int selection) && selection >= 1 && selection <= GameManager.MarketOffers.Count)
            {
                Console.WriteLine(_game.Trade(GameManager.MarketOffers[selection - 1].Id) ? "  已完成交换。" : "  缺少物资或已用完今日额度。");
                Pause();
            }
        }

        private void ShowEndingChoice()
        {
            if (!_game.CanChooseEnding) return;
            var choices = GameManager.EndingChoices;
            var endings = _game.Endings.GetAllEndings();
            for (int index = 0; index < choices.Count; index++)
            {
                var ending = endings.Find(entry => entry.Type == choices[index]);
                var cost = ending.Type == EndingType.Upload ? $"（消耗 {GameConfig.UploadShardCost} 枚碎片）" : "";
                Console.WriteLine($"  {index + 1}. {ending.Title} {cost}");
            }
            Console.Write("  选择结局，回车返回: ");
            if (int.TryParse(Console.ReadLine(), out int selection) && selection >= 1 && selection <= choices.Count)
            {
                Console.Write("  确认结束这局游戏？(y/N) ");
                if (string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase))
                {
                    if (_game.ChooseEnding(choices[selection - 1])) _game.SaveGame();
                    else Console.WriteLine("  当前无法选择该结局。");
                    Pause();
                }
            }
        }

        // ========== 辅助方法 ==========

        private void ShowResources()
        {
            Console.Write("  资源: ");
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int amount = _game.Resources.GetResourceAmount(type);
                Console.Write($"{GameConfig.ResourceNames[(int)type]}:{amount} ");
            }
            Console.WriteLine();
        }

        private void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ===================================");
            Console.WriteLine("  ||     最 后 档 案 城             ||");
            Console.WriteLine("  ||     Last Archive               ||");
            Console.WriteLine("  ===================================");
        }

        private void PrintHeader(string title)
        {
            Console.WriteLine($"  ====== 第{_game.Time.CurrentDay}天 | {title} ======");
        }

        private void Pause()
        {
            Console.WriteLine("\n  按任意键继续...");
            Console.ReadKey();
        }

        // ========== 危机/心理 ==========

        private void ShowCrisisAndPsychology()
        {
            Console.Clear();
            PrintHeader("危 机 / 心 理 状 态");

            // 危机
            var crises = _game.Crises.ActiveCrises;
            if (crises.Count > 0)
            {
                Console.WriteLine("  【活跃危机】");
                foreach (var c in crises)
                {
                    string sev = c.Severity == CrisisSeverity.Critical ? "!!!" : c.Severity == CrisisSeverity.Active ? "!" : "?";
                    Console.WriteLine($"    {sev} {c.Type}: {c.Description}");
                    Console.WriteLine($"       持续: 第{c.StartDay}天起");
                }
            }
            else
            {
                Console.WriteLine("  【危机】当前无活跃危机");
            }

            Console.WriteLine();

            // 心理状态
            Console.WriteLine("  【NPC心理状态】");
            foreach (var npc in _game.NPCs.GetAllNPCs())
            {
                if (!npc.IsAlive) continue;
                var state = _game.Psychology.GetState(npc.Id);
                var traits = _game.Psychology.GetTraits(npc.Id);
                string stateStr = _game.Psychology.GetStateName(state);
                string traitStr = "";
                if ((traits & MentalTrait.Trauma) != 0) traitStr += "创伤 ";
                if ((traits & MentalTrait.Hope) != 0) traitStr += "希望 ";
                if ((traits & MentalTrait.Fear) != 0) traitStr += "恐惧 ";
                if ((traits & MentalTrait.Trust) != 0) traitStr += "信任 ";
                if ((traits & MentalTrait.Despair) != 0) traitStr += "绝望 ";
                if ((traits & MentalTrait.Paranoia) != 0) traitStr += "偏执 ";
                if ((traits & MentalTrait.Resolve) != 0) traitStr += "决心 ";
                Console.WriteLine($"    {npc.Name}: [{stateStr}] {traitStr}");
            }

            Console.WriteLine();

            // 结局预测
            Console.WriteLine("  【结局预测】");
            var allEndings = _game.Endings.GetAllEndings();
            foreach (var e in allEndings)
            {
                Console.WriteLine($"    {e.Title} - {e.Description}");
            }

            Pause();
        }
    }

    // 两个本地界面共用的战役提示；资格、消耗与结算均由游戏系统判断。
    internal static class CampaignUI
    {
        internal static readonly CampDuty[] Duties = { CampDuty.Pump, CampDuty.Triage };

        internal static string CampStatus(GameManager game)
        {
            string title = GameManager.Openings.First(o => o.Scenario == game.Scenario).Title;
            return game.Scenario == CampaignScenario.MedicalShortage
                ? $"{title} · 重伤居民 {game.CriticalResidents} 人（生命低于 {GameManager.CriticalHealth}）" : title;
        }

        internal static string CareStatus(GameManager.CareForecast patient) =>
            $"{patient.Name} · 预计生命 {patient.Health} → {patient.AfterCare}" +
            (patient.ReceivesTriage ? " · 值班救治" : "") +
            (patient.AfterCare < GameManager.CriticalHealth ? " · 日终仍需救治" : "");

        private static string RouteName(SupplyRoute route) => route == SupplyRoute.Homestead ? "本地生产" : "商队补给";

        internal static string SupplyStatus(GameManager game)
        {
            if (game.Route != SupplyRoute.Undecided) return $"补给路线 · {RouteName(game.Route)}已生效";
            if (game.PlannedSupplyRoute != SupplyRoute.Undecided)
                return $"计划采用{RouteName(game.PlannedSupplyRoute)}，日终满足条件时生效。";
            if (!game.SupplyDecisionOpen) return "完成「唤醒档案馆」后可建立补给路线。";
            return game.SupplyRouteReady(SupplyRoute.Homestead) && game.SupplyRouteReady(SupplyRoute.Caravan)
                ? "两条补给路线均已就绪，请在日终前选择一条。" : "日终仅一条路线就绪时，会自动采用该路线。";
        }

        internal static string RouteOption(GameManager game, SupplyRoute route)
        {
            string requirement = route == SupplyRoute.Homestead ? "温室和水处理站" : $"集市并预留 {GameManager.CaravanDeposit} 零件";
            string benefit = route == SupplyRoute.Homestead ? "提高粮水日产" : "增加集市额度，降低远征消耗";
            return $"{RouteName(route)} · {benefit} · {requirement} · {(game.SupplyRouteReady(route) ? "已就绪" : "条件未满足")}";
        }

        internal static List<NPCInstance> DutyCandidates(GameManager game, CampDuty duty) =>
            game.NPCs.GetAllNPCs().Where(npc => game.CanStaffDuty(duty, npc)).ToList();

        internal static string DutyName(CampDuty duty) => duty == CampDuty.Pump ? "净水值班" : "医疗值班";

        internal static string AssignedDuty(GameManager game) =>
            $"今日{DutyName(game.TodayDuty)} · {game.NPCs.GetNPC(game.TodayDutyWorker).Name}" +
            (game.DutyCanRun ? "留守营地，日终交班。" : "当前无法履职。");

        internal static string DutyOption(GameManager game, CampDuty duty)
        {
            string terms = duty == CampDuty.Pump
                ? $"工程 ≥ {GameManager.PumpSkill} · 预付 {GameManager.PumpPower} 电力，日终供水 +{GameManager.EmergencyWater}"
                : $"医疗 ≥ {GameManager.TriageSkill} · 预付 {GameManager.TriageMedicine} 药品，日终救治 {GameManager.TriagePatients} 人，每人最多 +{GameManager.TriageHeal} 生命";
            string issue = !game.AffordsDuty(duty) ? " · 物资不足" : "";
            if (DutyCandidates(game, duty).Count == 0) issue += " · 缺少今日未远征的合格空闲居民";
            return $"{DutyName(duty)} · {terms}{issue}";
        }

        internal static string InstallOption(GameManager game) =>
            $"安装广播组件 · {GameManager.RelayPartsCost} 零件 / {GameManager.RelayPowerCost} 电力" +
            (game.CanAffordRelay ? "" : " · 材料不足");

        internal static string RelayForecast(GameManager game) =>
            $"日终试送电（{GameManager.RelayTestPower} 电力） · " +
            (game.RelayTestForecast is { Length: > 0 } issue ? issue : "预计可通过");

        internal static string DepartureIssue(GameManager game, int eligibleResidents)
        {
            if (game.IsGameOver) return "本局已结束。";
            if (game.Combat.InCombat) return "请先结束战斗。";
            if (game.DeparturesLeft == 0) return "今日出发次数已用完。";
            if (!game.CanAffordDeparture) return $"出发需 {game.DepartureFood} 食物和 {game.DepartureWater} 饮水。";
            return eligibleResidents == 0 ? "没有可出发的居民，请检查值班、伤势、疲劳与心理状态。" : "";
        }

        internal static string SearchIssue(ExplorationSystem expedition)
        {
            if (!expedition.HasSurvivors) return "队伍已无法行动，请返回基地。";
            if (expedition.ActiveCheck != null) return "请先完成当前检定。";
            return expedition.SearchedRooms.Contains(expedition.CurrentRoom.Id) ? "本次远征已搜寻" : expedition.SearchRequirement;
        }

        internal static string EntryIssue(GameManager game, RoomData room)
        {
            var expedition = game.Exploration;
            if (!expedition.HasSurvivors) return "队伍已无法行动，请返回基地。";
            if (expedition.ActiveCheck != null) return "请先完成当前检定。";
            if (!expedition.HasClearance(room))
            {
                var guardRoom = expedition.CurrentMap.Rooms.Find(r => r.Id == room.RequiredClearance);
                return $"需先清除{guardRoom?.Name ?? room.RequiredClearance}的守卫";
            }
            if (expedition.CanEnter(room)) return "";
            return string.IsNullOrEmpty(room.RequiredItem) ? "房间已上锁" :
                $"需要{game.Items.GetTemplate(room.RequiredItem)?.Name ?? room.RequiredItem}";
        }
    }
}
