// ============================================================
// Last Archive - Web 服务器
// C# HttpListener + HTML/JS 前端
// ============================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace LastArchive
{
    public class WebGameServer
    {
        private HttpListener _listener;
        private const string PlayerCookie = "last_archive_player";
        private const string SecurePlayerCookie = "__Host-last_archive_player";
        private readonly bool _onHuggingFace = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SPACE_HOST"));
        private readonly ConcurrentDictionary<Guid, WebPlayerSession> _players = new();
        private readonly string _saveDirectory = Path.GetFullPath(
            Environment.GetEnvironmentVariable("LAST_ARCHIVE_SAVE_DIR") ?? "web-saves");
        private int _port = 8080;

        public void Start(int port = 8080)
        {
            // 读取环境变量 PORT (适用于云端部署，如 Render, Railway 等)
            var envPort = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out var p))
            {
                port = p;
            }

            _port = port;
            Directory.CreateDirectory(_saveDirectory);

            _listener = new HttpListener();
            
            // 尝试绑定到通配符 http://*:{port}/ 供外网或云托管服务访问
            // Windows 下非管理员运行此绑定会失败，此时自动降级至 localhost 绑定
            try
            {
                _listener.Prefixes.Add($"http://*:{_port}/");
                _listener.Start();
                Console.WriteLine($"🎮 Last Archive Web Server running at http://*:{_port} (已开启公网/局域网共享)");
            }
            catch (Exception)
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Start();
                Console.WriteLine($"🎮 Last Archive Web Server running at http://localhost:{_port} (本地模式)");
            }

            Console.WriteLine("Press Ctrl+C to stop.");

            while (_listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    System.Threading.ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            HandleRequest((HttpListenerContext)state);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error handling request: {ex.Message}");
                        }
                    }, ctx);
                }
                catch (HttpListenerException) { break; }
                catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var path = ctx.Request.Url.AbsolutePath;
            var method = ctx.Request.HttpMethod;

            try
            {
                ctx.Response.Headers["Cache-Control"] = "no-store";

                if (method == "GET" && (path == "/" || path == "/index.html"))
                {
                    if (GetPlayerId(ctx.Request) == null)
                    {
                        bool secure = IsHttps(ctx.Request);
                        string name = secure ? SecurePlayerCookie : PlayerCookie;
                        ctx.Response.Headers.Add("Set-Cookie",
                            $"{name}={Guid.NewGuid():N}; Path=/; Max-Age=31536000; HttpOnly; SameSite=Lax" +
                            (secure ? "; Secure" : ""));
                    }

                    SendResponse(ctx, WebGamePage.GetIndexHtml(), "text/html; charset=utf-8");
                    return;
                }

                if (method == "GET" && WebGamePage.TryGetAsset(path, out var asset))
                {
                    ctx.Response.Headers["Cache-Control"] = "public, no-cache";
                    ctx.Response.Headers["ETag"] = asset.ETag;
                    if (ctx.Request.Headers["If-None-Match"] == asset.ETag)
                    {
                        ctx.Response.StatusCode = 304;
                        ctx.Response.Close();
                        return;
                    }
                    SendResponse(ctx, asset.Bytes, asset.ContentType);
                    return;
                }

                if (!path.StartsWith("/api/", StringComparison.Ordinal))
                {
                    ctx.Response.StatusCode = 404;
                    SendResponse(ctx, "Not Found", "text/plain");
                    return;
                }

                if (method is not ("GET" or "POST"))
                {
                    SendError(ctx, 405, "不支持此请求方法。");
                    return;
                }

                if (path == "/api/ai/config" && method == "GET")
                {
                    SendJson(ctx, new { provider = "Mock", configurable = false });
                    return;
                }

                if (path.StartsWith("/api/ai/", StringComparison.Ordinal))
                {
                    SendError(ctx, 403, "公开试玩使用内置 AI。");
                    return;
                }

                if (method == "POST" && ctx.Request.Headers["Sec-Fetch-Site"] == "cross-site")
                {
                    SendError(ctx, 403, "请从游戏页面执行此操作。");
                    return;
                }

                if (method == "POST" &&
                    !string.Equals(ctx.Request.ContentType?.Split(';', 2)[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    SendError(ctx, 415, "此操作需要 JSON 请求。");
                    return;
                }

                var playerId = GetPlayerId(ctx.Request);
                if (playerId == null)
                {
                    SendError(ctx, 409, "无法建立玩家会话。请在新窗口打开游戏并允许本站 Cookie。");
                    return;
                }

                var player = _players.GetOrAdd(playerId.Value, id =>
                    new WebPlayerSession(Path.Combine(_saveDirectory, $"{id:N}.json")));
                var response = player.HandleApi(path, method, ctx.Request);
                SendResponse(ctx, response, "application/json; charset=utf-8");
            }
            catch (JsonException)
            {
                SendError(ctx, 400, "请求格式无效。");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"请求处理失败：{ex.Message}");
                SendError(ctx, 500, "服务器暂时无法完成此操作。");
            }
        }

        private bool IsHttps(HttpListenerRequest request) => _onHuggingFace || request.IsSecureConnection ||
            string.Equals(request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);

        private Guid? GetPlayerId(HttpListenerRequest request)
        {
            string name = IsHttps(request) ? SecurePlayerCookie : PlayerCookie;
            return Guid.TryParseExact(request.Cookies[name]?.Value, "N", out var id) && id != Guid.Empty ? id : null;
        }

        private static void SendError(HttpListenerContext ctx, int status, string message)
        {
            ctx.Response.StatusCode = status;
            SendJson(ctx, new { ok = false, error = message });
        }

        private static void SendJson(HttpListenerContext ctx, object body)
            => SendResponse(ctx, JsonSerializer.Serialize(body), "application/json; charset=utf-8");

        private static void SendResponse(HttpListenerContext ctx, string body, string contentType)
            => SendResponse(ctx, Encoding.UTF8.GetBytes(body), contentType);

        private static void SendResponse(HttpListenerContext ctx, byte[] bytes, string contentType)
        {
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }
    }

    internal class WebPlayerSession
    {
        private readonly string _savePath;
        private GameScope _scope = new();
        private readonly object _gate = new();
        private readonly List<string> _log = new();
        private GameManager _game;
        private static readonly Dictionary<string, ResourceAmount[]> Recipes = new()
        {
            ["herb_paste"] = [new(ResourceType.Parts, 2), new(ResourceType.Medicine, 1)],
            ["morale_candy"] = [new(ResourceType.Food, 2), new(ResourceType.Water, 1)],
            ["combat_knife"] = [new(ResourceType.Parts, 5), new(ResourceType.Power, 2)],
            ["military_vest"] = [new(ResourceType.Parts, 6), new(ResourceType.Power, 3)]
        };

        public WebPlayerSession(string savePath)
        {
            _savePath = savePath;
        }

        public string HandleApi(string path, string method, HttpListenerRequest request)
        {
            lock (_gate)
            {
                return _scope.Run(() =>
                {
                    if (_game == null)
                    {
                        _game = CreateGame();
                        if (_game.Save.HasSave()) _game.LoadGame();
                    }
                    return RouteApi(path, method, request);
                });
            }
        }

        private GameManager CreateGame(CampaignScenario scenario = CampaignScenario.Standard)
        {
            var game = new GameManager(_savePath);
            game.Initialize();
            game.StartNewGame(scenario);
            return game;
        }

        // ═══════════════ API 路由 ═══════════════

        private string RouteApi(string path, string method, HttpListenerRequest req)
        {
            // GET /api/status - 游戏状态概览
            if (method == "POST" && _game.IsGameOver &&
                path is not ("/api/save" or "/api/load" or "/api/newgame" or "/api/save/import"))
                return Failure("游戏已结束。");
            if (path == "/api/status" && method == "GET") return GetStatus();
            // GET /api/npcs - NPC列表
            if (path == "/api/npcs" && method == "GET") return GetNPCs();
            // GET /api/buildings - 建筑列表
            if (path == "/api/buildings" && method == "GET") return GetBuildings();
            // GET /api/quests - 任务列表
            if (path == "/api/quests" && method == "GET") return GetQuests();
            // GET /api/items - 背包
            if (path == "/api/items" && method == "GET") return GetItems();
            // GET /api/factions - 派系
            if (path == "/api/factions" && method == "GET") return GetFactions();
            // GET /api/crisis - 危机/心理
            if (path == "/api/crisis" && method == "GET") return GetCrisis();
            // GET /api/log - 日志
            if (path == "/api/log" && method == "GET") return GetLog();
            // GET /api/endings - 结局预测
            if (path == "/api/endings" && method == "GET") return GetEndings();

            // POST /api/nextday - 下一天
            if (path == "/api/nextday" && method == "POST") return DoNextDay(req);
            // POST /api/save - 保存
            if (path == "/api/save" && method == "POST") return DoSave();
            if (path == "/api/save/export" && method == "GET") return ExportSave();
            if (path == "/api/save/import" && method == "POST") return ImportSave(req);
            // POST /api/load - 读档
            if (path == "/api/load" && method == "POST") return DoLoad();
            // POST /api/newgame - 新游戏
            if (path == "/api/newgame" && method == "POST") return DoNewGame(req);
            // POST /api/build - 建造
            if (path == "/api/build" && method == "POST") return DoBuild(req);
            // POST /api/upgrade - 升级
            if (path == "/api/upgrade" && method == "POST") return DoUpgrade(req);
            // POST /api/heal - 治疗
            if (path == "/api/heal" && method == "POST") return DoHeal(req);
            // POST /api/talk - 对话
            if (path == "/api/talk" && method == "POST") return DoTalk(req);
            // POST /api/explore - 探索
            if (path == "/api/explore" && method == "POST") return DoExplore(req);
            // POST /api/combat - 战斗
            if (path == "/api/combat" && method == "POST") return DoCombat(req);

            // GET /api/npcs/relations - NPC关系网
            if (path == "/api/npcs/relations" && method == "GET") return GetNPCRelations();
            // GET /api/combat/status - 战斗状态
            if (path == "/api/combat/status" && method == "GET") return GetCombatStatus();
            // GET /api/explore/status - 探索状态
            if (path == "/api/explore/status" && method == "GET") return GetExploreStatus();
            // POST /api/explore/start - 开始探索
            if (path == "/api/explore/start" && method == "POST") return DoExploreStart(req);
            // POST /api/explore/move - 探索移动
            if (path == "/api/explore/move" && method == "POST") return DoExploreMove(req);
            // POST /api/explore/search - 探索搜索
            if (path == "/api/explore/search" && method == "POST") return DoExploreSearch();
            if (path == "/api/explore/bypass-tower" && method == "POST") return DoBypassTower();
            // POST /api/explore/skillcheck - 探索判定
            if (path == "/api/explore/skillcheck" && method == "POST") return DoExploreSkillCheck(req);
            // POST /api/explore/return - 探索返城
            if (path == "/api/explore/return" && method == "POST") return DoExploreReturn();
            // POST /api/combat/start - 开始战斗
            if (path == "/api/combat/start" && method == "POST") return DoCombatStart(req);
            // POST /api/combat/action - 战斗动作
            if (path == "/api/combat/action" && method == "POST") return DoCombatAction(req);
            // POST /api/craft - 合成物品
            if (path == "/api/craft" && method == "POST") return DoCraft(req);
            if (path == "/api/recipes" && method == "GET") return GetRecipes();
            if (path == "/api/market" && method == "GET") return GetMarket();
            if (path == "/api/market" && method == "POST") return DoTrade(req);
            if (path == "/api/ending/choose" && method == "POST") return DoChooseEnding(req);
            if (path == "/api/campaign/duty" && method == "POST") return DoCampDuty(req);
            if (path == "/api/campaign/supply" && method == "POST") return DoPlanSupply(req);
            if (path == "/api/campaign/relay" && method == "POST") return DoInstallRelay();
            if (path == "/api/items/equip" && method == "POST") return DoItemAction(req, equip: true);
            if (path == "/api/items/use" && method == "POST") return DoItemAction(req, equip: false);

            return JsonSerializer.Serialize(new { error = "Unknown API" });
        }

        // ═══════════════ GET API ═══════════════

        private string GetStatus()
        {
            return JsonSerializer.Serialize(new
            {
                day = _game.Time.CurrentDay,
                phase = _game.Time.CurrentPhase.ToString(),
                isGameOver = _game.IsGameOver,
                isVictory = _game.IsVictory,
                canSave = _game.CanSave,
                canChooseEnding = _game.CanChooseEnding,
                uploadShardCost = GameConfig.UploadShardCost,
                expeditionFatigueLimit = GameConfig.ExpeditionFatigueLimit,
                campaign = new
                {
                    route = _game.Route.ToString(),
                    plannedRoute = _game.PlannedSupplyRoute.ToString(),
                    supplyDecisionOpen = _game.SupplyDecisionOpen,
                    homesteadReady = _game.SupplyRouteReady(SupplyRoute.Homestead),
                    caravanReady = _game.SupplyRouteReady(SupplyRoute.Caravan),
                    caravanDeposit = GameManager.CaravanDeposit,
                    scenario = _game.Scenario.ToString(),
                    openings = GameManager.Openings.Select(opening => new
                    { id = opening.Scenario.ToString(), title = opening.Title, desc = opening.Description }),
                    duty = new { kind = _game.TodayDuty.ToString(), workerId = _game.TodayDutyWorker, canRun = _game.DutyCanRun },
                    duties = new[] { CampDuty.Pump, CampDuty.Triage }.Where(_game.OffersDuty).Select(PresentCampDuty),
                    criticalResidents = _game.CriticalResidents,
                    criticalHealth = GameManager.CriticalHealth,
                    care = _game.GetCareForecast().Select(patient => new { id = patient.Id, name = patient.Name,
                        health = patient.Health, afterCare = patient.AfterCare, treated = patient.ReceivesTriage }),
                    towerCalibrated = _game.TowerCalibrated,
                    relayInstalled = _game.RelayInstalled,
                    canInstall = _game.CanInstallRelay,
                    canAffordRelay = _game.CanAffordRelay,
                    relayParts = GameManager.RelayPartsCost,
                    relayPower = GameManager.RelayPowerCost,
                    relayTestPower = GameManager.RelayTestPower,
                    relayTestIssue = _game.RelayTestForecast,
                    relayAwaitingTest = _game.RelayAwaitingTest
                },
                lastDailyEvent = _game.LastDailyEvent,
                ending = _game.IsGameOver ? PresentEnding() : null,
                forecast = _game.Time.GetSupplyForecast().Select(entry => new
                {
                    resource = entry.Type.ToString(), stock = entry.Stock, produced = entry.Produced, used = entry.Used,
                    reserve = entry.Reserve, shortfall = entry.Shortfall
                }),
                resources = new
                {
                    food = _game.Resources.GetResourceAmount(ResourceType.Food),
                    water = _game.Resources.GetResourceAmount(ResourceType.Water),
                    power = _game.Resources.GetResourceAmount(ResourceType.Power),
                    medicine = _game.Resources.GetResourceAmount(ResourceType.Medicine),
                    parts = _game.Resources.GetResourceAmount(ResourceType.Parts),
                    shards = _game.Resources.GetResourceAmount(ResourceType.MemoryShards)
                },
                aliveNPCs = _game.NPCs.GetAliveCount(),
                totalCombats = _game.TotalCombatsWon,
                totalExplorations = _game.TotalExplorationsCompleted,
                hasCrisis = _game.Crises.HasAnyCrisis
            });
        }

        private object PresentCampDuty(CampDuty duty)
        {
            bool pump = duty == CampDuty.Pump;
            return new
            {
                id = duty.ToString(),
                title = pump ? "净水值班" : "医疗值班",
                desc = pump ? $"预付 {GameManager.PumpPower} 电力。日终供水 +{GameManager.EmergencyWater}。" :
                    $"预付 {GameManager.TriageMedicine} 药品。日终为生命最低的 {GameManager.TriagePatients} 位居民各恢复最多 {GameManager.TriageHeal} 生命。",
                requirement = pump ? $"工程 ≥ {GameManager.PumpSkill}" : $"医疗 ≥ {GameManager.TriageSkill}",
                affordable = _game.AffordsDuty(duty),
                candidates = _game.NPCs.GetAllNPCs().Where(npc => _game.CanStaffDuty(duty, npc)).Select(npc => new { id = npc.Id, name = npc.Name })
            };
        }

        private string GetNPCs()
        {
            var npcs = new List<object>();
            foreach (var n in _game.NPCs.GetAllNPCs())
            {
                var mental = _game.Psychology.GetState(n.Id);
                npcs.Add(new
                {
                    id = n.Id, name = n.Name, age = n.Age, role = n.Role.ToString(), desc = n.Description,
                    weapon = _game.Items.GetTemplate(_game.Items.GetEquippedWeapon(n.Id) ?? "")?.Name,
                    armor = _game.Items.GetTemplate(_game.Items.GetEquippedArmor(n.Id) ?? "")?.Name,
                    isAlive = n.IsAlive, health = n.Health, morale = n.Morale,
                    loyalty = n.Loyalty, hunger = n.Hunger, fatigue = n.Fatigue,
                    status = n.Status.ToString(), work = n.CurrentWork.ToString(),
                    mental = _game.Psychology.GetStateName(mental), mentalKey = mental.ToString(),
                    canExplore = _game.CanExploreResident(n),
                    onDuty = _game.TodayDutyWorker == n.Id,
                    combat = n.Combat, medical = n.Medical, engineering = n.Engineering,
                    scavenging = n.Scavenging, social = n.Social,
                    diaries = n.Memory.DiaryHistory,
                    relationships = n.Relationships.Select(r => new { targetId = r.TargetId, value = r.Value }).ToList()
                });
            }
            return JsonSerializer.Serialize(npcs);
        }

        private string GetBuildings()
        {
            var list = new List<object>();
            foreach (var b in _game.Buildings.GetAllBuildings())
            {
                list.Add(new
                {
                    id = b.Id, name = b.Name, desc = b.Description,
                    built = b.Built, level = b.Level, maxLevel = b.MaxLevel, effect = b.EffectDescription,
                    output = FormatAmounts(b.DailyOutput.Amounts), upkeep = FormatAmounts(b.DailyConsumption.Amounts),
                    cost = FormatAmounts((b.Built ? b.UpgradeCost : b.BuildCost).Amounts),
                    affordable = _game.Resources.HasEnoughBundle(b.Built ? b.UpgradeCost : b.BuildCost)
                });
            }
            return JsonSerializer.Serialize(list);
        }

        private string GetQuests()
        {
            var list = new List<object>();
            foreach (var q in _game.Quests.GetAllQuests())
            {
                list.Add(new
                {
                    id = q.QuestId, title = q.Title, desc = q.Description,
                    type = q.Type.ToString(), status = q.Status.ToString(),
                    rewards = q.Rewards.Where(r => r.ResourceAmount > 0 && r.ResourceType.HasValue)
                        .Select(r => new { resource = r.ResourceType.Value.ToString(), amount = r.ResourceAmount }),
                    objectives = q.Objectives.Select(o => new
                    {
                        type = o.Type.ToString(), target = o.TargetId,
                        current = o.CurrentProgress, required = o.RequiredAmount,
                        label = ObjectiveLabel(o)
                    }).ToList()
                });
            }
            return JsonSerializer.Serialize(list);
        }

        private string GetItems()
        {
            var list = new List<object>();
            foreach (var i in _game.Items.GetInventory())
            {
                list.Add(new
                {
                    id = i.Id, name = i.Name, desc = i.Description,
                    type = i.Type.ToString(), rarity = i.GetRarityName(),
                    attack = i.AttackBonus, defense = i.DefenseBonus, heal = i.HealAmount,
                    morale = i.MoraleBoost, source = i.Source,
                    supply = i.ResourceType.HasValue ? i.ResourceType.Value.ToString() : null,
                    supplyAmount = i.ResourceAmount
                });
            }
            return JsonSerializer.Serialize(list);
        }

        private string GetFactions()
        {
            var list = new List<object>();
            foreach (var f in _game.Factions.GetAllFactions())
            {
                list.Add(new
                {
                    name = f.Name, rep = f.Reputation, repName = f.GetReputationName(),
                    unlocked = f.Unlocked, desc = f.Description, leader = f.Leader
                });
            }
            return JsonSerializer.Serialize(list);
        }

        private string GetCrisis()
        {
            var crises = _game.Crises.ActiveCrises.Select(c => new
            {
                type = c.Type.ToString(), severity = c.Severity.ToString(),
                desc = c.Description, startDay = c.StartDay
            }).ToList();

            var mental = new List<object>();
            foreach (var n in _game.NPCs.GetAllNPCs())
            {
                if (!n.IsAlive) continue;
                var s = _game.Psychology.GetState(n.Id);
                mental.Add(new { name = n.Name, state = _game.Psychology.GetStateName(s), key = s.ToString() });
            }

            string endingTitle = "未定";
            try { endingTitle = _game.Endings.CalculateEnding(_game).Title; } catch { }

            return JsonSerializer.Serialize(new { crises, mental, endingTitle });
        }

        private string GetLog()
        {
            return JsonSerializer.Serialize(new { log = _log.TakeLast(20).ToList() });
        }

        private string GetEndings()
        {
            var list = _game.Endings.GetAllEndings().Select(e => new
            {
                title = e.Title, desc = e.Description, good = e.IsGood, type = e.Type.ToString()
            });
            return JsonSerializer.Serialize(list);
        }

        // ═══════════════ POST API ═══════════════

        private string DoNextDay(HttpListenerRequest req)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(ReadBody(req));
            int settledDay = _game.Time.CurrentDay;
            if (data != null && data.TryGetValue("expectedDay", out int expectedDay) && expectedDay != settledDay)
                return Failure("游戏日期已经更新。");
            if (_game.IsGameOver) return Failure("游戏已结束。");
            if (!_game.AdvanceDay()) return Failure("请先完成当前探索或战斗。");

            var messages = new List<string> { $"第{settledDay}天结算完成" };
            if (!string.IsNullOrEmpty(_game.LastDailyEvent)) messages.Add(_game.LastDailyEvent);
            foreach (var crisis in _game.Crises.ActiveCrises) messages.Add($"危机: {crisis.Type}");
            if (_game.IsGameOver) messages.Add(_game.IsVictory ? "胜利！" : "游戏结束");
            bool saved = _game.SaveGame();
            messages.Add(saved ? "系统自动保存成功" : "自动保存失败，请手动保存。");
            AddLog(messages);

            var ending = _game.IsGameOver ? PresentEnding() : null;
            return JsonSerializer.Serialize(new
            {
                ok = true, day = _game.Time.CurrentDay, saved, messages,
                gameOver = _game.IsGameOver, victory = _game.IsVictory,
                ending
            });
        }

        private string DoSave()
        {
            if (!_game.CanSave) return Failure("请先结束探索和战斗，再保存进度。");
            bool ok = _game.SaveGame();
            AddLog(ok ? "保存成功" : "保存失败");
            return JsonSerializer.Serialize(new { ok, msg = ok ? "已保存当前进度。" : "保存失败，请重试。" });
        }

        private string DoLoad()
        {
            bool ok = _game.LoadGame();
            if (ok) _log.Clear();
            AddLog(ok ? "读档成功" : "读档失败");
            return JsonSerializer.Serialize(new { ok, msg = ok ? "已读取存档。" : "暂无可读取的存档，或存档已损坏。" });
        }

        private string ExportSave()
        {
            var data = _game.CaptureSave();
            if (data == null) return Failure("请先结束探索和战斗，再导出存档。");
            return PortableSave.Export(data) ?? Failure("当前存档超过 2 MB，无法导出。");
        }

        private string ImportSave(HttpListenerRequest request)
        {
            if (!_game.CanSave) return Failure("请先结束探索和战斗，再导入存档。");
            var (data, error) = PortableSave.Read(request.InputStream, request.ContentLength64);
            if (data == null) return Failure(error);
            if (!PortableSave.MatchesWorld(data, _game)) return Failure("存档中的城镇或探索数据不完整。");

            // Build the replacement away from the active game's services, then publish it only after saving.
            var scope = new GameScope();
            var game = new GameManager(_savePath);
            bool saved = scope.Run(() =>
            {
                game.RestoreSave(data);
                return game.SaveGame();
            });
            if (!saved) return Failure("导入未保存，请稍后重试。");

            _scope = scope;
            _game = game;
            _log.Clear();
            AddLog("已导入文件存档。");
            return JsonSerializer.Serialize(new { ok = true, day = data.CurrentDay, msg = "已导入并保存进度。" });
        }

        private static string Failure(string message) => JsonSerializer.Serialize(new { ok = false, msg = message });

        private string DoNewGame(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            var scenario = data?.GetValueOrDefault("scenario", nameof(CampaignScenario.Standard)) ?? nameof(CampaignScenario.Standard);
            var opening = GameManager.Openings.FirstOrDefault(entry => entry.Scenario.ToString() == scenario);
            if (opening == null) return Failure("请选择有效的开局局势。");
            _game = CreateGame(opening.Scenario);
            _log.Clear();
            AddLog($"新游戏开始：{opening.Title}。");
            return JsonSerializer.Serialize(new { ok = true, msg = $"已开启「{opening.Title}」。" });
        }

        private string DoCampDuty(HttpListenerRequest req)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadBody(req));
            var duty = data?.GetValueOrDefault("duty") switch
            {
                nameof(CampDuty.Pump) => CampDuty.Pump,
                nameof(CampDuty.Triage) => CampDuty.Triage,
                _ => CampDuty.None
            };
            var id = data?.GetValueOrDefault("npcId");
            if (!_game.AssignCampDuty(duty, id)) return Failure("无法排班。请检查物资、居民技能以及今天是否已经出过远征。");
            var message = $"{_game.NPCs.GetNPC(id).Name} 已留守，日终完成值班。";
            AddLog(message);
            return JsonSerializer.Serialize(new { ok = true, msg = message });
        }

        private string DoPlanSupply(HttpListenerRequest req)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadBody(req));
            var route = data?.GetValueOrDefault("route") switch
            {
                nameof(SupplyRoute.Homestead) => SupplyRoute.Homestead,
                nameof(SupplyRoute.Caravan) => SupplyRoute.Caravan,
                _ => SupplyRoute.Undecided
            };
            return _game.PlanSupplyRoute(route)
                ? JsonSerializer.Serialize(new { ok = true, msg = "已选择补给路线，日终结算时确认。" })
                : Failure("当前不能确认这条补给路线，请检查设施和物资。");
        }

        private string DoBuild(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string id = data.GetValueOrDefault("id", "");
            bool ok = _game.Buildings.Build(id);
            AddLog(new List<string> { ok ? $"建造了 {id}" : "建造失败" });
            return JsonSerializer.Serialize(new { ok });
        }

        private string DoUpgrade(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string id = data.GetValueOrDefault("id", "");
            bool ok = _game.Buildings.Upgrade(id);
            AddLog(new List<string> { ok ? $"升级了 {id}" : "升级失败" });
            return JsonSerializer.Serialize(new { ok });
        }

        private string DoHeal(HttpListenerRequest req)
        {
            if (!_game.CanSave) return Failure("请先返回基地，战斗中可使用战斗面板的药品。");
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string id = data.GetValueOrDefault("id", "");
            var npc = string.IsNullOrEmpty(id) ? null : _game.NPCs.GetNPC(id);
            if (npc == null || !npc.IsAlive || npc.Health >= GameConfig.MaxNPCHealth)
                return Failure("该居民当前无法接受治疗。");
            if (_game.Resources.GetResourceAmount(ResourceType.Medicine) <= 0)
                return Failure("没有药品。");
            _game.Resources.ConsumeResource(ResourceType.Medicine, 1);
            _game.NPCs.HealNPC(id, 30);
            AddLog(new List<string> { $"治疗了 {id}" });
            return JsonSerializer.Serialize(new { ok = true });
        }

        private string DoTalk(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string id = data.GetValueOrDefault("id", "");
            if (string.IsNullOrEmpty(id) || _game.NPCs.GetNPC(id)?.IsAlive != true)
                return Failure("该居民当前无法对话。");
            string dialogue = _game.TalkToNPC(id);
            AddLog(new List<string> { $"与 {id} 对话" });
            return JsonSerializer.Serialize(new { ok = true, dialogue });
        }

        private string DoExplore(HttpListenerRequest req) => DoExploreStart(req);

        private string DoCombat(HttpListenerRequest req)
        {
            if (_game.Exploration.IsExploring) return Failure("远征中的战斗由房间遭遇触发。");
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
            var teamIds = new List<string>();
            if (data.ContainsKey("team"))
            {
                foreach (var el in data["team"].EnumerateArray()) teamIds.Add(el.GetString());
            }
            var enemyIds = new List<string>();
            if (data.ContainsKey("enemies"))
            {
                foreach (var el in data["enemies"].EnumerateArray()) enemyIds.Add(el.GetString());
            }

            if (!_game.Combat.StartCombat(teamIds, enemyIds))
                return JsonSerializer.Serialize(new { ok = false, msg = "战斗启动失败" });

            var results = new List<string>();
            int turns = 0;
            while (_game.Combat.InCombat && turns < 20)
            {
                var result = _game.Combat.ExecutePlayerAction(CombatAction.Attack);
                if (result != null)
                {
                    results.Add(result.Victory ? "战斗胜利！" : result.Escaped ? "成功逃跑" : "战斗失败");
                    break;
                }
                turns++;
            }
            AddLog(results);
            return JsonSerializer.Serialize(new { ok = true, messages = results });
        }

        private static IEnumerable<object> FormatAmounts(IEnumerable<ResourceAmount> amounts) =>
            amounts.Select(value => new { resource = value.Type.ToString(), amount = value.Amount });

        private string ObjectiveLabel(QuestObjective objective) => objective.Type switch
        {
            ObjectiveType.CollectResource when Enum.TryParse<ResourceType>(objective.TargetId, out var type)
                => $"收集{GameConfig.ResourceNames[(int)type]}",
            ObjectiveType.BuildFacility => $"建造{_game.Buildings.GetBuilding(objective.TargetId)?.Name ?? objective.TargetId}",
            ObjectiveType.UpgradeBuilding => $"升级{_game.Buildings.GetBuilding(objective.TargetId)?.Name ?? objective.TargetId}",
            ObjectiveType.VisitLocation => $"前往{_game.Exploration.GetMap(objective.TargetId)?.Name ?? objective.TargetId}",
            ObjectiveType.TalkToNPC => $"与{_game.NPCs.GetNPC(objective.TargetId)?.Name ?? objective.TargetId}交谈",
            ObjectiveType.SurviveDays => "维持档案城运转",
            ObjectiveType.DefeatEnemy => "击败敌人",
            _ => objective.TargetId
        };

        private string GetRecipes() => JsonSerializer.Serialize(Recipes.Select(recipe =>
        {
            var item = _game.Items.GetTemplate(recipe.Key);
            return new
            {
                id = recipe.Key, name = item.Name, desc = item.Description,
                cost = FormatAmounts(recipe.Value),
                available = !_game.Items.IsFull && recipe.Value.All(price => _game.Resources.HasEnough(price.Type, price.Amount))
            };
        }));

        private string DoCraft(HttpListenerRequest req)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadBody(req));
            string recipeId = data?.GetValueOrDefault("recipeId");
            if (recipeId == null || !Recipes.TryGetValue(recipeId, out var prices)) return Failure("未知配方。");
            var template = _game.Items.GetTemplate(recipeId);
            if (template == null) return Failure("配方暂不可用。");
            if (_game.Items.IsFull) return Failure("背包已满，请先使用物品。");
            var cost = new ResourceBundle { Amounts = prices.ToList() };
            if (!_game.Resources.ConsumeBundle(cost)) return Failure("缺少合成材料。");
            _game.Items.AddItemFromTemplate(recipeId, "合成制作");
            string msg = $"制成{template.Name}";
            AddLog(msg);
            return JsonSerializer.Serialize(new { ok = true, msg });
        }

        private string GetMarket() => JsonSerializer.Serialize(new
        {
            capacity = _game.MarketCapacity,
            remaining = _game.MarketTradesLeft,
            offers = GameManager.MarketOffers.Select(offer => new
            {
                id = offer.Id,
                cost = FormatAmounts(new[] { offer.Cost }).Single(),
                reward = FormatAmounts(new[] { offer.Reward }).Single(),
                available = _game.MarketTradesLeft > 0 && _game.CanSave &&
                    _game.Resources.HasEnough(offer.Cost.Type, offer.Cost.Amount)
            })
        });

        private string DoTrade(HttpListenerRequest req)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadBody(req));
            var id = data?.GetValueOrDefault("id");
            if (!_game.Trade(id)) return Failure("集市额度不足，或缺少交换物资。");
            var offer = GameManager.MarketOffers.First(entry => entry.Id == id);
            string msg = $"换得 {offer.Reward.Amount} {GameConfig.ResourceNames[(int)offer.Reward.Type]}";
            AddLog(msg);
            return JsonSerializer.Serialize(new { ok = true, msg });
        }

        private object PresentEnding()
        {
            var ending = _game.Endings.CalculateEnding(_game);
            return new { title = ending.Title, epilogue = ending.Epilogue, type = ending.Type.ToString() };
        }

        private string DoChooseEnding(HttpListenerRequest req)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadBody(req));
            if (!Enum.TryParse<EndingType>(data?.GetValueOrDefault("type"), out var type) || !_game.ChooseEnding(type))
                return Failure("目前无法选择这个结局。");
            bool saved = _game.SaveGame();
            AddLog($"档案城已决定记忆的去向。");
            return JsonSerializer.Serialize(new { ok = true, saved, ending = PresentEnding() });
        }

        private string DoItemAction(HttpListenerRequest req, bool equip)
        {
            if (!_game.CanSave) return Failure("请先返回基地。");
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadBody(req));
            var npcId = data?.GetValueOrDefault("npcId");
            var itemId = data?.GetValueOrDefault("itemId");
            if (string.IsNullOrEmpty(npcId) || _game.NPCs.GetNPC(npcId)?.IsAlive != true)
                return Failure("请选择仍在城中的居民。");
            var item = _game.Items.GetInventory().Find(entry => entry.Id == itemId);
            if (item == null) return Failure("物品已不在背包。");
            bool ok = equip ? item.Type switch
            {
                ItemType.Weapon => _game.Items.EquipWeapon(npcId, itemId),
                ItemType.Armor => _game.Items.EquipArmor(npcId, itemId),
                _ => false
            } : _game.Items.UseConsumable(itemId, npcId);
            return ok
                ? JsonSerializer.Serialize(new { ok, msg = $"已{(equip ? "装备" : "使用")}{item.Name}" })
                : Failure(equip ? "没有空闲的同款装备。" : "无法使用此物品。");
        }

        private string GetNPCRelations()
        {
            var matrix = _game.NPCs.GetRelationshipMatrix();
            return JsonSerializer.Serialize(matrix);
        }

        private string GetCombatStatus()
        {
            if (!_game.Combat.InCombat)
            {
                return JsonSerializer.Serialize(new { inCombat = false });
            }

            var livingEnemies = _game.Combat.EnemyUnits.Where(unit => unit.IsAlive).ToList();
            var timeline = _game.Combat.ActionTimeline.Select(u => new
            {
                id = u.Id,
                name = u.Name,
                hp = u.Hp,
                maxHp = u.MaxHp,
                attack = u.Attack,
                defense = u.Defense,
                speed = u.Speed,
                isPlayerSide = u.IsPlayerSide,
                isDefending = u.IsDefending,
                role = u.Role,
                skillUsed = u.SkillUsed,
                buffDefense = u.BuffDefense,
                isCovering = u.IsCovering,
                intent = _game.Combat.EnemyIntent(u),
                incoming = _game.Combat.ChargeForecast(u),
                attackDamage = !u.IsPlayerSide && u.IsAlive && _game.Combat.IsPlayerTurn
                    ? CombatSystem.DamageAgainst(u, _game.Combat.ActiveUnit.Attack) : 0,
                targetIndex = livingEnemies.IndexOf(u),
                isDead = u.IsDead
            }).ToList();

            var active = _game.Combat.ActiveUnit;

            return JsonSerializer.Serialize(new
            {
                inCombat = true,
                timeline,
                currentIndex = _game.Combat.CurrentTimelineIndex,
                activeUnitId = active?.Id ?? "",
                isPlayerTurn = _game.Combat.IsPlayerTurn,
                combatLog = _game.Combat.CombatLog.ToString()
            });
        }

        private string GetExploreStatus()
        {
            if (!_game.Exploration.IsExploring)
            {
                var maps = _game.Exploration.GetAllMaps().Where(map => map.Unlocked).Select(map => new
                {
                    id = map.Id,
                    name = map.Name,
                    desc = map.Description,
                    danger = map.Rooms.Max(room => room.DangerLevel),
                    rooms = map.Rooms.Count,
                    cacheNotes = map.Rooms.Where(room => room.IsFiniteCache || room.EventType == ExplorationEventType.FindMemoryShard)
                        .Select(room => room.Name + (room.Claimed ? "已取完" : "可回收")),
                    lockedReason = _game.ExpeditionLockReason(map.Id),
                    loot = map.MainLoot.Select(type => GameConfig.ResourceNames[(int)type])
                });
                var eligible = _game.NPCs.GetAllNPCs()
                    .Where(_game.CanExploreResident).Select(npc => npc.Id);
                return JsonSerializer.Serialize(new
                {
                    isExploring = false, maps, eligible,
                    departuresLeft = _game.DeparturesLeft,
                    foodCost = _game.DepartureFood,
                    waterCost = _game.DepartureWater,
                    affordable = _game.CanAffordDeparture
                });
            }

            var connected = _game.Exploration.GetConnectedRooms().Select(r => new
            {
                id = r.Id,
                name = r.Name,
                desc = r.Description,
                visited = r.Visited,
                danger = r.DangerLevel,
                locked = r.Locked,
                canEnter = _game.Exploration.HasSurvivors && _game.Exploration.CanEnter(r),
                blocked = !_game.Exploration.HasClearance(r) ? "需先清除走廊守卫" : "",
                requiredItem = r.RequiredItem ?? "",
                keyName = string.IsNullOrEmpty(r.RequiredItem) ? "" : _game.Items.GetTemplate(r.RequiredItem)?.Name ?? r.RequiredItem
            }).ToList();

            var team = _game.Exploration.TeamNPCIds.Select(id =>
            {
                var npc = _game.NPCs.GetNPC(id);
                return new
                {
                    id,
                    name = npc?.Name ?? "",
                    role = npc?.Role.ToString() ?? "",
                    health = npc?.Health ?? 0,
                    morale = npc?.Morale ?? 0,
                    fatigue = npc?.Fatigue ?? 0,
                    engineering = npc?.Engineering ?? 0,
                    medical = npc?.Medical ?? 0,
                    combat = npc?.Combat ?? 0
                };
            }).ToList();

            object checkInfo = null;
            if (_game.Exploration.ActiveCheck != null)
            {
                checkInfo = new
                {
                    desc = _game.Exploration.ActiveCheck.Description,
                    skill = _game.Exploration.ActiveCheck.TargetSkill,
                    difficulty = _game.Exploration.ActiveCheck.Difficulty
                };
            }

            return JsonSerializer.Serialize(new
            {
                isExploring = true,
                mapId = _game.Exploration.CurrentMap?.Id ?? "",
                mapName = _game.Exploration.CurrentMap?.Name ?? "",
                roomId = _game.Exploration.CurrentRoom?.Id ?? "",
                roomName = _game.Exploration.CurrentRoom?.Name ?? "",
                roomDesc = _game.Exploration.CurrentRoom?.Description ?? "",
                danger = _game.Exploration.CurrentRoom?.DangerLevel ?? 0,
                searched = _game.Exploration.SearchedRooms.Contains(_game.Exploration.CurrentRoom?.Id ?? ""),
                hasSurvivors = _game.Exploration.HasSurvivors,
                offerBypass = _game.Exploration.CanOfferTowerBypass,
                canBypass = _game.Exploration.CanBypassTower,
                bypassPower = ExplorationSystem.BypassPower,
                bypassEngineering = ExplorationSystem.BypassEngineering,
                bypassRequirements = ExplorationSystem.BypassRequirements,
                bypassIssue = _game.Exploration.TowerBypassIssue,
                searchRequirement = _game.Exploration.SearchRequirement,
                connectedRooms = connected,
                fatigueOnReturn = Math.Min(20, 6 + 4 * _game.Exploration.SearchedRooms.Count),
                loot = FormatAmounts(_game.Exploration.CurrentResult.CollectedResources),
                activeCheck = checkInfo,
                team
            });
        }

        private string DoExploreStart(HttpListenerRequest req)
        {
            if (_game.Combat.InCombat) return Failure("请先完成当前战斗。");
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
            string mapId = data.ContainsKey("map") ? data["map"].GetString() : "";
            string locked = _game.ExpeditionLockReason(mapId);
            if (locked.Length > 0) return Failure(locked);
            if (_game.DeparturesLeft == 0) return Failure("今日远征名额已用完。");
            if (!_game.CanAffordDeparture) return Failure("远征出发补给不足。");
            var teamIds = new List<string>();
            if (data.ContainsKey("team"))
            {
                foreach (var el in data["team"].EnumerateArray()) teamIds.Add(el.GetString());
            }

            bool ok = _game.Exploration.StartExploration(mapId, teamIds);
            if (ok)
            {
                AddLog($"开启了 {mapId} 的探索之旅");
            }
            return JsonSerializer.Serialize(new { ok, msg = ok ? "" : "启动探索失败" });
        }

        private string DoExploreMove(HttpListenerRequest req)
        {
            if (_game.Combat.InCombat) return Failure("请先完成当前战斗。");
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string roomId = data.GetValueOrDefault("roomId", "");
            bool ok = _game.Exploration.MoveToRoom(roomId);
            if (ok) AddLog($"队伍移动到了房间: {_game.Exploration.CurrentRoom.Name}");
            return JsonSerializer.Serialize(new { ok, msg = ok ? "" : "无法移动到指定房间" });
        }

        private string DoExploreSearch()
        {
            if (_game.Combat.InCombat) return Failure("请先完成当前战斗。");
            if (!_game.Exploration.IsExploring)
            {
                return JsonSerializer.Serialize(new { ok = false, msg = "未在探索中" });
            }

            var roomName = _game.Exploration.CurrentRoom.Name;
            var evt = _game.SearchExpeditionRoom();

            if (evt != null)
            {
                AddLog($"搜寻了 {roomName}，结果: {evt.Description}");

                if (evt.Type == ExplorationEventType.EnemyEncounter)
                {
                    AddLog($"警告！在 {roomName} 遭遇敌人，被迫进入战斗！");
                }

                object rewardInfo = evt.Rewards.Select(r => new { type = r.Type.ToString(), amount = r.Amount }).ToList();

                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    type = evt.Type.ToString(),
                    desc = evt.Description,
                    rewards = rewardInfo,
                    enemyId = evt.EnemyId ?? "",
                    combatTriggered = _game.Combat.InCombat
                });
            }

            var issue = _game.Exploration.SearchRequirement;
            return Failure(issue.Length > 0 ? issue : "这里现在不能搜寻。");
        }

        private string DoBypassTower()
        {
            if (_game.Combat.InCombat) return Failure("无法在战斗中接线。");
            if (!_game.Exploration.BypassTower()) return Failure(_game.Exploration.TowerBypassIssue);
            const string message = "已绕过守备队并取得校准数据，可以返回营地。";
            AddLog(message);
            return JsonSerializer.Serialize(new { ok = true, msg = message });
        }

        private string DoInstallRelay()
        {
            if (!_game.InstallRelay()) return Failure("缺少校准数据或安装材料。");
            const string message = "广播组件已安装。请检查日终试送电条件。";
            AddLog(message);
            return JsonSerializer.Serialize(new { ok = true, msg = message });
        }

        private string DoExploreSkillCheck(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string npcId = data.GetValueOrDefault("npcId", "");

            string resultText;
            bool success;
            bool ok = _game.Exploration.ResolveSkillCheck(npcId, out resultText, out success);

            if (ok)
            {
                AddLog($"指派 {npcId} 执行技能判定：{(success ? "成功" : "失败")}。{resultText}");
            }

            return JsonSerializer.Serialize(new { ok, success, resultText });
        }

        private string DoExploreReturn()
        {
            if (_game.Combat.InCombat) return Failure("请先完成当前战斗。");
            if (!_game.Exploration.IsExploring)
            {
                return JsonSerializer.Serialize(new { ok = false, msg = "未在探索中" });
            }

            var survived = _game.Exploration.HasSurvivors;
            var result = _game.Exploration.ReturnToBase();
            if (result != null)
            {
                AddLog(survived ? "探索队伍已返回基地。" : "远征小队全灭，营地未收到新的现场档案。");
                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    survived,
                    loot = result.CollectedResources.Select(r => new { type = r.Type.ToString(), amount = r.Amount }).ToList(),
                    injured = result.InjuredNPCs
                });
            }

            return JsonSerializer.Serialize(new { ok = false, msg = "返回基地失败" });
        }

        private string DoCombatStart(HttpListenerRequest req)
        {
            if (_game.Exploration.IsExploring) return Failure("远征中的战斗由房间遭遇触发。");
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
            var teamIds = new List<string>();
            if (data.ContainsKey("team"))
            {
                foreach (var el in data["team"].EnumerateArray()) teamIds.Add(el.GetString());
            }
            var enemyIds = new List<string>();
            if (data.ContainsKey("enemies"))
            {
                foreach (var el in data["enemies"].EnumerateArray()) enemyIds.Add(el.GetString());
            }

            bool ok = _game.Combat.StartCombat(teamIds, enemyIds);
            if (ok)
            {
                AddLog("拉响警报！战斗开始！");
            }
            return JsonSerializer.Serialize(new { ok, msg = ok ? "" : "启动战斗失败" });
        }

        private string DoCombatAction(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
            string actionStr = data.ContainsKey("action") ? data["action"].GetString() : "Attack";
            int targetIndex = data.ContainsKey("targetIndex") ? data["targetIndex"].GetInt32() : 0;
            string itemId = data.ContainsKey("itemId") ? data["itemId"].GetString() : "";

            if (!_game.Combat.InCombat) return Failure("当前没有战斗。");
            if (targetIndex < 0 || !Enum.TryParse<CombatAction>(actionStr, out var action) || !Enum.IsDefined(action))
                return Failure("无效的战斗行动。");

            var result = _game.Combat.ExecutePlayerAction(action, targetIndex, itemId);

            bool combatEnded = (result != null);
            object rewardsList = null;
            List<string> injuredNPCs = null;

            if (combatEnded)
            {
                rewardsList = result.Rewards.Select(r => new { type = r.Type.ToString(), amount = r.Amount }).ToList();
                injuredNPCs = result.InjuredNPCs;
                AddLog($"战斗结束：{(result.Victory ? "胜利" : result.Escaped ? "成功撤退" : "我方失败")}");
            }

            return JsonSerializer.Serialize(new
            {
                ok = true,
                combatEnded,
                victory = result?.Victory ?? false,
                escaped = result?.Escaped ?? false,
                combatLog = _game.Combat.CombatLog.ToString(),
                rewards = rewardsList,
                injured = injuredNPCs
            });
        }

        // ═══════════════ 工具 ═══════════════

        private string ReadBody(HttpListenerRequest req)
        {
            using var rs = req.InputStream;
            using var sr = new System.IO.StreamReader(rs, Encoding.UTF8);
            return sr.ReadToEnd();
        }

        private void AddLog(List<string> msgs) { _log.AddRange(msgs); if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200); }
        private void AddLog(string msg) { AddLog(new List<string> { msg }); }

    }

}
