---
title: Last Archive
emoji: 🎮
colorFrom: blue
colorTo: indigo
sdk: docker
app_port: 7860
pinned: false
---

# 🎮 Last Archive | 最后档案城

<p align="center">
  <img src="docs/images/cover.png" alt="Last Archive Cover" width="100%" style="border-radius: 8px; margin-bottom: 20px;" />
</p>

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
  <img src="https://img.shields.io/badge/Framework-.NET%208.0-blueviolet.svg?style=flat-square&logo=.net" alt=".NET 8.0">
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

👉 **[最后档案城 (Last Archive) 线上直玩](https://huggingface.co/spaces/Jiehu-Claire/last-archive)**

---

## 📸 游戏视觉速览 / Visual Showcase

<table align="center" width="100%">
  <tr>
    <td width="50%" align="center">
      <img src="docs/images/screenshot1.png" alt="游戏主界面与居民拓扑" width="100%" style="border-radius: 6px;" /><br />
      <b>🌐 极客 Web 终端与动态社交图谱</b>
    </td>
    <td width="50%" align="center">
      <img src="docs/images/screenshot2.png" alt="生存探险与装备合成" width="100%" style="border-radius: 6px;" /><br />
      <b>🛠️ 废土探索与物品合成工作台</b>
    </td>
  </tr>
</table>

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
支持接入 **GLM-4 / DeepSeek / Ollama / 自定义中转站** 等大语言模型，为每天生成随机事件、日记和独特的人际对话。内置精密的 Mock 数据兜底，即使完全离线也能顺畅体验完整的叙事深度。

---

## 🎨 极客复古 Web 控制台亮点 / Web UI Highlights

为了拉满末世科幻氛围，我们精心设计了极具质感的三栏式 Web 管理终端：
* 🌌 **灰烬漂移微粒效果 (Apocalyptic Ash)**：在背景层由 Canvas 实时绘制缓缓坠落、轻微浮动的辐射微粒，打造荒凉寂寥的废土美学。
* 🔊 **纯代码 8-bit 合成音效 (Web Audio SFX)**：无任何外部音频资源，纯 JS 算法合成复古电子音效（Beep 操作音、Laser 日期扫描、Explosion 战斗冲击及和弦警报）。
* 🔗 **SVG 动态人际拓扑图 (SVG Social Map)**：直观展现居民间的关系纽带，绿色连线代表友好，红色连线代表敌对，悬停特定居民可一键聚焦其社交网络。
* 🛠️ **合成生存工作台 (Crafting Station)**：在背包面板中新增工作台。消耗零件和电力，可合成草药膏（回复 HP）、希望糖果（恢复士气）、战术刀与军用防弹衣等关键作战装备。
* 📺 **CRT 滤镜与双主题**：支持一键切换 CRT 扫描线显像管滤镜，内置明亮与暗黑极客双重色调。
* 💾 **日终云端自动存档**：每日清晨自动保存最新状态，规避意外丢失进度。
* 🤖 **沙盒自演化游玩 (Autoplay Mode)**：可开启自动决策，系统以每 2秒一天的速度自主管理、探索和应对危机，供您观察小镇的演化进程。

---

## 🚀 本地快速开始 / Getting Started

本项目基于多目标编译（Multi-targeting），支持在 Windows、macOS 和 Linux 上流畅运行。

### 准备工作
请确保本地已安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

### 运行方式

#### 📦 方式 1：Web 网页版（推荐）
```bash
dotnet run --framework net8.0 -- --web
```
启动后在浏览器中访问：👉 **[http://localhost:8080](http://localhost:8080)** 即可游玩。

#### 💻 方式 2：桌面客户端 (Windows Forms)
仅在 Windows 系统下支持：
```bash
dotnet run --framework net8.0-windows -- --gui
```

#### 📟 方式 3：经典终端文字版
在纯字符模式下进行极客交互：
```bash
dotnet run --framework net8.0
```

#### 🧪 方式 4：运行自动化测试
项目内置 251 个全系统自动化测试用例，覆盖资源、探索、战斗、存档等所有核心逻辑，运行以下命令即可开始一键校验：
```bash
dotnet run --framework net8.0 -- --test
```

---

## 🐳 云端一键部署 / Cloud Deployment

项目已针对 Linux 容器化进行深度优化，且附带完整的 `Dockerfile`。你可以直接 Fork 本仓库并一键部署到 Render、Railway 或 Fly.io 等云平台：

1. 将本仓库 Fork 至您的 GitHub 账号。
2. 登录 [Railway](https://railway.app/)，创建新 Web Service 并绑定 Fork 的仓库。
3. Railway 会自动识别项目根目录 of `Dockerfile` 并进行多阶段构建。
4. 部署完成后，即可获得公网可访问的专属游戏页面！

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
│       ├── Quests/            # 4主线+6支线等25+个任务推进
│       ├── AI/                # 大模型接入适配与校验器
│       ├── Save/              # JSON存档与加载
│       └── UI/                # 控制台文字版、GUI、Web 服务器
├── docs/                      # 架构设计与迁移文档
│   ├── images/                # README 封面图及游戏截图
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
