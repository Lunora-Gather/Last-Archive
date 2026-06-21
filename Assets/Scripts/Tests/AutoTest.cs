// ============================================================
// Last Archive - 自动化系统测试
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace LastArchive
{
    /// <summary>
    /// 自动化测试 - 验证所有核心系统闭环
    /// </summary>
    public class AutoTest
    {
        private int _passCount = 0;
        private int _failCount = 0;

        public void RunAll()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("========================================");
            Console.WriteLine("  Last Archive - 系统自动化测试");
            Console.WriteLine("========================================\n");

            TestResourceSystem();
            TestNPCSystem();
            TestBuildingSystem();
            TestTimeSystem();
            TestExplorationSystem();
            TestCombatSystem();
            TestQuestSystem();
            TestItemSystem();
            TestFactionSystem();
            TestAchievementSystem();
            TestQuestChainSystem();
            TestCrisisSystem();
            TestPsychologySystem();
            TestEndingSystem();
            TestOpenAIProvider();
            TestI18nSystem();
            TestTutorialSystem();
            TestAISystem();
            TestSaveSystem();
            TestGameOverSystem();
            TestHealAndInjure();
            TestBuildingUpgrade();
            TestDailyEventSystem();
            TestCrisisResolve();
            TestEndingAllTypes();
            TestPsychologyCombatModifier();
            TestFactionReputation();
            TestSaveLoadRoundtrip();
            TestMultipleCrisisSequence();
            TestPsychologyHopeAndDespair();
            TestFullGameLoop();
            Console.WriteLine($"  测试结果: {_passCount} 通过 / {_failCount} 失败");

            if (_failCount == 0)
            {
                Console.WriteLine("\n  ★ 所有测试通过！MVP 系统闭环完整。");
            }
            else
            {
                Console.WriteLine("\n  ✗ 有测试失败，请检查。");
            }
        }

        private void Assert(bool condition, string testName, string detail = "")
        {
            if (condition)
            {
                _passCount++;
                Console.WriteLine($"  [PASS] {testName}");
            }
            else
            {
                _failCount++;
                Console.WriteLine($"  [FAIL] {testName} {detail}");
            }
        }

        // ========== 资源系统测试 ==========

        private void TestResourceSystem()
        {
            Console.WriteLine("\n--- 资源系统测试 ---");
            var rs = new ResourceSystem();
            rs.Initialize(GameConfig.InitialResources);

            Assert(rs.GetResourceAmount(ResourceType.Food) == 50, "初始食物=50");
            Assert(rs.GetResourceAmount(ResourceType.Water) == 50, "初始水=50");
            Assert(rs.GetResourceAmount(ResourceType.MemoryShards) == 0, "初始记忆碎片=0");

            rs.AddResource(ResourceType.Food, 10);
            Assert(rs.GetResourceAmount(ResourceType.Food) == 60, "增加食物后=60");

            Assert(rs.ConsumeResource(ResourceType.Food, 5), "消耗5食物成功");
            Assert(rs.GetResourceAmount(ResourceType.Food) == 55, "消耗后食物=55");

            Assert(!rs.ConsumeResource(ResourceType.MemoryShards, 1), "消耗不存在的记忆碎片失败");
            Assert(rs.HasEnough(ResourceType.Food, 55), "食物>=55");
            Assert(!rs.HasEnough(ResourceType.Food, 56), "食物<56");

            rs.ApplyDailyConsumption(3, 0);
            Assert(rs.GetResourceAmount(ResourceType.Food) == 52, "3NPC消耗食物后=52");
            Assert(rs.GetResourceAmount(ResourceType.Water) == 47, "3NPC消耗水后=47");
        }

        // ========== NPC系统测试 ==========

        private void TestNPCSystem()
        {
            Console.WriteLine("\n--- NPC系统测试 ---");
            var ns = new NPCSystem();

            var npc = new NPCInstance
            {
                Id = "test_npc", Name = "测试NPC", Role = NPCRole.Doctor,
                Health = 80, Morale = 60, Hunger = 30, Fatigue = 20, Loyalty = 50,
                Medical = 5, Engineering = 2, Scavenging = 3, Combat = 2, Social = 4,
                Memory = new NPCMemory { NPCId = "test_npc" }
            };
            npc.Relationships.Add(new NPCRelationship { TargetId = "player", Value = 0 });
            ns.AddNPC(npc);

            Assert(ns.GetNPC("test_npc") != null, "NPC存在");
            Assert(ns.GetAliveCount() == 1, "存活NPC数=1");
            Assert(npc.CanWork, "NPC可工作");
            Assert(npc.CanExplore, "NPC可探索");

            ns.AssignWork("test_npc", WorkType.Doctor);
            Assert(npc.Status == NPCStatus.Working, "分配工作后状态=Working");
            Assert(!npc.CanExplore, "工作中不可探索");

            ns.AddMemory("test_npc", new MemoryEntry
            {
                MemoryId = "m1", Day = 1, Actor = "player", Target = "test_npc",
                EventType = MemoryEventType.PlayerHelpedNPC, Description = "测试记忆",
                EmotionalWeight = 5
            });
            Assert(npc.Memory.Entries.Count == 1, "记忆条目数=1");

            ns.UpdateRelationship("test_npc", "player", 10);
            Assert(npc.GetRelationship("player") == 10, "关系值=10");

            ns.InjureNPC("test_npc", 60);
            Assert(npc.Health == 20, "受伤后HP=20");
            Assert(npc.Status == NPCStatus.Injured, "受伤状态=Injured");

            ns.HealNPC("test_npc", 20);
            Assert(npc.Health == 40, "治疗后HP=40");
            Assert(npc.Status == NPCStatus.Idle, "治疗足够后状态=Idle");
        }

        // ========== 建筑系统测试 ==========

        private void TestBuildingSystem()
        {
            Console.WriteLine("\n--- 建筑系统测试 ---");
            var rs = new ResourceSystem();
            rs.Initialize(GameConfig.InitialResources);
            var bs = new BuildingSystem(rs);

            var greenhouse = new BuildingInstance
            {
                Id = "greenhouse", Name = "温室", Level = 0, MaxLevel = 3,
                Built = false, Unlocked = true, Description = "生产食物", EffectDescription = "每天产食物"
            };
            greenhouse.BuildCost.Add(ResourceType.Parts, 5);
            greenhouse.BuildCost.Add(ResourceType.Power, 2);
            greenhouse.DailyOutput.Add(ResourceType.Food, 5);
            greenhouse.UpgradeCost.Add(ResourceType.Parts, 10);
            greenhouse.UpgradeCost.Add(ResourceType.Power, 3);
            bs.AddBuilding(greenhouse);

            Assert(!greenhouse.Built, "温室初始未建造");
            Assert(bs.Build("greenhouse"), "建造温室成功");
            Assert(greenhouse.Built, "温室已建造");
            Assert(greenhouse.Level == 1, "温室等级=1");

            // 每日产出
            bs.ProduceDailyResources();
            Assert(rs.GetResourceAmount(ResourceType.Food) == 55, "温室产出后食物=55");

            // 升级（需要额外资源）
            rs.AddResource(ResourceType.Parts, 20);
            rs.AddResource(ResourceType.Power, 10);
            Assert(bs.Upgrade("greenhouse"), "升级温室成功");
            Assert(greenhouse.Level == 2, "温室等级=2");
        }

        // ========== 时间系统测试 ==========

        private void TestTimeSystem()
        {
            Console.WriteLine("\n--- 时间系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            Assert(game.Time.CurrentDay == 1, "初始天数=1");
            Assert(game.Time.CurrentPhase == GamePhase.Day, "初始阶段=Day");

            game.Time.AdvanceToNight();
            Assert(game.Time.CurrentPhase == GamePhase.Night, "切换后阶段=Night");

            game.Time.AdvanceToSummary();
            Assert(game.Time.CurrentPhase == GamePhase.Summary, "切换后阶段=Summary");

            game.Time.AdvanceToNextDay();
            Assert(game.Time.CurrentDay == 2, "下一天天数=2");
            Assert(game.Time.CurrentPhase == GamePhase.Day, "下一天阶段=Day");
        }

        // ========== 探索系统测试 ==========

        private void TestExplorationSystem()
        {
            Console.WriteLine("\n--- 探索系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var maps = game.Exploration.GetAllMaps();
            Assert(maps.Count == 8, "地图数=8");

            // 开始探索
            var teamIds = new List<string> { "anna" };
            Assert(game.Exploration.StartExploration("abandoned_hospital", teamIds), "开始探索成功");
            Assert(game.Exploration.IsExploring, "正在探索");
            Assert(game.Exploration.CurrentRoom != null, "当前房间不为空");
            Assert(game.Exploration.CurrentRoom.Id == "entrance", "进入初始房间 entrance");

            // 移动到 corridor
            Assert(game.Exploration.MoveToRoom("corridor"), "移动到急诊走廊 corridor");

            // 移动到 surgery
            Assert(game.Exploration.MoveToRoom("surgery"), "移动到手术室 surgery");

            // 尝试移动到 locked morgue (无钥匙应失败)
            Assert(!game.Exploration.MoveToRoom("morgue"), "无钥匙尝试移动到太平间 morgue 失败");

            // 放入 hospital_key 到背包
            game.Items.AddItemFromTemplate("hospital_key", "测试");

            // 再次尝试移动到 locked morgue (有钥匙应成功，且钥匙应被消耗，morgue 被解锁)
            Assert(game.Exploration.MoveToRoom("morgue"), "有钥匙尝试移动到太平间 morgue 成功");
            Assert(game.Exploration.CurrentRoom.Id == "morgue", "当前位置在 morgue");
            Assert(!game.Exploration.CurrentRoom.Locked, "morgue 已解锁");
            
            // 检查钥匙是否已被消耗
            var inv = game.Items.GetInventory();
            bool hasKey = inv.Exists(i => i.Id == "hospital_key");
            Assert(!hasKey, "hospital_key 已从背包中被消耗");

            // 返回基地
            game.Exploration.ReturnToBase();
            Assert(!game.Exploration.IsExploring, "不再探索");
        }

        // ========== 战斗系统测试 ==========

        private void TestCombatSystem()
        {
            Console.WriteLine("\n--- 战斗系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var teamIds = new List<string> { "anna" };
            Assert(game.Combat.StartCombat(teamIds, new List<string> { "wanderer" }), "战斗开始成功");
            Assert(game.Combat.InCombat, "正在战斗");

            // 执行战斗
            Assert(game.Combat.PlayerUnits.Count > 0, "玩家单位存在");
            Assert(game.Combat.EnemyUnits.Count > 0, "敌人单位存在");

            int turns = 0;
            while (game.Combat.InCombat && turns < 20)
            {
                var result = game.Combat.ExecutePlayerAction(CombatAction.Attack);
                if (result != null)
                {
                    Assert(true, "战斗结束，结果: " + (result.Victory ? "胜利" : result.Escaped ? "逃跑" : "失败"));
                    break;
                }
                turns++;
            }
            Assert(!game.Combat.InCombat, "战斗已结束");
        }

        // ========== 任务系统测试 ==========

        private void TestQuestSystem()
        {
            Console.WriteLine("\n--- 任务系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var allQuests = game.Quests.GetAllQuests();
            Assert(allQuests.Count >= 6, "任务数>=6");
            Assert(allQuests.Count >= 20, "任务数>=20");

            var activeQuests = game.Quests.GetActiveQuests();
            Assert(activeQuests.Count >= 1, "活跃任务>=1");

            var mainQuest = game.Quests.GetQuest("main_1");
            Assert(mainQuest != null, "主线任务1存在");
            if (mainQuest != null)
            {
                Assert(mainQuest.Status == QuestStatus.Active, "主线任务1已激活");

                game.Resources.AddResource(ResourceType.MemoryShards, 1);
                game.Quests.UpdateResourceObjectives(ResourceType.MemoryShards, 1);
                Assert(mainQuest.Objectives[0].CurrentProgress >= 1, "记忆碎片目标进度>=1");
            }
        }

        // ========== 物品系统测试 ==========

        private void TestItemSystem()
        {
            Console.WriteLine("\n--- 物品系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var inventory = game.Items.GetInventory();
            Assert(inventory.Count > 0, "初始背包不为空");

            var weapons = game.Items.GetItemsByType(ItemType.Weapon);
            Assert(weapons.Count > 0, "有武器物品");

            // 装备武器
            Assert(game.Items.EquipWeapon("anna", "rusty_knife"), "装备武器成功");
            Assert(game.Items.GetNPCAttackBonus("anna") > 0, "装备后攻击加成>0");

            // 使用消耗品
            var consumables = game.Items.GetItemsByType(ItemType.Consumable);
            Assert(consumables.Count > 0, "有消耗品");
        }

        // ========== 派系系统测试 ==========

        private void TestFactionSystem()
        {
            Console.WriteLine("\n--- 派系系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var factions = game.Factions.GetAllFactions();
            Assert(factions.Count == 3, "派系数=3");

            var unlocked = game.Factions.GetUnlockedFactions();
            Assert(unlocked.Count >= 1, "至少1个已解锁派系");

            // 修改声望
            game.Factions.ChangeReputation(FactionType.Survivalists, 20);
            var faction = game.Factions.GetFaction(FactionType.Survivalists);
            Assert(faction.Reputation == 30, "声望修改正确");

            // NPC派系查询
            var annaFaction = game.Factions.GetNPCFaction("anna");
            Assert(annaFaction == FactionType.Wanderers, "安娜属于流浪者联盟");
        }

        // ========== 成就系统测试 ==========

        private void TestAchievementSystem()
        {
            Console.WriteLine("\n--- 成就系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var all = game.Achievements.GetAllAchievements();
            Assert(all.Count >= 15, "成就数>=15");

            var unlocked = game.Achievements.GetUnlockedAchievements();
            Assert(unlocked.Count == 0, "初始无解锁成就");

            // 手动解锁
            Assert(game.Achievements.Unlock("survive_3days"), "手动解锁成就成功");
            Assert(!game.Achievements.Unlock("survive_3days"), "重复解锁失败");
        }

        // ========== 任务链+派系解锁测试 ==========

        private void TestQuestChainSystem()
        {
            Console.WriteLine("\n--- 任务链+派系解锁测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // main_1 是 Active
            var main1 = game.Quests.GetQuest("main_1");
            Assert(main1 != null && main1.Status == QuestStatus.Active, "主线1初始激活");

            // main_2 是 NotStarted
            var main2 = game.Quests.GetQuest("main_2");
            Assert(main2 != null && main2.Status == QuestStatus.NotStarted, "主线2初始未开始");

            // 完成主线1 → 自动激活主线2和支线
            game.Resources.AddResource(ResourceType.MemoryShards, 5);
            game.Quests.UpdateResourceObjectives(ResourceType.MemoryShards, 5);
            Assert(main1.Status == QuestStatus.Completed, "主线1完成");
            Assert(main2.Status == QuestStatus.Active, "主线2自动激活");

            var side1 = game.Quests.GetQuest("side_1");
            Assert(side1 != null && side1.Status == QuestStatus.Active, "支线1自动激活");

            // 完成主线3 → 解锁档案教团
            var main3 = game.Quests.GetQuest("main_3");
            Assert(main3 != null, "主线3存在");
            game.Quests.StartQuest("main_3");
            game.Quests.UpdateVisitLocationObjectives("archive_ruins");
            var archiveOrder = game.Factions.GetFaction(FactionType.ArchiveOrder);
            Assert(archiveOrder != null && archiveOrder.Unlocked, "完成主线3后档案教团解锁");
        }

        // ========== 危机系统测试 ==========

        private void TestCrisisSystem()
        {
            Console.WriteLine("\n--- 危机系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            Assert(!game.Crises.HasAnyCrisis, "初始无危机");

            // 清空食物触发饥荒
            game.Resources.ConsumeResource(ResourceType.Food, game.Resources.GetResourceAmount(ResourceType.Food));
            string crisisMsg = game.Crises.CheckAndTrigger(game);
            Assert(game.Crises.HasAnyCrisis, "食物清空后触发危机");
            Assert(crisisMsg.Contains("饥荒") || crisisMsg.Contains("Famine"), "饥荒危机消息");

            // 恢复食物解决危机
            game.Resources.AddResource(ResourceType.Food, 100);
            string resolveMsg = game.Crises.TryResolve(game);
            Assert(!game.Crises.HasAnyCrisis, "食物恢复后危机解除");
        }

        // ========== 心理系统测试 ==========

        private void TestPsychologySystem()
        {
            Console.WriteLine("\n--- 心理系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var lin = game.NPCs.GetNPC("lin_doctor");
            Assert(lin != null, "林医生存在");

            var state = game.Psychology.GetState("lin_doctor");
            Assert(state == MentalState.Stable || state == MentalState.Hopeful, "林医生初始心理正常");

            // 施加创伤
            game.Psychology.ApplyTrauma("lin_doctor", "目睹惨剧");
            Assert(game.Psychology.GetState("lin_doctor") == MentalState.Traumatized, "创伤后心理=创伤");

            // 施加希望
            game.Psychology.ApplyHope("lin_doctor", "收到好消息");
            var traits = game.Psychology.GetTraits("lin_doctor");
            Assert((traits & MentalTrait.Hope) != 0, "施加希望后有希望特征");

            // 心理影响战斗
            var mod = game.Psychology.GetCombatModifier("lin_doctor");
            Assert(mod >= -3 && mod <= 2, "战斗修正值在合理范围");

            // 心理限制探索
            game.Psychology.ApplyTrauma("xiaobei", "噩梦");
            Assert(!game.Psychology.CanExplore("xiaobei"), "创伤后不能探索");
        }

        // ========== 结局系统测试 ==========

        private void TestEndingSystem()
        {
            Console.WriteLine("\n--- 结局系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var allEndings = game.Endings.GetAllEndings();
            Assert(allEndings != null && allEndings.Count == 6, "结局数=6");

            // 计算当前结局
            try
            {
                var ending = game.Endings.CalculateEnding(game);
                Assert(ending != null && ending.Type != EndingType.None, "有确定结局");
                Assert(!string.IsNullOrEmpty(ending.Title), "结局有标题");
                Assert(!string.IsNullOrEmpty(ending.Epilogue), "结局有尾声");
            }
            catch (Exception ex)
            {
                Assert(false, $"结局计算异常: {ex.Message}");
            }
        }

        // ========== OpenAI Provider 测试 ==========

        private void TestOpenAIProvider()
        {
            Console.WriteLine("\n--- OpenAI Provider 测试 ---");

            // 预设配置测试
            var glmConfig = AIProviderConfig.GLM4Flash("test-key");
            Assert(glmConfig.ApiBaseUrl.Contains("bigmodel"), "GLM URL正确");
            Assert(glmConfig.Model == "glm-4-flash", "GLM模型名正确");

            var dsConfig = AIProviderConfig.DeepSeekChat("test-key");
            Assert(dsConfig.ApiBaseUrl.Contains("deepseek"), "DeepSeek URL正确");
            Assert(dsConfig.Model == "deepseek-chat", "DeepSeek模型名正确");

            var ollamaConfig = AIProviderConfig.Ollama("qwen2.5");
            Assert(ollamaConfig.ApiBaseUrl.Contains("localhost"), "Ollama URL正确");
            Assert(ollamaConfig.Model == "qwen2.5", "Ollama模型名正确");

            var proxyConfig = AIProviderConfig.CustomProxy("https://my-proxy.com/v1", "key", "gpt-4");
            Assert(proxyConfig.ApiBaseUrl == "https://my-proxy.com/v1", "自定义中转站URL正确");
            Assert(proxyConfig.Model == "gpt-4", "自定义模型名正确");

            // OpenAIProvider 创建测试（不实际调用API）
            var provider = new OpenAIProvider(AIProviderConfig.GLM4Flash("fake-key"));
            Assert(provider.Name.Contains("glm-4-flash"), "Provider名称包含模型名");
            Assert(!provider.IsFallback, "初始未降级");

            // Fallback 测试：用无效key创建，调用时自动降级
            var providerWithBadKey = new OpenAIProvider(new AIProviderConfig
            {
                ApiBaseUrl = "https://invalid-url.example.com/v1",
                ApiKey = "bad-key",
                Model = "test-model",
                TimeoutSeconds = 3
            });

            var context = new DialogueContext
            {
                NPCName = "林医生",
                PlayerAction = "你好",
                NPCMood = "平静",
                CurrentDay = 1
            };
            string result = providerWithBadKey.GenerateDialogue(context);
            Assert(!string.IsNullOrEmpty(result), "降级后仍返回结果");
            Assert(providerWithBadKey.IsFallback, "降级标记=true");

            // 环境变量工厂测试（无环境变量时返回MockAI）
            var envProvider = AIProviderFactory.FromEnvironment();
            Assert(envProvider is MockAIProvider, "无环境变量时返回MockAI");

            // 配置文件工厂测试（无文件时返回MockAI）
            var fileProvider = AIProviderFactory.FromConfigFile("nonexistent.json");
            Assert(fileProvider is MockAIProvider, "无配置文件时返回MockAI");
        }

        // ========== i18n 系统测试 ==========

        private void TestI18nSystem()
        {
            Console.WriteLine("\n--- i18n 系统测试 ---");

            // 默认中文
            I18n.Initialize();
            Assert(I18n.CurrentLanguage == Language.Chinese, "默认语言=中文");
            Assert(I18n.T("phase.day") == "白天", "中文白天");
            Assert(I18n.T("res.food") == "食物", "中文食物");

            // 切换英文
            I18n.CurrentLanguage = Language.English;
            Assert(I18n.T("phase.day") == "Day", "英文Day");
            Assert(I18n.T("res.food") == "Food", "英文Food");
            Assert(I18n.T("res.shards") == "Memory Shards", "英文MemoryShards");

            // 切回中文
            I18n.CurrentLanguage = Language.Chinese;
            Assert(I18n.T("phase.night") == "夜晚", "切回中文");

            // 资源类型翻译
            Assert(I18n.ResourceName(ResourceType.Food) == "食物", "资源名翻译");
            Assert(I18n.ResourceName(ResourceType.MemoryShards) == "记忆碎片", "记忆碎片翻译");

            // 缺失key
            Assert(I18n.T("nonexistent.key").Contains("nonexistent"), "缺失key返回[key]");
        }

        // ========== 新手引导测试 ==========

        private void TestTutorialSystem()
        {
            Console.WriteLine("\n--- 新手引导测试 ---");

            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var tutorial = game.Tutorial;
            Assert(tutorial.Enabled, "新手引导默认启用");

            // 第1天引导
            string day1Guide = tutorial.CheckDayGuide(1, game);
            Assert(!string.IsNullOrEmpty(day1Guide), "第1天有引导");
            Assert(day1Guide.Contains("欢迎") || day1Guide.Contains("Welcome"), "引导包含欢迎语");

            // 第2天引导
            string day2Guide = tutorial.CheckDayGuide(2, game);
            Assert(!string.IsNullOrEmpty(day2Guide), "第2天有引导");

            // 同一天不重复
            string day2Again = tutorial.CheckDayGuide(2, game);
            Assert(string.IsNullOrEmpty(day2Again), "同一天不重复引导");

            // 操作提示
            string hint = tutorial.GetContextHint("day_phase");
            Assert(!string.IsNullOrEmpty(hint), "白天操作提示不为空");

            // 关闭引导
            tutorial.Enabled = false;
            string disabled = tutorial.CheckDayGuide(3, game);
            Assert(string.IsNullOrEmpty(disabled), "关闭后无引导");
        }

        // ========== 治疗/受伤测试 ==========

        private void TestHealAndInjure()
        {
            Console.WriteLine("\n--- 治疗/受伤测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var npc = game.NPCs.GetNPC("lin_doctor");
            Assert(npc != null, "林医生存在");
            int originalHp = npc.Health;

            // 受伤
            game.NPCs.InjureNPC("lin_doctor", 30);
            Assert(npc.Health == originalHp - 30, "受伤后HP减少");

            // 治疗
            game.NPCs.HealNPC("lin_doctor", 20);
            Assert(npc.Health == originalHp - 10, "治疗后HP恢复");

            // 过度治疗不超过上限
            game.NPCs.HealNPC("lin_doctor", 200);
            Assert(npc.Health <= 100, "HP不超过上限");

            // 死亡和复活
            game.NPCs.InjureNPC("lin_doctor", 200);
            Assert(!npc.IsAlive, "HP<=0时NPC死亡");

            // 对其他NPC操作
            game.NPCs.InjureNPC("anna", 10);
            var anna = game.NPCs.GetNPC("anna");
            Assert(anna.Health < 100, "安娜受伤");
        }

        // ========== 建筑升级测试 ==========

        private void TestBuildingUpgrade()
        {
            Console.WriteLine("\n--- 建筑升级测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 建造温室
            Assert(game.Buildings.Build("greenhouse"), "建造温室成功");
            var gh = game.Buildings.GetBuilding("greenhouse");
            Assert(gh != null && gh.Built, "温室已建造");
            Assert(gh.Level == 1, "初始等级=1");

            // 升级
            Assert(game.Buildings.Upgrade("greenhouse"), "升级温室成功");
            Assert(gh.Level == 2, "升级后等级=2");

            // 重复建造失败
            Assert(!game.Buildings.Build("greenhouse"), "重复建造失败");

            // 不存在的建筑
            Assert(!game.Buildings.Build("nonexistent"), "不存在建筑建造失败");
        }

        // ========== 每日事件系统测试 ==========

        private void TestDailyEventSystem()
        {
            Console.WriteLine("\n--- 每日事件系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var dailyEvents = game.DailyEvents;
            Assert(dailyEvents != null, "DailyEventSystem存在");

            // 触发多次事件，至少有一次触发
            int triggered = 0;
            for (int i = 0; i < 50; i++)
            {
                string evt = dailyEvents.TriggerRandomEvent(game);
                if (!string.IsNullOrEmpty(evt)) triggered++;
            }
            Assert(triggered > 0, "50次中至少1次触发事件");

            // 事件不崩溃
            string result = dailyEvents.TriggerRandomEvent(game);
            Assert(result != null, "事件返回非null");
        }

        // ========== 危机解决测试 ==========

        private void TestCrisisResolve()
        {
            Console.WriteLine("\n--- 危机解决测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 触发饥荒
            game.Resources.ConsumeResource(ResourceType.Food, game.Resources.GetResourceAmount(ResourceType.Food));
            game.Crises.CheckAndTrigger(game);
            Assert(game.Crises.HasAnyCrisis, "饥荒触发");

            // 恢复食物解决
            game.Resources.AddResource(ResourceType.Food, 200);
            string resolve = game.Crises.TryResolve(game);
            Assert(!game.Crises.HasAnyCrisis, "饥荒解决");

            // 停电危机
            game.Resources.ConsumeResource(ResourceType.Power, game.Resources.GetResourceAmount(ResourceType.Power));
            game.Crises.CheckAndTrigger(game);
            Assert(game.Crises.HasAnyCrisis, "停电触发");

            // 恢复电力解决
            game.Resources.AddResource(ResourceType.Power, 10);
            game.Crises.TryResolve(game);
            Assert(!game.Crises.HasAnyCrisis, "停电解决");
        }

        // ========== 结局全类型测试 ==========

        private void TestEndingAllTypes()
        {
            Console.WriteLine("\n--- 结局全类型测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            var allEndings = game.Endings.GetAllEndings();
            Assert(allEndings.Count == 6, "6种结局");
            foreach (var e in allEndings)
            {
                Assert(e.Type != EndingType.None, $"结局{e.Title}类型不为None");
                Assert(!string.IsNullOrEmpty(e.Title), $"结局有标题");
                Assert(!string.IsNullOrEmpty(e.Epilogue), $"结局有尾声");
            }

            // 广播结局
            Assert(allEndings.Any(e => e.Type == EndingType.Broadcast && e.IsGood), "广播是好结局");
            // 摧毁结局
            Assert(allEndings.Any(e => e.Type == EndingType.Destroy && !e.IsGood), "摧毁是坏结局");
        }

        // ========== 心理战斗修正测试 ==========

        private void TestPsychologyCombatModifier()
        {
            Console.WriteLine("\n--- 心理战斗修正测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 希望状态修正+2
            foreach (var npc in game.NPCs.GetAllNPCs())
            {
                if (npc.IsAlive)
                {
                    var mod = game.Psychology.GetCombatModifier(npc.Id);
                    Assert(mod >= -3 && mod <= 2, $"{npc.Name}战斗修正合理");
                }
            }

            // 创伤后修正
            game.Psychology.ApplyTrauma("anna", "噩梦");
            var traumaMod = game.Psychology.GetCombatModifier("anna");
            Assert(traumaMod <= -1, "创伤后战斗修正<=-1");

            // 心理限制探索
            Assert(!game.Psychology.CanExplore("anna"), "创伤不能探索");
        }

        // ========== 派系声望测试 ==========

        private void TestFactionReputation()
        {
            Console.WriteLine("\n--- 派系声望测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 初始声望
            var survivalists = game.Factions.GetFaction(FactionType.Survivalists);
            Assert(survivalists != null, "生存主义者存在");
            Assert(survivalists.Reputation == 10, "初始声望=10");

            // 修改声望
            game.Factions.ChangeReputation(FactionType.Survivalists, 50);
            Assert(survivalists.Reputation == 60, "声望+50后=60");

            // 负声望
            game.Factions.ChangeReputation(FactionType.Survivalists, -100);
            Assert(survivalists.Reputation < 0, "声望可以为负");

            // NPC派系查询
            var annaFaction = game.Factions.GetNPCFaction("anna");
            Assert(annaFaction == FactionType.Wanderers, "安娜=流浪者");

            // 解锁派系
            game.Factions.UnlockFaction(FactionType.ArchiveOrder);
            var archive = game.Factions.GetFaction(FactionType.ArchiveOrder);
            Assert(archive.Unlocked, "档案教团已解锁");
        }

        // ========== 存档读档往返测试 ==========

        private void TestSaveLoadRoundtrip()
        {
            Console.WriteLine("\n--- 存档读档往返测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 修改一些状态
            game.Resources.AddResource(ResourceType.Food, 30);
            game.Resources.AddResource(ResourceType.MemoryShards, 5);
            int foodBefore = game.Resources.GetResourceAmount(ResourceType.Food);
            int shardsBefore = game.Resources.GetResourceAmount(ResourceType.MemoryShards);

            // 保存
            Assert(game.SaveGame(), "保存成功");
            Assert(game.Save.HasSave(), "存档存在");

            // 新游戏覆盖状态
            game.StartNewGame();
            Assert(game.Resources.GetResourceAmount(ResourceType.Food) != foodBefore, "新游戏食物不同");

            // 读档
            Assert(game.LoadGame(), "读档成功");
            Assert(game.Resources.GetResourceAmount(ResourceType.Food) == foodBefore, "读档后食物恢复");
            Assert(game.Resources.GetResourceAmount(ResourceType.MemoryShards) == shardsBefore, "读档后碎片恢复");

            // 清理
            game.Save.DeleteSave();
        }

        // ========== 多危机连锁测试 ==========

        private void TestMultipleCrisisSequence()
        {
            Console.WriteLine("\n--- 多危机连锁测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 触发饥荒
            game.Resources.ConsumeResource(ResourceType.Food, game.Resources.GetResourceAmount(ResourceType.Food));
            game.Crises.CheckAndTrigger(game);
            int crisisCount = game.Crises.ActiveCrises.Count;
            Assert(crisisCount >= 1, "至少1个危机");

            // 触发停电
            game.Resources.ConsumeResource(ResourceType.Power, game.Resources.GetResourceAmount(ResourceType.Power));
            game.Crises.CheckAndTrigger(game);
            Assert(game.Crises.ActiveCrises.Count >= crisisCount, "危机数不减少");

            // 应用效果不崩溃
            game.Crises.ApplyEffects(game);
            Assert(true, "应用危机效果不崩溃");

            // 解决所有
            game.Resources.AddResource(ResourceType.Food, 200);
            game.Resources.AddResource(ResourceType.Power, 50);
            game.Crises.TryResolve(game);
        }

        // ========== 心理希望与绝望测试 ==========

        private void TestPsychologyHopeAndDespair()
        {
            Console.WriteLine("\n--- 心理希望与绝望测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 施加希望
            game.Psychology.ApplyHope("lin_doctor", "好消息");
            var traits = game.Psychology.GetTraits("lin_doctor");
            Assert((traits & MentalTrait.Hope) != 0, "施加希望后有希望特征");
            Assert((traits & MentalTrait.Trust) != 0, "施加希望后有信任特征");

            // 施加绝望（通过降低士气）
            var lin = game.NPCs.GetNPC("lin_doctor");
            lin.Morale = 5;
            lin.Loyalty = 5;
            game.Psychology.DailyUpdate(lin);
            var state = game.Psychology.GetState("lin_doctor");
            Assert(state == MentalState.Despair || state == MentalState.Anxious, "低士气导致绝望/焦虑");

            // 绝望蔓延
            var rng = new Random();
            game.Psychology.ApplyDespairSpread(game.NPCs, rng);
            Assert(true, "绝望蔓延不崩溃");
        }


        // ========== GameOver系统测试 ==========

        private void TestGameOverSystem()
        {
            Console.WriteLine("\n--- GameOver系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 初始不结束
            game.CheckGameOver();
            Assert(!game.IsGameOver, "初始游戏未结束");

            // 全灭NPC触发结束
            foreach (var npc in game.NPCs.GetAllNPCs())
            {
                game.NPCs.InjureNPC(npc.Id, 200);
            }
            game.CheckGameOver();
            Assert(game.IsGameOver, "全灭NPC后游戏结束");
            Assert(!game.IsVictory, "不是胜利结局");
        }

        // ========== AI系统测试 ==========

        private void TestAISystem()
        {
            Console.WriteLine("\n--- AI系统测试 ---");
            var ai = new MockAIProvider();
            Assert(ai.Name == "MockAI", "MockAI名称正确");

            // 对话生成
            var dialogue = ai.GenerateDialogue(new DialogueContext
            {
                NPCId = "lin_doctor", NPCName = "林医生", NPCRole = "Doctor",
                RelationshipValue = 10, NPCMood = "normal", CurrentDay = 1
            });
            Assert(!string.IsNullOrEmpty(dialogue), "对话生成不为空");
            Assert(dialogue.Contains("林医生"), "对话包含NPC名字");

            // 任务生成
            var questText = ai.GenerateQuest(new QuestContext { CurrentDay = 1 });
            Assert(!string.IsNullOrEmpty(questText), "任务生成不为空");

            // 记忆总结
            var summary = ai.SummarizeMemory(new MemoryContext
            {
                NPCId = "anna", NPCName = "安娜", Day = 1,
                TodayEntries = new List<MemoryEntry>()
            });
            Assert(!string.IsNullOrEmpty(summary), "记忆总结不为空");

            // 每日事件
            var evt = ai.GenerateDailyEvent(new EventContext { CurrentDay = 1, NPCCount = 5 });
            Assert(!string.IsNullOrEmpty(evt), "每日事件不为空");

            // 内容校验
            var validator = new ContentValidator();
            validator.RegisterNPCIds(new[] { "lin_doctor", "anna" });
            validator.RegisterLocationIds(new[] { "abandoned_hospital" });

            var validQuest = new QuestData
            {
                QuestId = "test_valid_quest", Title = "测试", Description = "测试",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                RelatedNPCs = new List<string> { "lin_doctor" },
                RelatedLocation = "abandoned_hospital",
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "abandoned_hospital", RequiredAmount = 1 }
                }
            };
            string error;
            Assert(validator.ValidateQuest(validQuest, out error), "合法任务校验通过");

            var invalidQuest = new QuestData
            {
                QuestId = "test_invalid_quest", Title = "测试", Description = "测试",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                RelatedNPCs = new List<string> { "nonexistent_npc" },
                Objectives = new List<QuestObjective>()
            };
            Assert(!validator.ValidateQuest(invalidQuest, out error), "非法任务校验拒绝");
        }

        // ========== 存档系统测试 ==========

        private void TestSaveSystem()
        {
            Console.WriteLine("\n--- 存档系统测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 保存
            Assert(game.SaveGame(), "保存游戏成功");

            // 先验证加载第1天存档
            var game1 = new GameManager();
            game1.Initialize(new MockAIProvider());
            game1.StartNewGame();
            bool loadOk = game1.LoadGame();
            Assert(loadOk, "加载第1天存档成功");
            if (loadOk)
            {
                Assert(game1.Time.CurrentDay == 1, $"加载第1天后天数=1(实际{game1.Time.CurrentDay})");
            }

            // 推进一天再保存，测试天数恢复
            game.Time.AdvanceToNight();
            game.Time.AdvanceToSummary();
            game.Time.AdvanceToNextDay();
            int savedDay = game.Time.CurrentDay;
            int savedFood = game.Resources.GetResourceAmount(ResourceType.Food);
            Assert(game.SaveGame(), "保存第2天游戏成功");

            // 加载到新游戏实例
            var game2 = new GameManager();
            game2.Initialize(new MockAIProvider());
            game2.StartNewGame();
            Assert(game2.LoadGame(), "加载游戏成功");

            Assert(game2.Time.CurrentDay == savedDay, $"加载后天数一致(期望{savedDay},实际{game2.Time.CurrentDay})");
            Assert(game2.Resources.GetResourceAmount(ResourceType.Food) == savedFood, "加载后食物一致");

            // 清理
            game.Save.DeleteSave();
            Assert(!game.Save.HasSave(), "删除存档成功");
        }

        // ========== 完整游戏闭环测试 ==========

        private void TestFullGameLoop()
        {
            Console.WriteLine("\n--- 完整游戏闭环测试 ---");
            var game = new GameManager();
            game.Initialize(new MockAIProvider());
            game.StartNewGame();

            // 模拟5天游戏流程
            for (int day = 1; day <= 5; day++)
            {
                Assert(game.Time.CurrentDay == day, $"第{day}天开始");
                Assert(game.Time.CurrentPhase == GamePhase.Day, $"第{day}天阶段=Day");

                // 白天操作：与NPC对话
                string dialogue = game.TalkToNPC("lin_doctor");
                Assert(!string.IsNullOrEmpty(dialogue), $"第{day}天对话成功");

                // 分配工作
                game.NPCs.AssignWork("old_zhou", WorkType.Engineer);

                // 结束白天
                game.Time.AdvanceToNight();
                Assert(game.Time.CurrentPhase == GamePhase.Night, $"第{day}天进入夜晚");

                // 夜晚探索
                var teamIds = new List<string> { "anna" };
                string[] mapIds = { "abandoned_hospital", "subway_ruins", "archive_ruins", "ruined_park", "broadcast_tower" };
                string mapId = mapIds[(day - 1) % 5];

                if (game.Exploration.StartExploration(mapId, teamIds))
                {
                    // 搜索几个房间
                    for (int i = 0; i < 3; i++)
                    {
                        var evt = game.Exploration.SearchRoom();
                        if (evt != null && evt.Type == ExplorationEventType.EnemyEncounter && !string.IsNullOrEmpty(evt.EnemyId))
                        {
                            // 战斗
                            if (game.Combat.StartCombat(teamIds, new List<string> { evt.EnemyId }))
                            {
                                int turns = 0;
                                while (game.Combat.InCombat && turns < 15)
                                {
                                    game.Combat.ExecutePlayerAction(CombatAction.Attack);
                                    turns++;
                                }
                            }
                        }

                        var connected = game.Exploration.GetConnectedRooms();
                        if (connected.Count > 0)
                        {
                            game.Exploration.MoveToRoom(connected[0].Id);
                        }
                    }

                    game.Exploration.ReturnToBase();
                }

                // 结束夜晚
                game.Time.AdvanceToSummary();
                Assert(game.Time.CurrentPhase == GamePhase.Summary, $"第{day}天进入结算");

                // 进入下一天
                game.Time.AdvanceToNextDay();

                // 检查游戏结束
                game.CheckGameOver();
                if (game.IsGameOver) break;
            }

            Assert(game.Time.CurrentDay >= 2, "至少经历了2天");

            // 检查NPC记忆
            bool hasMemory = false;
            foreach (var npc in game.NPCs.GetAllNPCs())
            {
                if (npc.Memory.Entries.Count > 0 || !string.IsNullOrEmpty(npc.Memory.Summary))
                {
                    hasMemory = true;
                    break;
                }
            }
            Assert(hasMemory, "NPC有记忆记录");

            // 检查资源变化
            Assert(game.Resources.GetResourceAmount(ResourceType.Food) >= 0, "食物>=0");
            Assert(game.Resources.GetResourceAmount(ResourceType.Water) >= 0, "水>=0");

            Console.WriteLine($"\n  5天闭环后状态:");
            Console.WriteLine($"    天数: {game.Time.CurrentDay}");
            Console.WriteLine($"    食物: {game.Resources.GetResourceAmount(ResourceType.Food)}");
            Console.WriteLine($"    水: {game.Resources.GetResourceAmount(ResourceType.Water)}");
            Console.WriteLine($"    记忆碎片: {game.Resources.GetResourceAmount(ResourceType.MemoryShards)}");
            Console.WriteLine($"    存活NPC: {game.NPCs.GetAliveCount()}");
            Console.WriteLine($"    活跃任务: {game.Quests.GetActiveQuests().Count}");
        }
    }
}
