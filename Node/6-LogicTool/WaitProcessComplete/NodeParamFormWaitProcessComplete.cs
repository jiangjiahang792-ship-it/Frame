using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._6_LogicTool.WaitProcessComplete
{
    public partial class NodeParamFormWaitProcessComplete : FormBase, INodeParamForm
    {
        Process process;
        public NodeParamFormWaitProcessComplete(Process process)
        {
            InitializeComponent();
            Shown += NodeParamFormProcessTrigger_Shown;
            this.process = process;
        }
        /// <summary>
        /// 流程列表显示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NodeParamFormProcessTrigger_Shown(object sender, EventArgs e)
        {
            InitProcess();
        }

        private void InitProcess()
        {
            string text = comboBoxProcess.Text;
            comboBoxProcess.Items.Clear();
            foreach (var pro in Solution.Instance.AllProcesses)
            {
                if (pro.ProcessName != process.ProcessName)
                    comboBoxProcess.Items.Add(pro.ProcessName);
            }
            // 选中直接流程
            if (comboBoxProcess.Items.Contains(text))
                comboBoxProcess.SelectedItem = text;
            else
                comboBoxProcess.SelectedIndex = -1;
        }

        public INodeParam Params { get; set; }

        /// <summary>
        /// 添加流程
        /// </summary>
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Contains(comboBoxProcess.Text))
            {
                MessageBoxTD.Show("当前流程已添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            listBox1.Items.Add(comboBoxProcess.Text);
        }

        /// <summary>
        /// 移除流程
        /// </summary>
        private void buttonRemove_Click(object sender, EventArgs e)
        {
            if(listBox1.SelectedItem == null)
            {
                MessageBoxTD.Show("请在下方流程列表中单击选中一条流程后点击删除！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            listBox1.Items.Remove(listBox1.SelectedItem);
        }

        /// <summary>
        /// 保存参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            try
            {
                List<int> proIds = new List<int>();
                if (listBox1.Items.Count == 0)
                {
                    MessageBoxTD.Show("请至少添加一个等待的流程名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                foreach (var item in listBox1.Items)
                {
                    var pro = Solution.Instance.AllProcesses.Find(p => p.ProcessName == item.ToString());
                    if (pro != default(Process))
                    {
                        proIds.Add(pro.ID);
                    }
                }
                NodeParamWaitProcessComplete param = new NodeParamWaitProcessComplete();
                param.CurProcessName = comboBoxProcess.Text;
                param.ProcessIDs = proIds;
                Params = param;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常原因：{ex.Message}", "异常", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            Hide();
        }

        void INodeParamForm.SetNodeBelong(NodeBase node) { }
        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if(Params is NodeParamWaitProcessComplete param)
            {
                InitProcess();
                comboBoxProcess.Text = param.CurProcessName;
                listBox1.Items.Clear();
                foreach (var id in param.ProcessIDs)
                {
                    var pro = Solution.Instance.AllProcesses.Find(p => p.ID == id);
                    if (pro != default(Process))
                    {
                        listBox1.Items.Add(pro.ProcessName);
                    }
                }
            }
        }
    }
}
