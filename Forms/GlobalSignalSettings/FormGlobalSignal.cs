using Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Forms.Common;

namespace TDJS_Vision.Forms.GlobalSignalSettings
{
    public partial class FormGlobalSignal : Form
    {
        private const int ListenInterval = 1;
        /// <summary>
        /// 心跳间隔未填写或填写非法时使用的默认毫秒数。
        /// </summary>
        private const int DefaultHeartbeatIntervalMilliseconds = 1000;
        /// <summary>
        /// 心跳间隔允许的最小毫秒数，避免配置为0造成后台线程空转。
        /// </summary>
        private const int MinimumHeartbeatIntervalMilliseconds = 1;
        private static CancellationTokenSource _listenSignalCts;
        /// <summary>
        /// 心跳信号后台发送任务的取消控制器，方案切换或配置变更时会先取消旧任务。
        /// </summary>
        private static CancellationTokenSource _heartbeatSignalCts;
        /// <summary>
        /// 监听信号状态锁，避免多个监听线程同时写入 SignalValue 和 SignalTriggered。
        /// </summary>
        private static readonly object _listenSignalStateLock = new object();
        private bool _isLoadingConfig;

        public FormGlobalSignal()
        {
            InitializeComponent();
            WindowIconProvider.Apply(this);
            Shown += FormGlobalSignal_Shown;
            dataGridView1.Leave += DataGridView1_Leave;
            dataGridView2.Leave += DataGridView2_Leave;
            dataGridView3.Leave += DataGridView3_Leave;
            dataGridView3.CellClick += dataGridView1_CellClick;
            dataGridView3.CellEndEdit += DataGridView3_CellEndEdit;
            dataGridView3.CellValueChanged += DataGridView3_CellValueChanged;
            dataGridView3.CurrentCellDirtyStateChanged += DataGridView3_CurrentCellDirtyStateChanged;
            dataGridView3.RowsRemoved += DataGridView3_RowsRemoved;
            dataGridView4.Leave += DataGridView4_Leave;
            dataGridView4.CellClick += dataGridView1_CellClick;
            dataGridView4.CellEndEdit += DataGridView4_CellEndEdit;
            dataGridView4.CellValueChanged += DataGridView4_CellValueChanged;
            dataGridView4.CurrentCellDirtyStateChanged += DataGridView4_CurrentCellDirtyStateChanged;
            dataGridView4.RowsRemoved += DataGridView4_RowsRemoved;
            dataGridView1.DataError += DataGridView_DataError;
            dataGridView2.DataError += DataGridView_DataError;
            dataGridView3.DataError += DataGridView_DataError;
            dataGridView4.DataError += DataGridView_DataError;
        }
        /// <summary>
        /// 点击删除按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {

