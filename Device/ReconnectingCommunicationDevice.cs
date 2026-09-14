using System;
using System.Net.Sockets;
using System.Threading;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.Pipe;
using Logger;
using Newtonsoft.Json;

namespace TDJS_Vision.Device
{
    /// <summary>可恢复通信设备的连接意图，区别于当前物理连接状态。</summary>
    public interface IReconnectableCommunicationDevice
    {
        /// <summary>用户要求保持连接时为true，主动断开后为false。</summary>
        bool RestoreConnectionRequested { get; }
    }

    /// <summary>共享HSL通信锁的单设备重连生命周期，不发送探测写入、不补发业务信号。</summary>
    public abstract class ReconnectingCommunicationDevice : IReconnectableCommunicationDevice
    {
        /// <summary>单设备检查间隔，避免一台离线设备阻塞其他设备恢复。</summary>
        private const int CheckIntervalMilliseconds = 1000;
        /// <summary>串行保护手动连接、断开与后台检查。</summary>
        private readonly object _connectionSync = new object();
        /// <summary>连接意图；使用原IsConnect字段保存以兼容旧方案。</summary>
        private int _connectionRequested;
        /// <summary>实际连接状态，不写入方案。</summary>
        private int _connected;
        /// <summary>仅在用户要求保持连接时存在的后台计时器。</summary>
        private Timer _reconnectTimer;
        /// <summary>故障日志节流时间，避免断线期间每秒重复记录。</summary>
        private DateTime _lastFailureLogUtc = DateTime.MinValue;

        /// <summary>实际连接状态；兼容原接口赋值，业务状态更新不改变连接意图。</summary>
        [JsonIgnore]
        public bool IsConnect
        {
            get => Volatile.Read(ref _connected) != 0;
            set
            {
                Volatile.Write(ref _connected, value ? 1 : 0);
                Volatile.Write(ref _connectionRequested, value ? 1 : 0);
            }
        }

        /// <summary>恢复旧方案保存的连接意图，但不伪装成已经建立物理连接。</summary>
        [JsonProperty("IsConnect")]
        private bool SavedConnectionRequested
        {
            get => RestoreConnectionRequested;
            set => Volatile.Write(ref _connectionRequested, value ? 1 : 0);
        }

        /// <inheritdoc />
        [JsonIgnore]
        public bool RestoreConnectionRequested => Volatile.Read(ref _connectionRequested) != 0;

        /// <summary>后台恢复和手动操作共用的状态通知。</summary>
        public event EventHandler<bool> ConnectStatusEvent;

        /// <summary>诊断中显示的设备名称。</summary>
        protected abstract string CommunicationName { get; }
        /// <summary>当前HSL客户端，尚未初始化时允许为空。</summary>
        protected abstract DeviceCommunication CommunicationClient { get; }
        /// <summary>打开或初始化连接；调用方已取得现有客户端的通信锁。</summary>
        protected abstract OperateResult OpenCommunicationCore();
        /// <summary>关闭连接；调用方已取得现有客户端的通信锁。</summary>
        protected abstract void CloseCommunicationCore();

        /// <summary>明确请求连接；首次失败也保留后台重试意图。</summary>
        protected bool ConnectWithRecovery()
        {
            lock (_connectionSync)
            {
                Volatile.Write(ref _connectionRequested, 1);
                if (_reconnectTimer == null)
                    _reconnectTimer = new Timer(CheckConnection, null, CheckIntervalMilliseconds, CheckIntervalMilliseconds);
                bool connected = TryOpenUnderLock(5000);
                SetConnected(IsConnect, true);
                return connected;
            }
        }

        /// <summary>用户主动断开时先撤销恢复意图，再串行关闭物理连接。</summary>
        protected void DisconnectWithRecovery()
        {
            lock (_connectionSync)
            {
                Volatile.Write(ref _connectionRequested, 0);
                _reconnectTimer?.Dispose();
                _reconnectTimer = null;
                ICommunicationLock communicationLock = CommunicationClient?.CommunicationPipe?.CommunicationLock;
                bool entered = communicationLock == null;
                try
                {
                    if (communicationLock != null)
                    {
                        OperateResult enterResult = communicationLock.EnterLock(5000);
                        entered = enterResult.IsSuccess;
                        if (!entered)
                            throw new InvalidOperationException("通信操作尚未结束，无法关闭设备：" + enterResult.Message);
                    }
                    CloseCommunicationCore();
                }
                finally
                {
                    if (entered && communicationLock != null) communicationLock.LeaveLock();
                    SetConnected(false, true);
                }
            }
        }

