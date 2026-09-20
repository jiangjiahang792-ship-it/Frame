using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>独立模板编辑器的数据交换，只保存创建参数与轮廓，不承担节点搜索。</summary>
    public partial class ContourTemplateEditor
    {
        /// <summary>传入或已提交的模型快照。</summary>
        private NodeParamContourMatch _savedParameters;
        /// <summary>已确认模型创建参数。</summary>
        private CreateModelOptions _modelOptions;
        /// <summary>按提交顺序记录的删除遮罩。</summary>
        private readonly List<byte[]> _eraseMasks = new List<byte[]>();
        /// <summary>是否需要恢复传入模型。</summary>
        private bool _needsModelRestore;
        /// <summary>使用独立副本与模板管理窗口交换数据。</summary>
        public NodeParamContourMatch Params { get { return _savedParameters?.Copy(); } set { _savedParameters = value?.Copy(); } }
        /// <summary>编辑器恢复模型时使用的保存快照。</summary>
        private NodeParamContourMatch SavedParameters { get { return _savedParameters; } }
        /// <summary>载入输入图像或已保存的模板源图。</summary>
        internal void Configure(NodeParamContourMatch model, OpenCvSharp.Mat source)
        {
            Params = model;
            if (source != null) _image = ImageFrame.CopyFrom(source);
            else if (model?.ModelImageBytes != null) _image = ImageFrame.Decode(model.ModelImageBytes);
            if (_image != null) imageCanvas.SetImage(_image);
            UpdateActionState();
        }
        /// <summary>按原Demo范围恢复创建参数。</summary>
        public void SetParam2Form()
        {
            var parameters = SavedParameters; if (parameters == null) return;
            _refreshingRegionUi = true;
            try
            {
            var options = parameters.CreateOptions ?? parameters.ModelOptions;
            NativeShapeMatcher.ValidateCreate(options);
            modelLevelsNumeric.Value = options.PyramidLevels; modelAngleStartNumeric.Value = (decimal)options.AngleStartDegrees;
            modelAngleEndNumeric.Value = (decimal)options.AngleEndDegrees; angleStepNumeric.Value = (decimal)options.AngleStepDegrees;
            contrastNumeric.Value = (decimal)options.Contrast; minimumContrastNumeric.Value = (decimal)options.MinimumContrast;
            featureCountNumeric.Value = options.FeatureCount; metricComboBox.SelectedIndex = (int)options.Metric;
            autoContrastCheckBox.Checked = options.AutoContrast;
            brushSizeNumeric.Value = ContourCompatibility.Clamp(parameters.BrushSize, (int)brushSizeNumeric.Minimum, (int)brushSizeNumeric.Maximum);
            _needsModelRestore = true;
            }
            finally { _refreshingRegionUi = false; }
        }
        /// <summary>打开独立窗口后延迟恢复原生模板。</summary>
        private async void Form_Shown(object sender, EventArgs e)
        {
            try { SetParam2Form(); if (_needsModelRestore) await RestoreEditorAsync(); }
            catch (Exception exception) { statusLabel.Text = "模板恢复失败：" + exception.Message; }
        }
        /// <summary>重建编辑模型；失败保留原模型并明确反馈。</summary>
        private async Task RestoreEditorAsync()
        {
            NodeParamContourMatch parameters = SavedParameters;
            if (parameters == null) return;
            SetBusy(true, "正在恢复轮廓模板……");
            IShapeMatcher candidate = null; ImageFrame image = null;
            try
            {
                candidate = await Task.Run(() => ContourMatchSession.Restore(parameters));
                image = ImageFrame.Decode(parameters.ModelImageBytes);
                _matcher.Dispose(); _matcher = candidate; candidate = null;
                _modelImage?.Dispose(); _modelImage = image; image = null;
                _modelRoi = parameters.ModelRoi;
                _modelOptions = NodeParamContourMatch.CopyCreate(parameters.ModelOptions);
                _eraseMasks.Clear();
                _eraseMasks.AddRange(parameters.EraseMasks.Select(mask => (byte[])mask.Clone()));
                _modelOnCurrentImage = true;
                _image?.Dispose(); _image = ImageFrame.CopyFrom(_modelImage.Mat);
                imageCanvas.SetImage(_image);
                _confirmedRegions = parameters.ModelRegions?.Select(region => region.Copy()).ToList();
                imageCanvas.SetRegions(_confirmedRegions ?? new List<TemplateRegion> { new TemplateRegion {
                    Kind = TemplateRegionKind.矩形, Name = "矩形 1", CenterX = _modelRoi.X + _modelRoi.Width / 2F,
                    CenterY = _modelRoi.Y + _modelRoi.Height / 2F, Width = _modelRoi.Width, Height = _modelRoi.Height } });
                imageCanvas.SetModel(_modelRoi, _matcher.GetModelFeatures(), _matcher.GetModelContours());
                _templateDirty = false; RefreshRegionList();
                _needsModelRestore = false;
                statusLabel.Text = "已恢复模板，可修改创建参数后重新建模，或涂抹删除轮廓";
            }
            finally { image?.Dispose(); candidate?.Dispose(); SetBusy(false, statusLabel.Text); }
        }


        /// <summary>保存已确认模板，不隐式提交ROI或涂抹。</summary>
        internal void SaveParameters()
        {
            if (_busy || imageCanvas.EditorMode != CanvasEditorMode.查看 || imageCanvas.HasRegionGesture) throw new InvalidOperationException("请先完成区域绘制，或右键确认当前轮廓擦除。");
            if (_templateDirty) throw new InvalidOperationException("区域或创建参数已改变，请先重新创建模板。");
            if (!_matcher.HasModel || _modelImage == null || _modelOptions == null) throw new InvalidOperationException("请先创建并确认模板。");
            var create = ReadCreateModelOptions(); NativeShapeMatcher.ValidateCreate(create);
            Params = new NodeParamContourMatch { ModelImageBytes = _modelImage.Encode(), ModelRoi = _modelRoi,
                ModelRegions = _confirmedRegions?.Select(region => region.Copy()).ToList(),
                ModelOptions = NodeParamContourMatch.CopyCreate(_modelOptions), CreateOptions = create,
                EraseMasks = _eraseMasks.Select(mask => (byte[])mask.Clone()).ToList(), BrushSize = (int)brushSizeNumeric.Value };
        }
        /// <summary>确认模板后交给父窗口管理，并即时应用到节点参数。</summary>
        private async void SaveButton_Click(object sender, EventArgs e)
        {
            if (_busy || imageCanvas.HasRegionGesture) return;
            try
            {
                if (_templateDirty) await ConfirmCreateAsync();
                if (_templateDirty) return;
                SaveParameters(); DialogResult = DialogResult.OK; Close();
            }
            catch (Exception exception) { ShowError(exception, "保存模板失败"); }
        }
        /// <summary>关闭独立窗口，不提交未确认的修改。</summary>
        private void CloseButton_Click(object sender, EventArgs e) { Close(); }
    }
}
