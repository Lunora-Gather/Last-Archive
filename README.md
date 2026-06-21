# Last Archive / 最后档案城

> Build a town. Preserve their memories. Decide what humanity deserves to remember.

2D 单机末世小镇经营 + 生存探索 + NPC 记忆系统 + AI 叙事 RPG。

## 一句话卖点

建造最后的档案城，保存人类最后的记忆。每个 NPC 都有过去、目标、关系和记忆，玩家的选择会改变小镇命运。

## 当前状态

**MVP 阶段 0-2 完成**：核心系统骨架、资源/时间系统、NPC 系统已实现，控制台文字版 Demo 可运行完整游戏闭环。

## 快速开始

### 方式 1：使用 .NET SDK（推荐）

```bash
# 安装 .NET 8 SDK 后
cd "C:\Users\24377\Desktop\Last Archive"
dotnet run
```

### 方式 2：在 Unity 中打开

1. 用 Unity Hub 创建一个 2D 项目，或直接打开本目录
2. 等待 Unity 编译所有 `Assets/Scripts/` 下的脚本
3. 将 `ConsoleGame.cs` 挂载到一个 GameObject 上即可在 Unity 控制台运行
4. 或将 `Program.cs` 作为 Unity 编辑器外部入口

### 方式 3：使用 Visual Studio

1. 用 Visual Studio 打开 `LastArchive.csproj`
2. 按 F5 运行

## 项目结构

```
Last Archive/
├── Program.cs                    # 控制台入口
├── LastArchive.csproj            # .NET 项目文件
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                 # GameManager, EventBus, ServiceLocator, GameConfig, GameEnums
│   │   ├── Time/                 # TimeSystem 时间/阶段管理
│   │   ├── Resources/            # ResourceSystem, ResourceAmount 资源管理
│   │   ├── NPC/                  # NPCInstance, NPCMemory, NPCSystem NPC管理
│   │   ├── Buildings/            # BuildingInstance, BuildingSystem 建筑管理
│   │   ├── Exploration/          # ExplorationData, ExplorationSystem 探索系统
│   │   ├── Combat/               # CombatData, CombatSystem 战斗系统
│   │   ├── Quests/               # QuestData, QuestSystem 任务系统
│   │   ├── AI/                   # AISystem (IAIProvider/MockAIProvider/生成器/校验器)
│   │   ├── Save/                 # SaveSystem 存档系统
│   │   └── UI/                   # ConsoleGame 控制台 Demo
│   ├── Data/                     # NPC/建筑/物品/敌人/地图/任务/事件 数据
│   ├── Scenes/                   # Unity 场景（后续）
│   └── Prefabs/                  # Unity 预制体（后续）
├── CHANGELOG.md                  # 变更记录
└── README.md                     # 本文档
```

## 核心系统一览

| 系统 | 文件 | 功能 |
|------|------|------|
| 时间系统 | `Time/TimeSystem.cs` | 管理天数、白天/夜晚/结算切换、触发每日结算 |
| 资源系统 | `Resources/ResourceSystem.cs` | 6 种资源（食物/水/电力/药品/零件/记忆碎片）的增删查 |
| NPC 系统 | `NPC/NPCSystem.cs` | 5 个 NPC 的状态、工作分配、记忆记录 |
| 建筑系统 | `Buildings/BuildingSystem.cs` | 档案馆/工坊/温室 的建造、升级、产出 |
| 探索系统 | `Exploration/ExplorationSystem.cs` | 3 张地图、房间移动、搜索、事件触发 |
| 战斗系统 | `Combat/CombatSystem.cs` | 回合制战斗：攻击/防御/用药/逃跑 |
| 任务系统 | `Quests/QuestSystem.cs` | 主线/支线/探索/角色任务，目标可代码验证 |
| AI 系统 | `AI/AISystem.cs` | IAIProvider、MockAIProvider、PromptBuilder、ContentValidator、各类生成器 |
| 存档系统 | `Save/SaveSystem.cs` | JSON 存档，保存资源/NPC/建筑/任务/记忆 |
| 游戏管理器 | `Core/GameManager.cs` | 顶层协调器，不堆砌逻辑 |

