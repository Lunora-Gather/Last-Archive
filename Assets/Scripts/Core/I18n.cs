// ============================================================
// Last Archive - 国际化系统 (i18n)
// 支持中/英双语，运行时切换
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>语言类型</summary>
    public enum Language
    {
        Chinese,  // 中文（默认）
        English   // 英文
    }

    /// <summary>
    /// 本地化系统 - 管理所有文本的翻译
    /// </summary>
    public static class I18n
    {
        private static Language _lang = Language.Chinese;
        private static readonly Dictionary<string, string> _zh = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _en = new Dictionary<string, string>();
        private static bool _initialized = false;

        /// <summary>当前语言</summary>
        public static Language CurrentLanguage
        {
            get => _lang;
            set
            {
                _lang = value;
                Console.WriteLine($"  [i18n] 语言切换为: {(_lang == Language.Chinese ? "中文" : "English")}");
            }
        }

        /// <summary>初始化翻译字典</summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // === 游戏阶段 ===
            Add("phase.day", "白天", "Day");
            Add("phase.night", "夜晚", "Night");
            Add("phase.summary", "结算", "Summary");

            // === 资源 ===
            Add("res.food", "食物", "Food");
            Add("res.water", "水", "Water");
            Add("res.power", "电力", "Power");
            Add("res.medicine", "药品", "Medicine");
            Add("res.parts", "零件", "Parts");
            Add("res.shards", "记忆碎片", "Memory Shards");

            // === 菜单 ===
            Add("menu.npcs", "查看居民", "View Residents");
            Add("menu.buildings", "管理建筑", "Manage Buildings");
            Add("menu.quests", "查看任务", "View Quests");
            Add("menu.dialogue", "与NPC对话", "Talk to NPC");
            Add("menu.work", "分配工作", "Assign Work");
            Add("menu.inventory", "背包物品", "Inventory");
            Add("menu.factions", "派系声望", "Factions");
            Add("menu.achievements", "成就", "Achievements");
            Add("menu.crisis", "危机/心理", "Crisis/Psychology");
            Add("menu.save", "保存游戏", "Save Game");
            Add("menu.end_day", "结束白天，进入夜晚", "End Day, Enter Night");

            // === 游戏结束 ===
            Add("gameover.title", "游戏结束", "Game Over");
            Add("gameover.victory", "胜利！", "Victory!");
            Add("gameover.defeat", "失败", "Defeat");

            // === 心理状态 ===
            Add("mental.hopeful", "希望", "Hopeful");
            Add("mental.stable", "稳定", "Stable");
            Add("mental.anxious", "焦虑", "Anxious");
            Add("mental.despair", "绝望", "Despair");
            Add("mental.traumatized", "创伤", "Traumatized");

            // === 危机 ===
            Add("crisis.famine", "饥荒", "Famine");
            Add("crisis.plague", "瘟疫", "Plague");
            Add("crisis.riot", "叛乱", "Riot");
            Add("crisis.power_outage", "停电", "Power Outage");
            Add("crisis.memory_storm", "记忆风暴", "Memory Storm");
            Add("crisis.raider_raid", "掠夺者袭击", "Raider Raid");

            // === 结局 ===
            Add("ending.broadcast", "广播重启", "Broadcast Restored");
            Add("ending.silence", "永恒沉默", "Eternal Silence");
            Add("ending.upload", "意识上传", "Consciousness Upload");
            Add("ending.destroy", "焚毁一切", "Destroy Everything");
            Add("ending.share", "真相公开", "Truth Revealed");
            Add("ending.vanish", "量子消失", "Quantum Vanish");

            // === 新手引导 ===
            Add("tutorial.welcome", "欢迎来到档案城！这里是人类最后的避难所。", "Welcome to Archive City! The last refuge of humanity.");
            Add("tutorial.day1", "第1天：和居民们对话，了解他们的需求。", "Day 1: Talk to residents and learn their needs.");
            Add("tutorial.build", "建造温室来确保食物供应！", "Build a greenhouse to secure food supply!");
            Add("tutorial.explore", "夜晚可以派NPC外出探索，收集资源。", "Send NPCs to explore at night to gather resources.");
            Add("tutorial.crisis", "注意资源消耗！食物和水耗尽会触发饥荒。", "Watch resource consumption! Running out triggers famine.");
        }

        /// <summary>获取翻译文本</summary>
        public static string T(string key)
        {
            Initialize();
            var dict = _lang == Language.Chinese ? _zh : _en;
            return dict.TryGetValue(key, out var val) ? val : $"[{key}]";
        }

        /// <summary>获取带参数的翻译</summary>
        public static string T(string key, params object[] args)
        {
            try
            {
                return string.Format(T(key), args);
            }
            catch
            {
                return T(key);
            }
        }

        /// <summary>资源类型翻译</summary>
        public static string ResourceName(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Food: return T("res.food");
                case ResourceType.Water: return T("res.water");
                case ResourceType.Power: return T("res.power");
                case ResourceType.Medicine: return T("res.medicine");
                case ResourceType.Parts: return T("res.parts");
                case ResourceType.MemoryShards: return T("res.shards");
                default: return type.ToString();
            }
        }

        private static void Add(string key, string zh, string en)
        {
            _zh[key] = zh;
            _en[key] = en;
        }
    }
}
