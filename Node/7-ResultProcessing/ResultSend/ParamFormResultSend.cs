using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultSend
{
    /// <summary>沿用现有参数窗体风格的结果发送编辑器；编辑副本，保存后才影响生产参数。</summary>
    public partial class ParamFormResultSend : FormBase, INodeParamForm
    {
        /// <summary>所属节点。</summary>
        private NodeResultSend _node;
        /// <summary>尚未保存的表格配置。</summary>
        private BindingList<ResultSendRow> _rows = new BindingList<ResultSendRow>();
        /// <summary>防止设置控件时反向触发配置写入。</summary>
        private bool _updating;
        /// <summary>当前正在编辑的行。</summary>
        private ResultSendRow _current;
        /// <summary>节点已经保存的参数。</summary>
        public INodeParam Params { get; set; }

        /// <summary>初始化设计器控件及八种基础写入类型。</summary>
        public ParamFormResultSend()
        {
            InitializeComponent();
            _updating = true;
            Bind(comboType, Enum.GetValues(typeof(ResultSendType)).Cast<ResultSendType>().Select(value => Pair(ResultSendValueConverter.GetTypeName(value), value)));
            Bind(comboMode, new[] { Pair("直接取值", ResultSendMode.Single), Pair("指定第几项", ResultSendMode.Index), Pair("全部连续发送", ResultSendMode.All), Pair("汇总为单值", ResultSendMode.Aggregate) });
            Bind(comboAggregate, new[] { Pair("最大值", ResultSendAggregate.Maximum), Pair("最小值", ResultSendAggregate.Minimum), Pair("平均值", ResultSendAggregate.Average), Pair("求和", ResultSendAggregate.Sum), Pair("全部为真", ResultSendAggregate.AllTrue), Pair("任意为真", ResultSendAggregate.AnyTrue) });
            comboEncoding.Items.AddRange(new object[] { "UTF-8", "ASCII", "GB18030" });
            comboEncoding.SelectedIndex = 0;
            gridRows.DataSource = _rows;
            _updating = false;
            NodeBase.RefreshNodeSubControl += OnNodeRenamed;
            NodeBase.OutputDefinitionChanged += OnNodeChanged;
            NodeBase.NodeDeletedEvent += OnNodeChanged;
        }

        /// <summary>创建显示名称与枚举值绑定。</summary>
        private static KeyValuePair<string, T> Pair<T>(string name, T value) => new KeyValuePair<string, T>(name, value);
        /// <summary>绑定下拉框，使用中文显示名称保存枚举值。</summary>
        private static void Bind<T>(ComboBox combo, IEnumerable<KeyValuePair<string, T>> values)
        {
            combo.DisplayMember = "Key";
            combo.ValueMember = "Value";
            combo.DataSource = values.ToList();
        }

        /// <summary>绑定节点及其连线变更事件。</summary>
        public void SetNodeBelong(NodeBase node)
        {
            if (_node?.Process != null) _node.Process.ConnectionsChanged -= OnConnectionsChanged;
            _node = node as NodeResultSend;
            if (_node?.Process != null) _node.Process.ConnectionsChanged += OnConnectionsChanged;
            RefreshSources();
        }

        /// <summary>从已保存参数恢复独立编辑副本。</summary>
        public void SetParam2Form()
        {
            var parameter = (Params as NodeParamResultSend)?.Copy() ?? new NodeParamResultSend();
            _updating = true;
            _current = null;
            _rows = new BindingList<ResultSendRow>(parameter.Rows);
            gridRows.DataSource = _rows;
            RefreshDevices(parameter.DeviceName);
            _updating = false;
            RefreshSources();
            SelectCurrentRow();
        }

        /// <summary>更新设备下拉框，保留缺失设备名称以便用户定位旧配置。</summary>
        private void RefreshDevices(string selected)
        {
            var devices = Solution.Instance.AllDevices.Where(device => device is IPlc || device is IModbus)
                .Where(device => device.DevType != Device.DevType.ModbusTcpSlave && device.DevType != Device.DevType.ModbusRTUSlave)
                .Select(device => Pair((device is IPlc ? "PLC：" : "Modbus：") + device.UserDefinedName, device.UserDefinedName)).ToList();
            if (!string.IsNullOrEmpty(selected) && !devices.Any(item => item.Value == selected)) devices.Add(Pair("设备缺失：" + selected, selected));
            devices.Insert(0, Pair("请选择通信设备", string.Empty));
            Bind(comboDevice, devices);
            comboDevice.SelectedValue = selected ?? string.Empty;
        }

        /// <summary>重算上游订阅目录，支持运行前配置和隐藏期间的连线变化。</summary>
        private void RefreshSources()
        {
            if (IsDisposed || Disposing || treeSources == null) return;
            if (InvokeRequired) { BeginInvoke(new Action(RefreshSources)); return; }
            treeSources.BeginUpdate();
            try
            {
                treeSources.Nodes.Clear();
                if (_node?.Process == null) return;
                foreach (var sourceNode in _node.Process.GetUpstreamNodes(_node))
                {
                    var sources = ResultSendSourceReader.GetSources(sourceNode);
                    if (sources.Count == 0) continue;
                    var branch = new TreeNode(sourceNode.ID + "." + sourceNode.NodeName);
                    foreach (var source in sources)
                        branch.Nodes.Add(new TreeNode(source.DisplayName.Substring(source.DisplayName.IndexOf(" → ", StringComparison.Ordinal) + 3)) { Tag = source });
                    treeSources.Nodes.Add(branch);
                }
                treeSources.ExpandAll();
                if (treeSources.Nodes.Count > 0) treeSources.TopNode = treeSources.Nodes[0];
            }
            finally { treeSources.EndUpdate(); }
        }

        /// <summary>连线变更后刷新树。</summary>
        private void OnConnectionsChanged(object sender, EventArgs e) => RefreshSources();
        /// <summary>节点输出变化或删除后刷新树。</summary>
        private void OnNodeChanged(object sender, NodeBase e) => RefreshSources();
        /// <summary>节点改名后按稳定ID刷新显示，不改变订阅路径。</summary>
        private void OnNodeRenamed(object sender, RenameResult e) => RefreshSources();
        /// <summary>实际释放时解除静态事件，避免节点删除后对象滞留。</summary>
        private void DetachEvents()
        {
            NodeBase.RefreshNodeSubControl -= OnNodeRenamed;
            NodeBase.OutputDefinitionChanged -= OnNodeChanged;
            NodeBase.NodeDeletedEvent -= OnNodeChanged;
            if (_node?.Process != null) _node.Process.ConnectionsChanged -= OnConnectionsChanged;
        }
        /// <summary>用户关闭参数窗体时隐藏，保留节点绑定。</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            base.OnFormClosing(e);
        }
        /// <summary>再次显示时刷新设备及订阅候选。</summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible || comboDevice == null) return;
            RefreshDevices(comboDevice.SelectedValue as string ?? (Params as NodeParamResultSend)?.DeviceName);
            RefreshSources();
        }

        /// <summary>表格切换行时同步下方参数编辑器。</summary>
        private void GridSelectionChanged(object sender, EventArgs e) { if (!_updating) SelectCurrentRow(); }
        /// <summary>将当前行的完整参数显示在编辑控件中。</summary>
        private void SelectCurrentRow()
        {
            _current = gridRows.CurrentRow?.DataBoundItem as ResultSendRow;
            groupEditor.Enabled = _current != null;
            if (_current == null) return;
            _updating = true;
            try
            {
                comboType.SelectedValue = _current.DataType;
                comboMode.SelectedValue = _current.Mode;
                comboAggregate.SelectedValue = _current.Aggregate;
                textAddress.Text = _current.Address;
                textItem.Text = DetectItemLanguage.GetDisplayName(_current.Source?.ItemName);
                textCount.Text = _current.CountAddress;
                numberIndex.Value = Clamp(numberIndex, _current.Index);
                numberCapacity.Value = Clamp(numberCapacity, _current.Capacity);
                numberStringBytes.Value = Clamp(numberStringBytes, _current.StringBytes);
                checkClear.Checked = _current.ClearRemaining;
                comboEncoding.SelectedItem = _current.EncodingName;
                labelPreview.Text = _current.SourceText;
                UpdateEditorState();
            }
            finally { _updating = false; }
        }
        /// <summary>显示旧参数时限制数值控件范围，保存前仍校验原参数。</summary>
        private static decimal Clamp(NumericUpDown control, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return control.Minimum;
            return (decimal)Math.Max((double)control.Minimum, Math.Min((double)control.Maximum, value));
        }
        /// <summary>只显示当前取值方式与写入类型适用的参数，收起无关行。</summary>
        private void UpdateEditorState()
        {
            if (_current == null) return;
            bool all = _current.Mode == ResultSendMode.All;
            bool text = _current.DataType == ResultSendType.String;
            Control[] fields = { numberIndex, comboAggregate, numberCapacity, textCount, checkClear, numberStringBytes, comboEncoding };
            bool[] visible = { _current.Mode == ResultSendMode.Index, _current.Mode == ResultSendMode.Aggregate, all, all, all, text, text };
            editorLayout.SuspendLayout();
            try
            {
                // 将适用字段连续排在前面，避免隐藏无关选项后留下大块空白；控件仍全部由设计器创建。
                int position = 0;
                foreach (int index in Enumerable.Range(0, fields.Length).OrderByDescending(index => visible[index]))
                {
                    var control = fields[index];
                    editorLayout.SetCellPosition(editorLayout.Controls["label" + control.Name], new TableLayoutPanelCellPosition(position % 4 * 2, 1 + position / 4));
                    editorLayout.SetCellPosition(control, new TableLayoutPanelCellPosition(position % 4 * 2 + 1, 1 + position / 4));
                    SetFieldVisible(control, visible[index]);
                    position++;
                }
                SetFieldVisible(textItem, _current.Source != null && _current.Source.Kind >= ResultSendSourceKind.AiValues);
                int count = visible.Count(value => value);
                editorLayout.RowStyles[1].Height = count > 0 ? 46 : 0;
                editorLayout.RowStyles[2].Height = count > 4 ? 46 : 0;
                mainLayout.RowStyles[2].Height = 80 + editorLayout.RowStyles[1].Height + editorLayout.RowStyles[2].Height;
            }
            finally { editorLayout.ResumeLayout(true); }
        }

        /// <summary>同时显示或隐藏设计器中的输入控件及对应中文标签。</summary>
        private void SetFieldVisible(Control control, bool visible)
        {
            control.Visible = visible;
            editorLayout.Controls["label" + control.Name].Visible = visible;
        }
        /// <summary>修改编辑控件只更新编辑副本，不写运行参数。</summary>
        private void EditorChanged(object sender, EventArgs e)
        {
            if (_updating || _current == null) return;
            _current.DataType = (ResultSendType)comboType.SelectedValue;
            _current.Mode = (ResultSendMode)comboMode.SelectedValue;
            _current.Aggregate = (ResultSendAggregate)comboAggregate.SelectedValue;
            _current.Address = textAddress.Text.Trim();
            // 只有用户改动检测项输入框才更新键，避免切换类型时把已有订阅键覆盖为显示文字。
            if (ReferenceEquals(sender, textItem)) _current.Source.ItemName = DetectItemLanguage.NormalizeName(textItem.Text.Trim());
            _current.CountAddress = textCount.Text.Trim();
            _current.Index = (int)numberIndex.Value;
            _current.Capacity = (int)numberCapacity.Value;
            _current.StringBytes = (int)numberStringBytes.Value;
            _current.EncodingName = comboEncoding.Text;
            _current.ClearRemaining = checkClear.Checked;
            _updating = true;
            _rows.ResetItem(_rows.IndexOf(_current));
            _updating = false;
            UpdateEditorState();
        }
        /// <summary>使勾选启用状态立即提交到编辑列表。</summary>
        private void GridDirtyChanged(object sender, EventArgs e)
        {
            if (gridRows.IsCurrentCellDirty) gridRows.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
        /// <summary>表格显示中文取值方式及基础类型名。</summary>
        private void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value is ResultSendType type) { e.Value = ResultSendValueConverter.GetTypeName(type); e.FormattingApplied = true; }
            if (e.Value is ResultSendMode mode)
            {
                e.Value = new[] { "直接取值", "指定单项", "全部发送", "汇总发送" }[(int)mode];
                e.FormattingApplied = true;
            }
        }

        /// <summary>新增发送行，可立即使用右侧选中的来源。</summary>
        private void AddClicked(object sender, EventArgs e)
        {
            var row = new ResultSendRow();
            var source = treeSources.SelectedNode?.Tag as ResultSendSource;
            if (source != null) AssignSource(row, source);
            _rows.Add(row);
            gridRows.CurrentCell = gridRows.Rows[_rows.Count - 1].Cells[1];
            SelectCurrentRow();
        }
        /// <summary>删除当前行。</summary>
        private void DeleteClicked(object sender, EventArgs e) { if (_current != null) _rows.Remove(_current); SelectCurrentRow(); }
        /// <summary>上移发送顺序。</summary>
        private void UpClicked(object sender, EventArgs e) => MoveRow(-1);
        /// <summary>下移发送顺序。</summary>
        private void DownClicked(object sender, EventArgs e) => MoveRow(1);
        /// <summary>调整表格顺序，运行时严格使用此顺序。</summary>
        private void MoveRow(int offset)
        {
            int index = _rows.IndexOf(_current), next = index + offset;
            if (index < 0 || next < 0 || next >= _rows.Count) return;
            var row = _current;
            _updating = true;
            _rows.RemoveAt(index); _rows.Insert(next, row);
            gridRows.CurrentCell = gridRows.Rows[next].Cells[1];
            _updating = false;
            SelectCurrentRow();
        }
        /// <summary>将右侧结果订阅到当前行。</summary>
        private void SubscribeClicked(object sender, EventArgs e)
        {
            var source = treeSources.SelectedNode?.Tag as ResultSendSource;
            if (source == null) { MessageBoxTD.Show(this, "请选择右侧具体结果字段。"); return; }
            if (_current == null) { AddClicked(sender, e); return; }
            AssignSource(_current, source);
            _rows.ResetBindings();
            SelectCurrentRow();
        }
        /// <summary>双击字段直接订阅。</summary>
        private void SourceDoubleClicked(object sender, TreeNodeMouseClickEventArgs e) => SubscribeClicked(sender, e);
        /// <summary>切换订阅来源时设置安全的默认取值方式。</summary>
        private static void AssignSource(ResultSendRow row, ResultSendSource source)
        {
            row.Source = source.Copy();
            row.Mode = source.Multiple ? ResultSendMode.Index : ResultSendMode.Single;
            row.DataType = source.Boolean ? ResultSendType.Boolean : ResultSendType.Double;
            row.Aggregate = source.Boolean ? ResultSendAggregate.AllTrue : ResultSendAggregate.Maximum;
        }
        /// <summary>构建保存或预览用的独立参数。</summary>
        private NodeParamResultSend GetEditingParameters()
        {
            gridRows.EndEdit();
            return new NodeParamResultSend { DeviceName = comboDevice.SelectedValue as string, Rows = _rows.Select(row => row.Copy()).ToList() };
        }
        /// <summary>校验配置并保存，允许尚未执行上游时先配置方案。</summary>
        private void SaveClicked(object sender, EventArgs e)
        {
            try
            {
                var param = GetEditingParameters();
                var writer = NodeResultSend.GetWriter(param);
                string endpoint = writer.EndpointKey;
                if (!param.Rows.Any(row => row.Enabled)) throw new InvalidOperationException("至少启用一个发送项。");
                foreach (var row in param.Rows.Where(row => row.Enabled))
                {
                    ResultSendValueConverter.ValidateConfiguration(row);
                    if (!_node.Process.GetUpstreamNodes(_node).Any(node => node.ID == row.Source.NodeId)) throw new InvalidOperationException("订阅源不再是上游节点。");
                    writer.Validate(row.Address, row.DataType, Array.CreateInstance(ResultSendValueConverter.GetValueType(row.DataType), 0));
                    if (row.Mode == ResultSendMode.All) writer.Validate(row.CountAddress, ResultSendType.UInt16, new ushort[1]);
                }
                Params = param;
                Hide();
            }
            catch (Exception exception) { MessageBoxTD.Show(this, exception.Message); }
        }
        /// <summary>预览最近一次结果的转换值，只读取数据，不访问设备写入接口。</summary>
        private void PreviewClicked(object sender, EventArgs e)
        {
            try
            {
                var param = GetEditingParameters();
                var rows = _node.PrepareRows(param, NodeResultSend.GetWriter(param), false);
                labelPreview.Text = string.Join("；", rows.Select(row => "第" + row.RowNumber + "行 " + row.Configuration.Address + " ← [" +
                    string.Join(", ", row.Values.Cast<object>().Take(8).Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))) +
                    (row.Values.Length > 8 ? "…" : "") + "]，有效数量=" + row.Count)) + "（仅预览，未发送）";
            }
            catch (Exception exception) { labelPreview.Text = "预览失败：" + exception.Message; }
        }
    }
}
