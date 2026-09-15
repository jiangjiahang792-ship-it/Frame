using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Forms.ShapeDraw;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop
{
    /// <summary>使用常用图像显示控件编辑可移动、缩放及旋转的裁剪ROI。</summary>
    public partial class NodeParamFormImageCrop : FormBase, INodeParamForm
    {
        /// <summary>所属流程，用于刷新上游图像。</summary>
        private readonly Process process;
        /// <summary>所属节点。</summary>
        private readonly NodeBase node;
        /// <summary>旧控件的初始视口尺寸，用于兼容未保存图像尺寸的旧ROI。</summary>
        private readonly System.Drawing.Size legacyViewportSize;
        /// <summary>旧参数需要等源图像可用后才能转换到图像坐标。</summary>
        private bool legacyRoisPending;

        /// <summary>初始化参数窗体及图像编辑器。</summary>
        public NodeParamFormImageCrop(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            this.process = process;
            node = nodeBase;
            legacyViewportSize = showImageControl1.ClientSize;
            SolRunParamControl.RefreshParamView += SolRunParamControl_RefreshParamView;
        }

        /// <summary>同步运行参数中的ROI启用状态。</summary>
        private void SolRunParamControl_RefreshParamView(object sender, EventArgs e)
        {
            if (Params is NodeParamImageCrop param)
                uiSwitch1.Active = param.RoiEnable;
        }

        /// <summary>已经保存的运行参数，不与界面编辑对象共享ROI实例。</summary>
        public INodeParam Params { get; set; }

        /// <summary>初始化上游图像订阅。</summary>
        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        /// <summary>恢复参数；旧方案首次取得图像时再转换控件坐标。</summary>
        public void SetParam2Form()
        {
            if (!(Params is NodeParamImageCrop param))
                return;

            nodeSubscription1.SetText(param.Text1 ?? string.Empty, param.Text2 ?? string.Empty);
            uiSwitch1.Active = param.RoiEnable;
            showImageControl1.ClearDynamicRoi();
            legacyRoisPending = param.ImageRois == null && param.ROIs != null && param.ROIs.Count > 0;
            if (param.ImageRois != null)
                LoadRoiRegions(param.ImageRois);
            else if (showImageControl1.Image != null)
                RestoreLegacyRois(showImageControl1.Image.Size);
        }

        /// <summary>把参数副本显示为可编辑旋转矩形。</summary>
        private void LoadRoiRegions(IEnumerable<ImageCropRoiRegion> regions)
        {
            TDJS_Vision.Forms.DispShowImage.ShowImageControl.IRoiShape selected = null;
            foreach (ImageCropRoiRegion region in regions)
            {
                if (region == null)
                    throw new InvalidOperationException("裁剪ROI参数为空。");
                var roi = showImageControl1.AddRoiRotatedRect(region.CenterY, region.CenterX,
                    (float)(region.Angle * Math.PI / 180), region.Width / 2, region.Height / 2);
                selected = roi;
            }
            if (selected != null)
                selected.IsSelected = true;
            showImageControl1.Invalidate();
        }

        /// <summary>旧方案保持原来的取整规则，转换后编辑不再依赖窗口大小。</summary>
        private void RestoreLegacyRois(System.Drawing.Size imageSize)
        {
            if (!legacyRoisPending || !(Params is NodeParamImageCrop param))
                return;
            var regions = new List<ImageCropRoiRegion>();
            foreach (ROI roi in param.ROIs)
            {
                if (roi == null)
                    throw new InvalidOperationException("旧版裁剪ROI参数为空。");
                Rect rect = ROI.ConvertClientRectToImageRect(roi.Rectangle, imageSize, legacyViewportSize);
                regions.Add(new ImageCropRoiRegion
                {
                    CenterX = rect.X + rect.Width / 2F, CenterY = rect.Y + rect.Height / 2F,
                    Width = rect.Width, Height = rect.Height, Angle = 0
                });
            }
            LoadRoiRegions(regions);
            legacyRoisPending = false;
        }

        /// <summary>读取图像坐标快照，保存后拖动控件不会修改运行参数。</summary>
        private List<ImageCropRoiRegion> ReadRoiRegions()
        {
            var regions = new List<ImageCropRoiRegion>();
            foreach (var info in showImageControl1.GetAllRotatedRectInfos())
                regions.Add(new ImageCropRoiRegion
                {
                    CenterX = info.Column, CenterY = info.Row,
                    Width = info.Length1 * 2, Height = info.Length2 * 2,
                    Angle = (float)(info.Phi * 180 / Math.PI)
                });
            return regions;
        }

        /// <summary>按已保存参数返回独立裁剪图像，调用方负责释放。</summary>
        public List<Mat> GetROIImages()
        {
            Mat source = GetInputMat(GetInputOutputImage());
            if (Params is NodeParamImageCrop param && param.ImageRois != null && param.ImageRois.Count > 0)
                return NodeImageCrop.CropRectifiedImages(source, param.ImageRois);
            return NodeImageCrop.CropImages(source, GetImageROIRects(source));
        }

        /// <summary>根据当前上游图像取得已保存的裁剪区域。</summary>
        public List<Rect> GetImageROIRects()
        {
            return GetImageROIRects(GetInputMat(GetInputOutputImage()));
        }

        /// <summary>运行只读取已保存参数，避免访问UI或进行Mat到Bitmap转换。</summary>
        public List<Rect> GetImageROIRects(Mat source)
        {
            if (!OutputImage.HasValidImage(source))
                throw new InvalidOperationException("订阅的图像为空！");
            var rects = new List<Rect>();
            if (Params is NodeParamImageCrop param)
            {
                if (param.ImageRois != null)
                {
                    foreach (ImageCropRoiRegion region in param.ImageRois)
                    {
                        if (region == null)
                            throw new InvalidOperationException("裁剪ROI参数为空。");
                        rects.Add(region.GetImageRect(source.Width, source.Height));
                    }
                }
                else if (param.ROIs != null)
                {
                    foreach (ROI roi in param.ROIs)
                    {
                        if (roi == null)
                            throw new InvalidOperationException("旧版裁剪ROI参数为空。");
                        rects.Add(ROI.ConvertClientRectToImageRect(roi.Rectangle,
                            new System.Drawing.Size(source.Width, source.Height), legacyViewportSize));
                    }
                }
            }
            if (rects.Count == 0)
                rects.Add(new Rect(0, 0, source.Width, source.Height));
            return rects;
        }

        /// <summary>获取上游订阅的完整图像输出。</summary>
        public OutputImage GetInputOutputImage()
        {
            return nodeSubscription1.GetValue<OutputImage>()
                ?? throw new InvalidOperationException("订阅的图像为空！");
        }

        /// <summary>从图像输出中读取第一张有效图像。</summary>
        public Mat GetInputMat(OutputImage outputImage)
        {
            if (outputImage?.Bitmaps == null || outputImage.Bitmaps.Count == 0 ||
                !OutputImage.HasValidImage(outputImage.Bitmaps[0]))
                throw new InvalidOperationException("订阅的图像为空！");
            return outputImage.Bitmaps[0];
        }

        /// <summary>获取上游可复用的灰度图缓存。</summary>
        public Mat GetInputGrayMat(OutputImage outputImage)
        {
            return OutputImage.HasValidImage(outputImage?.GrayImg) ? outputImage.GrayImg : null;
        }

        /// <summary>刷新图像并恢复旧ROI，不覆盖已经编辑的矩形。</summary>
        public Mat UpdataImage()
        {
            Mat image = GetInputMat(GetInputOutputImage());
            showImageControl1.SetImage(image.ToBitmap());
            RestoreLegacyRois(new System.Drawing.Size(image.Width, image.Height));
            showImageControl1.ShowFit();
            return image;
        }

        /// <summary>保存ROI快照；尚未取得图像的旧方案保持原参数。</summary>
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                List<ImageCropRoiRegion> regions = legacyRoisPending ? null : ReadRoiRegions();
                if (uiSwitch1.Active && regions != null && showImageControl1.Image != null)
                    foreach (ImageCropRoiRegion region in regions)
                        region.GetImageRect(showImageControl1.Image.Width, showImageControl1.Image.Height);
                Params = new NodeParamImageCrop
                {
                    Text1 = nodeSubscription1.GetText1(), Text2 = nodeSubscription1.GetText2(),
                    ROIs = legacyRoisPending ? (Params as NodeParamImageCrop)?.ROIs : null,
                    ImageRois = regions, RoiEnable = uiSwitch1.Active
                };
                Hide();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("保存裁剪ROI失败，原因：" + ex.Message);
            }
        }

        /// <summary>清空全部ROI，下一次保存后使用全图。</summary>
        private void buttonClearRois_Click(object sender, EventArgs e)
        {
            legacyRoisPending = false;
            showImageControl1.ClearDynamicRoi();
            showImageControl1.Focus();
        }

        /// <summary>异步刷新上游并获取图像，防止重复点击及未捕获异常。</summary>
        private async void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;
            try
            {
                await process.RunForUpdateImages(node);
                if (!IsDisposed)
                {
                    UpdataImage();
                    showImageControl1.Focus();
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBoxTD.Show("刷新图像失败，原因：" + ex.Message);
            }
            finally
            {
                if (!IsDisposed)
                    button3.Enabled = true;
            }
        }
    }
}
