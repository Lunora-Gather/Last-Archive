# 最后档案城 · Last Archive

一款中文废土经营与回合制探索游戏。你要带领 15 位居民守住营地，找回旧城档案，修复广播塔，决定这段记忆如何留给后来的人。

**[在线试玩](https://jiehu-claire-last-archive.hf.space/)** · [Hugging Face Space](https://huggingface.co/spaces/Jiehu-Claire/last-archive) · [更新记录](CHANGELOG.md)

[![构建与测试](https://github.com/Lunora-Gather/Last-Archive/actions/workflows/ci.yml/badge.svg)](https://github.com/Lunora-Gather/Last-Archive/actions/workflows/ci.yml)

## 第一次玩

1. 总览显示当前目标和当天补给预测。首个目标是收集 4 枚记忆碎片。可以攻进医院、分两趟搜公园和地铁，也可以派工程队员启动中继器。
2. 在「探索/战斗」选地点和最多 3 名队员。每天最多出发两次。资源在存活队员返城后入库，固定物资搜空后不会刷新。
3. 按「推进到下一天」结算产出、配给和营地事件，并自动存档。返城后也可以手动保存。

网页支持明暗主题、键盘操作和窄窗口。手机上默认收起目的地列表，点「更换地点」可看地图和场景图。组队时出发按钮保持可见，交战时可在底部查看当前目标并下达指令。

## 换一种打法

- **开局局势**　「进度管理 → 开启全新游戏」可以选常规、净水故障或药品告急。净水故障会增加每日耗水，可安排工程师留守水泵。药品告急要照顾重伤居民，可让医护留守治疗。同一名居民当天只能在远征与留守中选一项。
- **补给路线**　建温室和水处理站可走本地生产。建集市并预留 4 零件可与商队签约，增加每日交换次数、降低出发消耗。两条路线同时就绪时，由你选择日终启用哪条。
- **回合制战斗**　敌人的蓄力目标、预计伤害和护盾支援会提前显示。守卫能替队友挡击，工程师能打断蓄力，侦察员能为全队加盾，医生能群体治疗。
- **主线结局**　取回旧城档案后，广播塔次日开放。可以强攻守备队，也可以让工程与搜寻队员搭档接线绕行。带回校准数据后先安装组件，再通过日终试送电，即可选择重启广播、封存记忆或上传意识。

## 存档和换设备

在线试玩按浏览器会话区分进度。在嵌入页无法使用 Cookie 时，可以[独立打开游戏](https://jiehu-claire-last-archive.hf.space/)。

在「进度管理」里点 **导出存档文件**，将当前进度保存为 JSON。换电脑、换浏览器或清除 Cookie 后，点 **导入存档文件** 并确认。导入会替换当前会话的存档。探索或交战中，请先返城。

本地网页版默认保存到 `web-saves/`。终端版和 Windows 桌面版使用 `save.json`。

## 本地运行

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。网页使用内置模拟叙事，可在本地运行。终端版的 AI 配置见[接入指南](docs/AI_INTEGRATION.md)。

| 版本 | 命令 |
| --- | --- |
| 网页版 | `dotnet run --framework net10.0 -- --web`，打开 [localhost:8080](http://localhost:8080/) |
| 终端版 | `dotnet run --framework net10.0` |
| Windows 桌面版 | `dotnet run --framework net10.0-windows -- --gui` |
| 核心测试 | `dotnet run --framework net10.0 -- --test` |

### 网页测试

使用 Node.js 24 和 Playwright WebKit。测试会启动独立服务，覆盖小屏布局、经营、战斗、主线和跨会话存档。

```sh
dotnet build -c Release
test_deps="$(mktemp -d)"
npm install --prefix "$test_deps" --no-save playwright@1.65.1
"$test_deps/node_modules/.bin/playwright" install webkit
NODE_PATH="$test_deps/node_modules" node tests/web-flow.cjs
```

Linux CI 可用 `playwright install --with-deps webkit` 补齐浏览器系统依赖。

## Docker 部署

容器默认监听 `7860`，网页存档目录为 `/data/last-archive`，可用 `PORT` 和 `LAST_ARCHIVE_SAVE_DIR` 覆盖。

```sh
docker build -t last-archive .
docker run --rm -p 7860:7860 -v last-archive-saves:/data last-archive
```

在 Hugging Face Spaces 使用 Docker SDK，设置 `app_port: 7860`，将持久存储挂载到 `/data`。同步源码时保留 Space README 顶部的配置和图片文件的 LFS 跟踪规则。游戏界面由程序集提供，封面与探索场景需要提交真实图片内容。

## 项目结构

- [Assets/Web](Assets/Web) 存放网页界面和场景图。
- [Assets/Scripts](Assets/Scripts) 存放游戏系统，以及网页、终端和 Windows 桌面入口。
- [tests](tests) 存放 WebKit 端到端测试。
- [docs](docs) 存放架构与 AI 接入资料。
