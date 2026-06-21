# Last Archive - Unity 迁移指南

## 概述

当前 MVP 使用纯 C# 实现所有核心逻辑，不依赖 Unity API。迁移到 Unity 只需要：

1. 将脚本复制到 Unity 项目 `Assets/Scripts/` 下
2. 创建 MonoBehaviour 包装器连接核心系统和 Unity 生命周期
3. 用 Unity UI 替换控制台 UI

## 迁移步骤

### Step 1：创建 Unity 2D 项目

- Unity 2022.3 LTS 或更高
- 2D 模板
- 将 `Assets/Scripts/` 整个目录复制到 Unity 项目

### Step 2：处理不兼容的 API

以下 API 需要替换：

| 纯 C# | Unity 替代 |
|-------|-----------|
| `Console.WriteLine()` | `Debug.Log()` / UI 显示 |
| `Console.ReadLine()` | UI 输入事件 |
| `Console.ReadKey()` | 协程等待 |
| `System.Text.Json` | `JsonUtility`（或保留，Unity 2022+ 支持） |
| `File.ReadAllText()` | `File.ReadAllText()`（可用，但建议用 StreamingAssets） |
| `Random` | `UnityEngine.Random`（可选） |

### Step 3：创建 MonoBehaviour 包装器

```csharp
// UnityGameManager.cs
using UnityEngine;

public class UnityGameManager : MonoBehaviour
{
    public static UnityGameManager Instance { get; private set; }
    
    private LastArchive.GameManager _gameManager;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        _gameManager = new LastArchive.GameManager();
        _gameManager.Initialize(new LastArchive.MockAIProvider());
    }

    void Start()
    {
        _gameManager.StartNewGame();
    }

    void Update()
    {
        // 处理输入、更新UI等
    }

    void OnApplicationQuit()
    {
        _gameManager?.SaveGame();
    }
}
```

### Step 4：创建 Unity UI

#### 主菜单场景 (MainMenu.unity)
- Canvas + Panel
- Button: New Game / Continue / Quit
- `MainMenuUI.cs` MonoBehaviour

#### 基地场景 (BaseScene.unity)
- 顶部：天数 + 阶段 + 资源栏
- 左侧：NPC 列表
- 右侧：建筑面板
- 底部：任务栏 + 操作按钮
- `BaseUI.cs` MonoBehaviour

#### 探索场景 (ExplorationScene.unity)
- 地图名称 + 房间列表
- 当前房间描述
- 搜索/移动/返回 按钮
- `ExplorationUI.cs` MonoBehaviour

#### 战斗面板 (CombatPanel)
- 我方/敌方 单位卡片
- 行动按钮：攻击/防御/用药/逃跑
- 战斗日志
- `CombatUI.cs` MonoBehaviour

### Step 5：场景切换

```csharp
public class SceneController : MonoBehaviour
{
    public void LoadBaseScene() => SceneManager.LoadScene("BaseScene");
    public void LoadExplorationScene() => SceneManager.LoadScene("ExplorationScene");
    public void LoadMainMenu() => SceneManager.LoadScene("MainMenu");
}
```

### Step 6：存档路径

```csharp
// 修改 SaveSystem 的路径
string savePath = Path.Combine(Application.persistentDataPath, "save.json");
```

## 注意事项

1. **命名空间**：所有核心代码在 `LastArchive` 命名空间下，Unity 脚本引用时需要 `using LastArchive;`
2. **JsonUtility 限制**：Unity 的 JsonUtility 不支持 Dictionary，需要自定义序列化或保留 System.Text.Json
3. **EventBus 线程安全**：当前 EventBus 不是线程安全的，Unity 中 AI 调用如果异步需要加锁
4. **DontDestroyOnLoad**：GameManager 需要跨场景存活
5. **Resources 文件夹**：Unity 的 Resources 文件夹有特殊含义，数据文件建议放在 StreamingAssets

## 美术资源占位

MVP 阶段可以用：
- 色块 Sprite 作为建筑/NPC 头像
- Unity UI Text/Button 作为界面
- 简单 Animation 作为占位动画
- 后续替换为正式美术
