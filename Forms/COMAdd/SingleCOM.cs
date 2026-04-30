using Logger;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Device;
using TDJS_Vision.Device.COM;

namespace TDJS_Vision.Forms.COMAdd
{
    /// <summary>
    /// 单个串口控件
    /// </summary>
    public partial class SingleCOM : UserControl
    {
        /// <summary>
        /// 串口对象
        /// </summary>
        public ComDevice com;
        /// <summary>
        /// 串口参数
        /// </summary>
        public SerialPortConfig Parms = new SerialPortConfig();
        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected = false;
        /// <summary>
        /// 串口名称
        /// </summary>
        public string ComName { get => label1.Text; set => label1.Text = value; }
        /// <summary>
        /// 当前类实例选中改变事件
        /// </summary>
        public static event EventHandler<SingleCOM> SelectedChange;
        /// <summary>
        /// 移除当前实例
        /// </summary>
        public static event EventHandler<SingleCOM> SingleComRemoveEvent;
        /// <summary>
        /// 保存所有的当前类实例
        /// </summary>
        public static List<SingleCOM> SingleComs = new List<SingleCOM>();

        /// <summary>
        /// 反序列化用
        /// </summary>
        public SingleCOM(ComDevice dev)
        {
            InitializeComponent();
            com = dev;
            com.ConnectStatusEvent += Com_ConnectStatusEvent;
            if (dev.IsOpen)
            {
                try
                {
                    dev.Connenct();
                }
                catch (Exception ex)
                {
                    uiSwitch1.Active = false;
                    LogHelper.AddLog(MsgLevel.Exception, $"串口（{dev.UserDefinedName}）打开失败，请检查串口状态！原因：{ex.Message}", true);
                }
            }
            this.label1.Text = $"串口[{dev.UserDefinedName}]";
            Parms = dev.ComParams;
            Solution.Instance.AllDevices.Add(dev);
            // 保存所有的实例
            SingleComs.Add(this);
        }

        private void Com_ConnectStatusEvent(object sender, bool e)
        {
            uiSwitch1.ValueChanged -= uiSwitch1_ValueChanged;
            uiSwitch1.Active = e;
            uiSwitch1.ValueChanged += uiSwitch1_ValueChanged;
        }

        /// <summary>
        /// 反序列化用
        /// </summary>
        /// <param name="parms"></param>
        public SingleCOM(SerialPortConfig parms)
        {
            InitializeComponent();
            this.label1.Text =  $"串口[{parms.PortName}]";
            Parms = parms;

            try
            {
                com = new ComDevice(parms);
                com.ConnectStatusEvent += Com_ConnectStatusEvent;
                Solution.Instance.AllDevices.Add(com);
                // 保存所有的实例
                SingleComs.Add(this);
            }
            catch (Exception ex)
            {
                throw ex;
            }
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
            foreach (var item in SingleComs)
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
        /// 连接COM
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private void uiSwitch1_ValueChanged(object sender, bool value)
        {
            if (value)
            {
                try
                {
                    com.Connenct();
                    LogHelper.AddLog(MsgLevel.Info, $"串口[{Parms.PortName}]已打开!", true);
                }
                catch (Exception e)
                {
                    LogHelper.AddLog(MsgLevel.Fatal, $"串口[{Parms.PortName}]打开失败！", true);
                }
            }
            else
            {
                try
                {
                    com.Close();
                    LogHelper.AddLog(MsgLevel.Info, $"串口[{Parms.PortName}]已关闭！", true);
                }
                catch (Exception e)
                {
                    LogHelper.AddLog(MsgLevel.Fatal, $"串口[{Parms.PortName}]无法关闭！", true);
                }
            }
            
        }

        /// <summary>
        /// 右击移除当前实例
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 移除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(IsSelected)
                SingleComRemoveEvent?.Invoke(this, this);
        }
    }
}
