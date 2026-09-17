using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using System.Diagnostics;
using System.Linq;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{

/// <summary>多区域组合建模、可视化几何编辑与轮廓擦除的独立窗体。</summary>
public partial class ContourTemplateEditor : FormBase
{
    /// <summary>可替换的匹配算法服务。</summary>
    private IShapeMatcher _matcher;
    /// <summary>当前显示及绘制区域所使用的模板源图像。</summary>
    private ImageFrame _image;
    /// <summary>模型来源图像快照，重新加载搜索图后仍可编辑模型。</summary>
    private ImageFrame _modelImage;
    /// <summary>已确认模型在来源图中的 ROI。</summary>
    private Rectangle _modelRoi;
    /// <summary>模型是否来源于当前搜索图像。</summary>
    private bool _modelOnCurrentImage;
    /// <summary>后台操作期间禁止再次编辑及释放模型。</summary>
    private bool _busy;

    /// <summary>设计器和运行共用构造函数；原生服务直到建模时才加载DLL。</summary>
    public ContourTemplateEditor()
    {
        _matcher = new NativeShapeMatcher();
        InitializeComponent();
        InitializeRegionEditing();
        Shown += Form_Shown;
        UpdateActionState();
    }

    /// <summary>首次显示时将窗体限制在当前屏幕工作区，避免高 DPI 下超出屏幕。</summary>
    protected override void OnLoad(EventArgs e)
    {
        FitWindowToWorkingArea(Screen.FromControl(this).WorkingArea);
        base.OnLoad(e);
    }

    /// <summary>跨显示器 DPI 变化后，按新屏幕约束窗体并重新布局画布。</summary>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        FitWindowToWorkingArea(Screen.FromControl(this).WorkingArea);
    }

    /// <summary>按物理工作区限制最小尺寸和初始尺寸，不让缩放后的窗体超出屏幕。</summary>
    internal void FitWindowToWorkingArea(Rectangle workingArea)
    {
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
            return;
        float scale = DeviceDpi / 96F;
        MinimumSize = new Size(
            Math.Min((int)Math.Round(900 * scale), (int)Math.Round(workingArea.Width * 0.85)),
            Math.Min((int)Math.Round(600 * scale), (int)Math.Round(workingArea.Height * 0.85)));
        if (WindowState != FormWindowState.Normal)
            return;
        Size = new Size(Math.Min(Width, workingArea.Width), Math.Min(Height, workingArea.Height));
        if (StartPosition == FormStartPosition.CenterScreen)
            Location = new Point(workingArea.Left + (workingArea.Width - Width) / 2,
                workingArea.Top + (workingArea.Height - Height) / 2);
        PerformLayout();
    }

    /// <summary>选择新的模板源图，重新绘制区域后才可保存。</summary>
    private void LoadImageButton_Click(object sender, EventArgs e)
    {
        if (imageOpenFileDialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            ImageFrame loaded = ImageFrame.Load(imageOpenFileDialog.FileName);
            ImageFrame previous = _image;
            _image = loaded;
            imageCanvas.SetImage(loaded);
            previous?.Dispose();
            _modelOnCurrentImage = false;
            _templateDirty = true;
            RefreshRegionList();
            ClearResults();
            statusLabel.Text = "选择顶部绘制工具添加区域，多个区域合并创建模板";
            UpdateActionState();
        }
        catch (Exception exception)
        {
            ShowError(exception, "加载图像失败");
        }
    }

    /// <summary>创建组合模板；尚无区域时先启动矩形工具。</summary>
    private async void CreateModelButton_Click(object sender, EventArgs e)
    {
        if (_image == null || _busy)
            return;
        ClearResults();
        if (imageCanvas.HasRegionGesture) { statusLabel.Text = "请先结束当前区域绘制，或按Esc取消"; return; }
        if (imageCanvas.RegionCount == 0)
        {
            imageCanvas.SetRegionTool(TemplateRegionKind.矩形);
            statusLabel.Text = "左键拖动绘制矩形，松开生成；再次点击“创建模板”组合建模";
        }
        else await ConfirmCreateAsync();
        UpdateActionState();
    }

    /// <summary>显示模型来源图及实际轮廓点，进入待提交的删除模式。</summary>
    private void EraseButton_Click(object sender, EventArgs e)
    {
        if (_modelImage == null || !_matcher.HasModel || _busy || _templateDirty)
            return;
        // 当前显示的是同源图或同一模板快照时保留放大位置，避免进入涂抹突然回到整图。
        if (!_modelOnCurrentImage) imageCanvas.SetImage(_modelImage);
        imageCanvas.SetModel(_modelRoi, _matcher.GetModelFeatures(), _matcher.GetModelContours());
        imageCanvas.BeginErase();
        ClearResults();
        statusLabel.Text = "左键涂抹，右键确认；滚轮缩放，中键拖动，Esc取消";
        UpdateActionState();
    }

    /// <summary>更新画笔大小，不改动图像或已提交模型。</summary>
    private void BrushSizeNumeric_ValueChanged(object sender, EventArgs e)
    {
        imageCanvas.BrushSize = decimal.ToInt32(brushSizeNumeric.Value);
        imageCanvas.Invalidate();
    }

    /// <summary>工具栏围绕画布中心放大，不提交当前编辑。</summary>
    private void ZoomInButton_Click(object sender, EventArgs e)
    {
        imageCanvas.ZoomAt(new Point(imageCanvas.ClientSize.Width / 2, imageCanvas.ClientSize.Height / 2), 1.25D);
    }

    /// <summary>工具栏围绕画布中心缩小。</summary>
    private void ZoomOutButton_Click(object sender, EventArgs e)
    {
        imageCanvas.ZoomAt(new Point(imageCanvas.ClientSize.Width / 2, imageCanvas.ClientSize.Height / 2), 0.8D);
    }

    /// <summary>恢复整图显示，保留未确认的涂抹遮罩。</summary>
    private void FitImageButton_Click(object sender, EventArgs e) { imageCanvas.ResetView(); }

    /// <summary>更新实际像素缩放比例及无图状态。</summary>
    private void ImageCanvas_ViewChanged(object sender, EventArgs e)
    {
        double scale = imageCanvas.ImageScale;
        zoomLabel.Text = scale > 0 ? $"{scale * 100:F0}%" : "--";
        zoomInButton.Enabled = zoomOutButton.Enabled = fitImageButton.Enabled = scale > 0;
    }

    /// <summary>自动时禁止手改阈值；取消勾选后可从最近显示值开始微调。</summary>
    private void AutoContrastCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        contrastNumeric.Enabled = !_busy && !autoContrastCheckBox.Checked;
    }

    /// <summary>在画布上右键提交当前框选或删除操作。</summary>
    private async void ImageCanvas_ConfirmRequested(object sender, EventArgs e)
    {
        if (_busy)
            return;
        if (imageCanvas.EditorMode == CanvasEditorMode.涂抹)
            await ConfirmEraseAsync();
    }

    /// <summary>组合区域构建成功后原子替换模型；失败保留旧模型但禁止保存过期结果。</summary>
    private async Task ConfirmCreateAsync()
    {
        if (_image == null)
            return;
        List<TemplateRegion> regions = imageCanvas.CopyRegions();
        Rectangle roi;
        try { roi = TemplateRegionMask.Bounds(regions, _image.Width, _image.Height); }
        catch (Exception exception) { statusLabel.Text = exception.Message; return; }
        if (roi.Width < 8 || roi.Height < 8)
        {
            statusLabel.Text = "组合区域的外接范围至少需要 8 × 8 像素";
            return;
        }
        ImageFrame source = _image;
        CreateModelOptions options = ReadCreateModelOptions();
        SetBusy(true, "正在创建模板……");
        ImageFrame snapshot = null;
        IShapeMatcher candidate = new NativeShapeMatcher();
        try
        {
            snapshot = ImageFrame.CopyFrom(source.Mat);
            TimeSpan elapsed = await Task.Run(() =>
            {
                long started = Stopwatch.GetTimestamp();
                TemplateRegionMask.Create(candidate, source.Mat, roi, options, regions);
                return TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency);
            });
            _matcher.Dispose(); _matcher = candidate; candidate = null;
            _modelOptions = NodeParamContourMatch.CopyCreate(options);
            _eraseMasks.Clear();
            _confirmedRegions = regions;
            _templateDirty = false;
            _modelImage?.Dispose();
            _modelImage = snapshot;
            snapshot = null;
            _modelRoi = roi;
            _modelOnCurrentImage = true;
            imageCanvas.SetModel(roi, _matcher.GetModelFeatures(), _matcher.GetModelContours());
            double usedContrast = _matcher.ModelContrast;
            if (options.AutoContrast)
                contrastNumeric.Value = ContourCompatibility.Clamp((decimal)usedContrast, contrastNumeric.Minimum, contrastNumeric.Maximum);
            modelTimeLabel.Text = $"建模耗时：{elapsed.TotalMilliseconds:F2} ms";
            statusLabel.Text = $"模板已确认（{_matcher.GetModelFeatures().Count} 个特征）；"
                + $"组合 {regions.Count(region => region.Enabled)} 个区域；可擦除细节或点击“确定”完成";
        }
        catch (Exception exception)
        {
            ShowError(exception, "创建模板失败");
        }
        finally
        {
            snapshot?.Dispose();
            candidate?.Dispose();
            SetBusy(false, statusLabel.Text ?? string.Empty);
        }
    }

    /// <summary>右键提交删除区域；失败时保持原模型和当前标红预览。</summary>
    private async Task ConfirmEraseAsync()
    {
        if (imageCanvas.PendingFeatureCount == 0)
        {
            imageCanvas.EndEdit();
            statusLabel.Text = "未涂中轮廓，模板保持不变";
            UpdateActionState();
            return;
        }
        byte[] eraseMask = imageCanvas.CopyEraseMask();
        int before = _matcher.GetModelFeatures().Count;
        SetBusy(true, "正在删除模板细节……");
        try
        {
            TimeSpan elapsed = await Task.Run(() =>
            {
                long started = Stopwatch.GetTimestamp();
                _matcher.EraseModelFeatures(eraseMask, _modelRoi.Width, _modelRoi.Height);
                return TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency);
            });
            _eraseMasks.Add(eraseMask);
            IReadOnlyList<PointF> features = _matcher.GetModelFeatures();
            imageCanvas.SetModel(_modelRoi, features, _matcher.GetModelContours());
            modelTimeLabel.Text = $"编辑耗时：{elapsed.TotalMilliseconds:F2} ms";
            statusLabel.Text = $"删除已确认：删除 {before - features.Count} 个特征，剩余 {features.Count} 个";
        }
        catch (Exception exception)
        {
            ShowError(exception, "删除失败，原模板保留；可按 Esc 取消本次涂抹");
        }
        finally
        {
            SetBusy(false, statusLabel.Text ?? string.Empty);
        }
    }

    /// <summary>显示本次待删除特征数量。</summary>
    private void ImageCanvas_EditChanged(object sender, EventArgs e)
    {
        statusLabel.Text = imageCanvas.EditorMode == CanvasEditorMode.涂抹
            ? $"待删除 {imageCanvas.PendingFeatureCount} 个特征；右键确认删除，Esc 取消"
            : $"共 {imageCanvas.RegionCount} 个区域；点击“创建模板”组合建模";
    }

    /// <summary>取消临时编辑后恢复操作按钮。</summary>
    private void ImageCanvas_EditCancelled(object sender, EventArgs e)
    {
        statusLabel.Text = "本次编辑已取消，已确认的模板保持不变";
        UpdateActionState();
    }

    /// <summary>后台任务运行时禁止关闭，避免释放正在使用的图像和原生句柄。</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            statusLabel.Text = "当前操作尚未结束，请完成后关闭";
        }
        base.OnFormClosing(e);
    }

    /// <summary>清理过期匹配结果。</summary>
    private void ClearResults()
    {
        imageCanvas.SetMatches(Array.Empty<ShapeMatchResult>());
    }

    /// <summary>读取本次建模参数快照。</summary>
    private CreateModelOptions ReadCreateModelOptions() => new CreateModelOptions()
    {
        PyramidLevels = decimal.ToInt32(modelLevelsNumeric.Value),
        AngleStartDegrees = decimal.ToDouble(modelAngleStartNumeric.Value),
        AngleEndDegrees = decimal.ToDouble(modelAngleEndNumeric.Value),
        AngleStepDegrees = decimal.ToDouble(angleStepNumeric.Value),
        Contrast = decimal.ToDouble(contrastNumeric.Value),
        AutoContrast = autoContrastCheckBox.Checked,
        MinimumContrast = decimal.ToDouble(minimumContrastNumeric.Value),
        FeatureCount = decimal.ToInt32(featureCountNumeric.Value),
        Metric = metricComboBox.SelectedIndex == 1 ? ShapeMetric.忽略极性 : ShapeMetric.使用极性
    };

    /// <summary>统一设置忙碌状态并锁定交互。</summary>
    private void SetBusy(bool busy, string message)
    {
        if (busy) _busy = true;
        UseWaitCursor = busy;
        imageCanvas.Enabled = !busy;
        parameterLayout.Enabled = !busy;
        subscriptionPanel.Enabled = !busy;
        zoomActions.Enabled = !busy;
        statusLabel.Text = message;
        UpdateActionStateForBusy(busy);
        // 操作完成必须同时意味着画布和缩放按钮已恢复可用。
        if (!busy) _busy = false;
    }

    /// <summary>更新绘制、创建和确认操作状态。</summary>
    private void UpdateActionState() { UpdateActionStateForBusy(_busy); }

    /// <summary>按目标忙碌状态更新控件，完成后再发布结束标记。</summary>
    private void UpdateActionStateForBusy(bool busy)
    {
        bool idle = !busy && imageCanvas.EditorMode == CanvasEditorMode.查看;
        loadImageButton.Enabled = idle;
        bool ready = idle && !imageCanvas.HasRegionGesture;
        saveButton.Enabled = ready && ((!_templateDirty && _matcher.HasModel) || imageCanvas.RegionCount > 0);
        closeButton.Enabled = !busy;
        createModelButton.Enabled = ready && _image != null;
        brushSizeNumeric.Enabled = !busy;
        contrastNumeric.Enabled = !busy && !autoContrastCheckBox.Checked;
        UpdateRegionActions(busy);
    }

    /// <summary>显示错误并记录诊断信息。</summary>
    private void ShowError(Exception exception, string operation)
    {
        LogHelper.AddLog(MsgLevel.Fatal, operation + "：" + exception.Message, true);
        statusLabel.Text = operation;
        MessageBox.Show(this, $"{operation}。{Environment.NewLine}{exception.Message}",
            "操作提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

}
}
