using System;
using System.Collections.Generic;
using System.Threading;

namespace TDJS_Vision
{
    /// <summary>
    /// 线程安全的共享变量类
    /// </summary>
    public class SharedVariable : Dictionary<string, object>
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// 获取所有共享变量名称
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetNames()
        {
            try
            {
                _lock.EnterReadLock();
                return new List<string>(Keys);
            }
            finally
            {
                if (_lock.IsReadLockHeld)
                    _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 通过名称获取对应值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public T GetValue<T>(string name)
        {
            try
            {
                _lock.EnterReadLock();
                if (TryGetValue(name, out object value))
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                    throw new InvalidCastException($"共享变量名称为 “{name}”的值不能转化为类型“{typeof(T).Name}”！");
                }
                throw new KeyNotFoundException($"找不到名为“{name}”的共享变量！");
            }
            finally
            {
                if (_lock.IsReadLockHeld)
                    _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 通过名称获取对应值，返回 object 类型
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public object GetValue(string name)
        {
            try
            {
                _lock.EnterReadLock();
                if (TryGetValue(name, out object value))
                {
                    return value;
                }
                throw new KeyNotFoundException($"找不到名为“{name}”的共享变量！");
            }
            finally
            {
                if (_lock.IsReadLockHeld)
                    _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 通过名称设置对应值
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetValue(string name, object value)
        {
            try
            {
                _lock.EnterWriteLock();
                this[name] = value;
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 检查是否存在指定名称的变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool Exist(string name)
        {
            try
            {
                _lock.EnterReadLock();
                return ContainsKey(name);
            }
            finally
            {
                if (_lock.IsReadLockHeld)
                    _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 删除指定名称的变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool RemoveOne(string name)
        {
            try
            {
                _lock.EnterWriteLock();
                return Remove(name);
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 清空所有变量
        /// </summary>
        public void ClearAll()
        {
            try
            {
                _lock.EnterWriteLock();
                Clear();
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
        }
    }
}