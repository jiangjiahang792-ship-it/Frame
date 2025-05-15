using System;
using System.Drawing;
using System.Windows.Forms;
using Logger;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal partial class ParamFormTDAI : Form, INodeParamForm
    {
        /// <summary>
        /// 不管加载几次，AI模型个数只增加1
        /// </summary>
        private bool flag = false;
        private readonly static object _lockObject = new object();

        private NodeBase _node;
        /// <summary>
        /// 节点参数
        /// </summary>
        public INodeParam Params { get; set; }

        public Yolov8Base Yolov8 = new Yolov8Base();

        public ParamFormTDAI()
        {
            InitializeComponent();
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


        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "xml文件|*.xml";
            openFileDialog1.Title = "选择模型文件";
            string modelOldPath = this.tbModelFlie.Text;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (modelOldPath != openFileDialog1.FileName)
                {
                    this.tbModelFlie.Text = openFileDialog1.FileName;
                    modelOldPath = openFileDialog1.FileName;
                }
            }
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
                textBoxLabel.Text = param.LabelPath;
                tbModelFlie.Text = param.ModelPath;
                comboBoxModelType.Text = param.ModelType;
                //TODO:还原label参数
            }
        }

        /// <summary>
        /// 点击选择标签文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bt_chooseLabel_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "label文件|*.label";
            openFileDialog1.Title = "选择标签文件";
            string labelOldPath = this.textBoxLabel.Text;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (labelOldPath != openFileDialog1.FileName)
                {
                    this.textBoxLabel.Text = openFileDialog1.FileName;
                    tdjsParameterGrid1.LoadLabel(openFileDialog1.FileName);
                    labelOldPath = openFileDialog1.FileName;
                }
            }
        }

        /// <summary>
        /// 开始加载模型
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonModelLoad_Click(object sender, EventArgs e)
        {
            try
            {
                switch (comboBoxModelType.SelectedIndex)
                {
                    case 0:
                        Yolov8 = new Yolov8Det(tbModelFlie.Text);
                        break;
                    case 1:
                        Yolov8 = new Yolov8DetXX(tbModelFlie.Text);
                        break;
                    case 2:
                        Yolov8 = new Yolov8Obb(tbModelFlie.Text);
                        break;
                    case 3:
                        Yolov8 = new Yolov8Seg(tbModelFlie.Text);
                        break;
                    default:
                        throw new Exception("非法的模型种类索引！");
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, "加载模型失败，原因：" + ex.Message, true);
            }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {

        }
    }
}
