using Logger;
using Sunny.UI;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.COM;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ComSend
{
    public partial class ParamFormComSend : FormBase, INodeParamForm
    {
        public ParamFormComSend()
        {
            InitializeComponent();
            InitComList();
            comboBoxEncoding.SelectedIndex = 0;
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node) { }


        /// <summary>
        /// 窗口每次显示时都要刷新下拉框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParamFormPlcWrite_Shown(object sender, EventArgs e)
        {
            InitComList();
        }
        /// <summary>
        /// 初始化串口下拉框
        /// </summary>
        private void InitComList()
        {
            string text1 = comboBoxComList.Text;

            comboBoxComList.Items.Clear();
            comboBoxComList.Items.Add("[未设置]");
            foreach (var com in Solution.Instance.ComDevices)
            {
                comboBoxComList.Items.Add(com.UserDefinedName);
            }
            int index1 = comboBoxComList.Items.IndexOf(text1);
            if (index1 == -1)
                comboBoxComList.SelectedIndex = 0;
            else
                comboBoxComList.SelectedIndex = index1;
        }

        /// <summary>
        /// 点击测试发送
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void buttonTest_Click(object sender, EventArgs e)
        {
            if (comboBoxComList.Text.IsNullOrEmpty() || comboBoxComList.Text == "[未设置]")
            {
                MessageBoxTD.Show("COM号不能为空！");
                return;
            }
            if (textBoxCMD.Text.IsNullOrEmpty())
            {
                MessageBoxTD.Show("发送的指令不能为空！");
                return;
            }
            if (!SaveParams())
                return;
            var param = Params as NodeParamComSend;
            if (param?.Dev == null || !param.Dev.IsOpen)
            {
                MessageBoxTD.Show("串口对象为空或未打开！");
                return;
            }
            if (string.IsNullOrEmpty(param.Cmd))
            {
                MessageBoxTD.Show("发送的指令不能为空！");
                return;
            }

            var deviceSnapshot = param.Dev;
            string commandSnapshot = param.Cmd;
            string encodingSnapshot = param.Encoding;
            buttonTest.Enabled = false;
            try
            {
                bool executed = await Task.Run(() =>
                    Solution.Instance.TryExecuteManualExternalSignal(
                        () => deviceSnapshot.Send(commandSnapshot, encodingSnapshot)));
                if (!executed)
                    MessageBoxTD.Show("方案正在运行或重置，禁止手动插入串口命令！");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"串口测试发送失败：{ex.Message}");
            }
            finally
            {
                buttonTest.Enabled = true;
            }
        }

        /// <summary>
        /// 点击保存当前参数配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSvae_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        /// <summary>
        /// 保存参数配置
        /// </summary>
        private bool SaveParams()
        {
            if (comboBoxComList.Text.IsNullOrEmpty() || comboBoxComList.Text == "[未设置]")
            {
                MessageBoxTD.Show("COM号不能为空！");
                return false;
            }
            if (textBoxCMD.Text.IsNullOrEmpty())
            {
                MessageBoxTD.Show("发送的指令不能为空！");
                return false;
            }

            //查找当前选择的串口
            ComDevice com = null;
            foreach (var comTmp in Solution.Instance.ComDevices)
            {
                if (comTmp.UserDefinedName == comboBoxComList.Text)
                {
                    com = comTmp;
                    break;
                }
            }
            if (com == null)
            {
                MessageBoxTD.Show("所选串口已不在当前方案中，请重新选择！");
                return false;
            }

            NodeParamComSend nodeParamComSend = new NodeParamComSend();
            nodeParamComSend.Dev = com;
            nodeParamComSend.DevName = com.UserDefinedName;
            nodeParamComSend.Encoding = comboBoxEncoding.Text;
            nodeParamComSend.Cmd = this.textBoxCMD.Text;
            Params = nodeParamComSend;
            return true;
        }
        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if (Params is NodeParamComSend param)
            {
                int index1 = comboBoxComList.Items.IndexOf(param.DevName);
                comboBoxComList.SelectedIndex = index1 == -1 ? 0 : index1;
                // 反序列化后方案中的COM设备对象才是完整的，需要赋值给用到COM的节点参数中
                foreach (var com in Solution.Instance.ComDevices)
                {
                    if (com.UserDefinedName == param.DevName)
                    {
                        param.Dev = com;
                        break;
                    }
                }
                comboBoxComList.Text = param.DevName;
                comboBoxEncoding.Text = param.Encoding;
                textBoxCMD.Text = param.Cmd;
            }
        }

        private void 通道1光源设为255ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBoxCMD.Text = "SA0255#";
        }

        private void 通道2光源设为0ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBoxCMD.Text = "SB0000#";
        }

        private void 通道3光源设为255ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBoxCMD.Text = "SC0255#";
        }

        private void 通道4光源设为0ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBoxCMD.Text = "SD0000#";
        }

        private void 光源全部关闭ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBoxCMD.Text = "SA0000#SB0000#SC0000#SD0000#";
        }

        private void 光源全部打开ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBoxCMD.Text = "SA0255#SB0255#SC0255#SD0255#";
        }
    }
}
