using Logger;
using System;
using System.Windows.Forms;
using TDJS_Vision.Device.Light;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Device.COM;
using System.Linq;
using TDJS_Vision.Startup;

namespace TDJS_Vision.Forms.COMAdd
{
    public partial class FrmCOMListView : FormBase
    {
        /// <summary>
        /// 添加串口时通过快捷键保存方案的事件
        /// </summary>
        public event EventHandler OnShotKeySavePressed;
        /// <summary>
        /// 串口添加窗口
        /// </summary>
        FrmCOMNew frmComNew = new FrmCOMNew();
        /// <summary>
        /// 串口反序列化完成事件
        /// </summary>
        public static event EventHandler<bool> OnCOMDeserializationCompletionEvent;

        public FrmCOMListView()
        {
            InitializeComponent();
            frmComNew.ComAddEvent += FrmComNew_ComAddEvent;
            SingleCOM.SelectedChange += SingleCOM_SelectedChange;
            SingleCOM.SingleComRemoveEvent += SingleCOM_RemoveEvent;
            ConfigHelper.DeserializationCompletionEvent += Deserialization;
            this.KeyPreview = true;
        }

        /// <summary>
        /// 反序列化串口设备
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Deserialization(object sender, bool e)
        {
            try
            {
                // 先移除旧方案的串口控件
                flowLayoutPanel1.Controls.Clear();
                // 添加新的串口
                var comDevices = ConfigHelper.SolConfig.Devices.OfType<ComDevice>().ToList();
                if (e)
                    LogHelper.AddLog(MsgLevel.Debug, $"================================================= 正在加载【串口设备列表】=================================================", true);
                if (comDevices.Count == 0)
                    StartupProgressContext.ReportItem("正在恢复串口设备", "没有需要恢复的串口", 0, 0, 77, 84);
                for (int index = 0; index < comDevices.Count; index++)
                {
                    ComDevice devCom = comDevices[index];
                    StartupProgressContext.ReportItem("正在恢复串口设备", devCom.DevName, index + 1, comDevices.Count, 77, 84);
                    devCom.CreateDevice(); // 创建串口，必要的
                    SingleCOM singleCOM = new SingleCOM(devCom);
                    singleCOM.Anchor = AnchorStyles.Left;
                    singleCOM.Anchor = AnchorStyles.Right;
                    flowLayoutPanel1.Controls.Add(singleCOM);
                    if (e)
                        LogHelper.AddLog(MsgLevel.Info, $"串口设备【{devCom.DevName}】已加载！", true);
                }
                if (e)
                    LogHelper.AddLog(MsgLevel.Debug, $"================================================【串口设备列表】已加载完成 ================================================", true);

            }
            catch (Exception ex)
            {
                StartupProgressContext.ReportFailure("恢复串口设备", ex);
                LogHelper.AddLog(MsgLevel.Exception, $"恢复串口设备失败：{ex}", true);
            }
            finally
            {
                OnCOMDeserializationCompletionEvent?.Invoke(this, e);
            }
        }

        /// <summary>
        /// 按下保存快捷键
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.S)
            {
                // 触发保存方案事件
                OnShotKeySavePressed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 移除设备
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SingleCOM_RemoveEvent(object sender, SingleCOM e)
        {
            SingleCOM.SingleComs.Remove(e);
            panel1.Controls.Clear();
            // 释放串口资源
            e.com.Close();
            //然后移除掉方案中的全局串口
            Solution.Instance.AllDevices.Remove(e.com);
            //最后移除掉串口控件和节点
            flowLayoutPanel1.Controls.Remove(e);
            LogHelper.AddLog(MsgLevel.Info, $"串口设备（{e.com.DevName}）已成功移除！", true);
        }

        /// <summary>
        /// 切换选择的串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SingleCOM_SelectedChange(object sender, SingleCOM e)
        {
            panel1.Controls.Clear();
            var control = new COMParamsShowControl(e.com.ComParams);
            control.Dock = DockStyle.Fill;
            panel1.Controls.Add(control);
        }

        /// <summary>
        /// 串口添加事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmComNew_ComAddEvent(object sender, SerialPortConfig e)
        {
            SingleCOM singleCOM = null;
            try
            {
                singleCOM = new SingleCOM(e);
                singleCOM.Anchor = AnchorStyles.Left;
                singleCOM.Anchor = AnchorStyles.Right;
                flowLayoutPanel1.Controls.Add(singleCOM);
            }
            catch (Exception ex)
            {
                SingleCOM.SingleComs.Remove(singleCOM);
                LogHelper.AddLog(MsgLevel.Fatal, $"添加串口失败:{ex.Message}", true);
                MessageBoxTD.Show($"添加串口失败:{ex.Message}");
            }
        }

        /// <summary>
        /// 点击添加串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            frmComNew.ShowDialog();
        }


        private void FrmPLCListView_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
