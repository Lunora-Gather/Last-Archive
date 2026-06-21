# Last Archive - AI 系统接入说明

## 当前状态

MVP 阶段使用 `MockAIProvider`，纯本地模板生成，不依赖任何外部 API。

## IAIProvider 接口

```csharp
public interface IAIProvider
{
    string Name { get; }
    string GenerateDialogue(DialogueContext context);
    string GenerateQuest(QuestContext context);
    string SummarizeMemory(MemoryContext context);
    string GenerateDailyEvent(EventContext context);
}
```

## 接入 GLM 5.2 的步骤

### 1. 创建 GLMProvider

```csharp
public class GLMProvider : IAIProvider
{
    public string Name => "GLM5.2";
    private string _apiKey;
    private string _apiUrl;

    public GLMProvider(string apiKey, string apiUrl)
    {
        _apiKey = apiKey;
        _apiUrl = apiUrl;
    }

    public string GenerateDialogue(DialogueContext context)
    {
        string prompt = new PromptBuilder().BuildDialoguePrompt(context);
        string response = CallGLMAPI(prompt);
        return response;
    }

    // ... 其他方法类似

    private string CallGLMAPI(string prompt)
    {
        // HTTP 调用 GLM API
        // 返回 JSON 响应
    }
}
```

### 2. 替换 Provider

```csharp
// 在 GameManager.Initialize() 中
var aiProvider = new GLMProvider(apiKey, apiUrl);
// 或 fallback 到 MockAI
var aiProvider = useGLM ? new GLMProvider(key, url) : new MockAIProvider();
```

### 3. AI 输出格式要求

#### 对话输出
纯文本，直接显示。

#### 任务输出
必须为 JSON：
```json
{
  "questId": "npc_xxx_001",
  "title": "任务标题",
  "description": "任务描述",
  "type": "NPCQuest",
  "relatedNPCs": ["lin_doctor"],
  "relatedLocation": "abandoned_hospital",
  "objectives": [
    { "type": "VisitLocation", "targetId": "abandoned_hospital", "requiredAmount": 1 }
  ],
  "rewards": [
    { "resourceType": "Medicine", "amount": 5 }
  ]
}
```

#### 记忆总结
纯文本摘要。

#### 每日事件
纯文本描述。

## ContentValidator 校验规则

1. questId 必须唯一
2. relatedNPCs 必须真实存在
3. relatedLocation 必须真实存在
4. objective 类型必须合法
5. targetId 必须存在
6. reward 数值在合理范围内（≤100）
7. 不允许奖励超过上限
8. 不允许惩罚直接导致游戏崩盘
9. 不允许引用未解锁地图
10. 不允许生成空任务
11. 不允许生成不存在的资源类型
12. 不允许生成无法完成的任务

## 容错机制

```
AI调用 → 解析JSON → 校验 → 使用
                ↓失败      ↓失败
            fallback模板  丢弃结果
```

- 网络超时 → 使用 MockAIProvider
- JSON 解析失败 → 使用模板
- 校验失败 → 丢弃，不进入游戏
- 任何异常 → 不影响游戏运行
