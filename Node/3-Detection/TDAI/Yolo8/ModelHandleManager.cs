using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// ModelHandleManager 类用于管理模型句柄的生命周期和资源释放
    /// </summary>
    internal static class ModelHandleManager
    {
        /// <summary>
        /// 模型句柄注册表。
        /// </summary>
        private static readonly List<IYolo8> _registry = new List<IYolo8>();
        /// <summary>
        /// 注册表并发访问锁。
        /// </summary>
        private static readonly object _registryLock = new object();

        /// <summary>
        /// 注册模型句柄
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="type"></param>
        public static void Add(IYolo8 handle)
        {
            if (handle == null)
                return;

            lock (_registryLock)
            {
                if (!_registry.Contains(handle))
                    _registry.Add(handle);
            }
        }

        /// <summary>
        /// 释放特定模型
        /// </summary>
        /// <param name="handle"></param>
        public static void Destroy(IYolo8 handle)
        {
            if (handle == null)
                return;

            lock (_registryLock)
                _registry.Remove(handle);
            handle.Destroy();
        }
        /// <summary>
        /// 释放所有模型句柄
        /// </summary>
        public static void DestroyAllModel()
        {
            IYolo8[] handles;
            lock (_registryLock)
            {
                handles = _registry.ToArray();
                _registry.Clear();
            }

            foreach (var handle in handles)
            {
                handle.Destroy();
            }
        }
    }
}
