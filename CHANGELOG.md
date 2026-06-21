# Last Archive - CHANGELOG

## [1.0.0] - 全系统完成 🎉

### Phase 1-6 全部完成

| Phase | 内容 | 状态 |
|-------|------|------|
| 1 | 核心系统（资源/NPC/建筑/时间/探索/战斗/任务/AI/存档） | ✅ |
| 2 | 新系统（派系/物品/成就）+ 闭环接通 | ✅ |
| 3 | 危机系统/多结局/心理系统/关系影响 | ✅ |
| 4 | Unity 迁移准备（数据层已就绪） | ✅ |
| 5 | AI 接入（GLM/DeepSeek/中转站/Ollama/Mock） | ✅ |
| 6 | i18n/新手引导/难度平衡/边界修复 | ✅ |

### 最终数据

- **17 个核心系统**
- **15 个 NPC**（含 14 对 NPC 间关系）
- **8 张地图**（每张 3-5 个房间）
- **25 个任务**（4 主线 + 6 支线 + 6 角色 + 3 探索 + 3 派系 + 3 连锁）
- **13 种敌人**（HP 20-120）
- **25+ 种物品**（武器/护甲/消耗品/材料/钥匙/遗物）
- **10 座建筑**
- **3 个派系** / **25 个成就** / **6 种危机** / **6 种结局**
- **5 种心理状态** × **7 种心理特征**
- **6 种 AI 后端**（GLM/DeepSeek/中转站/Ollama/Mock/环境变量）
- **中/英双语 i18n**
- **新手引导系统**
- **168 测试通过 / 0 失败**

---

## [0.1.0] - MVP 完成 ✅

### 所有阶段完成

- **阶段 0**：项目骨架 ✅
- **阶段 1**：资源和时间系统 ✅
- **阶段 2**：NPC 系统 ✅
- **阶段 3**：建筑系统 ✅
- **阶段 4**：探索系统 ✅
- **阶段 5**：战斗系统 ✅
- **阶段 6**：任务系统 ✅
- **阶段 7**：AI 系统 ✅
- **阶段 8**：存档系统 ✅
- **阶段 9**：整合和打磨 ✅

### 测试结果

**100 测试全部通过，0 失败。**

### 完成的文件

| 文件 | 行数 | 功能 |
|------|------|------|
| `Core/GameEnums.cs` | 203 | 所有枚举类型和游戏常量 |
| `Core/EventBus.cs` | 112 | 全局事件总线 + 15种事件定义 |
| `Core/ServiceLocator.cs` | 55 | 简单服务定位器 |
| `Core/GameConfig.cs` | 63 | 集中配置参数 |
| `Core/GameManager.cs` | 592 | 顶层协调器，初始化所有系统 |
| `Time/TimeSystem.cs` | 154 | 天数/阶段管理，触发每日结算 |
| `Resources/ResourceAmount.cs` | 53 | 资源数据结构 |
| `Resources/ResourceSystem.cs` | 137 | 6种资源的增删查、每日消耗、危机触发 |
| `NPC/NPCInstance.cs` | 100 | NPC完整属性 |
| `NPC/NPCMemory.cs` | 46 | 记忆条目和容器 |
| `NPC/NPCSystem.cs` | 187 | NPC管理、工作分配、受伤/治疗、记忆记录 |
| `Buildings/BuildingInstance.cs` | 38 | 建筑完整属性 |
| `Buildings/BuildingSystem.cs` | 111 | 建造、升级、每日产出 |
| `Exploration/ExplorationData.cs` | 70 | 探索数据结构 |
| `Exploration/ExplorationSystem.cs` | 231 | 地图选择、队伍组建、房间移动、搜索、事件触发 |
| `Combat/CombatData.cs` | 58 | 战斗数据结构 |
| `Combat/CombatSystem.cs` | 324 | 回合制战斗：攻击/防御/用药/逃跑 |
| `Quests/QuestData.cs` | 62 | 任务数据结构 |
| `Quests/QuestSystem.cs` | 249 | 任务状态管理、目标进度、自动完成、奖励发放 |
| `AI/AISystem.cs` | 447 | IAIProvider/MockAIProvider/生成器/校验器 |
| `Save/SaveSystem.cs` | 213 | JSON存档、加载、删除 |
| `UI/ConsoleGame.cs` | 811 | 完整控制台文字版Demo |
| `Tests/AutoTest.cs` | 490 | 100个自动化测试 |
| `Program.cs` | 48 | 程序入口 |

### 核心类说明

| 类 | 职责 |
|---|---|
| `GameManager` | 顶层协调，不堆砌逻辑 |
| `TimeSystem` | 天数/阶段管理，触发每日结算 |
| `ResourceSystem` | 所有资源修改的唯一入口 |
| `NPCSystem` | 所有NPC状态修改的唯一入口 |
| `BuildingSystem` | 建筑建造/升级/产出 |
| `ExplorationSystem` | 地图/房间/搜索/事件 |
| `CombatSystem` | 回合制战斗 |
| `QuestSystem` | 所有任务状态修改的唯一入口 |
| `MockAIProvider` | 离线AI，模板生成 |
| `ContentValidator` | AI输出校验，防止非法数据 |
| `SaveSystem` | 所有存档读写的唯一入口 |
| `ConsoleGame` | 控制台UI，文字按钮版Demo |

### 运行方式

```bash
# 交互式游戏
cd "C:\Users\24377\Desktop\Last Archive"
dotnet run

# 自动化测试
dotnet run -- --test
```

### 游戏闭环验证

完整5天游戏流程自动测试通过：
- 白天经营（对话、分配工作）
- 夜晚探索（3张地图、搜索、战斗）
- 每日结算（资源消耗、NPC状态、记忆总结）
- NPC记忆记录
- 任务系统推进
- 存档保存/加载

### 当前已知问题

1. 控制台UI中文对齐在不同终端可能显示不一致
2. 战斗系统简化为单单位轮流行，后续可扩展为多单位按速度排序
3. MockAI 对话模板有限，后续接入 GLM 可大幅提升质量
4. 资源危机后没有游戏结束判定（仅触发事件）
5. 部分数据类仍使用字段而非属性（不影响功能，仅影响未来序列化扩展）

### 下一阶段计划

1. 创建 Unity 2D 工程并迁移脚本
2. 实现 Unity UI 替换控制台 UI
3. 添加美术资源占位
4. 接入 GLM 5.2 API
5. 扩展内容（更多NPC、地图、事件、任务）
6. 实现派系系统和多结局
