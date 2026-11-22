# Last Archive - AI 系统接入说明

## 网页试玩

网页固定使用 `MockAIProvider`。浏览器只能查询当前提供者，网页拒绝保存或测试第三方配置。

## 终端版

新游戏启动时，在 AI 提供者菜单中选择 GLM、DeepSeek、Ollama 或自定义 OpenAI 兼容服务。也可以通过菜单读取本地 `ai_config.json`，或使用 `LAST_ARCHIVE_API_KEY`、`LAST_ARCHIVE_BASE_URL` 和 `LAST_ARCHIVE_MODEL` 环境变量。

`OpenAIProvider` 调用 OpenAI 兼容的聊天接口，网络或接口错误时回退到本地模板。终端版的 API 密钥应只放在本机配置或进程环境变量中。

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

## AI 输出格式要求

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
