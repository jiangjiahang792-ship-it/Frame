using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Forms.ShapeDraw;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop
{
    public partial class NodeParamFormImageCrop : FormBase, INodeParamForm
    {
        private Process process;//所属流程
        private NodeBase node;//所属节点
        public NodeParamFormImageCrop(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            imageROIEditControl1.SetROIType2Draw(ROIType.Rectangle);
            this.process = process;
            this.node = nodeBase;
            SolRunParamControl.RefreshParamView += SolRunParamControl_RefreshParamView;
        }

        private void SolRunParamControl_RefreshParamView(object sender, EventArgs e)
        {
            if (Params is NodeParamImageCrop param)
            {
                uiSwitch1.Active = param.RoiEnable;
            }
        }

        public INodeParam Params { get; set; }

        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 反序列化恢复参数
        /// </summary>
        public void SetParam2Form()
        {
            if(Params is NodeParamImageCrop param)
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                imageROIEditControl1.SetROIs(param.ROIs);
                uiSwitch1.Active = param.RoiEnable;
            }
        }

        /// <summary>
        /// 获取ROI图像
        /// </summary>
        public List<Mat> GetROIImages() 
        {
            var img = imageROIEditControl1.GetROIImages();
            return img;
        }

        public List<Rect> GetImageROIRects()
        {
            try
            {
                return imageROIEditControl1.GetImageROIRects();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 按指定源图像尺寸计算 ROI 矩形，运行时不需要先把 Mat 转成 Bitmap。
        /// </summary>
        /// <param name="source">源图像。</param>
        /// <returns>图像坐标系下的 ROI 矩形列表。</returns>
        public List<Rect> GetImageROIRects(Mat source)
        {
            if (source == null || source.Empty())
                throw new Exception("订阅的图像为null！");

            return imageROIEditControl1.GetImageROIRects(source.Width, source.Height);
        }

        /// <summary>
        /// 获取上游订阅的完整图像输出。
        /// </summary>
        /// <returns>上游图像输出对象。</returns>
        public OutputImage GetInputOutputImage()
        {
            OutputImage outputImage = nodeSubscription1.GetValue<OutputImage>();
            if (outputImage == null)
                throw new Exception("订阅的图像为null！");

            return outputImage;
        }

        /// <summary>
        /// 从图像输出中读取第一张有效图像。
        /// </summary>
        /// <param name="outputImage">上游图像输出对象。</param>
        /// <returns>上游输出图像中的第一张 Mat。</returns>
        public Mat GetInputMat(OutputImage outputImage)
        {
            if (outputImage == null ||
                outputImage.Bitmaps == null ||
                outputImage.Bitmaps.Count == 0 ||
                outputImage.Bitmaps[0] == null ||
                outputImage.Bitmaps[0].Empty())
            {
                throw new Exception("订阅的图像为null！");
            }

            return outputImage.Bitmaps[0];
        }

        /// <summary>
        /// 获取上游可复用的灰度图缓存。
        /// </summary>
        /// <param name="outputImage">上游图像输出对象。</param>
        /// <returns>有效灰度图；没有缓存时返回 null。</returns>
        public Mat GetInputGrayMat(OutputImage outputImage)
        {
            return OutputImage.HasValidImage(outputImage?.GrayImg) ? outputImage.GrayImg : null;
        }

        /// <summary>
        /// 获取订阅的图像设置到显示控件中
        /// </summary>
        public Mat UpdataImage()
        {
            OutputImage outputImage = GetInputOutputImage();
            Mat image = GetInputMat(outputImage);
            imageROIEditControl1.SetImage(image.ToBitmap());
            return image;
        }

        /// <summary>
        /// 确定ROI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            NodeParamImageCrop nodeParamImageCrop = new NodeParamImageCrop();
            nodeParamImageCrop.Text1 = nodeSubscription1.GetText1();
            nodeParamImageCrop.Text2 = nodeSubscription1.GetText2();
            nodeParamImageCrop.ROIs = imageROIEditControl1.GetROIs();
            nodeParamImageCrop.RoiEnable = uiSwitch1.Active;
            Params = nodeParamImageCrop;
            Hide();
        }

        /// <summary>
        /// 刷新图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button3_Click(object sender, EventArgs e)
        {
            await process.RunForUpdateImages(node);
            UpdataImage();
        }
    }
}
