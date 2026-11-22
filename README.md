# 🎮 Last Archive | 最后档案城

<p align="center">
  <a href="https://huggingface.co/spaces/Jiehu-Claire/last-archive">
    <img src="https://img.shields.io/badge/🚀%20在线试玩-Hugging%20Face-FFD21E?style=for-the-badge&logo=huggingface&logoColor=black" alt="Hugging Face Spaces" />
  </a>
  <a href="https://github.com/Lunora-Gather/Last-Archive">
    <img src="https://img.shields.io/badge/📦%20GitHub%20仓库-Last%20Archive-181717?style=for-the-badge&logo=github&logoColor=white" alt="GitHub Repo" />
  </a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Language-C%23-blue.svg?style=flat-square&logo=c-sharp" alt="C#">
  <img src="https://img.shields.io/badge/Framework-.NET%2010.0-blueviolet.svg?style=flat-square&logo=.net" alt=".NET 10.0">
  <img src="https://img.shields.io/badge/Build-Passing-brightgreen.svg?style=flat-square" alt="Build">
  <img src="https://img.shields.io/badge/Docker-Supported-cyan.svg?style=flat-square&logo=docker" alt="Docker">
  <img src="https://img.shields.io/badge/License-MIT-green.svg?style=flat-square" alt="MIT">
</p>

> **「人类应该记住全部真相，还是为了活下去选择遗忘？」**
>
> 在末日废土的灰烬之中，你是这片避难所的最后监督者。在灾难后的废墟中建设城镇、分派工作、平息危机，并在残存的人类记忆中进行抉择，引导人类文明走向最终的命运终局。

---

## 🌐 线上即刻体验 / Play Online

我们已经在 **Hugging Face Spaces** 完成了容器化云端部署，无需任何本地安装或配置，点击下方链接即可在浏览器中一键开启废土之旅：

