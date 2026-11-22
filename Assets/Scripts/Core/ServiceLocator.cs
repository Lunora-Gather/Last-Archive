// ============================================================
// Last Archive - 服务定位器
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 当前游戏的服务定位器
    /// </summary>
    public static class ServiceLocator
    {
        private static Dictionary<Type, object> Services => GameScope.Current.Services;

        /// <summary>
        /// 注册服务
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            Services[type] = service;
        }

        /// <summary>
        /// 获取服务
        /// </summary>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            if (Services.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            return null;
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public static bool Has<T>() where T : class
        {
            return Services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 清除所有服务
        /// </summary>
        public static void Clear()
        {
            Services.Clear();
        }
    }
}
