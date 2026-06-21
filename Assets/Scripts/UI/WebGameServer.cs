// ============================================================
// Last Archive - Web 服务器
// C# HttpListener + HTML/JS 前端
// ============================================================

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace LastArchive
{
    public class WebGameServer
    {
        private HttpListener _listener;
        private GameManager _game;
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
            _game = new GameManager();
            _game.Initialize(new MockAIProvider());
            _game.StartNewGame();

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
                    HandleRequest(ctx);
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
                string response = "";
                string contentType = "application/json";

                if (path == "/" || path == "/index.html")
                {
                    response = GetIndexHtml();
                    contentType = "text/html; charset=utf-8";
                }
                else if (path.StartsWith("/api/"))
                {
                    response = HandleApi(path, method, ctx.Request);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    SendResponse(ctx, "Not Found", "text/plain");
                    return;
                }

                SendResponse(ctx, response, contentType);
            }
            catch (Exception ex)
            {
                SendResponse(ctx, JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
            }
        }

        private void SendResponse(HttpListenerContext ctx, string body, string contentType)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }

        // ═══════════════ API 路由 ═══════════════

        private string HandleApi(string path, string method, HttpListenerRequest req)
        {
            // GET /api/status - 游戏状态概览
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
            if (path == "/api/nextday" && method == "POST") return DoNextDay();
            // POST /api/save - 保存
            if (path == "/api/save" && method == "POST") return DoSave();
            // POST /api/load - 读档
            if (path == "/api/load" && method == "POST") return DoLoad();
            // POST /api/newgame - 新游戏
            if (path == "/api/newgame" && method == "POST") return DoNewGame();
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
            // GET /api/ai/config - 获取AI配置
            if (path == "/api/ai/config" && method == "GET") return GetAIConfig();

            // POST /api/ai/config - 配置AI
            if (path == "/api/ai/config" && method == "POST") return DoAIConfig(req);
            // POST /api/ai/test - 测试AI
            if (path == "/api/ai/test" && method == "POST") return DoAITest(req);
            // POST /api/explore/start - 开始探索
            if (path == "/api/explore/start" && method == "POST") return DoExploreStart(req);
            // POST /api/explore/move - 探索移动
            if (path == "/api/explore/move" && method == "POST") return DoExploreMove(req);
            // POST /api/explore/search - 探索搜索
            if (path == "/api/explore/search" && method == "POST") return DoExploreSearch();
            // POST /api/explore/skillcheck - 探索判定
            if (path == "/api/explore/skillcheck" && method == "POST") return DoExploreSkillCheck(req);
            // POST /api/explore/return - 探索返城
            if (path == "/api/explore/return" && method == "POST") return DoExploreReturn();
            // POST /api/combat/start - 开始战斗
            if (path == "/api/combat/start" && method == "POST") return DoCombatStart(req);
            // POST /api/combat/action - 战斗动作
            if (path == "/api/combat/action" && method == "POST") return DoCombatAction(req);

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

        private string GetNPCs()
        {
            var npcs = new List<object>();
            foreach (var n in _game.NPCs.GetAllNPCs())
            {
                var mental = _game.Psychology.GetState(n.Id);
                npcs.Add(new
                {
                    id = n.Id, name = n.Name, age = n.Age, role = n.Role.ToString(),
                    isAlive = n.IsAlive, health = n.Health, morale = n.Morale,
                    loyalty = n.Loyalty, hunger = n.Hunger, fatigue = n.Fatigue,
                    status = n.Status.ToString(), work = n.CurrentWork.ToString(),
                    mental = _game.Psychology.GetStateName(mental), mentalKey = mental.ToString(),
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
                    built = b.Built, level = b.Level, maxLevel = b.MaxLevel
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
                    objectives = q.Objectives.Select(o => new
                    {
                        type = o.Type.ToString(), target = o.TargetId,
                        current = o.CurrentProgress, required = o.RequiredAmount
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
                    attack = i.AttackBonus, defense = i.DefenseBonus, heal = i.HealAmount
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

        private string GetAIConfig()
        {
            string url = "";
            string key = "";
            string model = "";
            string provider = "Mock";

            if (_game.AIProvider != null && _game.AIProvider.Name != "MockAI")
            {
                provider = "OpenAI";
            }

            if (System.IO.File.Exists("ai_config.json"))
            {
                try
                {
                    string json = System.IO.File.ReadAllText("ai_config.json");
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("ApiBaseUrl", out var u)) url = u.GetString();
                        if (root.TryGetProperty("ApiKey", out var k)) key = k.GetString();
                        if (root.TryGetProperty("Model", out var m)) model = m.GetString();
                        if (provider != "Mock") provider = "OpenAI";
                    }
                }
                catch { }
            }

            return JsonSerializer.Serialize(new
            {
                provider,
                apiBaseUrl = url,
                apiKey = key,
                model
            });
        }

        // ═══════════════ POST API ═══════════════

        private string DoNextDay()
        {
            if (_game.IsGameOver) return JsonSerializer.Serialize(new { ok = false, msg = "游戏已结束" });
            if (_game.Time.CurrentPhase == GamePhase.Day) _game.Time.AdvanceToNight();
            _game.Time.AdvanceToSummary();
            _game.OnDayEnd();

            var result = new List<string>();
            result.Add($"第{_game.Time.CurrentDay}天结算完成");
            if (!string.IsNullOrEmpty(_game.LastDailyEvent)) result.Add(_game.LastDailyEvent);
            foreach (var c in _game.Crises.ActiveCrises) result.Add($"危机: {c.Type}");
            if (_game.IsGameOver) result.Add(_game.IsVictory ? "胜利！" : "游戏结束");

            AddLog(result);
            return JsonSerializer.Serialize(new { ok = true, messages = result, gameOver = _game.IsGameOver, victory = _game.IsVictory });
        }

        private string DoSave()
        {
            bool ok = _game.SaveGame();
            AddLog(ok ? "保存成功" : "保存失败");
            return JsonSerializer.Serialize(new { ok });
        }

        private string DoLoad()
        {
            bool ok = _game.LoadGame();
            AddLog(ok ? "读档成功" : "读档失败");
            return JsonSerializer.Serialize(new { ok });
        }

        private string DoNewGame()
        {
            _game = new GameManager();
            _game.Initialize(new MockAIProvider());
            _game.StartNewGame();
            _log.Clear();
            AddLog(new List<string> { "新游戏开始！" });
            return JsonSerializer.Serialize(new { ok = true });
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
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string id = data.GetValueOrDefault("id", "");
            if (_game.Resources.GetResourceAmount(ResourceType.Medicine) <= 0)
                return JsonSerializer.Serialize(new { ok = false, msg = "没有药品" });
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
            string dialogue = _game.TalkToNPC(id);
            AddLog(new List<string> { $"与 {id} 对话" });
            return JsonSerializer.Serialize(new { ok = true, dialogue });
        }

        private string DoExplore(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
            string mapId = data.ContainsKey("map") ? data["map"].GetString() : "";
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

        private string DoCombat(HttpListenerRequest req)
        {
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
                return JsonSerializer.Serialize(new { isExploring = false });
            }

            var connected = _game.Exploration.GetConnectedRooms().Select(r => new
            {
                id = r.Id,
                name = r.Name,
                desc = r.Description,
                visited = r.Visited,
                danger = r.DangerLevel,
                locked = r.Locked,
                requiredItem = r.RequiredItem ?? ""
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
                connectedRooms = connected,
                activeCheck = checkInfo,
                team
            });
        }

        private string DoAIConfig(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string url = data.GetValueOrDefault("apiBaseUrl", "");
            string key = data.GetValueOrDefault("apiKey", "");
            string model = data.GetValueOrDefault("model", "");
            string provider = data.GetValueOrDefault("provider", "");

            IAIProvider newProvider;
            if (provider == "Mock")
            {
                newProvider = new MockAIProvider();
            }
            else
            {
                newProvider = new OpenAIProvider(new AIProviderConfig
                {
                    ApiBaseUrl = url,
                    ApiKey = key,
                    Model = model
                });
            }

            _game.UpdateAIProvider(newProvider);

            try
            {
                var cfg = new AIProviderConfig
                {
                    ApiBaseUrl = url,
                    ApiKey = key,
                    Model = model
                };
                string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText("ai_config.json", json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config Save Error] {ex.Message}");
            }

            AddLog($"AI配置更新成功：模型={model}");
            return JsonSerializer.Serialize(new { ok = true });
        }

        private string DoAITest(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string url = data.GetValueOrDefault("apiBaseUrl", "");
            string key = data.GetValueOrDefault("apiKey", "");
            string model = data.GetValueOrDefault("model", "");
            string provider = data.GetValueOrDefault("provider", "");

            if (provider == "Mock")
            {
                return JsonSerializer.Serialize(new { ok = true, latencyMs = 0 });
            }

            var testProvider = new OpenAIProvider(new AIProviderConfig
            {
                ApiBaseUrl = url,
                ApiKey = key,
                Model = model,
                TimeoutSeconds = 10
            });

            string error;
            long latencyMs;
            bool ok = testProvider.TestConnection(out error, out latencyMs);

            return JsonSerializer.Serialize(new { ok, error, latencyMs });
        }

        private string DoExploreStart(HttpListenerRequest req)
        {
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
            string mapId = data.ContainsKey("map") ? data["map"].GetString() : "";
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
            var body = ReadBody(req);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string roomId = data.GetValueOrDefault("roomId", "");
            bool ok = _game.Exploration.MoveToRoom(roomId);
            if (ok)
            {
                _game.Quests.UpdateVisitLocationObjectives(roomId);
                AddLog($"队伍移动到了房间: {_game.Exploration.CurrentRoom.Name}");
            }
            return JsonSerializer.Serialize(new { ok, msg = ok ? "" : "无法移动到指定房间" });
        }

        private string DoExploreSearch()
        {
            if (!_game.Exploration.IsExploring)
            {
                return JsonSerializer.Serialize(new { ok = false, msg = "未在探索中" });
            }

            var roomName = _game.Exploration.CurrentRoom.Name;
            var evt = _game.Exploration.SearchRoom();

            if (evt != null)
            {
                AddLog($"搜寻了 {roomName}，结果: {evt.Description}");

                if (evt.Type == ExplorationEventType.EnemyEncounter && !string.IsNullOrEmpty(evt.EnemyId))
                {
                    var teamIds = _game.Exploration.TeamNPCIds;
                    _game.Combat.StartCombat(teamIds, new List<string> { evt.EnemyId });
                    AddLog($"警告！在 {roomName} 遭遇敌人，被迫进入战斗！");
                }

                foreach (var rt in Enum.GetValues(typeof(ResourceType)))
                {
                    _game.Quests.UpdateResourceObjectives((ResourceType)rt, _game.Resources.GetResourceAmount((ResourceType)rt));
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

            return JsonSerializer.Serialize(new { ok = false, msg = "搜寻失败" });
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
            if (!_game.Exploration.IsExploring)
            {
                return JsonSerializer.Serialize(new { ok = false, msg = "未在探索中" });
            }

            var result = _game.Exploration.ReturnToBase();
            if (result != null)
            {
                AddLog($"探索队伍安全返回基地，带回战利品！");
                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    loot = result.CollectedResources.Select(r => new { type = r.Type.ToString(), amount = r.Amount }).ToList(),
                    injured = result.InjuredNPCs
                });
            }

            return JsonSerializer.Serialize(new { ok = false, msg = "返回基地失败" });
        }

        private string DoCombatStart(HttpListenerRequest req)
        {
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

            CombatAction action = CombatAction.Attack;
            Enum.TryParse(actionStr, out action);

            var result = _game.Combat.ExecutePlayerAction(action, targetIndex, itemId);

            if (result != null && result.Victory)
            {
                _game.Quests.UpdateObjectiveProgress("", ObjectiveType.DefeatEnemy, "", 1);
            }

            foreach (var rt in Enum.GetValues(typeof(ResourceType)))
            {
                _game.Quests.UpdateResourceObjectives((ResourceType)rt, _game.Resources.GetResourceAmount((ResourceType)rt));
            }

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

        private List<string> _log = new List<string>();
        private void AddLog(List<string> msgs) { _log.AddRange(msgs); if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200); }
        private void AddLog(string msg) { AddLog(new List<string> { msg }); }

        // ═══════════════ HTML 前端 ═══════════════

        private string GetIndexHtml()
        {
            return @"<!DOCTYPE html>
<html lang='zh-CN'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>最后档案城 - Command Terminal</title>
<link href='https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;700&family=Orbitron:wght@400;600;800;900&family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap' rel='stylesheet'>
<style>
:root {
  /* Dark Theme (Default) */
  --bg-dark: #06060a;
  --bg-gradient: radial-gradient(circle at 50% 50%, #131326 0%, #050509 100%);
  --panel-bg: rgba(18, 18, 28, 0.7);
  --border-glow: rgba(80, 180, 255, 0.12);
  --border-glow-danger: rgba(239, 68, 68, 0.25);
  --border-glow-success: rgba(16, 185, 129, 0.25);
  
  --color-food: #10b981;
  --color-water: #06b6d4;
  --color-power: #fbbf24;
  --color-med: #ec4899;
  --color-parts: #94a3b8;
  --color-shards: #a855f7;
  
  --text-primary: #f8fafc;
  --text-secondary: #94a3b8;
  --text-accent: #38bdf8;
  --text-accent-dim: #0284c7;
  --sidebar-bg: rgba(12, 12, 20, 0.8);
  --card-bg: rgba(24, 24, 38, 0.65);
  --log-bg: #040406;
  --log-text: #10b981;
  --log-shadow: rgba(16, 185, 129, 0.3);
  --btn-bg: rgba(30, 30, 45, 0.6);
  --btn-border: rgba(255, 255, 255, 0.08);
  --tabs-bg: rgba(15, 15, 25, 0.6);
  --tabs-btn-active: rgba(56, 189, 248, 0.12);
  --topbar-bg: rgba(20, 20, 32, 0.85);
  --sidebar-status-box-bg: rgba(0, 0, 0, 0.35);
  --scanlines-opacity: 0.12;
}

body.light-theme {
  /* Light Theme */
  --bg-dark: #f1f5f9;
  --bg-gradient: radial-gradient(circle at 50% 50%, #ffffff 0%, #e2e8f0 100%);
  --panel-bg: rgba(255, 255, 255, 0.75);
  --border-glow: rgba(2, 132, 199, 0.15);
  --border-glow-danger: rgba(239, 68, 68, 0.25);
  --border-glow-success: rgba(16, 185, 129, 0.25);
  
  --color-food: #059669;
  --color-water: #0891b2;
  --color-power: #d97706;
  --color-med: #db2777;
  --color-parts: #64748b;
  --color-shards: #7c3aed;
  
  --text-primary: #0f172a;
  --text-secondary: #475569;
  --text-accent: #0284c7;
  --text-accent-dim: #0369a1;
  --sidebar-bg: rgba(241, 245, 249, 0.95);
  --card-bg: rgba(255, 255, 255, 0.85);
  --log-bg: #f8fafc;
  --log-text: #166534;
  --log-shadow: rgba(22, 101, 52, 0.1);
  --btn-bg: rgba(255, 255, 255, 0.9);
  --btn-border: rgba(15, 23, 42, 0.08);
  --tabs-bg: rgba(226, 232, 240, 0.7);
  --tabs-btn-active: rgba(2, 132, 199, 0.1);
  --topbar-bg: rgba(241, 245, 249, 0.95);
  --sidebar-status-box-bg: rgba(255, 255, 255, 0.6);
  --scanlines-opacity: 0.03;
}

*{margin:0;padding:0;box-sizing:border-box}
body{
  background: var(--bg-dark);
  background-image: var(--bg-gradient);
  color: var(--text-primary);
  font-family: 'Plus Jakarta Sans', -apple-system, sans-serif;
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
  position: relative;
  transition: background 0.3s, color 0.3s;
}

/* CRT Scanlines overlay */
body::before {
  content: ' ';
  display: block;
  position: absolute;
  top: 0; left: 0; bottom: 0; right: 0;
  background: linear-gradient(rgba(18, 16, 16, 0) 50%, rgba(0, 0, 0, 0.2) 50%);
  z-index: 999;
  background-size: 100% 4px;
  pointer-events: none;
  opacity: var(--scanlines-opacity);
  transition: opacity 0.3s;
}

.topbar{
  background: var(--topbar-bg);
  backdrop-filter: blur(12px);
  padding: 12px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--border-glow);
  flex-shrink: 0;
  box-shadow: 0 4px 24px rgba(0,0,0,0.1);
  transition: background 0.3s, border 0.3s;
}

.topbar .title-container {
  display: flex;
  align-items: center;
  gap: 12px;
}

.topbar .status-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #fbbf24;
  box-shadow: 0 0 10px #fbbf24;
  transition: all 0.4s;
}

.topbar .title{
  color: var(--text-accent);
  font-family: 'Orbitron', sans-serif;
  font-weight: 800;
  font-size: 17px;
  letter-spacing: 1px;
  text-shadow: 0 0 10px var(--border-glow);
}

.topbar .res{
  display: flex;
  gap: 10px;
  font-size: 13px;
}

.topbar .res-card {
  background: var(--btn-bg);
  border: 1px solid var(--btn-border);
  padding: 5px 12px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: all 0.3s;
}

.topbar .res-card:hover {
  background: var(--btn-bg);
  border-color: var(--text-accent);
  transform: translateY(-1px);
}

.main{display:flex;flex:1;overflow:hidden}

.sidebar{
  width: 220px;
  background: var(--sidebar-bg);
  backdrop-filter: blur(12px);
  padding: 20px 16px;
  overflow-y: auto;
  border-right: 1px solid var(--btn-border);
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 16px;
  transition: background 0.3s, border 0.3s;
}

.sidebar h3{
  color: var(--text-secondary);
  font-family: 'Orbitron', sans-serif;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 1.5px;
  margin-bottom: 6px;
  border-left: 2px solid var(--text-accent);
  padding-left: 6px;
}

.sidebar button{
  display: flex;
  align-items: center;
  gap: 12px;
  width:100%;
  padding: 10px 14px;
  background: var(--btn-bg);
  color: var(--text-primary);
  border: 1px solid var(--btn-border);
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  text-align: left;
  transition: all 0.2s ease;
}

.sidebar button:hover{
  background: var(--btn-bg);
  border-color: var(--text-accent);
  box-shadow: 0 0 12px var(--border-glow);
  transform: translateY(-1px);
}

.sidebar button.danger{
  border-color: rgba(239, 68, 68, 0.4);
  color: #f87171;
}
.sidebar button.danger:hover {
  background: rgba(239, 68, 68, 0.1);
  border-color: #ef4444;
  box-shadow: 0 0 12px rgba(239, 68, 68, 0.2);
}

.sidebar button.danger.active {
  background: rgba(239, 68, 68, 0.2);
  border-color: #ef4444;
  box-shadow: 0 0 12px rgba(239, 68, 68, 0.4);
  color: #fff;
  animation: pulse-danger-border 1.5s infinite;
}

@keyframes pulse-danger-border {
  0% { border-color: rgba(239, 68, 68, 0.6); }
  50% { border-color: rgba(239, 68, 68, 1); }
  100% { border-color: rgba(239, 68, 68, 0.6); }
}

.sidebar button.gold{
  border-color: rgba(245, 158, 11, 0.4);
  color: #fbbf24;
}
.sidebar button.gold:hover {
  background: rgba(245, 158, 11, 0.1);
  border-color: #f59e0b;
  box-shadow: 0 0 12px rgba(245, 158, 11, 0.2);
}

.sidebar-status-box {
  background: var(--sidebar-status-box-bg);
  border: 1px solid var(--btn-border);
  padding: 12px;
  border-radius: 8px;
  font-size: 12px;
  color: var(--text-secondary);
  line-height: 1.4;
  transition: background 0.3s, border 0.3s;
}

.content{
  flex: 1;
  overflow-y: auto;
  padding: 24px;
  background: rgba(10, 10, 15, 0.02);
}

.tabs{
  display: flex;
  gap: 6px;
  background: var(--tabs-bg);
  padding: 4px;
  border-radius: 8px;
  border: 1px solid var(--btn-border);
  margin-bottom: 20px;
  transition: background 0.3s, border 0.3s;
}

.tabs button{
  padding: 8px 16px;
  background: none;
  border: none;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  border-radius: 6px;
  transition: all 0.2s;
}

.tabs button.active{
  background: var(--tabs-btn-active);
  color: var(--text-accent);
  font-weight: 600;
  box-shadow: inset 0 0 8px var(--border-glow);
}

.card{
  background: var(--card-bg);
  backdrop-filter: blur(12px);
  border-radius: 10px;
  padding: 16px;
  margin-bottom: 12px;
  border: 1px solid var(--btn-border);
  box-shadow: 0 4px 16px rgba(0,0,0,0.05);
  transition: all 0.3s, background 0.3s, border 0.3s;
}

.card:hover {
  border-color: var(--text-accent);
  box-shadow: 0 4px 18px var(--border-glow);
}

.card h4{
  color: var(--text-accent);
  margin-bottom: 8px;
  font-size: 14px;
  font-family: 'Orbitron', sans-serif;
  letter-spacing: 0.5px;
}

.card p{
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.6;
}

.bar{
  height: 8px;
  background: rgba(0, 0, 0, 0.15);
  border-radius: 4px;
  margin-top: 6px;
  overflow: hidden;
  border: 1px solid var(--btn-border);
}

.bar .fill{
  height: 100%;
  border-radius: 4px;
  transition: width 0.6s cubic-bezier(0.1, 0.8, 0.2, 1);
  box-shadow: 0 0 8px currentColor;
}

.tag{
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
  margin: 2px;
}

.tag.green{background:rgba(16, 185, 129, 0.15);color:#10b981;border:1px solid rgba(16, 185, 129, 0.2)}
.tag.red{background:rgba(239, 68, 68, 0.15);color:#f87171;border:1px solid rgba(239, 68, 68, 0.2)}
.tag.yellow{background:rgba(245, 158, 11, 0.15);color:#fbbf24;border:1px solid rgba(245, 158, 11, 0.2)}
.tag.blue{background:rgba(59, 130, 246, 0.15);color:#60a5fa;border:1px solid rgba(59, 130, 246, 0.2)}
.tag.purple{background:rgba(168, 85, 247, 0.15);color:#c084fc;border:1px solid rgba(168, 85, 247, 0.2)}
.tag.gray{background:rgba(148, 163, 184, 0.15);color:#94a3b8;border:1px solid rgba(148, 163, 184, 0.2)}

.log{
  background: var(--log-bg);
  border-top: 1px solid var(--border-glow);
  padding: 12px 20px;
  height: 140px;
  overflow-y: auto;
  font-family: 'JetBrains Mono', monospace;
  font-size: 12px;
  color: var(--log-text);
  text-shadow: 0 0 4px var(--log-shadow);
  box-shadow: inset 0 4px 20px rgba(0, 0, 0, 0.05);
  flex-shrink: 0;
  position: relative;
  transition: background 0.3s, color 0.3s, border 0.3s;
}

.log div{
  padding: 2px 0;
  line-height: 1.4;
  border-bottom: 1px solid rgba(16, 185, 129, 0.03);
}

.btn-sm{
  padding: 5px 12px;
  font-size: 12px;
  font-weight: 500;
  border-radius: 4px;
  background: var(--btn-bg);
  border: 1px solid var(--btn-border);
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.2s, background 0.3s, border 0.3s;
}

.btn-sm:hover{
  background: var(--btn-bg);
  border-color: var(--text-accent);
  box-shadow: 0 0 8px var(--border-glow);
}

.btn-sm.red{
  border-color: rgba(239, 68, 68, 0.4);
  color: #f87171;
}
.btn-sm.red:hover {
  background: rgba(239, 68, 68, 0.1);
  border-color: #ef4444;
  box-shadow: 0 0 8px rgba(239, 68, 68, 0.2);
}

/* NPC Grid layout */
.npc-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 16px;
}
.npc-card {
  position: relative;
  overflow: hidden;
}
.npc-card.injured {
  border-color: rgba(239, 68, 68, 0.3);
  box-shadow: 0 0 12px rgba(239, 68, 68, 0.08);
}
.npc-card::after {
  content: '';
  position: absolute;
  top: 0; left: 0; right: 0; height: 3px;
  background: var(--text-accent);
}
.npc-card.injured::after {
  background: #ef4444;
}
.npc-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}
.npc-stat-row {
  margin: 6px 0;
}

/* Building Layout */
.building-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}
.building-card {
  position: relative;
}
.building-card::after {
  content: '';
  position: absolute;
  top: 0; left: 0; right: 0; height: 3px;
  background: #fbbf24;
}

/* Item Grid */
.item-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 12px;
}

/* Pulse animation for active crisis */
.crisis-alert {
  animation: crisis-pulse 2s infinite;
  border-color: #ef4444 !important;
}
@keyframes crisis-pulse {
  0% { box-shadow: 0 0 0 0 rgba(239, 68, 68, 0.4); }
  70% { box-shadow: 0 0 0 10px rgba(239, 68, 68, 0); }
  100% { box-shadow: 0 0 0 0 rgba(239, 68, 68, 0); }
}

/* Config inputs styling */
input[type='text'], input[type='password'], select {
  width: 100%;
  padding: 10px 14px;
  background: rgba(0, 0, 0, 0.25);
  border: 1px solid var(--btn-border);
  border-radius: 6px;
  color: var(--text-primary);
  font-family: inherit;
  font-size: 13px;
  margin-top: 6px;
  margin-bottom: 16px;
  transition: all 0.3s;
}
input[type='text']:focus, input[type='password']:focus, select:focus {
  border-color: var(--text-accent);
  outline: none;
  box-shadow: 0 0 8px var(--border-glow);
}
.light-theme input[type='text'], .light-theme input[type='password'], .light-theme select {
  background: rgba(255, 255, 255, 0.85);
}
</style>
</head>
<body>

<div class='topbar'>
  <div class='title-container'>
    <div class='status-dot' id='statusDot'></div>
    <div class='title' id='title'>第1天 ☀白天</div>
    <button class='btn-sm' id='themeToggleBtn' style='margin-left: 12px; font-size: 11px' onclick='toggleTheme()'>☀️ 明亮模式</button>
  </div>
  <div class='res' id='resbar'>加载中...</div>
</div>

<div class='main'>
  <div class='sidebar'>
    <h3>控制面板</h3>
    <button onclick='api(""nextday"")'>⏭ 推进到下一天</button>
    <button onclick='api(""save"")'>💾 保存当前进度</button>
    <button onclick='api(""load"")'>📂 读取历史存档</button>
    <button class='gold' onclick='api(""newgame"")'>🆕 开启全新游戏</button>
    <button class='danger' id='autoplayBtn' onclick='autoPlay()'>🤖 开启自动游玩</button>
    
    <h3 style='margin-top:12px'>活跃危机</h3>
    <div id='crisisSidebar' class='sidebar-status-box'>无</div>
    
    <h3 style='margin-top:12px'>当前结局判定</h3>
    <div id='endingSidebar' class='sidebar-status-box' style='color:#fbbf24; font-family:""Orbitron"", sans-serif; font-weight:bold; font-size:14px; text-shadow:0 0 6px rgba(251,191,36,0.3)'>—</div>
  </div>
  <div class='content'>
    <div class='tabs' id='tabs'>
      <button class='active' onclick='switchTab(0)'>📊 概览</button>
      <button onclick='switchTab(1)'>👥 居民</button>
      <button onclick='switchTab(2)'>🏗 建筑</button>
      <button onclick='switchTab(3)'>📜 任务</button>
      <button onclick='switchTab(4)'>🎒 背包</button>
      <button onclick='switchTab(5)'>🏛 派系</button>
      <button onclick='switchTab(6)'>⚠ 危机</button>
      <button onclick='switchTab(7)'>🤖 AI配置</button>
      <button onclick='switchTab(8)'>🌍 探索/战斗</button>
    </div>
    <div id='page'>加载中...</div>
  </div>
</div>

<div class='log' id='log'></div>

<script>
// Initial theme check
if(localStorage.getItem('theme') === 'light') {
  document.body.classList.add('light-theme');
}

let currentTab=0,autoTimer=null,allNPCs=[],selectedEnemyIndex=0;

async function api(action,body){
  let opts=body?{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}:{method:'POST'};
  let r=await fetch('/api/'+action,opts);
  return await r.json();
}

async function load(){
  let s=await(await fetch('/api/status')).json();
  document.getElementById('title').textContent=`第${s.day}天 ${s.phase==='Day'?'☀白天':s.phase==='Night'?'🌙夜晚':'📊结算中'}`;
  
  // Update status dot color dynamically
  let dot = document.getElementById('statusDot');
  if(s.phase==='Day') {
    dot.style.background = '#fbbf24';
    dot.style.boxShadow = '0 0 10px #fbbf24';
  } else if(s.phase==='Night') {
    dot.style.background = '#38bdf8';
    dot.style.boxShadow = '0 0 10px #38bdf8';
  } else {
    dot.style.background = '#a855f7';
    dot.style.boxShadow = '0 0 10px #a855f7';
  }

  // Render top resource indicators
  document.getElementById('resbar').innerHTML=
    `<div class='res-card' style='border-bottom: 2px solid var(--color-food)'><span>🍖 食物:</span><strong style='color:var(--color-food)'>${s.resources.food}</strong></div>` +
    `<div class='res-card' style='border-bottom: 2px solid var(--color-water)'><span>💧 水源:</span><strong style='color:var(--color-water)'>${s.resources.water}</strong></div>` +
    `<div class='res-card' style='border-bottom: 2px solid var(--color-power)'><span>⚡ 电力:</span><strong style='color:var(--color-power)'>${s.resources.power}</strong></div>` +
    `<div class='res-card' style='border-bottom: 2px solid var(--color-med)'><span>💊 药品:</span><strong style='color:var(--color-med)'>${s.resources.medicine}</strong></div>` +
    `<div class='res-card' style='border-bottom: 2px solid var(--color-parts)'><span>🔧 零件:</span><strong style='color:var(--color-parts)'>${s.resources.parts}</strong></div>` +
    `<div class='res-card' style='border-bottom: 2px solid var(--color-shards)'><span>💎 碎片:</span><strong style='color:var(--color-shards)'>${s.resources.shards}</strong></div>` +
    `<div class='res-card'><span>👥 人口:</span><strong>${s.aliveNPCs}</strong></div>`;
  
  // Crisis sidebar
  let c=await(await fetch('/api/crisis')).json();
  document.getElementById('crisisSidebar').innerHTML=c.crises.length?c.crises.map(x=>`<div style='color:#ef4444; font-weight:bold'>⚠ ${x.type}</div>`).join(''):'<div>✅ 正常无异常</div>';
  document.getElementById('endingSidebar').textContent=c.endingTitle;

  if(s.isGameOver&&!s.isVictory) document.getElementById('title').style.color='#ef4444';
  else if(s.isGameOver&&s.isVictory) document.getElementById('title').style.color='#10b981';
  else document.getElementById('title').style.color='#38bdf8';

  // Cache NPCs list
  try {
    let npcs = await(await fetch('/api/npcs')).json();
    allNPCs = npcs;
  } catch(e) {}

  updateThemeButton();
  loadTab();
  loadLog();
}

function toggleTheme(){
  let isLight = document.body.classList.toggle('light-theme');
  localStorage.setItem('theme', isLight ? 'light' : 'dark');
  updateThemeButton();
}

function updateThemeButton(){
  let btn = document.getElementById('themeToggleBtn');
  if(btn) {
    let isLight = document.body.classList.contains('light-theme');
    btn.textContent = isLight ? '🌙 暗黑模式' : '☀️ 明亮模式';
  }
}

async function loadTab(){
  let html='';
  if(currentTab===0){
    let s=await(await fetch('/api/status')).json();
    let r=s.resources;
    html+=`<div class='card'><h4>📊 档案城概览</h4>
      <p style='margin:6px 0; font-size:14px'>存活时间: <strong style='color:var(--text-accent)'>${s.day}天</strong> | 居民总数: <strong style='color:#10b981'>${s.aliveNPCs}人</strong> | 战斗胜利: <strong>${s.totalCombats}次</strong> | 探索次数: <strong>${s.totalExplorations}次</strong></p></div>`;
    html+=`<div class='card'><h4>📦 资源仓库状况</h4>`;
    html+=bar('食物 (🍖)',r.food,100,'var(--color-food)')+
          bar('水源 (💧)',r.water,100,'var(--color-water)')+
          bar('电力 (⚡)',r.power,50,'var(--color-power)')+
          bar('药品 (💊)',r.medicine,30,'var(--color-med)')+
          bar('零件 (🔧)',r.parts,50,'var(--color-parts)')+
          bar('碎片 (💎)',r.shards,20,'var(--color-shards)');
    html+=`</div>`;
  } else if(currentTab===1){
    html+=`<div class='npc-grid'>`;
    allNPCs.forEach(n=>{
      let mcolor=n.mentalKey==='Hopeful'?'green':n.mentalKey==='Despair'||n.mentalKey==='Traumatized'?'red':n.mentalKey==='Anxious'?'yellow':'gray';
      let cardClass = n.isAlive && n.status==='Injured' ? 'card npc-card injured' : 'card npc-card';
      html+=`<div class='${cardClass}'>` +
        `<div class='npc-header'>` +
          `<h4>${n.isAlive?(n.status==='Injured'?'🤕':'😊'):'💀'} ${n.name}</h4>` +
          `<span class='tag ${mcolor}'>${n.mental}</span>` +
        `</div>`;
      if(n.isAlive) {
        html+=`<div class='npc-stat-row'>` +
          bar('生命值', n.health, 100, '#ef4444') +
          bar('士气值', n.morale, 100, '#a855f7') +
          bar('饱食度', 100 - n.hunger, 100, '#fbbf24') +
          `</div>` +
          `<p style='margin-top:8px;font-size:12px;color:var(--text-secondary)'>能力属性: 战斗力 ${n.combat} | 医疗 ${n.medical} | 工程 ${n.engineering}</p>` +
          `<p style='margin-top:4px;font-size:12px;color:var(--text-secondary)'>忠诚度: ${n.loyalty} | 当前状态: <strong>${n.status}</strong></p>` +
          `<div style='margin-top:12px;display:flex;gap:6px'>` +
            `<button class='btn-sm' onclick='talk(""${n.id}"")'>💬 交流对话</button>` +
            `<button class='btn-sm' style='border-color:var(--color-shards); color:var(--color-shards);' onclick='showDiaries(""${n.id}"")'>📖 生存日记</button>` +
            `${n.status==='Injured'?`<button class='btn-sm red' onclick='heal(""${n.id}"")'>💊 使用药品治疗</button>`:''}` +
          `</div>`;
      } else {
        html+=`<p style='color:#ef4444;font-weight:bold;margin-top:10px'>已在档案城中安息。</p>`;
      }
      html+=`</div>`;
    });
    html+=`</div>`;
    let relHtml = await renderRelationsMatrix();
    html += relHtml;
  } else if(currentTab===2){
    let bs=await(await fetch('/api/buildings')).json();
    html+=`<div class='building-grid'>`;
    bs.forEach(b=>{
      html+=`<div class='card building-card'>` +
        `<h4>${b.built?'✅':'⬜'} ${b.name} ${b.built?'Lv'+b.level:''}</h4>` +
        `<p style='margin:8px 0'>${b.desc}</p>` +
        `<div style='margin-top:12px;display:flex;gap:6px'>` +
          `${!b.built?`<button class='btn-sm' onclick='build(""${b.id}"")'>🛠 建造此建筑</button>`:''}` +
          `${b.built&&b.level<b.maxLevel?`<button class='btn-sm' onclick='upgrade(""${b.id}"")'>⚡ 升级建筑</button>`:''}` +
        `</div>` +
      `</div>`;
    });
    html+=`</div>`;
  } else if(currentTab===3){
    let qs=await(await fetch('/api/quests')).json();
    qs.forEach(q=>{
      let sc=q.status==='Active'?'yellow':q.status==='Completed'?'green':'gray';
      html+=`<div class='card'>` +
        `<div style='display:flex;justify-content:space-between;align-items:center;margin-bottom:8px'>` +
          `<h4>${q.title}</h4>` +
          `<span class='tag ${sc}'>${q.status}</span>` +
        `</div>` +
        `<p style='margin-bottom:8px'>${q.desc}</p>`;
      q.objectives.forEach(o=>{
        let done=o.current>=o.required;
        html+=`<div style='display:flex;align-items:center;gap:8px;font-size:12px;margin:4px 0;color:${done?'#10b981':'#94a3b8'}'>` +
          `<span>${done?'✅':'⏸'}</span>` +
          `<span>目标: ${o.type} [${o.target}]</span>` +
          `<span style='margin-left:auto;font-weight:bold'>${o.current}/${o.required}</span>` +
        `</div>`;
      });
      html+=`</div>`;
    });
  } else if(currentTab===4){
    let items=await(await fetch('/api/items')).json();
    if(!items.length) {
      html+=`<div class='card'><p style='text-align:center'>🎒 仓库存放空间当前为空</p></div>`;
    } else {
      html+=`<div class='item-grid'>`;
      items.forEach(i=>{
        let rc=i.rarity==='传说'?'purple':i.rarity==='史诗'?'blue':i.rarity==='稀有'?'yellow':'gray';
        html+=`<div class='card'>` +
          `<h4><span class='tag ${rc}'>${i.rarity}</span> ${i.name}</h4>` +
          `<p style='margin-top:6px;font-size:12px'>${i.desc}</p>`;
        let stats = [];
        if(i.attack) stats.push(`攻击值: +${i.attack}`);
        if(i.defense) stats.push(`防护值: +${i.defense}`);
        if(i.heal) stats.push(`医疗包回复: ${i.heal} HP`);
        if(stats.length > 0) {
          html+=`<p style='margin-top:6px;font-size:11px;color:#fbbf24'>属性加成: ${stats.join(' | ')}</p>`;
        }
        html+=`</div>`;
      });
      html+=`</div>`;
    }
  } else if(currentTab===5){
    let fs=await(await fetch('/api/factions')).json();
    fs.forEach(f=>{
      html+=`<div class='card'>` +
        `<h4>${f.name}${f.unlocked?'':' <span class=""tag gray"">未接触</span>'}</h4>` +
        `<p style='margin:4px 0'>派系领袖: <strong>${f.leader}</strong> | 声望等级: <span class='tag yellow'>${f.repName} (${f.rep})</span></p>` +
        `<p style='font-size:12px;margin-top:6px'>${f.desc}</p>` +
      `</div>`;
    });
  } else if(currentTab===6){
    let c=await(await fetch('/api/crisis')).json();
    if(c.crises.length) {
      c.crises.forEach(x=>{
        html+=`<div class='card crisis-alert'><h4 style='color:#ef4444'>⚠ 警报: ${x.type}</h4><p style='color:#f87171'>${x.desc}</p></div>`;
      });
    } else {
      html+=`<div class='card' style='border-color:rgba(16, 185, 129, 0.35)'><h4 style='color:#10b981'>✅ 系统状态平稳，当前无活跃灾害</h4></div>`;
    }
    html+=`<div class='card'><h4>居民精神指数 (Psychology)</h4>`;
    html+=`<div style='display:flex;flex-wrap:wrap;gap:8px;margin-top:8px'>`;
    c.mental.forEach(m=>{
      let mc=m.key==='Hopeful'?'green':m.key==='Despair'||m.key==='Traumatized'?'red':m.key==='Anxious'?'yellow':'gray';
      html+=`<span class='tag ${mc}'>${m.name}: ${m.state}</span>`;
    });
    html+=`</div></div>`;
  } else if(currentTab===7){
    let cfg = { provider: 'Mock', apiBaseUrl: '', apiKey: '', model: '' };
    try {
      cfg = await(await fetch('/api/ai/config')).json();
    } catch(e) {}
    html+=`<div class='card' style='max-width:600px; margin: 0 auto;'>` +
      `<h4>🤖 AI 联网与大模型配置</h4>` +
      `<p style='margin-bottom:16px;'>配置您的 AI 接口，开启生动的剧情、日记和任务生成。如果不配置，系统将默认采用 Mock 本地模拟器。</p>` +
      `<label style='font-size:12px; font-weight:bold; color:var(--text-secondary)'>AI 提供者 (Provider)</label>` +
      `<select id='aiProvider' onchange='toggleAIFields()'>` +
        `<option value='Mock' ${cfg.provider==='Mock'?'selected':''}>Mock (本地单机模拟)</option>` +
        `<option value='OpenAI' ${cfg.provider==='OpenAI'?'selected':''}>OpenAI (网络 API)</option>` +
      `</select>` +
      `<div id='aiNetworkFields' style='display:${cfg.provider==='OpenAI'?'block':'none'}'>` +
        `<label style='font-size:12px; font-weight:bold; color:var(--text-secondary)'>API 接口基地址 (Base URL)</label>` +
        `<input type='text' id='aiBaseUrl' value='${cfg.apiBaseUrl||''}' placeholder='例如: https://api.openai.com/v1' />` +
        `<label style='font-size:12px; font-weight:bold; color:var(--text-secondary)'>API 密钥 (API Key)</label>` +
        `<input type='password' id='aiApiKey' value='${cfg.apiKey||''}' placeholder='输入您的 API Key' />` +
        `<label style='font-size:12px; font-weight:bold; color:var(--text-secondary)'>模型名称 (Model)</label>` +
        `<input type='text' id='aiModel' value='${cfg.model||''}' placeholder='例如: gpt-3.5-turbo' />` +
      `</div>` +
      `<div style='display:flex; gap:10px; margin-top:8px'>` +
        `<button class='btn-sm' onclick='saveAIConfig()'>💾 保存配置</button>` +
        `<button class='btn-sm' onclick='testAIConfig()'>⚡ 测试连接</button>` +
      `</div>` +
      `<div id='aiTestResult' style='margin-top:16px; font-size:13px; padding:10px; border-radius:6px; display:none;'></div>` +
    `</div>`;
  } else if(currentTab===8){
    let combatStatus = await(await fetch('/api/combat/status')).json();
    let exploreStatus = await(await fetch('/api/explore/status')).json();
    
    if (combatStatus.inCombat) {
      html += renderCombatView(combatStatus);
    } else if (exploreStatus.isExploring) {
      html += renderExplorationView(exploreStatus);
    } else {
      html += renderExplorationSetup();
    }
  }
  document.getElementById('page').innerHTML=html;
}

async function loadLog(){
  let r=await(await fetch('/api/log')).json();
  document.getElementById('log').innerHTML=r.log.map(l=>`<div>${l}</div>`).join('');
  let el=document.getElementById('log');el.scrollTop=el.scrollHeight;
}

function bar(name,val,max,color){
  let pct=Math.min(100,Math.round(val/max*100));
  let c=val<=max*0.3?'#ef4444':color;
  return `<div style='display:flex;align-items:center;gap:12px;margin:10px 0;font-size:13px'>` +
    `<span style='width:90px;color:var(--text-secondary)'>${name}</span>` +
    `<span style='width:55px;text-align:right;font-weight:bold;color:${c}'>${val}/${max}</span>` +
    `<div class='bar' style='flex:1'><div class='fill' style='width:${pct}%;background:${c};color:${c}'></div></div>` +
    `</div>`;
}

function switchTab(n){currentTab=n;document.querySelectorAll('.tabs button').forEach((b,i)=>b.classList.toggle('active',i===n));loadTab();}

async function talk(id){let r=await api('talk',{id});alert(r.dialogue||'对话失败');load();}
async function heal(id){await api('heal',{id});load();}
async function build(id){await api('build',{id});load();}
async function upgrade(id){await api('upgrade',{id});load();}

function autoPlay(){
  let btn = document.getElementById('autoplayBtn');
  if(autoTimer){
    clearInterval(autoTimer);
    autoTimer=null;
    btn.textContent = '🤖 开启自动游玩';
    btn.classList.remove('active');
    alert('自动游玩已停止');
    return;
  }
  if(!confirm('确认启动自主档案城模拟？（系统将每2秒自动向前推演一日）'))return;
  btn.textContent = '⏸ 停止自动';
  btn.classList.add('active');
  autoTimer=setInterval(()=>api('nextday').then(r=>{
    load();
    if(r.gameOver){
      clearInterval(autoTimer);
      autoTimer=null;
      btn.textContent = '🤖 开启自动游玩';
      btn.classList.remove('active');
    }
  }),2000);
}

async function renderRelationsMatrix() {
  let relations = {};
  try {
    relations = await(await fetch('/api/npcs/relations')).json();
  } catch(e) {}
  let aliveNPCs = allNPCs.filter(n => n.isAlive);
  
  if (aliveNPCs.length === 0) return '';
  
  let html = `<div class='card' style='margin-top:20px;'>` +
    `<h4>🔗 居民人际关系网格矩阵</h4>` +
    `<div style='overflow-x:auto; margin-top:12px;'>` +
    `<table style='width:100%; border-collapse:collapse; text-align:center; font-size:11px;'>` +
      `<thead><tr>` +
        `<th style='padding:8px; border:1px solid var(--btn-border); background:rgba(0,0,0,0.15); color:var(--text-primary)'>居民</th>`;
  
  aliveNPCs.forEach(n => {
    html += `<th style='padding:8px; border:1px solid var(--btn-border); background:rgba(0,0,0,0.15); color:var(--text-primary)'>${n.name}</th>`;
  });
  html += `</tr></thead><tbody>`;
  
  aliveNPCs.forEach(rowNPC => {
    html += `<tr>` +
      `<td style='padding:8px; border:1px solid var(--btn-border); background:rgba(0,0,0,0.15); font-weight:bold; color:var(--text-primary)'>${rowNPC.name}</td>`;
    
    aliveNPCs.forEach(colNPC => {
      if (rowNPC.id === colNPC.id) {
        html += `<td style='padding:8px; border:1px solid var(--btn-border); color:var(--text-secondary); background:rgba(0,0,0,0.05)'>-</td>`;
      } else {
        let val = 0;
        if (relations[rowNPC.id] && relations[rowNPC.id][colNPC.id] !== undefined) {
          val = relations[rowNPC.id][colNPC.id];
        }
        let color = val > 0 ? '#10b981' : val < 0 ? '#ef4444' : 'var(--text-secondary)';
        let weight = val !== 0 ? 'bold' : 'normal';
        html += `<td style='padding:8px; border:1px solid var(--btn-border); color:${color}; font-weight:${weight}'>${val > 0 ? '+' : ''}${val}</td>`;
      }
    });
    html += `</tr>`;
  });
  
  html += `</tbody></table></div></div>`;
  return html;
}

function renderCombatView(status) {
  let timelineHtml = `<div style='display:flex; gap:10px; overflow-x:auto; padding:10px 0; margin-bottom:16px;'>`;
  status.timeline.forEach((u, i) => {
    let isActive = status.activeUnitId === u.id && status.currentIndex === i;
    let sideColor = u.isPlayerSide ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)';
    let sideBorder = u.isPlayerSide ? 'var(--color-food)' : '#ef4444';
    let activeGlow = isActive ? `box-shadow: 0 0 12px ${sideBorder}; border-color:${sideBorder}; transform: scale(1.02);` : '';
    let deadStyle = u.isDead ? 'opacity: 0.4;' : '';
    
    timelineHtml += `<div style='background:${sideColor}; border:1px solid var(--btn-border); border-radius:6px; padding:8px 12px; min-width:110px; text-align:center; transition: all 0.3s; ${activeGlow} ${deadStyle}'>` +
      `<div style='font-size:10px; text-transform:uppercase; color:var(--text-secondary); font-family:""Orbitron"", sans-serif;'>` +
        `${u.isPlayerSide?'我方':'敌方'}${isActive?' (当前)':''}` +
      `</div>` +
      `<div style='font-weight:bold; font-size:12px; margin:2px 0;'>${u.name}</div>` +
      `<div style='font-size:10px; color:${u.hp > 0 ? sideBorder : ""#94a3b8""}'>HP: ${u.hp}/${u.maxHp}</div>` +
      `</div>`;
  });
  timelineHtml += `</div>`;

  let playersList = status.timeline.filter(u => u.isPlayerSide);
  let enemiesList = status.timeline.filter(u => !u.isPlayerSide);
  
  let listsHtml = `<div style='display:grid; grid-template-columns: 1fr 1fr; gap:16px;'>` +
    `<div>` +
      `<h5>我方战斗队员</h5>`;
  
  playersList.forEach(p => {
    let deadBadge = p.isDead ? `<span class='tag red'>战损</span>` : '';
    let activeBorder = status.activeUnitId === p.id && status.isPlayerTurn ? 'border-color:var(--text-accent); box-shadow:0 0 8px var(--border-glow);' : '';
    listsHtml += `<div class='card' style='padding:10px; margin-top:8px; ${activeBorder}'>` +
      `<div style='display:flex; justify-content:space-between; align-items:center;'>` +
        `<strong>${p.name}</strong>` +
        `<div>${p.role} ${deadBadge}</div>` +
      `</div>` +
      bar('生命值', p.hp, p.maxHp, '#ef4444') +
      `<div style='font-size:11px; color:var(--text-secondary); margin-top:4px;'>` +
        `攻击: ${p.attack} | 防御: ${p.defense}${p.buffDefense ? ' (+' + p.buffDefense + ')' : ''} | 速度: ${p.speed} ` +
        `${p.isDefending ? ' <span class=""tag yellow"">正在防御</span>' : ''}` +
      `</div>` +
      `</div>`;
  });
  
  listsHtml += `</div><div>` +
    `<h5>敌方目标 (点击选择)</h5>`;
  
  let aliveEnemies = enemiesList.filter(e => !e.isDead);
  if (selectedEnemyIndex >= aliveEnemies.length) {
    selectedEnemyIndex = 0;
  }
  
  enemiesList.forEach((e, idx) => {
    let deadBadge = e.isDead ? `<span class='tag red'>已击败</span>` : '';
    let aliveIdx = aliveEnemies.indexOf(e);
    let isTarget = aliveIdx !== -1 && selectedEnemyIndex === aliveIdx;
    let borderStyle = isTarget ? 'border-color:#ef4444; box-shadow: 0 0 10px rgba(239, 68, 68, 0.4);' : '';
    let clickHandler = e.isDead ? '' : `onclick='selectedEnemyIndex=${aliveIdx}; switchTab(8)' style='cursor:pointer'`;
    
    listsHtml += `<div class='card' ${clickHandler} style='padding:10px; margin-top:8px; ${borderStyle}'>` +
      `<div style='display:flex; justify-content:space-between; align-items:center;'>` +
        `<strong>${isTarget ? '🎯 ' : ''}${e.name}</strong>` +
        `<div>${deadBadge}</div>` +
      `</div>` +
      bar('生命值', e.hp, e.maxHp, '#ef4444') +
      `<div style='font-size:11px; color:var(--text-secondary); margin-top:4px;'>` +
        `攻击: ${e.attack} | 防御: ${e.defense} | 速度: ${e.speed}` +
      `</div>` +
      `</div>`;
  });
  
  listsHtml += `</div></div>`;

  let activeUnit = status.timeline.find(u => u.id === status.activeUnitId);
  let activeRole = activeUnit ? activeUnit.role : '';
  let skillBtnText = '🌟 角色技能';
  let skillDesc = '释放当前单位的特有职业技能';
  if (activeRole === 'Doctor') { skillBtnText = '💉 团队治疗 (Group Heal)'; skillDesc = '为全队存活单位恢复 15 HP'; }
  else if (activeRole === 'Scout') { skillBtnText = '🛡 搭建护盾 (Shield Wall)'; skillDesc = '为全队所有存活成员提升 5 点临时防御值'; }
  else if (activeRole === 'Child') { skillBtnText = '🎯 破甲狙击 (Snipe)'; skillDesc = '对目标造成 [攻击力 + 5] 的无视防御伤害'; }
  else if (activeRole === 'Engineer') { skillBtnText = '⚡ 机械重载 (Overload)'; skillDesc = '对目标进行 1.5 倍攻击力的机械重击'; }
  else if (activeRole === 'Stranger') { skillBtnText = '🔍 洞察破绽 (Expose Flaw)'; skillDesc = '造成常规伤害，并使目标的防御力永久降低 3 点'; }

  let ctrlHtml = `<div class='card' style='margin-top:16px;'>` +
    `<h4>🎮 战术控制台</h4>`;
  
  if (status.isPlayerTurn) {
    ctrlHtml += `<p style='margin-bottom:12px; font-size:13px; color:var(--text-accent);'>` +
      `当前行动成员: <strong>${activeUnit?.name}</strong> (${activeRole})` +
      `</p>` +
      `<div style='display:grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap:10px;'>` +
        `<button class='btn-sm' onclick='doCombatAction(""Attack"")'>⚔ 普通攻击 (单体)</button>` +
        `<button class='btn-sm' onclick='doCombatAction(""Defend"")'>🛡 临时防御 (本轮)</button>` +
        `<button class='btn-sm' style='border-color:var(--color-shards); color:var(--color-shards);' onclick='doCombatAction(""UseSkill"")' title='${skillDesc}'>${skillBtnText}</button>` +
        `<button class='btn-sm' style='border-color:var(--color-power); color:var(--color-power);' onclick='showCombatItemSelect()'>🎒 使用消耗品道具</button>` +
        `<button class='btn-sm red' onclick='doCombatAction(""Escape"")'>🏃 撤退撤离</button>` +
      `</div>` +
      `<div id='combatItemSelectArea' style='margin-top:12px; display:none; background:rgba(0,0,0,0.15); border:1px solid var(--btn-border); border-radius:6px; padding:10px;'>` +
        `正在读取背包...` +
      `</div>`;
  } else {
    ctrlHtml += `<p style='color:var(--text-secondary); text-align:center; padding:10px;'>⏳ 敌方或其他单位行动中...</p>`;
  }
  ctrlHtml += `</div>`;

  let logHtml = `<div class='card' style='margin-top:16px;'>` +
    `<h4>📜 战斗日志记录</h4>` +
    `<div id='combatLogBox' style='background:var(--log-bg); border:1px solid var(--btn-border); border-radius:6px; padding:12px; font-family:""JetBrains Mono"", monospace; font-size:11px; color:#ef4444; height:180px; overflow-y:auto;'>` +
      status.combatLog.replace(/\n/g, '<br/>') +
    `</div>` +
    `</div>`;

  return `<h3>⚔ 遭遇战</h3>` + timelineHtml + listsHtml + ctrlHtml + logHtml;
}

function renderExplorationView(status) {
  let svgHtml = `<div style='text-align:center; background:rgba(0,0,0,0.4); border:1px solid var(--btn-border); border-radius:8px; padding:10px; margin-bottom:12px; position:relative; overflow:hidden;'>` +
    `<div style='position:absolute; top:8px; left:12px; font-size:10px; font-family:""Orbitron"", sans-serif; color:var(--text-accent); text-transform:uppercase; letter-spacing:1px; z-index:2;'>` +
      `📡 SQUAD HUD RADAR SCANNER` +
    `</div>` +
    `<svg width='100%' height='200' viewBox='0 0 320 200' style='background:transparent; font-family:""JetBrains Mono"", monospace;'>` +
      `<defs>` +
        `<pattern id='grid' width='20' height='20' patternUnits='userSpaceOnUse'>` +
          `<path d='M 20 0 L 0 0 0 20' fill='none' stroke='rgba(16, 185, 129, 0.05)' stroke-width='1'/>` +
        `</pattern>` +
      `</defs>` +
      `<rect width='100%' height='100%' fill='url(#grid)' />` +
      `<circle cx='160' cy='100' r='80' fill='none' stroke='rgba(16, 185, 129, 0.1)' stroke-dasharray='4 4' />` +
      `<circle cx='160' cy='100' r='50' fill='none' stroke='rgba(16, 185, 129, 0.07)' stroke-dasharray='4 4' />`;

  let N = status.connectedRooms.length;
  status.connectedRooms.forEach((r, i) => {
    let angle = i * (2 * Math.PI / Math.max(1, N)) - Math.PI / 2;
    let rx = 160 + 100 * Math.cos(angle);
    let ry = 100 + 65 * Math.sin(angle);
    let lineColor = r.locked ? '#ef4444' : r.visited ? 'rgba(16, 185, 129, 0.4)' : 'var(--text-accent)';
    let strokeDash = r.locked ? '3 3' : 'none';
    
    svgHtml += `<line x1='160' y1='100' x2='${rx}' y2='${ry}' stroke='${lineColor}' stroke-width='1.5' stroke-dasharray='${strokeDash}' />`;
    
    let nodeFill = r.locked ? 'rgba(239, 68, 68, 0.2)' : r.visited ? 'rgba(16, 185, 129, 0.1)' : 'rgba(56, 189, 248, 0.15)';
    let nodeStroke = r.locked ? '#ef4444' : r.visited ? 'rgba(16, 185, 129, 0.6)' : 'var(--text-accent)';
    let nodeCursor = r.locked ? 'not-allowed' : 'pointer';
    let clickCall = r.locked ? '' : `onclick='moveToRoom(""${r.id}"")'`;
    
    svgHtml += `<circle cx='${rx}' cy='${ry}' r='18' fill='${nodeFill}' stroke='${nodeStroke}' stroke-width='2' style='cursor:${nodeCursor};' ${clickCall} />`;
    
    let textY = ry + 4;
    let label = r.name.substring(0, 4);
    svgHtml += `<text x='${rx}' y='${textY}' fill='${nodeStroke}' font-size='8' font-weight='bold' text-anchor='middle' style='pointer-events:none;'>${label}</text>`;
    if (r.locked) {
      svgHtml += `<text x='${rx}' y='${ry - 22}' fill='#ef4444' font-size='7' text-anchor='middle'>🔒 LOCK</text>`;
    }
  });

  let currentStroke = 'var(--color-food)';
  svgHtml += `<circle cx='160' cy='100' r='24' fill='rgba(16, 185, 129, 0.2)' stroke='${currentStroke}' stroke-width='2.5' style='filter: drop-shadow(0 0 6px rgba(16, 185, 129, 0.5));' />` +
    `<text x='160' y='104' fill='${currentStroke}' font-size='10' font-weight='bold' text-anchor='middle'>SQUAD</text>` +
    `</svg></div>`;

  let infoHtml = svgHtml + `<div class='card'>` +
    `<h4>🌍 探索地图：${status.mapName}</h4>` +
    `<p style='font-size:14px; margin-top:4px;'>当前位置：<strong style='color:var(--text-accent); font-family:""Orbitron"", sans-serif;'>${status.roomName}</strong> (危险度: ${status.danger})</p>` +
    `<p style='margin-top:6px; font-size:12px; color:var(--text-secondary)'>${status.roomDesc}</p>` +
    `<p style='margin-top:8px;'>` +
      `${status.searched ? '<span class=""tag green"">✓ 当前房间已完成搜寻</span>' : '<span class=""tag yellow"">⚠ 当前房间尚未搜寻</span>'}` +
    `</p>` +
    `</div>`;
    
  let teamHtml = `<div class='card'>` +
    `<h4>👥 探索小队成员</h4>` +
    `<div style='display:flex; flex-direction:column; gap:8px; margin-top:8px;'>`;
    
  status.team.forEach(m => {
    teamHtml += `<div style='border:1px solid var(--btn-border); border-radius:6px; padding:8px; font-size:12px; background:rgba(0,0,0,0.1);'>` +
      `<div style='display:flex; justify-content:space-between; font-weight:bold;'>` +
        `<span>${m.name} (${m.role})</span>` +
        `<span style='color:#ef4444'>HP: ${m.health}/100</span>` +
      `</div>` +
      `<div style='color:var(--text-secondary); margin-top:4px;'>` +
        `工程: ${m.engineering} | 医疗: ${m.medical} | 战斗: ${m.combat} | 士气: ${m.morale}/100` +
      `</div>` +
      `</div>`;
  });
  teamHtml += `</div></div>`;

  let actionHtml = '';
  
  if (status.activeCheck) {
    let check = status.activeCheck;
    actionHtml = `<div class='card' style='border-color:#fbbf24; box-shadow:0 0 10px rgba(251,191,36,0.3);'>` +
      `<h4 style='color:#fbbf24;'>⚠️ 遭遇判定事件</h4>` +
      `<p style='margin:8px 0; font-size:13px;'>${check.desc}</p>` +
      `<p style='font-size:12px; color:var(--text-secondary);'>判定类型：<strong>${check.skill}</strong> | 目标难度值 (D20)：<strong>${check.difficulty}</strong></p>` +
      `<div style='margin-top:12px;'>` +
        `<label style='font-size:12px; font-weight:bold;'>指派队员执行判定：</label>` +
        `<select id='skillCheckNPC' style='margin-top:6px; margin-bottom:12px;'>`;
        
    status.team.forEach(m => {
      let skillVal = check.skill === 'engineering' ? m.engineering : check.skill === 'medical' ? m.medical : m.combat;
      actionHtml += `<option value='${m.id}'>${m.name} (相关技能值: +${skillVal})</option>`;
    });
    
    actionHtml += `</select>` +
        `<button class='btn-sm' style='width:100%; border-color:#fbbf24; color:#fbbf24; font-weight:bold;' onclick='resolveSkillCheck()'>🎲 执行骰子判定</button>` +
      `</div>` +
      `</div>`;
  } else {
    actionHtml = `<div class='card'>` +
      `<h4>🛠 探索操作区</h4>` +
      `<div style='display:flex; flex-direction:column; gap:10px; margin-top:12px;'>` +
        `<button class='btn-sm' ${status.searched ? 'disabled style=""opacity:0.6; cursor:not-allowed;""' : ''} onclick='searchRoom()'>` +
          `🔍 搜寻当前房间` +
        `</button>` +
      `</div>` +
      `</div>`;
      
    actionHtml += `<div class='card'>` +
      `<h4>🚪 移动到相邻房间</h4>` +
      `<div style='display:flex; flex-direction:column; gap:8px; margin-top:8px;'>`;
      
    status.connectedRooms.forEach(r => {
      if (r.locked) {
        actionHtml += `<button class='btn-sm' disabled style='text-align:left; opacity:0.6; cursor:not-allowed; display:flex; justify-content:space-between;'>` +
          `<span>🔒 ${r.name} (已锁定)</span>` +
          `<span style='font-size:10px; color:#ef4444;'>需要钥匙: ${r.requiredItem}</span>` +
          `</button>`;
      } else {
        actionHtml += `<button class='btn-sm' style='text-align:left;' onclick='moveToRoom(""${r.id}"")'>` +
          `🏃 前往: ${r.name} ${r.visited ? '(已访问)' : '(未探索)'}` +
          `</button>`;
      }
    });
    
    actionHtml += `</div></div>`;
    
    actionHtml += `<div class='card'>` +
      `<h4>🏠 返回基地</h4>` +
      `<p style='font-size:12px; color:var(--text-secondary); margin-bottom:8px;'>带回所有搜集到的战利品。如果在野外遭遇灭队，物资将全部丢失且队员受重创！</p>` +
      `<button class='btn-sm' style='width:100%; border-color:var(--color-food); color:var(--color-food); font-weight:bold;' onclick='returnToBase()'>` +
        `🏠 携带物资返回安全屋` +
      `</button>` +
      `</div>`;
  }

  return `<h3>🌍 地点探索</h3>` +
    `<div style='display:grid; grid-template-columns: 1fr 1fr; gap:16px; margin-top:10px;'>` +
      `<div>${infoHtml}${teamHtml}</div>` +
      `<div>${actionHtml}</div>` +
    `</div>`;
}

function renderExplorationSetup() {
  let maps = [
    { id: 'abandoned_hospital', name: '🏥 废弃医院', desc: '主要掉落：药品、记忆碎片 | 难度: 1-4' },
    { id: 'subway_ruins', name: '🚇 地铁废墟', desc: '主要掉落：零件、电力 | 难度: 2-3' },
    { id: 'archive_ruins', name: '📚 旧档案馆', desc: '主要掉落：记忆碎片 | 难度: 3-5' },
    { id: 'ruined_park', name: '🌳 废墟公园', desc: '主要掉落：食物、药品 | 难度: 1-3' },
    { id: 'broadcast_tower', name: '📡 广播塔', desc: '主要掉落：零件、电力 | 难度: 2-4' }
  ];
  
  let eligibleNPCs = allNPCs.filter(n => n.isAlive && n.status !== 'Injured' && n.status !== 'Dead' && n.status !== 'Working');
  
  let mapSelectHtml = `<h4>1. 选择目的地地图</h4>` +
    `<select id='exploreMapSelect' style='margin-top:6px; margin-bottom:16px;'>`;
  maps.forEach(m => {
    mapSelectHtml += `<option value='${m.id}'>${m.name} (${m.desc})</option>`;
  });
  mapSelectHtml += `</select>`;

  let teamChecklistHtml = `<h4>2. 选派探索队员 (最多3名)</h4>` +
    `<div style='display:flex; flex-direction:column; gap:6px; margin-top:8px; margin-bottom:16px;'>`;
  
  if (eligibleNPCs.length === 0) {
    teamChecklistHtml += `<p style='color:#ef4444; font-size:13px;'>⚠️ 当前基地中没有可行动的居民（均在工作、受伤或已战损）</p>`;
  } else {
    eligibleNPCs.forEach(n => {
      teamChecklistHtml += `<label style='display:flex; align-items:center; gap:8px; font-size:13px; cursor:pointer; background:var(--btn-bg); border:1px solid var(--btn-border); padding:8px; border-radius:6px;'>` +
        `<input type='checkbox' name='exploreTeamCheckbox' value='${n.id}' onchange='validateTeamSelection()' />` +
        `<span><strong>${n.name}</strong> (${n.role}) | HP: ${n.health} | 战斗力: ${n.combat} | 工程: ${n.engineering} | 医疗: ${n.medical}</span>` +
        `</label>`;
    });
  }
  teamChecklistHtml += `</div>`;

  let startBtn = `<button class='btn-sm' id='startExplorationBtn' style='width:100%; border-color:var(--text-accent); color:var(--text-accent); font-weight:bold; font-size:14px; padding:10px;' onclick='startExploration()'>` +
    `🚀 开启野外探索` +
    `</button>`;

  return `<div class='card' style='max-width:600px; margin:0 auto;'>` +
    `<h4>🌍 新建探索任务</h4>` +
    `<p style='font-size:12px; color:var(--text-secondary); margin-bottom:16px;'>组织一支最多3名健康居民组成的队伍，指派他们前往废土搜集资源和记忆碎片。</p>` +
    mapSelectHtml + teamChecklistHtml + startBtn +
    `</div>`;
}

function validateTeamSelection() {
  let checkboxes = document.getElementsByName('exploreTeamCheckbox');
  let selectedCount = 0;
  for (let i = 0; i < checkboxes.length; i++) {
    if (checkboxes[i].checked) selectedCount++;
  }
  
  for (let i = 0; i < checkboxes.length; i++) {
    if (!checkboxes[i].checked) {
      checkboxes[i].disabled = selectedCount >= 3;
    }
  }
}

async function startExploration() {
  let mapId = document.getElementById('exploreMapSelect').value;
  let checkboxes = document.getElementsByName('exploreTeamCheckbox');
  let team = [];
  for (let i = 0; i < checkboxes.length; i++) {
    if (checkboxes[i].checked) team.push(checkboxes[i].value);
  }
  
  if (team.length === 0) {
    alert('您必须至少选择 1 名居民加入探索队伍！');
    return;
  }
  
  let r = await api('explore/start', { map: mapId, team: team });
  if (r.ok) {
    alert('探索开启成功！');
    load();
  } else {
    alert('探索启动失败: ' + (r.msg || '未知原因'));
  }
}

async function searchRoom() {
  let r = await api('explore/search');
  if (r.ok) {
    let msg = '搜寻结果：' + r.desc + '\n';
    if (r.rewards && r.rewards.length > 0) {
      msg += '发现物资：\n' + r.rewards.map(rw => `${rw.type}: +${rw.amount}`).join('\n');
    }
    alert(msg);
    if (r.combatTriggered) {
      alert('⚠️ 危险！遭遇敌袭，被迫卷入战斗！');
    }
    load();
  } else {
    alert('搜寻失败：' + (r.msg || '未知错误'));
  }
}

async function moveToRoom(roomId) {
  let r = await api('explore/move', { roomId });
  if (r.ok) {
    load();
  } else {
    alert('移动失败：' + (r.msg || '无法前往'));
  }
}

async function resolveSkillCheck() {
  let npcId = document.getElementById('skillCheckNPC').value;
  let r = await api('explore/skillcheck', { npcId });
  if (r.ok) {
    alert(`判定结果：\n${r.resultText}\n判定${r.success ? '成功' : '失败'}`);
    load();
  } else {
    alert('判定失败：' + (r.msg || '未知原因'));
  }
}

async function returnToBase() {
  let r = await api('explore/return');
  if (r.ok) {
    let lootMsg = '没有带回任何新物资。';
    if (r.loot && r.loot.length > 0) {
      lootMsg = r.loot.map(lt => `${lt.type}: +${lt.amount}`).join('\n');
    }
    let injuredMsg = '';
    if (r.injured && r.injured.length > 0) {
      injuredMsg = '\n\n受伤居民名单：\n' + r.injured.join(', ');
    }
    alert(`🎉 队伍安全返回基地！\n\n带回物资：\n${lootMsg}${injuredMsg}`);
    load();
  } else {
    alert('返城失败：' + (r.msg || '未知原因'));
  }
}

async function showCombatItemSelect() {
  let area = document.getElementById('combatItemSelectArea');
  if (area.style.display === 'block') {
    area.style.display = 'none';
    return;
  }
  
  let items = await(await fetch('/api/items')).json();
  let consumables = items.filter(i => i.type === 'Consumable' || i.heal > 0);
  
  if (consumables.length === 0) {
    area.innerHTML = `<span style='color:var(--text-secondary)'>背包里没有可用的战术消耗品</span>`;
  } else {
    let listHtml = `<div style='display:flex; flex-direction:column; gap:6px;'>`;
    consumables.forEach(i => {
      listHtml += `<div style='display:flex; justify-content:space-between; align-items:center; font-size:12px; padding:4px; border-bottom:1px solid rgba(255,255,255,0.05);'>` +
        `<span>${i.name} (HP恢复:${i.heal})</span>` +
        `<button class='btn-sm' style='font-size:10px;' onclick='useItemInCombat(""${i.id}"")'>使用</button>` +
        `</div>`;
    });
    listHtml += `</div>`;
    area.innerHTML = listHtml;
  }
  area.style.display = 'block';
}

async function useItemInCombat(itemId) {
  await doCombatAction('UseItem', itemId);
}

async function doCombatAction(actionStr, itemId = "") {
  let r = await api('combat/action', {
    action: actionStr,
    targetIndex: selectedEnemyIndex,
    itemId: itemId
  });
  
  if (r.ok) {
    if (r.combatEnded) {
      let msg = r.victory ? '🎉 战斗胜利！' : r.escaped ? '🏃 成功撤退！' : '💀 战斗失败，队伍受到重创！';
      let rewardText = '';
      if (r.rewards && r.rewards.length > 0) {
        rewardText = '\n\n获得战利品：\n' + r.rewards.map(rw => `${rw.type}: +${rw.amount}`).join('\n');
      }
      let injuredText = '';
      if (r.injured && r.injured.length > 0) {
        injuredText = '\n\n受伤队员：\n' + r.injured.join(', ');
      }
      
      alert(msg + rewardText + injuredText);
    }
    load();
  } else {
    alert('行动执行失败: ' + (r.msg || '未知原因'));
  }
}

function showDiaries(npcId) {
  let npc = allNPCs.find(n => n.id === npcId);
  if (!npc) return;
  
  let modal = document.getElementById('diaryModal');
  if (!modal) {
    modal = document.createElement('div');
    modal.id = 'diaryModal';
    modal.style.position = 'fixed';
    modal.style.top = '0';
    modal.style.left = '0';
    modal.style.width = '100vw';
    modal.style.height = '100vh';
    modal.style.background = 'rgba(0, 0, 0, 0.75)';
    modal.style.backdropFilter = 'blur(8px)';
    modal.style.zIndex = '9999';
    modal.style.display = 'flex';
    modal.style.alignItems = 'center';
    modal.style.justifyContent = 'center';
    modal.style.padding = '20px';
    document.body.appendChild(modal);
  }
  
  let diaryHtml = '';
  if (npc.diaries && npc.diaries.length > 0) {
    npc.diaries.forEach((d, idx) => {
      diaryHtml += `<div style='background:var(--btn-bg); border:1px solid var(--btn-border); border-radius:6px; padding:12px; margin-bottom:12px; font-size:13px; line-height:1.6; color:var(--text-primary);'>` +
        `<div style='font-weight:bold; color:var(--text-accent); margin-bottom:6px; font-family:""Orbitron"", sans-serif;'>日志记录 #${idx + 1}</div>` +
        `<p>${d}</p>` +
        `</div>`;
    });
  } else {
    diaryHtml = `<p style='text-align:center; color:var(--text-secondary); margin-top:20px;'>这里空空如也，没有任何日记记录。</p>`;
  }
  
  modal.innerHTML = `
    <div style='background:var(--panel-bg); border:1px solid var(--text-accent); border-radius:12px; width:100%; max-width:600px; max-height:80vh; overflow-y:auto; padding:24px; box-shadow:0 0 24px var(--border-glow); position:relative;'>
      <h3 style='font-family:""Orbitron"", sans-serif; color:var(--text-accent); margin-bottom:16px; display:flex; align-items:center; justify-content:space-between;'>
        <span>📖 ${npc.name} 的废土生存日记</span>
        <button class='btn-sm' style='font-size:12px; border-color:var(--text-accent);' onclick='closeDiaryModal()'>关闭</button>
      </h3>
      <div style='max-height:60vh; overflow-y:auto; padding-right:6px;'>
        ${diaryHtml}
      </div>
    </div>
  `;
  modal.style.display = 'flex';
}

function closeDiaryModal() {
  let modal = document.getElementById('diaryModal');
  if (modal) modal.style.display = 'none';
}

function toggleAIFields() {
  let provider = document.getElementById('aiProvider').value;
  document.getElementById('aiNetworkFields').style.display = provider === 'OpenAI' ? 'block' : 'none';
}

async function saveAIConfig() {
  let provider = document.getElementById('aiProvider').value;
  let apiBaseUrl = document.getElementById('aiBaseUrl')?.value || '';
  let apiKey = document.getElementById('aiApiKey')?.value || '';
  let model = document.getElementById('aiModel')?.value || '';
  
  let r = await api('ai/config', { provider, apiBaseUrl, apiKey, model });
  if (r.ok) {
    alert('AI配置保存并更新成功！');
    load();
  } else {
    alert('AI配置保存失败');
  }
}

async function testAIConfig() {
  let provider = document.getElementById('aiProvider').value;
  let apiBaseUrl = document.getElementById('aiBaseUrl')?.value || '';
  let apiKey = document.getElementById('aiApiKey')?.value || '';
  let model = document.getElementById('aiModel')?.value || '';
  
  let resDiv = document.getElementById('aiTestResult');
  resDiv.style.display = 'block';
  resDiv.style.background = 'rgba(251, 191, 36, 0.1)';
  resDiv.style.border = '1px solid #fbbf24';
  resDiv.style.color = '#fbbf24';
  resDiv.textContent = '正在测试连接，请稍候...';
  
  let r = await api('ai/test', { provider, apiBaseUrl, apiKey, model });
  if (r.ok) {
    resDiv.style.background = 'rgba(16, 185, 129, 0.1)';
    resDiv.style.border = '1px solid #10b981';
    resDiv.style.color = '#10b981';
    resDiv.textContent = `连接测试成功！延迟: ${r.latencyMs} ms`;
  } else {
    resDiv.style.background = 'rgba(239, 68, 68, 0.1)';
    resDiv.style.border = '1px solid #ef4444';
    resDiv.style.color = '#ef4444';
    resDiv.textContent = `连接测试失败: ${r.error || '未知错误'}`;
  }
}

// Initial update of button text on script load
updateThemeButton();
load();
setInterval(loadLog,3000);
</script>
</body>
</html>";
        }
    }
}