        /// <summary>非重入检查；设备正在读写时跳过本次，不排队堆积后台任务。</summary>
        private void CheckConnection(object state)
        {
            if (!Monitor.TryEnter(_connectionSync)) return;
            try
            {
                if (!RestoreConnectionRequested) return;
                CommunicationPipe pipe = CommunicationClient?.CommunicationPipe;
                ICommunicationLock communicationLock = pipe?.CommunicationLock;
                if (communicationLock != null && !communicationLock.EnterLock(0).IsSuccess) return;
                try
                {
                    if (IsConnect && !IsDisconnected(pipe)) return;
                    SetConnected(false);
                }
                finally { communicationLock?.LeaveLock(); }
                TryOpenUnderLock(0);
            }
            catch (Exception exception)
            {
                SetConnected(false);
                LogFailure(exception.Message);
            }
            finally { Monitor.Exit(_connectionSync); }
        }

        /// <summary>在设备生命周期和HSL通信锁内完成连接及协议握手，不与任何读写交叉。</summary>
        private bool TryOpenUnderLock(int lockTimeoutMilliseconds)
        {
            ICommunicationLock communicationLock = CommunicationClient?.CommunicationPipe?.CommunicationLock;
            bool entered = communicationLock == null;
            try
            {
                if (communicationLock != null)
                {
                    entered = communicationLock.EnterLock(lockTimeoutMilliseconds).IsSuccess;
                    if (!entered) return IsConnect;
                }
                OperateResult result = OpenCommunicationCore();
                bool connected = result != null && result.IsSuccess && !IsDisconnected(CommunicationClient?.CommunicationPipe);
                bool wasConnected = IsConnect;
                if (connected) ConfigureTcpKeepAlive();
                SetConnected(connected);
                if (!connected) LogFailure(result?.Message ?? "连接API返回空结果。");
                else if (!wasConnected)
                {
                    _lastFailureLogUtc = DateTime.MinValue;
                    LogHelper.AddLog(MsgLevel.Info, $"通信设备【{CommunicationName}】连接已恢复，不自动补发历史结果。", true);
                }
                return connected;
            }
            catch (Exception exception)
            {
                SetConnected(false);
                LogFailure(exception.Message);
                return false;
            }
            finally { if (entered && communicationLock != null) communicationLock.LeaveLock(); }
        }

        /// <summary>只检查传输层状态，不读取任意业务地址，更不写寄存器进行心跳。</summary>
        private static bool IsDisconnected(CommunicationPipe pipe)
        {
            if (pipe == null || pipe.IsConnectError()) return true;
            if (pipe is PipeTcpNet tcp)
            {
                Socket socket = tcp.Socket;
                return socket == null || !socket.Connected ||
                    (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
            }
            if (pipe is PipeSerialPort serial) return !serial.IsOpen();
            return false;
        }

        /// <summary>配置Windows TCP保活，空闲断网时由传输层探测，不占用业务寄存器。</summary>
        private void ConfigureTcpKeepAlive()
        {
            if (!(CommunicationClient?.CommunicationPipe is PipeTcpNet tcp) || tcp.Socket == null) return;
            try
            {
                // Windows SIO_KEEPALIVE_VALS依次为启用、空闲毫秒数、探测间隔毫秒数。
                byte[] options = new byte[12];
                Buffer.BlockCopy(BitConverter.GetBytes(1U), 0, options, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(3000U), 0, options, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(1000U), 0, options, 8, 4);
                tcp.Socket.IOControl(IOControlCode.KeepAliveValues, options, null);
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"通信设备【{CommunicationName}】TCP保活设置失败，继续使用系统保活和收发超时：{exception.Message}", true);
            }
        }

        /// <summary>仅在状态变化时通知界面，订阅异常不破坏重连生命周期。</summary>
        private void SetConnected(bool connected, bool forceNotification = false)
        {
            bool unchanged = Interlocked.Exchange(ref _connected, connected ? 1 : 0) == (connected ? 1 : 0);
            if (unchanged && !forceNotification) return;
            var handlers = ConnectStatusEvent;
            if (handlers == null) return;
            // 不在持有HSL通信锁的线程上调用外部订阅，避免订阅读写设备时锁反转。
            ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (EventHandler<bool> handler in handlers.GetInvocationList())
                {
                    try { handler(this, IsConnect); }
                    catch (Exception exception)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"通信设备【{CommunicationName}】连接状态通知失败：{exception.Message}", true);
                    }
                }
            });
        }

        /// <summary>持续离线时最多每30秒报警一次，方案不因重连请求停止。</summary>
        private void LogFailure(string message)
        {
            DateTime now = DateTime.UtcNow;
            if (now - _lastFailureLogUtc < TimeSpan.FromSeconds(30)) return;
            _lastFailureLogUtc = now;
            LogHelper.AddLog(MsgLevel.Warn, $"通信设备【{CommunicationName}】连接不可用，将自动重试：{message}", true);
        }
    }
}
