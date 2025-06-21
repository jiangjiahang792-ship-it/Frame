using Logger;
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
                            res.ResultList.Clear();

                            // 4种模型通用检出结果
                            int result_count = 0;

                            switch (modelType)
                            {
                                case ModelType.DET:

                                    List<DetResult> det_results = new List<DetResult>();
                                    result_count = Yolo8Det.Detect(param.Handle, CameraHik.ConvertBitmapToBgr24(srcImg), srcImg.Width, srcImg.Height, 3, det_results);
                                    // 输出检测项配置
                                    res.AIInputInfo = param.AIInputInfo;
                                    //超日项目的结果解析
                                    AI.ResultParse(det_results, result_count, ref res);
                                    break;

                                case ModelType.OBB:

                                    List<ObbResult> obb_results = new List<ObbResult>();
                                    result_count = Yolo8Obb.Detect(param.Handle, CameraHik.ConvertBitmapToBgr24(srcImg), srcImg.Width, srcImg.Height, 3, obb_results);
                                    // TODO: 根据项目需要解析Ai结果
                                    break;

                                case ModelType.SEG:

                                    List<SegResult> seg_results = new List<SegResult>();
                                    result_count = Yolo8Seg.Infer(param.Handle, CameraHik.ConvertBitmapToBgr24(srcImg), srcImg.Width, srcImg.Height, 3, seg_results);
                                    // TODO: 根据项目需要解析Ai结果
                                    break;

                                case ModelType.POSE:

                                    List<PoseResult> pose_results = new List<PoseResult>();
                                    result_count = Yolo8Pose.Infer(param.Handle, CameraHik.ConvertBitmapToBgr24(srcImg), srcImg.Width, srcImg.Height, 3, pose_results);
                                    // TODO: 根据项目需要解析Ai结果
                                    break;

                                default:
                                    break;
                            }

                            #region 测试数据

                            //// 线芯项目
                            //ProcessingAIResults(results, result_count, ref res);

                            // 获取结果（测试数据）
                            //res = TestRun(srcImg, param, res.AIInputInfo);

                            #endregion

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
        /// <summary>
        /// 处理AI结果
        /// </summary>
        public static void ProcessingAIResults(List<DetResult> results, int resultCount, ref NodeResultTDAI nodeResult)
        {
            for (int i = 0; i < resultCount; ++i)
            {
                DetResult result = results[i];
                Rectangle rect = new Rectangle(result.Box.Left, result.Box.Top, result.Box.Width, result.Box.Height);

                // 如果当前检测项不测,直接跳过
                if (!nodeResult.AIInputInfo.DetectItems[result.ClassId].Enable)
                    continue;

                switch (nodeResult.AIInputInfo.ClassNames[result.ClassId])
                {
                    case "out_line":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "front_foot":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "line":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "rubber":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "behind_foot":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "waterproof_plug":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "only_rubber":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "only_line":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "fly_line":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    case "fly_rubber":
                        HandleOutLine(result, rect, ref nodeResult);
                        break;

                    default:
                        break;
                }
            }
        }

        #region 处理不同检测项的结果

        private static void HandleOutLine(DetResult result, Rectangle rect, ref NodeResultTDAI nodeResult)
        {
            var itemRes = new SingleDetectItemResult();
            itemRes.Rects.Add(rect);
            itemRes.Value = rect.Width.ToString();
            double curV = double.Parse(itemRes.Value);
            double minV = double.Parse(nodeResult.AIInputInfo.DetectItems[result.ClassId].MinValue);
            double maxV = double.Parse(nodeResult.AIInputInfo.DetectItems[result.ClassId].MaxValue);
            itemRes.IsOk = (curV >= minV && curV <= maxV) ? true : false;

            var deteItem = new DetectItemResult();
            deteItem.Name = nodeResult.AIInputInfo.DetectItems[result.ClassId].Name;
            deteItem.Results.Add(itemRes);
            nodeResult.ResultList.Add(deteItem);

            nodeResult.AIInputInfo.DetectItems[result.ClassId].CurValue = itemRes.Value;
        }

        #endregion

        #region 假数据供测试使用

        /// <summary>
        /// 测试运行
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="param">检测项参数</param>
        private NodeResultTDAI TestRun(Bitmap image, NodeParamTDAI param, AIInputInfo info)
        {
            // 第一步：执行AI算法

            // 第二步：结果赋值
            NodeResultTDAI res = new NodeResultTDAI();
            res.ResultList = GetTestResults(image, info);
            return res;
        }

        /// <summary>
        /// 获取测试用例结果数据
        /// </summary>
        /// <returns></returns>
        private List<DetectItemResult> GetTestResults(Bitmap image, AIInputInfo info)
        {
            var rand = new Random();
            var resultItems = new List<DetectItemResult>();
            var itemNames = GetDetectItemNames(info);
            // 可以控制生成多少个 DetectItemResult 对象
            int itemCount = rand.Next(1, 5); // 生成 1~4 个不同项
            for (int i = 0; i < itemCount; i++)
            {
                var itemName = itemNames[rand.Next(itemNames.Count)];
                int resultCount = rand.Next(1, 3); // 每个项有 1~2 个 SingleDetectItemResult
                var item = info.DetectItems.Find(it => it.Name == itemName);
                var detectItem = new DetectItemResult
                {
                    Name = itemName,
                    Results = new List<SingleDetectItemResult>()
                };

                for (int j = 0; j < resultCount; j++)
                {
                    double value = Math.Round(rand.NextDouble() * 200, 2);
                    //bool isOk = value >= 20 && value <= 180;
                    bool isOk = value >= double.Parse(item.MinValue) && value <= double.Parse(item.MaxValue);

                    // 随机生成一个矩形，确保不超过输入图像边界
                    int width = rand.Next(20, 200);
                    int height = rand.Next(20, 200);
                    int x = rand.Next(0, image.Width - width);
                    int y = rand.Next(0, image.Height - height);

                    detectItem.Results.Add(new SingleDetectItemResult
                    {
                        IsOk = isOk,
                        Value = value.ToString("F2"),
                        Rects = new List<Rectangle> { new Rectangle(x, y, width, height) }
                    });
                }

                resultItems.Add(detectItem);
            }

            return resultItems;
        }
        /// <summary>
        /// 获取检测项字符串列表
        /// </summary>
        /// <param name="aiInputInfo"></param>
        /// <returns></returns>
        private List<string> GetDetectItemNames(AIInputInfo aiInputInfo)
        {
            if (aiInputInfo?.DetectItems == null)
                return new List<string>();

            return aiInputInfo.DetectItems
                              .Select(item => item.Name)
                              .Where(name => !string.IsNullOrWhiteSpace(name)) // 可选过滤空名
                              .ToList();
        }

        #endregion
    }
}
