using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    public class NodeTDAI : NodeBase
    {
        public NodeTDAI(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormTDAI();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultTDAI();

            
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

            if(ParamForm is ParamFormTDAI form)
            {
                if (form.Params is NodeParamTDAI param)
                {
                    if (Result is NodeResultTDAI res)
                    {
                        try
                        {
                            SetStatus(NodeStatus.Unexecuted, "*");
                            base.CheckTokenCancel(token);

                            await EnsureModelReadyAsync(param);

                            // 清空上次检测结果
                            res.AlgorithmResult.Clear();

                            // 获取图像
                            OutputImage inputImage = form.GetOutputImage();

                            #region 通信获取对应检测项的配置(不是使用固定检测项时)

                            // terminalType端子型号、wireType电线型号、waterproofType防水栓型号
                            short terminalType = 0, wireType = 0, waterproofType = 0;
                            if (!param.IsFixed)
                            {
                                // 使用 ASCII 解码
                                var encoding = Encoding.ASCII;
                                string configName = "";
                                if (param.Device is IPlc plc2)
                                {
//                                    short[] data;
//                                    if (param.ModelName.ToString().Contains("线芯"))
//                                    {
//                                        (terminalType, wireType, waterproofType) = await GetTypeCode(plc2, "D1601");
//                                    }
//                                    else if (param.ModelName.ToString().Contains("端子"))
//                                    {
//                                        (terminalType, wireType, waterproofType) = await GetTypeCode(plc2, "D1631");
//                                    }
//                                    else
//                                    {
//                                        throw new Exception("模型名称不包含关键字：线芯或端子，无法通过PLC获取对应的检测项配置！");
//                                    }
//#if DEBUG
//                                    LogHelper.AddLog(MsgLevel.Debug, $"端子型号({terminalType})，电线型号({wireType})，防水栓型号({waterproofType})", true);
//#endif
//                                    configName = $"{terminalType}_{wireType}_{waterproofType}（{param.ModelName}）";
                                }
                                else if (param.Device is IModbus mod)
                                {
                                    ushort readValue = await GetModbusCode(mod, param.Adress);
                                    string strVal = readValue.ToString();

                                    try
                                    {
                                        var matchModel = param.TDAICommuntionParams.First(m => m.TriggerVal == strVal);
                                        param.CurDetectItemName = matchModel.DetectionName;
                                    }
                                    catch (Exception ex){ 
                                        
                                    }
                                }
                                else
                                {
                                    throw new Exception("暂时不支持的通信设备来获取检测项配置！");
                                }

                                //if (!Solution.Instance.DetectItemDic.ContainsKey(configName))
                                //{
                                //    // 不存在对应的检测项配置，需要复制一个选定的配置赋值
                                //    Solution.Instance.DetectItemDic[configName] = Solution.Instance.DetectItemDic[param.DetectItemName2].ToList();
                                //}
                                //param.CurDetectItemName = configName;

                            }

                            #endregion

                            // 4种模型检出结果解析
                            int result_count = 0;
                            int keypoint_count = 0;

                            switch (param.Yolo8.ModelType)
                            {
                                case ModelType.DET:

                                    #region 执行推理

                                    List<DetResult> det_results = new List<DetResult>();

                                    var handleDet = param.Yolo8 as Yolo8Det;
                                    if (handleDet == null)
                                        throw new Exception("AI模型句柄类型与DET模型不匹配，请重新加载模型！");
                                    for (int i = 0; i < inputImage.Bitmaps.Count; i++)
                                    {
                                        List<DetResult> tmp = new List<DetResult>();
                                        var img = inputImage.Bitmaps[i].Clone();
                                        if (inputImage.Rectangles.Count != 0)
                                            tmp = handleDet.Detect(img, inputImage.Rectangles[i].Location.X, inputImage.Rectangles[i].Location.Y);
                                        else
                                            tmp = handleDet.Detect(img);
                                        det_results.AddRange(tmp);
                                    }

                                    #endregion

                                    #region 解析结果

                                    switch (param.ModelName)
                                    {
                                        case ModelName.RL_12类线芯模型:
                                            RL12Parse.Parse(det_results, result_count, param, Process,ref res);
                                            break;
                                        case ModelName.合压模型:
                                            ClosingTerminals_encrypted_Parse.Parse(det_results, result_count, param, Process,ref res);
                                            break;
                                        case ModelName.XM_Fakra模型:
                                            XMSGParse.Parse(det_results, result_count, param, Process,ref res);
                                            break;
                                        default:
                                            throw new Exception("不存在的模型名称！");
                                    }
                                    #endregion

                                    break;

                                case ModelType.OBB:

                                    #region 执行推理

                                    List<ObbResult> obb_results = new List<ObbResult>();

                                    for (int i = 0; i < inputImage.Bitmaps.Count; i++)
                                    {
                                        List<ObbResult> tmp = new List<ObbResult>();
                                        var handle = param.Yolo8 as Yolo8Obb;
                                        if (handle == null)
                                            throw new Exception("AI模型句柄类型与OBB模型不匹配，请重新加载模型！");
                                        var img = inputImage.Bitmaps[i].Clone();
                                        if (inputImage.Rectangles.Count != 0)
                                        {
                                            tmp = handle.Detect(img, inputImage.Rectangles[i].Location.X, inputImage.Rectangles[i].Location.Y);
                                        }
                                        else
                                        {
                                            tmp = handle.Detect(img);
                                        }
                                        obb_results.AddRange(tmp);
                                    }

                                    #endregion

                                    #region 解析结果
                                    // 对输出数据存在宽高互换的情况进行处理
                                    ParseCommon.NormalizeObbResultsInPlace(obb_results);

                                    //超日项目的结果解析
                                    switch (param.ModelName)
                                    {
                                        default:
                                            throw new Exception("不存在的模型名称！");
                                    }

                                    #endregion

                                    break;

                                case ModelType.SEG:

                                    # region 执行推理
                                    List<SegResult> seg_results = new List<SegResult>();

                                    for (int i = 0; i < inputImage.Bitmaps.Count; i++)
                                    {
                                        List<SegResult> tmp = new List<SegResult>();
                                        var handle = param.Yolo8 as Yolo8Seg;
                                        if (handle == null)
                                            throw new Exception("AI模型句柄类型与SEG模型不匹配，请重新加载模型！");
                                        var img = inputImage.Bitmaps[i].Clone();
                                        if (inputImage.Rectangles.Count != 0)
                                        {
                                            tmp = handle.Detect(img, inputImage.Rectangles[i].Location.X, inputImage.Rectangles[i].Location.Y);
                                        }
                                        else
                                        {
                                            tmp = handle.Detect(img);
                                        }
                                        seg_results.AddRange(tmp);
                                    }
                                    #endregion

                                    #region 解析结果

                                    switch (param.ModelName)
                                    {
                                        case ModelName.RL_线芯截面:
                                            LineCoreCrossSection.Parse(seg_results, result_count, param, Process, ref res);
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                    #endregion

                                case ModelType.POSE:

                                    // TODO: 根据项目需要解析Ai结果
                                    break;

                                default:
                                    break;
                            }

                            var time = SetRunResult(startTime, NodeStatus.Successful);
                            res.RunTime = time;


                            //显示当前运用的模板,只限于启用
                            if (!param.IsFixed)
                            {
                                res.AlgorithmResult.Texts.Add(new ColorText($"当前模板: {param.CurDetectItemName}", Color.Green));
                            }
                            

                            // 返回结果
                            Result = res;
                            if (showLog)
                                LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                            return new NodeReturn(NodeRunFlag.ContinueRun);
                        }
                        catch (OperationCanceledException)
                        {
                            LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                            int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                            Result.RunTime = time;
                            throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                            int time = SetRunResult(startTime, NodeStatus.Failed);
                            Result.RunTime = time;
                            throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
                        }
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>
        /// 等待AI模型异步加载结束，并校验当前句柄可用于推理。
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        private static async Task EnsureModelReadyAsync(NodeParamTDAI param)
        {
            if (param == null)
                throw new Exception("AI节点参数为空，不能运行推理！");

            Task loadTask = param.ModelLoadTask;
            if (loadTask != null && !loadTask.IsCompleted)
                await loadTask.ConfigureAwait(false);

            EnsureModelReady(param);
        }

        /// <summary>
        /// 校验AI模型句柄与当前配置是否一致。
        /// </summary>
        /// <param name="param"></param>
        private static void EnsureModelReady(NodeParamTDAI param)
        {
            if (param.AIInputInfo == null)
                throw new Exception("AI配置未加载或解析失败，请重新选择配置文件并保存参数！");

            string modelPath = param.AIInputInfo.ModelInfo.ModelPath;
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new Exception("AI模型路径为空，请检查AI配置文件！");

            if (param.Yolo8 == null)
            {
                string reason = string.IsNullOrWhiteSpace(param.LastModelLoadError) ? "" : $" 最后一次加载失败原因：{param.LastModelLoadError}";
                throw new Exception($"AI模型未加载成功，不能运行推理！模型路径：{modelPath}{reason}");
            }

            if (!string.IsNullOrWhiteSpace(param.LoadedModelPath) &&
                !string.Equals(param.LoadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"AI模型路径已变更但新模型未加载成功，不能运行推理！配置模型路径：{modelPath}，已加载模型路径：{param.LoadedModelPath}");
            }

            if (param.Yolo8.ModelType != param.AIInputInfo.ModelInfo.ModelType)
            {
                throw new Exception(
                    $"AI模型句柄类型({param.Yolo8.ModelType})与配置类型({param.AIInputInfo.ModelInfo.ModelType})不一致，请重新加载模型！");
            }
        }

        /// <summary>
        /// 获取型号代码
        /// </summary>
        /// <param name="plc"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        private async Task<(short, short, short)> GetTypeCode(IPlc plc, string address)
        {
            short[] data = (await plc.ReadInt16Async(address, 15)).Content;
            short[] type1 = new short[5];
            short[] type2 = new short[5];
            short[] type3 = new short[5];
            Array.Copy(data, 0, type1, 0, 5); // 线芯工位端子型号，从索引0开始复制5个
            Array.Copy(data, 5, type2, 0, 5); // 线芯工位电线型号，从索引5开始复制5个
            Array.Copy(data, 10, type3, 0, 5); // 线芯工位防水栓型号，从索引10开始复制5个

            short terminalType = 0, wireType = 0, waterproofType = 0;
            // 判断5个型号是否都有数据，正常为1个型号，
            // 有多个型号时取第一个非0的型号，一个型号没有PLC会防呆
            foreach (var t in type1)
            {
                if (t > 0)
                {
                    terminalType = t;// 端子型号
                    break;
                }
            }
            foreach (var t in type2)
            {
                if (t > 0)
                {
                    wireType = t;// 电线型号
                    break;
                }
            }
            foreach (var t in type3)
            {
                if (t > 0)
                {
                    waterproofType = t;// 防水栓型号
                    break;
                }
            }
            return (terminalType, wireType, waterproofType);
        }

        private async Task<ushort> GetModbusCode(IModbus modbus,string address) {
            ushort[] value = modbus.ReadUInt16(address, 1);
            return value[0];
        }
    }
}
