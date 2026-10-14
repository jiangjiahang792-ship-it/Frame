using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Light;
using TDJS_Vision.Node._5_EquipmentCommunication.LightOpen;

namespace TDJS_Vision.Forms.LightAdd
{
    /// <summary>
    /// 单个光源控件
    /// </summary>
    public partial class SingleLight : UserControl
    {
        /// <summary>
        /// 光源对象
        /// </summary>
        public ILight Light;
        /// <summary>
        /// 光源参数
        /// </summary>
        public LightParam Parms = new LightParam();
        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected = false;
        /// <summary>
        /// 光源名称
        /// </summary>
        public string LightName { get => label1.Text; set => label1.Text = value; }
        /// <summary>
        /// 当前类实例选中改变事件
        /// </summary>
        public static event EventHandler<SingleLight> SelectedChange;
        /// <summary>
        /// 移除当前实例
        /// </summary>
        public static event EventHandler<SingleLight> SingleLightRemoveEvent;
        /// <summary>
        /// 保存所有的当前类实例
        /// </summary>
        public static List<SingleLight> SingleLights = new List<SingleLight>();

        /// <summary>
        /// 反序列化用
        /// </summary>
        /// <param name="light"></param>
        public SingleLight(ILight light)
        {
            InitializeComponent();
            Light = light;
            light.ConnectStatusEvent += Light_ConnectStatusEvent;
            if (light.IsOpen)
            {
                TDJS_Vision.Startup.StartupDeviceConnectionBatch.RunOrQueue(light.UserDefinedName, () =>
                {
                    light.CreateDevice();
                    light.Connenct();
                    light.TurnOn(light.Brightness);
                    if (!light.IsOpen)
                        throw new InvalidOperationException($"光源（{light.UserDefinedName}）连接失败。");
                });
            }
            else
                TDJS_Vision.Startup.StartupDeviceConnectionBatch.RunOrQueue(light.UserDefinedName, light.CreateDevice);
            this.label1.Text = light.UserDefinedName;
            Light = light;
            Parms = light.LightParam;
            Solution.Instance.AllDevices.Add(Light);
            // 保存所有的实例
            SingleLights.Add(this);
        }

        private void Light_ConnectStatusEvent(object sender, bool e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => Light_ConnectStatusEvent(sender, e))); }
                catch (InvalidOperationException) { }
                return;
            }
            uiSwitch1.ValueChanged -= uiSwitch1_ValueChanged;
            uiSwitch1.Active = Light?.IsOpen == true;
            uiSwitch1.ValueChanged += uiSwitch1_ValueChanged;
        }

        /// <summary>首次创建窗口句柄时刷新实际连接状态。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Light_ConnectStatusEvent(Light, Light?.IsOpen == true);
        }

        public SingleLight(LightParam parms)
        {
            InitializeComponent();
            this.label1.Text =  parms.LightName;
            Parms = parms;

            try
            {
                //创建光源设备并添加到方案中
                if (Parms.Brand == DeviceBrand.PPX)
                    Light = new LightPPX(Parms);
                else if (Parms.Brand == DeviceBrand.Rsee)
                    Light = new LightRsee(Parms);
                else if (Parms.Brand == DeviceBrand.TDJS)
                    Light = new LightTDJS(Parms);
                else
                    throw new Exception("暂时不支持的光源品牌！");
                Solution.Instance.AllDevices.Add(Light);
                // 保存所有的实例
                SingleLights.Add(this);
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
            foreach (var item in SingleLights)
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
        /// 连接光源
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private void uiSwitch1_ValueChanged(object sender, bool value)
        {
            if (value)
            {
                try
                {
                    Light.TurnOn(255);
                    LogHelper.AddLog(MsgLevel.Info, $"{Parms.LightName}(串口号：{Parms.Port}，通道：{Parms.Channel})已打开！", true);
                }
                catch (Exception e)
                {
                    LogHelper.AddLog(MsgLevel.Fatal, $"{Parms.LightName}(串口号：{Parms.Port}，通道：{Parms.Channel})打开失败！", true);
                }
            }
            else
            {
                try
                {
                    Light.TurnOff();
                    LogHelper.AddLog(MsgLevel.Info, $"{Parms.LightName}(串口号：{Parms.Port}，通道：{Parms.Channel})已关闭！", true);
                }
                catch (Exception e)
                {
                    LogHelper.AddLog(MsgLevel.Fatal, $"{Parms.LightName}(串口号：{Parms.Port}，通道：{Parms.Channel})无法关闭！", true);
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
                SingleLightRemoveEvent?.Invoke(this, this);
        }
    }
}
