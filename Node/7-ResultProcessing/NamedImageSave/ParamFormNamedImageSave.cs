using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._7_ResultProcessing.NamedImageSave
{
    /// <summary>编辑逐级目录和订阅来源，独立草稿避免未保存的修改影响运行。</summary>
    public partial class ParamFormNamedImageSave : FormBase, INodeParamForm
    {
        /// <summary>已保存的运行参数。</summary>
        public INodeParam Params { get; set; }
        /// <summary>当前所属节点。</summary>
        private NodeBase _node;
        /// <summary>界面正在编辑的独立配置。</summary>
        private NodeParamNamedImageSave _draft = new NodeParamNamedImageSave();
        /// <summary>同步控件期间禁止反向写入草稿。</summary>
        private bool _loading;

        /// <summary>初始化设计器控件、盘符和初始一行。</summary>
        public ParamFormNamedImageSave()
        {
            InitializeComponent();
            foreach (string drive in Directory.GetLogicalDrives()) comboDrive.Items.Add(drive);
            if (!comboDrive.Items.Contains(_draft.DriveRoot)) comboDrive.Items.Add(_draft.DriveRoot);
            LoadDraft();
        }

        /// <summary>绑定所属流程，用其上游节点构建可订阅结果树。</summary>
        public void SetNodeBelong(NodeBase node) { _node = node; RefreshSources(); }

        /// <summary>恢复方案参数并复制为编辑草稿。</summary>
        public void SetParam2Form()
        {
            _draft = (Params as NodeParamNamedImageSave)?.Copy() ?? new NodeParamNamedImageSave();
            if (_draft.Folders == null) _draft.Folders = new System.Collections.Generic.List<NamedImageValue>();
            if (_draft.FileName == null) _draft.FileName = new NamedImageValue();
            EnsureFileNameParts();
            LoadDraft();
            RefreshSources();
        }

        /// <summary>打开时恢复已保存参数并刷新来源，兼容断线后重新连线及节点改名。</summary>
        private void Form_Shown(object sender, EventArgs e) { SetParam2Form(); }

        /// <summary>显示草稿中的盘符、图像、文件名和目录行。</summary>
        private void LoadDraft()
        {
            _loading = true;
            if (!string.IsNullOrEmpty(_draft.DriveRoot) && !comboDrive.Items.Contains(_draft.DriveRoot)) comboDrive.Items.Add(_draft.DriveRoot);
            comboDrive.SelectedItem = _draft.DriveRoot;
            numberQuality.Value = Math.Max(1, Math.Min(100, _draft.JpegQuality));
            _loading = false;
            RefreshRows(0);
            RefreshFileParts(0);
            UpdateLabels();
        }

        /// <summary>兼容旧方案单文件名字段，并保证界面始终至少有一个文件名片段。</summary>
        private void EnsureFileNameParts()
        {
            if (_draft.FileNameParts == null) _draft.FileNameParts = new System.Collections.Generic.List<NamedImageValue>();
            if (_draft.FileNameParts.Count == 1 && IsEmptyManual(_draft.FileNameParts[0]) && !IsEmptyManual(_draft.FileName))
                _draft.FileNameParts[0] = _draft.FileName.Copy();
            if (_draft.FileNameParts.Count == 0) _draft.FileNameParts.Add((_draft.FileName ?? new NamedImageValue()).Copy());
            if (_draft.FileName == null) _draft.FileName = _draft.FileNameParts.FirstOrDefault()?.Copy() ?? new NamedImageValue();
        }

        /// <summary>识别旧参数默认生成的空白手填片段。</summary>
        private static bool IsEmptyManual(NamedImageValue value)
        {
            return value == null || value.EffectiveMode == NamedImageValueMode.Manual && string.IsNullOrWhiteSpace(value.Text);
        }

        /// <summary>重建单列列表并保留指定行，行内值由草稿直接管理。</summary>
        private void RefreshRows(int selected)
        {
            _loading = true;
            listFolders.BeginUpdate();
            listFolders.Items.Clear();
            foreach (var row in _draft.Folders) listFolders.Items.Add(row ?? new NamedImageValue());
            if (listFolders.Items.Count > 0) listFolders.SelectedIndex = Math.Max(0, Math.Min(selected, listFolders.Items.Count - 1));
            listFolders.EndUpdate();
            _loading = false;
            LoadCurrentRow();
        }

        /// <summary>当前选中目录行。</summary>
        private NamedImageValue CurrentRow => listFolders.SelectedIndex < 0 ? null : _draft.Folders[listFolders.SelectedIndex];
        /// <summary>当前选中文件名片段。</summary>
        private NamedImageValue CurrentFilePart => listFileNames.SelectedIndex < 0 ? null : _draft.FileNameParts[listFileNames.SelectedIndex];

        /// <summary>切换行时显示手填内容和当前订阅，编辑即时保存到该行草稿。</summary>
        private void LoadCurrentRow()
        {
            _loading = true;
            textRow.Enabled = CurrentRow != null && CurrentRow.EffectiveMode == NamedImageValueMode.Manual;
            textRow.Text = CurrentRow?.Text ?? string.Empty;
            labelRowSource.Text = GetRowStatusText(CurrentRow);
            _loading = false;
            UpdatePreview();
        }

        /// <summary>根据行模式生成简体中文状态说明。</summary>
        private static string GetRowStatusText(NamedImageValue row)
        {
            if (row == null) return "当前行：未选择";
            switch (row.EffectiveMode)
            {
                case NamedImageValueMode.CurrentDate:
                    return "当前行：当前日期（yyyy-MM-dd）";
                case NamedImageValueMode.BoolOkNg:
                    return "当前行OK/NG订阅：" + (row.Source?.DisplayName ?? "未选择布尔结果");
                case NamedImageValueMode.Subscription:
                    return "当前行订阅：" + row.Source.DisplayName;
                default:
                    return "当前行：手动输入";
            }
        }

        /// <summary>用户选择另一目录行。</summary>
        private void Row_SelectedIndexChanged(object sender, EventArgs e) { if (!_loading) LoadCurrentRow(); }

        /// <summary>手填当前行时切换回文字模式，保留其他行订阅。</summary>
        private void Row_TextChanged(object sender, EventArgs e)
        {
            if (_loading || CurrentRow == null) return;
            int index = listFolders.SelectedIndex;
            CurrentRow.Text = textRow.Text;
            CurrentRow.Mode = NamedImageValueMode.Manual;
            CurrentRow.Source = null;
            _loading = true;
            listFolders.Items[index] = CurrentRow;
            _loading = false;
            labelRowSource.Text = "当前行：手动输入";
            UpdatePreview();
        }

        /// <summary>在末尾增加一级目录。</summary>
        private void Add_Click(object sender, EventArgs e) { _draft.Folders.Add(new NamedImageValue()); RefreshRows(_draft.Folders.Count - 1); textRow.Focus(); }
        /// <summary>在当前行之前插入目录。</summary>
        private void Insert_Click(object sender, EventArgs e)
        {
            int index = Math.Max(0, listFolders.SelectedIndex);
            _draft.Folders.Insert(index, new NamedImageValue()); RefreshRows(index); textRow.Focus();
        }
        /// <summary>删除当前行，允许全部删除后重新添加。</summary>
        private void Delete_Click(object sender, EventArgs e)
        {
            int index = listFolders.SelectedIndex;
            if (index < 0) return;
            _draft.Folders.RemoveAt(index); RefreshRows(index);
        }
        /// <summary>将当前行上移一级。</summary>
        private void Up_Click(object sender, EventArgs e) { MoveRow(-1); }
        /// <summary>将当前行下移一级。</summary>
        private void Down_Click(object sender, EventArgs e) { MoveRow(1); }
        /// <summary>只交换当前行的位置，内容和订阅整体移动。</summary>
        private void MoveRow(int offset)
        {
            int index = listFolders.SelectedIndex, target = index + offset;
            if (index < 0 || target < 0 || target >= _draft.Folders.Count) return;
            var row = _draft.Folders[index]; _draft.Folders.RemoveAt(index); _draft.Folders.Insert(target, row); RefreshRows(target);
        }

        /// <summary>重建文件名片段列表并保留指定片段。</summary>
        private void RefreshFileParts(int selected)
        {
            EnsureFileNameParts();
            _loading = true;
            listFileNames.BeginUpdate();
            listFileNames.Items.Clear();
            foreach (var part in _draft.FileNameParts) listFileNames.Items.Add(part ?? new NamedImageValue());
            if (listFileNames.Items.Count > 0) listFileNames.SelectedIndex = Math.Max(0, Math.Min(selected, listFileNames.Items.Count - 1));
            listFileNames.EndUpdate();
            _loading = false;
            LoadCurrentFilePart();
        }

        /// <summary>显示当前文件名片段内容。</summary>
        private void LoadCurrentFilePart()
        {
            _loading = true;
            textFileName.Enabled = CurrentFilePart != null && CurrentFilePart.EffectiveMode == NamedImageValueMode.Manual;
            textFileName.Text = CurrentFilePart?.Text ?? string.Empty;
            _loading = false;
            UpdateLabels();
        }

        /// <summary>用户选择另一文件名片段。</summary>
        private void FilePart_SelectedIndexChanged(object sender, EventArgs e) { if (!_loading) LoadCurrentFilePart(); }

        /// <summary>在文件名末尾增加片段。</summary>
        private void AddFile_Click(object sender, EventArgs e) { _draft.FileNameParts.Add(new NamedImageValue()); RefreshFileParts(_draft.FileNameParts.Count - 1); textFileName.Focus(); }

        /// <summary>在当前文件名片段前插入新片段。</summary>
        private void InsertFile_Click(object sender, EventArgs e)
        {
            int index = Math.Max(0, listFileNames.SelectedIndex);
            _draft.FileNameParts.Insert(index, new NamedImageValue());
            RefreshFileParts(index);
            textFileName.Focus();
        }

        /// <summary>删除当前文件名片段，至少保留一个空片段用于默认时间命名。</summary>
        private void DeleteFile_Click(object sender, EventArgs e)
        {
            int index = listFileNames.SelectedIndex;
            if (index < 0) return;
            _draft.FileNameParts.RemoveAt(index);
            if (_draft.FileNameParts.Count == 0) _draft.FileNameParts.Add(new NamedImageValue());
            RefreshFileParts(index);
        }

        /// <summary>上移当前文件名片段。</summary>
        private void UpFile_Click(object sender, EventArgs e) { MoveFilePart(-1); }

        /// <summary>下移当前文件名片段。</summary>
        private void DownFile_Click(object sender, EventArgs e) { MoveFilePart(1); }

        /// <summary>交换文件名片段顺序，订阅和文字整体移动。</summary>
        private void MoveFilePart(int offset)
        {
            int index = listFileNames.SelectedIndex, target = index + offset;
            if (index < 0 || target < 0 || target >= _draft.FileNameParts.Count) return;
            var part = _draft.FileNameParts[index]; _draft.FileNameParts.RemoveAt(index); _draft.FileNameParts.Insert(target, part);
            RefreshFileParts(target);
        }

        /// <summary>刷新PLC、共享变量、多条件和图像等可用上游端口。</summary>
        private void RefreshSources()
        {
            var savedSources = _draft.Folders.Select(row => row?.Source)
                .Concat(_draft.FileNameParts.Select(row => row?.Source))
                .Concat(new[] { _draft.ImageSource, _draft.FileName?.Source }).Where(source => source != null).ToArray();
            treeSources.BeginUpdate();
            treeSources.Nodes.Clear();
            if (_node?.Process != null)
            {
                foreach (var node in _node.Process.GetUpstreamNodes(_node).OrderBy(item => item.ID))
                {
                    var root = new TreeNode(node.ID + "." + node.NodeName);
                    foreach (var output in NamedImageSubscriptions.GetOutputs(node))
                    {
                        var source = new NamedImageSource { SourceNodeId = node.ID, PropertyPath = output.PropertyPath,
                            DisplayName = root.Text + " → " + output.DisplayName };
                        // 依据稳定编号刷新展示文本，复制或改名后不再显示旧节点名称。
                        foreach (var saved in savedSources.Where(item => item.SourceNodeId == source.SourceNodeId && item.PropertyPath == source.PropertyPath))
                            saved.DisplayName = source.DisplayName;
                        var name = output.ValueType == typeof(OutputImage) ? "图像" : output.ValueType == typeof(bool) ? "布尔" : "名称";
                        var leaf = new TreeNode(output.DisplayName) { Tag = source, Name = name };
                        root.Nodes.Add(leaf);
                    }
                    if (root.Nodes.Count > 0) treeSources.Nodes.Add(root);
                }
            }
            treeSources.ExpandAll();
            treeSources.EndUpdate();
            RefreshRows(listFolders.SelectedIndex);
            RefreshFileParts(listFileNames.SelectedIndex);
            UpdateLabels();
        }
        /// <summary>用户主动刷新来源。</summary>
        private void Refresh_Click(object sender, EventArgs e) { RefreshSources(); }

        /// <summary>检查当前树叶节点的用途并复制稳定引用。</summary>
        private NamedImageSource SelectedSource(bool image)
        {
            var selected = treeSources.SelectedNode;
            if (!(selected?.Tag is NamedImageSource source) || image && selected.Name != "图像" || !image && selected.Name == "图像")
            {
                MessageBoxTD.Show(image ? "请在右侧选择图像结果。" : "请在右侧选择文本、数值或布尔结果。");
                return null;
            }
            return source.Copy();
        }
        /// <summary>把选中结果绑定到当前目录行。</summary>
        private void SubscribeRow_Click(object sender, EventArgs e)
        {
            if (CurrentRow == null) { MessageBoxTD.Show("请先选择或增加文件夹行。"); return; }
            var source = SelectedSource(false);
            if (source == null) return;
            CurrentRow.Mode = NamedImageValueMode.Subscription; CurrentRow.Source = source; RefreshRows(listFolders.SelectedIndex);
        }
        /// <summary>将当前目录行改回手动输入。</summary>
        private void ManualRow_Click(object sender, EventArgs e)
        {
            if (CurrentRow == null) return;
            CurrentRow.Mode = NamedImageValueMode.Manual; CurrentRow.Source = null; RefreshRows(listFolders.SelectedIndex); textRow.Focus();
        }
        /// <summary>将当前目录行设为运行当天日期。</summary>
        private void CurrentDateRow_Click(object sender, EventArgs e)
        {
            if (CurrentRow == null) { MessageBoxTD.Show("请先选择或增加文件夹行。"); return; }
            CurrentRow.Text = string.Empty;
            CurrentRow.Mode = NamedImageValueMode.CurrentDate;
            CurrentRow.Source = null;
            RefreshRows(listFolders.SelectedIndex);
        }
        /// <summary>将当前目录行绑定布尔结果，并在运行时转换为OK或NG。</summary>
        private void OkNgRow_Click(object sender, EventArgs e)
        {
            if (CurrentRow == null) { MessageBoxTD.Show("请先选择或增加文件夹行。"); return; }
            var selected = treeSources.SelectedNode;
            if (!(selected?.Tag is NamedImageSource source) || selected.Name != "布尔")
            {
                MessageBoxTD.Show("请在右侧选择布尔结果，例如多条件判定结果。");
                return;
            }
            CurrentRow.Text = string.Empty;
            CurrentRow.Mode = NamedImageValueMode.BoolOkNg;
            CurrentRow.Source = source.Copy();
            RefreshRows(listFolders.SelectedIndex);
        }
        /// <summary>选择输出图像。</summary>
        private void SubscribeImage_Click(object sender, EventArgs e)
        {
            var source = SelectedSource(true); if (source == null) return;
            _draft.ImageSource = source; UpdateLabels();
        }
        /// <summary>选择文件名来源。</summary>
        private void SubscribeFile_Click(object sender, EventArgs e)
        {
            if (CurrentFilePart == null) { MessageBoxTD.Show("请先选择或增加文件名片段。"); return; }
            var source = SelectedSource(false); if (source == null) return;
            CurrentFilePart.Mode = NamedImageValueMode.Subscription;
            CurrentFilePart.Source = source;
            RefreshFileParts(listFileNames.SelectedIndex);
        }
        /// <summary>文件名改回手填，空白内容按当前日期时间命名。</summary>
        private void ManualFile_Click(object sender, EventArgs e)
        {
            if (CurrentFilePart == null) return;
            CurrentFilePart.Mode = NamedImageValueMode.Manual;
            CurrentFilePart.Source = null;
            RefreshFileParts(listFileNames.SelectedIndex);
            textFileName.Focus();
        }
        /// <summary>当前文件名片段设为运行当天日期。</summary>
        private void CurrentDateFile_Click(object sender, EventArgs e)
        {
            if (CurrentFilePart == null) { MessageBoxTD.Show("请先选择或增加文件名片段。"); return; }
            CurrentFilePart.Text = string.Empty;
            CurrentFilePart.Mode = NamedImageValueMode.CurrentDate;
            CurrentFilePart.Source = null;
            RefreshFileParts(listFileNames.SelectedIndex);
        }
        /// <summary>手填文件名即时写入草稿并替代此前订阅。</summary>
        private void File_TextChanged(object sender, EventArgs e)
        {
            if (_loading || CurrentFilePart == null) return;
            int index = listFileNames.SelectedIndex;
            CurrentFilePart.Text = textFileName.Text;
            CurrentFilePart.Mode = NamedImageValueMode.Manual;
            CurrentFilePart.Source = null;
            _loading = true;
            listFileNames.Items[index] = CurrentFilePart;
            _loading = false;
            UpdateLabels();
        }
        /// <summary>盘符和压缩质量变更即时更新草稿。</summary>
        private void Options_Changed(object sender, EventArgs e)
        {
            if (_loading) return;
            _draft.DriveRoot = comboDrive.Text; _draft.JpegQuality = (int)numberQuality.Value; UpdatePreview();
        }
        /// <summary>显示图像和文件名订阅状态。</summary>
        private void UpdateLabels()
        {
            labelImage.Text = "保存图像：" + (_draft.ImageSource?.DisplayName ?? "请在右侧选择图像结果，再点击“设为图像”");
            labelFileSource.Text = "当前文件名段：" + GetRowStatusText(CurrentFilePart).Replace("当前行", "文件名段");
            UpdatePreview();
        }
        /// <summary>用占位描述预览规则，不要求设备已连接或流程已运行。</summary>
        private void UpdatePreview()
        {
            string[] parts = _draft.Folders.Select(GetPreviewPart).ToArray();
            string name = string.Concat(_draft.FileNameParts.Select(GetPreviewPart));
            if (string.IsNullOrWhiteSpace(name)) name = "当前日期时间";
            textPreview.Text = _draft.DriveRoot + string.Join("\\", parts) + "\\" + name + ".jpg";
        }
        /// <summary>按当前行模式显示不依赖运行结果的规则预览。</summary>
        private static string GetPreviewPart(NamedImageValue row)
        {
            if (row == null) return string.Empty;
            switch (row.EffectiveMode)
            {
                case NamedImageValueMode.CurrentDate:
                    return "{当前日期}";
                case NamedImageValueMode.BoolOkNg:
                    return "{OK/NG：" + (row.Source?.DisplayName ?? "未选择") + "}";
                case NamedImageValueMode.Subscription:
                    return "{" + row.Source.DisplayName + "}";
                default:
                    return row.Text ?? string.Empty;
            }
        }
        /// <summary>保存独立参数副本，所有运行读值都基于参数而非当前UI选择。</summary>
        private void Save_Click(object sender, EventArgs e)
        {
            try
            {
                NamedImagePathBuilder.Validate(_draft);
                if (_draft.Folders.Any(row => row == null || row.EffectiveMode == NamedImageValueMode.Manual && string.IsNullOrWhiteSpace(row.Text)))
                    throw new InvalidOperationException("文件夹行不能为空，请填写内容、订阅结果或删除空行。");
                EnsureFileNameParts();
                _draft.FileName = _draft.FileNameParts.FirstOrDefault()?.Copy() ?? new NamedImageValue();
                Params = _draft.Copy(); Hide();
            }
            catch (Exception exception) { MessageBoxTD.Show(exception.Message); }
        }
    }
}
