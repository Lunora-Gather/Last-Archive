// ============================================================
// Last Archive - 新手引导系统
// 第1天教程 + 渐进式提示
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 新手引导 - 根据天数和进度显示教程提示
    /// </summary>
    public class TutorialSystem
    {
        private HashSet<string> _shownTips = new HashSet<string>();
        private int _currentDay = 0;

        /// <summary>是否启用新手引导</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>检查并显示当天引导</summary>
        public string CheckDayGuide(int day, GameManager game)
        {
            if (!Enabled) return "";
            if (day == _currentDay) return "";
            _currentDay = day;

            var tips = new List<string>();

            switch (day)
            {
                case 1:
                    tips.Add("══════ 新手引导 ════════");
                    tips.Add(I18n.T("tutorial.welcome"));
                    tips.Add(I18n.T("tutorial.day1"));
                    tips.Add("  提示：白天可以与NPC对话、分配工作、管理建筑");
                    tips.Add("  提示：夜晚可以派NPC外出探索收集资源");
                    tips.Add("════════════════════════");
                    break;

                case 2:
                    if (!HasBuilding(game, "greenhouse"))
                    {
                        tips.Add("💡 " + I18n.T("tutorial.build"));
                    }
                    tips.Add("  提示：记得分配NPC工作，闲着的NPC不会产出资源");
                    break;

                case 3:
                    tips.Add("💡 " + I18n.T("tutorial.explore"));
                    tips.Add("  提示：探索有风险！派战斗能力强的NPC更安全");
                    break;

                case 4:
                    tips.Add("💡 " + I18n.T("tutorial.crisis"));
                    tips.Add("  提示：建造水站和医疗室可以应对危机");
                    break;

                case 5:
                    tips.Add("  提示：查看任务列表，完成主线推进剧情");
                    tips.Add("  提示：派系声望会影响可用的NPC和交易");
                    break;

                case 7:
                    tips.Add("  进阶：装备武器可以提升NPC战斗力");
                    tips.Add("  进阶：心理状态影响NPC行为，关注绝望的NPC");
                    break;

                case 10:
                    tips.Add("  进阶：6种结局由你的选择决定");
                    tips.Add("  进阶：完成主线4 + 存活15天 = 广播结局");
                    break;
            }

            // 情境提示
            if (game.Crises.HasAnyCrisis && ShowOnce("crisis_tip"))
            {
                tips.Add("⚠️ 危机正在发生！打开'危机/心理'面板查看详情");
            }

            if (game.Resources.GetResourceAmount(ResourceType.Food) < 15 && ShowOnce("low_food"))
            {
                tips.Add("⚠️ 食物储备偏低！建造温室或派NPC探索获取食物");
            }

            return tips.Count > 0 ? string.Join("\n", tips) : "";
        }

        /// <summary>获取操作提示（按场景）</summary>
        public string GetContextHint(string context)
        {
            if (!Enabled) return "";

            switch (context)
            {
                case "day_phase":
                    return "选择1-9执行操作，0结束白天";
                case "night_phase":
                    return "选择1开始探索，2进入结算";
                case "exploration":
                    return "S=搜索房间, M=移动, R=返回基地";
                case "combat":
                    return "A=攻击, D=防御, U=使用物品, E=逃跑";
                default:
                    return "";
            }
        }

        private bool HasBuilding(GameManager game, string buildingId)
        {
            var b = game.Buildings.GetBuilding(buildingId);
            return b != null && b.Built;
        }

        private bool ShowOnce(string tipId)
        {
            if (_shownTips.Contains(tipId)) return false;
            _shownTips.Add(tipId);
            return true;
        }
    }
}
