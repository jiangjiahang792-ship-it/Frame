using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Device.PLC;

namespace TDJS_Vision.Forms.PLCAdd
{
    /// <summary>
    /// 单个PLC
    /// </summary>
    public partial class SinglePLC : UserControl
    {
        public IPlc Plc = null;
        /// <summary>
        /// 通信方式
        /// </summary>
        public PlcConType ConType;
        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected;
        /// <summary>
        /// 选中改变事件
        /// </summary>
        public static event EventHandler<SinglePLC> SelectedChange;
        /// <summary>
        /// 移除事件
        /// </summary>
        public static event EventHandler<SinglePLC> SinglePLCRemoveEvent;
        /// <summary>
        /// 保存所有的当前类实例
        /// </summary>
        public static List<SinglePLC> SinglePLCs = new List<SinglePLC>();

        /// <summary>
        /// 反序列化用
        /// </summary>
        /// <param name="plc"></param>
        public SinglePLC(IPlc plc)
        {
            InitializeComponent();
            plc.ConnectStatusEvent += Plc_ConnectStatusEvent;
            if (plc.IsConnect || (plc as TDJS_Vision.Device.IReconnectableCommunicationDevice)?.RestoreConnectionRequested == true)
            {
                if (!plc.Connect())
                    LogHelper.AddLog(MsgLevel.Exception, $"PLC（{plc.UserDefinedName}）连接失败，请检查PLC通信是否正常！", true);
            }
            ConType = plc.PLCParms.PlcConType;
            this.label1.Text = plc.UserDefinedName;
            Plc = plc;
            Solution.Instance.AllDevices.Add(Plc);
            SinglePLCs.Add(this);
        }

        public SinglePLC(PLCParms parms)
        {
            InitializeComponent();
            ConType = parms.PlcConType;
            this.label1.Text = parms.UserDefinedName;
            IPlc plc = null;
            switch (parms.DeviceBrand)
            {
                case Device.DeviceBrand.Panasonic:
                    plc = new PlcPanasonic(parms);
                    break;
                case Device.DeviceBrand.Melsec:
                    plc = new PlcMelsec(parms);
                    break;
                case Device.DeviceBrand.Keyence:
                    switch (parms.KeyenceProtocol)
                    {
                        case KeyenceProtocol.NanoOverTcp:
                            plc = new PlcKeyenceNano(parms);
                            break;
                        case KeyenceProtocol.Kv300Older:
                            plc = new PlcKeyenceKvOld(parms);
                            break;
                        default:
                            throw new NotSupportedException("不支持的基恩士通信协议！");
                    }
                    break;
                default:
                    break;
            }
            plc.ConnectStatusEvent += Plc_ConnectStatusEvent;
            Plc = plc;
            Solution.Instance.AllDevices.Add(Plc);
            SinglePLCs.Add(this);
        }

        /// <summary>
        /// 订阅PLC连接状态
        /// </summary>
        private void Plc_ConnectStatusEvent(object sender, bool e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => Plc_ConnectStatusEvent(sender, e))); }
                catch (InvalidOperationException) { }
                return;
            }
            uiSwitch1.ValueChanged -= uiSwitch1_ValueChanged;
            uiSwitch1.Active = Plc?.IsConnect == true;
            uiSwitch1.ValueChanged += uiSwitch1_ValueChanged;
        }

        /// <summary>首次显示时刷新实际连接状态，避免构造期间的后台通知丢失。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Plc_ConnectStatusEvent(Plc, Plc?.IsConnect == true);
        }

        /// <summary>
        /// 点击选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SinglePLCInfo_MouseClick(object sender, MouseEventArgs e)
        {
            SetSelected();
            SelectedChange?.Invoke(this, this);
        }

        /// <summary>
        /// 设置控件选中状态
        /// </summary>
        /// <param name="flag"></param>
        private void SetSelected()
        {
            //先清除所有选中状态
            foreach (var item in SinglePLCs)
            {
                item.tableLayoutPanel1.BackColor = Color.LightSteelBlue;
                item.label1.BackColor = Color.LightSteelBlue;
                IsSelected = false;
            }

            // 设置当前选中的样式
            this.tableLayoutPanel1.BackColor = Color.CornflowerBlue;
            this.label1.BackColor = Color.CornflowerBlue;
            IsSelected = true;
        }

        /// <summary>
        /// 连接PLC
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private void uiSwitch1_ValueChanged(object sender, bool value)
        {
            try
            {
                if (value)
                {
                    if (Plc.Connect())
                        LogHelper.AddLog(MsgLevel.Info, $"{Plc.UserDefinedName}打开", true);
                    else
                    {
                        LogHelper.AddLog(MsgLevel.Exception, $"{Plc.UserDefinedName}连接失败！请检查参数设置是否为无效值或与其他通信设置冲突！", true);
                    }
                }
                else
                {
                    if (Plc.IsConnect || (Plc as TDJS_Vision.Device.IReconnectableCommunicationDevice)?.RestoreConnectionRequested == true)
                    {
                        Plc.Disconnect();
                        LogHelper.AddLog(MsgLevel.Info, $"{Plc.UserDefinedName}关闭", true);
                    }
                }
            }
            catch(Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"{((Device.IDevice)Plc).UserDefinedName}" + e.Message, true);
            }
        }

        /// <summary>
        /// 右击移除当前设备
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 移除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(IsSelected)
                SinglePLCRemoveEvent?.Invoke(this, this);
        }

        /// <summary>离线重试期间也可主动停止恢复，不必等待连接成功。</summary>
        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uiSwitch1_ValueChanged(sender, false);
        }
    }
}
