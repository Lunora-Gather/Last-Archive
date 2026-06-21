// ============================================================
// Last Archive - AI系统
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace LastArchive
{
    /// <summary>
    /// AI提供者接口
    /// </summary>
    public interface IAIProvider
    {
        string Name { get; }
        string GenerateDialogue(DialogueContext context);
        string GenerateQuest(QuestContext context);
        string SummarizeMemory(MemoryContext context);
        string GenerateDailyEvent(EventContext context);
    }

    /// <summary>
    /// 对话上下文
    /// </summary>
    public class DialogueContext
    {
        public string NPCId;
        public string NPCName;
        public string NPCRole;
        public int RelationshipValue;
        public string NPCMood;          // 基于morale/hunger等
        public string RecentMemory;     // 最近记忆摘要
        public string PlayerAction;     // 触发对话的行为
        public int CurrentDay;
    }

    /// <summary>
    /// 任务生成上下文
    /// </summary>
    public class QuestContext
    {
        public int CurrentDay;
        public List<string> AvailableNPCs = new List<string>();
        public List<string> AvailableLocations = new List<string>();
        public Dictionary<ResourceType, int> CurrentResources = new Dictionary<ResourceType, int>();
        public List<string> CompletedQuests = new List<string>();
    }

    /// <summary>
    /// 记忆总结上下文
    /// </summary>
    public class MemoryContext
    {
        public string NPCId;
        public string NPCName;
        public int Day;
        public List<MemoryEntry> TodayEntries = new List<MemoryEntry>();
        public string PreviousSummary;
    }

    /// <summary>
    /// 事件生成上下文
    /// </summary>
    public class EventContext
    {
        public int CurrentDay;
        public int NPCCount;
        public Dictionary<ResourceType, int> Resources = new Dictionary<ResourceType, int>();
        public List<string> ActiveQuests = new List<string>();
    }

    // ========== MockAIProvider ==========

    /// <summary>
    /// 本地假AI，用模板返回内容，保证游戏不联网也能玩
    /// </summary>
    public class MockAIProvider : IAIProvider
    {
        public string Name => "MockAI";

        private Random _rng = new Random();

        public string GenerateDialogue(DialogueContext context)
        {
            var sb = new StringBuilder();

            // 根据关系值和NPC角色生成不同对话
            string greeting = context.RelationshipValue > 20 ? "很高兴见到你。"
                : context.RelationshipValue > 0 ? "你好。"
                : context.RelationshipValue > -20 ? "有什么事？"
                : "我不想和你说话。";

            // 根据角色添加特色
            switch (context.NPCRole)
            {
                case "Doctor":
                    sb.AppendLine($"林医生：{greeting}");
                    if (context.RelationshipValue > 10)
                        sb.AppendLine("  \"药品库存还够吗？如果不够，我可以想办法。\"");
                    else
                        sb.AppendLine("  \"希望今天不会有人受伤。\"");
                    break;
                case "Engineer":
                    sb.AppendLine($"老周：{greeting}");
                    sb.AppendLine("  \"设备维护不能停，我来想办法。\"");
                    break;
                case "Scout":
                    sb.AppendLine($"安娜：{greeting}");
                    if (context.RelationshipValue > 0)
                        sb.AppendLine("  \"外面有些地方还没探索过，要不要去看看？\"");
                    else
                        sb.AppendLine("  \"别跟着我。\"");
                    break;
                case "Child":
                    sb.AppendLine($"小北：{greeting}");
                    sb.AppendLine("  \"我今天又画了灯塔...你觉得它在哪里？\"");
                    break;
                case "Stranger":
                    sb.AppendLine($"沈牧：{greeting}");
                    sb.AppendLine("  \"档案馆里有些东西...不是所有人都该知道的。\"");
                    break;
                default:
                    sb.AppendLine($"{context.NPCName}：{greeting}");
                    break;
            }

            // 根据心情补充
            if (context.NPCMood == "hungry")
                sb.AppendLine("  \"...我好饿。\"");
            else if (context.NPCMood == "injured")
                sb.AppendLine("  \"...我需要治疗。\"");
            else if (context.NPCMood == "happy")
                sb.AppendLine("  \"今天感觉还不错！\"");

            return sb.ToString();
        }

        public string GenerateQuest(QuestContext context)
        {
            // 模板生成简单支线任务
            var templates = new List<string>
            {
                $"\"物资搜集\"：小镇的储备不足，需要派人去搜索附近的废墟。当前食物：{context.CurrentResources.GetValueOrDefault(ResourceType.Food, 0)}",
                $"\"巡逻任务\"：第{context.CurrentDay}天了，周围似乎有异常动静，需要加强巡逻。",
                $"\"设施维护\"：有些建筑需要零件来维护，当前零件：{context.CurrentResources.GetValueOrDefault(ResourceType.Parts, 0)}"
            };

            return templates[_rng.Next(templates.Count)];
        }

        public string SummarizeMemory(MemoryContext context)
        {
            var sb = new StringBuilder();
            string prefix = "";
            switch (context.NPCId)
            {
                case "lin_doctor":
                    prefix = "【林医生的诊断日志】";
                    break;
                case "old_zhou":
                    prefix = "【老周的地铁工单】";
                    break;
                case "anna":
                    prefix = "【安娜的废土侦测】";
                    break;
                case "xiaobei":
                    prefix = "【小北的秘密本子】";
                    break;
                case "shen_mu":
                    prefix = "【沈牧的绝密备忘】";
                    break;
                default:
                    prefix = $"【{context.NPCName}的生存自白】";
                    break;
            }
            sb.Append(prefix);

            if (context.TodayEntries.Count == 0)
            {
                sb.Append("今天一切如常，风暴未至。我们守在城内，耳边只有引擎的低鸣。");
            }
            else
            {
                foreach (var entry in context.TodayEntries)
                {
                    switch (entry.EventType)
                    {
                        case MemoryEventType.PlayerHelpedNPC:
                            sb.Append("管理者今天给予了我极大的关怀与倾听。在这废墟深处，仍有值得守护的信任。");
                            break;
                        case MemoryEventType.PlayerIgnoredNPC:
                            sb.Append("管理者冷漠地拒绝了我的诉求。废土的法则早已印证，能拯救自己的只有我们自己。");
                            break;
                        case MemoryEventType.NPCJoinedExploration:
                            sb.Append("今天背起行囊走进了死寂的迷宫荒野，脚踩在碎石瓦砾上，我们只是为了微薄的希望挣扎。");
                            break;
                        case MemoryEventType.NPCInjured:
                            sb.Append("伤痛撕裂了皮肤。这片废墟在向我索要代价，鲜血滴落在泥土里，我不知道还能撑多久。");
                            break;
                        case MemoryEventType.QuestCompleted:
                            sb.Append("今天完成了一项看似不可能的命令。看到大家脸上的疲惫稍微消褪，我心里也有些许暖意。");
                            break;
                        case MemoryEventType.ResourceCrisis:
                            sb.Append("仓库传来的警报刺痛着每个人的神经。食物和水越来越少，饥荒与危机如影随形。");
                            break;
                        default:
                            sb.Append(entry.Description + "。");
                            break;
                    }
                }
            }

            return sb.ToString();
        }

        public string GenerateDailyEvent(EventContext context)
        {
            var events = new List<string>
            {
                "清晨的雾气中，远处传来奇怪的声响。",
                "今天的阳光比往常更温暖，居民们精神好了些。",
                "档案馆的灯光在夜里闪烁了一下，似乎有什么被激活了。",
                "一个孩子说在梦里看到了旧世界的城市。",
                "温室里的植物长势不错，大家都很高兴。",
                "有人在夜里看到档案馆的墙壁上出现了奇怪的文字。"
            };

            return events[_rng.Next(events.Count)];
        }
    }

    // ========== PromptBuilder ==========

    /// <summary>
    /// 根据游戏状态构造提示词
    /// </summary>
    public class PromptBuilder
    {
        public string BuildDialoguePrompt(DialogueContext context)
        {
            return $"[对话生成] NPC:{context.NPCName} 角色:{context.NPCRole} 关系:{context.RelationshipValue} 心情:{context.NPCMood} 天数:{context.CurrentDay}";
        }

        public string BuildQuestPrompt(QuestContext context)
        {
            return $"[任务生成] 天数:{context.CurrentDay} NPC数:{context.AvailableNPCs.Count} 已完成任务:{string.Join(",", context.CompletedQuests)}";
        }

        public string BuildMemoryPrompt(MemoryContext context)
        {
            return $"[记忆总结] NPC:{context.NPCName} 天:{context.Day} 事件数:{context.TodayEntries.Count} 前摘要:{context.PreviousSummary}";
        }

        public string BuildEventPrompt(EventContext context)
        {
            return $"[事件生成] 天:{context.CurrentDay} NPC:{context.NPCCount} 任务:{string.Join(",", context.ActiveQuests)}";
        }
    }

    // ========== AIResponseParser ==========

    /// <summary>
    /// 解析AI返回的JSON
    /// </summary>
    public class AIResponseParser
    {
        /// <summary>简单JSON解析（不依赖外部库）</summary>
        public static Dictionary<string, string> ParseSimpleJson(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return result;

            // 简单的key:value解析
            json = json.Trim().TrimStart('{').TrimEnd('}');
            var parts = json.Split(',');

            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length >= 2)
                {
                    string key = kv[0].Trim().Trim('"');
                    string value = kv[1].Trim().Trim('"');
                    result[key] = value;
                }
            }

            return result;
        }
    }

    // ========== ContentValidator ==========

    /// <summary>
    /// 校验AI生成内容是否合法
    /// </summary>
    public class ContentValidator
    {
        private HashSet<string> _validNPCIds = new HashSet<string>();
        private HashSet<string> _validLocationIds = new HashSet<string>();
        private HashSet<string> _existingQuestIds = new HashSet<string>();
        private HashSet<string> _validResourceTypes = new HashSet<string>();

        public ContentValidator()
        {
            foreach (ResourceType rt in Enum.GetValues(typeof(ResourceType)))
            {
                _validResourceTypes.Add(rt.ToString());
            }
            foreach (ObjectiveType ot in Enum.GetValues(typeof(ObjectiveType)))
            {
                // 合法目标类型
            }
        }

        public void RegisterNPCIds(string[] ids) { foreach (var id in ids) _validNPCIds.Add(id); }
        public void RegisterLocationIds(string[] ids) { foreach (var id in ids) _validLocationIds.Add(id); }
        public void RegisterQuestIds(string[] ids) { foreach (var id in ids) _existingQuestIds.Add(id); }

        /// <summary>校验任务数据</summary>
        public bool ValidateQuest(QuestData quest, out string error)
        {
            error = "";

            // 1. questId唯一
            if (_existingQuestIds.Contains(quest.QuestId))
            {
                error = "QuestId已存在";
                return false;
            }

            // 2. relatedNPCs真实存在
            foreach (var npcId in quest.RelatedNPCs)
            {
                if (!_validNPCIds.Contains(npcId))
                {
                    error = $"NPC {npcId} 不存在";
                    return false;
                }
            }

            // 3. relatedLocation真实存在
            if (!string.IsNullOrEmpty(quest.RelatedLocation) && !_validLocationIds.Contains(quest.RelatedLocation))
            {
                error = $"地点 {quest.RelatedLocation} 不存在";
                return false;
            }

            // 4. 目标类型合法
            foreach (var obj in quest.Objectives)
            {
                if (!Enum.IsDefined(typeof(ObjectiveType), obj.Type))
                {
                    error = $"目标类型 {obj.Type} 不合法";
                    return false;
                }
            }

            // 5. 奖励数值合理
            foreach (var reward in quest.Rewards)
            {
                if (reward.ResourceType.HasValue && reward.ResourceAmount > 100)
                {
                    error = "奖励数值过大";
                    return false;
                }
            }

            // 6. 不允许空任务
            if (quest.Objectives.Count == 0)
            {
                error = "任务没有目标";
                return false;
            }

            return true;
        }
    }

    // ========== 生成器 ==========

    public class QuestGenerator
    {
        private IAIProvider _provider;
        private ContentValidator _validator;

        public QuestGenerator(IAIProvider provider, ContentValidator validator)
        {
            _provider = provider;
            _validator = validator;
        }

        public QuestData Generate(QuestContext context)
        {
            string text = _provider.GenerateQuest(context);
            // MockAI返回的是文本描述，不是JSON
            // 这里简单包装成一个探索任务
            var quest = new QuestData
            {
                QuestId = $"auto_{context.CurrentDay}_{Guid.NewGuid().ToString().Substring(0, 8)}",
                Title = "日常任务",
                Description = text,
                Type = QuestType.SideQuest,
                Status = QuestStatus.NotStarted,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective
                    {
                        Type = ObjectiveType.SurviveDays,
                        TargetId = "",
                        RequiredAmount = context.CurrentDay + 2,
                        CurrentProgress = context.CurrentDay
                    }
                }
            };

            string error;
            if (_validator.ValidateQuest(quest, out error))
            {
                return quest;
            }
            return null; // 校验失败，丢弃
        }
    }

    public class DialogueGenerator
    {
        private IAIProvider _provider;

        public DialogueGenerator(IAIProvider provider)
        {
            _provider = provider;
        }

        public string Generate(DialogueContext context)
        {
            return _provider.GenerateDialogue(context);
        }
    }

    public class MemorySummarizer
    {
        private IAIProvider _provider;

        public MemorySummarizer(IAIProvider provider)
        {
            _provider = provider;
        }

        public string Summarize(MemoryContext context)
        {
            return _provider.SummarizeMemory(context);
        }
    }

    public class EventGenerator
    {
        private IAIProvider _provider;

        public EventGenerator(IAIProvider provider)
        {
            _provider = provider;
        }

        public string Generate(EventContext context)
        {
            return _provider.GenerateDailyEvent(context);
        }
    }
}
