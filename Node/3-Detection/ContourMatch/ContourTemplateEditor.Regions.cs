using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>区域列表、几何参数与画布之间的同步，模型仅在显式创建或确定时更新。</summary>
    public partial class ContourTemplateEditor
    {
        /// <summary>阻止程序恢复控件值时误生成区域编辑记录。</summary>
        private bool _refreshingRegionUi;
        /// <summary>当前区域或创建参数尚未构建为模型。</summary>
        private bool _templateDirty = true;
        /// <summary>当前已确认模型的区域快照；旧模板保留null兼容语义。</summary>
        private List<TemplateRegion> _confirmedRegions;

        /// <summary>初始化控件事件，控件实例与布局均由Designer定义。</summary>
        private void InitializeRegionEditing()
        {
            imageCanvas.RegionEditingEnabled = true;
            imageCanvas.RegionsChanged += ImageCanvas_RegionsChanged;
            imageCanvas.RegionSelectionChanged += ImageCanvas_RegionSelectionChanged;
            foreach (NumericUpDown numeric in new[] { modelLevelsNumeric, modelAngleStartNumeric, modelAngleEndNumeric,
                angleStepNumeric, contrastNumeric, minimumContrastNumeric, featureCountNumeric })
                numeric.ValueChanged += ModelParameter_Changed;
            autoContrastCheckBox.CheckedChanged += ModelParameter_Changed;
            metricComboBox.SelectedIndexChanged += ModelParameter_Changed;
        }

        /// <summary>选择顶部绘制工具，每次创建区域后自动回到选择。</summary>
        private void RegionTool_Click(object sender, EventArgs e)
        {
            if (_busy || _image == null) return;
            var button = (ToolStripButton)sender;
            imageCanvas.SetRegionTool((TemplateRegionKind)button.Tag);
            statusLabel.Text = imageCanvas.RegionTool == TemplateRegionKind.多边形
                ? "左键逐点绘制，双击、右键或回车闭合；Esc取消"
                : imageCanvas.RegionTool == TemplateRegionKind.选择
                ? "点击选择区域；拖动移动，方形柄缩放，圆形柄旋转"
                : "左键拖动，松开生成区域；可再次点击工具追加区域";
            UpdateActionState();
        }

        /// <summary>删除选区。</summary>
        private void DeleteRegionButton_Click(object sender, EventArgs e) { imageCanvas.DeleteSelectedRegion(); }
        /// <summary>撤销区域操作。</summary>
        private void UndoRegionButton_Click(object sender, EventArgs e) { imageCanvas.UndoRegion(false); }
        /// <summary>重做区域操作。</summary>
        private void RedoRegionButton_Click(object sender, EventArgs e) { imageCanvas.UndoRegion(true); }
        /// <summary>清空当前所有区域。</summary>
        private void ClearRegionsButton_Click(object sender, EventArgs e) { imageCanvas.ClearRegions(); }
        /// <summary>工具栏擦除真实轮廓入口。</summary>
        private void EraseRegionButton_Click(object sender, EventArgs e) { EraseButton_Click(sender, e); }

        /// <summary>完成一次区域操作后刷新列表并标记模板需要重建。</summary>
        private void ImageCanvas_RegionsChanged(object sender, EventArgs e)
        {
            _templateDirty = true; RefreshRegionList();
            statusLabel.Text = $"共 {imageCanvas.RegionCount} 个区域；点击“创建模板”预览组合轮廓，或点击“确定”完成";
            UpdateActionState();
        }

        /// <summary>刷新列表时保留画布选择，不回写选择事件。</summary>
        private void RefreshRegionList()
        {
            _refreshingRegionUi = true;
            try
            {
                regionList.BeginUpdate(); regionList.Items.Clear();
                foreach (var region in imageCanvas.CopyRegions()) regionList.Items.Add((region.Enabled ? "" : "[停用] ") + region.Name);
                regionList.SelectedIndex = imageCanvas.SelectedRegionIndex;
            }
            finally { regionList.EndUpdate(); _refreshingRegionUi = false; }
            RefreshRegionProperties();
        }

        /// <summary>画布选择同步到列表和数值参数。</summary>
        private void ImageCanvas_RegionSelectionChanged(object sender, EventArgs e)
        {
            if (_refreshingRegionUi) return;
            _refreshingRegionUi = true;
            try { if (imageCanvas.SelectedRegionIndex < regionList.Items.Count) regionList.SelectedIndex = imageCanvas.SelectedRegionIndex; }
            finally { _refreshingRegionUi = false; }
            RefreshRegionProperties(); UpdateActionState();
        }

        /// <summary>列表选择同步到画布。</summary>
        private void RegionList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_refreshingRegionUi) imageCanvas.SelectRegion(regionList.SelectedIndex);
        }

        /// <summary>显示选区的中心、尺寸、角度及扇形角度。</summary>
        private void RefreshRegionProperties()
        {
            TemplateRegion region = imageCanvas.SelectedRegion;
            _refreshingRegionUi = true;
            try
            {
                regionProperties.Enabled = region != null && !_busy;
                if (region == null) return;
                regionEnabledCheckBox.Checked = region.Enabled;
                SetRegionValue(regionXNumeric, region.CenterX); SetRegionValue(regionYNumeric, region.CenterY);
                SetRegionValue(regionWidthNumeric, region.Width); SetRegionValue(regionHeightNumeric, region.Height);
                SetRegionValue(regionAngleNumeric, region.Angle);
                SetRegionValue(sectorStartNumeric, region.StartAngle); SetRegionValue(sectorSweepNumeric, region.SweepAngle);
                sectorStartNumeric.Enabled = sectorSweepNumeric.Enabled = region.Kind == TemplateRegionKind.扇形;
            }
            finally { _refreshingRegionUi = false; }
        }

        /// <summary>限制显示数值，不改变区域的原始精度。</summary>
        private static void SetRegionValue(NumericUpDown input, float value)
        {
            input.Value = Math.Max(input.Minimum, Math.Min(input.Maximum, (decimal)value));
        }

        /// <summary>仅写回用户改动的属性，避免拖动产生的小数被其它输入框舍入。</summary>
        private void RegionProperty_ValueChanged(object sender, EventArgs e)
        {
            if (_refreshingRegionUi || _busy) return;
            var region = imageCanvas.SelectedRegion; if (region == null) return;
            if (sender == regionXNumeric) region.CenterX = (float)regionXNumeric.Value;
            else if (sender == regionYNumeric) region.CenterY = (float)regionYNumeric.Value;
            else if (sender == regionWidthNumeric) region.Width = (float)regionWidthNumeric.Value;
            else if (sender == regionHeightNumeric) region.Height = (float)regionHeightNumeric.Value;
            else if (sender == regionAngleNumeric) region.Angle = (float)regionAngleNumeric.Value;
            else if (sender == sectorStartNumeric) region.StartAngle = (float)sectorStartNumeric.Value;
            else if (sender == sectorSweepNumeric) region.SweepAngle = (float)sectorSweepNumeric.Value;
            else if (sender == regionEnabledCheckBox) region.Enabled = regionEnabledCheckBox.Checked;
            imageCanvas.UpdateSelectedRegion(region);
        }

        /// <summary>创建参数改动后要求重建，防止保存时使用旧参数的模型。</summary>
        private void ModelParameter_Changed(object sender, EventArgs e)
        {
            if (_refreshingRegionUi || _busy) return;
            _templateDirty = true; imageCanvas.ClearModelPreview();
            statusLabel.Text = "创建参数已修改，点击“创建模板”或“确定”重新建模";
            UpdateActionState();
        }

        /// <summary>区域工具状态与当前画布同步，不依赖按钮的自动切换。</summary>
        private void UpdateRegionActions(bool busy)
        {
            bool canEdit = !busy && _image != null && imageCanvas.EditorMode == CanvasEditorMode.查看;
            regionTools.Enabled = !busy;
            foreach (ToolStripButton button in new[] { selectRegionButton, rectangleRegionButton, ellipseRegionButton,
                sectorRegionButton, polygonRegionButton, brushRegionButton })
            {
                button.Enabled = canEdit;
                button.Checked = imageCanvas.RegionTool == (TemplateRegionKind)button.Tag && imageCanvas.EditorMode == CanvasEditorMode.查看;
            }
            deleteRegionButton.Enabled = canEdit && imageCanvas.SelectedRegionIndex >= 0;
            clearRegionsButton.Enabled = canEdit && imageCanvas.RegionCount > 0;
            undoRegionButton.Enabled = canEdit && imageCanvas.CanUndoRegion;
            redoRegionButton.Enabled = canEdit && imageCanvas.CanRedoRegion;
            eraseRegionButton.Enabled = canEdit && !_templateDirty && _matcher.HasModel;
            eraseRegionButton.Checked = imageCanvas.EditorMode == CanvasEditorMode.涂抹;
            regionList.Enabled = canEdit; regionProperties.Enabled = canEdit && imageCanvas.SelectedRegionIndex >= 0;
        }
    }
}
