using OpenCvSharp;
using OpenCvSharp.Extensions;
using TieCornerInspection.WinForms.Controls;
using TieCornerInspection.WinForms.Models;
using TieCornerInspection.WinForms.Services;
using CvRect = OpenCvSharp.Rect;

namespace TieCornerInspection.WinForms;

/// <summary>
/// 提供扎带右端缺角检测的图像加载、ROI 框选、参数编辑和结果展示界面。
/// </summary>
public partial class MainForm : Form
{
    /// <summary>
    /// 保存可替换的右端检测器实现。
    /// </summary>
    private readonly IRightEndInspector _inspector;

    /// <summary>
    /// 保存当前加载的 OpenCV 原图。
    /// </summary>
    private Mat? _sourceImage;

    /// <summary>
    /// 保存原图 PictureBox 正在显示的位图。
    /// </summary>
    private Bitmap? _sourceBitmap;

    /// <summary>
    /// 保存检测标注 PictureBox 正在显示的位图。
    /// </summary>
    private Bitmap? _annotatedBitmap;

    /// <summary>
    /// 保存二值图 PictureBox 正在显示的位图。
    /// </summary>
    private Bitmap? _binaryBitmap;

    /// <summary>
    /// 保存端头提取预览控件正在显示的位图。
    /// </summary>
    private Bitmap? _endRegionPreviewBitmap;

    /// <summary>
    /// 保存鼠标框选 ROI 时的原图起点。
    /// </summary>
    private System.Drawing.Point _dragStart;

    /// <summary>
    /// 表示用户是否正在原图上拖动框选 ROI。
    /// </summary>
    private bool _isDragging;

    /// <summary>
    /// 表示代码是否正在批量同步 ROI 数值，防止事件递归。
    /// </summary>
    private bool _isUpdatingRegionControls;

    /// <summary>
    /// 使用默认 OpenCV 检测器创建主窗体。
    /// </summary>
    public MainForm()
        : this(new OpenCvRightEndInspector())
    {
    }

