using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Logger;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 在后台性能验收主UI线程中阻止任何窗口激活，避免WinForms内部停车窗抢占用户前台焦点。
    /// </summary>
    internal sealed class BackgroundWindowActivationGuard : IDisposable
    {
        /// <summary>线程级计算机培训钩子类型。</summary>
        private const int HookTypeComputerBasedTraining = 5;

        /// <summary>窗口即将激活的计算机培训钩子代码。</summary>
        private const int HookCodeActivate = 5;

        /// <summary>保持原生回调委托存活，防止钩子运行期间被垃圾回收。</summary>
        private readonly HookProcedure hookProcedure;

        /// <summary>当前主UI线程安装的钩子句柄；普通启动时为零。</summary>
        private IntPtr hookHandle;

        /// <summary>标记守卫是否已经释放。</summary>
        private bool disposed;

        /// <summary>Windows线程钩子回调签名。</summary>
        /// <param name="code">钩子事件代码。</param>
        /// <param name="wordParameter">事件相关窗口句柄。</param>
        /// <param name="longParameter">事件附加数据。</param>
        /// <returns>非零值阻止激活；其他事件传递给下一个钩子。</returns>
        private delegate IntPtr HookProcedure(int code, IntPtr wordParameter, IntPtr longParameter);

        /// <summary>
        /// 根据当前启动显示模式创建守卫；普通启动不安装钩子。
        /// </summary>
        private BackgroundWindowActivationGuard()
        {
            if (!StartupDisplayMode.IsBackgroundAcceptance)
                return;

            hookProcedure = HandleHook;
            hookHandle = SetWindowsHookEx(
                HookTypeComputerBasedTraining,
                hookProcedure,
                IntPtr.Zero,
                GetCurrentThreadId());
            if (hookHandle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "后台性能验收窗口激活守卫安装失败。");
        }

        /// <summary>
        /// 为当前主UI线程创建窗口激活守卫。
        /// </summary>
        /// <returns>需要在主消息循环退出后释放的守卫。</returns>
        public static BackgroundWindowActivationGuard CreateForCurrentThread()
        {
            return new BackgroundWindowActivationGuard();
        }

        /// <summary>
        /// 拒绝后台验收线程的窗口激活，其余钩子事件继续传递。
        /// </summary>
        /// <param name="code">钩子事件代码。</param>
        /// <param name="wordParameter">事件相关窗口句柄。</param>
        /// <param name="longParameter">事件附加数据。</param>
        /// <returns>激活事件返回一，其他事件返回下一个钩子的结果。</returns>
        private IntPtr HandleHook(int code, IntPtr wordParameter, IntPtr longParameter)
        {
            if (code == HookCodeActivate)
                return new IntPtr(1);

            return CallNextHookEx(hookHandle, code, wordParameter, longParameter);
        }

        /// <summary>解除当前线程钩子；可重复调用。</summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (hookHandle != IntPtr.Zero)
            {
                if (!UnhookWindowsHookEx(hookHandle))
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    try
                    {
                        LogHelper.AddLog(
                            MsgLevel.Warn,
                            $"后台性能验收窗口激活守卫解除失败，Windows错误码：{errorCode}。",
                            true);
                    }
                    catch
                    {
                        // 程序退出阶段日志系统可能已经停止，Windows会在线程退出时回收线程钩子。
                    }
                }

                hookHandle = IntPtr.Zero;
            }
        }

        /// <summary>为当前线程安装Windows钩子。</summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int hookType,
            HookProcedure hookProcedure,
            IntPtr moduleHandle,
            uint threadId);

        /// <summary>将未处理事件传递给钩子链中的下一个处理器。</summary>
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hookHandle,
            int code,
            IntPtr wordParameter,
            IntPtr longParameter);

        /// <summary>解除已经安装的Windows钩子。</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

        /// <summary>读取当前托管主线程对应的Windows线程ID。</summary>
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
    }
}
