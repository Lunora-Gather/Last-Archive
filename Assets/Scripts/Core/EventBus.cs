// ============================================================
// Last Archive - 事件总线
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 全局事件总线，用于系统间解耦通信
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        /// <summary>
        /// 订阅事件
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_subscribers.ContainsKey(type))
            {
                _subscribers[type] = new List<Delegate>();
            }
            _subscribers[type].Add(handler);
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_subscribers.ContainsKey(type))
            {
                _subscribers[type].Remove(handler);
            }
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        public static void Publish<T>(T eventData) where T : struct
        {
            var type = typeof(T);
            if (_subscribers.ContainsKey(type))
            {
                foreach (var handler in _subscribers[type].ToArray())
                {
                    ((Action<T>)handler)(eventData);
                }
            }
        }

        /// <summary>
        /// 清除所有订阅
        /// </summary>
        public static void Clear()
        {
            _subscribers.Clear();
        }
    }

    // ========== 事件定义 ==========

    /// <summary>新的一天开始</summary>
    public struct OnDayStarted { public int Day; }

    /// <summary>夜晚开始</summary>
    public struct OnNightStarted { public int Day; }

    /// <summary>每日结算完成</summary>
    public struct OnDayEnded { public int Day; }

    /// <summary>资源变化</summary>
    public struct OnResourceChanged { public ResourceType Type; public int Amount; public int Total; }

    /// <summary>资源危机</summary>
    public struct OnResourceCrisis { public ResourceType Type; public int Shortage; }

    /// <summary>NPC状态变化</summary>
    public struct OnNPCStatusChanged { public string NPCId; public NPCStatus OldStatus; public NPCStatus NewStatus; }

    /// <summary>NPC记忆添加</summary>
    public struct OnNPCMemoryAdded { public string NPCId; public MemoryEntry Memory; }

    /// <summary>建筑建造完成</summary>
    public struct OnBuildingBuilt { public string BuildingId; }

    /// <summary>建筑升级完成</summary>
    public struct OnBuildingUpgraded { public string BuildingId; public int NewLevel; }

    /// <summary>战斗开始</summary>
    public struct OnCombatStarted { public string MapId; public string RoomId; }

    /// <summary>战斗结束</summary>
    public struct OnCombatEnded { public bool Victory; }

    /// <summary>任务状态变化</summary>
    public struct OnQuestStatusChanged { public string QuestId; public QuestStatus OldStatus; public QuestStatus NewStatus; }

    /// <summary>探索开始</summary>
    public struct OnExplorationStarted { public string MapId; }

    /// <summary>探索结束</summary>
    public struct OnExplorationEnded { public string MapId; public bool Success; }

    /// <summary>游戏结束</summary>
    public struct OnGameOver { public string Reason; }
}
