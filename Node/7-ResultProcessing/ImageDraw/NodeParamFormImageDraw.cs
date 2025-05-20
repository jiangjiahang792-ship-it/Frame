using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageDraw
{
    internal partial class NodeParamFormImageDraw : Form, INodeParamForm
    {
        public NodeParamFormImageDraw()
        {
            InitializeComponent();
        }

        public INodeParam Params { get; set; }

        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
            nodeSubscription2.Init(node);
        }

        /// <summary>
        /// 获取订阅的图像
        /// </summary>
        /// <returns></returns>
        public Bitmap GetImage()
        {
            Bitmap bitmap = null;
            try
            {
                bitmap = nodeSubscription1.GetValue<Bitmap>();
            }
            catch (Exception)
            {
                throw new Exception("订阅的图像为null！");
            }
            return bitmap;
        }
        /// <summary>
        /// 获取订阅的AI结果
        /// </summary>
        /// <returns></returns>
            public List<DetectItemResult> GetAIResults()
            {
                //List<DetectItemResult> res = null;
                //try
                //{
                //    res = nodeSubscription2.GetValue<List<DetectItemResult>>();
                //}
                //catch (Exception)
                //{
                //    throw new Exception("订阅的结果为null！");
                //}
                //return res;

                // 模拟AI结果
                var results = new List<DetectItemResult>
                {
                    new DetectItemResult
                    {
                        Name = "胶皮长度",
                        Results = new List<SingleDetectItemResult>
                        {
                            new SingleDetectItemResult
                            {
                                IsOk = false,
                                Value = "异常的值",
                                Rects = new List<Rect> { new Rect(100, 100, 50, 50) }
                            },
                            new SingleDetectItemResult
                            {
                                IsOk = true,
                                Value = "正常的值",
                                Rects = new List<Rect> { new Rect(200, 200, 60, 60) }
                            }
                        }
                    }
                };
                return results;
            }
        /// <summary>
        /// 保存参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            var param = new NodeParamImageDraw();
            param.Text1 = nodeSubscription1.GetText1();
            param.Text2 = nodeSubscription1.GetText2();
            param.Text3 = nodeSubscription2.GetText1();
            param.Text4 = nodeSubscription2.GetText2();
            Params = param;
            Hide();
        }
        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if (Params is NodeParamImageDraw param)
            {
                // 还原界面显示
                nodeSubscription1.SetText(param.Text1, param.Text2);
                nodeSubscription2.SetText(param.Text3, param.Text4);
            }
        }
    }
}
