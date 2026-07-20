using Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Device.PLC;

namespace TDJS_Vision.Forms.GlobalSignalSettings
{
    public partial class FormGlobalSignal : Form
    {
        private const int ListenInterval = 1;
        private static CancellationTokenSource _listenSignalCts;
        /// <summary>
        /// 监听信号状态锁，避免多个监听线程同时写入 SignalValue 和 SignalTriggered。
        /// </summary>
        private static readonly object _listenSignalStateLock = new object();
        private bool _isLoadingConfig;

        public FormGlobalSignal()
        {
            InitializeComponent();
            Shown += FormGlobalSignal_Shown;
            dataGridView1.Leave += DataGridView1_Leave;
            dataGridView2.Leave += DataGridView2_Leave;
            dataGridView3.Leave += DataGridView3_Leave;
            dataGridView3.CellClick += dataGridView1_CellClick;
            dataGridView3.CellEndEdit += DataGridView3_CellEndEdit;
            dataGridView3.CellValueChanged += DataGridView3_CellValueChanged;
            dataGridView3.CurrentCellDirtyStateChanged += DataGridView3_CurrentCellDirtyStateChanged;
            dataGridView3.RowsRemoved += DataGridView3_RowsRemoved;
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

        private void FormGlobalSignal_Shown(object sender, EventArgs e)
        {
            #region 设置表格样式

            // 设置列标题的高度
            dataGridView1.ColumnHeadersHeight = 64;
            dataGridView2.ColumnHeadersHeight = 64;
            dataGridView3.ColumnHeadersHeight = 64;
            // 设置行的高度
            dataGridView1.RowTemplate.Height = 36;
            dataGridView2.RowTemplate.Height = 36;
            dataGridView3.RowTemplate.Height = 36;
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
            #endregion

            #region 初始化表格数据
            try
            {
                _isLoadingConfig = true;
                var deviceNames = Solution.Instance.AllDevices.Where(d => d is IPlc || d is IModbus).Select(d => d.DevName).ToList();
                deviceColumn1.DataSource = deviceNames;
                deviceColumn2.DataSource = deviceNames;
                dataGridViewComboBoxColumn1.DataSource = deviceNames;
                if (Solution.Instance.GlobalSignal != null)
                {
                    LoadConfig(dataGridView1, Solution.Instance.GlobalSignal.ReadySignals);
                    LoadConfig(dataGridView2, Solution.Instance.GlobalSignal.StopSignals);
                    LoadConfig(dataGridView3, Solution.Instance.GlobalSignal.ListenSignals);
                }
                _isLoadingConfig = false;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"加载全局信号配置异常！原因:{ex.Message}", true);
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
        /// 默认构造函数
        /// </summary>
        public GlobalSignal() { }
    }

    /// <summary>
    /// 单条信号
    /// </summary>
    public class SingleGlobalSignalSettings
    {
        public string DeviceName { get; set; }
        public string Address { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public bool Enable { get; set; }
        /// <summary>
        /// 监听信号当前是否满足配置值，后续节点可读取该值。
        /// </summary>
        public bool SignalValue { get; set; }
        [JsonIgnore]
        public bool SignalTriggered { get; set; }
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
}