                if (sender is DataGridView view && view == dataGridView1)
                {
                    // 检查点击是否发生在 deleteColumn1 列
                    if (e.ColumnIndex == dataGridView1.Columns["deleteColumn1"].Index && e.RowIndex >= 0)
                    {
                        // 获取被点击的行
                        DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
                        if (row.IsNewRow) return;
                        // 删除当前行
                        dataGridView1.Rows.Remove(row);
                    }
                }
                else if (sender is DataGridView view2 && view2 == dataGridView2)
                {
                    // 检查点击是否发生在 deleteColumn2 列
                    if (e.ColumnIndex == dataGridView2.Columns["deleteColumn2"].Index && e.RowIndex >= 0)
                    {
                        // 获取被点击的行
                        DataGridViewRow row = dataGridView2.Rows[e.RowIndex];
                        if (row.IsNewRow) return;
                        // 删除当前行
                        dataGridView2.Rows.Remove(row);
                    }
                }
                else if (sender is DataGridView view3 && view3 == dataGridView3)
                {
                    // 检查点击是否发生在 dataGridViewButtonColumn1 列
                    if (e.ColumnIndex == dataGridView3.Columns["dataGridViewButtonColumn1"].Index && e.RowIndex >= 0)
                    {
                        // 获取被点击的行
                        DataGridViewRow row = dataGridView3.Rows[e.RowIndex];
                        if (row.IsNewRow) return;
                        // 删除当前行
                        dataGridView3.Rows.Remove(row);
                    }
                }
                else if (sender is DataGridView view4 && view4 == dataGridView4)
                {
                    // 检查点击是否发生在 heartbeatDeleteColumn 列
                    if (e.ColumnIndex == dataGridView4.Columns["heartbeatDeleteColumn"].Index && e.RowIndex >= 0)
                    {
                        // 获取被点击的行
                        DataGridViewRow row = dataGridView4.Rows[e.RowIndex];
                        if (row.IsNewRow) return;
                        // 删除当前行
                        dataGridView4.Rows.Remove(row);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"删除信号失败！{ex.Message}", true);
            }
        }
        /// <summary>
        /// 根据运行状态改变，给PLC发送信号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        public static async Task SendGlobalSignals(object sender, bool e)
        {
            await Task.Run(async () =>
            {
                string txt = e ? "准备" : "离线";
                try
                {
                    List<SingleGlobalSignalSettings> signals;
                    if (e && (Solution.Instance.GlobalSignal == null || Solution.Instance.GlobalSignal.ReadySignals == null || Solution.Instance.GlobalSignal.ReadySignals.Count == 0))
                        return;
                    if (!e && (Solution.Instance.GlobalSignal == null || Solution.Instance.GlobalSignal.StopSignals == null || Solution.Instance.GlobalSignal.StopSignals.Count == 0))
                        return;
                    if (e)
                    {
                        signals = Solution.Instance.GlobalSignal.ReadySignals;
                    }
                    else
                    {
                        signals = Solution.Instance.GlobalSignal.StopSignals;
                    }
                    foreach (var signal in signals)
                    {
                        if (!signal.Enable || string.IsNullOrEmpty(signal.DeviceName) || string.IsNullOrEmpty(signal.Address))
                            continue;
                        var device = Solution.Instance.AllDevices.Find(r => r.DevName == signal.DeviceName);
                        if (device == default(IDevice))
                            continue;
                        else
                        {
                            if (device is IPlc plc)
                            {
                                if(!plc.IsConnect)
                                {
                                    LogHelper.AddLog(MsgLevel.Exception, $"设备({signal.DeviceName})未连接，视觉{txt}信号发送失败！", true);
                                    continue;
                                }
                                switch (signal.Type)
                                {
                                    case "布尔类型":
                                        await plc.WriteBoolAsync(signal.Address, new bool[] { signal.Value.Equals("True", StringComparison.OrdinalIgnoreCase) || signal.Value != "0" });
                                        break;
                                    case "整数类型":
                                        await plc.WriteIntAsync(signal.Address, int.Parse(signal.Value));
                                        break;
                                    case "字符串类型":
                                        await plc.WriteStringAsync(signal.Address, signal.Value);
                                        break;
                                    default:
                                        LogHelper.AddLog(MsgLevel.Exception, $"不支持的信号类型！", true);
                                        break;
                                }
                            }
                            else if (device is IModbus modbus)
                            {
                                if (!modbus.IsConnect)
                                {
                                    LogHelper.AddLog(MsgLevel.Exception, $"设备({signal.DeviceName})未连接，视觉{txt}信号发送失败！", true);
                                    continue;
                                }
                                switch (signal.Type)
                                {
                                    case "布尔类型":
                                        modbus.Write(signal.Address, signal.Value.Equals("True", StringComparison.OrdinalIgnoreCase) || signal.Value != "0");
                                        break;
                                    case "整数类型":
                                        modbus.Write(signal.Address, ushort.Parse(signal.Value));
                                        break;
                                    case "字符串类型":
                                        modbus.Write(signal.Address, StringToHoldingRegisters(signal.Value));
                                        break;
                                    default:
                                        LogHelper.AddLog(MsgLevel.Exception, $"不支持的信号类型！", true);
                                        break;
                                }
                            }
                            else
                            {
                                LogHelper.AddLog(MsgLevel.Exception, $"设备({signal.DeviceName})不支持全局信号功能！", true);
                                continue;
                            }
                        }
                    }
                    LogHelper.AddLog(MsgLevel.Info, $"视觉{txt}信号已发送！", true);
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"视觉{txt}信号发送异常！原因:{ex.Message}", true);
                }
            });
        }

        /// <summary>
        /// 加载完方案后触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private async void FormNewProcessWizard_OnLoadFinished(object sender, EventArgs e)
        {
            await Task.Run(async() =>
            {
                try
                {
                    if (Solution.Instance.GlobalSignal == null || Solution.Instance.GlobalSignal.ReadySignals == null)
                        return;
                    foreach (var signal in Solution.Instance.GlobalSignal.ReadySignals)
                    {
                        if (string.IsNullOrEmpty(signal.DeviceName) || string.IsNullOrEmpty(signal.Address))
                            continue;
                        var device = Solution.Instance.AllDevices.Find(r => r.DevName == signal.DeviceName);
                        if (device == default(IDevice))
                            continue;
                        else
                        {
                            if (device is IPlc plc)
                            {
                                if (!plc.IsConnect)
                                {
                                    LogHelper.AddLog(MsgLevel.Exception, $"设备({signal.DeviceName})未连接，视觉准备信号发送失败！", true);
                                    continue;
                                }
                                switch (signal.Type)
                                {
                                    case "布尔类型":
                                        await plc.WriteBoolAsync(signal.Address, signal.Value.Equals("True", StringComparison.OrdinalIgnoreCase) || signal.Value != "0");
                                        break;
                                    case "整数类型":
                                        await plc.WriteIntAsync(signal.Address, int.Parse(signal.Value));
                                        break;
                                    case "字符串类型":
                                        await plc.WriteStringAsync(signal.Address, signal.Value);
                                        break;
                                    default:
                                        LogHelper.AddLog(MsgLevel.Exception, $"不支持的信号类型！", true);
                                        break;
                                }
                            }
                            else if (device is IModbus modbus)
                            {
                                if (!modbus.IsConnect)
                                {
                                    LogHelper.AddLog(MsgLevel.Exception, $"设备({signal.DeviceName})未连接，视觉准备信号发送失败！", true);
                                    continue;
                                }
                                switch (signal.Type)
                                {
                                    case "布尔类型":
                                        modbus.Write(signal.Address, signal.Value.Equals("True", StringComparison.OrdinalIgnoreCase) || signal.Value != "0");
                                        break;
                                    case "整数类型":
                                        modbus.Write(signal.Address, int.Parse(signal.Value));
                                        break;
                                    case "字符串类型":
                                        modbus.Write(signal.Address, StringToHoldingRegisters(signal.Value));
                                        break;
                                    default:
                                        LogHelper.AddLog(MsgLevel.Exception, $"不支持的信号类型！", true);
                                        break;
                                }
                            }
                            else
                            {
                                LogHelper.AddLog(MsgLevel.Exception, $"设备({signal.DeviceName})不支持全局信号功能！", true);
                                continue;
                            }
                        }
                    }
                    LogHelper.AddLog(MsgLevel.Info, "视觉准备信号已发送！", true);
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"准备信号发送异常！原因:{ex.Message}", true);
                }
            });
        }
        public static ushort[] StringToHoldingRegisters(string input)
        {
            // 确保输入不为空
            if (string.IsNullOrEmpty(input))
                return new ushort[0];

            // 将字符串转换为字节数组，这里使用 ASCII 编码
            byte[] bytes = Encoding.ASCII.GetBytes(input);

            // 计算所需的寄存器数量，向上取整以确保有足够的空间存放最后一个字符（如果字符串长度为奇数）
            int registerCount = (bytes.Length + 1) / 2;

            // 创建目标数组
            ushort[] registers = new ushort[registerCount];

            for (int i = 0; i < bytes.Length; i += 2)
            {
                // 第一个字节
                byte highByte = bytes[i];
                // 如果存在第二个字节
                byte lowByte = (i + 1) < bytes.Length ? bytes[i + 1] : (byte)0;

                // 合并两个字节成为一个 16 位整数，并添加到结果数组中
                registers[i / 2] = (ushort)((highByte << 8) | lowByte);
            }

            return registers;
        }

        /// <summary>
        /// 监听信号线程控制。
        /// </summary>
        /// <summary>
        /// 为 dataGridView3 中配置的每一条监听信号启动一个独立线程。
        /// </summary>
        public static void StartListenSignals()
        {
            StopListenSignals();

            var signals = Solution.Instance.GlobalSignal?.ListenSignals;
            if (signals == null || signals.Count == 0)
                return;

            _listenSignalCts = new CancellationTokenSource();
            foreach (var signal in signals)
            {
                if (signal == null || !signal.Enable || string.IsNullOrEmpty(signal.DeviceName) || string.IsNullOrEmpty(signal.Address))
                    continue;

                signal.SignalValue = false;
                signal.SignalTriggered = false;

                var token = _listenSignalCts.Token;
                Thread thread = new Thread(() => ListenSignalLoop(signal, token));
                thread.IsBackground = true;
                thread.Start();
            }
        }

        /// <summary>
        /// 停止所有监听信号线程。
        /// </summary>
        public static void StopListenSignals()
        {
            if (_listenSignalCts == null)
                return;

            _listenSignalCts.Cancel();
            _listenSignalCts = null;
        }

        /// <summary>
        /// 为当前方案中所有有效心跳配置启动后台发送任务。
        /// </summary>
        public static void StartHeartbeatSignals()
        {
            StopHeartbeatSignals();

            var signals = Solution.Instance.GlobalSignal?.HeartbeatSignals;
            if (signals == null || signals.Count == 0)
                return;

            _heartbeatSignalCts = new CancellationTokenSource();
            foreach (var signal in signals)
            {
                if (!IsValidHeartbeatSignal(signal))
                    continue;

                CancellationToken token = _heartbeatSignalCts.Token;
                Task.Run(() => HeartbeatSignalLoopAsync(signal, token), token);
            }
        }

        /// <summary>
        /// 停止所有心跳信号后台发送任务。
        /// </summary>
        public static void StopHeartbeatSignals()
        {
            CancellationTokenSource cts = _heartbeatSignalCts;
            if (cts == null)
                return;

            _heartbeatSignalCts = null;
            cts.Cancel();
        }

        /// <summary>
        /// 判断心跳配置是否具备启动后台发送的必要字段。
        /// </summary>
        /// <param name="signal">需要检查的心跳配置。</param>
        /// <returns>字段完整且启用时返回 true。</returns>
        private static bool IsValidHeartbeatSignal(HeartbeatGlobalSignalSettings signal)
        {
            return signal != null
                && signal.Enable
                && !string.IsNullOrWhiteSpace(signal.CameraName)
                && !string.IsNullOrWhiteSpace(signal.DeviceName)
                && !string.IsNullOrWhiteSpace(signal.Address)
                && !string.IsNullOrWhiteSpace(signal.Type)
                && !string.IsNullOrWhiteSpace(signal.OnValue)
                && !string.IsNullOrWhiteSpace(signal.OffValue);
        }

        /// <summary>
        /// 按间隔时间循环写入心跳 On 值和 Off 值。
        /// </summary>
        /// <param name="signal">心跳配置。</param>
        /// <param name="token">后台任务取消令牌。</param>
        /// <returns>心跳循环任务。</returns>
        private static async Task HeartbeatSignalLoopAsync(HeartbeatGlobalSignalSettings signal, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                int intervalMilliseconds = NormalizeHeartbeatInterval(signal.IntervalMilliseconds);
                try
                {
                    if (!IsHeartbeatCameraReady(signal))
                    {
                        await Task.Delay(intervalMilliseconds, token);
                        continue;
                    }

                    await WriteHeartbeatValueAsync(signal, signal.OnValue, token);
                    await Task.Delay(intervalMilliseconds, token);
                    if (!IsHeartbeatCameraReady(signal))
                        continue;

                    await WriteHeartbeatValueAsync(signal, signal.OffValue, token);
                    await Task.Delay(intervalMilliseconds, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"心跳信号发送异常！相机:{signal.CameraName} 设备:{signal.DeviceName} 地址:{signal.Address} 原因:{ex.Message}", true);
                    try
                    {
                        await Task.Delay(intervalMilliseconds, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 标准化心跳间隔，非法值使用默认值，过小值提升到最小值。
        /// </summary>
        /// <param name="intervalMilliseconds">配置的毫秒间隔。</param>
        /// <returns>可安全用于 Task.Delay 的毫秒间隔。</returns>
        private static int NormalizeHeartbeatInterval(int intervalMilliseconds)
        {
            if (intervalMilliseconds <= 0)
                return DefaultHeartbeatIntervalMilliseconds;

            return Math.Max(MinimumHeartbeatIntervalMilliseconds, intervalMilliseconds);
        }

        /// <summary>
        /// 确认心跳绑定的相机仍存在并处于连接状态。
        /// </summary>
        /// <param name="signal">心跳配置。</param>
        /// <returns>相机存在且已连接时返回 true。</returns>
        private static bool IsHeartbeatCameraReady(HeartbeatGlobalSignalSettings signal)
        {
            var camera = FindDeviceByName(signal.CameraName) as ICamera;
            return camera != null && camera.IsOpen;
        }

        /// <summary>
        /// 按心跳配置的显式写入类型写入通信设备。
        /// </summary>
        /// <param name="signal">心跳配置。</param>
        /// <param name="value">本次要写入的 On 或 Off 值。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>写入完成任务。</returns>
        private static async Task WriteHeartbeatValueAsync(HeartbeatGlobalSignalSettings signal, string value, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var device = FindDeviceByName(signal.DeviceName);
            if (device == null)
                return;

            if (device is IPlc plc)
            {
                if (!plc.IsConnect)
                    return;

                switch (NormalizeHeartbeatType(signal.Type))
                {
                    case "布尔":
                        await plc.WriteBoolAsync(signal.Address, new bool[] { ParseHeartbeatBoolValue(value) });
                        break;
                    case "Int16":
                        await WriteHeartbeatTypedPlcValueAsync(plc, signal.Address, new short[] { short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) });
                        break;
                    case "UInt16":
                        await WriteHeartbeatTypedPlcValueAsync(plc, signal.Address, new ushort[] { ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) });
                        break;
                    case "Int32":
                        await plc.WriteIntAsync(signal.Address, int.Parse(value));
                        break;
                    case "UInt32":
                        await WriteHeartbeatTypedPlcValueAsync(plc, signal.Address, new uint[] { uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) });
                        break;
                    case "浮点":
                        await plc.WriteFloatAsync(signal.Address, float.Parse(value));
                        break;
                    case "字符串":
                        await plc.WriteStringAsync(signal.Address, value);
                        break;
                    default:
                        LogHelper.AddLog(MsgLevel.Exception, $"PLC设备不支持心跳写入类型：{signal.Type}，设备:{signal.DeviceName} 地址:{signal.Address}", true);
                        break;
                }
                return;
            }

            if (device is IModbus modbus)
            {
                if (!modbus.IsConnect)
                    return;

                switch (NormalizeHeartbeatType(signal.Type))
                {
                    case "布尔":
                        modbus.Write(signal.Address, ParseHeartbeatBoolValue(value));
                        break;
                    case "Int16":
                        modbus.Write(signal.Address, short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
                        break;
                    case "UInt16":
                        modbus.Write(signal.Address, ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
                        break;
                    case "Int32":
                        modbus.Write(signal.Address, int.Parse(value));
                        break;
                    case "UInt32":
                        modbus.Write(signal.Address, uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
                        break;
                    case "浮点":
                        modbus.Write(signal.Address, float.Parse(value));
                        break;
                    case "字符串":
                        modbus.Write(signal.Address, StringToHoldingRegisters(value));
                        break;
                    default:
                        LogHelper.AddLog(MsgLevel.Exception, $"Modbus设备不支持心跳写入类型：{signal.Type}，设备:{signal.DeviceName} 地址:{signal.Address}", true);
                        break;
                }
            }
        }

        /// <summary>
        /// 按明确位宽写入PLC心跳整数，避免16位值被普通Int32写入接口占用两个字。
        /// </summary>
        /// <param name="plc">目标PLC设备。</param>
        /// <param name="address">写入地址。</param>
        /// <param name="values">已经按目标位宽解析好的写入值。</param>
        /// <returns>PLC写入任务。</returns>
        private static Task WriteHeartbeatTypedPlcValueAsync(IPlc plc, string address, Array values)
        {
            if (!(plc is IResultSendTypedPlc typedPlc))
                throw new NotSupportedException("当前PLC设备不支持Int16、UInt16或UInt32心跳写入。");

            return typedPlc.WriteTypedValuesAsync(address, values);
        }

        /// <summary>
        /// 支持用户用常见文本配置布尔心跳值。
        /// </summary>
        /// <param name="value">用户填写的值。</param>
        /// <param name="result">解析出的布尔值。</param>
        /// <returns>可识别为布尔值时返回 true。</returns>
        private static bool TryParseHeartbeatBool(string value, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalizedValue = value.Trim();
            if (normalizedValue.Equals("True", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.Equals("On", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.Equals("1", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.Equals("高", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (normalizedValue.Equals("False", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.Equals("Off", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.Equals("0", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.Equals("低", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 将旧方案保存的心跳写入类型文本归一为当前PLC写入节点一致的显示名称。
        /// </summary>
        /// <param name="type">方案或界面中的心跳写入类型。</param>
        /// <returns>统一后的写入类型名称。</returns>
        private static string NormalizeHeartbeatType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return "布尔";

            switch (type.Trim())
            {
                case "布尔类型":
                    return "布尔";
                case "整数":
                case "整数类型":
                    return "Int32";
                case "short类型":
                    return "Int16";
                case "ushort类型":
                    return "UInt16";
                case "浮点类型":
                    return "浮点";
                case "字符串类型":
                    return "字符串";
                default:
                    return type.Trim();
            }
        }

        /// <summary>
        /// 解析心跳布尔值，非法文本抛出异常并交由心跳循环记录日志。
        /// </summary>
        /// <param name="value">用户填写的心跳值。</param>
        /// <returns>解析后的布尔值。</returns>
        private static bool ParseHeartbeatBoolValue(string value)
        {
            if (TryParseHeartbeatBool(value, out bool result))
                return result;

            throw new FormatException($"心跳布尔值无效：{value}");
        }

        /// <summary>
        /// 按设备名或用户自定义名查找当前方案设备，提高旧配置和界面显示名的兼容性。
        /// </summary>
        /// <param name="deviceName">设备名。</param>
        /// <returns>找到的设备，未找到返回 null。</returns>
        private static IDevice FindDeviceByName(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return null;

            return Solution.Instance.AllDevices.Find(device =>
                string.Equals(device.DevName, deviceName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(device.UserDefinedName, deviceName, StringComparison.OrdinalIgnoreCase));
        }

        private static void ListenSignalLoop(SingleGlobalSignalSettings signal, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool isMatched = false;
                try
                {
                    isMatched = ReadSignalMatched(signal);
                }
                catch
                {
                    isMatched = false;
                }

                if (isMatched)
                {
                    bool shouldLog = false;
                    lock (_listenSignalStateLock)
                    {
                        if (!signal.SignalTriggered)
                        {
                            ClearSameSourceOtherSignalValues(signal);
                            signal.SignalValue = true;
                            signal.SignalTriggered = true;
                            shouldLog = true;
                        }
                    }

                    if (shouldLog)
                        LogHelper.AddLog(MsgLevel.Exception, $"地址:{signal.Address}监听到了:{signal.Value}", true);
                }
                else
                {
                    lock (_listenSignalStateLock)
                    {
                        signal.SignalTriggered = false;
                    }
                }

                Thread.Sleep(ListenInterval);
            }
        }

        /// <summary>
        /// 清除同一信号源下其他监听值的锁存状态，避免同一地址的不同值同时为 true。
        /// </summary>
        /// <param name="triggeredSignal">当前触发的监听信号。</param>
        private static void ClearSameSourceOtherSignalValues(SingleGlobalSignalSettings triggeredSignal)
        {
            var signals = Solution.Instance.GlobalSignal?.ListenSignals;
            if (signals == null)
                return;

            foreach (var signal in signals)
            {
                if (signal == null || ReferenceEquals(signal, triggeredSignal))
                    continue;

                if (!IsSameSignalSource(signal, triggeredSignal))
                    continue;

                if (string.Equals(signal.Value, triggeredSignal.Value, StringComparison.OrdinalIgnoreCase))
                    continue;

                signal.SignalValue = false;
                signal.SignalTriggered = false;
            }
        }

        /// <summary>
        /// 判断两条监听信号是否来自同一个通信设备、地址和数据类型。
        /// </summary>
        /// <param name="left">第一条监听信号。</param>
        /// <param name="right">第二条监听信号。</param>
        /// <returns>同一信号源返回 true，否则返回 false。</returns>
        private static bool IsSameSignalSource(SingleGlobalSignalSettings left, SingleGlobalSignalSettings right)
        {
            return string.Equals(left.DeviceName, right.DeviceName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Address, right.Address, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Type, right.Type, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ReadSignalMatched(SingleGlobalSignalSettings signal)
        {
            var device = Solution.Instance.AllDevices.Find(r => r.DevName == signal.DeviceName);
            if (device == null)
                return false;

            if (device is IPlc plc)
            {
                if (!plc.IsConnect)
                    return false;

                switch (signal.Type)
                {
                    case "布尔类型":
                        return plc.ReadBool(signal.Address).Content == ParseBoolValue(signal.Value);
                    case "整数类型":
                        return plc.ReadInt32(signal.Address).Content == int.Parse(signal.Value);
                    case "字符串类型":
                        return plc.ReadString(signal.Address, (ushort)Math.Max(1, signal.Value.Length)).Content == signal.Value;
                    case "short类型":
                        return plc.ReadInt16(signal.Address).Content == int.Parse(signal.Value);
                    default:
                        return false;
                }
            }

            if (device is IModbus modbus)
            {
                if (!modbus.IsConnect)
                    return false;

                switch (signal.Type)
                {
                    case "布尔类型":
                        bool[] boolValues = modbus.ReadBool(signal.Address, 1);
                        return boolValues != null && boolValues.Length > 0 && boolValues[0] == ParseBoolValue(signal.Value);
                    case "整数类型":
                        int[] intValues = modbus.ReadInt32(signal.Address, 1);
                        return intValues != null && intValues.Length > 0 && intValues[0] == int.Parse(signal.Value);
                    case "字符串类型":
                        ushort[] expected = StringToHoldingRegisters(signal.Value);
                        ushort[] actual = modbus.ReadUInt16(signal.Address, (ushort)expected.Length);
                        return expected.SequenceEqual(actual ?? new ushort[0]);
                    case "short类型":
                        short[] shortValues = modbus.ReadInt16(signal.Address, 1);
                        return shortValues != null && shortValues.Length > 0 && shortValues[0] == int.Parse(signal.Value);
                    case "ushort类型":
                        ushort[] ushortValues = modbus.ReadUInt16(signal.Address, 1);
                        return ushortValues != null && ushortValues.Length > 0 && ushortValues[0] == int.Parse(signal.Value);
                    default:
                        return false;
                }
            }
            return false;
        }

        private static bool ParseBoolValue(string value)
        {
            return value.Equals("True", StringComparison.OrdinalIgnoreCase) || value == "1";
        }

        /// <summary>
        /// dataGridView1 离开事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void DataGridView1_Leave(object sender, EventArgs e)
        {
            if (Solution.Instance.GlobalSignal == null)
                Solution.Instance.GlobalSignal = new GlobalSignal();
            Solution.Instance.GlobalSignal.ReadySignals = GetConfig(dataGridView1);
        }
        /// <summary>
        /// dataGridView2 离开事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void DataGridView2_Leave(object sender, EventArgs e)
        {
            if (Solution.Instance.GlobalSignal == null)
                Solution.Instance.GlobalSignal = new GlobalSignal();
            Solution.Instance.GlobalSignal.StopSignals = GetConfig(dataGridView2);
        }
        /// <summary>
        /// dataGridView3 离开事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGridView3_Leave(object sender, EventArgs e)
        {
            SaveListenSignalsAndRefresh();
        }

        private void DataGridView3_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            SaveListenSignalsAndRefresh();
        }

        private void DataGridView3_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveListenSignalsAndRefresh();
        }

        private void DataGridView3_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView3.IsCurrentCellDirty)
            {
                dataGridView3.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DataGridView3_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            SaveListenSignalsAndRefresh();
        }

        /// <summary>
        /// dataGridView4 离开事件，保存心跳信号并刷新后台发送任务。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void DataGridView4_Leave(object sender, EventArgs e)
        {
            SaveHeartbeatSignalsAndRefresh();
        }

        /// <summary>
        /// 心跳单元格编辑结束后刷新后台发送任务。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">单元格事件参数。</param>
        private void DataGridView4_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            SaveHeartbeatSignalsAndRefresh();
        }

        /// <summary>
        /// 心跳单元格值变化后刷新后台发送任务。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">单元格事件参数。</param>
        private void DataGridView4_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveHeartbeatSignalsAndRefresh();
        }

        /// <summary>
        /// 心跳表格下拉框变更后立即提交当前编辑值。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void DataGridView4_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView4.IsCurrentCellDirty)
            {
                dataGridView4.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        /// <summary>
        /// 心跳表格行删除后刷新后台发送任务。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">行删除事件参数。</param>
        private void DataGridView4_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            SaveHeartbeatSignalsAndRefresh();
        }

        /// <summary>
        /// 拦截表格下拉值不在候选项中的格式化异常，避免 WinForms 弹出默认 ERR 对话框。
        /// </summary>
        /// <param name="sender">触发异常的表格。</param>
        /// <param name="e">表格数据错误事件参数。</param>
        private void DataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            string gridName = sender is DataGridView grid ? grid.Name : "未知表格";
            string message = e.Exception == null ? "未知数据格式错误" : e.Exception.Message;
            LogHelper.AddLog(MsgLevel.Warn, $"全局信号表格({gridName})数据格式已自动忽略：行={e.RowIndex}，列={e.ColumnIndex}，原因={message}", true);
        }

        private void SaveListenSignalsAndRefresh()
        {
            if (_isLoadingConfig)
                return;

            if (Solution.Instance.GlobalSignal == null)
                Solution.Instance.GlobalSignal = new GlobalSignal();

            var listenSignals = GetListenConfig();
            if (listenSignals == null)
                return;

            Solution.Instance.GlobalSignal.ListenSignals = listenSignals;
            StartListenSignals();
        }

        /// <summary>
        /// 保存心跳信号配置并重启后台心跳任务。
        /// </summary>
        private void SaveHeartbeatSignalsAndRefresh()
        {
            if (_isLoadingConfig)
                return;

            if (Solution.Instance.GlobalSignal == null)
                Solution.Instance.GlobalSignal = new GlobalSignal();

            var heartbeatSignals = GetHeartbeatConfig();
            if (heartbeatSignals == null)
                return;

            Solution.Instance.GlobalSignal.HeartbeatSignals = heartbeatSignals;
            StartHeartbeatSignals();
        }

        private void FormGlobalSignal_Shown(object sender, EventArgs e)
        {
            #region 设置表格样式

            // 设置列标题的高度
            dataGridView1.ColumnHeadersHeight = 64;
            dataGridView2.ColumnHeadersHeight = 64;
            dataGridView3.ColumnHeadersHeight = 64;
            dataGridView4.ColumnHeadersHeight = 64;
            // 设置行的高度
            dataGridView1.RowTemplate.Height = 36;
            dataGridView2.RowTemplate.Height = 36;
            dataGridView3.RowTemplate.Height = 36;
            dataGridView4.RowTemplate.Height = 36;
            //等分表格列宽
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            foreach (DataGridViewColumn column in dataGridView2.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            foreach (DataGridViewColumn column in dataGridView3.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            foreach (DataGridViewColumn column in dataGridView4.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            #endregion

            #region 初始化表格数据
            try
            {
                _isLoadingConfig = true;
                var deviceNames = BuildCommunicationDeviceNameOptions();
                deviceColumn1.DataSource = deviceNames;
                deviceColumn2.DataSource = deviceNames;
                dataGridViewComboBoxColumn1.DataSource = deviceNames;
                heartbeatDeviceColumn.DataSource = deviceNames;
                var cameraNames = BuildHeartbeatCameraNameOptions();
                heartbeatCameraColumn.DataSource = cameraNames;
                if (Solution.Instance.GlobalSignal != null)
                {
                    LoadConfig(dataGridView1, Solution.Instance.GlobalSignal.ReadySignals);
                    LoadConfig(dataGridView2, Solution.Instance.GlobalSignal.StopSignals);
                    LoadConfig(dataGridView3, Solution.Instance.GlobalSignal.ListenSignals);
                    LoadHeartbeatConfig(dataGridView4, Solution.Instance.GlobalSignal.HeartbeatSignals);
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"加载全局信号配置异常！原因:{ex.Message}", true);
            }
            finally
            {
                _isLoadingConfig = false;
            }
            #endregion
        }

        /// <summary>
        /// 加载配置到表格中
        /// </summary>
        /// <param name="infos"></param>
        public void LoadConfig(DataGridView dataGridView, List<SingleGlobalSignalSettings> infos)
        {
            dataGridView.Rows.Clear();
            if (infos == null) return;
            foreach (var info in infos)
            {
                var row = new DataGridViewRow();
                row.CreateCells(dataGridView);

                row.Cells[0].Value = info.DeviceName;
                row.Cells[1].Value = info.Address;
                row.Cells[2].Value = info.Type;
                row.Cells[3].Value = info.Value;
                row.Cells[4].Value = info.Enable;
                row.Tag = info;

                dataGridView.Rows.Add(row);
            }
        }

        /// <summary>
        /// 加载心跳配置到心跳表格中。
        /// </summary>
        /// <param name="dataGridView">心跳配置表格。</param>
        /// <param name="infos">心跳配置列表。</param>
        public void LoadHeartbeatConfig(DataGridView dataGridView, List<HeartbeatGlobalSignalSettings> infos)
        {
            dataGridView.Rows.Clear();
            if (infos == null) return;
            foreach (var info in infos)
            {
                var row = new DataGridViewRow();
                row.CreateCells(dataGridView);

                row.Cells[0].Value = info.CameraName;
                row.Cells[1].Value = info.DeviceName;
                row.Cells[2].Value = info.Address;
                row.Cells[3].Value = NormalizeHeartbeatType(info.Type);
                row.Cells[4].Value = info.IntervalMilliseconds;
                row.Cells[5].Value = info.OnValue;
                row.Cells[6].Value = info.OffValue;
                row.Tag = info;

                dataGridView.Rows.Add(row);
            }
        }

        /// <summary>
        /// 构建通信设备下拉候选，额外保留方案中已保存的旧名称，避免下拉单元格显示时报错。
        /// </summary>
        /// <returns>通信设备名称候选列表。</returns>
        private static List<string> BuildCommunicationDeviceNameOptions()
        {
            var names = new List<string>();
            foreach (IDevice device in Solution.Instance.AllDevices.Where(d => d is IPlc || d is IModbus))
            {
                AddUniqueNonEmpty(names, device.DevName);
                AddUniqueNonEmpty(names, device.UserDefinedName);
            }

            GlobalSignal globalSignal = Solution.Instance.GlobalSignal;
            if (globalSignal == null)
                return names;

            AddGlobalSignalDeviceNames(names, globalSignal.ReadySignals);
            AddGlobalSignalDeviceNames(names, globalSignal.StopSignals);
            AddGlobalSignalDeviceNames(names, globalSignal.ListenSignals);
            if (globalSignal.HeartbeatSignals != null)
            {
                foreach (HeartbeatGlobalSignalSettings signal in globalSignal.HeartbeatSignals)
                    AddUniqueNonEmpty(names, signal.DeviceName);
            }

            return names;
        }

        /// <summary>
        /// 构建心跳相机下拉候选，额外保留方案中已保存的旧相机名，避免下拉单元格显示时报错。
        /// </summary>
        /// <returns>相机名称候选列表。</returns>
        private static List<string> BuildHeartbeatCameraNameOptions()
        {
            var names = new List<string>();
            foreach (ICamera camera in Solution.Instance.CameraDevices)
            {
                AddUniqueNonEmpty(names, camera.DevName);
                AddUniqueNonEmpty(names, camera.UserDefinedName);
            }

            GlobalSignal globalSignal = Solution.Instance.GlobalSignal;
            if (globalSignal?.HeartbeatSignals == null)
                return names;

            foreach (HeartbeatGlobalSignalSettings signal in globalSignal.HeartbeatSignals)
                AddUniqueNonEmpty(names, signal.CameraName);

            return names;
        }

        /// <summary>
        /// 将全局信号配置中的通信设备名加入下拉候选。
        /// </summary>
        /// <param name="names">待补充的候选列表。</param>
        /// <param name="signals">全局信号配置集合。</param>
        private static void AddGlobalSignalDeviceNames(List<string> names, IEnumerable<SingleGlobalSignalSettings> signals)
        {
            if (signals == null)
                return;

            foreach (SingleGlobalSignalSettings signal in signals)
                AddUniqueNonEmpty(names, signal.DeviceName);
        }

        /// <summary>
        /// 向候选列表追加非空且不重复的文本。
        /// </summary>
        /// <param name="values">候选列表。</param>
        /// <param name="value">待追加文本。</param>
        private static void AddUniqueNonEmpty(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
                return;

            string normalized = value.Trim();
            if (values.Any(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)))
                return;

            values.Add(normalized);
        }

        private List<SingleGlobalSignalSettings> GetListenConfig()
        {
            dataGridView3.EndEdit();
            var infos = new List<SingleGlobalSignalSettings>();
            try
            {
                foreach (DataGridViewRow row in dataGridView3.Rows)
                {
                    if (row.IsNewRow) continue;

                    var deviceName = row.Cells[0].Value?.ToString() ?? string.Empty;
                    var address = row.Cells[1].Value?.ToString() ?? string.Empty;
                    var type = row.Cells[2].Value?.ToString() ?? string.Empty;
                    var value = row.Cells[3].Value?.ToString() ?? string.Empty;
                    var enable = Convert.ToBoolean(row.Cells[4].Value ?? false);
                    var info = row.Tag as SingleGlobalSignalSettings;

                    if (info == null)
                    {
                        info = new SingleGlobalSignalSettings(deviceName, address, type, value, enable);
                        row.Tag = info;
                    }
                    else
                    {
                        info.DeviceName = deviceName;
                        info.Address = address;
                        info.Type = type;
                        info.Value = value;
                        info.Enable = enable;
                    }

                    infos.Add(info);
                }
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
                return null;
            }

            return infos;
        }

        /// <summary>
        /// 获取心跳表格中的配置数据。
        /// </summary>
        /// <returns>心跳配置列表，读取失败时返回 null。</returns>
        private List<HeartbeatGlobalSignalSettings> GetHeartbeatConfig()
        {
            dataGridView4.EndEdit();
            var infos = new List<HeartbeatGlobalSignalSettings>();
            try
            {
                foreach (DataGridViewRow row in dataGridView4.Rows)
                {
                    if (row.IsNewRow) continue;

                    string cameraName = row.Cells[0].Value?.ToString() ?? string.Empty;
                    string deviceName = row.Cells[1].Value?.ToString() ?? string.Empty;
                    string address = row.Cells[2].Value?.ToString() ?? string.Empty;
                    string type = NormalizeHeartbeatType(row.Cells[3].Value?.ToString());
                    int intervalMilliseconds = ParseHeartbeatInterval(row.Cells[4].Value?.ToString());
                    string onValue = row.Cells[5].Value?.ToString() ?? string.Empty;
                    string offValue = row.Cells[6].Value?.ToString() ?? string.Empty;
                    var info = row.Tag as HeartbeatGlobalSignalSettings;

                    if (info == null)
                    {
                        info = new HeartbeatGlobalSignalSettings(cameraName, deviceName, address, type, intervalMilliseconds, onValue, offValue, true);
                        row.Tag = info;
                    }
                    else
                    {
                        info.CameraName = cameraName;
                        info.DeviceName = deviceName;
                        info.Address = address;
                        info.Type = type;
                        info.IntervalMilliseconds = intervalMilliseconds;
                        info.OnValue = onValue;
                        info.OffValue = offValue;
                        info.Enable = true;
                    }

                    infos.Add(info);
                }
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
                return null;
            }

            return infos;
        }

        /// <summary>
        /// 解析界面填写的心跳间隔。
        /// </summary>
        /// <param name="value">界面文本。</param>
        /// <returns>合法毫秒间隔，非法时返回默认间隔。</returns>
        private static int ParseHeartbeatInterval(string value)
        {
            if (!int.TryParse(value, out int intervalMilliseconds))
                return DefaultHeartbeatIntervalMilliseconds;

            return NormalizeHeartbeatInterval(intervalMilliseconds);
        }

        /// <summary>
        /// 获取表格的数据
        /// </summary>
        /// <param name="dataGridView"></param>
        /// <returns></returns>
        public List<SingleGlobalSignalSettings> GetConfig(DataGridView dataGridView)
        {
            dataGridView.EndEdit();
            var infos = new List<SingleGlobalSignalSettings>();
            try
            {
                foreach (DataGridViewRow row in dataGridView.Rows)
                {
                    if (row.IsNewRow) continue;
                    var info = new SingleGlobalSignalSettings(
                        row.Cells[0].Value?.ToString() ?? string.Empty,
                        row.Cells[1].Value?.ToString() ?? string.Empty,
                        row.Cells[2].Value?.ToString() ?? string.Empty,
                        row.Cells[3].Value?.ToString() ?? string.Empty,
                        Convert.ToBoolean(row.Cells[4].Value ?? false)
                    );
                    infos.Add(info);
                }
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
                return null;
            }
            return infos;
        }

        private void FormGlobalSignal_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Solution.Instance.GlobalSignal == null)
                Solution.Instance.GlobalSignal = new GlobalSignal();
            Solution.Instance.GlobalSignal.ReadySignals = GetConfig(dataGridView1);
            Solution.Instance.GlobalSignal.StopSignals = GetConfig(dataGridView2);
            SaveListenSignalsAndRefresh();
            SaveHeartbeatSignalsAndRefresh();
        }
    }
    /// <summary>
    /// 全局信号配置类
    /// </summary>
    public class GlobalSignal
    {
        /// <summary>
        /// 准备信号
        /// </summary>
        public List<SingleGlobalSignalSettings> ReadySignals { get; set; } = new List<SingleGlobalSignalSettings>();
        /// <summary>
        /// 停止信号
        /// </summary>
        public List<SingleGlobalSignalSettings> StopSignals { get; set; } = new List<SingleGlobalSignalSettings>();
        /// <summary>
        /// 监听信号
        /// </summary>
        public List<SingleGlobalSignalSettings> ListenSignals { get; set; } = new List<SingleGlobalSignalSettings>();
        /// <summary>
        /// 心跳信号
        /// </summary>
        public List<HeartbeatGlobalSignalSettings> HeartbeatSignals { get; set; } = new List<HeartbeatGlobalSignalSettings>();
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public GlobalSignal() { }
    }

    /// <summary>
    /// 单条信号
    /// </summary>
    public class SingleGlobalSignalSettings
    {
        /// <summary>
        /// 通信设备名称。
        /// </summary>
        public string DeviceName { get; set; }
        /// <summary>
        /// 信号地址。
        /// </summary>
        public string Address { get; set; }
        /// <summary>
        /// 信号数据类型。
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// 信号数据值。
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool Enable { get; set; }
        /// <summary>
        /// 监听信号当前是否满足配置值，后续节点可读取该值。
        /// </summary>
        public bool SignalValue { get; set; }
        /// <summary>
        /// 监听信号是否已被当前值锁存。
        /// </summary>
        [JsonIgnore]
        public bool SignalTriggered { get; set; }
        /// <summary>
        /// 初始化单条全局信号配置。
        /// </summary>
        /// <param name="devName">通信设备名称。</param>
        /// <param name="address">信号地址。</param>
        /// <param name="type">信号数据类型。</param>
        /// <param name="value">信号数据值。</param>
        /// <param name="enable">是否启用。</param>
        public SingleGlobalSignalSettings(string devName, string address, string type, string value, bool enable)
        {
            DeviceName = devName;
            Address = address;
            Type = type;
            Value = value;
            Enable = enable;
            SignalValue = false;
            SignalTriggered = false;
        }
    }

    /// <summary>
    /// 单条心跳信号配置。
    /// </summary>
    public class HeartbeatGlobalSignalSettings
    {
        /// <summary>
        /// 绑定的相机名称，相机未连接时暂停发送。
        /// </summary>
        public string CameraName { get; set; }
        /// <summary>
        /// 写入心跳的通信设备名称。
        /// </summary>
        public string DeviceName { get; set; }
        /// <summary>
        /// 写入心跳的地址。
        /// </summary>
        public string Address { get; set; }
        /// <summary>
        /// 写入心跳的数据类型。
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// On 和 Off 两次写入之间的间隔时间，单位毫秒。
        /// </summary>
        public int IntervalMilliseconds { get; set; }
        /// <summary>
        /// 心跳打开值。
        /// </summary>
        public string OnValue { get; set; }
        /// <summary>
        /// 心跳关闭值。
        /// </summary>
        public string OffValue { get; set; }
        /// <summary>
        /// 是否启用心跳配置。
        /// </summary>
        public bool Enable { get; set; }

        /// <summary>
        /// 默认构造函数，供方案反序列化使用。
        /// </summary>
        public HeartbeatGlobalSignalSettings()
        {
            CameraName = string.Empty;
            DeviceName = string.Empty;
            Address = string.Empty;
            Type = "布尔";
            IntervalMilliseconds = 1000;
            OnValue = string.Empty;
            OffValue = string.Empty;
            Enable = true;
        }

        /// <summary>
        /// 初始化单条心跳配置。
        /// </summary>
        /// <param name="cameraName">绑定的相机名称。</param>
        /// <param name="deviceName">写入心跳的通信设备名称。</param>
        /// <param name="address">写入心跳的地址。</param>
        /// <param name="type">写入心跳的数据类型。</param>
        /// <param name="intervalMilliseconds">写入间隔，单位毫秒。</param>
        /// <param name="onValue">On值。</param>
        /// <param name="offValue">Off值。</param>
        /// <param name="enable">是否启用。</param>
        public HeartbeatGlobalSignalSettings(string cameraName, string deviceName, string address, string type, int intervalMilliseconds, string onValue, string offValue, bool enable)
        {
            CameraName = cameraName;
            DeviceName = deviceName;
            Address = address;
            Type = NormalizeType(type);
            IntervalMilliseconds = intervalMilliseconds;
            OnValue = onValue;
            OffValue = offValue;
            Enable = enable;
        }

        /// <summary>
        /// 将构造入参中的旧心跳类型名称转换为当前统一名称，避免反序列化或旧调用保留旧文本。
        /// </summary>
        /// <param name="type">写入类型名称。</param>
        /// <returns>统一后的写入类型名称。</returns>
        private static string NormalizeType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return "布尔";

            switch (type.Trim())
            {
                case "布尔类型":
                    return "布尔";
                case "整数":
                case "整数类型":
                    return "Int32";
                case "short类型":
                    return "Int16";
                case "ushort类型":
                    return "UInt16";
                case "浮点类型":
                    return "浮点";
                case "字符串类型":
                    return "字符串";
                default:
                    return type.Trim();
            }
        }
    }
}
