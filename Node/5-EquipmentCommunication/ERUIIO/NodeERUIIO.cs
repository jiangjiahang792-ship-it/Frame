using Logger;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO
{
    public class NodeERUIIO : NodeBase
    {

        public NodeERUIIO(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormERUIIO();
            Result = new NodeResultERUIIO();
            ParamForm.SetNodeBelong(this);
        }

        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is ParamFormERUIIO form)
            {
                if (ParamForm.Params is NodeParamERUIIO param)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        if (param.Modbus == null || !param.Modbus.IsConnect) throw new Exception("Modbus设备对象为空或未连接！");

                        NodeResultERUIIO res = new NodeResultERUIIO();
                        bool[] boolV = new bool[] { };
                        switch (param.Operation)
                        {
                            case OperationType.读取输入信号:
                                do
                                {
                                    var data = param.Modbus.ReadUInt16("16", 1);
                                    boolV = Parse8BitStatus(data[0]);
                                    res.ReadDatas = boolV;
                                    res.SpecifiedBit = boolV[param.IONumber];
                                    await Task.Delay(5);
                                } while (param.IsContinue && !token.IsCancellationRequested && !boolV[param.IONumber]);
                                break;
                            case OperationType.读取输出信号:
                                do
                                {
                                    var data = param.Modbus.ReadUInt16("32", 1);
                                    boolV = Parse8BitStatus(data[0]);
                                    res.ReadDatas = boolV;
                                    res.SpecifiedBit = boolV[param.IONumber];
                                    await Task.Delay(5);
                                } while (param.IsContinue && !token.IsCancellationRequested && !boolV[param.IONumber]);
                                break;
                            case OperationType.写入输出信号:
                                ushort writeSignals;
                                if (param.IsFixed)
                                    writeSignals = BoolArrayToUshort(param.FixedSignals);
                                else
                                    writeSignals = BoolArrayToUshort(form.GetWriteValues());
                                param.Modbus.Write("32", new ushort[] { writeSignals });
                                break;
                            default:
                                break;
                        }

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败！");
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }
        /// <summary>
        /// 解析 IO 模块输入/输出8位状态
        /// </summary>
        /// <param name="inputValue">从 IO 模块读取的 ushort 类型输入状态值</param>
        /// <returns>长度为8的布尔数组，索引0-7分别对应输入口0-7，true表示有信号</returns>
        public static bool[] Parse8BitStatus(ushort inputValue)
        {
            // 取低八位数据
            byte lowByte = (byte)(inputValue & 0xFF);

            // 创建一个长度为8的布尔数组来存储每个输入口的状态
            bool[] inputStates = new bool[8];

            // 遍历低八位的每一位
            for (int i = 0; i < 8; i++)
            {
                // 判断第i位是否为1（即第i个输入口是否有信号）
                // 使用位与操作：lowByte & (1 << i)
                // 如果结果不为0，说明该位为1，对应输入口有信号
                inputStates[i] = (lowByte & (1 << i)) != 0;
            }

            return inputStates;
        }
        /// <summary>
        /// 将长度为8的布尔数组转换为 ushort 类型的 IO 状态值
        /// </summary>
        /// <param name="inputStates">长度为8的布尔数组，索引0-7分别对应输入口0-7，true表示有信号</param>
        /// <returns>ushort 类型值，低8位表示8个输入口状态</returns>
        public static ushort BoolArrayToUshort(bool[] inputStates)
        {
            // 参数校验
            if (inputStates == null)
                throw new ArgumentNullException(nameof(inputStates));

            if (inputStates.Length != 8)
                throw new ArgumentException("布尔数组长度必须为8", nameof(inputStates));

            ushort result = 0;

            // 遍历每一位，如果为 true，则将对应位设置为 1
            for (int i = 0; i < 8; i++)
            {
                if (inputStates[i])
                {
                    result |= (ushort)(1 << i);  // 使用按位或设置第 i 位
                }
            }

            return result;
        }
    }
}
