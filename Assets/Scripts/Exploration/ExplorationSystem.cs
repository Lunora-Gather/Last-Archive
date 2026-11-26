// ============================================================
// Last Archive - 探索系统
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace LastArchive
{
    /// <summary>
    /// 探索系统 - 管理地图、房间移动、搜索和事件
    /// </summary>
    public class ExplorationSystem
    {
        private readonly Dictionary<string, ExplorationMapData> _maps = new Dictionary<string, ExplorationMapData>();
        private ResourceSystem _resourceSystem;
        private NPCSystem _npcSystem;
        private ItemSystem _itemSystem;
        private readonly GameManager _game;
        private RoomData _pendingEncounter;
        public const int BypassEngineering = 8;
        public const int BypassScavenging = 6;
        public const int BypassPower = 4;
        public static string BypassRequirements => $"工程 ≥ {BypassEngineering} + 另一位搜寻 ≥ {BypassScavenging} · 消耗 {BypassPower} 电力";
        // 当前探索状态
        public bool IsExploring { get; private set; }
        public ExplorationMapData CurrentMap { get; private set; }
        public RoomData CurrentRoom { get; private set; }
        public List<string> TeamNPCIds { get; private set; } = new List<string>();
        public ExplorationResult CurrentResult { get; private set; }
        public SkillCheckEvent ActiveCheck { get; private set; }
        public HashSet<string> SearchedRooms { get; private set; } = new HashSet<string>();
        public bool HasSurvivors => TeamNPCIds.Exists(id => _npcSystem.GetNPC(id)?.IsAlive == true);

        private Random _rng = new Random();

        public ExplorationSystem(ResourceSystem resourceSystem, NPCSystem npcSystem, ItemSystem itemSystem = null, GameManager game = null)
        {
            _resourceSystem = resourceSystem;
            _npcSystem = npcSystem;
            _itemSystem = itemSystem;
            _game = game;
        }

        /// <summary>添加地图</summary>
        public void AddMap(ExplorationMapData map)
        {
            _maps[map.Id] = map;
        }

        /// <summary>获取所有地图</summary>
        public List<ExplorationMapData> GetAllMaps()
        {
            return new List<ExplorationMapData>(_maps.Values);
        }

        /// <summary>获取地图</summary>
        public ExplorationMapData GetMap(string id)
        {
            return _maps.TryGetValue(id, out var m) ? m : null;
        }

        /// <summary>选择地图和队伍，开始探索</summary>
        public bool StartExploration(string mapId, List<string> teamNPCIds)
        {
            var map = GetMap(mapId);
            if (IsExploring || _game?.Combat.InCombat == true || map == null || !map.Unlocked || map.Rooms.Count == 0) return false;
            if (teamNPCIds == null || teamNPCIds.Count == 0) return false;
            if (teamNPCIds.Count > GameConfig.MaxExplorationTeamSize ||
                new HashSet<string>(teamNPCIds).Count != teamNPCIds.Count) return false;

            // 验证队伍
            var psychology = ServiceLocator.Get<PsychologySystem>();
            foreach (var npcId in teamNPCIds)
            {
                var npc = string.IsNullOrEmpty(npcId) ? null : _npcSystem.GetNPC(npcId);
                if (npc == null || !npc.CanExplore ||
                    _game?.TodayDutyWorker == npcId) return false;
                if (psychology != null && !psychology.CanExplore(npcId)) return false;
            }
            if (_game != null && !_game.PayForDeparture(mapId)) return false;
            _game?.RecordDepartedTeam(teamNPCIds);

            CurrentMap = map;
            TeamNPCIds = new List<string>(teamNPCIds);
            CurrentResult = new ExplorationResult();
            IsExploring = true;
            SearchedRooms.Clear();
            _pendingEncounter = null;

            // 进入第一个房间
            CurrentRoom = map.Rooms[0];
            CurrentRoom.Visited = true;

            // 记录NPC参与探索
            foreach (var npcId in TeamNPCIds)
            {
                _npcSystem.SetNPCStatus(npcId, NPCStatus.Working);
                _npcSystem.AddMemory(npcId, new MemoryEntry
                {
                    MemoryId = Guid.NewGuid().ToString(),
                    Day = _game?.Time.CurrentDay ?? 0,
                    Actor = "player",
                    Target = npcId,
                    EventType = MemoryEventType.NPCJoinedExploration,
                    Description = $"参加了{map.Name}的探索",
                    EmotionalWeight = 1
                });
            }

            EventBus.Publish(new OnExplorationStarted { MapId = mapId });
            EventBus.Publish(new OnExplorationRoomEntered { RoomId = CurrentRoom.Id });
            return true;
        }

        /// <summary>移动到指定房间</summary>
        public bool MoveToRoom(string roomId)
        {
            if (!IsExploring || !HasSurvivors || _pendingEncounter != null || _game?.Combat.InCombat == true) return false;

            // 检查是否有未解决的判定事件
            if (ActiveCheck != null && !ActiveCheck.Resolved) return false;

            // 检查是否相邻
            if (!CurrentRoom.ConnectedRooms.Contains(roomId)) return false;

            // 找到房间
            foreach (var room in CurrentMap.Rooms)
            {
                if (room.Id == roomId)
                {
                    if (!CanEnter(room)) return false;
                    // 检查锁定并尝试消耗钥匙解锁
                    if (room.Locked)
                    {
                        if (string.IsNullOrEmpty(room.RequiredItem) || _itemSystem?.RemoveItem(room.RequiredItem) != true)
                            return false;
                        room.Locked = false;
                    }

                    CurrentRoom = room;
                    CurrentRoom.Visited = true;
                    EventBus.Publish(new OnExplorationRoomEntered { RoomId = room.Id });
                    return true;
                }
            }
            return false;
        }

        public bool HasClearance(RoomData room) => string.IsNullOrEmpty(room.RequiredClearance) ||
            CurrentMap?.Rooms.Exists(entry => entry.Id == room.RequiredClearance && entry.Cleared) == true;

        public bool CanEnter(RoomData room) => HasClearance(room) &&
            (!room.Locked || (!string.IsNullOrEmpty(room.RequiredItem) && _itemSystem?.HasItem(room.RequiredItem) == true));

        public bool CanOfferTowerBypass => IsExploring && HasSurvivors && CurrentRoom.Evidence == ExpeditionEvidence.Tower &&
            !CurrentRoom.Cleared && _pendingEncounter == null && !SearchedRooms.Contains(CurrentRoom.Id);

        public bool CanBypassTower => CanOfferTowerBypass && TowerBypassIssue.Length == 0;
        public string TowerBypassIssue
        {
            get
            {
                if (!CanOfferTowerBypass) return "当前不能接线绕过守备。";
                var alive = TeamNPCIds.Select(_npcSystem.GetNPC).Where(npc => npc?.IsAlive == true).ToArray();
                if (!alive.Any(engineer => engineer.Engineering >= BypassEngineering &&
                    alive.Any(spotter => engineer != spotter && spotter.Scavenging >= BypassScavenging)))
                    return "需要工程师控线、另一位队员稳住天线。";
                return !_resourceSystem.HasEnough(ResourceType.Power, BypassPower) ? "可用电力不足。" : "";
            }
        }

        private bool HasEngineering(int minimum) => minimum <= 0 || TeamNPCIds.Any(id =>
            _npcSystem.GetNPC(id) is { IsAlive: true } resident && resident.Engineering >= minimum);

        public string SearchRequirement
        {
            get
            {
                if (CurrentRoom == null || CurrentRoom.Claimed) return "";
                if (!HasEngineering(CurrentRoom.SearchEngineering)) return $"需要工程 ≥ {CurrentRoom.SearchEngineering} 的队员";
                if (!_resourceSystem.HasEnough(ResourceType.Power, CurrentRoom.SearchPower))
                    return $"需要 {CurrentRoom.SearchPower} 电力";
                return "";
            }
        }

        public bool BypassTower()
        {
            if (!CanBypassTower) return false;
            _resourceSystem.ConsumeResource(ResourceType.Power, BypassPower);
            SearchedRooms.Add(CurrentRoom.Id);
            CurrentRoom.Cleared = true;
            CurrentResult.TowerEvidence = true;
            return true;
        }

        internal void ResolveEncounter(bool victory, IEnumerable<string> injured = null)
        {
            var room = _pendingEncounter;
            _pendingEncounter = null;
            if (CurrentResult == null) return;
            CurrentResult.CombatVictory = victory;
            if (injured != null)
                foreach (var id in injured)
                    if (!CurrentResult.InjuredNPCs.Contains(id)) CurrentResult.InjuredNPCs.Add(id);
            if (!victory || room == null) return;
            room.Cleared = true;
            if (room.Evidence == ExpeditionEvidence.Tower) CurrentResult.TowerEvidence = true;
        }

        /// <summary>触发判定事件</summary>
        public SkillCheckEvent TriggerSkillCheck()
        {
            if (CurrentRoom == null) return null;

            string[] skills = { "engineering", "medical", "combat" };
            string skill = skills[_rng.Next(skills.Length)];
            int difficulty = Math.Max(5, 5 + CurrentRoom.DangerLevel);

            string desc = "";
            if (skill == "engineering")
                desc = $"在{CurrentRoom.Name}发现一扇关闭的防爆电子锁门，需要工程学判定绕过电路（难度: {difficulty}）";
            else if (skill == "medical")
                desc = $"在{CurrentRoom.Name}空气中弥漫着辐射泄露的气味，需要医疗判定进行抗辐射防护（难度: {difficulty}）";
            else
                desc = $"在{CurrentRoom.Name}遭遇一堵钢筋障碍墙，需要战斗力判定以强力破拆通过（难度: {difficulty}）";

            ActiveCheck = new SkillCheckEvent
            {
                Description = desc,
                TargetSkill = skill,
                Difficulty = difficulty,
                Resolved = false,
                ResultText = ""
            };

            return ActiveCheck;
        }

        /// <summary>解决判定事件</summary>
        public bool ResolveSkillCheck(string npcId, out string resultText, out bool success)
        {
            resultText = "";
            success = false;
            if (ActiveCheck == null || ActiveCheck.Resolved) return false;

            var npc = _npcSystem.GetNPC(npcId);
            if (npc == null || !npc.IsAlive || !TeamNPCIds.Contains(npcId)) return false;

            int skillVal = 0;
            if (ActiveCheck.TargetSkill == "engineering") skillVal = npc.Engineering;
            else if (ActiveCheck.TargetSkill == "medical") skillVal = npc.Medical;
            else if (ActiveCheck.TargetSkill == "combat") skillVal = npc.Combat;

            // 投骰子 (1-20) + 技能值 >= 难度
            int roll = _rng.Next(1, 21);
            int total = roll + skillVal;
            if (total >= ActiveCheck.Difficulty)
            {
                success = true;
                if (ActiveCheck.TargetSkill == "engineering")
                {
                    CollectReward(ResourceType.Parts, 4);
                    resultText = $"{npc.Name} 成功破解了电子锁！获得 4 零件。";
                }
                else if (ActiveCheck.TargetSkill == "medical")
                {
                    CollectReward(ResourceType.Medicine, 2);
                    resultText = $"{npc.Name} 成功进行了抗辐射中和！获得 2 药品。";
                }
                else
                {
                    CollectReward(ResourceType.MemoryShards, 1);
                    resultText = $"{npc.Name} 成功击碎并破拆了钢筋混凝土墙！发现 1 记忆碎片。";
                }
            }
            else
            {
                success = false;
                if (ActiveCheck.TargetSkill == "engineering")
                {
                    npc.Morale = Math.Max(0, npc.Morale - 15);
                    resultText = $"{npc.Name} 触发了警报，士气 -15。";
                }
                else if (ActiveCheck.TargetSkill == "medical")
                {
                    _npcSystem.InjureNPC(npc.Id, 20);
                    resultText = $"{npc.Name} 判定失败，吸入有毒尘埃，自身健康受损 (-20 HP)。";
                }
                else
                {
                    npc.Fatigue = Math.Min(GameConfig.MaxNPCFatigue, npc.Fatigue + 25);
                    resultText = $"{npc.Name} 判定失败，搬运障碍瓦砾过度劳累，疲劳值上升 (+25 疲劳)。";
                }
            }

            ActiveCheck.Resolved = true;
            ActiveCheck.ResultText = resultText;

            // 写入记录
            CurrentResult.Events.Add(new ExplorationEvent
            {
                Id = Guid.NewGuid().ToString(),
                Type = ExplorationEventType.StoryEvent,
                Description = $"[判定] " + resultText
            });

            ActiveCheck = null; // 解除当前阻塞状态
            return true;
        }

        /// <summary>搜索当前房间</summary>
        public ExplorationEvent SearchRoom()
        {
            if (!IsExploring || !HasSurvivors || CurrentRoom == null || ActiveCheck != null ||
                _pendingEncounter != null || _game?.Combat.InCombat == true || SearchRequirement.Length > 0 ||
                !SearchedRooms.Add(CurrentRoom.Id)) return null;
            if (!CurrentRoom.Claimed) _resourceSystem.ConsumeResource(ResourceType.Power, CurrentRoom.SearchPower);

            var evt = new ExplorationEvent
            {
                Id = Guid.NewGuid().ToString()
            };
            if (CurrentRoom.Cleared && CurrentRoom.Encounter.Count > 0)
            {
                evt.Type = ExplorationEventType.StoryEvent;
                evt.Description = "这里的警戒已经解除。";
                if (CurrentRoom.Evidence == ExpeditionEvidence.Tower) CurrentResult.TowerEvidence = true;
                CurrentResult.Events.Add(evt);
                return evt;
            }

            // 有固定剧情的房间优先执行其预设事件。
            if (CurrentRoom.EventType == null && CurrentRoom.DangerLevel > 2 && _rng.Next(100) < 30)
            {
                TriggerSkillCheck();
                evt.Type = ExplorationEventType.TrapEvent;
                evt.Description = ActiveCheck.Description;
                CurrentResult.Events.Add(evt);
                return evt;
            }

            var eventType = CurrentRoom.EventType ?? (_rng.Next(100) switch
            {
                < 40 => ExplorationEventType.FindResources,
                < 55 => ExplorationEventType.FindMemoryShard,
                < 80 => ExplorationEventType.EnemyEncounter,
                _ => ExplorationEventType.StoryEvent
            });

            if (eventType == ExplorationEventType.FindResources)
            {
                evt.Type = ExplorationEventType.FindResources;
                if (CurrentRoom.IsFiniteCache && CurrentRoom.Claimed)
                {
                    evt.Type = ExplorationEventType.StoryEvent;
                    evt.Description = "这里的固定物资已经取完。";
                }
                else
                {
                    evt.Description = $"在{CurrentRoom.Name}中发现了物资！";
                    var lootTable = CurrentRoom.LootTable.Count > 0
                        ? CurrentRoom.LootTable
                        : CurrentMap.MainLoot.ConvertAll(type => new ResourceAmount(type,
                            type is ResourceType.Food or ResourceType.Water ? 6 : type == ResourceType.MemoryShards ? 1 : 4));
                    foreach (var loot in lootTable)
                    {
                        if (CurrentRoom.IsFiniteCache) RecordLoot(evt, loot.Type, loot.Amount);
                        else if (_rng.Next(100) < GameConfig.BaseSearchSuccessRate)
                            RecordLoot(evt, loot.Type, _rng.Next(1, Math.Max(1, loot.Amount) + 1));
                    }
                    if (CurrentRoom.IsFiniteCache) CurrentRoom.Claimed = true;
                    if (_itemSystem != null && _rng.Next(100) < 20)
                    {
                        foreach (var item in _itemSystem.GenerateLoot(CurrentRoom.DangerLevel, _rng))
                            if (_itemSystem.AddItem(item, "探索掉落"))
                                evt.Description += $" 获得[{item.Name}]！";
                    }
                }
            }
            else if (eventType == ExplorationEventType.FindMemoryShard)
            {
                evt.Type = ExplorationEventType.FindMemoryShard;
                int amount = CurrentRoom.Claimed ? 0 : CurrentRoom.MemoryShardReward;
                if (CurrentRoom.EventType == ExplorationEventType.FindMemoryShard) CurrentRoom.Claimed = true;
                evt.Description = amount > 0 ? $"在{CurrentRoom.Name}中发现了 {amount} 枚记忆碎片！" : "这里的记忆碎片已经取走。";
                if (amount > 0) RecordLoot(evt, ResourceType.MemoryShards, amount);
            }
            else if (eventType == ExplorationEventType.EnemyEncounter && CurrentMap.MainEnemies.Count > 0)
            {
                evt.Type = ExplorationEventType.EnemyEncounter;
                evt.Description = $"在{CurrentRoom.Name}遭遇了敌人！";
                CurrentResult.HadCombat = true;

                if (CurrentRoom.Encounter.Count > 0)
                {
                    evt.EnemyIds.AddRange(CurrentRoom.Encounter);
                    _pendingEncounter = CurrentRoom;
                }
                else
                    evt.EnemyIds.Add(CurrentMap.MainEnemies[_rng.Next(CurrentMap.MainEnemies.Count)]);
            }
            else
            {
                evt.Type = ExplorationEventType.StoryEvent;
                evt.Description = CurrentRoom.EventType == ExplorationEventType.StoryEvent
                    ? CurrentRoom.Description : $"在{CurrentRoom.Name}中什么也没发现。";
            }

            if (CurrentRoom.Evidence == ExpeditionEvidence.Archive)
            {
                CurrentResult.ArchiveEvidence = true;
                evt.Description += " 发现了旧城档案，返城后可定位广播塔。";
            }
            var itemId = CurrentRoom.SearchItemId;
            if (!string.IsNullOrEmpty(itemId) && _itemSystem != null && !_itemSystem.HasItem(itemId))
            {
                var item = _itemSystem.GetTemplate(itemId);
                if (item != null)
                {
                    evt.Description += _itemSystem.AddItemFromTemplate(itemId, "探索掉落")
                        ? $" 获得[{item.Name}]！"
                        : $" 背包空间不足，无法带走[{item.Name}]。";
                }
            }
            if (evt.Type == ExplorationEventType.FindResources && evt.Rewards.Count == 0 &&
                !evt.Description.Contains("获得[") && !evt.Description.Contains("背包"))
                evt.Description = $"在{CurrentRoom.Name}中未找到可用物资。";

            // 危险房间可能受伤
            if (CurrentRoom.EventType == null && CurrentRoom.DangerLevel > 3 && _rng.Next(100) < GameConfig.DangerInjuryChance)
            {
                string injuredId = TeamNPCIds[_rng.Next(TeamNPCIds.Count)];
                _npcSystem.InjureNPC(injuredId, 10);
                CurrentResult.InjuredNPCs.Add(injuredId);
                evt.Description += $" {injuredId}在搜索中受了轻伤。";
            }

            CurrentResult.Events.Add(evt);
            return evt;
        }

        private void RecordLoot(ExplorationEvent evt, ResourceType type, int amount)
        {
            evt.Rewards.Add(new ResourceAmount(type, amount));
            CollectReward(type, amount);
        }

        internal void CollectReward(ResourceType type, int amount)
        {
            if (IsExploring) CurrentResult.CollectedResources.Add(new ResourceAmount(type, amount));
            else _resourceSystem.AddResource(type, amount);
        }

        /// <summary>返回基地</summary>
        public ExplorationResult ReturnToBase()
        {
            if (!IsExploring || _pendingEncounter != null || _game?.Combat.InCombat == true) return null;

            int fatigue = Math.Min(20, 6 + 4 * SearchedRooms.Count);
            foreach (var npcId in TeamNPCIds)
            {
                var npc = _npcSystem.GetNPC(npcId);
                if (npc?.IsAlive != true) continue;
                npc.Fatigue = Math.Min(GameConfig.MaxNPCFatigue, npc.Fatigue + fatigue);
                if (npc.Status == NPCStatus.Working) _npcSystem.SetNPCStatus(npcId, NPCStatus.Idle);
            }

            var resultMapId = CurrentMap?.Id ?? "";
            var result = CurrentResult;
            bool survived = HasSurvivors;
            IsExploring = false;
            CurrentMap = null;
            CurrentRoom = null;
            TeamNPCIds.Clear();
            CurrentResult = null;
            ActiveCheck = null;

            if (survived)
            {
                foreach (var loot in result.CollectedResources) _resourceSystem.AddResource(loot.Type, loot.Amount);
                _game?.SyncResourceObjectives();
                _game?.RecordReturnedEvidence(result);
            }
            else result.CollectedResources.Clear();
            EventBus.Publish(new OnExplorationEnded { MapId = resultMapId, Success = survived });
            return result;
        }

        /// <summary>获取当前房间可前往的房间</summary>
        public List<RoomData> GetConnectedRooms()
        {
            var result = new List<RoomData>();
            if (CurrentMap == null || CurrentRoom == null) return result;

            foreach (var roomId in CurrentRoom.ConnectedRooms)
            {
                foreach (var room in CurrentMap.Rooms)
                {
                    if (room.Id == roomId) result.Add(room);
                }
            }
            return result;
        }
    }
}
