// ============================================================
// Last Archive - 游戏管理器
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 游戏管理器 - 顶层协调器，不堆砌逻辑
    /// </summary>
    public class GameManager
    {
        // ===== 核心系统 =====
        public ResourceSystem Resources { get; private set; }
        public NPCSystem NPCs { get; private set; }
        public BuildingSystem Buildings { get; private set; }
        public ExplorationSystem Exploration { get; private set; }
        public CombatSystem Combat { get; private set; }
        public QuestSystem Quests { get; private set; }
        public TimeSystem Time { get; private set; }
        public SaveSystem Save { get; private set; }

        // ===== 新系统 =====
        public FactionSystem Factions { get; private set; }
        public ItemSystem Items { get; private set; }
        public AchievementSystem Achievements { get; private set; }

        // ===== AI系统 =====
        public IAIProvider AIProvider { get; private set; }
        public ContentValidator Validator { get; private set; }
        public QuestGenerator QuestGen { get; private set; }
        public DialogueGenerator DialogueGen { get; private set; }
        public MemorySummarizer MemSummarizer { get; private set; }
        public EventGenerator EventGen { get; private set; }

        // ===== 游戏状态 =====
        public bool IsInitialized { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsVictory { get; private set; }
        public string LastDailyEvent { get; private set; }
        public int TotalDaysSurvived { get; private set; }
        public int TotalExplorationsCompleted { get; private set; }
        public int TotalCombatsWon { get; private set; }
        private int _starvationDays = 0;
        public DailyEventSystem DailyEvents { get; private set; }
        public CrisisSystem Crises { get; private set; }
        public EndingSystem Endings { get; private set; }
        public PsychologySystem Psychology { get; private set; }
        public TutorialSystem Tutorial { get; private set; }

        public GameManager()
        {
        }

        /// <summary>初始化所有系统</summary>
        public void Initialize(IAIProvider aiProvider = null)
        {
            // 清理
            ServiceLocator.Clear();
            EventBus.Clear();

            // AI
            AIProvider = aiProvider ?? new MockAIProvider();
            Validator = new ContentValidator();
            QuestGen = new QuestGenerator(AIProvider, Validator);
            DialogueGen = new DialogueGenerator(AIProvider);
            MemSummarizer = new MemorySummarizer(AIProvider);
            EventGen = new EventGenerator(AIProvider);

            // 核心系统
            Resources = new ResourceSystem();
            NPCs = new NPCSystem();
            Buildings = new BuildingSystem(Resources);
            Items = new ItemSystem(NPCs, Resources);
            Exploration = new ExplorationSystem(Resources, NPCs, Items);
            Quests = new QuestSystem(Resources, NPCs);
            Time = new TimeSystem(Resources, NPCs, Buildings, Quests, MemSummarizer, EventGen);
            Save = new SaveSystem();

            // 新系统（需要在Combat之前初始化，因为Combat依赖ItemSystem）
            Factions = new FactionSystem();
            Achievements = new AchievementSystem();
            Combat = new CombatSystem(Resources, NPCs, Items);
            DailyEvents = new DailyEventSystem();
            Crises = new CrisisSystem();
            Endings = new EndingSystem();
            Psychology = new PsychologySystem();
            Tutorial = new TutorialSystem();
            I18n.Initialize();
            ServiceLocator.Register(Resources);
            ServiceLocator.Register(NPCs);
            ServiceLocator.Register(Buildings);
            ServiceLocator.Register(Exploration);
            ServiceLocator.Register(Combat);
            ServiceLocator.Register(Quests);
            ServiceLocator.Register(Time);
            ServiceLocator.Register(Items);
            ServiceLocator.Register(Factions);
            ServiceLocator.Register(Achievements);
            ServiceLocator.Register(Psychology);

            // 订阅事件
            EventBus.Subscribe<OnResourceCrisis>(OnResourceCrisis);
            EventBus.Subscribe<OnGameOver>(OnGameOverEvent);
            EventBus.Subscribe<OnCombatEnded>(OnCombatEndedEvent);
            EventBus.Subscribe<OnExplorationEnded>(OnExplorationEndedEvent);
            EventBus.Subscribe<OnAchievementUnlocked>(OnAchievementUnlockedEvent);
            EventBus.Subscribe<OnQuestStatusChanged>(OnQuestStatusChangedEvent);
            EventBus.Subscribe<OnBuildingBuilt>(OnBuildingBuiltEvent);

            IsInitialized = true;
            IsGameOver = false;
            IsVictory = false;
            TotalDaysSurvived = 0;
            TotalExplorationsCompleted = 0;
            TotalCombatsWon = 0;
        }

        /// <summary>动态更新AI提供者</summary>
        public void UpdateAIProvider(IAIProvider newProvider)
        {
            AIProvider = newProvider;
            QuestGen = new QuestGenerator(AIProvider, Validator);
            DialogueGen = new DialogueGenerator(AIProvider);
            MemSummarizer = new MemorySummarizer(AIProvider);
            EventGen = new EventGenerator(AIProvider);
            Time.UpdateAI(MemSummarizer, EventGen);
        }

        /// <summary>开始新游戏</summary>
        public void StartNewGame()
        {
            // 初始化资源
            Resources.Initialize(GameConfig.InitialResources);

            // 初始化各系统
            InitializeNPCs();
            InitializeBuildings();
            InitializeMaps();
            InitializeEnemies();
            InitializeQuests();
            InitializeItems();
            InitializeFactions();
            InitializeAchievements();
            InitializeValidator();

            // 初始化心理系统
            foreach (var npc in NPCs.GetAllNPCs())
            {
                Psychology.Initialize(npc.Id, npc.Morale, npc.Loyalty);
            }

            // 开始时间
            Time.StartNewGame();
            Achievements.UpdateDay(1);
        }

        /// <summary>初始化8个NPC（原5个+3个新NPC）</summary>
        private void InitializeNPCs()
        {
            var npcs = new List<NPCInstance>
            {
                // === 原有5个 ===
                new NPCInstance
                {
                    Id = "lin_doctor", Name = "林医生", Age = 35, Role = NPCRole.Doctor,
                    Description = "灾难前是医院急诊医生，对失去的病人始终怀有愧疚。",
                    Health = 80, Morale = 60, Hunger = 30, Fatigue = 20, Loyalty = 50,
                    Medical = 8, Engineering = 2, Scavenging = 3, Combat = 2, Social = 5,
                    Traits = new List<NPCTrait> { NPCTrait.Kind, NPCTrait.Cautious },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 0 } }
                },
                new NPCInstance
                {
                    Id = "old_zhou", Name = "老周", Age = 55, Role = NPCRole.Engineer,
                    Description = "曾经是地铁维修工，熟悉旧城市地下结构。",
                    Health = 70, Morale = 65, Hunger = 25, Fatigue = 30, Loyalty = 60,
                    Medical = 2, Engineering = 9, Scavenging = 5, Combat = 3, Social = 4,
                    Traits = new List<NPCTrait> { NPCTrait.Practical, NPCTrait.Loyal },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 5 } }
                },
                new NPCInstance
                {
                    Id = "anna", Name = "安娜", Age = 28, Role = NPCRole.Scout,
                    Description = "独自在废墟里生活了两年，不轻易相信任何人。",
                    Health = 90, Morale = 40, Hunger = 20, Fatigue = 15, Loyalty = 30,
                    Medical = 3, Engineering = 3, Scavenging = 8, Combat = 6, Social = 3,
                    Traits = new List<NPCTrait> { NPCTrait.Brave, NPCTrait.Suspicious },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = -5 } }
                },
                new NPCInstance
                {
                    Id = "xiaobei", Name = "小北", Age = 10, Role = NPCRole.Child,
                    Description = "记不清父母的样子，但总会画同一座灯塔。",
                    Health = 50, Morale = 50, Hunger = 40, Fatigue = 10, Loyalty = 70,
                    Medical = 1, Engineering = 1, Scavenging = 2, Combat = 0, Social = 7,
                    Traits = new List<NPCTrait> { NPCTrait.Kind, NPCTrait.Fearful },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 10 } }
                },
                new NPCInstance
                {
                    Id = "shen_mu", Name = "沈牧", Age = 40, Role = NPCRole.Stranger,
                    Description = "一个沉默的幸存者，似乎知道档案城真正的用途。",
                    Health = 75, Morale = 55, Hunger = 25, Fatigue = 25, Loyalty = 40,
                    Medical = 4, Engineering = 5, Scavenging = 5, Combat = 5, Social = 8,
                    Traits = new List<NPCTrait> { NPCTrait.Calm, NPCTrait.Mysterious },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 0 } }
                },
                // === 新增3个 ===
                new NPCInstance
                {
                    Id = "mei_cook", Name = "梅姐", Age = 45, Role = NPCRole.Doctor,
                    Description = "曾是学校食堂的大厨，灾难后自学了基础医疗。性格泼辣但心地善良。",
                    Health = 65, Morale = 70, Hunger = 20, Fatigue = 30, Loyalty = 55,
                    Medical = 4, Engineering = 1, Scavenging = 3, Combat = 2, Social = 7,
                    Traits = new List<NPCTrait> { NPCTrait.Kind, NPCTrait.Brave },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 5 } }
                },
                new NPCInstance
                {
                    Id = "iron_guard", Name = "铁柱", Age = 32, Role = NPCRole.Scout,
                    Description = "前军人，右臂在灾难中受伤。沉默寡言，但守护同伴的决心从未动摇。",
                    Health = 85, Morale = 50, Hunger = 25, Fatigue = 20, Loyalty = 65,
                    Medical = 2, Engineering = 3, Scavenging = 4, Combat = 9, Social = 2,
                    Traits = new List<NPCTrait> { NPCTrait.Loyal, NPCTrait.Calm },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 10 } }
                },
                new NPCInstance
                {
                    Id = "scholar_wang", Name = "王学者", Age = 60, Role = NPCRole.Stranger,
                    Description = "灾难前是大学教授，研究的是记忆与意识。对档案城有独特的见解。",
                    Health = 45, Morale = 45, Hunger = 35, Fatigue = 40, Loyalty = 35,
                    Medical = 3, Engineering = 4, Scavenging = 2, Combat = 1, Social = 9,
                    Traits = new List<NPCTrait> { NPCTrait.Mysterious, NPCTrait.Cautious },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = -5 } }
                },
                // === 第二批7个 ===
                new NPCInstance
                {
                    Id = "lily_nurse", Name = "莉莉", Age = 26, Role = NPCRole.Doctor,
                    Description = "林医生的助手，温柔细心，灾难前是护理专业学生。",
                    Health = 60, Morale = 55, Hunger = 25, Fatigue = 30, Loyalty = 45,
                    Medical = 6, Engineering = 1, Scavenging = 2, Combat = 1, Social = 6,
                    Traits = new List<NPCTrait> { NPCTrait.Kind, NPCTrait.Cautious },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 5 } }
                },
                new NPCInstance
                {
                    Id = "big_hu", Name = "大虎", Age = 38, Role = NPCRole.Scout,
                    Description = "前建筑工人，力大无穷但脑子转得慢。对朋友极其忠诚。",
                    Health = 95, Morale = 60, Hunger = 35, Fatigue = 15, Loyalty = 70,
                    Medical = 1, Engineering = 6, Scavenging = 3, Combat = 7, Social = 2,
                    Traits = new List<NPCTrait> { NPCTrait.Loyal, NPCTrait.Brave },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 15 } }
                },
                new NPCInstance
                {
                    Id = "qin_mechanic", Name = "小秦", Age = 22, Role = NPCRole.Engineer,
                    Description = "天才机械师，能修好任何东西。性格孤僻，只跟机器说话。",
                    Health = 55, Morale = 35, Hunger = 30, Fatigue = 40, Loyalty = 30,
                    Medical = 1, Engineering = 10, Scavenging = 4, Combat = 2, Social = 1,
                    Traits = new List<NPCTrait> { NPCTrait.Practical, NPCTrait.Calm },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 0 } }
                },
                new NPCInstance
                {
                    Id = "grandma_liu", Name = "刘奶奶", Age = 72, Role = NPCRole.Doctor,
                    Description = "退休中医，用草药治病救人。大家叫她'活着的百科全书'。",
                    Health = 40, Morale = 65, Hunger = 30, Fatigue = 50, Loyalty = 60,
                    Medical = 7, Engineering = 0, Scavenging = 1, Combat = 0, Social = 8,
                    Traits = new List<NPCTrait> { NPCTrait.Kind, NPCTrait.Calm },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 15 } }
                },
                new NPCInstance
                {
                    Id = "fox_trader", Name = "狐狸", Age = 30, Role = NPCRole.Stranger,
                    Description = "自称商人，总能在废墟中找到稀奇玩意儿。没人知道他的真名。",
                    Health = 70, Morale = 50, Hunger = 20, Fatigue = 20, Loyalty = 20,
                    Medical = 2, Engineering = 3, Scavenging = 9, Combat = 4, Social = 8,
                    Traits = new List<NPCTrait> { NPCTrait.Selfish, NPCTrait.Mysterious },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = -10 } }
                },
                new NPCInstance
                {
                    Id = "dawn_scout", Name = "黎明", Age = 24, Role = NPCRole.Scout,
                    Description = "代号黎明的侦察兵，从北方幸存者营地远道而来，带来外界消息。",
                    Health = 75, Morale = 45, Hunger = 25, Fatigue = 35, Loyalty = 25,
                    Medical = 3, Engineering = 2, Scavenging = 7, Combat = 5, Social = 4,
                    Traits = new List<NPCTrait> { NPCTrait.Brave, NPCTrait.Suspicious },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 0 } }
                },
                new NPCInstance
                {
                    Id = "echo_child", Name = "回声", Age = 13, Role = NPCRole.Child,
                    Description = "沉默的女孩，从不说话但画出的画令人心碎。小北唯一的朋友。",
                    Health = 45, Morale = 30, Hunger = 35, Fatigue = 15, Loyalty = 50,
                    Medical = 0, Engineering = 2, Scavenging = 3, Combat = 1, Social = 3,
                    Traits = new List<NPCTrait> { NPCTrait.Fearful, NPCTrait.Calm },
                    Relationships = new List<NPCRelationship> { new NPCRelationship { TargetId = "player", Value = 5 } }
                }
            };

            foreach (var npc in npcs)
            {
                npc.Memory = new NPCMemory { NPCId = npc.Id };
                NPCs.AddNPC(npc);
            }

            // NPC间关系
            NPCs.UpdateRelationship("xiaobei", "lin_doctor", 20);
            NPCs.UpdateRelationship("lin_doctor", "xiaobei", 15);
            NPCs.UpdateRelationship("anna", "shen_mu", -10);
            NPCs.UpdateRelationship("iron_guard", "old_zhou", 15);
            NPCs.UpdateRelationship("old_zhou", "iron_guard", 10);
            NPCs.UpdateRelationship("mei_cook", "xiaobei", 20);
            NPCs.UpdateRelationship("scholar_wang", "shen_mu", 10);
            NPCs.UpdateRelationship("lily_nurse", "lin_doctor", 25);
            NPCs.UpdateRelationship("lin_doctor", "lily_nurse", 20);
            NPCs.UpdateRelationship("big_hu", "iron_guard", 15);
            NPCs.UpdateRelationship("xiaobei", "echo_child", 30);
            NPCs.UpdateRelationship("echo_child", "xiaobei", 25);
            NPCs.UpdateRelationship("fox_trader", "scholar_wang", -15);
            NPCs.UpdateRelationship("grandma_liu", "mei_cook", 20);
        }

        /// <summary>初始化建筑（原3个+2个新建筑）</summary>
        private void InitializeBuildings()
        {
            // 档案馆
            var archiveHall = new BuildingInstance
            {
                Id = "archive_hall", Name = "档案馆", Description = "保存记忆碎片和推进主线。",
                Level = 1, MaxLevel = GameConfig.MaxBuildingLevel, Built = true, Unlocked = true,
                EffectDescription = "解锁记忆总结和主线任务"
            };

            // 工坊
            var workshop = new BuildingInstance
            {
                Id = "workshop", Name = "工坊", Description = "升级建筑、制作装备、修理广播塔。",
                Level = 1, MaxLevel = GameConfig.MaxBuildingLevel, Built = true, Unlocked = true,
                EffectDescription = "启用升级功能"
            };
            workshop.UpgradeCost.Add(ResourceType.Parts, GameConstants.BuildingUpgradePartsBase);
            workshop.UpgradeCost.Add(ResourceType.Power, GameConstants.BuildingUpgradePowerBase);

            // 温室
            var greenhouse = new BuildingInstance
            {
                Id = "greenhouse", Name = "温室", Description = "生产食物。",
                Level = 0, MaxLevel = GameConfig.MaxBuildingLevel, Built = false, Unlocked = true,
                EffectDescription = "每天生产食物"
            };
            greenhouse.BuildCost.Add(ResourceType.Parts, GameConstants.GreenhouseBuildPartsCost);
            greenhouse.BuildCost.Add(ResourceType.Power, GameConstants.GreenhouseBuildPowerCost);
            greenhouse.DailyOutput.Add(ResourceType.Food, GameConstants.GreenhouseFoodProduction);
            greenhouse.UpgradeCost.Add(ResourceType.Parts, 8);
            greenhouse.UpgradeCost.Add(ResourceType.Power, 2);

            // === 新增建筑 ===

            // 水处理站
            var waterStation = new BuildingInstance
            {
                Id = "water_station", Name = "水处理站", Description = "净化水源，每天生产水。",
                Level = 0, MaxLevel = GameConfig.MaxBuildingLevel, Built = false, Unlocked = true,
                EffectDescription = "每天生产水"
            };
            waterStation.BuildCost.Add(ResourceType.Parts, 6);
            waterStation.BuildCost.Add(ResourceType.Power, 3);
            waterStation.DailyOutput.Add(ResourceType.Water, 4);
            waterStation.UpgradeCost.Add(ResourceType.Parts, 10);
            waterStation.UpgradeCost.Add(ResourceType.Power, 3);

            // 医疗室
            var medRoom = new BuildingInstance
            {
                Id = "med_room", Name = "医疗室", Description = "治疗受伤的NPC，每天自动恢复少量生命值。",
                Level = 0, MaxLevel = GameConfig.MaxBuildingLevel, Built = false, Unlocked = true,
                EffectDescription = "受伤NPC每日额外恢复5HP"
            };
            medRoom.BuildCost.Add(ResourceType.Parts, 4);
            medRoom.BuildCost.Add(ResourceType.Medicine, 3);
            medRoom.UpgradeCost.Add(ResourceType.Parts, 8);
            medRoom.UpgradeCost.Add(ResourceType.Medicine, 2);

            Buildings.AddBuilding(archiveHall);
            Buildings.AddBuilding(workshop);
            Buildings.AddBuilding(greenhouse);
            Buildings.AddBuilding(waterStation);
            Buildings.AddBuilding(medRoom);

            // === 新增3座建筑 ===
            var watchtower = new BuildingInstance
            {
                Id = "watchtower", Name = "瞭望塔", Description = "监视周围动静，减少夜袭损失。",
                Level = 0, MaxLevel = 3, Built = false, Unlocked = true,
                EffectDescription = "减少夜晚探索受伤概率"
            };
            watchtower.BuildCost.Add(ResourceType.Parts, 8);
            watchtower.BuildCost.Add(ResourceType.Power, 3);
            watchtower.DailyConsumption.Add(ResourceType.Power, 1);
            watchtower.UpgradeCost.Add(ResourceType.Parts, 12);
            watchtower.UpgradeCost.Add(ResourceType.Power, 4);

            var market = new BuildingInstance
            {
                Id = "market", Name = "集市", Description = "与商人交易物资。",
                Level = 0, MaxLevel = 3, Built = false, Unlocked = true,
                EffectDescription = "解锁交易功能"
            };
            market.BuildCost.Add(ResourceType.Parts, 6);
            market.BuildCost.Add(ResourceType.Power, 2);
            market.UpgradeCost.Add(ResourceType.Parts, 10);
            market.UpgradeCost.Add(ResourceType.Power, 3);

            var generator = new BuildingInstance
            {
                Id = "generator", Name = "发电机", Description = "独立供电，增加电力产出。",
                Level = 0, MaxLevel = 3, Built = false, Unlocked = true,
                EffectDescription = "每日产出电力"
            };
            generator.BuildCost.Add(ResourceType.Parts, 10);
            generator.BuildCost.Add(ResourceType.Power, 2);
            generator.DailyOutput.Add(ResourceType.Power, 3);
            generator.UpgradeCost.Add(ResourceType.Parts, 15);
            generator.UpgradeCost.Add(ResourceType.Power, 4);

            Buildings.AddBuilding(watchtower);
            Buildings.AddBuilding(market);
            Buildings.AddBuilding(generator);
        }

        /// <summary>初始化地图（原3个+2个新地图）</summary>
        private void InitializeMaps()
        {
            // === 废弃医院 ===
            var hospital = new ExplorationMapData
            {
                Id = "abandoned_hospital", Name = "废弃医院",
                Description = "灾难前的急救中心，如今只剩破碎病床和发霉药柜。",
                MainLoot = new List<ResourceType> { ResourceType.Medicine, ResourceType.MemoryShards },
                MainEnemies = new List<string> { "wanderer", "toxic_spore" }
            };
            hospital.Rooms.Add(new RoomData { Id = "entrance", Name = "入口", Description = "医院大厅，一片狼藉。", DangerLevel = 1, ConnectedRooms = new List<string> { "corridor" } });
            hospital.Rooms.Add(new RoomData { Id = "corridor", Name = "走廊", Description = "昏暗的走廊，墙上有抓痕。", DangerLevel = 2, ConnectedRooms = new List<string> { "entrance", "pharmacy", "surgery" } });
            hospital.Rooms.Add(new RoomData { Id = "pharmacy", Name = "药房", Description = "部分药品还在货架上。", DangerLevel = 1, ConnectedRooms = new List<string> { "corridor" } });
            hospital.Rooms.Add(new RoomData { Id = "surgery", Name = "手术室", Description = "手术台上残留着暗色痕迹。", DangerLevel = 3, ConnectedRooms = new List<string> { "corridor", "morgue" } });
            hospital.Rooms.Add(new RoomData { Id = "morgue", Name = "太平间", Description = "冰柜中隐约传来声响。", DangerLevel = 4, Locked = true, RequiredItem = "hospital_key", ConnectedRooms = new List<string> { "surgery" } });

            // === 地铁废墟 ===
            var subway = new ExplorationMapData
            {
                Id = "subway_ruins", Name = "地铁废墟",
                Description = "坍塌的地铁隧道，老周说这里通向城市的地下网络。",
                MainLoot = new List<ResourceType> { ResourceType.Parts, ResourceType.Power },
                MainEnemies = new List<string> { "mechanical_guard", "scavenger_gang" }
            };
            subway.Rooms.Add(new RoomData { Id = "platform", Name = "站台", Description = "破碎的站台，广告牌还亮着微光。", DangerLevel = 2, ConnectedRooms = new List<string> { "tunnel", "control_room" } });
            subway.Rooms.Add(new RoomData { Id = "tunnel", Name = "隧道", Description = "黑暗的隧道，远处有脚步声。", DangerLevel = 3, ConnectedRooms = new List<string> { "platform", "storage" } });
            subway.Rooms.Add(new RoomData { Id = "control_room", Name = "控制室", Description = "老旧的控制台，也许还能启动什么。", DangerLevel = 2, ConnectedRooms = new List<string> { "platform" } });
            subway.Rooms.Add(new RoomData { Id = "storage", Name = "物资仓库", Description = "被遗忘的仓库，可能还有有用的东西。", DangerLevel = 3, ConnectedRooms = new List<string> { "tunnel" } });

            // === 旧档案城 ===
            var archive_ruins = new ExplorationMapData
            {
                Id = "archive_ruins", Name = "旧档案城",
                Description = "档案城的旧址，据说这里保存着灾难前的核心记忆。",
                MainLoot = new List<ResourceType> { ResourceType.MemoryShards },
                MainEnemies = new List<string> { "shadow_creature", "memory_ghost", "archive_warden" }
            };
            archive_ruins.Rooms.Add(new RoomData { Id = "gate", Name = "大门", Description = "锈蚀的铁门，上面刻着档案城的标记。", DangerLevel = 3, ConnectedRooms = new List<string> { "hall" } });
            archive_ruins.Rooms.Add(new RoomData { Id = "hall", Name = "大厅", Description = "穹顶大厅，记忆碎片在空中飘浮。", DangerLevel = 4, ConnectedRooms = new List<string> { "gate", "archive_room", "server_room" } });
            archive_ruins.Rooms.Add(new RoomData { Id = "archive_room", Name = "档案室", Description = "一排排数据柜，有些还在运转。", DangerLevel = 5, Locked = true, RequiredItem = "archive_key", ConnectedRooms = new List<string> { "hall" } });
            archive_ruins.Rooms.Add(new RoomData { Id = "server_room", Name = "服务器室", Description = "嗡嗡作响的服务器，核心记忆就在这里。", DangerLevel = 5, Locked = true, RequiredItem = "archive_key", ConnectedRooms = new List<string> { "hall" } });

            // === 废墟公园 ===
            var park = new ExplorationMapData
            {
                Id = "ruined_park", Name = "废墟公园",
                Description = "曾经的市民公园，如今被变异植物覆盖。",
                MainLoot = new List<ResourceType> { ResourceType.Food, ResourceType.Medicine },
                MainEnemies = new List<string> { "mutant_plant", "frost_crawler" }
            };
            park.Rooms.Add(new RoomData { Id = "park_entrance", Name = "公园入口", Description = "锈蚀的铁门和倒塌的售票亭。", DangerLevel = 1, ConnectedRooms = new List<string> { "fountain" } });
            park.Rooms.Add(new RoomData { Id = "fountain", Name = "喷泉广场", Description = "干涸的喷泉，长满了藤蔓。", DangerLevel = 2, ConnectedRooms = new List<string> { "park_entrance", "greenhouse", "grove" } });
            park.Rooms.Add(new RoomData { Id = "greenhouse", Name = "温室遗迹", Description = "破碎的玻璃温室，里面还有活的植物。", DangerLevel = 2, ConnectedRooms = new List<string> { "fountain" } });
            park.Rooms.Add(new RoomData { Id = "grove", Name = "小树林", Description = "密林深处，光线几乎无法穿透。", DangerLevel = 3, ConnectedRooms = new List<string> { "fountain" } });

            // === 广播塔 ===
            var tower = new ExplorationMapData
            {
                Id = "broadcast_tower", Name = "广播塔",
                Description = "城市最高的建筑，如果能修复天线，也许能联系外界。",
                MainLoot = new List<ResourceType> { ResourceType.Parts, ResourceType.Power },
                MainEnemies = new List<string> { "broken_android", "shadow_swarm" }
            };
            tower.Rooms.Add(new RoomData { Id = "lobby", Name = "大厅", Description = "空旷的大厅，电梯早已停运。", DangerLevel = 2, ConnectedRooms = new List<string> { "stairs" } });
            tower.Rooms.Add(new RoomData { Id = "stairs", Name = "楼梯间", Description = "向上延伸的楼梯，越往上越危险。", DangerLevel = 3, ConnectedRooms = new List<string> { "lobby", "antenna_deck" } });
            tower.Rooms.Add(new RoomData { Id = "antenna_deck", Name = "天线平台", Description = "塔顶的天线平台，可以看到远方的灯火。", DangerLevel = 4, Locked = true, RequiredItem = "tower_key", ConnectedRooms = new List<string> { "stairs" } });

            Exploration.AddMap(hospital);
            Exploration.AddMap(subway);
            Exploration.AddMap(archive_ruins);
            Exploration.AddMap(park);
            Exploration.AddMap(tower);

            // === 地下避难所 ===
            var shelter = new ExplorationMapData
            {
                Id = "underground_shelter", Name = "地下避难所",
                Description = "灾难前政府的秘密避难所，据说储备了大量物资。",
                MainLoot = new List<ResourceType> { ResourceType.Food, ResourceType.Medicine, ResourceType.Parts },
                MainEnemies = new List<string> { "mechanical_guard", "broken_android" }
            };
            shelter.Rooms.Add(new RoomData { Id = "bunker_door", Name = "避难所大门", Description = "厚重的防爆门，需要电力才能开启。", DangerLevel = 2, ConnectedRooms = new List<string> { "corridor_b" } });
            shelter.Rooms.Add(new RoomData { Id = "corridor_b", Name = "走廊", Description = "应急灯还亮着，空气中有消毒水味。", DangerLevel = 2, ConnectedRooms = new List<string> { "bunker_door", "supply_room", "med_bay" } });
            shelter.Rooms.Add(new RoomData { Id = "supply_room", Name = "物资库", Description = "架子上还有密封的罐头和药品。", DangerLevel = 3, ConnectedRooms = new List<string> { "corridor_b" } });
            shelter.Rooms.Add(new RoomData { Id = "med_bay", Name = "医疗舱", Description = "完好的医疗设备，像是从未使用过。", DangerLevel = 2, ConnectedRooms = new List<string> { "corridor_b" } });

            // === 旧工厂 ===
            var factory = new ExplorationMapData
            {
                Id = "old_factory", Name = "旧工厂",
                Description = "曾经制造零件的工厂，也许还能找到能用的设备。",
                MainLoot = new List<ResourceType> { ResourceType.Parts, ResourceType.Power },
                MainEnemies = new List<string> { "scavenger_gang", "mechanical_guard", "frost_crawler" }
            };
            factory.Rooms.Add(new RoomData { Id = "factory_gate", Name = "工厂大门", Description = "锈蚀的铁门半开着。", DangerLevel = 2, ConnectedRooms = new List<string> { "workshop_f" } });
            factory.Rooms.Add(new RoomData { Id = "workshop_f", Name = "车间", Description = "巨大的车间，机器还保持着生产姿态。", DangerLevel = 3, ConnectedRooms = new List<string> { "factory_gate", "warehouse_f" } });
            factory.Rooms.Add(new RoomData { Id = "warehouse_f", Name = "成品仓库", Description = "整齐排列的零件箱，有些还没开封。", DangerLevel = 3, ConnectedRooms = new List<string> { "workshop_f" } });

            // === 信号中继站 ===
            var relay = new ExplorationMapData
            {
                Id = "relay_station", Name = "信号中继站",
                Description = "山丘上的中继站，修复它可以扩大广播范围。",
                MainLoot = new List<ResourceType> { ResourceType.Parts, ResourceType.MemoryShards },
                MainEnemies = new List<string> { "shadow_swarm", "memory_ghost" }
            };
            relay.Rooms.Add(new RoomData { Id = "relay_path", Name = "山路", Description = "蜿蜒上山的路，风声呼啸。", DangerLevel = 2, ConnectedRooms = new List<string> { "relay_building" } });
            relay.Rooms.Add(new RoomData { Id = "relay_building", Name = "中继站", Description = "小型建筑，天线已经折断。", DangerLevel = 3, ConnectedRooms = new List<string> { "relay_path", "relay_roof" } });
            relay.Rooms.Add(new RoomData { Id = "relay_roof", Name = "屋顶", Description = "从这里可以看到整个城市废墟。", DangerLevel = 4, ConnectedRooms = new List<string> { "relay_building" } });

            Exploration.AddMap(shelter);
            Exploration.AddMap(factory);
            Exploration.AddMap(relay);
        }

        /// <summary>初始化敌人（13个）</summary>
        private void InitializeEnemies()
        {
            Combat.RegisterEnemy(new EnemyData { Id = "wanderer", Name = "流浪者", Hp = 30, MaxHp = 30, Attack = 5, Defense = 2, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.Parts, Amount = 2 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "shadow_creature", Name = "暗影生物", Hp = 50, MaxHp = 50, Attack = 8, Defense = 3, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.MemoryShards, Amount = 1 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "corrupted_memory", Name = "腐化记忆", Hp = 70, MaxHp = 70, Attack = 10, Defense = 5, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.MemoryShards, Amount = 3 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "mutant_plant", Name = "变异藤蔓", Hp = 25, MaxHp = 25, Attack = 4, Defense = 6, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.Food, Amount = 3 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "mechanical_guard", Name = "机械守卫", Hp = 60, MaxHp = 60, Attack = 12, Defense = 8, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.Parts, Amount = 5 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "forgotten_one", Name = "被遗忘者", Hp = 100, MaxHp = 100, Attack = 15, Defense = 10, Speed = 2, MemoryShardDropChance = 0.5f, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.MemoryShards, Amount = 5 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "scavenger_gang", Name = "拾荒帮", Hp = 40, MaxHp = 40, Attack = 7, Defense = 3, Speed = 4, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.Parts, Amount = 3 }, new ResourceAmount { Type = ResourceType.Food, Amount = 2 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "memory_ghost", Name = "记忆幽灵", Hp = 35, MaxHp = 35, Attack = 9, Defense = 1, Speed = 6, MemoryShardDropChance = 0.3f, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.MemoryShards, Amount = 2 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "toxic_spore", Name = "毒孢子", Hp = 20, MaxHp = 20, Attack = 6, Defense = 2, Speed = 3, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.Medicine, Amount = 1 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "broken_android", Name = "损坏仿生人", Hp = 80, MaxHp = 80, Attack = 14, Defense = 12, Speed = 2, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.Parts, Amount = 8 }, new ResourceAmount { Type = ResourceType.Power, Amount = 3 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "shadow_swarm", Name = "暗影群", Hp = 45, MaxHp = 45, Attack = 11, Defense = 4, Speed = 5, MemoryShardDropChance = 0.2f, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.MemoryShards, Amount = 1 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "frost_crawler", Name = "霜爬虫", Hp = 55, MaxHp = 55, Attack = 8, Defense = 7, Speed = 3, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.Food, Amount = 4 }, new ResourceAmount { Type = ResourceType.Water, Amount = 2 } } });
            Combat.RegisterEnemy(new EnemyData { Id = "archive_warden", Name = "档案守卫", Hp = 120, MaxHp = 120, Attack = 18, Defense = 15, Speed = 1, MemoryShardDropChance = 0.8f, Loot = new List<ResourceAmount> { new ResourceAmount { Type = ResourceType.MemoryShards, Amount = 8 }, new ResourceAmount { Type = ResourceType.Parts, Amount = 10 } } });
        }

        /// <summary>初始化任务（原6个+3个新任务）</summary>
        private void InitializeQuests()
        {
            // === 原有4主线+2支线 ===
            Quests.AddQuest(new QuestData
            {
                QuestId = "main_1", Title = "唤醒档案馆", Description = "修复档案馆的基础设施，开始收集记忆碎片。",
                Type = QuestType.MainQuest, Status = QuestStatus.Active,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.CollectResource, TargetId = "MemoryShards", RequiredAmount = 5 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.Parts, ResourceAmount = 10 }, new QuestReward { ResourceType = ResourceType.Power, ResourceAmount = 5 } }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "main_2", Title = "建造温室", Description = "建造温室以确保食物供应。",
                Type = QuestType.MainQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.BuildFacility, TargetId = "greenhouse", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.Food, ResourceAmount = 15 } },
                RelatedNPCs = new List<string> { "lin_doctor" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "main_3", Title = "探索旧档案城", Description = "前往旧档案城寻找更多线索。",
                Type = QuestType.MainQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "archive_ruins", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.MemoryShards, ResourceAmount = 5 } },
                RelatedLocation = "archive_ruins"
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "main_4", Title = "修复广播塔", Description = "修复广播塔以联系外界幸存者。",
                Type = QuestType.MainQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "broadcast_tower", RequiredAmount = 1 },
                    new QuestObjective { Type = ObjectiveType.CollectResource, TargetId = "Parts", RequiredAmount = 20 }
                },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.MemoryShards, ResourceAmount = 10 }, new QuestReward { ResourceType = ResourceType.Power, ResourceAmount = 10 } },
                RelatedLocation = "broadcast_tower"
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "side_1", Title = "林医生的请求", Description = "林医生需要药品来治疗伤员。",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.CollectResource, TargetId = "Medicine", RequiredAmount = 5 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.Parts, ResourceAmount = 5 }, new QuestReward { RelationshipTarget = "lin_doctor", RelationshipAmount = 15 } },
                RelatedNPCs = new List<string> { "lin_doctor" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "side_2", Title = "安娜的试炼", Description = "安娜想证明自己的价值。",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "abandoned_hospital", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "anna", RelationshipAmount = 20 }, new QuestReward { ResourceType = ResourceType.Medicine, ResourceAmount = 3 } },
                RelatedNPCs = new List<string> { "anna" }
            });

            // === 新增3个任务 ===
            Quests.AddQuest(new QuestData
            {
                QuestId = "side_3", Title = "梅姐的食谱", Description = "梅姐想要在废墟公园找一些特殊的草药来改善伙食。",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "ruined_park", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.Food, ResourceAmount = 10 }, new QuestReward { RelationshipTarget = "mei_cook", RelationshipAmount = 15 } },
                RelatedNPCs = new List<string> { "mei_cook" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "side_4", Title = "铁柱的守卫", Description = "铁柱想要更多装备来保护基地。",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.CollectResource, TargetId = "Parts", RequiredAmount = 10 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "iron_guard", RelationshipAmount = 20 } },
                RelatedNPCs = new List<string> { "iron_guard" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "side_5", Title = "王学者的秘密", Description = "王学者似乎知道关于档案城的重要秘密，需要多次对话才能探出真相。",
                Type = QuestType.NPCQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.TalkToNPC, TargetId = "scholar_wang", RequiredAmount = 3 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.MemoryShards, ResourceAmount = 8 }, new QuestReward { RelationshipTarget = "scholar_wang", RelationshipAmount = 25 } },
                RelatedNPCs = new List<string> { "scholar_wang" }
            });
            // === 角色专属任务 ===
            Quests.AddQuest(new QuestData
            {
                QuestId = "npc_lily", Title = "莉莉的誓言", Description = "莉莉想成为像林医生一样的真正医生。",
                Type = QuestType.NPCQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.CollectResource, TargetId = "Medicine", RequiredAmount = 8 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "lily_nurse", RelationshipAmount = 25 }, new QuestReward { ResourceType = ResourceType.Medicine, ResourceAmount = 5 } },
                RelatedNPCs = new List<string> { "lily_nurse" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "npc_hu", Title = "大虎的承诺", Description = "大虎承诺为基地建一道防御工事。",
                Type = QuestType.NPCQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.CollectResource, TargetId = "Parts", RequiredAmount = 15 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "big_hu", RelationshipAmount = 20 }, new QuestReward { ResourceType = ResourceType.Parts, ResourceAmount = 5 } },
                RelatedNPCs = new List<string> { "big_hu" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "npc_qin", Title = "小秦的执念", Description = "小秦执意要修好地下避难所的发电机。",
                Type = QuestType.NPCQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "underground_shelter", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "qin_mechanic", RelationshipAmount = 30 }, new QuestReward { ResourceType = ResourceType.Power, ResourceAmount = 10 } },
                RelatedNPCs = new List<string> { "qin_mechanic" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "npc_grandma", Title = "刘奶奶的药方", Description = "刘奶奶需要废墟公园的草药来配制特效药。",
                Type = QuestType.NPCQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "ruined_park", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "grandma_liu", RelationshipAmount = 20 }, new QuestReward { ResourceType = ResourceType.Medicine, ResourceAmount = 8 } },
                RelatedNPCs = new List<string> { "grandma_liu" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "npc_fox", Title = "狐狸的交易", Description = "狐狸声称能搞到稀缺物资，但需要零件做交换。",
                Type = QuestType.NPCQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.CollectResource, TargetId = "Parts", RequiredAmount = 12 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "fox_trader", RelationshipAmount = 30 }, new QuestReward { ResourceType = ResourceType.Food, ResourceAmount = 15 } },
                RelatedNPCs = new List<string> { "fox_trader" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "npc_dawn", Title = "黎明的情报", Description = "黎明带来了北方的消息，但需要验证。",
                Type = QuestType.NPCQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.TalkToNPC, TargetId = "dawn_scout", RequiredAmount = 3 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "dawn_scout", RelationshipAmount = 25 }, new QuestReward { ResourceType = ResourceType.MemoryShards, ResourceAmount = 5 } },
                RelatedNPCs = new List<string> { "dawn_scout" }
            });
            // === 探索任务 ===
            Quests.AddQuest(new QuestData
            {
                QuestId = "explore_1", Title = "地铁深处", Description = "探索地铁废墟，寻找通往地下网络的入口。",
                Type = QuestType.ExplorationQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "subway_ruins", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.Parts, ResourceAmount = 8 }, new QuestReward { ResourceType = ResourceType.Power, ResourceAmount = 5 } }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "explore_2", Title = "工厂遗迹", Description = "旧工厂里可能还有能用的机械设备。",
                Type = QuestType.ExplorationQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "old_factory", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.Parts, ResourceAmount = 15 } }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "explore_3", Title = "信号中继", Description = "修复信号中继站，扩大广播范围。",
                Type = QuestType.ExplorationQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "relay_station", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.MemoryShards, ResourceAmount = 8 }, new QuestReward { ResourceType = ResourceType.Power, ResourceAmount = 8 } }
            });
            // === 派系任务 ===
            Quests.AddQuest(new QuestData
            {
                QuestId = "faction_1", Title = "档案教团的请求", Description = "为档案教团收集记忆碎片，证明你的诚意。",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.CollectResource, TargetId = "MemoryShards", RequiredAmount = 15 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.MemoryShards, ResourceAmount = 5 } }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "faction_2", Title = "生存主义者的考验", Description = "在不探索的情况下存活5天，证明稳健路线可行。",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.SurviveDays, TargetId = "", RequiredAmount = 10 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.Food, ResourceAmount = 20 }, new QuestReward { ResourceType = ResourceType.Water, ResourceAmount = 20 } }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "faction_3", Title = "流浪者的远征", Description = "探索3个不同地点，证明探索的价值。",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "abandoned_hospital", RequiredAmount = 1 }, new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "subway_ruins", RequiredAmount = 1 }, new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "archive_ruins", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.MemoryShards, ResourceAmount = 10 } }
            });
            // === 连锁任务 ===
            Quests.AddQuest(new QuestData
            {
                QuestId = "chain_1", Title = "失踪的回声", Description = "回声不见了，小北非常担心。",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "ruined_park", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { RelationshipTarget = "xiaobei", RelationshipAmount = 30 }, new QuestReward { RelationshipTarget = "echo_child", RelationshipAmount = 20 } },
                RelatedNPCs = new List<string> { "xiaobei", "echo_child" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "chain_2", Title = "沈牧的真相", Description = "沈牧终于愿意透露他所知道的……",
                Type = QuestType.SideQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.TalkToNPC, TargetId = "shen_mu", RequiredAmount = 5 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.MemoryShards, ResourceAmount = 15 }, new QuestReward { RelationshipTarget = "shen_mu", RelationshipAmount = 40 } },
                RelatedNPCs = new List<string> { "shen_mu" }
            });
            Quests.AddQuest(new QuestData
            {
                QuestId = "chain_3", Title = "地下避难所", Description = "老周发现了地下避难所的入口，那里可能有大批物资。",
                Type = QuestType.ExplorationQuest, Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective> { new QuestObjective { Type = ObjectiveType.VisitLocation, TargetId = "underground_shelter", RequiredAmount = 1 } },
                Rewards = new List<QuestReward> { new QuestReward { ResourceType = ResourceType.Food, ResourceAmount = 20 }, new QuestReward { ResourceType = ResourceType.Medicine, ResourceAmount = 10 }, new QuestReward { ResourceType = ResourceType.Parts, ResourceAmount = 15 } },
                RelatedNPCs = new List<string> { "old_zhou" }
            });
        }

        /// <summary>初始化物品模板</summary>
        private void InitializeItems()
        {
            // === 武器 ===
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "rusty_knife", Name = "生锈的刀", Description = "一把锈迹斑斑的小刀，聊胜于无。",
                Type = ItemType.Weapon, Rarity = ItemRarity.Common, Value = 5,
                AttackBonus = 2
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "pipe_wrench", Name = "管钳", Description = "沉重的管钳，挥舞起来很有力量。",
                Type = ItemType.Weapon, Rarity = ItemRarity.Common, Value = 8,
                AttackBonus = 4
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "combat_knife", Name = "战术刀", Description = "保存完好的军用战术刀。",
                Type = ItemType.Weapon, Rarity = ItemRarity.Uncommon, Value = 20,
                AttackBonus = 7
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "memory_blade", Name = "记忆之刃", Description = "用记忆碎片锻造的神秘武器，散发着微弱的光芒。",
                Type = ItemType.Weapon, Rarity = ItemRarity.Rare, Value = 50,
                AttackBonus = 12
            });

            // === 护甲 ===
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "scrap_armor", Name = "废铁护甲", Description = "用废弃金属拼凑的简易护甲。",
                Type = ItemType.Armor, Rarity = ItemRarity.Common, Value = 6,
                DefenseBonus = 3
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "lab_coat", Name = "实验白大褂", Description = "医院里的白大褂，口袋里还有些药品残留。",
                Type = ItemType.Armor, Rarity = ItemRarity.Common, Value = 10,
                DefenseBonus = 2
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "military_vest", Name = "军用防弹衣", Description = "保存完好的防弹衣，能抵挡大部分攻击。",
                Type = ItemType.Armor, Rarity = ItemRarity.Uncommon, Value = 30,
                DefenseBonus = 8
            });

            // === 消耗品 ===
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "herb_paste", Name = "草药膏", Description = "用公园里采的草药制成的药膏，能恢复少量生命。",
                Type = ItemType.Consumable, Rarity = ItemRarity.Common, Value = 5,
                HealAmount = 15
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "canned_food", Name = "罐头食品", Description = "灾难前的罐头，还能吃。",
                Type = ItemType.Consumable, Rarity = ItemRarity.Common, Value = 5,
                ResourceType = ResourceType.Food, ResourceAmount = 5
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "water_bottle", Name = "瓶装水", Description = "密封的瓶装水，还很干净。",
                Type = ItemType.Consumable, Rarity = ItemRarity.Common, Value = 5,
                ResourceType = ResourceType.Water, ResourceAmount = 5
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "memory_potion", Name = "记忆药剂", Description = "一种发光的液体，据说能唤起深处的记忆。",
                Type = ItemType.Consumable, Rarity = ItemRarity.Rare, Value = 25,
                ResourceType = ResourceType.MemoryShards, ResourceAmount = 3
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "morale_candy", Name = "希望糖果", Description = "小北在废墟中找到的糖果，甜蜜的滋味能提升士气。",
                Type = ItemType.Consumable, Rarity = ItemRarity.Uncommon, Value = 10,
                MoraleBoost = 20
            });

            // === 材料 ===
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "scrap_metal", Name = "废金属", Description = "各种金属碎片，可以用于建造。",
                Type = ItemType.Material, Rarity = ItemRarity.Common, Value = 3,
                ResourceType = ResourceType.Parts, ResourceAmount = 2
            });

            // === 关键物品 ===
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "hospital_key", Name = "医院钥匙", Description = "一把生锈的钥匙，可能能打开医院某扇锁着的门。",
                Type = ItemType.KeyItem, Rarity = ItemRarity.Uncommon, Value = 0
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "archive_key", Name = "档案库钥匙", Description = "一把精巧的钥匙，上面刻着档案城的标记。",
                Type = ItemType.KeyItem, Rarity = ItemRarity.Rare, Value = 0
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "tower_key", Name = "广播塔钥匙", Description = "一把带有电子芯片的钥匙卡。",
                Type = ItemType.KeyItem, Rarity = ItemRarity.Rare, Value = 0
            });

            // === 遗物 ===
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "old_photo", Name = "旧照片", Description = "一张褪色的照片，上面是几个人站在灯塔前的合影。",
                Type = ItemType.Relic, Rarity = ItemRarity.Epic, Value = 100,
                ResourceType = ResourceType.MemoryShards, ResourceAmount = 5
            });
            Items.RegisterTemplate(new ItemInstance
            {
                Id = "music_box", Name = "音乐盒", Description = "一个精致的音乐盒，打开后播放着悠扬的旋律。让所有人都想起了过去。",
                Type = ItemType.Relic, Rarity = ItemRarity.Legendary, Value = 200,
                ResourceType = ResourceType.MemoryShards, ResourceAmount = 10
            });

            // 给玩家一些初始物品
            Items.AddItemFromTemplate("rusty_knife", "初始装备");
            Items.AddItemFromTemplate("herb_paste", "初始物资");
            Items.AddItemFromTemplate("canned_food", "初始物资");
        }


        /// <summary>初始化派系</summary>
        private void InitializeFactions()
        {
            Factions.AddFaction(new FactionInstance
            {
                Type = FactionType.ArchiveOrder,
                Name = "档案教团",
                Description = "坚信保存记忆是人类存续的关键，不惜一切代价收集和保存记忆碎片。",
                Leader = "王学者",
                Reputation = 0,
                Unlocked = false,
                MemberNPCIds = new List<string> { "scholar_wang" }
            });

            Factions.AddFaction(new FactionInstance
            {
                Type = FactionType.Survivalists,
                Name = "生存主义者",
                Description = "认为活着才是最重要的，反对冒险探索。主张囤积资源，避免不必要的冲突。",
                Leader = "铁柱",
                Reputation = 10,
                Unlocked = true,
                MemberNPCIds = new List<string> { "iron_guard", "mei_cook" }
            });

            Factions.AddFaction(new FactionInstance
            {
                Type = FactionType.Wanderers,
                Name = "流浪者联盟",
                Description = "相信外面还有更广阔的世界，致力于探索和发现。愿意冒险换取知识。",
                Leader = "安娜",
                Reputation = 0,
                Unlocked = false,
                MemberNPCIds = new List<string> { "anna" }
            });
        }

        /// <summary>初始化成就</summary>
        private void InitializeAchievements()
        {
            var achievements = new List<Achievement>
            {
                // 生存
                new Achievement { Id = "survive_3days", Name = "初来乍到", Description = "存活3天", Category = "生存" },
                new Achievement { Id = "survive_7days", Name = "站稳脚跟", Description = "存活7天", Category = "生存" },
                new Achievement { Id = "survive_14days", Name = "老居民", Description = "存活14天", Category = "生存" },
                new Achievement { Id = "survive_30days", Name = "档案城之盾", Description = "存活30天", Category = "生存" },
                // 资源
                new Achievement { Id = "food_hoarder", Name = "粮仓满溢", Description = "食物达到50", Category = "资源" },
                new Achievement { Id = "memory_collector", Name = "记忆收集者", Description = "收集10个记忆碎片", Category = "资源" },
                new Achievement { Id = "archive_master", Name = "档案大师", Description = "收集30个记忆碎片", Category = "资源" },
                // 社交
                new Achievement { Id = "community_builder", Name = "社区建设者", Description = "5名NPC同时存活", Category = "社交" },
                new Achievement { Id = "trusted_leader", Name = "值得信赖的领袖", Description = "与某位NPC关系达到80", Category = "社交" },
                // 建筑
                new Achievement { Id = "builder", Name = "建设者", Description = "建造所有建筑", Category = "建筑" },
                new Achievement { Id = "master_architect", Name = "建筑大师", Description = "将任一建筑升至最高等级", Category = "建筑" },
                // 任务
                new Achievement { Id = "first_quest", Name = "第一步", Description = "完成第一个任务", Category = "任务" },
                new Achievement { Id = "quest_hunter", Name = "任务猎人", Description = "完成5个任务", Category = "任务" },
                // 派系
                new Achievement { Id = "faction_allied", Name = "派系盟友", Description = "与任一派系达到同盟", Category = "派系" },
                // 探索
                new Achievement { Id = "explorer", Name = "探索者", Description = "完成3次探索", Category = "探索" },
                new Achievement { Id = "deep_explorer", Name = "深渊探索者", Description = "探索旧档案城", Category = "探索" },
                // 战斗
                new Achievement { Id = "warrior", Name = "战士", Description = "赢得3场战斗", Category = "战斗" },
                new Achievement { Id = "vanquisher", Name = "征服者", Description = "击败被遗忘者", Category = "战斗" },
                new Achievement { Id = "pacifist", Name = "和平主义者", Description = "存活7天不战斗", Category = "战斗" },
                // 物品
                new Achievement { Id = "first_loot", Name = "第一件战利品", Description = "获得第一件物品", Category = "物品" },
                new Achievement { Id = "rare_find", Name = "稀有发现", Description = "获得稀有品质物品", Category = "物品" },
                new Achievement { Id = "legendary", Name = "传说持有者", Description = "获得传说品质物品", Category = "物品" },
                // NPC
                new Achievement { Id = "healer", Name = "治愈之手", Description = "治疗NPC 5次", Category = "社交" },
                new Achievement { Id = "everyone_alive", Name = "全员幸存", Description = "所有NPC存活", Category = "社交" },
                // 探索
                new Achievement { Id = "all_maps", Name = "足迹遍及", Description = "探索所有地图", Category = "探索" },
                new Achievement { Id = "lockpicker", Name = "开锁专家", Description = "进入锁着的房间", Category = "探索" }
            };

            foreach (var ach in achievements)
            {
                Achievements.Register(ach);
            }
        }

        /// <summary>初始化验证器</summary>
        private void InitializeValidator()
        {
            Validator.RegisterQuestIds(new[] { "main_1", "main_2", "main_3", "main_4", "side_1", "side_2", "side_3", "side_4", "side_5", "npc_lily", "npc_hu", "npc_qin", "npc_grandma", "npc_fox", "npc_dawn", "explore_1", "explore_2", "explore_3", "faction_1", "faction_2", "faction_3", "chain_1", "chain_2", "chain_3" });
            Validator.RegisterNPCIds(new[] { "lin_doctor", "old_zhou", "anna", "xiaobei", "shen_mu", "mei_cook", "iron_guard", "scholar_wang", "lily_nurse", "big_hu", "qin_mechanic", "grandma_liu", "fox_trader", "dawn_scout", "echo_child" });
            Validator.RegisterLocationIds(new[] { "abandoned_hospital", "subway_ruins", "archive_ruins", "ruined_park", "broadcast_tower", "underground_shelter", "old_factory", "relay_station" });
        }

        /// <summary>与NPC对话</summary>
        public string TalkToNPC(string npcId)
        {
            var npc = NPCs.GetNPC(npcId);
            if (npc == null) return "找不到这个NPC。";

            string mood = npc.Morale > 70 ? "good" : npc.Morale > 30 ? "normal" : "bad";

            var context = new DialogueContext
            {
                NPCId = npcId,
                NPCName = npc.Name,
                NPCRole = npc.Role.ToString(),
                RelationshipValue = npc.GetRelationship("player"),
                NPCMood = mood,
                RecentMemory = npc.Memory.Summary,
                CurrentDay = Time.CurrentDay
            };

            string dialogue = DialogueGen.Generate(context);

            // 更新对话类任务目标
            foreach (var quest in Quests.GetActiveQuests())
            {
                Quests.UpdateObjectiveProgress(quest.QuestId, ObjectiveType.TalkToNPC, npcId, 1);
            }

            // 派系声望微调
            var faction = Factions.GetNPCFaction(npcId);
            if (faction.HasValue)
            {
                Factions.ChangeReputation(faction.Value, 1);
            }

            return dialogue;
        }

        /// <summary>保存游戏</summary>
        public bool SaveGame()
        {
            var data = Save.CreateNewSaveData(Time.CurrentDay, Time.CurrentPhase, Resources, NPCs, Buildings, Quests, Items, Factions, Achievements);
            return Save.SaveGame(data);
        }

        /// <summary>加载游戏</summary>
        public bool LoadGame()
        {
            var data = Save.LoadGame();
            if (data == null) return false;

            // 恢复时间
            Time.Restore(data.CurrentDay, data.CurrentPhase);

            // 恢复资源
            var resDict = new Dictionary<ResourceType, int>();
            foreach (var r in data.Resources)
            {
                resDict[r.Type] = r.Amount;
            }
            Resources.RestoreFromSnapshot(resDict);

            // 恢复NPC
            foreach (var nsd in data.NPCStates)
            {
                var npc = NPCs.GetNPC(nsd.Id);
                if (npc != null)
                {
                    npc.Health = nsd.Health;
                    npc.Morale = nsd.Morale;
                    npc.Hunger = nsd.Hunger;
                    npc.Fatigue = nsd.Fatigue;
                    npc.Loyalty = nsd.Loyalty;
                    npc.Status = nsd.Status;
                    npc.CurrentWork = nsd.CurrentWork;
                    npc.Relationships = nsd.Relationships;
                    npc.Memory.Entries = nsd.MemoryEntries;
                    npc.Memory.Summary = nsd.MemorySummary;
                    npc.Memory.DiaryHistory = nsd.DiaryHistory ?? new List<string>();
                }
            }

            // 恢复建筑
            foreach (var bsd in data.BuildingStates)
            {
                var building = Buildings.GetBuilding(bsd.Id);
                if (building != null)
                {
                    building.Level = bsd.Level;
                    building.Built = bsd.Built;
                }
            }

            // 恢复任务
            foreach (var qsd in data.QuestStates)
            {
                var quest = Quests.GetQuest(qsd.QuestId);
                if (quest != null)
                {
                    quest.Status = qsd.Status;
                    for (int i = 0; i < qsd.ObjectiveProgress.Count && i < quest.Objectives.Count; i++)
                    {
                        quest.Objectives[i].CurrentProgress = qsd.ObjectiveProgress[i];
                    }
                }
            }

            // 恢复物品
            if (data.Items != null)
            {
                foreach (var isd in data.Items)
                {
                    Items.AddItem(new ItemInstance
                    {
                        Id = isd.Id, Name = isd.Name, Description = isd.Description,
                        Type = isd.Type, Rarity = isd.Rarity, Value = isd.Value,
                        AttackBonus = isd.AttackBonus, DefenseBonus = isd.DefenseBonus,
                        HealthBonus = isd.HealthBonus, HealAmount = isd.HealAmount,
                        MoraleBoost = isd.MoraleBoost, ResourceType = isd.ResourceType,
                        ResourceAmount = isd.ResourceAmount, Source = isd.Source
                    });
                }
            }

            // 恢复派系
            if (data.Factions != null)
            {
                foreach (var fsd in data.Factions)
                {
                    var faction = Factions.GetFaction(fsd.Type);
                    if (faction != null)
                    {
                        faction.Reputation = fsd.Reputation;
                        faction.Unlocked = fsd.Unlocked;
                    }
                }
            }

            // 恢复成就
            if (data.UnlockedAchievements != null)
            {
                foreach (var achId in data.UnlockedAchievements)
                {
                    Achievements.Unlock(achId);
                }
            }
            return true;
        }

        // ===== 事件处理 =====

        private void OnResourceCrisis(OnResourceCrisis evt)
        {
            string typeName = GameConfig.ResourceNames[(int)evt.Type];
            Console.WriteLine($"[危机] {typeName}不足！缺少 {evt.Shortage}");

            foreach (var npc in NPCs.GetAllNPCs())
            {
                if (!npc.IsAlive) continue;
                NPCs.AddMemory(npc.Id, new MemoryEntry
                {
                    MemoryId = Guid.NewGuid().ToString(),
                    Day = Time.CurrentDay,
                    Actor = "system",
                    Target = "town",
                    EventType = MemoryEventType.ResourceCrisis,
                    Description = $"{typeName}不足，缺少{evt.Shortage}",
                    EmotionalWeight = -5
                });
            }
        }

        private void OnGameOverEvent(OnGameOver evt)
        {
            IsGameOver = true;
            IsVictory = false;
            Console.WriteLine($"[游戏结束] {evt.Reason}");
        }

        private void OnCombatEndedEvent(OnCombatEnded evt)
        {
            if (evt.Victory)
            {
                TotalCombatsWon++;
                if (TotalCombatsWon >= 3) Achievements.Unlock("warrior");
            }
        }

        private void OnExplorationEndedEvent(OnExplorationEnded evt)
        {
            if (evt.Success)
            {
                TotalExplorationsCompleted++;
                if (TotalExplorationsCompleted >= 3) Achievements.Unlock("explorer");
            }
        }
        private void OnAchievementUnlockedEvent(OnAchievementUnlocked evt)
        {
            Console.WriteLine($"★ 成就解锁: {evt.AchievementName}！");
        }

        private void OnQuestStatusChangedEvent(OnQuestStatusChanged evt)
        {
            if (evt.NewStatus != QuestStatus.Completed) return;

            // 主线任务链：完成 main_N 后自动激活 main_N+1
            if (evt.QuestId == "main_1") { Quests.StartQuest("main_2"); StartSideQuestsAfterMain1(); }
            else if (evt.QuestId == "main_2") { Quests.StartQuest("main_3"); }
            else if (evt.QuestId == "main_3") { Quests.StartQuest("main_4"); Factions.UnlockFaction(FactionType.ArchiveOrder); }
            else if (evt.QuestId == "main_4") { Factions.UnlockFaction(FactionType.Wanderers); }

            // 派系任务触发
            if (evt.QuestId == "side_3") { Factions.UnlockFaction(FactionType.ArchiveOrder); }
            if (evt.QuestId == "side_4") { Factions.ChangeReputation(FactionType.Survivalists, 10); }

            // 成就
            Achievements.Unlock("first_quest");
            var completedCount = 0;
            foreach (var q in Quests.GetAllQuests()) { if (q.Status == QuestStatus.Completed) completedCount++; }
            if (completedCount >= 5) Achievements.Unlock("quest_hunter");
        }

        private void StartSideQuestsAfterMain1()
        {
            Quests.StartQuest("side_1");
            Quests.StartQuest("side_2");
            Quests.StartQuest("side_3");
            Quests.StartQuest("side_4");
            Quests.StartQuest("side_5");
        }

        private void OnBuildingBuiltEvent(OnBuildingBuilt evt)
        {
            if (evt.BuildingId == "greenhouse")
            {
                Quests.UpdateBuildingObjectives("greenhouse", false, 1);
            }
        }

        public void CheckGameOver()
        {
            if (NPCs.GetAliveCount() == 0)
            {
                EventBus.Publish(new OnGameOver { Reason = "所有居民都已离世，档案城陷入了死寂。" });
                return;
            }

            if (Resources.GetResourceAmount(ResourceType.Food) == 0 && Resources.GetResourceAmount(ResourceType.Water) == 0)
            {
                _starvationDays++;
                if (_starvationDays >= 3)
                {
                    EventBus.Publish(new OnGameOver { Reason = "连续3天断粮断水，档案城的居民们再也撑不下去了……" });
                    return;
                }
            }
            else
            {
                _starvationDays = 0;
            }

            var mainQuest4 = Quests.GetQuest("main_4");
            if (mainQuest4 != null && mainQuest4.Status == QuestStatus.Completed && Time.CurrentDay >= 15)
            {
                IsVictory = true;
                IsGameOver = true;
                EventBus.Publish(new OnGameOver { Reason = "广播塔修复成功！你联系到了外界幸存者，新的篇章即将开启……" });
            }
        }

        public void OnDayEnd()
        {
            TotalDaysSurvived = Time.CurrentDay;
            Achievements.UpdateDay(Time.CurrentDay);
            Achievements.CheckAchievements(this);

            // 心理系统每日更新
            var rng = new Random();
            foreach (var npc in NPCs.GetAllNPCs())
            {
                if (npc.IsAlive) Psychology.DailyUpdate(npc);
            }
            Psychology.ApplyDespairSpread(NPCs, rng);

            // 危机系统
            string crisisMsg = Crises.CheckAndTrigger(this);
            if (!string.IsNullOrEmpty(crisisMsg))
            {
                Console.WriteLine($"  {crisisMsg}");
            }
            Crises.ApplyEffects(this);
            string resolveMsg = Crises.TryResolve(this);
            if (!string.IsNullOrEmpty(resolveMsg))
            {
                Console.WriteLine($"  {resolveMsg}");
            }

            CheckGameOver();

            if (!IsGameOver)
            {
                string evt = DailyEvents.TriggerRandomEvent(this);
                if (!string.IsNullOrEmpty(evt))
                {
                    LastDailyEvent = evt;
                    Console.WriteLine($"  {evt}");
                }
            }
        }
    }
}
