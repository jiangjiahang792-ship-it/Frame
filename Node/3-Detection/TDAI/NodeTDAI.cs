using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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

                            // 获取结果（测试数据）
                            res = TestRun(srcImg, param);

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
        /// 测试运行
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="param">检测项参数</param>
        public NodeResultTDAI TestRun(Bitmap image, NodeParamTDAI param)
        {
            // 第一步：执行AI算法

            // 第二步：结果赋值
            NodeResultTDAI res = new NodeResultTDAI();
            //res.ResultList = GetResult(); 
            res.ResultList = GetTestResults(image);
            return res;
        }

        /// <summary>
        /// 获取测试用例结果数据
        /// </summary>
        /// <returns></returns>
        public static List<DetectItemResult> GetTestResults(Bitmap image)
        {
            var rand = new Random();
            var resultItems = new List<DetectItemResult>();
            var itemNames = new[]
            {
                "露线芯长度",
                "咬胶长度",
                "后铆角面积",
                "前铆角宽度"
            };

            // 可以控制生成多少个 DetectItemResult 对象
            int itemCount = rand.Next(1, 5); // 生成 1~4 个不同项
            for (int i = 0; i < itemCount; i++)
            {
                var itemName = itemNames[rand.Next(itemNames.Length)];
                int resultCount = rand.Next(1, 3); // 每个项有 1~2 个 SingleDetectItemResult

                var detectItem = new DetectItemResult
                {
                    Name = itemName,
                    Results = new List<SingleDetectItemResult>()
                };

                for (int j = 0; j < resultCount; j++)
                {
                    double value = Math.Round(rand.NextDouble() * 200, 2);
                    bool isOk = value >= 20 && value <= 180;

                    // 随机生成一个矩形，确保不超过输入图像边界
                    int width = rand.Next(20, 150);
                    int height = rand.Next(20, 150);
                    int x = rand.Next(0, image.Width - width);
                    int y = rand.Next(0, image.Height - height);

                    detectItem.Results.Add(new SingleDetectItemResult
                    {
                        IsOk = isOk,
                        Value = value.ToString("F2"),
                        Rects = new List<Rect> { new Rect(x, y, width, height) }
                    });
                }

                resultItems.Add(detectItem);
            }

            return resultItems;
        }

        /// <summary>
        /// 获取模拟结果数据
        /// </summary>
        /// <returns></returns>
        private List<DetectItemResult> GetResult()
        {
            // 模拟AI结果
            var results = new List<DetectItemResult>
            {
                new DetectItemResult
                {
                    Name = "露线芯长度",
                    Results = new List<SingleDetectItemResult>
                    {
                        new SingleDetectItemResult
                        {
                            IsOk = false,
                            Value = "120.23",
                            Rects = new List<Rect> { new Rect(100, 100, 80, 150) }
                        },
                        new SingleDetectItemResult
                        {
                            IsOk = true,
                            Value = "56.32",
                            Rects = new List<Rect> { new Rect(200, 200, 70, 120) }
                        }
                    }
                },
                new DetectItemResult
                {
                    Name = "咬胶长度",
                    Results = new List<SingleDetectItemResult>
                    {
                        new SingleDetectItemResult
                        {
                            IsOk = true,
                            Value = "42.21",
                            Rects = new List<Rect> { new Rect(400, 300, 80, 150) }
                        },
                        new SingleDetectItemResult
                        {
                            IsOk = true,
                            Value = "23.86",
                            Rects = new List<Rect> { new Rect(300, 500, 70, 120) }
                        }
                    }
                }
            };
            return results;
        }
    }
}
