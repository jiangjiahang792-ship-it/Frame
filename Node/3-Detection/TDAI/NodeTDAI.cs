using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
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
    /// <summary>
    /// TDAI AI检测节点。
    /// </summary>
    public class NodeTDAI : NodeBase
    {
        /// <summary>
        /// 当前检测项配置切换或检测项上下限被运行过程更新时触发。
        /// </summary>
        public event EventHandler<TDAIDetectItemChangedEventArgs> DetectItemConfigChanged;

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
                        OutputImage subscribedInputImage = null;
                        OutputImage inputImage = null;
                        try
                        {
                            SetStatus(NodeStatus.Unexecuted, "*");
                            base.CheckTokenCancel(token);

                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测开始{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);
                            // 清空上次检测结果
                            res.AlgorithmResult.Clear();
                            res.ResetJudgeOk();

                            // 获取图像
                            subscribedInputImage = form.GetOutputImage();
                            inputImage = subscribedInputImage;

                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测,图像获取完毕{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);
                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测输入图像摘要：订阅({param.Text1}-{param.Text2})，{BuildOutputImageSummary(inputImage)}", true);
                            EnsureInputImageReady(inputImage, param);
                            inputImage = BuildRoiInputImage(inputImage, param);
                            if (param.RoiEnable)
                                LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测ROI裁剪完成：区域数量={param.RoiRegions.Count}，{BuildOutputImageSummary(inputImage)}", true);
                            EnsureInputImageReady(inputImage, param);
                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测模型检查开始：{BuildModelSummary(param)}", true);
                            await EnsureModelReadyAsync(param);
                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测模型检查通过：{BuildModelSummary(param)}", true);

                            #region 通信获取对应检测项的配置(不是使用固定检测项时)
                            // terminalType端子型号、wireType电线型号、waterproofType防水栓型号
                            ushort readValue = 1212;
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
                                    readValue = await GetModbusCode(mod, param.Adress);
                                    string strVal = readValue.ToString();

                                    var matchModel = param.TDAICommuntionParams.FirstOrDefault(m => m.TriggerVal == strVal);
                                    if (matchModel == null || string.IsNullOrWhiteSpace(matchModel.DetectionName))
                                    {
                                        configName = $"{Process.ProcessName}模板{strVal}";
                                    }
                                    else
                                    {
                                        configName = matchModel.DetectionName.Trim();
                                    }
                                    EnsureDetectItemConfig(configName, param.DetectItemName2);
                                    param.CurDetectItemName = configName;
                                    OnDetectItemConfigChanged(configName);
                                }
                                else
                                {
                                    throw new Exception("暂时不支持的通信设备来获取检测项配置！");
                                }

                            }

                            #endregion

                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测,Modbus地址{param.Adress} 获取完毕:{readValue}, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);

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
                                        var img = inputImage.Bitmaps[i];
                                        Rect offsetRect = GetOffsetRect(inputImage, i);
                                        tmp = handleDet.Detect(img, offsetRect.X, offsetRect.Y);

                                        det_results.AddRange(tmp);

                                        LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测推理完毕,图像序号:{i + 1},结果数量:{tmp.Count} {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);
                                    }

                                    #endregion

                                    #region 解析结果

                                    switch (param.ModelName)
                                    {
                                        case ModelName.RL_12类线芯模型:
                                            RL12Parse.Parse(det_results, result_count, param, Process,ref res);
                                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测结果运算完毕{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);
                                            break;
                                        case ModelName.合压模型:
                                            ClosingTerminals_encrypted_Parse.Parse(det_results, result_count, param, Process,ref res);
                                            break;
                                        case ModelName.XM_Fakra模型:
                                            XMSGParse.Parse(det_results, result_count, param, Process,ref res);
                                            break;
                                        case ModelName.多端子模型:
                                            MultiTerminalParse.Parse(det_results, result_count, param, Process,ref res);
                                            break;
                                        case ModelName.超声波焊接侧面三类模型:
                                            Ultrasound_Side_3Class.Parse(det_results, det_results.Count, param, ref res);
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
                                        using (Mat img = inputImage.Bitmaps[i].Clone())
                                        {
                                            if (inputImage.Rectangles.Count != 0)
                                            {
                                                tmp = handle.Detect(img, inputImage.Rectangles[i].Location.X, inputImage.Rectangles[i].Location.Y);
                                            }
                                            else
                                            {
                                                tmp = handle.Detect(img);
                                            }
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

                                    {
                                        List<SegResult> tmp = new List<SegResult>();
                                        var handle = param.Yolo8 as Yolo8Seg;
                                        if (handle == null)
                                            throw new Exception("AI模型句柄类型与SEG模型不匹配，请重新加载模型！");
                                        // 线芯截面只使用矩形统计，跳过SEG掩膜四角提取以降低推理后处理耗时。
                                        bool needMaskBox = param.ModelName != ModelName.RL_线芯截面;
                                        for (int i = 0; i < inputImage.Bitmaps.Count; i++)
                                        {
                                            var img = inputImage.Bitmaps[i];
                                            Rect offsetRect = GetOffsetRect(inputImage, i);
                                            tmp = handle.Detect(img, offsetRect.X, offsetRect.Y, needMaskBox);
                                            seg_results.AddRange(tmp);

                                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测推理完毕,图像序号:{i + 1},结果数量:{tmp.Count} {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);
                                        }
                                    }
                                    #endregion

                                    #region 解析结果

                                    switch (param.ModelName)
                                    {
                                        case ModelName.RL_线芯截面:
                                            LineCoreFront_C1_Seg.Parse(seg_results, result_count, param, Process, ref res);
                                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测结果运算完毕{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);
                                            LogAlgorithmResultSummary(Process.ProcessName, param, res);
                                            break;
                                        default:
                                            throw new Exception($"SEG模型名称({param.ModelName})没有对应解析逻辑，请检查AI节点模型名称是否选择为RL_线芯截面！");
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

                            //Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                            LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}AI检测完毕{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);

                            // 返回结果
                            Result = res;
                            OnDetectItemConfigChanged(param.CurDetectItemName);
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
                            LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}；模型状态：{BuildModelSummary(param)}；输入图像：{BuildOutputImageSummary(inputImage)}", true);
                            LogHelper.AddLog(MsgLevel.Debug, $"节点({ID}.{NodeName})运行失败堆栈：{ex}", true);
                            int time = SetRunResult(startTime, NodeStatus.Failed);
                            Result.RunTime = time;
                            throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", ex);
                        }
                        finally
                        {
                            if (inputImage != null && !ReferenceEquals(inputImage, subscribedInputImage))
                                inputImage.Dispose();
                        }
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>
        /// 校验AI输入图像是否包含至少一张可读图像，并修正旧输出中可能为空的矩形列表。
        /// </summary>
        /// <param name="inputImage">上游订阅得到的图像输出。</param>
        /// <param name="param">当前AI节点参数。</param>
        private static void EnsureInputImageReady(OutputImage inputImage, NodeParamTDAI param)
        {
            string subscriptionText = BuildSubscriptionText(param);
            if (inputImage == null)
                throw new Exception($"AI输入图像为空，订阅：{subscriptionText}，请检查上游节点是否运行成功！");

            if (inputImage.Bitmaps == null)
                throw new Exception($"AI输入图像列表为空，订阅：{subscriptionText}，请检查上游图像输出！");

            if (inputImage.Bitmaps.Count == 0)
                throw new Exception($"AI输入图像数量为0，订阅：{subscriptionText}，请检查上游图像输出！");

            if (!OutputImage.HasValidImage(inputImage.Bitmaps[0]))
                throw new Exception($"AI输入图像无有效像素，订阅：{subscriptionText}，图像摘要：{BuildOutputImageSummary(inputImage)}");

            if (inputImage.Rectangles == null)
            {
                inputImage.Rectangles = new List<OpenCvSharp.Rect>();
                LogHelper.AddLog(MsgLevel.Warn, $"AI输入图像矩形列表为空，订阅：{subscriptionText}，已按无ROI偏移处理。", true);
            }

            if (inputImage.Rectangles.Count > 0 && inputImage.Rectangles.Count < inputImage.Bitmaps.Count)
            {
                throw new Exception(
                    $"AI输入图像数量({inputImage.Bitmaps.Count})大于ROI数量({inputImage.Rectangles.Count})，订阅：{subscriptionText}，请检查上游裁剪或分割节点！");
            }
        }

        /// <summary>
        /// 根据节点内部 ROI 参数构建实际送入 AI 的图像集合；未启用 ROI 时直接返回原输入。
        /// </summary>
        /// <param name="inputImage">上游订阅得到的图像输出。</param>
        /// <param name="param">当前 AI 节点参数。</param>
        /// <returns>用于 AI 推理的图像输出。</returns>
        private static OutputImage BuildRoiInputImage(OutputImage inputImage, NodeParamTDAI param)
        {
            if (param == null || !param.RoiEnable)
                return inputImage;

            if (param.RoiRegions == null || param.RoiRegions.Count == 0)
                throw new Exception("AI检测已启用ROI，但没有配置任何检测区域。");

            Mat sourceImage = GetRoiSourceImage(inputImage);
            if (!OutputImage.HasValidImage(sourceImage))
                throw new Exception("AI检测ROI裁剪失败，输入图像为空。");

            List<Rect> localRoiRects = BuildRoiRects(param.RoiRegions, sourceImage.Width, sourceImage.Height);
            List<Mat> roiImages = null;
            Mat grayImage = null;
            OutputImage roiOutput = null;
            try
            {
                roiImages = CropImages(sourceImage, localRoiRects);
                if (roiImages.Count == 0)
                    throw new Exception("AI检测ROI裁剪失败，没有得到有效ROI图像。");

                grayImage = BuildFirstGrayCrop(inputImage, localRoiRects, roiImages);
                roiOutput = new OutputImage();
                roiOutput.TakeOwnership(roiImages);
                roiOutput.TakeOwnership(grayImage);
                roiOutput.TakeDependency(inputImage);
                roiOutput.SrcImg = sourceImage;
                roiOutput.Bitmaps = roiImages;
                roiOutput.Rectangles = BuildOutputRoiRects(localRoiRects, GetBaseOffsetRect(inputImage));
                roiOutput.GrayImg = grayImage;
                roiOutput.DisplayResult = inputImage.DisplayResult;
                return roiOutput;
            }
            catch
            {
                if (roiOutput != null)
                {
                    roiOutput.Dispose();
                }
                else
                {
                    if (roiImages != null)
                    {
                        foreach (Mat roiImage in roiImages)
                            roiImage?.Dispose();
                    }
                    if (grayImage != null && (roiImages == null || !roiImages.Any(image => ReferenceEquals(image, grayImage))))
                        grayImage.Dispose();
                }
                throw;
            }
        }

        /// <summary>
        /// 获取 ROI 裁剪使用的源图，优先使用当前订阅输出的第一张图。
        /// </summary>
        /// <param name="inputImage">上游图像输出。</param>
        /// <returns>用于裁剪的源图。</returns>
        private static Mat GetRoiSourceImage(OutputImage inputImage)
        {
            if (inputImage == null)
                return null;
            if (inputImage.Bitmaps != null && inputImage.Bitmaps.Count > 0 && OutputImage.HasValidImage(inputImage.Bitmaps[0]))
                return inputImage.Bitmaps[0];
            return OutputImage.HasValidImage(inputImage.SrcImg) ? inputImage.SrcImg : null;
        }

        /// <summary>
        /// 将保存的 ROI 参数转换为当前图像范围内的 OpenCV 矩形。
        /// </summary>
        /// <param name="regions">保存的 ROI 参数集合。</param>
        /// <param name="imageWidth">当前图像宽度。</param>
        /// <param name="imageHeight">当前图像高度。</param>
        /// <returns>图像坐标系下的 ROI 矩形集合。</returns>
        private static List<Rect> BuildRoiRects(IEnumerable<TDAIRoiRegion> regions, int imageWidth, int imageHeight)
        {
            var rects = new List<Rect>();
            int index = 0;
            foreach (TDAIRoiRegion region in regions)
            {
                index++;
                if (region == null)
                    continue;
                if (region.Width <= 0F || region.Height <= 0F)
                    throw new Exception($"AI检测ROI区域({FormatRoiName(region, index)})宽高无效。");

                var rotatedRect = new OpenCvSharp.RotatedRect(
                    new OpenCvSharp.Point2f(region.CenterX, region.CenterY),
                    new OpenCvSharp.Size2f(region.Width, region.Height),
                    region.Angle);
                Rect rect = ClampRect(rotatedRect.BoundingRect(), imageWidth, imageHeight);
                if (rect.Width <= 0 || rect.Height <= 0)
                    throw new Exception($"AI检测ROI区域({FormatRoiName(region, index)})超出当前图像范围。");

                rects.Add(rect);
            }

            if (rects.Count == 0)
                throw new Exception("AI检测ROI区域全部无效。");

            return rects;
        }

        /// <summary>
        /// 裁剪多个 ROI 图像，并返回独立 Mat，避免下游推理修改源图。
        /// </summary>
        /// <param name="sourceImage">源图像。</param>
        /// <param name="rects">ROI 矩形集合。</param>
        /// <returns>裁剪图像集合。</returns>
        private static List<Mat> CropImages(Mat sourceImage, IEnumerable<Rect> rects)
        {
            var images = new List<Mat>();
            try
            {
                foreach (Rect rect in rects)
                {
                    using (Mat roi = new Mat(sourceImage, rect))
                    {
                        images.Add(roi.Clone());
                    }
                }

                return images;
            }
            catch
            {
                foreach (Mat image in images)
                    image?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 构建送入 YOLO 的 ROI 坐标偏移，内部 ROI 叠加上游已有裁剪偏移。
        /// </summary>
        /// <param name="localRoiRects">当前源图局部坐标下的 ROI 矩形。</param>
        /// <param name="baseOffset">上游图像相对原图的偏移。</param>
        /// <returns>相对原图的 ROI 偏移矩形。</returns>
        private static List<Rect> BuildOutputRoiRects(IEnumerable<Rect> localRoiRects, Rect baseOffset)
        {
            var rects = new List<Rect>();
            if (localRoiRects == null)
                return rects;

            foreach (Rect rect in localRoiRects)
            {
                rects.Add(new Rect(
                    rect.X + baseOffset.X,
                    rect.Y + baseOffset.Y,
                    rect.Width,
                    rect.Height));
            }

            return rects;
        }

        /// <summary>
        /// 获取上游输入图像的基础偏移；没有上游 ROI 时返回零偏移。
        /// </summary>
        /// <param name="inputImage">上游图像输出。</param>
        /// <returns>第一张图像对应的上游偏移。</returns>
        private static Rect GetBaseOffsetRect(OutputImage inputImage)
        {
            if (inputImage != null && inputImage.Rectangles != null && inputImage.Rectangles.Count > 0)
                return inputImage.Rectangles[0];

            return new Rect();
        }

        /// <summary>
        /// 为第一张 ROI 图像构建灰度缓存，优先同步裁剪上游灰度图。
        /// </summary>
        /// <param name="inputImage">上游图像输出。</param>
        /// <param name="roiRects">ROI 矩形集合。</param>
        /// <param name="roiImages">已裁剪的 ROI 图像集合。</param>
        /// <returns>第一张 ROI 对应的灰度图。</returns>
        private static Mat BuildFirstGrayCrop(OutputImage inputImage, IReadOnlyList<Rect> roiRects, IReadOnlyList<Mat> roiImages)
        {
            Mat sourceImage = GetRoiSourceImage(inputImage);
            if (roiRects != null &&
                roiRects.Count > 0 &&
                OutputImage.HasValidImage(inputImage?.GrayImg) &&
                OutputImage.HasValidImage(sourceImage) &&
                inputImage.GrayImg.Width == sourceImage.Width &&
                inputImage.GrayImg.Height == sourceImage.Height)
            {
                using (Mat grayRoi = new Mat(inputImage.GrayImg, roiRects[0]))
                {
                    return grayRoi.Clone();
                }
            }

            if (roiImages != null && roiImages.Count > 0)
                return OutputImage.BuildGrayImage(roiImages[0]);

            return null;
        }

        /// <summary>
        /// 获取指定输入图像的坐标偏移矩形。
        /// </summary>
        /// <param name="inputImage">AI 输入图像集合。</param>
        /// <param name="index">图像序号。</param>
        /// <returns>对应 ROI 偏移；没有偏移时返回零矩形。</returns>
        private static Rect GetOffsetRect(OutputImage inputImage, int index)
        {
            if (inputImage != null &&
                inputImage.Rectangles != null &&
                index >= 0 &&
                index < inputImage.Rectangles.Count)
            {
                return inputImage.Rectangles[index];
            }

            return new Rect();
        }

        /// <summary>
        /// 将 ROI 矩形限制在图像范围内。
        /// </summary>
        /// <param name="rect">原始矩形。</param>
        /// <param name="imageWidth">图像宽度。</param>
        /// <param name="imageHeight">图像高度。</param>
        /// <returns>限制后的矩形。</returns>
        private static Rect ClampRect(Rect rect, int imageWidth, int imageHeight)
        {
            int left = Math.Max(0, rect.X);
            int top = Math.Max(0, rect.Y);
            int right = Math.Min(imageWidth, rect.X + rect.Width);
            int bottom = Math.Min(imageHeight, rect.Y + rect.Height);
            return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }

        /// <summary>
        /// 格式化 ROI 名称，名称为空时使用序号兜底。
        /// </summary>
        /// <param name="region">ROI 参数。</param>
        /// <param name="index">ROI 序号。</param>
        /// <returns>可读 ROI 名称。</returns>
        private static string FormatRoiName(TDAIRoiRegion region, int index)
        {
            return region == null || string.IsNullOrWhiteSpace(region.Name) ? "ROI" + index : region.Name;
        }

        /// <summary>
        /// 构建AI输入图像摘要，避免只看到空引用而无法判断是哪一路图像为空。
        /// </summary>
        /// <param name="inputImage">上游订阅得到的图像输出。</param>
        /// <returns>图像尺寸、列表数量和ROI数量摘要。</returns>
        private static string BuildOutputImageSummary(OutputImage inputImage)
        {
            if (inputImage == null)
                return "OutputImage=null";

            int bitmapCount = inputImage.Bitmaps == null ? -1 : inputImage.Bitmaps.Count;
            int rectangleCount = inputImage.Rectangles == null ? -1 : inputImage.Rectangles.Count;
            string firstBitmap = bitmapCount > 0 ? FormatMat(inputImage.Bitmaps[0]) : "无";
            string firstRectangle = rectangleCount > 0 ? FormatRect(inputImage.Rectangles[0]) : "无";

            return $"SrcImg={FormatMat(inputImage.SrcImg)}，GrayImg={FormatMat(inputImage.GrayImg)}，Bitmaps数量={FormatCount(bitmapCount)}，Bitmaps[0]={firstBitmap}，Rectangles数量={FormatCount(rectangleCount)}，Rectangles[0]={firstRectangle}";
        }

        /// <summary>
        /// 输出AI解析后的可订阅结果摘要，便于远程排查推理有结果但ROI没有可绘制元素的情况。
        /// </summary>
        /// <param name="processName">当前流程名称。</param>
        /// <param name="param">当前AI节点参数。</param>
        /// <param name="result">当前AI节点运行结果。</param>
        private static void LogAlgorithmResultSummary(string processName, NodeParamTDAI param, NodeResultTDAI result)
        {
            AlgorithmResult algorithmResult = result == null ? null : result.AlgorithmResult;
            int detectResultCount = algorithmResult == null || algorithmResult.DetectResults == null
                ? 0
                : algorithmResult.DetectResults.Values.Sum(items => items == null ? 0 : items.Count);
            int rectCount = algorithmResult == null || algorithmResult.Rects == null ? 0 : algorithmResult.Rects.Count;
            int ngRectCount = algorithmResult == null || algorithmResult.RectsNgMap == null
                ? 0
                : algorithmResult.RectsNgMap.Values.Sum(items => items == null ? 0 : items.Count);
            int textCount = algorithmResult == null || algorithmResult.Texts == null ? 0 : algorithmResult.Texts.Count;
            string modelName = param == null ? "参数为空" : param.ModelName.ToString();
            string detectItemName = param == null || string.IsNullOrWhiteSpace(param.CurDetectItemName)
                ? "空"
                : param.CurDetectItemName;

            LogHelper.AddLog(
                MsgLevel.Debug,
                $"流程{processName}AI检测解析结果：模型名称={modelName}，检测项={detectItemName}，检测结果={detectResultCount}，矩形={rectCount}，NG缓存矩形={ngRectCount}，文本={textCount}。",
                true);
        }

        /// <summary>
        /// 构建AI模型加载状态摘要，用于区分模型未加载、加载失败和模型类型不匹配。
        /// </summary>
        /// <param name="param">当前AI节点参数。</param>
        /// <returns>模型路径、句柄、加载任务和最后错误摘要。</returns>
        private static string BuildModelSummary(NodeParamTDAI param)
        {
            if (param == null)
                return "参数=null";

            string configModelType = param.AIInputInfo == null ? "配置未加载" : param.AIInputInfo.ModelInfo.ModelType.ToString();
            string configModelPath = param.AIInputInfo == null ? "配置未加载" : param.AIInputInfo.ModelInfo.ModelPath;
            string handleModelType = param.Yolo8 == null ? "句柄=null" : param.Yolo8.ModelType.ToString();
            string loadedPath = string.IsNullOrWhiteSpace(param.LoadedModelPath) ? "空" : param.LoadedModelPath;
            string loadError = string.IsNullOrWhiteSpace(param.LastModelLoadError) ? "无" : param.LastModelLoadError;

            return $"配置路径={param.ConfigPath ?? "空"}，模型路径={configModelPath ?? "空"}，配置类型={configModelType}，句柄类型={handleModelType}，加载任务={FormatTaskStatus(param.ModelLoadTask)}，已加载路径={loadedPath}，最后加载错误={loadError}";
        }

        /// <summary>
        /// 格式化图像尺寸和通道数。
        /// </summary>
        /// <param name="image">待格式化的图像。</param>
        /// <returns>图像可读状态摘要。</returns>
        private static string FormatMat(OpenCvSharp.Mat image)
        {
            if (image == null)
                return "null";

            try
            {
                if (image.Empty())
                    return "空Mat";

                return $"{image.Width}x{image.Height}x{image.Channels()}";
            }
            catch (Exception ex)
            {
                return $"不可读({ex.Message})";
            }
        }

        /// <summary>
        /// 格式化ROI矩形范围。
        /// </summary>
        /// <param name="rect">待格式化的ROI矩形。</param>
        /// <returns>矩形坐标和尺寸。</returns>
        private static string FormatRect(OpenCvSharp.Rect rect)
        {
            return $"X={rect.X},Y={rect.Y},W={rect.Width},H={rect.Height}";
        }

        /// <summary>
        /// 格式化列表数量，-1表示列表对象本身为空。
        /// </summary>
        /// <param name="count">列表数量。</param>
        /// <returns>数量文本。</returns>
        private static string FormatCount(int count)
        {
            return count < 0 ? "null" : count.ToString();
        }

        /// <summary>
        /// 格式化模型加载任务状态。
        /// </summary>
        /// <param name="task">模型加载任务。</param>
        /// <returns>任务状态文本。</returns>
        private static string FormatTaskStatus(Task task)
        {
            return task == null ? "null" : task.Status.ToString();
        }

        /// <summary>
        /// 获取当前AI节点订阅来源文本。
        /// </summary>
        /// <param name="param">当前AI节点参数。</param>
        /// <returns>订阅来源文本。</returns>
        private static string BuildSubscriptionText(NodeParamTDAI param)
        {
            if (param == null)
                return "参数=null";

            return $"{param.Text1 ?? "空"}-{param.Text2 ?? "空"}";
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
        /// 通知外部界面刷新当前检测项配置。
        /// </summary>
        /// <param name="detectItemName">当前检测项名称。</param>
        private void OnDetectItemConfigChanged(string detectItemName)
        {
            if (string.IsNullOrWhiteSpace(detectItemName))
                return;

            DetectItemConfigChanged?.Invoke(this, new TDAIDetectItemChangedEventArgs(this, detectItemName));
        }

        /// <summary>
        /// 确保当前检测项配置存在，不存在时从默认模板复制一份。
        /// </summary>
        /// <param name="detectItemName">当前通信解析出来的检测项名称。</param>
        /// <param name="defaultTemplateName">用于复制的默认模板检测项名称。</param>
        public static void EnsureDetectItemConfig(string detectItemName, string defaultTemplateName)
        {
            if (Solution.Instance.DetectItemDic == null)
                Solution.Instance.DetectItemDic = new Dictionary<string, List<DetectItemInfo>>();

            if (string.IsNullOrWhiteSpace(detectItemName))
                throw new Exception("当前检测项名称为空，无法创建检测项配置！");

            detectItemName = detectItemName.Trim();

            if (Solution.Instance.DetectItemDic.ContainsKey(detectItemName))
                return;

            if (string.IsNullOrWhiteSpace(defaultTemplateName))
                throw new Exception($"检测项({detectItemName})不存在，且默认模板未设置！");

            defaultTemplateName = defaultTemplateName.Trim();

            if (!Solution.Instance.DetectItemDic.TryGetValue(defaultTemplateName, out List<DetectItemInfo> defaultTemplate) || defaultTemplate == null)
                throw new Exception($"检测项({detectItemName})不存在，且默认模板({defaultTemplateName})不存在！");

            Solution.Instance.DetectItemDic[detectItemName] = CloneDetectItems(defaultTemplate);
            LogHelper.AddLog(MsgLevel.Info, $"检测项({detectItemName})不存在，已从默认模板({defaultTemplateName})复制。", true);
        }

        /// <summary>
        /// 根据通信配置批量补齐检测项配置，空检测项名称按“模板+触发值”生成。
        /// </summary>
        /// <param name="communicationParams">通信触发值和检测项名称映射集合。</param>
        /// <param name="defaultTemplateName">用于复制的默认模板检测项名称。</param>
        public static void EnsureCommunicationDetectItemConfigs(IEnumerable<TDAICommuntionParam> communicationParams, string defaultTemplateName)
        {
            if (communicationParams == null)
                return;

            foreach (var communicationParam in communicationParams)
            {
                if (communicationParam == null)
                    continue;

                string detectItemName = communicationParam.DetectionName;
                if (string.IsNullOrWhiteSpace(detectItemName))
                {
                    if (string.IsNullOrWhiteSpace(communicationParam.TriggerVal))
                        continue;

                    detectItemName = $"模板{communicationParam.TriggerVal.Trim()}";
                    communicationParam.DetectionName = detectItemName;
                }
                else
                {
                    detectItemName = detectItemName.Trim();
                    communicationParam.DetectionName = detectItemName;
                }

                EnsureDetectItemConfig(detectItemName, defaultTemplateName);
            }
        }

        /// <summary>
        /// 复制检测项模板，避免新检测项和默认模板共用同一批检测项对象。
        /// </summary>
        /// <param name="sourceItems">默认模板中的检测项集合。</param>
        /// <returns>新的检测项配置集合。</returns>
        private static List<DetectItemInfo> CloneDetectItems(List<DetectItemInfo> sourceItems)
        {
            return sourceItems.Select(item => new DetectItemInfo
            {
                Name = item.Name,
                MinValue = item.MinValue,
                MaxValue = item.MaxValue,
                Enable = item.Enable,
                IsCountItem = item.IsCountItem,
                CurValue = item.CurValue
            }).ToList();
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

        private async Task<ushort> GetModbusCode(IModbus modbus,string address) {
            ushort[] value = modbus.ReadUInt16(address, 1);
            return value[0];
        }
    }

    /// <summary>
    /// AI检测项配置切换事件参数。
    /// </summary>
    public class TDAIDetectItemChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 触发检测项切换的AI节点。
        /// </summary>
        public NodeTDAI Node { get; }

        /// <summary>
        /// 当前通信或固定模式选中的检测项名称。
        /// </summary>
        public string DetectItemName { get; }

        /// <summary>
        /// 初始化AI检测项配置切换事件参数。
        /// </summary>
        /// <param name="node">触发检测项切换的AI节点。</param>
        /// <param name="detectItemName">当前检测项名称。</param>
        public TDAIDetectItemChangedEventArgs(NodeTDAI node, string detectItemName)
        {
            Node = node;
            DetectItemName = detectItemName;
        }
    }
}
