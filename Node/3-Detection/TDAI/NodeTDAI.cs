using Logger;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal class NodeTDAI : NodeBase
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

                            // 获取订阅的图像
                            Bitmap srcImg = form.GetImage();

                            // 获取模型句柄种类
                            var modelType = ModelHandleManager.GetModelHandleType(param.Handle);

                            // 清空上次AI结果
                            res.AiResult?.Clear();

                            // 4种模型检出结果解析
                            int result_count = 0;
                            int keypoint_count = 0;

                            switch (modelType)
                            {
                                case ModelType.DET:

                                    List<DetResult> det_results = new List<DetResult>();
                                    result_count = Yolo8Det.Detect(param.Handle, CameraHik.ConvertBitmapToBgr24(srcImg), srcImg.Width, srcImg.Height, 3, det_results);
                                    // 输出检测项配置
                                    res.AIInputInfo = param.AIInputInfo;
                                    //超日项目的结果解析
                                    AI.TypeCResultParse(det_results, result_count, ref res); 
                                    break;

                                case ModelType.OBB:

                                    List<ObbResult> obb_results = new List<ObbResult>();
                                    result_count = Yolo8Obb.Detect(param.Handle, CameraHik.ConvertBitmapToBgr24(srcImg), srcImg.Width, srcImg.Height, 3, obb_results);
                                    // 输出检测项配置
                                    res.AIInputInfo = param.AIInputInfo;

                                    // 对输出数据存在宽高互换的情况进行处理
                                    // (目前OBB模型检出的目标结果存在宽高互换的情况，实际角度应为“90°-检出角度”，顺时针为正)
                                    AI.ProcessingRotatedRects(ref obb_results);

                                    //超日项目的结果解析
                                    switch (param.AIInputInfo.ModelName)
                                    {
                                        case "线芯":
                                            AI.LineCoreResultParse(obb_results, result_count, ref res);
                                            break;
                                        case "胶壳":
                                            AI.GelCoatResultParse(obb_results, result_count, ref res);
                                            break;
                                        default:
                                            throw new Exception("不存在的模型名称！");
                                    }
                                    break;

                                case ModelType.SEG:

                                    List<SegResult> seg_results = new List<SegResult>();
                                    result_count = Yolo8Seg.Infer(param.Handle, CameraHik.ConvertBitmapToBgr24(srcImg), srcImg.Width, srcImg.Height, 3, seg_results);
                                    // TODO: 根据项目需要解析Ai结果
                                    break;

                                case ModelType.POSE:

                                    PoseResult pose_result = new PoseResult();
                                    (result_count, keypoint_count) = Yolo8Pose.Infer(param.Handle, CameraHik.ConvertBitmapToBgr24(srcImg), srcImg.Width, srcImg.Height, 3, pose_result);
                                    // TODO: 根据项目需要解析Ai结果
                                    break;

                                default:
                                    break;
                            }
                            // 返回结果
                            Result = res;

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
            }
        }
    }
}
