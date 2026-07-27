using System;
using System.Windows.Forms;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO
{
    public partial class GetIOValue : UserControl
    {
        public GetIOValue()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 初始化订阅节点控件
        /// </summary>
        /// <param name="node"></param>
        public void Init(NodeBase node)
        {
            SubscriptionInputContract inputContract = SubscriptionInputContract.ForCategories(new[]
            {
                SubscriptionDataCategory.AlgorithmResult,
                SubscriptionDataCategory.StructuredObject
            });
            nodeSubscription1.SetInputContract(inputContract);
            nodeSubscription2.SetInputContract(inputContract);
            nodeSubscription3.SetInputContract(inputContract);
            nodeSubscription4.SetInputContract(inputContract);
            nodeSubscription5.SetInputContract(inputContract);
            nodeSubscription6.SetInputContract(inputContract);
            nodeSubscription7.SetInputContract(inputContract);
            nodeSubscription8.SetInputContract(inputContract);
            nodeSubscription1.Init(node);
            nodeSubscription2.Init(node);
            nodeSubscription3.Init(node);
            nodeSubscription4.Init(node);
            nodeSubscription5.Init(node);
            nodeSubscription6.Init(node);
            nodeSubscription7.Init(node);
            nodeSubscription8.Init(node);
        }
        /// <summary>
        /// 获取界面参数
        /// </summary>
        /// <returns></returns>
        public IOSettings GetIOSettings()
        {
            IOSettings iOSettings = new IOSettings();
            iOSettings.UseSubscription1 = checkBox1.Checked;
            iOSettings.UseSubscription2 = checkBox2.Checked;
            iOSettings.UseSubscription3 = checkBox3.Checked;
            iOSettings.UseSubscription4 = checkBox4.Checked;
            iOSettings.UseSubscription5 = checkBox5.Checked;
            iOSettings.UseSubscription6 = checkBox6.Checked;
            iOSettings.UseSubscription7 = checkBox7.Checked;
            iOSettings.UseSubscription8 = checkBox8.Checked;
            iOSettings.V1 = checkBox9.Checked;
            iOSettings.V2 = checkBox10.Checked;
            iOSettings.V3 = checkBox11.Checked;
            iOSettings.V4 = checkBox12.Checked;
            iOSettings.V5 = checkBox13.Checked;
            iOSettings.V6 = checkBox14.Checked;
            iOSettings.V7 = checkBox15.Checked;
            iOSettings.V8 = checkBox16.Checked;
            iOSettings.Text1_1 = nodeSubscription1.GetText1();
            iOSettings.Text1_2 = nodeSubscription1.GetText2();
            iOSettings.Text2_1 = nodeSubscription2.GetText1();
            iOSettings.Text2_2 = nodeSubscription2.GetText2();
            iOSettings.Text3_1 = nodeSubscription3.GetText1();
            iOSettings.Text3_2 = nodeSubscription3.GetText2();
            iOSettings.Text4_1 = nodeSubscription4.GetText1();
            iOSettings.Text4_2 = nodeSubscription4.GetText2();
            iOSettings.Text5_1 = nodeSubscription5.GetText1();
            iOSettings.Text5_2 = nodeSubscription5.GetText2();
            iOSettings.Text6_1 = nodeSubscription6.GetText1();
            iOSettings.Text6_2 = nodeSubscription6.GetText2();
            iOSettings.Text7_1 = nodeSubscription7.GetText1();
            iOSettings.Text7_2 = nodeSubscription7.GetText2();
            iOSettings.Text8_1 = nodeSubscription8.GetText1();
            iOSettings.Text8_2 = nodeSubscription8.GetText2();
            return iOSettings;
        }
        /// <summary>
        /// 设置界面参数
        /// </summary>
        /// <param name="iOSettings"></param>
        public void SetIOSettings(IOSettings iOSettings)
        {
            checkBox1.Checked = iOSettings.UseSubscription1;
            checkBox2.Checked = iOSettings.UseSubscription2;
            checkBox3.Checked = iOSettings.UseSubscription3;
            checkBox4.Checked = iOSettings.UseSubscription4;
            checkBox5.Checked = iOSettings.UseSubscription5;
            checkBox6.Checked = iOSettings.UseSubscription6;
            checkBox7.Checked = iOSettings.UseSubscription7;
            checkBox8.Checked = iOSettings.UseSubscription8;
            checkBox9.Checked = iOSettings.V1;
            checkBox10.Checked = iOSettings.V2;
            checkBox11.Checked = iOSettings.V3;
            checkBox12.Checked = iOSettings.V4;
            checkBox13.Checked = iOSettings.V5;
            checkBox14.Checked = iOSettings.V6;
            checkBox15.Checked = iOSettings.V7;
            checkBox16.Checked = iOSettings.V8;
            nodeSubscription1.SetText(iOSettings.Text1_1, iOSettings.Text1_2);
            nodeSubscription2.SetText(iOSettings.Text2_1, iOSettings.Text2_2);
            nodeSubscription3.SetText(iOSettings.Text3_1, iOSettings.Text3_2);
            nodeSubscription4.SetText(iOSettings.Text4_1, iOSettings.Text4_2);
            nodeSubscription5.SetText(iOSettings.Text5_1, iOSettings.Text5_2);
            nodeSubscription6.SetText(iOSettings.Text6_1, iOSettings.Text6_2);
            nodeSubscription7.SetText(iOSettings.Text7_1, iOSettings.Text7_2);
            nodeSubscription8.SetText(iOSettings.Text8_1, iOSettings.Text8_2);
        }
        /// <summary>
        /// 获取信号值
        /// </summary>
        /// <returns></returns>
        public bool[] GetValues()
        {
            bool[] result = new bool[8];
            try
            {
                result = new bool[8]
                {
                    checkBox1.Checked? GetNodeResult(nodeSubscription1) : checkBox9.Checked,
                    checkBox2.Checked? GetNodeResult(nodeSubscription2) : checkBox10.Checked,
                    checkBox3.Checked? GetNodeResult(nodeSubscription3) : checkBox11.Checked,
                    checkBox4.Checked? GetNodeResult(nodeSubscription4) : checkBox12.Checked,
                    checkBox5.Checked? GetNodeResult(nodeSubscription5) : checkBox13.Checked,
                    checkBox6.Checked? GetNodeResult(nodeSubscription6) : checkBox14.Checked,
                    checkBox7.Checked? GetNodeResult(nodeSubscription7) : checkBox15.Checked,
                    checkBox8.Checked? GetNodeResult(nodeSubscription8) : checkBox16.Checked
                };
            }
            catch (Exception)
            {
                throw;
            }
            return result;

        }
        /// <summary>
        /// 获取订阅的bool值
        /// </summary>
        private static bool GetNodeResult(NodeSubscription nodeSubscription)
        {
            if (nodeSubscription == null)
                return false; // 或 throw, 根据你的需求

            switch (nodeSubscription.GetNodeType())
            {
                case NodeType.AITD:
                    return nodeSubscription.GetValue<AlgorithmResult>().IsAllOk;

                case NodeType.PLCRead:
                    return (bool)nodeSubscription.GetValue<PlcReadResult>().Data;

                case NodeType.ModbusRead:
                    return (bool)nodeSubscription.GetValue<ModbusReadResult>().Data;

                case NodeType.SharedVariable:
                    var sharedVar = nodeSubscription.GetValue<SharedVarValue>();
                    if (sharedVar.Type == typeof(bool))
                        return (bool)sharedVar.Data;
                    else if (sharedVar.Type == typeof(AlgorithmResult))
                        return ((AlgorithmResult)sharedVar.Data).IsAllOk;
                    else
                        throw new Exception($"该共享变量类型不支持条件判断！当前类型：{sharedVar.Type}");

                default:
                    // 可根据需求返回 false 或抛出异常
                    return false;
            }
        }
        /// <summary>
        /// 复选框状态改变
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if(sender is CheckBox box)
            {
                switch (box.Name)
                {
                    case "checkBox1":
                        checkBox9.Enabled = !box.Checked;
                        nodeSubscription1.Enabled = box.Checked;
                        break;
                    case "checkBox2":
                        checkBox10.Enabled = !box.Checked;
                        nodeSubscription2.Enabled = box.Checked;
                        break;
                    case "checkBox3":
                        checkBox11.Enabled = !box.Checked;
                        nodeSubscription3.Enabled = box.Checked;
                        break;
                    case "checkBox4":
                        checkBox12.Enabled = !box.Checked;
                        nodeSubscription4.Enabled = box.Checked;
                        break;
                    case "checkBox5":
                        checkBox13.Enabled = !box.Checked;
                        nodeSubscription5.Enabled = box.Checked;
                        break;
                    case "checkBox6":
                        checkBox14.Enabled = !box.Checked;
                        nodeSubscription6.Enabled = box.Checked;
                        break;
                    case "checkBox7":
                        checkBox15.Enabled = !box.Checked;
                        nodeSubscription7.Enabled = box.Checked;
                        break;
                    case "checkBox8":
                        checkBox16.Enabled = !box.Checked;
                        nodeSubscription8.Enabled = box.Checked;
                        break;
                    default:
                        break;
                }
            }
        }
    }
    /// <summary>
    /// 界面参数设置
    /// </summary>
    public struct IOSettings 
    {
        public bool UseSubscription1;
        public bool UseSubscription2;
        public bool UseSubscription3;
        public bool UseSubscription4;
        public bool UseSubscription5;
        public bool UseSubscription6;
        public bool UseSubscription7;
        public bool UseSubscription8;
        public bool V1;
        public bool V2;
        public bool V3;
        public bool V4;
        public bool V5;
        public bool V6;
        public bool V7;
        public bool V8;
        public string Text1_1;
        public string Text1_2;
        public string Text2_1;
        public string Text2_2;
        public string Text3_1;
        public string Text3_2;
        public string Text4_1;
        public string Text4_2;
        public string Text5_1;
        public string Text5_2;
        public string Text6_1;
        public string Text6_2;
        public string Text7_1;
        public string Text7_2;
        public string Text8_1;
        public string Text8_2;
    }

}
