# Last Archive - 系统架构说明

## 架构原则

1. **低耦合**：系统间通过 EventBus 通信，不直接依赖
2. **数据驱动**：NPC/建筑/地图/任务均为数据结构，逻辑在系统中
3. **规则与文本分离**：游戏规则由代码控制，文本由 AI 生成
4. **游戏逻辑与 UI 分离**：核心系统不依赖任何 UI
5. **可替换 AI Provider**：通过 IAIProvider 接口，MockAI 和 GLM 可互换

## 系统依赖关系

```
GameManager (顶层协调)
    ├── TimeSystem (时间/阶段)
    │   ├── ResourceSystem (资源)
    │   ├── NPCSystem (NPC)
    │   ├── BuildingSystem (建筑) → ResourceSystem
    │   ├── QuestSystem (任务) → ResourceSystem, NPCSystem
    │   ├── MemorySummarizer (AI)
    │   └── EventGenerator (AI)
    ├── ExplorationSystem (探索) → ResourceSystem, NPCSystem
    ├── CombatSystem (战斗) → ResourceSystem, NPCSystem
    ├── SaveSystem (存档)
    ├── DialogueGenerator (AI)
    └── QuestGenerator (AI) → ContentValidator
```

## 事件流

```
OnDayStarted → 玩家经营
OnNightStarted → 玩家探索
OnDayEnded → 结算完成

OnResourceChanged → UI更新
OnResourceCrisis → NPC记忆记录

OnNPCStatusChanged → UI更新
OnNPCMemoryAdded → 记忆系统

OnBuildingBuilt / OnBuildingUpgraded → 任务更新
OnCombatStarted / OnCombatEnded → 战斗结算
OnQuestStatusChanged → UI更新/奖励发放

OnExplorationStarted / OnExplorationEnded → 资源结算
OnGameOver → 游戏结束
```

## 数据流

### 每日结算流程
```
1. BuildingSystem.ProduceDailyResources()
   → 温室产出 Food 等

2. ResourceSystem.ApplyDailyConsumption()
   → 每NPC消耗 Food + Water
   → 建筑消耗 Power
   → 不足触发 OnResourceCrisis

3. NPCSystem.ApplyDailyChanges()
   → 饥饿/疲劳增加
   → 士气/忠诚变化
   → 受伤恢复

4. EventGenerator.Generate()
   → 生成每日事件文本

5. MemorySummarizer.Summarize()
   → 每个NPC总结当天记忆

6. QuestSystem.UpdateResourceObjectives()
   → 检查资源收集类任务
```

### 探索流程
```
1. 选择地图 + 队伍
2. ExplorationSystem.StartExploration()
3. 循环：
   a. MoveToRoom() → 更新任务目标
   b. SearchRoom() → 获得资源/触发事件
   c. 如遇敌人 → CombatSystem.StartCombat()
   d. 战斗循环 → ExecutePlayerAction()
   e. 战斗结束 → 奖励/惩罚
4. ReturnToBase() → 结算
```

## AI 系统架构

```
IAIProvider (接口)
├── MockAIProvider (本地模板)
└── GLMProvider (预留，后续接入)

PromptBuilder → 构造提示词
AIResponseParser → 解析JSON
ContentValidator → 校验合法性

QuestGenerator → IAIProvider.GenerateQuest() → ContentValidator.ValidateQuest()
DialogueGenerator → IAIProvider.GenerateDialogue()
MemorySummarizer → IAIProvider.SummarizeMemory()
EventGenerator → IAIProvider.GenerateDailyEvent()
```

### AI 输出安全规则
1. 所有 AI 输出必须解析为结构化数据
2. 解析失败 → 使用本地 fallback 模板
3. 校验失败 → 丢弃 AI 结果，不能让游戏崩溃
4. 不允许 AI 直接修改资源/NPC/任务状态
5. 不允许 AI 引用不存在的 NPC/地点/物品
