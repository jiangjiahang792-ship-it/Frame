using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>基础参数、特征模板和运行参数三页式节点配置窗口。</summary>
    public partial class NodeParamFormContourMatch : FormBase, INodeParamForm
    {
        /// <summary>界面编辑数据，变更后立即发布独立生产快照。</summary>
        private NodeParamContourMatch _draft = new NodeParamContourMatch { Templates = new List<ContourTemplateDefinition>() };
        /// <summary>界面独占的当前搜索图像。</summary>
        private ImageFrame _image;
        /// <summary>右侧模板裁剪图，独立于搜索图像。</summary>
        private ImageFrame _templateImage;
        /// <summary>已显示模型的引用，避免启停和重命名时重复建立轮廓。</summary>
        private NodeParamContourMatch _previewedModel;
        /// <summary>独立的预览算法会话。</summary>
        private readonly ContourMatchSession _previewSession = new ContourMatchSession();
        /// <summary>禁止原生预览期间修改或释放资源。</summary>
        private bool _busy;
        /// <summary>防止列表填充触发草稿写回。</summary>
        private bool _populating;
        /// <summary>初始化设计器布局。</summary>
        public NodeParamFormContourMatch() { InitializeComponent(); Shown += Form_Shown; }

        /// <summary>打开时恢复当前参数，并尝试读取订阅图像。</summary>
        private void Form_Shown(object sender, EventArgs e)
        {
            try { SetParam2Form(); if (!string.IsNullOrWhiteSpace(imageSubscription.GetText1())) RefreshSubscribedImage(); }
            catch (Exception exception) { ShowFailure(exception); }
        }
        /// <summary>只在界面上显示操作失败原因，保留当前参数和模板。</summary>
        private void ShowFailure(Exception exception) { statusLabel.Text = exception.Message; }
        /// <summary>加载独立本地图像用于区域绘制和模板创建。</summary>
        private void LoadImageButton_Click(object sender, EventArgs e)
        {
            if (imageOpenDialog.ShowDialog(this) != DialogResult.OK) return;
            try { SetImage(ImageFrame.Load(imageOpenDialog.FileName)); statusLabel.Text = "已加载本地图像"; }
            catch (Exception exception) { ShowFailure(exception); }
        }
        /// <summary>替换界面图像后释放旧快照。</summary>
        private void SetImage(ImageFrame image)
        {
            var previous = _image; _image = image; imageCanvas.SetImage(image); previous?.Dispose();
            resultsGrid.Rows.Clear(); ShowSearchRegion();
        }
        /// <summary>刷新所选图像订阅。</summary>
        private void RefreshImageButton_Click(object sender, EventArgs e)
        {
            try { RefreshSubscribedImage(); } catch (Exception exception) { ShowFailure(exception); }
        }
        /// <summary>在上游租约内建立编辑快照。</summary>
        private void RefreshSubscribedImage()
        {
            var owner = imageSubscription.GetValue<TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage>();
            if (owner == null) throw new InvalidOperationException("订阅图像为空，请先运行上游节点。");
            using (owner.AcquireLease()) SetImage(ImageFrame.CopyFrom(NodeContourMatch.GetSourceMat(owner)));
            statusLabel.Text = "已刷新输入图像";
        }
        /// <summary>在基础参数页框选矩形搜索范围。</summary>
        private void DrawRegionButton_Click(object sender, EventArgs e)
        {
            if (_image == null) { statusLabel.Text = "请先刷新订阅图像或加载本地图像"; return; }
            imageCanvas.BeginCreate(); parameterTabs.Enabled = false; executeButton.Enabled = false;
            statusLabel.Text = "搜索区域：左键拖动矩形，右键确认，Esc取消";
        }
        /// <summary>右键仅确认搜索区域，不创建模型。</summary>
        private void SearchRegion_ConfirmRequested(object sender, EventArgs e)
        {
            Rectangle region = imageCanvas.Roi;
            if (region.Width < 8 || region.Height < 8) { statusLabel.Text = "搜索区域至少需要8×8像素"; return; }
            _draft.SearchRegion = region; allSearchCheckBox.Checked = false;
            imageCanvas.EndEdit(); FinishRegionEdit(); ApplyParameters(); statusLabel.Text = "搜索区域已确认";
        }
        /// <summary>取消临时搜索框，保留已确认范围。</summary>
        private void SearchRegion_EditCancelled(object sender, EventArgs e) { FinishRegionEdit(); statusLabel.Text = "已取消本次区域绘制"; }
        /// <summary>恢复编辑按钮和已确认的区域显示。</summary>
        private void FinishRegionEdit() { parameterTabs.Enabled = true; executeButton.Enabled = true; ShowSearchRegion(); }
        /// <summary>切换全图和指定区域搜索。</summary>
        private void SearchMode_Changed(object sender, EventArgs e)
        {
            if (_populating) return;
            _draft.AllSearch = allSearchCheckBox.Checked;
            ShowSearchRegion(); ApplyParameters();
        }
        /// <summary>显示已确认区域，不将它混入模板的建模ROI。</summary>
        private void ShowSearchRegion()
        {
            var region = _draft.SearchRegion;
            regionLabel.Text = allSearchCheckBox.Checked ? "当前搜索整幅图像" : region.IsEmpty ? "尚未绘制搜索区域" : $"X={region.X}，Y={region.Y}，宽={region.Width}，高={region.Height}";
            if (_image != null) imageCanvas.SetModel(allSearchCheckBox.Checked ? Rectangle.Empty : region, Array.Empty<PointF>());
        }
        /// <summary>读取当前选中的模板，不隐式选择其他条目。</summary>
        private ContourTemplateDefinition SelectedTemplate { get { return templatesGrid.CurrentRow?.Tag as ContourTemplateDefinition; } }
        /// <summary>重建模板管理列表，稳定保留指定模板选择。</summary>
        private void RefreshTemplateList(string selectedId = null)
        {
            _populating = true;
            try
            {
                templatesGrid.Rows.Clear();
                foreach (var entry in _draft.Templates)
                {
                    int index = templatesGrid.Rows.Add(entry.Enabled, entry.Name, $"{entry.Model.ModelRoi.Width} × {entry.Model.ModelRoi.Height}");
                    templatesGrid.Rows[index].Tag = entry;
                    if (entry.Id == selectedId) templatesGrid.CurrentCell = templatesGrid.Rows[index].Cells[1];
                }
            }
            finally { _populating = false; }
            UpdateTemplatePreview(); ApplyParameters();
        }
        /// <summary>立即提交启用复选框，避免运行时仍使用上一次勾选状态。</summary>
        private void TemplatesGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (templatesGrid.IsCurrentCellDirty) templatesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
        /// <summary>同步启用和名称并立即更新节点参数。</summary>
        private void TemplatesGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_populating || e.RowIndex < 0) return;
            var row = templatesGrid.Rows[e.RowIndex]; var entry = row.Tag as ContourTemplateDefinition; if (entry == null) return;
            entry.Enabled = Convert.ToBoolean(row.Cells[0].Value);
            string name = Convert.ToString(row.Cells[1].Value)?.Trim();
            if (!string.IsNullOrWhiteSpace(name)) entry.Name = name;
            else { _populating = true; row.Cells[1].Value = entry.Name; _populating = false; }
            UpdateTemplatePreview(); ApplyParameters();
        }
        /// <summary>选择模板时更新独立缩略图。</summary>
        private void TemplatesGrid_SelectionChanged(object sender, EventArgs e) { if (!_populating) UpdateTemplatePreview(); }
        /// <summary>特征页独占右侧图像区，隐藏下方搜索结果，切回时保留原搜索图。</summary>
        private void ParameterTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool templates = parameterTabs.SelectedTab == templatesPage;
            previewLayout.SuspendLayout();
            imageCanvas.Visible = !templates; templateCanvas.Visible = templates; resultsGrid.Visible = !templates;
            previewLayout.RowStyles[1].Height = templates ? 0 : 155;
            previewLayout.ResumeLayout(true);
            if (templates) UpdateTemplatePreview();
        }
        /// <summary>在右侧显示模板裁剪图及已确认的真实轮廓，复用当前模型预览。</summary>
        private void UpdateTemplatePreview()
        {
            var entry = SelectedTemplate;
            editTemplateButton.Enabled = deleteTemplateButton.Enabled = exportTemplatesButton.Enabled = entry != null;
            if (entry == null)
            {
                templateCanvas.SetImage(null); _templateImage?.Dispose(); _templateImage = null; _previewedModel = null;
                templateSummary.Text = "创建或导入模板，选中后在右侧查看图像与轮廓。"; return;
            }
            templateSummary.Text = $"{entry.Name} · {(entry.Enabled ? "启用" : "停用")} · 涂抹{entry.Model.EraseMasks.Count}次\n双击名称可重命名";
            if (parameterTabs.SelectedTab != templatesPage || ReferenceEquals(_previewedModel, entry.Model)) return;
            try
            {
                using (var frame = ImageFrame.Decode(entry.Model.ModelImageBytes))
                using (var matcher = ContourMatchSession.Restore(entry.Model))
                using (var cropped = new OpenCvSharp.Mat(frame.Mat, new OpenCvSharp.Rect(entry.Model.ModelRoi.X, entry.Model.ModelRoi.Y, entry.Model.ModelRoi.Width, entry.Model.ModelRoi.Height)))
                {
                    var features = matcher.GetModelFeatures(); var contours = matcher.GetModelContours();
                    var replacement = ImageFrame.CopyFrom(cropped); var previous = _templateImage;
                    _templateImage = replacement; templateCanvas.SetImage(replacement);
                    templateCanvas.SetModel(new Rectangle(0, 0, replacement.Width, replacement.Height), features, contours);
                    previous?.Dispose(); _previewedModel = entry.Model;
                }
            }
            catch (Exception exception)
            {
                templateCanvas.SetImage(null); _templateImage?.Dispose(); _templateImage = null; _previewedModel = null;
                templateSummary.Text = "模板预览失败：" + exception.Message;
            }
        }
        /// <summary>打开独立创建窗口，提交后加入模板列表。</summary>
        private void CreateTemplateButton_Click(object sender, EventArgs e) { EditTemplate(null); }
        /// <summary>编辑选中模板，取消不改变原模板。</summary>
        private void EditTemplateButton_Click(object sender, EventArgs e) { if (SelectedTemplate != null) EditTemplate(SelectedTemplate); }
        /// <summary>通过独立窗口提交模板，搜索参数始终留在节点运行页。</summary>
        private void EditTemplate(ContourTemplateDefinition original)
        {
            try
            {
                using (var editor = new ContourTemplateEditor())
                {
                    editor.Configure(original?.Model, original == null ? _image?.Mat : null);
                    if (editor.ShowDialog(this) != DialogResult.OK) return;
                    string name = original?.Name ?? $"模板{_draft.Templates.Count + 1}";
                    var replacement = ContourTemplateDefinition.FromModel((NodeParamContourMatch)editor.Params, name, original?.Id);
                    replacement.Enabled = original?.Enabled ?? true;
                    if (original == null) _draft.Templates.Add(replacement); else _draft.Templates[_draft.Templates.IndexOf(original)] = replacement;
                    RefreshTemplateList(replacement.Id); statusLabel.Text = "模板已更新并生效";
                }
            }
            catch (Exception exception) { ShowFailure(exception); }
        }
        /// <summary>删除选中模板并立即更新节点参数。</summary>
        private void DeleteTemplateButton_Click(object sender, EventArgs e) { if (SelectedTemplate != null) _draft.Templates.Remove(SelectedTemplate); RefreshTemplateList(); }
        /// <summary>清空模板列表，旧模型不会从兼容字段自动复活。</summary>
        private void ClearTemplatesButton_Click(object sender, EventArgs e) { _draft.Templates.Clear(); RefreshTemplateList(); }
        /// <summary>原子导入经过算法验证的模板集合，不覆盖原有模板。</summary>
        private async void ImportTemplatesButton_Click(object sender, EventArgs e)
        {
            if (templateOpenDialog.ShowDialog(this) != DialogResult.OK) return;
            SetBusy(true);
            try
            {
                string json = File.ReadAllText(templateOpenDialog.FileName);
                var package = await Task.Run(() => ContourTemplatePackage.Decode(json));
                _draft.Templates.AddRange(package.Templates); RefreshTemplateList(package.Templates[0].Id);
                statusLabel.Text = $"已导入{package.Templates.Count}个模板";
            }
            catch (Exception exception) { ShowFailure(exception); }
            finally { SetBusy(false); }
        }
        /// <summary>导出当前选中模板，包含源图及所有涂抹记录。</summary>
        private void ExportTemplatesButton_Click(object sender, EventArgs e)
        {
            var entry = SelectedTemplate; if (entry == null || templateSaveDialog.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllText(templateSaveDialog.FileName, new ContourTemplatePackage { Templates = new List<ContourTemplateDefinition> { entry.Copy() } }.Encode()); statusLabel.Text = "模板已导出"; }
            catch (Exception exception) { ShowFailure(exception); }
        }
        /// <summary>使用当前参数进行搜索预览，无需保存工程方案。</summary>
        private async void ExecuteButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(imageSubscription.GetText1())) RefreshSubscribedImage();
                if (_image == null) throw new InvalidOperationException("请先选择输入图像。");
                var snapshot = ReadDraft();
                parameterTabs.SelectedTab = runtimePage;
                SetBusy(true);
                var result = await Task.Run(() => _previewSession.Execute(_image.Mat, snapshot, CancellationToken.None));
                imageCanvas.SetMatches(result.Matches); resultsGrid.Rows.Clear();
                foreach (var match in result.Matches) resultsGrid.Rows.Add(match.TemplateName, match.CenterX.ToString("F3"), match.CenterY.ToString("F3"), match.AngleDegrees.ToString("F3"), match.Score.ToString("F4"));
                statusLabel.Text = $"匹配完成：{result.Matches.Count}个目标，耗时{result.Milliseconds:F2}毫秒";
            }
            catch (Exception exception) { ShowFailure(exception); }
            finally { SetBusy(false); }
        }
        /// <summary>确定仅关闭窗口，参数已在修改时生效。</summary>
        private void CloseButton_Click(object sender, EventArgs e) { Close(); }
        /// <summary>锁定编辑及关闭，保护预览调用中的图像。</summary>
        private void SetBusy(bool busy)
        {
            if (busy) _busy = true;
            contentSplit.Enabled = !busy; footerActions.Enabled = !busy; UseWaitCursor = busy;
            // 忙碌标记结束意味着按钮也已恢复，不能让调用方先观察到完成状态。
            if (!busy) _busy = false;
        }
        /// <summary>等待正在执行的预览完成后才允许关闭。</summary>
        protected override void OnFormClosing(FormClosingEventArgs e) { if (_busy) { e.Cancel = true; statusLabel.Text = "当前操作尚未完成"; } base.OnFormClosing(e); }
    }
}
