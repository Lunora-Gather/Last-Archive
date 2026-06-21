---
title: Last Archive
emoji: 🎮
colorFrom: blue
colorTo: indigo
sdk: docker
app_port: 7860
pinned: false
---

# 🎮 最后档案城 / Last Archive


<p align="center">
  <img src="https://img.shields.io/badge/Language-C%23-blue.svg?style=flat-square" alt="C#">
  <img src="https://img.shields.io/badge/Framework-.NET%208.0-blueviolet.svg?style=flat-square" alt=".NET 8.0">
  <img src="https://img.shields.io/badge/Build-Passing-brightgreen.svg?style=flat-square" alt="Build">
  <img src="https://img.shields.io/badge/Docker-Supported-cyan.svg?style=flat-square" alt="Docker">
  <img src="https://img.shields.io/badge/License-MIT-green.svg?style=flat-square" alt="MIT">
</p>

> **「人类应该记住全部真相，还是为了活下去选择遗忘？」**

《最后档案城》是一款融合了 **2D 单机末世经营、生存探索、NPC 记忆系统与 AI 动态叙事** 的沙盒 RPG 游戏。作为档案城的管理者，你需要建设城镇、分配工作、指派探索任务，并决定在末世的废墟中保留哪些人类的记忆，决定幸存者的最终宿命。

---

## ✨ 核心特色

* 📊 **深度沙盒经营**：管理食物、水源、电力、零件、药品及记忆碎片等 6 种核心资源的平衡，处理突发的饥荒、停电、疾病等致命危机。
* 👥 **NPC 心理与社会关系**：15 个性格各异的居民，包含 14 对复杂的人际关系。居民拥有独特的心理状态（希望、焦虑、创伤、绝望）和行为偏好，心理状态直接影响其战斗表现或探索意愿。
* 🗺 **废土迷宫探索**：派谴探索队伍前往医院、学校、地铁站等 8 张危险地图。多房间探索，解开机关，利用钥匙卡解锁秘密区域。
* ⚔ **回合制策略战斗**：精心设计的回合制多对多战斗系统。依据速度进行行动条排序，支持攻击、防御减伤、吃药与战术逃跑。
* 📜 **分支剧情与多结局**：完成 25+ 个主线、支线、派系及居民任务。根据你在派系声望、资源储备、居民存活及碎片保留上的选择，游戏将走向 **6 种完全不同的终局结局**（如广播重启、意识上传、永恒沉默等）。
* 🤖 **AI 叙事引擎与优雅降级**：支持接入 **GLM-4 / DeepSeek / Ollama** 等任何兼容 OpenAI 的大模型后端。AI 将为你动态生成每日小镇事件、居民日记与精彩的对话。在无网络时自动降级为本地 Mock 模板，保证流畅的单机游玩体验。

---

## 🎨 极客复古 Web 交互端

项目内置了精心设计的网页控制台（Web UI），通过 `dotnet run -- --web` 启动后即可享受：
* 📺 **CRT 扫描线动效**：充满沉浸感的复古显像管科技废土风。
* ☀️ **明暗双主题一键切换**：支持“暗黑科技风”与“明亮白昼风”。
* 🔗 **社交网格矩阵**：实时可视化的二维人际好感度热力图。
* 🛡 ** Timeline 战斗指示器**：直观的出手顺序与血条状态，支持鼠标点击选取敌人并进行战术操作。
* 🤖 **自主演化模式**：支持一键“自动游玩”，以每 2 秒一天的速度自主推进，观察沙盒小镇的演化直至结局。

---

## 🚀 快速开始

本项目为多目标编译（Multi-targeting），完美支持 Windows 及跨平台 Linux 部署。

### 方式 1：本地网页直接玩（推荐）
在终端中进入项目目录并运行：
```bash
dotnet run --framework net8.0 -- --web
```
启动后在浏览器中打开：👉 **[http://localhost:8080](http://localhost:8080)** 即可开始体验！

### 方式 2：桌面客户端（Windows Form）
仅在 Windows 环境下支持原生桌面 UI：
```bash
dotnet run --framework net8.0-windows -- --gui
```

### 方式 3：经典终端文字版
在控制台纯文字交互：
```bash
dotnet run --framework net8.0
```

---

## 🌐 网页在线部署（供他人在线体验）

想要让其他人通过外网直接体验？我们已对项目进行了 Linux 构建优化并附带了 `Dockerfile`，您可以非常简单地将其部署到主流的云托管平台。

### 1. 一键部署到 Railway / Render / Fly.io
1. 将本项目 Fork 到您的 GitHub 账号下。
2. 登录 [Railway](https://railway.app/) 或 [Render](https://render.com/)。
3. 新建 Web Service，选择您 Fork 的 GitHub 仓库。
4. 平台将自动识别项目中的 `Dockerfile` 进行多阶段编译构建，并自动注入 `PORT` 端口。
5. 部署完成后即可获得公网可访问的专属游戏网址！

### 2. 本地局域网/公网共享
在启动 Web 服务时，系统会尝试绑定 `http://*:{port}/` 通配符。
* 如果您以管理员身份运行，或者在 Linux/Docker 容器中运行，服务器将自动开启**局域网及公网共享**，同局域网的伙伴输入您的 IP 地址加端口（如 `http://192.168.1.100:8080`）即可直接联机体验。
* 如果在 Windows 下非管理员权限运行，系统将安全退回 `localhost` 本地模式，确保不会因权限报错。

---

## 🤖 配置大模型叙事

进入网页后，切换至 **「🤖 AI配置」** 选项卡，您可以：
1. **单机模式 (Mock)**：默认无需配置，使用本地预设的模板库，断网可用。
2. **联网大模型 (OpenAI 兼容)**：
   * **API Base URL**：如 GLM (`https://open.bigmodel.cn/api/paas/v4`)、DeepSeek (`https://api.deepseek.com/v1`) 或本地 Ollama (`http://localhost:11434/v1`)。
   * **API Key**：填入您获取的 Key。
   * **Model**：填入对应的模型名称，如 `glm-4-flash`（推荐，速度快且免费）、`deepseek-chat` 或 `qwen2.5`。

---

## 🧪 自动化测试验证

项目内置了 251 个全系统闭环测试用例，覆盖全部核心规则。运行以下命令即可执行自测：
```bash
dotnet run --framework net8.0 -- --test
```

---

## 🛠 技术架构原则

1. **规则归代码，包装归 AI**：AI 仅负责润色文本和背景描述，资源的扣除、战斗结果、任务完成状态等核心逻辑全部由 C# 强类型代码控制，安全可靠。
2. **EventBus 解耦**：各子系统（NPC、资源、建筑、心理等）均通过全局事件总线订阅/发布消息，架构清晰，易于维护与 Unity 移植。
3. **内容安全校验 (ContentValidator)**：所有 AI 生成的 JSON 数据必须通过严格的本地黑白名单校验，防止出现非法物品、不存在的地图坐标或导致游戏崩溃的溢出数值。

---

## 📄 开源协议

本项目基于 **MIT License** 开源。
