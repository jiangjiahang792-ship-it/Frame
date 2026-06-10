using Logger;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite
{
    public class NodeModbusWrite : NodeBase
    {
        private Process _process;
        public NodeModbusWrite(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new ParamFormModbusWrite();
            form.RunHandler += RunHandler;
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultModbusWrite();
            _process = process;
        }

        private async Task RunHandler(object sender, EventArgs e)
        {
            await Run(CancellationToken.None, _process != null && _process.ShowLog && OutputLog);
        }

        /// <summary>
        /// 节点运行
        /// </summary>
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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            LogHelper.AddLog(MsgLevel.Fatal, $"流程{Process.ProcessName}Modbus发送信号开始{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
            var param = (NodeParamModbusWrite)ParamForm.Params;

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                //如果没有连接则不运行
                if (param.Device == null || !param.Device.IsConnect)
                    throw new Exception("Modbus设备资源已释放或尚未连接！");

                //如果是订阅的数据，需要先获取订阅的值
                if (param.IsSubscribed)
                    param.Data = ((ParamFormModbusWrite)ParamForm).GetSubValue();

                var modbus = param.Device as IModbus;
                switch (param.DataType)
                {
                    case RegistersType.Bool:
                        {
                            string[] datas = param.Data.Split(',');
                            bool[] boolArray = datas.Select(part => part.Trim() != "0").ToArray();
                            modbus.Write(param.StartAddress, boolArray);
                        }
                        break;

                    case RegistersType.Short:
                        {
                            string[] parts = param.Data.Split(',');
                            short[] shortArray = Array.ConvertAll(parts, part => short.Parse(part.Trim()));
                            modbus.Write(param.StartAddress, shortArray);
                        }
                        break;

                    case RegistersType.UShort:
                        {
                            string[] parts = param.Data.Split(',');
                            ushort[] ushortArray = Array.ConvertAll(parts, part => ushort.Parse(part.Trim()));
                            modbus.Write(param.StartAddress, ushortArray);
                        }
                        break;

                    case RegistersType.Int:
                        {
                            string[] parts = param.Data.Split(',');
                            int[] intArray = Array.ConvertAll(parts, part => int.Parse(part.Trim()));
                            modbus.Write(param.StartAddress, intArray);
                        }
                        break;

                    case RegistersType.UInt:
                        {
                            string[] parts = param.Data.Split(',');
                            uint[] uintArray = Array.ConvertAll(parts, part => uint.Parse(part.Trim()));
                            modbus.Write(param.StartAddress, uintArray);
                        }
                        break;

                    case RegistersType.Float:
                        {
                            string[] parts = param.Data.Split(',');
                            float[] floatArray = Array.ConvertAll(parts, part => float.Parse(part.Trim(), System.Globalization.CultureInfo.InvariantCulture));
                            modbus.Write(param.StartAddress, floatArray);
                        }
                        break;

                    case RegistersType.Double:
                        {
                            string[] parts = param.Data.Split(',');
                            double[] doubleArray = Array.ConvertAll(parts, part => double.Parse(part.Trim(), System.Globalization.CultureInfo.InvariantCulture));
                            modbus.Write(param.StartAddress, doubleArray);
                        }
                        break;

                    case RegistersType.Long:
                        {
                            string[] parts = param.Data.Split(',');
                            long[] longArray = Array.ConvertAll(parts, part => long.Parse(part.Trim()));
                            modbus.Write(param.StartAddress, longArray);
                        }
                        break;

                    case RegistersType.ULong:
                        {
                            string[] parts = param.Data.Split(',');
                            ulong[] ulongArray = Array.ConvertAll(parts, part => ulong.Parse(part.Trim()));
                            modbus.Write(param.StartAddress, ulongArray);
                        }
                        break;

                    case RegistersType.线圈:
                        {
                            string[] datas = param.Data.Split(',');
                            bool[] boolArray = datas.Select(part => part.Trim() != "0").ToArray();
                            modbus.Write(param.StartAddress, boolArray);
                        }
                        break;

                    default:
                        throw new NotSupportedException($"不支持的数据类型: {param.DataType}");
                }

                LogHelper.AddLog(MsgLevel.Fatal, $"流程{Process.ProcessName}Modbus发送信号完毕{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);

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
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }
    }
}
