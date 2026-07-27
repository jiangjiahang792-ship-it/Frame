using System;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.LargeModel;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.Unsupervised;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;

namespace TDJS_Vision.Node._6_LogicTool.MessageBox
{
    public partial class NodeParamFormMessageBox : FormBase, INodeParamForm
    {
        public NodeParamFormMessageBox()
        {
            InitializeComponent();
        }

        public INodeParam Params { get; set; }
        /// <summary>
        /// 获取订阅的值
        /// </summary>
        /// <returns></returns>
        public bool GetSubValue()
        {
            try
            {
                bool result = false;
                switch (nodeSubscription1.GetNodeType())
                {
                    case NodeType.AITD:
                        result = nodeSubscription1.GetValue<AlgorithmResult>().IsAllOk;
                        break;
                    case NodeType.UnsupervisedDetection:
                        var unsupervisedResult = nodeSubscription1.GetSelectedResult<NodeResultUnsupervisedDetection>().AlgorithmResult;
                        if (unsupervisedResult == null)
                            throw new Exception("无监督输出结果为空，无法进行条件判断！");
                        result = unsupervisedResult.IsAllOk;
                        break;
                    case NodeType.LargeModelDetection:
                        var largeModelResult = nodeSubscription1.GetSelectedResult<NodeResultLargeModelDetection>().AlgorithmResult;
                        if (largeModelResult == null)
                            throw new Exception("大模型输出结果为空，无法进行条件判断！");
                        result = largeModelResult.IsAllOk;
                        break;
                    case NodeType.Summarize:
                        result = nodeSubscription1.GetValue<AlgorithmResult>().IsAllOk;
                        break;
                    case NodeType.PLCRead:
                        var data1 = nodeSubscription1.GetValue<PlcReadResult>();
                        if (data1.DataType == typeof(bool[]).Name)
                            result = ((bool[])data1.Data)[0];
                        else
                            result = (bool)nodeSubscription1.GetValue<PlcReadResult>().Data;
                        break;
                    case NodeType.ModbusRead:
                        var data2 = nodeSubscription1.GetValue<ModbusReadResult>();
                        if (data2.DataType == typeof(bool[]).Name)
                            result = ((bool[])data2.Data)[0];
                        else
                            result = (bool)nodeSubscription1.GetValue<ModbusReadResult>().Data;
                        break;
                    case NodeType.SharedVariable:
                        var sharedVar = nodeSubscription1.GetValue<SharedVarValue>();
                        if (sharedVar.Type == typeof(bool))
                            result = (bool)sharedVar.Data;
                        else if (sharedVar.Type == typeof(AlgorithmResult))
                            result = ((AlgorithmResult)sharedVar.Data).IsAllOk;
                        else
                            throw new Exception($"该共享变量类型不支持条件判断！当前类型：{sharedVar.Type}");
                        break;
                    default:
                        break;
                }
                return result;
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                NodeParamMessageBox nodeParamMessageBox = new NodeParamMessageBox();
                nodeParamMessageBox.Text1 = nodeSubscription1.GetText1();
                nodeParamMessageBox.Text2 = nodeSubscription1.GetText2();
                nodeParamMessageBox.Message = textBox1.Text;
                nodeParamMessageBox.ShowCondition = radioButton1.Checked;
                Params = nodeParamMessageBox;
            }
            catch (Exception)
            {
                MessageBoxTD.Show("参数设置异常！", "异常", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            Hide();
        }

        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetInputContract(SubscriptionInputContract.ForCategories(new[]
            {
                SubscriptionDataCategory.AlgorithmResult,
                SubscriptionDataCategory.StructuredObject
            }));
            nodeSubscription1.Init(node);
        }
        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if(Params is NodeParamMessageBox param)
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                radioButton1.Checked = param.ShowCondition;
                radioButton2.Checked = !param.ShowCondition;
                textBox1.Text = param.Message;
            }
        }
    }
}
