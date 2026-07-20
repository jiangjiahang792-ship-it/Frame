using Logger;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusSoftTrigger
{
    public class NodeModbusSoftTrigger: NodeBase
    {

        public NodeModbusSoftTrigger(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormModbusSoftTrigger();
            Result = new NodeReusltModbusSoftTrigger();
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

            if (ParamForm is ParamFormModbusSoftTrigger form)
            {
                if (form.Params is NodeParamModbusSoftTrigger param)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        //如果没有连接则不运行
                        if (!param.modbus.IsConnect)
                            throw new Exception("设备尚未连接！");

                        // 监听拍照信号
                        await Task.Run(() => GetShotSignalFromModbus(param, token));
                        startTime = DateTime.Now;
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
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        private async Task GetShotSignalFromModbus(NodeParamModbusSoftTrigger param, CancellationToken token)
        {
            try
            {
                bool value = false;
                // 读取拍照信号
                do
                {
                    base.CheckTokenCancel(token);
                    if (param.Type == ModbusRead.RegistersType.Bool)
                    {
                        var readResult = param.modbus.ReadBool(param.Address, 1);
                        value = readResult.Any(b => b);// 只要有一个 true 就返回 true
                    }else if (param.Type == ModbusRead.RegistersType.线圈)
                    {
                        var readResult = param.modbus.ReadCoils(param.Address, 1);
                        value = readResult.Any(b => b);// 只要有一个 true 就返回 true
                    }else if (param.Type == ModbusRead.RegistersType.离散输入)
                    {
                        var readResult = param.modbus.ReadDiscretes(param.Address, 1);
                        value = readResult.Any(b => b);// 只要有一个 true 就返回 true
                    }
                    else
                    {
                        throw new Exception("暂不支持非bool类型的地址监听信号触发！");
                    }
                    Thread.Sleep(10); 
                } while (!value);  // 读取失败或读取不到拍照信号为true均需要重试

                LogHelper.AddLog(MsgLevel.Exception, $"收到触发信号 {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);

                // 重置拍照信号，DiscreteInput只读不用重置
                if (param.Reset && param.Type == ModbusRead.RegistersType.Bool)
                {
                    param.modbus.Write(param.Address, false);
                }
                LogHelper.AddLog(MsgLevel.Info, $"相机触发信号地址“{param.Address}”获取成功！", true);
            }
            catch (OperationCanceledException ex)
            {
                throw ex;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
