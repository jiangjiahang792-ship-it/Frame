using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Modbus;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead
{
    /// <summary>
    /// Modbus 读取节点，负责读取一段寄存器或线圈并发布可订阅的单值结果。
    /// </summary>
    public class NodeModbusRead : NodeBase, IDynamicResultVariableProvider, IDynamicResultVariableTypeProvider
    {
        private Process _process;
        public NodeModbusRead(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new ParamFormModbusRead();
            form.RunHandler += RunHandler;
            ParamForm = form;
            Result = new NodeResultModbusRead();
            _process = process;
        }

        /// <summary>
        /// 根据当前读取参数预先声明每个可订阅的 Modbus 值。
        /// </summary>
        public IEnumerable<string> GetDynamicResultVariableNames()
        {
            NodeParamModbusRead param = ParamForm == null ? null : ParamForm.Params as NodeParamModbusRead;
            if (param == null)
                return new string[0];

            return ModbusReadDynamicVariable.BuildNames(param.StartAddress, param.OutputValueCount);
        }

        /// <summary>
        /// 获取 Modbus 单值订阅变量的真实类型。
        /// </summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = typeof(object);
            NodeParamModbusRead param = ParamForm == null ? null : ParamForm.Params as NodeParamModbusRead;
            if (param == null ||
                !ModbusReadDynamicVariable.ContainsVariable(param.StartAddress, param.OutputValueCount, variableName))
            {
                return false;
            }

            valueType = ModbusReadDynamicVariable.GetValueType(param.DataType);
            return true;
        }

        /// <summary>
        /// 节点界面点击执行Modbus读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task RunHandler(object sender, EventArgs e)
        {
            await Run(CancellationToken.None, _process != null && _process.ShowLog && OutputLog);
            // 手动执行后在界面显示读取值；自动运行不更新界面，避免增加每帧开销。
            NodeResultModbusRead result = Result as NodeResultModbusRead;
            if (ParamForm is ParamFormModbusRead form && result?.ReadData != null)
                form.SetReadResult(result.ReadData.StartAddress + ": " + ArrayObjectToString(result.ReadData.Data));
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            // 每次读取先清除上次结果，取消或解码失败时不能让下游使用上一件的条码。
            if (Result is NodeResultModbusRead previousResult)
                previousResult.ReadData = null;

            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            var param = (NodeParamModbusRead)ParamForm.Params;
            var modbus = param.Device as IModbus;
            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                //如果没有连接则不运行
                if (param.Device == null || !param.Device.IsConnect)
                    throw new Exception("Modbus设备资源已释放或尚未连接！");

                object data = null;
                switch (param.DataType)
                {
                    case RegistersType.Bool:
                        data = modbus.ReadBool(param.StartAddress, param.Count);
                        break;
                    case RegistersType.Short:
                        data = modbus.ReadInt16(param.StartAddress, param.Count);
                        break;
                    case RegistersType.UShort:
                        data = modbus.ReadUInt16(param.StartAddress, param.Count);
                        break;
                    case RegistersType.Int:
                        data = modbus.ReadInt32(param.StartAddress, param.Count);
                        break;
                    case RegistersType.UInt:
                        data = modbus.ReadUInt32(param.StartAddress, param.Count);
                        break;
                    case RegistersType.Float:
                        data = modbus.ReadFloat(param.StartAddress, param.Count);
                        break;
                    case RegistersType.Double:
                        data = modbus.ReadDouble(param.StartAddress, param.Count);
                        break;
                    case RegistersType.Long:
                        data = modbus.ReadInt64(param.StartAddress, param.Count);
                        break;
                    case RegistersType.ULong:
                        data = modbus.ReadUInt64(param.StartAddress, param.Count);
                        break;
                    case RegistersType.线圈:
                        data = modbus.ReadCoils(param.StartAddress, param.Count);
                        break;
                    case RegistersType.离散输入:
                        data = modbus.ReadDiscretes(param.StartAddress, param.Count);
                        break;
                    case RegistersType.String:
                        // 沿用结果数组协议，仅发布一个完整字符串，避免把寄存器数量当成字符串数量。
                        data = new[] { modbus.ReadString(param.StartAddress, param.Count,
                            param.StringEncodingName, param.StringLowByteFirst) };
                        break;
                    default:
                        throw new InvalidOperationException("不支持的 Modbus 读取类型。");
                }



                if (Result is NodeResultModbusRead result)
                {
                    result.ReadData = new ModbusReadResult(
                        data,
                        ModbusReadDynamicVariable.GetArrayTypeName(param.DataType),
                        param.StartAddress);
                    Result = result;
                }
                var time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                if(showLog)
                {
                    string resultStr = ArrayObjectToString(data);
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms, 读取结果：{resultStr})", true);
                }
                return new NodeReturn(NodeRunFlag.ContinueRun);

            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                SetRunResult(startTime, NodeStatus.Unexecuted);
                throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }

        /// <summary>
        /// 将数组对象转换为字符串表示形式
        /// </summary>
        /// <param name="arrayObject"></param>
        /// <returns></returns>
        public static string ArrayObjectToString(object arrayObject)
        {
            if (arrayObject == null)
                return "[]";

            // 确保是一个数组
            if (!arrayObject.GetType().IsArray)
                return arrayObject.ToString(); // 或抛异常

            var array = arrayObject as Array;
            if (array.Length == 0)
                return "[]";

            // 根据数组元素类型进行转换
            var elementType = array.GetType().GetElementType();

            switch (elementType.Name)
            {
                case "Boolean":
                    var boolArray = (bool[])array;
                    return "[" + string.Join(",", boolArray.Select(b => b.ToString())) + "]";

                case "Int16":
                    var shortArray = (short[])array;
                    return "[" + string.Join(",", shortArray) + "]";

                case "UInt16":
                    var ushortArray = (ushort[])array;
                    return "[" + string.Join(",", ushortArray) + "]";

                case "Int32":
                    var intArray = (int[])array;
                    return "[" + string.Join(",", intArray) + "]";

                case "UInt32":
                    var uintArray = (uint[])array;
                    return "[" + string.Join(",", uintArray) + "]";

                case "Single":
                    var floatArray = (float[])array;
                    // 使用 G 格式化避免多余的小数位
                    return "[" + string.Join(",", floatArray.Select(f => f.ToString("G"))) + "]";

                case "Double":
                    var doubleArray = (double[])array;
                    return "[" + string.Join(",", doubleArray.Select(d => d.ToString("G"))) + "]";

                case "Int64":
                    var longArray = (long[])array;
                    return "[" + string.Join(",", longArray) + "]";

                case "UInt64":
                    var ulongArray = (ulong[])array;
                    return "[" + string.Join(",", ulongArray) + "]";

                default:
                    // 通用 fallback（适用于未知类型）
                    return "[" + string.Join(",", array.Cast<object>()) + "]";
            }
        }
    }
}