    /// <summary>
    /// 使用指定检测器创建主窗体，便于以后注入替代算法。
    /// </summary>
    /// <param name="inspector">右端检测器。</param>
    public MainForm(IRightEndInspector inspector)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        InitializeComponent();
    }

    /// <summary>
    /// 窗体首次显示时尝试加载随程序复制的样图。
    /// </summary>
    /// <param name="e">事件参数。</param>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        string samplePath = Path.Combine(AppContext.BaseDirectory, "samples", "扎带右端样图.png");
        if (File.Exists(samplePath))
        {
            LoadImage(samplePath, useSampleRegion: true);
        }
    }

    /// <summary>
    /// 窗体关闭时释放 Bitmap 和 OpenCV Mat，避免非托管内存泄漏。
    /// </summary>
    /// <param name="e">事件参数。</param>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        ReplaceBitmap(ref _sourceBitmap, pictureSource, null);
        ReplaceBitmap(ref _annotatedBitmap, pictureAnnotated, null);
        ReplaceBitmap(ref _binaryBitmap, pictureBinary, null);
        ReplaceBitmap(ref _endRegionPreviewBitmap, pictureEndRegions, null);
        _sourceImage?.Dispose();
        _sourceImage = null;
        base.OnFormClosed(e);
    }

    /// <summary>
    /// 弹出文件选择框并加载用户选择的图像。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">事件参数。</param>
    private void ButtonOpen_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "选择扎带图像",
            Filter = "图像文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadImage(dialog.FileName, useSampleRegion: false);
        }
    }

    /// <summary>
    /// 加载随 Demo 提供的样图。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">事件参数。</param>
    private void ButtonSample_Click(object? sender, EventArgs e)
    {
        string samplePath = Path.Combine(AppContext.BaseDirectory, "samples", "扎带右端样图.png");
        if (!File.Exists(samplePath))
        {
            MessageBox.Show(this, "未找到 samples 文件夹中的扎带右端样图。", "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        LoadImage(samplePath, useSampleRegion: true);
    }

    /// <summary>
    /// 以四个图像查看器各自中心为锚点同步放大一级。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">事件参数。</param>
    private void ButtonZoomIn_Click(object? sender, EventArgs e)
    {
        pictureSource.ZoomIn();
        pictureAnnotated.ZoomIn();
        pictureBinary.ZoomIn();
        pictureEndRegions.ZoomIn();
        statusLabel.Text = "全部图像已放大一级；也可以把鼠标放在目标位置后滚轮缩放。";
    }

    /// <summary>
    /// 以四个图像查看器各自中心为锚点同步缩小一级。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">事件参数。</param>
    private void ButtonZoomOut_Click(object? sender, EventArgs e)
    {
        pictureSource.ZoomOut();
        pictureAnnotated.ZoomOut();
        pictureBinary.ZoomOut();
        pictureEndRegions.ZoomOut();
        statusLabel.Text = "全部图像已缩小一级。";
    }

    /// <summary>
    /// 让四个图像查看器同时恢复完整适应窗口显示。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">事件参数。</param>
    private void ButtonResetView_Click(object? sender, EventArgs e)
    {
        pictureSource.ResetView();
        pictureAnnotated.ResetView();
        pictureBinary.ResetView();
        pictureEndRegions.ResetView();
        statusLabel.Text = "全部图像已恢复适应窗口显示。";
    }

    /// <summary>
    /// 在后台执行检测并把结果刷新到图像和表格。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">事件参数。</param>
    private async void ButtonInspect_Click(object? sender, EventArgs e)
    {
        if (_sourceImage is null || _sourceImage.Empty())
        {
            MessageBox.Show(this, "请先打开一张扎带图像。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        InspectionParameters parameters;
        try
        {
            parameters = ReadParameters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        buttonInspect.Enabled = false;
        statusLabel.Text = "正在检测，请稍候……";

        try
        {
            using Mat sourceCopy = _sourceImage.Clone();
            using InspectionResult result = await Task.Run(() => _inspector.Inspect(sourceCopy, parameters));
            Bitmap annotated = BitmapConverter.ToBitmap(result.AnnotatedImage);
            Bitmap binary = BitmapConverter.ToBitmap(result.BinaryRegion);
            Bitmap endRegionPreview = BitmapConverter.ToBitmap(result.EndRegionPreview);
            ReplaceBitmap(ref _annotatedBitmap, pictureAnnotated, annotated);
            ReplaceBitmap(ref _binaryBitmap, pictureBinary, binary);
            ReplaceBitmap(ref _endRegionPreviewBitmap, pictureEndRegions, endRegionPreview);
            ShowResults(result.Regions);
            resultTabControl.SelectedTab = endRegionTabPage;
        }
        catch (Exception ex)
        {
            statusLabel.Text = "检测失败";
            MessageBox.Show(this, ex.Message, "检测失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            buttonInspect.Enabled = true;
        }
    }

    /// <summary>
    /// 加载支持中文路径的图像并初始化显示和 ROI。
    /// </summary>
    /// <param name="path">图像文件路径。</param>
    /// <param name="useSampleRegion">是否使用针对随附样图标定的 ROI。</param>
    private void LoadImage(string path, bool useSampleRegion)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            using Mat buffer = Mat.FromPixelData(bytes.Length, 1, MatType.CV_8UC1, bytes);
            Mat image = Cv2.ImDecode(buffer, ImreadModes.Color);
            if (image.Empty())
            {
                image.Dispose();
                throw new InvalidDataException("图像解码失败，请确认文件格式有效。 ");
            }

            _sourceImage?.Dispose();
            _sourceImage = image;
            ReplaceBitmap(ref _sourceBitmap, pictureSource, BitmapConverter.ToBitmap(image));
            ReplaceBitmap(ref _annotatedBitmap, pictureAnnotated, null);
            ReplaceBitmap(ref _binaryBitmap, pictureBinary, null);
            ReplaceBitmap(ref _endRegionPreviewBitmap, pictureEndRegions, null);
            resultGrid.Rows.Clear();

            CvRect region = useSampleRegion && image.Width == 1920 && image.Height == 1280
                ? new CvRect(810, 50, 147, 1125)
                : CreateDefaultRegion(image.Size());
            SetRegionControls(region);
            Text = $"扎带右端缺角检测 Demo - {Path.GetFileName(path)}";
            statusLabel.Text = $"已加载：{image.Width} × {image.Height}，请框选右端区域后执行检测。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "打开图像失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 按图像比例生成适合右端区域的默认 ROI。
    /// </summary>
    /// <param name="size">原图尺寸。</param>
    /// <returns>默认右端检测区域。</returns>
    private static CvRect CreateDefaultRegion(OpenCvSharp.Size size)
    {
        int x = (int)Math.Round(size.Width * 0.42);
        int y = (int)Math.Round(size.Height * 0.04);
        // 加宽默认 ROI，确保圆头朝左或朝右时都能同时包含一段齿纹和完整端头。
        int width = Math.Max(20, (int)Math.Round(size.Width * 0.16));
        int height = Math.Max(20, (int)Math.Round(size.Height * 0.88));
        return new CvRect(x, y, Math.Min(width, size.Width - x), Math.Min(height, size.Height - y));
    }

    /// <summary>
    /// 从界面读取并组装强类型检测参数。
    /// </summary>
    /// <returns>当前检测参数。</returns>
    private InspectionParameters ReadParameters()
    {
        if (_sourceImage is null)
        {
            throw new InvalidOperationException("请先打开图像。 ");
        }

        CvRect region = new(
            Decimal.ToInt32(numericRoiX.Value),
            Decimal.ToInt32(numericRoiY.Value),
            Decimal.ToInt32(numericRoiWidth.Value),
            Decimal.ToInt32(numericRoiHeight.Value));

        if (region.Right > _sourceImage.Width || region.Bottom > _sourceImage.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "右端检测区域超出图像范围。 ");
        }

        return new InspectionParameters
        {
            RegionOfInterest = region,
            BlackThreshold = Decimal.ToByte(numericBlackThreshold.Value),
            MinimumComponentArea = Decimal.ToInt32(numericMinimumArea.Value),
            MinimumComponentWidth = Decimal.ToInt32(numericMinimumWidth.Value),
            MinimumComponentHeight = Decimal.ToInt32(numericMinimumHeight.Value),
            MaximumComponentHeight = Decimal.ToInt32(numericMaximumHeight.Value),
            MorphologyKernelSize = Decimal.ToInt32(numericKernelSize.Value),
            TextureSensitivity = Decimal.ToInt32(numericTextureSensitivity.Value),
            EndBoundaryOffsetPixels = Decimal.ToInt32(numericEndBoundaryOffset.Value)
        };
    }

    /// <summary>
    /// 把端头截取结果填入表格。
    /// </summary>
    /// <param name="regions">从上到下排列的区域结果。</param>
    private void ShowResults(IReadOnlyList<InspectionRegionResult> regions)
    {
        resultGrid.Rows.Clear();

        foreach (InspectionRegionResult region in regions)
        {
            resultGrid.Rows.Add(
                region.Index,
                region.BoundingBox.X,
                region.BoundingBox.Y,
                region.BoundingBox.Width,
                region.BoundingBox.Height,
                region.ComponentArea,
                region.HasEndBoundary ? region.EndBoundaryBounds.Right.ToString() : "-",
                region.HasEndBoundary ? region.ExtractedEndBounds.Width.ToString() : "-",
                GetBoundarySourceText(region.BoundaryLocationSource));
        }

        if (regions.Count == 0)
        {
            statusLabel.Text = "未找到有效黑色区域，请调整 ROI、黑色阈值或尺寸过滤参数。";
            return;
        }

        int extractedCount = regions.Count(region => region.HasEndBoundary);
        int missingCount = regions.Count - extractedCount;
        int localCount = regions.Count(region => region.BoundaryLocationSource == EndBoundaryLocationSource.LocalTexture);
        int consensusCount = regions.Count(region => region.BoundaryLocationSource == EndBoundaryLocationSource.ImageConsensus);
        statusLabel.Text = $"检测到 {regions.Count} 根扎带；成功提取 {extractedCount} 个端头（局部纹理 {localCount}，整体补齐 {consensusCount}）；未定位 {missingCount} 个。";
    }

    /// <summary>
    /// 把端头起点的定位来源转换为界面中文。
    /// </summary>
    /// <param name="source">端头边界定位来源。</param>
    /// <returns>用于结果表格的中文说明。</returns>
    private static string GetBoundarySourceText(EndBoundaryLocationSource source)
    {
        return source switch
        {
            EndBoundaryLocationSource.LocalTexture => "局部纹理",
            EndBoundaryLocationSource.ImageConsensus => "整体补齐",
            _ => "未找到"
        };
    }

    /// <summary>
    /// 替换可缩放查看器中的位图并释放旧位图。
    /// </summary>
    /// <param name="storage">保存位图引用的字段。</param>
    /// <param name="pictureBox">目标可缩放图像查看器。</param>
    /// <param name="replacement">新位图，可为空。</param>
    private static void ReplaceBitmap(ref Bitmap? storage, ZoomableImageBox pictureBox, Bitmap? replacement)
    {
        pictureBox.Image = null;
        storage?.Dispose();
        storage = replacement;
        pictureBox.Image = storage;
    }

    /// <summary>
    /// 鼠标按下时开始在原图坐标系中框选 ROI。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">鼠标事件参数。</param>
    private void PictureSource_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !TryPicturePointToImage(e.Location, out System.Drawing.Point imagePoint))
        {
            return;
        }

        _dragStart = imagePoint;
        _isDragging = true;
        pictureSource.Capture = true;
    }

    /// <summary>
    /// 鼠标移动时实时更新 ROI 数值和红色框线。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">鼠标事件参数。</param>
    private void PictureSource_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging || !TryPicturePointToImage(e.Location, out System.Drawing.Point current))
        {
            return;
        }

        int x = Math.Min(_dragStart.X, current.X);
        int y = Math.Min(_dragStart.Y, current.Y);
        int width = Math.Max(1, Math.Abs(current.X - _dragStart.X));
        int height = Math.Max(1, Math.Abs(current.Y - _dragStart.Y));
        SetRegionControls(new CvRect(x, y, width, height));
    }

    /// <summary>
    /// 鼠标抬起时结束 ROI 框选。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">鼠标事件参数。</param>
    private void PictureSource_MouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
        pictureSource.Capture = false;
    }

    /// <summary>
    /// 在原图可缩放查看器上绘制当前 ROI 红框和左侧切割线。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">绘图事件参数。</param>
    private void PictureSource_Paint(object? sender, PaintEventArgs e)
    {
        if (_sourceImage is null || pictureSource.Image is null)
        {
            return;
        }

        CvRect roi = new(
            Decimal.ToInt32(numericRoiX.Value),
            Decimal.ToInt32(numericRoiY.Value),
            Decimal.ToInt32(numericRoiWidth.Value),
            Decimal.ToInt32(numericRoiHeight.Value));
        RectangleF shownRegion = pictureSource.ImageToClientRectangle(
            new RectangleF(roi.X, roi.Y, roi.Width, roi.Height));

        using Pen regionPen = new(Color.Red, 2);
        using Pen cutPen = new(Color.Magenta, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        e.Graphics.DrawRectangle(regionPen, shownRegion.X, shownRegion.Y, shownRegion.Width, shownRegion.Height);
        e.Graphics.DrawLine(cutPen, shownRegion.Left, shownRegion.Top, shownRegion.Left, shownRegion.Bottom);
    }

    /// <summary>
    /// ROI 数值改变时刷新原图上的框线。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">事件参数。</param>
    private void RegionValueChanged(object? sender, EventArgs e)
    {
        if (!_isUpdatingRegionControls)
        {
            pictureSource.Invalidate();
        }
    }

    /// <summary>
    /// 把可缩放查看器坐标转换到原图坐标，并把点限制在图像范围内。
    /// </summary>
    /// <param name="picturePoint">图像查看器客户区坐标。</param>
    /// <param name="imagePoint">转换后的原图坐标。</param>
    /// <returns>点是否落在实际图像显示范围内。</returns>
    private bool TryPicturePointToImage(System.Drawing.Point picturePoint, out System.Drawing.Point imagePoint)
    {
        return pictureSource.TryClientToImage(picturePoint, out imagePoint);
    }

    /// <summary>
    /// 把 ROI 同步到四个数值框并刷新原图显示。
    /// </summary>
    /// <param name="region">原图坐标系中的 ROI。</param>
    private void SetRegionControls(CvRect region)
    {
        if (_sourceImage is null)
        {
            return;
        }

        int x = Math.Clamp(region.X, 0, _sourceImage.Width - 1);
        int y = Math.Clamp(region.Y, 0, _sourceImage.Height - 1);
        int width = Math.Clamp(region.Width, 1, _sourceImage.Width - x);
        int height = Math.Clamp(region.Height, 1, _sourceImage.Height - y);

        _isUpdatingRegionControls = true;
        try
        {
            numericRoiX.Maximum = _sourceImage.Width - 1;
            numericRoiY.Maximum = _sourceImage.Height - 1;
            numericRoiWidth.Maximum = _sourceImage.Width;
            numericRoiHeight.Maximum = _sourceImage.Height;
            numericRoiX.Value = x;
            numericRoiY.Value = y;
            numericRoiWidth.Value = width;
            numericRoiHeight.Value = height;
        }
        finally
        {
            _isUpdatingRegionControls = false;
        }

        pictureSource.Invalidate();
    }
}
