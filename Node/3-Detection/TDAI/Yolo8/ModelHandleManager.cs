using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    #region 使用示例

    //IntPtr detectorHandle = ...;

    //ModelHandleManager.Register(detectorHandle, ModelType.DET);

    //// 后续释放时只需传入句柄即可
    //ModelHandleManager.AutoRelease(detectorHandle);

    #endregion

    /// <summary>
    /// ModelHandleManager 类用于管理模型句柄的生命周期和资源释放
    /// </summary>
    internal static class ModelHandleManager
    {
        private static Dictionary<IntPtr, ModelType> _registry = new Dictionary<IntPtr, ModelType>();

        /// <summary>
        /// 注册模型句柄
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="type"></param>
        public static void Register(IntPtr handle, ModelType type)
        {
            _registry[handle] = type;
        }

        /// <summary>
        /// 释放特定模型
        /// </summary>
        /// <param name="handle"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void ReleaseModel(IntPtr handle)
        {
            if (_registry.TryGetValue(handle, out var type))
            {
                ReleaseResource(handle, type);
                _registry.Remove(handle);
            }
            else
            {
                throw new InvalidOperationException("未知句柄，无法释放");
            }
        }
        /// <summary>
        /// 释放所有模型句柄
        /// </summary>
        public static void ReleaseAllModel()
        {
            foreach (var kvp in _registry)
            {
                ReleaseResource(kvp.Key, kvp.Value);
            }
        }
        /// <summary>
        /// 获取模型种类
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static ModelType GetModelHandleType(IntPtr handle)
        {
            if (_registry.TryGetValue(handle, out var type))
            {
                return type;
            }
            throw new InvalidOperationException("未知句柄，无法获取类型");
        }

        /// <summary>
        /// 具体释放逻辑
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="type"></param>
        /// <exception cref="ArgumentException"></exception>
        private static void ReleaseResource(IntPtr handle, ModelType type)
        {
            switch (type)
            {
                case ModelType.DET:
                    YoloV8Det.DestroyDetector(handle);
                    break;
                case ModelType.SEG:
                    Yolo8Seg.Release(handle);
                    break;
                case ModelType.POSE:
                    Yolo8Pose.Cleanup(handle);
                    break;
                default:
                    throw new ArgumentException("不支持的资源类型");
            }
        }
    }
}
