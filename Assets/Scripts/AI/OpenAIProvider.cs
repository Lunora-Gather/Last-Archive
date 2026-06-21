// ============================================================
// Last Archive - OpenAI 兼容 AI Provider
// 支持 GLM / DeepSeek / 任何 OpenAI 兼容中转站
// ============================================================

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LastArchive
{
    /// <summary>
    /// AI 提供商配置 - 一个配置走天下
    /// </summary>
    public class AIProviderConfig
    {
        /// <summary>API 基础URL（含 /v1）</summary>
        /// <remarks>
        /// GLM:      https://open.bigmodel.cn/api/paas/v4
        /// DeepSeek: https://api.deepseek.com/v1
        /// 中转站:   https://your-proxy.com/v1
        /// 本地Ollama: http://localhost:11434/v1
        /// </remarks>
        public string ApiBaseUrl { get; set; } = "https://open.bigmodel.cn/api/paas/v4";

        /// <summary>API Key</summary>
        public string ApiKey { get; set; } = "";

        /// <summary>模型名称</summary>
        /// <remarks>
        /// GLM:      glm-4-flash / glm-4 / glm-4-plus
        /// DeepSeek: deepseek-chat / deepseek-reasoner
        /// 中转站:   视中转站支持的模型
        /// Ollama:   qwen2.5 / llama3 等
        /// </remarks>
        public string Model { get; set; } = "glm-4-flash";

        /// <summary>请求超时（秒）</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>最大 token</summary>
        public int MaxTokens { get; set; } = 1024;

        /// <summary>温度（0.0=确定性, 1.0=创造性）</summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>系统提示词前缀</summary>
        public string SystemPromptPrefix { get; set; } = "你是「最后档案城」游戏的AI叙事引擎。用简洁的中文回复，保持末日废土风格。";

        // === 预设配置 ===

        /// <summary>GLM-4-Flash（免费）</summary>
        public static AIProviderConfig GLM4Flash(string apiKey) => new AIProviderConfig
        {
            ApiBaseUrl = "https://open.bigmodel.cn/api/paas/v4",
            ApiKey = apiKey,
            Model = "glm-4-flash",
            Temperature = 0.8
        };

        /// <summary>GLM-4-Plus</summary>
        public static AIProviderConfig GLM4Plus(string apiKey) => new AIProviderConfig
        {
            ApiBaseUrl = "https://open.bigmodel.cn/api/paas/v4",
            ApiKey = apiKey,
            Model = "glm-4-plus",
            Temperature = 0.7
        };

        /// <summary>DeepSeek-Chat</summary>
        public static AIProviderConfig DeepSeekChat(string apiKey) => new AIProviderConfig
        {
            ApiBaseUrl = "https://api.deepseek.com/v1",
            ApiKey = apiKey,
            Model = "deepseek-chat",
            Temperature = 0.7
        };

        /// <summary>DeepSeek-Reasoner（推理型）</summary>
        public static AIProviderConfig DeepSeekReasoner(string apiKey) => new AIProviderConfig
        {
            ApiBaseUrl = "https://api.deepseek.com/v1",
            ApiKey = apiKey,
            Model = "deepseek-reasoner",
            Temperature = 0.3
        };

        /// <summary>自定义中转站</summary>
        public static AIProviderConfig CustomProxy(string baseUrl, string apiKey, string model) => new AIProviderConfig
        {
            ApiBaseUrl = baseUrl,
            ApiKey = apiKey,
            Model = model,
            Temperature = 0.7
        };

        /// <summary>本地 Ollama</summary>
        public static AIProviderConfig Ollama(string model = "qwen2.5") => new AIProviderConfig
        {
            ApiBaseUrl = "http://localhost:11434/v1",
            ApiKey = "ollama",  // Ollama 不需要 key，但字段不能为空
            Model = model,
            Temperature = 0.7,
            TimeoutSeconds = 60
        };
    }

    // === OpenAI 兼容 API 请求/响应结构 ===

    internal class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; }
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; }
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("stream")] public bool Stream { get; set; } = false;
    }

    internal class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; }
        [JsonPropertyName("content")] public string Content { get; set; }
    }

    internal class ChatResponse
    {
        [JsonPropertyName("choices")] public List<ChatChoice> Choices { get; set; }
        [JsonPropertyName("error")] public ChatError Error { get; set; }
    }

    internal class ChatChoice
    {
        [JsonPropertyName("message")] public ChatMessage Message { get; set; }
    }

    internal class ChatError
    {
        [JsonPropertyName("message")] public string Message { get; set; }
    }

    /// <summary>
    /// OpenAI 兼容 AI Provider - 通用实现
    /// 兼容 GLM / DeepSeek / 任何中转站 / Ollama
    /// </summary>
    public class OpenAIProvider : IAIProvider
    {
        private readonly AIProviderConfig _config;
        private readonly HttpClient _http;
        private readonly PromptBuilder _promptBuilder = new PromptBuilder();
        private readonly IAIProvider _fallback;

        /// <summary>提供商名称</summary>
        public string Name => $"OpenAI兼容({_config.Model}@{_config.ApiBaseUrl})";

        /// <summary>上次错误信息</summary>
        public string LastError { get; private set; } = "";

        /// <summary>是否降级到 Mock</summary>
        public bool IsFallback { get; private set; } = false;

        /// <summary>
        /// 创建 OpenAI 兼容 Provider
        /// </summary>
        /// <param name="config">API 配置</param>
        /// <param name="fallback">降级 Provider（默认 MockAIProvider）</param>
        public OpenAIProvider(AIProviderConfig config, IAIProvider fallback = null)
        {
            _config = config;
            _fallback = fallback ?? new MockAIProvider();

            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
            _http.DefaultRequestHeaders.Add("User-Agent", "LastArchive/1.0");
        }

        /// <summary>生成对话</summary>
        public string GenerateDialogue(DialogueContext context)
        {
            string systemPrompt = _config.SystemPromptPrefix + "\n\n你正在扮演NPC与玩家对话。根据上下文生成自然、有角色感的回复。不要输出旁白或动作描述。";
            string userPrompt = _promptBuilder.BuildDialoguePrompt(context);
            return CallAPI(systemPrompt, userPrompt, () => _fallback.GenerateDialogue(context));
        }

        /// <summary>生成任务</summary>
        public string GenerateQuest(QuestContext context)
        {
            string systemPrompt = _config.SystemPromptPrefix + "\n\n你正在为游戏生成任务。输出格式：任务标题+描述，简洁有创意。";
            string userPrompt = _promptBuilder.BuildQuestPrompt(context);
            return CallAPI(systemPrompt, userPrompt, () => _fallback.GenerateQuest(context));
        }

        /// <summary>总结记忆</summary>
        public string SummarizeMemory(MemoryContext context)
        {
            string systemPrompt = _config.SystemPromptPrefix + "\n\n你正在总结NPC的日常经历。用1-2句话概括，保留关键事件。";
            string userPrompt = _promptBuilder.BuildMemoryPrompt(context);
            return CallAPI(systemPrompt, userPrompt, () => _fallback.SummarizeMemory(context));
        }

        /// <summary>生成每日事件</summary>
        public string GenerateDailyEvent(EventContext context)
        {
            string systemPrompt = _config.SystemPromptPrefix + "\n\n你正在生成每日随机事件。输出格式：事件标题+描述，简短有趣。";
            string userPrompt = _promptBuilder.BuildEventPrompt(context);
            return CallAPI(systemPrompt, userPrompt, () => _fallback.GenerateDailyEvent(context));
        }

        /// <summary>核心 API 调用</summary>
        private string CallAPI(string systemPrompt, string userPrompt, Func<string> fallbackFn)
        {
            try
            {
                var request = new ChatRequest
                {
                    Model = _config.Model,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "system", Content = systemPrompt },
                        new ChatMessage { Role = "user", Content = userPrompt }
                    },
                    MaxTokens = _config.MaxTokens,
                    Temperature = _config.Temperature
                };

                string json = JsonSerializer.Serialize(request);
                string url = _config.ApiBaseUrl.TrimEnd('/') + "/chat/completions";

                var httpResponse = _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                string responseJson = httpResponse.Content.ReadAsStringAsync().Result;

                if (!httpResponse.IsSuccessStatusCode)
                {
                    LastError = $"HTTP {(int)httpResponse.StatusCode}: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}";
                    Console.WriteLine($"  [AI错误] {LastError}");
                    return Fallback(fallbackFn);
                }

                var response = JsonSerializer.Deserialize<ChatResponse>(responseJson);

                if (response?.Error != null)
                {
                    LastError = $"API错误: {response.Error.Message}";
                    Console.WriteLine($"  [AI错误] {LastError}");
                    return Fallback(fallbackFn);
                }

                if (response?.Choices == null || response.Choices.Count == 0)
                {
                    LastError = "API返回空choices";
                    Console.WriteLine($"  [AI错误] {LastError}");
                    return Fallback(fallbackFn);
                }

                IsFallback = false;
                string content = response.Choices[0].Message?.Content ?? "";
                return content.Trim();
            }
            catch (AggregateException ex)
            {
                LastError = ex.InnerException?.Message ?? ex.Message;
                Console.WriteLine($"  [AI异常] {LastError}");
                return Fallback(fallbackFn);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"  [AI异常] {LastError}");
                return Fallback(fallbackFn);
            }
        }

        /// <summary>降级到 MockAI</summary>
        private string Fallback(Func<string> fallbackFn)
        {
            IsFallback = true;
            Console.WriteLine("  [AI降级] 使用 MockAIProvider 兜底");
            return fallbackFn();
        }

        // === 异步版本（供 Unity 等异步环境使用）===

        public async Task<string> GenerateDialogueAsync(DialogueContext context)
        {
            string systemPrompt = _config.SystemPromptPrefix + "\n\n你正在扮演NPC与玩家对话。";
            string userPrompt = _promptBuilder.BuildDialoguePrompt(context);
            return await CallAPIAsync(systemPrompt, userPrompt, () => _fallback.GenerateDialogue(context));
        }

        public async Task<string> GenerateQuestAsync(QuestContext context)
        {
            string systemPrompt = _config.SystemPromptPrefix + "\n\n你正在为游戏生成任务。";
            string userPrompt = _promptBuilder.BuildQuestPrompt(context);
            return await CallAPIAsync(systemPrompt, userPrompt, () => _fallback.GenerateQuest(context));
        }

        private async Task<string> CallAPIAsync(string systemPrompt, string userPrompt, Func<string> fallbackFn)
        {
            try
            {
                var request = new ChatRequest
                {
                    Model = _config.Model,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "system", Content = systemPrompt },
                        new ChatMessage { Role = "user", Content = userPrompt }
                    },
                    MaxTokens = _config.MaxTokens,
                    Temperature = _config.Temperature
                };

                string json = JsonSerializer.Serialize(request);
                string url = _config.ApiBaseUrl.TrimEnd('/') + "/chat/completions";

                var httpResponse = await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                string responseJson = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    LastError = $"HTTP {(int)httpResponse.StatusCode}";
                    return Fallback(fallbackFn);
                }

                var response = JsonSerializer.Deserialize<ChatResponse>(responseJson);
                if (response?.Choices?.Count > 0)
                {
                    IsFallback = false;
                    return (response.Choices[0].Message?.Content ?? "").Trim();
                }

                return Fallback(fallbackFn);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return Fallback(fallbackFn);
            }
        }

        /// <summary>测试连接与延迟测试</summary>
        public bool TestConnection(out string error, out long latencyMs)
        {
            error = "";
            latencyMs = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var request = new ChatRequest
                {
                    Model = _config.Model,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "user", Content = "ping" }
                    },
                    MaxTokens = 5,
                    Temperature = 0.0
                };

                string json = JsonSerializer.Serialize(request);
                string url = _config.ApiBaseUrl.TrimEnd('/') + "/chat/completions";

                var httpResponse = _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                string responseJson = httpResponse.Content.ReadAsStringAsync().Result;
                sw.Stop();
                latencyMs = sw.ElapsedMilliseconds;

                if (!httpResponse.IsSuccessStatusCode)
                {
                    error = $"HTTP {(int)httpResponse.StatusCode}: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}";
                    return false;
                }

                var response = JsonSerializer.Deserialize<ChatResponse>(responseJson);
                if (response?.Error != null)
                {
                    error = $"API Error: {response.Error.Message}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                sw.Stop();
                latencyMs = sw.ElapsedMilliseconds;
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }
    }

    // === 快捷工厂 ===

    /// <summary>
    /// AI Provider 工厂 - 快速创建各种 Provider
    /// </summary>
    public static class AIProviderFactory
    {
        /// <summary>从环境变量创建（LAST_ARCHIVE_API_KEY + LAST_ARCHIVE_BASE_URL + LAST_ARCHIVE_MODEL）</summary>
        public static IAIProvider FromEnvironment()
        {
            string apiKey = Environment.GetEnvironmentVariable("LAST_ARCHIVE_API_KEY") ?? "";
            string baseUrl = Environment.GetEnvironmentVariable("LAST_ARCHIVE_BASE_URL") ?? "";
            string model = Environment.GetEnvironmentVariable("LAST_ARCHIVE_MODEL") ?? "";

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(baseUrl))
            {
                Console.WriteLine("  [AI] 未设置环境变量，使用 MockAIProvider");
                return new MockAIProvider();
            }

            return new OpenAIProvider(new AIProviderConfig
            {
                ApiBaseUrl = baseUrl,
                ApiKey = apiKey,
                Model = string.IsNullOrEmpty(model) ? "glm-4-flash" : model
            });
        }

        /// <summary>从配置文件创建（ai_config.json）</summary>
        public static IAIProvider FromConfigFile(string path = "ai_config.json")
        {
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    Console.WriteLine($"  [AI] 未找到配置文件 {path}，使用 MockAIProvider");
                    return new MockAIProvider();
                }

                string json = System.IO.File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<AIProviderConfig>(json);

                if (string.IsNullOrEmpty(config?.ApiKey) || string.IsNullOrEmpty(config?.ApiBaseUrl))
                {
                    Console.WriteLine("  [AI] 配置文件缺少 ApiKey 或 ApiBaseUrl，使用 MockAIProvider");
                    return new MockAIProvider();
                }

                Console.WriteLine($"  [AI] 从配置文件加载: {config.Model}@{config.ApiBaseUrl}");
                return new OpenAIProvider(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [AI] 配置文件读取失败: {ex.Message}，使用 MockAIProvider");
                return new MockAIProvider();
            }
        }

        /// <summary>交互式选择 Provider</summary>
        public static IAIProvider Interactive()
        {
            Console.WriteLine("\n  === AI Provider 选择 ===");
            Console.WriteLine("  0. MockAI（离线，无需API）");
            Console.WriteLine("  1. GLM-4-Flash（智谱，免费）");
            Console.WriteLine("  2. GLM-4-Plus（智谱，付费）");
            Console.WriteLine("  3. DeepSeek-Chat");
            Console.WriteLine("  4. DeepSeek-Reasoner");
            Console.WriteLine("  5. 自定义中转站");
            Console.WriteLine("  6. Ollama（本地）");
            Console.WriteLine("  7. 从配置文件加载");
            Console.WriteLine("  8. 从环境变量加载");
            Console.Write("  选择 (0-8): ");

            string choice = Console.ReadLine()?.Trim() ?? "0";
            string apiKey;

            switch (choice)
            {
                case "1":
                    Console.Write("  GLM API Key: ");
                    apiKey = Console.ReadLine()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(apiKey)) goto default;
                    return new OpenAIProvider(AIProviderConfig.GLM4Flash(apiKey));

                case "2":
                    Console.Write("  GLM API Key: ");
                    apiKey = Console.ReadLine()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(apiKey)) goto default;
                    return new OpenAIProvider(AIProviderConfig.GLM4Plus(apiKey));

                case "3":
                    Console.Write("  DeepSeek API Key: ");
                    apiKey = Console.ReadLine()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(apiKey)) goto default;
                    return new OpenAIProvider(AIProviderConfig.DeepSeekChat(apiKey));

                case "4":
                    Console.Write("  DeepSeek API Key: ");
                    apiKey = Console.ReadLine()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(apiKey)) goto default;
                    return new OpenAIProvider(AIProviderConfig.DeepSeekReasoner(apiKey));

                case "5":
                    Console.Write("  API Base URL (含/v1): ");
                    string baseUrl = Console.ReadLine()?.Trim() ?? "";
                    Console.Write("  API Key: ");
                    apiKey = Console.ReadLine()?.Trim() ?? "";
                    Console.Write("  模型名称: ");
                    string model = Console.ReadLine()?.Trim() ?? "gpt-3.5-turbo";
                    if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey)) goto default;
                    return new OpenAIProvider(AIProviderConfig.CustomProxy(baseUrl, apiKey, model));

                case "6":
                    Console.Write("  Ollama 模型 (默认qwen2.5): ");
                    string ollamaModel = Console.ReadLine()?.Trim() ?? "qwen2.5";
                    return new OpenAIProvider(AIProviderConfig.Ollama(ollamaModel));

                case "7":
                    return FromConfigFile();

                case "8":
                    return FromEnvironment();

                default:
                    Console.WriteLine("  使用 MockAIProvider");
                    return new MockAIProvider();
            }
        }
    }
}
