// ============================================================
// Last Archive - 探索系统
// ============================================================

using System;
using System.Collections.Generic;

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
        // 当前探索状态
        public bool IsExploring { get; private set; }
        public ExplorationMapData CurrentMap { get; private set; }
        public RoomData CurrentRoom { get; private set; }
        public List<string> TeamNPCIds { get; private set; } = new List<string>();
        public ExplorationResult CurrentResult { get; private set; }
        public SkillCheckEvent ActiveCheck { get; private set; }
        public HashSet<string> SearchedRooms { get; private set; } = new HashSet<string>();

        private Random _rng = new Random();

        public ExplorationSystem(ResourceSystem resourceSystem, NPCSystem npcSystem, ItemSystem itemSystem = null)
        {
            _resourceSystem = resourceSystem;
            _npcSystem = npcSystem;
            _itemSystem = itemSystem;
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
            if (map == null || !map.Unlocked) return false;
            if (teamNPCIds == null || teamNPCIds.Count == 0) return false;
            if (teamNPCIds.Count > GameConfig.MaxExplorationTeamSize) return false;

            // 验证队伍
            var psychology = ServiceLocator.Get<PsychologySystem>();
            foreach (var npcId in teamNPCIds)
            {
                var npc = _npcSystem.GetNPC(npcId);
                if (npc == null || !npc.CanExplore) return false;
                if (psychology != null && !psychology.CanExplore(npcId)) return false;
            }

            CurrentMap = map;
            TeamNPCIds = new List<string>(teamNPCIds);
            CurrentResult = new ExplorationResult();
            IsExploring = true;
            SearchedRooms.Clear();

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
                    Day = 0, // 由外部设置
                    Actor = "player",
                    Target = npcId,
                    EventType = MemoryEventType.NPCJoinedExploration,
                    Description = $"参加了{map.Name}的探索",
                    EmotionalWeight = 1
                });
            }

            EventBus.Publish(new OnExplorationStarted { MapId = mapId });
            return true;
        }

        /// <summary>移动到指定房间</summary>
        public bool MoveToRoom(string roomId)
        {
            if (!IsExploring || CurrentMap == null) return false;

            // 检查是否有未解决的判定事件
            if (ActiveCheck != null && !ActiveCheck.Resolved) return false;

            // 检查是否相邻
            if (!CurrentRoom.ConnectedRooms.Contains(roomId)) return false;

            // 找到房间
            foreach (var room in CurrentMap.Rooms)
            {
                if (room.Id == roomId)
                {
                    // 检查锁定并尝试消耗钥匙解锁
                    if (room.Locked && !string.IsNullOrEmpty(room.RequiredItem))
                    {
                        if (_itemSystem != null)
                        {
                            bool hasKey = false;
                            foreach (var item in _itemSystem.GetInventory())
                            {
                                if (item.Id == room.RequiredItem)
                                {
                                    hasKey = true;
                                    break;
                                }
                            }
                            if (hasKey)
                            {
                                _itemSystem.RemoveItem(room.RequiredItem);
                                room.Locked = false;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }

                    CurrentRoom = room;
                    CurrentRoom.Visited = true;
                    return true;
                }
            }
            return false;
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
                    _resourceSystem.AddResource(ResourceType.Parts, 4);
                    resultText = $"{npc.Name} 成功破解了电子锁！获得 4 零件。";
                }
                else if (ActiveCheck.TargetSkill == "medical")
                {
                    _resourceSystem.AddResource(ResourceType.Medicine, 2);
                    resultText = $"{npc.Name} 成功进行了抗辐射中和！获得 2 药品。";
                }
                else
                {
                    _resourceSystem.AddResource(ResourceType.MemoryShards, 1);
                    resultText = $"{npc.Name} 成功击碎并破拆了钢筋混凝土墙！发现 1 记忆碎片。";
                }
            }
            else
            {
                success = false;
                if (ActiveCheck.TargetSkill == "engineering")
                {
                    npc.Morale = Math.Max(0, npc.Morale - 15);
                    resultText = $"{npc.Name} 判定失败，触发了警报线圈，全队士气低落 (-15 士气)。";
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
            if (!IsExploring || CurrentRoom == null) return null;
            SearchedRooms.Add(CurrentRoom.Id);

            var evt = new ExplorationEvent
            {
                Id = Guid.NewGuid().ToString()
            };

            // 30% 几率触发判定事件（仅在房间Danger > 2，且当前没有判定事件时）
            if (CurrentRoom.DangerLevel > 2 && ActiveCheck == null && _rng.Next(100) < 30)
            {
                TriggerSkillCheck();
                evt.Type = ExplorationEventType.TrapEvent;
                evt.Description = ActiveCheck.Description;
                CurrentResult.Events.Add(evt);
                return evt;
            }

            // 根据事件类型决定搜索结果
            int roll = _rng.Next(100);

            if (CurrentRoom.EventType == ExplorationEventType.FindResources || roll < 40)
            {
                evt.Type = ExplorationEventType.FindResources;
                evt.Description = $"在{CurrentRoom.Name}中发现了物资！";

                foreach (var loot in CurrentRoom.LootTable)
                {
                    if (_rng.Next(100) < GameConfig.BaseSearchSuccessRate)
                    {
                        int amount = loot.Amount > 0 ? _rng.Next(1, loot.Amount + 1) : 1;
                        evt.Rewards.Add(new ResourceAmount(loot.Type, amount));
                        _resourceSystem.AddResource(loot.Type, amount);
                        CurrentResult.CollectedResources.Add(new ResourceAmount(loot.Type, amount));
                    }
                }

                if (_itemSystem != null && _rng.Next(100) < 20)
                {
                    var loot = _itemSystem.GenerateLoot(CurrentRoom.DangerLevel, _rng);
                    foreach (var item in loot)
                    {
                        _itemSystem.AddItem(item, "探索掉落");
                        evt.Description += $" 获得[{item.Name}]！";
                    }
                }
            }
            else if (CurrentRoom.EventType == ExplorationEventType.FindMemoryShard || (roll >= 40 && roll < 55))
            {
                evt.Type = ExplorationEventType.FindMemoryShard;
                evt.Description = $"在{CurrentRoom.Name}中发现了一枚记忆碎片！";
                _resourceSystem.AddResource(ResourceType.MemoryShards, 1);
                evt.Rewards.Add(new ResourceAmount(ResourceType.MemoryShards, 1));
                CurrentResult.CollectedResources.Add(new ResourceAmount(ResourceType.MemoryShards, 1));
            }
            else if (CurrentRoom.EventType == ExplorationEventType.EnemyEncounter || (roll >= 55 && roll < 80))
            {
                evt.Type = ExplorationEventType.EnemyEncounter;
                evt.Description = $"在{CurrentRoom.Name}遭遇了敌人！";
                CurrentResult.HadCombat = true;

                if (CurrentMap.MainEnemies.Count > 0)
                {
                    evt.EnemyId = CurrentMap.MainEnemies[_rng.Next(CurrentMap.MainEnemies.Count)];
                }
            }
            else
            {
                evt.Type = ExplorationEventType.StoryEvent;
                evt.Description = $"在{CurrentRoom.Name}中什么也没发现。";
            }

            // 危险房间可能受伤
            if (CurrentRoom.DangerLevel > 3 && _rng.Next(100) < GameConfig.DangerInjuryChance)
            {
                string injuredId = TeamNPCIds[_rng.Next(TeamNPCIds.Count)];
                _npcSystem.InjureNPC(injuredId, 10);
                CurrentResult.InjuredNPCs.Add(injuredId);
                evt.Description += $" {injuredId}在搜索中受了轻伤。";
            }

            CurrentResult.Events.Add(evt);
            return evt;
        }

        /// <summary>返回基地</summary>
        public ExplorationResult ReturnToBase()
        {
            if (!IsExploring) return null;

            foreach (var npcId in TeamNPCIds)
            {
                var npc = _npcSystem.GetNPC(npcId);
                if (npc != null && npc.Status == NPCStatus.Working)
                {
                    _npcSystem.SetNPCStatus(npcId, NPCStatus.Idle);
                }
            }

            var resultMapId = CurrentMap?.Id ?? "";
            var result = CurrentResult;
            IsExploring = false;
            CurrentMap = null;
            CurrentRoom = null;
            TeamNPCIds.Clear();
            CurrentResult = null;
            ActiveCheck = null;

            EventBus.Publish(new OnExplorationEnded { MapId = resultMapId, Success = true });
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
