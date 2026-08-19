using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.ColorDiscern;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.BinaryAnalysis
{
    public class NodeBinaryAnalysis : NodeBase
    {
        public NodeBinaryAnalysis(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormBinaryAnalysis(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultBinaryAnalysis();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            Bitmap analysisBitmap = null;
            OutputImage pendingOutputImage = null;
            // 参数合法性校验
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

            if (ParamForm is NodeParamFormBinaryAnalysis form)
            {
                if (ParamForm.Params is NodeParamBinaryAnalysis param)
                {
                    try
                    {
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        // 获取图像
                        form.UpdateImage(false);

                        // 执行模版匹配
                        var (bitmap, resultList, isOk) = await form.RunAnalysisAsync(false);
                        analysisBitmap = bitmap;
                        if (analysisBitmap == null)
                            throw new Exception("二值分析没有生成有效输出图像！");

                        List<Rect> rectList = new List<Rect>();

                        Mat outputMat = analysisBitmap.ToMat();
                        pendingOutputImage = new OutputImage
                        {
                            Bitmaps = new List<Mat> { outputMat }
                        };
                        pendingOutputImage.TakeOwnership(outputMat);
                        var nodeResult = new NodeResultBinaryAnalysis
                        {
                            OutputImage = pendingOutputImage
                        };

                        int count = 0;

                        nodeResult.Result.Clear();

                        if (resultList.Count==0)
                        {
                            nodeResult.Result.Texts.Add(new ColorText($"未识别到任务区域", Color.Red));
                        }

                        foreach (var sr in resultList)
                        {
                            rectList.Add(sr.DetectedRect);
                            ColorRotatedRect roiRect = new ColorRotatedRect(sr.DetectedRect);
                            nodeResult.Result.Rects.Add(roiRect);

                            nodeResult.Result.Texts.Add(new ColorText($"区域:{sr.Name} 面积:{sr.Area}", sr.IsPass == false ? Color.Red : Color.Green));
                        }

                        nodeResult.OutputImage.Rectangles = rectList;

                        nodeResult.Result.Texts.Add(new ColorText($"区域匹配结果个数：{rectList.Count}个", isOk == false ? Color.Red : Color.Green));
                        nodeResult.OutputImage.DisplayResult = nodeResult.Result;

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        nodeResult.RunTime = time;
                        nodeResult.Result.IsAllOk = isOk;
                        Result = nodeResult;
                        pendingOutputImage = null;

                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms, 匹配是否成功: {isOk}", true);

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
                    finally
                    {
                        pendingOutputImage?.Dispose();
                        analysisBitmap?.Dispose();
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);

        }
    }
}
