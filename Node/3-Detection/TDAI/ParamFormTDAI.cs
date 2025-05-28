using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.SolRunParam;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal partial class ParamFormTDAI : Form, INodeParamForm
    {
        private NodeBase _node;
        /// <summary>
        /// 节点参数
        /// </summary>
        public INodeParam Params { get; set; }

        public ParamFormTDAI()
        {
            InitializeComponent();
            SolRunParamControl.RefreshParamView += SolRunParamControl_RefreshParamView;
        }
        /// <summary>
        /// 刷新从SolRunParamControl设置的参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SolRunParamControl_RefreshParamView(object sender, EventArgs e)
        {
            if(Params is NodeParamTDAI param)
            {
                uiSwitch_Learning.Active = param.IsAutoStudy;
            }
        }

        /// <summary>
        /// 用于节点参数界面需要订阅结果的情况调用
        /// </summary>
        /// <param name="node"></param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
            _node = node;
        }

        /// <summary>
        /// 获取订阅的图片
        /// </summary>
        /// <returns></returns>
        public Bitmap GetImage()
        {
            return (Bitmap)nodeSubscription1.GetValue<Bitmap>().Clone();
        }

        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if (Params is NodeParamTDAI param)
            {
                // 还原界面显示
                nodeSubscription1.SetText(param.Text1, param.Text2);
                textBoxConfigPath.Text = param.ConfigPath;
            }
        }

        /// <summary>
        /// 点击选择标签文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bt_chooseLabel_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Json文件|*.json";
            openFileDialog1.Title = "选择配置文件";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                this.textBoxConfigPath.Text = openFileDialog1.FileName;
            }
        }
        /// <summary>
        /// 设置参数
        /// </summary>
        /// <returns></returns>
        private bool SaveParams()
        {
            try
            {
                if (string.IsNullOrEmpty(nodeSubscription1.GetText1())
                || string.IsNullOrEmpty(nodeSubscription1.GetText2())
                || string.IsNullOrEmpty(textBoxConfigPath.Text)
                )
                {
                    throw new Exception();
                }
                NodeParamTDAI nodeParamTDAI = new NodeParamTDAI();
                nodeParamTDAI.Text1 = nodeSubscription1.GetText1();
                nodeParamTDAI.Text2 = nodeSubscription1.GetText2();
                nodeParamTDAI.ConfigPath = textBoxConfigPath.Text;
                nodeParamTDAI.AIInputInfo = new AIInputInfo(textBoxConfigPath.Text);
                nodeParamTDAI.IsAutoStudy = uiSwitch_Learning.Active;
                Params = nodeParamTDAI;
            }
            catch (Exception)
            {
                MessageBox.Show("参数设置异常，请检查参数设置是否合理！");
                return false;
            }
            return true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }
    }
}
