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
                // 使用 HashSet 去重 (设备名 + 地址)
                var sentTargets = new HashSet<(string CommModbus, string SignalAddress)>();

                foreach (var result in aiResult)
                {
                    if (mapping.TryGetValue(result.Name, out var config))
                    {
                        // 不符合config.Judgment条件就直接跳过
                        if (!config.ShouldSend((result.IsOk && result.IsCountOk)))
                            continue;

                        var targetKey = (config.CommModbus, config.SignalAddress);
                        
                        // 如果该信号没发过才要发
                        if (!sentTargets.Contains(targetKey))
                        {
                            //await SendSignal(targetKey, isAutoReset, holdTime, sentTargets);
                             var modbus = Solution.Instance.ModbusDevices.Find(mod => mod.UserDefinedName == targetKey.Item1);
                            if (modbus == null)
                                throw new Exception($"不存在名称为{targetKey.Item1}的Modbus设备，请检查该设备是否已经被移除！");
                            ushort address = ushort.Parse(targetKey.Item2);

                            // 内部会根据RTU还是TCP类型来调用不同的写入方法
                            await modbus.WriteSingleCoilAsync(address, true);

                            // 保持设定时间后自动重置信号
                            if (isAutoReset)
                            {
                                await Task.Delay((int)holdTime).ContinueWith(t =>
                                {
                                    modbus.WriteSingleCoilAsync(address, false);
                                });
                            }
                            // 标记已发送
                            sentTargets.Add(targetKey);
                        }
                    }
                    else
                    {
                        LogHelper.AddLog(MsgLevel.Exception, $"[未配置] 检测项 [{result.Name}] 没有找到对应的 Modbus 配置", true);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { throw; }
        }
    }
    /// <summary>
    /// 检测项地址
    /// </summary>
    public class DetectItemAddress
    {
        public string CommModbus { get; set; }
        public string SignalAddress { get; set; }
        public string Judgment { get; set; }  // "检测项OK时"或“检测项NG时”

        public bool ShouldSend(bool isOk)
        {
            if (Judgment == "检测项OK时")
                return isOk;
            else if (Judgment == "检测项NG时")
                return !isOk;
            return false;
        }
    }

}
