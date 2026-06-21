// ============================================================
// Last Archive - 服务定位器
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 简单服务定位器，用于管理全局系统实例
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// 注册服务
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            _services[type] = service;
        }

        /// <summary>
        /// 获取服务
        /// </summary>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
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
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 清除所有服务
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}
