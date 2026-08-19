using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageSplit
{
    public partial class ParamFormImageSplit : FormBase, INodeParamForm
    {
        /// <summary>
        /// 所属流程。
        /// </summary>
        private readonly Process _process;

        /// <summary>
        /// 所属节点。
        /// </summary>
        private readonly NodeBase _node;

        /// <summary>
        /// 当前原始预览图，由参数窗体持有并在刷新或销毁时释放。
        /// </summary>
        private Bitmap _image;

        /// <summary>
        /// 当前窗体保存的节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        public ParamFormImageSplit(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            _process = process;
            _node = nodeBase;
        }

        public void SetParam2Form()
        {
            if (Params is NodeParamImageSplit param)
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                numericUpDownRows.Value = param.Rows;
                numericUpDownCols.Value = param.Cols;
                Hide();
            }
        }

        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 获取订阅的图像设置到显示控件中
        /// </summary>
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
        /// 从图像输出中获取第一张有效图像。
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
        /// 刷新图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void buttonRefresh_Click(object sender, EventArgs e)
        {
            Bitmap refreshedImage = null;
            try
            {
                await _process.RunForUpdateImages(_node);
                refreshedImage = GetImage();
                SetSourceImage(refreshedImage);
                refreshedImage = null;
            }
            catch (Exception)
            {
                MessageBoxTD.Show("刷新图像失败！请检查是否订阅正确的结果或前面节点运行存在异常！");
            }
            finally
            {
                refreshedImage?.Dispose();
            }
        }


        /// <summary>
        /// 保存参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            NodeParamImageSplit param = new NodeParamImageSplit();
            param.Text1 = nodeSubscription1.GetText1();
            param.Text2 = nodeSubscription1.GetText2();
            param.Rows = (int)numericUpDownRows.Value;
            param.Cols = (int)numericUpDownCols.Value;
            Params = param;

            Hide();
        }

        /// <summary>
        /// 绘制分割线
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonImplement_Click(object sender, EventArgs e)
        {
            Bitmap previewImage = null;
            try
            {
                previewImage = DrawSplitLines(_image, (int)numericUpDownRows.Value, (int)numericUpDownCols.Value);
                SetSplitPreviewImage(previewImage);
                previewImage = null;
            }
            catch (Exception)
            {
                MessageBoxTD.Show("预览失败，请检查参数！");
            }
            finally
            {
                previewImage?.Dispose();
            }
        }

        /// <summary>
        /// 在图像上画线，标识 m 行 n 列的分割位置。
        /// </summary>
        /// <param name="originalImage">原始图像。</param>
        /// <param name="rows">行数。</param>
        /// <param name="cols">列数。</param>
        /// <returns>包含分割线的图像。</returns>
        public static Bitmap DrawSplitLines(Bitmap originalImage, int rows, int cols)
        {
            if (originalImage == null)
                throw new ArgumentNullException(nameof(originalImage), "原始图像不能为空。");

            if (rows <= 0 || cols <= 0)
                throw new ArgumentException("行数和列数必须是正整数。");

            // 创建一个新的 24 位 RGB 图像
            Bitmap newImage = new Bitmap(originalImage.Width, originalImage.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(newImage))
            {
                // 将原始图像绘制到新图像上
                g.DrawImage(originalImage, System.Drawing.Point.Empty);

                using (Pen pen = new Pen(Color.Cyan, 5))
                {
                    int baseHeight = originalImage.Height / rows;
                    int baseWidth = originalImage.Width / cols;

                    // 画水平线
                    for (int row = 1; row < rows; row++)
                    {
                        int y = row * baseHeight;
                        g.DrawLine(pen, 0, y, newImage.Width, y);
                    }

                    // 画垂直线
                    for (int col = 1; col < cols; col++)
                    {
                        int x = col * baseWidth;
                        g.DrawLine(pen, x, 0, x, newImage.Height);
                    }
                }
            }

            return newImage;
        }

        /// <summary>
        /// 接管新的原始预览图，并释放上一张原图和上一张分割线预览图。
        /// </summary>
        /// <param name="image">由参数窗体接管的新原始图。</param>
        private void SetSourceImage(Bitmap image)
        {
            Bitmap previousSource = _image;
            Image previousDisplay = pictureBoxShowImage.Image;
            if (ReferenceEquals(previousSource, image))
            {
                pictureBoxShowImage.Image = image;
                if (!ReferenceEquals(previousDisplay, image))
                    previousDisplay?.Dispose();
                return;
            }

            _image = image;
            pictureBoxShowImage.Image = image;

            if (!ReferenceEquals(previousDisplay, previousSource))
                previousDisplay?.Dispose();
            previousSource?.Dispose();
        }

        /// <summary>
        /// 显示带分割线的临时图，保留当前原始图供再次调整参数。
        /// </summary>
        /// <param name="image">由PictureBox接管的分割线预览图。</param>
        private void SetSplitPreviewImage(Bitmap image)
        {
            Image previousDisplay = pictureBoxShowImage.Image;
            if (ReferenceEquals(previousDisplay, image))
                return;

            pictureBoxShowImage.Image = image;
            if (!ReferenceEquals(previousDisplay, _image))
                previousDisplay?.Dispose();
        }

        /// <summary>
        /// 释放原始图和当前显示图，两个字段引用同一Bitmap时只释放一次。
        /// </summary>
        private void ReleaseImageResources()
        {
            Bitmap sourceImage = _image;
            Image displayImage = pictureBoxShowImage.Image;
            _image = null;
            pictureBoxShowImage.Image = null;

            if (!ReferenceEquals(displayImage, sourceImage))
                displayImage?.Dispose();
            sourceImage?.Dispose();
        }
    }
   
}
