using System;
using System.Collections.Concurrent;


namespace TDJS_Vision.Forms.MyCSharpScript
{
    /// <summary>
    /// 线程安全的脚本全局变量管理类
    /// </summary>
    public class ScriptGlobalVariable
    {
        private static readonly ConcurrentDictionary<string, object> Data
            = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// 获取指定名称的变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量值，不存在返回 null</returns>
        public static object GetValue(string name)
        {
            return Data.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// 获取指定名称的变量值并转换为指定类型
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量值，不存在返回 null</returns>
        public static T GetValue<T>(string name, T defaultValue = default)
        {
            return Data.TryGetValue(name, out var value) && value is T t ? t : defaultValue;
        }

        /// <summary>
        /// 设置或更新指定名称的变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public static void SetValue(string name, object value)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("变量名不能为空", nameof(name));

            Data[name] = value; // ConcurrentDictionary 支持线程安全的赋值
        }

        /// <summary>
        /// 检查是否存在指定名称的变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>存在返回 true，否则 false</returns>
        public static bool Contains(string name)
        {
            return Data.ContainsKey(name);
        }

        /// <summary>
        /// 移除某个变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>是否成功移除</returns>
        public static bool Remove(string name)
        {
            return Data.TryRemove(name, out _);
        }
    }
}
