using OpenCvSharp.Extensions;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Size = OpenCvSharp.Size;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._3_Detection.QRScan
{
    public partial class NodeParamFormQRScan : FormBase, INodeParamForm
    {
        private Process process;//所属流程
        private NodeBase node;//所属节点

        /// <summary>
        /// 二维码模型后台加载任务，所有检测共用同一个模型实例。
        /// </summary>
        private readonly Task<WeChatQRCode> _modelLoadTask;

        /// <summary>
        /// 参数窗体自有模型和预览资源是否已经释放。
        /// </summary>
        private int _resourcesReleased;

        public NodeParamFormQRScan(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            this.process = process;
            this.node = nodeBase;
            imageROIEditControl1.SetROIType2Draw(Forms.ShapeDraw.ROIType.Rectangle);
            Shown += NodeParamFormQRCodeIdentification_Shown;
            toolTip1.SetToolTip(label1, "减少噪点,仅限奇数,影响边缘检测图像");
            toolTip1.SetToolTip(label2, "较小的值会导致较少的对比度增强，而较大的值会允许更多的对比度增强但是噪声提高。通常，在 1.0 到 4.0 之间是常见的选择");
            toolTip1.SetToolTip(label3, "适用于光照不均的图像");
            toolTip1.SetToolTip(label4, "较小的块会导致更细致的对比度调整，而较大的块则会导致更平滑的结果");
            _modelLoadTask = Task.Run(LoadQrCodeModel);
        }

        /// <summary>
        /// 创建二维码模型实例，模型路径保持原有相对目录规则。
        /// </summary>
        /// <returns>加载完成的二维码模型。</returns>
        private static WeChatQRCode LoadQrCodeModel()
        {
            string detectCaffeModel = ".\\QRCodeModel\\detect.caffemodel";
            string detectPrototxt = ".\\QRCodeModel\\detect.prototxt";
            string srCaffeModel = ".\\QRCodeModel\\sr.caffemodel";
            string srPrototxt = ".\\QRCodeModel\\sr.prototxt";

            return WeChatQRCode.Create(
                detectPrototxt,
                detectCaffeModel,
                srPrototxt,
                srCaffeModel);
        }

        private void NodeParamFormQRCodeIdentification_Shown(object sender, EventArgs e)
        {
            UpdataImage();
        }

        public INodeParam Params { get; set; }

        public void SetParam2Form()
        {
            if (Params is NodeParamQRScan param)
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                checkBoxMoreParams.Checked = param.MoreParamsEnable;
                textBoxBlurSize.Text = param.GaussianBlur.ToString();
                checkBox1.Checked = param.ISHistogramEqualization;
                this.textBox2.Text = param.ClipLimit.ToString();
                this.textBox3.Text = param.TileGridSize.Width.ToString();
                //还原ROI
                imageROIEditControl1.SetROIs(param.ROIs);
            }
        }

        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            await process.RunForUpdateImages(node);
            UpdataImage();
        }

        public void UpdataImage()
        {
            Bitmap bitmap = null;
            try
            {
                bitmap = nodeSubscription1.GetValue<OutputImage>().Bitmaps[0].ToBitmap();
            }
            catch (Exception)
            {
                bitmap = null;
            }
            imageROIEditControl1.SetImage(bitmap);
        }

        private async void buttonRun_Click(object sender, EventArgs e)
        {
            try
            {
                await QRCodeDetect();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(ex.Message);
            }
        }

        public async Task<string[]> QRCodeDetect(bool show = true)
        {
            List<Mat> roiImages = null;
            Mat blurred = null;
            try
            {
                WeChatQRCode qrCode = await _modelLoadTask;
                if (qrCode == null)
                    throw new Exception("模型加载失败！");

                // 更新输入图像和获取ROI图像
                ReplacePictureBoxImage(pictureBoxCanny, null);
                UpdataImage();
                roiImages = imageROIEditControl1.GetROIImages();
                if (roiImages == null || roiImages.Count == 0)
                    throw new Exception("请先绘制有效的二维码检测区域！");

                // 处理图像
                blurred = await ImageProcessingasync(roiImages[0]);

                // 设置参数界面点击运行才需要刷新，节点正常运行调用时不需要刷新，降低耗时
                if (show)
                    ReplacePictureBoxImage(pictureBoxCanny, blurred.ToBitmap());

                // 检测二维码
                string[] Information = IdentifyQRCodesAndBarcodes(blurred, qrCode);

                return Information;
            }
            catch (Exception ex)
            {
                throw new Exception($"检测二维码失败，原因：{ex.Message}");
            }
            finally
            {
                blurred?.Dispose();
                if (roiImages != null)
                {
                    foreach (Mat roiImage in roiImages)
                        roiImage?.Dispose();
                }
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        /// <summary>
        /// 设置参数
        /// </summary>
        /// <returns></returns>
        private bool SaveParams()
        {
            try
            {
                NodeParamQRScan nodeParamQRCodeIdentification = new NodeParamQRScan();
                nodeParamQRCodeIdentification.Text1 = nodeSubscription1.GetText1();
                nodeParamQRCodeIdentification.Text2 = nodeSubscription1.GetText2();
                nodeParamQRCodeIdentification.MoreParamsEnable = checkBoxMoreParams.Checked;
                nodeParamQRCodeIdentification.GaussianBlur = int.Parse(textBoxBlurSize.Text);
                nodeParamQRCodeIdentification.ROIs = imageROIEditControl1.GetROIs();
                nodeParamQRCodeIdentification.ISHistogramEqualization = this.checkBox1.Checked;
                nodeParamQRCodeIdentification.ClipLimit = double.Parse(this.textBox2.Text);
                nodeParamQRCodeIdentification.TileGridSize = new Size(int.Parse(this.textBox3.Text), int.Parse(this.textBox3.Text));
                Params = nodeParamQRCodeIdentification;
            }
            catch (Exception)
            {
                MessageBoxTD.Show("参数设置异常，请检查参数设置是否合理！");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检测二维码
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        private string[] IdentifyQRCodesAndBarcodes(Mat map, WeChatQRCode QRCode)
        {
            Mat[] bbox = null;
            try
            {
                string[] results;  // 存放二维码解码内容

                QRCode.DetectAndDecode(map, out bbox, out results);

                return results;
            }
            finally
            {
                if (bbox != null)
                {
                    foreach (Mat box in bbox)
                        box?.Dispose();
                }
            }
        }

        private void checkBoxMoreParams_CheckedChanged(object sender, EventArgs e)
        {
            tableLayoutPanel2.Visible = checkBoxMoreParams.Checked;
        }

        public async Task<Mat> ImageProcessingasync(Mat image)
        {
            bool histogramEqualization = checkBox1.Checked;
            double clipLimit = double.Parse(textBox2.Text);
            int tileGridSize = int.Parse(textBox3.Text);
            int blurSize = int.Parse(textBoxBlurSize.Text);
            return await Task.Run(() =>
            {
                Mat blurred = null;
                try
                {
                    // 图像格式转换
                    Cv2.CvtColor(image, image, ColorConversionCodes.BGR2GRAY);

                    //直方图均衡化
                    if (histogramEqualization)
                    {
                        using (CLAHE claheLimited = Cv2.CreateCLAHE(clipLimit, new Size(tileGridSize, tileGridSize)))
                            claheLimited.Apply(image, image);
                    }

                    // 应用高斯模糊减少噪点
                    blurred = new Mat();
                    Cv2.GaussianBlur(image, blurred, new Size(blurSize, blurSize), 0);
                    Mat result = blurred;
                    blurred = null;
                    return result;
                }
                finally
                {
                    blurred?.Dispose();
                }
            });

        }

        /// <summary>
        /// 替换PictureBox图像并释放上一张GDI图像。
        /// </summary>
        /// <param name="pictureBox">需要更新的预览控件。</param>
        /// <param name="image">由预览控件接管的新图像。</param>
        private static void ReplacePictureBoxImage(PictureBox pictureBox, Image image)
        {
            Image previous = pictureBox.Image;
            if (ReferenceEquals(previous, image))
                return;

            pictureBox.Image = image;
            previous?.Dispose();
        }

        /// <summary>
        /// 释放二维码模型、ROI底图和最后一张边缘预览图。
        /// </summary>
        private void ReleaseImageResources()
        {
            if (System.Threading.Interlocked.Exchange(ref _resourcesReleased, 1) != 0)
                return;

            imageROIEditControl1.SetImage(null);
            ReplacePictureBoxImage(pictureBoxCanny, null);
            _modelLoadTask?.ContinueWith(
                task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                        task.Result?.Dispose();
                    else if (task.IsFaulted)
                        GC.KeepAlive(task.Exception);
                },
                TaskScheduler.Default);
        }
    }
}
