using Logger;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._5_EquipmentCommunication.AIResultSendByModbus
{
    internal class NodeSignalSendByModbus : NodeBase
    {
        public NodeSignalSendByModbus(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormSignalSendByModbus();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultSignalSendByModbus();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task Run(CancellationToken token)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return;
            }


            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            var param = (NodeParamSignalSendByModbus)ParamForm.Params;

            if(ParamForm is ParamFormSignalSendByModbus form)
            {
                try
                {
                    // 初始化运行状态
                    SetStatus(NodeStatus.Unexecuted, "*");
                    base.CheckTokenCancel(token);

                    // 获取AI检测结果
                    List<DetectItemResult> aiResult = form.GetAiResult();

                    // 获取“检测项-地址”映射关系
                    var map = form.GetMapping();

                    // 获取信号配置
                    BindingList<DetectItemRow> signalConfigs = param.BindingData;

                    #region 判断检测项是否配置了信号

                    var (isExist, missingNames) = CheckAiResultExistInSignalConfig(aiResult, signalConfigs);
                    if(!isExist)
                    {
                        string str = "";
                        for (int i = 0; i < missingNames.Count; i++)
                        {
                            if (i != (missingNames.Count - 1))
                                str += $"“{missingNames[i]}”、";
                            else
                                str += $"“{missingNames[i]}”";
                        }
                        LogHelper.AddLog(MsgLevel.Exception, $"识别到检测项{str}尚未配置信号！");
                        throw new Exception($"识别到检测项{str}尚未配置信号！");
                    }

                    #endregion

                    // 执行线圈信号写入
                    await SendToModbus(aiResult, map, param.SignalHoldTime, param.IsAutoReset, token);
                    //if (aiResult.TrueForAll(i => (i.IsOk && i.IsCountOk)))
                    //{
                    //    var modbus = param.ModbusDevice;
                    //    if (modbus == null)
                    //        throw new Exception($"不存在的Modbus设备，请检查设备是否已经被移除！");
                    //    ushort address = ushort.Parse(targetKey.Item2);

                    //    // 内部会根据RTU还是TCP类型来调用不同的写入方法
                    //    await modbus.WriteSingleCoilAsync(address, config.Value);

                    //    // 保持设定时间后自动重置信号
                    //    if (param.IsAutoReset)
                    //    {
                    //        await Task.Delay((int)param.SignalHoldTime).ContinueWith(t =>
                    //        {
                    //            modbus.WriteSingleCoilAsync(address, !config.Value);
                    //        });
                    //    }
                    //}

                    long time = SetRunResult(startTime, NodeStatus.Successful);
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
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

        /// <summary>
        /// 判断检出结果是否配置了信号
        /// </summary>
        /// <param name="aiResult"></param>
        /// <param name="signalConfigs"></param>
        private (bool, List<string>) CheckAiResultExistInSignalConfig(List<DetectItemResult> aiResult, BindingList<DetectItemRow> signalConfigs)
        {
            // 收集所有存在的检测项名称（支持多个用 ; 分隔的情况）
            var allValidNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in signalConfigs)
            {
                if (!string.IsNullOrWhiteSpace(row.DetectItems))
                {
                    string[] items = row.DetectItems.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in items)
                    {
                        string trimmed = item.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            allValidNames.Add(trimmed);
                        }
                    }
                }
            }

            // 检查 aiResult 中的 Name 是否都在 allValidNames 中
            var missingNames = aiResult
                .Where(r => !allValidNames.Contains(r.Name.Trim()))
                .Select(r => r.Name)
                .ToList();

            if (missingNames.Count == 0)
            {
                return (true, new List<string>());
            }
            else
            {
                return (false, missingNames);
            }
        }

        /// <summary>
        /// 发送检测项对应的modbus信号
        /// </summary>
        /// <param name="detectedItems"></param>
        /// <param name="mapping"></param>
        /// <param name=""></param>
        /// <param name="time"></param>
        /// <param name="token"></param>
        private async Task SendToModbus(List<DetectItemResult> aiResult, Dictionary<string, DetectItemAddress> mapping, double holdTime, bool isAutoReset, CancellationToken token)
        {
            try
            {
                // 使用字典来按 (设备名, 地址) 分组
                var grouped = new Dictionary<(string CommModbus, string SignalAddress), List<DetectItemResult>>();

                foreach (var result in aiResult)
                {
                    if (!mapping.TryGetValue(result.Name, out var config))
                    {
                        LogHelper.AddLog(MsgLevel.Exception, $"[未配置] 检测项 [{result.Name}] 没有找到对应的 Modbus 配置", true);
                        continue;
                    }

                    var key = (config.CommModbus, config.SignalAddress);

                    if (!grouped.ContainsKey(key))
                    {
                        grouped[key] = new List<DetectItemResult>();
                    }

                    grouped[key].Add(result);
                }

                // 遍历每个分组，判断是否所有项都 OK
                foreach (var group in grouped)
                {
                    var key = group.Key;
                    var results = group.Value;

                    // 判断这个组中是否所有检测项都通过
                    bool allOk = results.All(r => r.IsOk && r.IsCountOk);

                    if (!allOk)
                        continue;

                    // 获取 Modbus 设备
                    var modbus = Solution.Instance.ModbusDevices.Find(mod => mod.UserDefinedName == key.CommModbus);
                    if (modbus == null)
                        throw new Exception($"不存在名称为 {key.CommModbus} 的Modbus设备，请检查该设备是否已经被移除！");

                    // 解析地址
                    if (!ushort.TryParse(key.SignalAddress, out var address))
                        throw new FormatException($"无法解析地址 [{key.SignalAddress}]");

                    // 获取写入值（取任意一个的值即可，因为它们共用同一地址）
                    var value = mapping.FirstOrDefault(x => x.Key == results[0].Name).Value?.Value ?? true;

                    // 写入信号
                    await modbus.WriteSingleCoilAsync(address, value);

                    // 自动复位
                    if (isAutoReset)
                    {
                        int delayMs = (int)holdTime;
                        await Task.Delay(delayMs, token); // 支持取消
                        await modbus.WriteSingleCoilAsync(address, !value);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, "Modbus发送被取消");
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"Modbus发送异常：{ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
    }
    /// <summary>
    /// 检测项地址
    /// </summary>
    public class DetectItemAddress
    {
        /// <summary>
        /// Modbus设备名
        /// </summary>
        public string CommModbus { get; set; }
        /// <summary>
        /// 信号地址
        /// </summary>
        public string SignalAddress { get; set; }
        /// <summary>
        /// 线圈值
        /// </summary>
        public bool Value {  get; set; }
    }

}