👉 **[最后档案城 (Last Archive) 线上直玩](https://jiehu-claire-last-archive.hf.space/)**

---

## ✨ 核心游戏特色 / Core Features

### 📊 1. 沙盒避难所经营 (Shelter Management)
管理 **食物、水源、电力、零件、药品、记忆碎片** 等 6 种核心资源的动态平衡。冷静应对突发的饥荒、停电、暴疫等毁灭性灾难，保障小镇平稳运转。

### 👥 2. 居民社交与动态心理 (Psychology & Social Networks)
* **15 位性格鲜明的居民**，包含 **14 对错综复杂的人际社交线**（好感、仇恨、信赖）。
* 每个人拥有 **希望、绝望、创伤、焦虑** 等多维心理指标，其心理特征将动态影响工作产出与探索战斗表现。

### 🗺️ 3. 废土迷宫探索 (Wasteland Exploration)
组建 3 人探索小队前往 **医院、旧地铁站、市立档案馆** 等 8 张危险地图。通过房间搜索判定、遭遇踩雷和钥匙解密，搜刮生存物资。

### ⚔️ 4. 回合制战术战斗 (Speed-Based Turn Combat)
根据敏捷度排序的行动时间轴（Timeline）进行策略对抗。支持普通攻击、角色专属技能（如医生的群体治疗、侦察兵的防线重组）、药品补给或紧急撤退。

### 📜 5. 多分支与 6 种结局终局 (Branching Narrative & 6 Endings)
承接 25+ 个精心设计的主线、支线、派系及个人任务。每次关键抉择都会将城镇推向不同的演化方向，解锁 **广播重启、意识上传、永恒沉默** 等 6 种完全不同的终局结局。

### 🤖 6. 兼容 OpenAI 的 AI 叙事引擎 (AI Narrative Engine)
终端版可以接入 OpenAI 兼容服务，为居民生成对话和日记。网页试玩使用内置 Mock 叙事，可离线运行。

---

## 网页控制台与玩法

- 总览集中展示资源、下一轮配给、主线进度和行动记录。明暗主题适配桌面与手机。
- 出发前比较八处地点的危险度和掉落。查看队员生命、战斗能力与疲劳，返城后的疲劳需要通过休息恢复。
- 建筑面板展示建造材料、日产和维护成本。集市建成后可按等级获得每日交换额度，及时补上食物或饮水。
- 背包支持合成、分配武器和护甲，以及在基地使用补给。战斗显示实际行动顺序和可选敌方目标。
- 修好广播塔并返城后，可决定重启广播、封存记忆或消耗碎片上传意识。档案城也会在第 15 天自动评定结局。
- 每个浏览器会话独立存档。推进到下一天自动保存，也可以在返城后手动保存。自动游玩以每两秒一天的速度推进结算。

---

## 🚀 本地快速开始 / Getting Started

本项目基于多目标编译（Multi-targeting），支持在 Windows、macOS 和 Linux 上流畅运行。

### 准备工作
请确保本地已安装 [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。

### 运行方式

#### 📦 方式 1：Web 网页版（推荐）
```bash
dotnet run --framework net10.0 -- --web
```
启动后在浏览器中访问：👉 **[http://localhost:8080](http://localhost:8080)** 即可游玩。

#### 💻 方式 2：桌面客户端 (Windows Forms)
仅在 Windows 系统下支持：
```bash
dotnet run --framework net10.0-windows -- --gui
```

#### 📟 方式 3：经典终端文字版
在纯字符模式下进行极客交互：
```bash
dotnet run --framework net10.0
```

#### 🧪 方式 4：运行自动化测试
项目内置核心系统自动化测试，覆盖资源、探索、战斗、存档等所有核心逻辑，运行以下命令即可开始一键校验：
```bash
dotnet run --framework net10.0 -- --test
```

#### 🧪 网页端到端测试

测试自动启动独立实例，使用 WebKit 点击页面并核对服务端存档。需要已安装 Node.js 和 .NET SDK。

```bash
dotnet build -c Release
test_deps=$(mktemp -d)
npm install --prefix "$test_deps" --no-save playwright@1.65.1
"$test_deps/node_modules/.bin/playwright" install webkit
NODE_PATH="$test_deps/node_modules" node tests/web-flow.cjs
```

---

## 🐳 云端一键部署 / Cloud Deployment

项目已针对 Linux 容器化进行深度优化，且附带完整的 `Dockerfile`。你可以直接 Fork 本仓库并一键部署到 Render、Railway 或 Fly.io 等云平台：

1. 将本仓库 Fork 至您的 GitHub 账号。
2. 登录 [Railway](https://railway.app/)，创建新 Web Service 并绑定 Fork 的仓库。
3. Railway 会自动识别项目根目录的 `Dockerfile` 并进行多阶段构建。
4. 部署完成后，即可获得公网可访问的专属游戏页面！

网页存档默认写入 `web-saves/`，每个浏览器通过同站 Cookie 识别自己的存档。清除 Cookie 或换用浏览器后会开启独立的进度。在嵌入页无法使用 Cookie 时，点「在独立窗口打开游戏」。原来的公共存档和终端版 `save.json` 分开保留。

Docker 默认将网页存档放在 `/data/last-archive`。在 Hugging Face Spaces 上要跨容器重启保留存档，需要把持久卷挂载到 `/data`，或将 `LAST_ARCHIVE_SAVE_DIR` 指向其他持久挂载目录。

---

## 📂 项目结构概览 / Project Structure

```text
Last Archive/
├── Assets/
│   └── Scripts/               # 核心 C# 游戏逻辑代码
│       ├── Core/              # 游戏管理器、事件总线、系统配置
│       ├── Time/              # 天数与阶段管理系统
│       ├── Resources/         # 资源增删、消耗与危机判定
│       ├── NPC/               # 居民属性、心理、关系及记忆管理
│       ├── Buildings/         # 建筑物建造、升级与生产
│       ├── Exploration/       # 8大地图、房间移动与搜刮
│       ├── Combat/            # 时间轴回合制战斗逻辑
│       ├── Quests/            # 4主线+6支线等24 个任务推进
│       ├── AI/                # 大模型接入适配与校验器
│       ├── Save/              # JSON存档与加载
│       └── UI/                # 控制台文字版、GUI、Web 服务器
├── docs/                      # 架构设计与迁移文档
│   ├── ARCHITECTURE.md        # 系统架构设计
│   ├── AI_INTEGRATION.md      # AI 大模型对接指南
│   └── UNITY_MIGRATION.md     # Unity 迁移手册
├── Dockerfile                 # Docker 容器化配置文件
├── Program.cs                 # 程序启动入口
└── README.md                  # 本说明文档
```

---

## 📄 开源协议 / License

本项目基于 **[MIT License](LICENSE)** 开源。
