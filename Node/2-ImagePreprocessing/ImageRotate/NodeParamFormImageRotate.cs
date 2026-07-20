using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate
{
    /// <summary>
    /// 图像旋转节点参数窗体，负责订阅上游图像、保存旋转角度并提供预览。
    /// </summary>
    public partial class NodeParamFormImageRotate : FormBase, INodeParamForm
    {
        /// <summary>
        /// 参数预览时使用的旋转角度，运行时以保存到参数对象中的角度为准。
        /// </summary>
        private float angle;

        /// <summary>
        /// 所属流程，用于参数窗体刷新上游图像。
        /// </summary>
        private Process process;

        /// <summary>
        /// 所属节点，用于参数窗体刷新时指定运行停止位置。
        /// </summary>
        private NodeBase node;

        /// <summary>
        /// 初始化图像旋转参数窗体。
        /// </summary>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeBase">所属节点。</param>
        public NodeParamFormImageRotate(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            this.process = process;
            this.node = nodeBase;
        }

        /// <summary>
        /// 当前窗体保存的节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 设置节点归属并初始化订阅控件。
        /// </summary>
        /// <param name="node">所属节点。</param>
        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 反序列化恢复参数。
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamImageRotate param)
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                numericUpDown1.ValueChanged -= numericUpDown1_ValueChanged;
                numericUpDown1.Value = -(decimal)param.Angle;
                numericUpDown1.ValueChanged += numericUpDown1_ValueChanged;
                Hide();
            }
        }

        /// <summary>
        /// 确认按钮点击后保存订阅关系和旋转角度。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void button1_Click(object sender, EventArgs e)
        {
            NodeParamImageRotate nodeParamImageRotate = new NodeParamImageRotate();
            nodeParamImageRotate.Text1 = nodeSubscription1.GetText1();
            nodeParamImageRotate.Text2 = nodeSubscription1.GetText2();
            nodeParamImageRotate.Angle = -(float)numericUpDown1.Value;
            Params = nodeParamImageRotate;
            Hide();
        }

        /// <summary>
        /// 获取订阅的图像并转换为参数窗体预览用 Bitmap。
        /// </summary>
        /// <returns>订阅图像的 Bitmap 副本。</returns>
        public Bitmap GetImage()
        {
            try
            {
                return GetInputMat(GetInputOutputImage()).ToBitmap();
            }
            catch (Exception)
            {
                throw new Exception("订阅的图像为null！");
            }
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
        /// 获取上游订阅的源图像 Mat，运行期直接使用该对象以避免 Bitmap 往返转换。
        /// </summary>
        /// <returns>上游输出图像中的第一张 Mat。</returns>
        public Mat GetInputMat()
        {
            return GetInputMat(GetInputOutputImage());
        }

        /// <summary>
        /// 从指定图像输出中获取第一张有效图像。
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
        /// 点击刷新图像。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private async void button2_Click(object sender, EventArgs e)
        {
            try
            {
                await process.RunForUpdateImages(node);
                SetPreviewBitmap(GetImage());
            }
            catch (Exception)
            {
                LogHelper.AddLog(MsgLevel.Exception, "刷新图像失败！请检查是否订阅正确的结果或前面节点运行存在异常！", true);
            }
        }

        /// <summary>
        /// 角度值变化时刷新参数窗体预览图。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                // 获取旋转角度（顺时针）。
                angle = -(float)numericUpDown1.Value;
                using (Mat previewImage = ImageRotate(GetInputMat(), angle))
                {
                    SetPreviewImage(previewImage);
                }
            }
            catch (Exception)
            {
                LogHelper.AddLog(MsgLevel.Exception, "刷新图像失败！请检查是否订阅正确的结果或前面节点运行存在异常！", true);
            }
        }

        /// <summary>
        /// 运行期旋转图像，零角度直接透传源图像以避免无效像素拷贝。
        /// </summary>
        /// <param name="srcImg">源图像。</param>
        /// <param name="angle">OpenCV 旋转角度，正值为逆时针。</param>
        /// <returns>旋转后的图像；零角度时返回源图像引用。</returns>
        public Mat ImageRotateForRun(Mat srcImg, float angle)
        {
            return ImageRotate(srcImg, angle, true);
        }

        /// <summary>
        /// 预览期旋转图像，始终返回调用方可释放的新图像对象。
        /// </summary>
        /// <param name="srcImg">源图像。</param>
        /// <param name="angle">OpenCV 旋转角度，正值为逆时针。</param>
        /// <returns>旋转后的新图像。</returns>
        public Mat ImageRotate(Mat srcImg, float angle)
        {
            return ImageRotate(srcImg, angle, false);
        }

        /// <summary>
        /// 执行图像旋转，直角旋转使用 OpenCV 快路径，任意角度使用仿射变换。
        /// </summary>
        /// <param name="srcImg">源图像。</param>
        /// <param name="angle">OpenCV 旋转角度，正值为逆时针。</param>
        /// <param name="allowSourcePassthrough">是否允许零角度时返回源图像引用。</param>
        /// <returns>旋转后的图像。</returns>
        private Mat ImageRotate(Mat srcImg, float angle, bool allowSourcePassthrough)
        {
            if (srcImg == null)
                throw new Exception("输入图像为null！");
            if (srcImg.Empty())
                throw new Exception("输入图像为空！");

            int quadrantAngle;
            if (TryGetQuadrantAngle(angle, out quadrantAngle))
                return RotateByQuadrant(srcImg, quadrantAngle, allowSourcePassthrough);

            // 非直角旋转保留完整外接矩形，避免裁切原图内容。
            var center = new Point2f(srcImg.Width / 2f, srcImg.Height / 2f);
            using (Mat rotationMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0))
            {
                double cos = Math.Abs(rotationMatrix.At<double>(0, 0));
                double sin = Math.Abs(rotationMatrix.At<double>(0, 1));
                int newWidth = Math.Max(1, (int)(srcImg.Height * sin + srcImg.Width * cos));
                int newHeight = Math.Max(1, (int)(srcImg.Width * sin + srcImg.Height * cos));

                rotationMatrix.Set(0, 2, rotationMatrix.At<double>(0, 2) + (newWidth - srcImg.Width) / 2.0);
                rotationMatrix.Set(1, 2, rotationMatrix.At<double>(1, 2) + (newHeight - srcImg.Height) / 2.0);

                Mat rotatedImage = new Mat();
                Cv2.WarpAffine(srcImg, rotatedImage, rotationMatrix, new OpenCvSharp.Size(newWidth, newHeight));
                return rotatedImage;
            }
        }

        /// <summary>
        /// 判断角度是否为 0/90/180/270 直角旋转。
        /// </summary>
        /// <param name="angle">OpenCV 旋转角度。</param>
        /// <param name="quadrantAngle">标准化后的直角角度。</param>
        /// <returns>角度为直角旋转时返回 true。</returns>
        private static bool TryGetQuadrantAngle(float angle, out int quadrantAngle)
        {
            double normalizedAngle = angle % 360.0;
            if (normalizedAngle < 0)
                normalizedAngle += 360.0;

            int roundedAngle = (int)Math.Round(normalizedAngle);
            if (Math.Abs(normalizedAngle - roundedAngle) > 0.0001 ||
                roundedAngle % 90 != 0)
            {
                quadrantAngle = 0;
                return false;
            }

            quadrantAngle = roundedAngle == 360 ? 0 : roundedAngle;
            return true;
        }

        /// <summary>
        /// 使用 OpenCV 直角旋转接口处理 0/90/180/270 度图像。
        /// </summary>
        /// <param name="srcImg">源图像。</param>
        /// <param name="quadrantAngle">标准化后的直角角度。</param>
        /// <param name="allowSourcePassthrough">是否允许零角度时返回源图像引用。</param>
        /// <returns>旋转后的图像。</returns>
        private static Mat RotateByQuadrant(Mat srcImg, int quadrantAngle, bool allowSourcePassthrough)
        {
            if (quadrantAngle == 0)
                return allowSourcePassthrough ? srcImg : srcImg.Clone();

            Mat rotatedImage = new Mat();
            switch (quadrantAngle)
            {
                case 90:
                    Cv2.Rotate(srcImg, rotatedImage, RotateFlags.Rotate90Counterclockwise);
                    break;
                case 180:
                    Cv2.Rotate(srcImg, rotatedImage, RotateFlags.Rotate180);
                    break;
                case 270:
                    Cv2.Rotate(srcImg, rotatedImage, RotateFlags.Rotate90Clockwise);
                    break;
                default:
                    rotatedImage.Dispose();
                    throw new ArgumentOutOfRangeException(nameof(quadrantAngle), "直角旋转角度必须为0、90、180或270！");
            }

            return rotatedImage;
        }

        /// <summary>
        /// 将 Mat 设置为参数窗体预览图。
        /// </summary>
        /// <param name="image">要显示的图像。</param>
        private void SetPreviewImage(Mat image)
        {
            SetPreviewBitmap(BitmapConverter.ToBitmap(image));
        }

        /// <summary>
        /// 替换 PictureBox 预览图并释放旧图，避免连续预览时堆积 GDI 对象。
        /// </summary>
        /// <param name="bitmap">新的预览图。</param>
        private void SetPreviewBitmap(Bitmap bitmap)
        {
            Image oldImage = pictureBox1.Image;
            pictureBox1.Image = bitmap;
            oldImage?.Dispose();
        }
    }
}