## 玩法循环

```
基地经营（白天）
    ↓
选择探索地点和队伍（夜晚）
    ↓
探索房间、收集资源、触发战斗
    ↓
返回基地
    ↓
每日结算（资源消耗、NPC 状态、记忆总结）
    ↓
NPC 记忆变化、任务推进
    ↓
进入新的一天……
```

## 核心设计原则

1. **先完成 MVP**，不追求大型开放世界
2. **AI 只负责生成内容和叙事包装**，游戏规则由代码控制
3. **所有 AI 输出必须通过 ContentValidator 校验**
4. **不允许 AI 凭空创造不存在的地点、物品、NPC 和奖励**
5. **游戏离线可运行**，AI 不可用时使用 MockAIProvider
6. **代码模块清晰**，便于后续扩展
7. 所有资源修改必须经过 `ResourceSystem`
8. 所有任务状态修改必须经过 `QuestSystem`
9. 所有 NPC 状态修改必须经过 `NPCSystem`
10. 所有存档读写必须经过 `SaveSystem`

## MVP 验收标准

- [x] 玩家可从主菜单开始新游戏
- [x] 玩家能查看基地、资源、NPC、建筑、任务
- [x] 玩家能经历昼夜循环（白天→夜晚→结算→下一天）
- [x] 玩家能建造温室、升级建筑
- [x] 玩家能选择 NPC 进行探索
- [x] 玩家能探索 3 张地图（医院/学校/地铁站）
- [x] 探索中能获得资源、触发事件、进入战斗
- [x] 战斗能正常胜利或失败
- [x] 玩家能完成主线任务和支线任务
- [x] NPC 能记录玩家行为（多种记忆事件）
- [x] 每天结束时能生成 NPC 记忆摘要
- [x] AI 系统通过 MockAIProvider 工作（不联网也能玩）
- [x] 游戏能保存和读取
- [ ] 没有明显阻断流程的崩溃 Bug（待完整测试验证）
- [x] 整个 MVP 能提供 30 分钟以上游玩内容

## 后续扩展路线

### 第二阶段：内容扩展
- 20 个 NPC、10 张探索地图、50 个事件、30 个任务
- 10 种建筑、20 种敌人、100 种物品
- 3 条主线结局

### 第三阶段：系统深化
- 派系系统（保守派/探索派/技术派/信仰派）
- NPC 关系网（NPC 互相喜欢或讨厌）
- 心理系统（创伤/希望/恐惧/信任/绝望/忠诚）
- 小镇危机（饥荒/疾病/袭击/叛乱/电力中断/记忆污染）
- 多结局（广播/沉默/上传/摧毁/公开/隐藏）

### 第四阶段：UGC 和 AI 高级功能
- 自定义 NPC 生成器
- 自定义事件包
- 创意工坊支持
- GLM 5.2 真实接入（高质量对话/长期记忆/动态任务/玩家行为评价/个性化剧情事件）

## 技术栈

- **语言**：C# 12 / .NET 8
- **目标平台**：PC / Steam（单机优先）
- **未来引擎**：Unity 2D（MVP 已用纯 C# 实现，可平滑迁移到 Unity）

## 开发文档

- [CHANGELOG.md](./CHANGELOG.md) - 每个阶段的变更记录
- [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) - 系统架构说明
- [docs/AI_INTEGRATION.md](./docs/AI_INTEGRATION.md) - AI 系统接入说明
- [docs/UNITY_MIGRATION.md](./docs/UNITY_MIGRATION.md) - Unity 迁移指南

## License

MIT

---

**「人类应该记住全部真相，还是为了活下去选择遗忘？」**
