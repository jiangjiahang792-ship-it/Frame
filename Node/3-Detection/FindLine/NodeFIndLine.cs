using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using YTVisionPro.Node._3_Detection.FindLine;

namespace TDJS_Vision.Node._3_Detection.FindLine
{
    /// <summary>
    /// 直线查找节点
    /// </summary>
    public class NodeFIndLine : NodeBase
    {
        public NodeFIndLine(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormFindLine(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultFindLine();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
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

            if (ParamForm is NodeParamFormFindLine form)
            {
                if (ParamForm.Params is NodeParamFindLine param)
                {
                    try
                    {
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        var (line, detectionSucceeded) = form.DetectLine();
                        if (!detectionSucceeded) { throw new Exception("直线查找失败！"); }

                        var outputImage = form.GetOutputImage();
                        if (outputImage == null || outputImage.Bitmaps == null || outputImage.Bitmaps.Count == 0 || !OutputImage.HasValidImage(outputImage.Bitmaps[0]))
                            throw new Exception("直线查找的上游输出图像为空！");

                        var nodeResult = new NodeResultFindLine();
                        nodeResult.Result.Lines.Add(new ColorLine(line, Color.Green));
                        nodeResult.Result.Texts.Add(new ColorText($"识别到的直线P1({line.P1.X}, {line.P1.Y}), P2({line.P2.X}, {line.P2.Y})", Color.Green));
                        pendingOutputImage = new OutputImage
                        {
                            Bitmaps = new List<Mat> { outputImage.Bitmaps[0] },
                            DisplayResult = nodeResult.Result
                        };
                        pendingOutputImage.TakeDependency(outputImage);
                        nodeResult.OutputImage = pendingOutputImage;

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        nodeResult.RunTime = time;
                        Result = nodeResult;
                        pendingOutputImage = null;
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，直线长度为：{LineUtils.PointDistance(line.P1, line.P2).ToString("F2")} 像素)", true);

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
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);

        }
    }
}
