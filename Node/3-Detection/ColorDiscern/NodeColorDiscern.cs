using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.ColorDiscern;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    public class NodeColorDiscern : NodeBase
    {
        public NodeColorDiscern(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormColorDiscern(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultColorDiscern();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
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

            if (ParamForm is NodeParamFormColorDiscern form)
            {
                if (ParamForm.Params is NodeParamColorDiscern param)
                {
                    try
                    {
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);
                        // 获取图像
                        form.UpdataImage(false);

                        // 执行匹配
                        var (bitmap, resultList, isOk) = await form.MatchTemplateAsync(false);
                        if (bitmap == null) return new NodeReturn(NodeRunFlag.ContinueRun);
                        List<Rect> rectList = new List<Rect>();

                        // 输出结果
                        ((NodeResultColorDiscern)Result).OutputImage.Bitmaps = new List<Mat>() { bitmap.ToMat() };
                        int count = 0;

                        ((NodeResultColorDiscern)Result).Result.Clear();

                        if (resultList.Count==0)
                        {
                            ((NodeResultColorDiscern)Result).Result.Texts.Add(new ColorText($"未识别到任何颜色", Color.Red));
                        }

                        foreach (var sr in resultList)
                        {
                            rectList.Add(sr.Region);

                            ColorRotatedRect roiRect = new ColorRotatedRect(sr.Region);
                            roiRect.AddFlag(sr.IsOk);
                            ((NodeResultColorDiscern)Result).Result.Rects.Add(roiRect);
                            ((NodeResultColorDiscern)Result).Result.Texts.Add(new ColorText($"颜色:{sr.Name} 面积:{sr.Area} 面积占比:{sr.AreaB}%", sr.IsOk == false ? Color.Red : Color.Green));

                            ((NodeResultColorDiscern)Result).Result.DetectResults.Add(sr.Name, new List<SingleDetectResult>() { new SingleDetectResult(sr.Name, sr.Area + "", sr.IsOk) });
                        }
                        ((NodeResultColorDiscern)Result).OutputImage.Rectangles = rectList;

                        ((NodeResultColorDiscern)Result).Result.Texts.Add(new ColorText($"颜色匹配结果个数：{rectList.Count}个", isOk == false ? Color.Red : Color.Green));

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                       ((NodeResultColorDiscern)Result).Result.IsAllOk = isOk;
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
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);

        }
    }
}
