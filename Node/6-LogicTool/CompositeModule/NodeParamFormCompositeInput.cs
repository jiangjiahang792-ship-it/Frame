using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合输入参数窗体，用于声明可由外部组合模块传入的输入变量。
    /// </summary>
    public partial class NodeParamFormCompositeInput : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>
        /// 输入端口绑定列表。
        /// </summary>
        private BindingList<InputPortViewModel> _ports;

        /// <summary>
        /// 创建组合输入参数窗体。
        /// </summary>
        public NodeParamFormCompositeInput()
        {
            InitializeComponent();
            _ports = new BindingList<InputPortViewModel>();
            ConfigureGrid();
            BindValueTypeColumn();
            dataGridViewPorts.DataSource = _ports;
            if (_ports.Count == 0)
                _ports.Add(CreateDefaultPort(0));
        }

        /// <summary>
        /// 节点运行参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 设置当前参数窗体所属节点。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node)
        {
            _node = node;
        }

        /// <summary>
        /// 将反序列化参数回写到界面。
        /// </summary>
        public void SetParam2Form()
        {
            NodeParamCompositeInput param = Params as NodeParamCompositeInput;
            _ports = new BindingList<InputPortViewModel>();
            if (param != null && param.Ports != null)
            {
                for (int index = 0; index < param.Ports.Count; index++)
                    _ports.Add(InputPortViewModel.FromPort(param.Ports[index], index));
            }

            if (_ports.Count == 0)
                _ports.Add(CreateDefaultPort(0));

            dataGridViewPorts.DataSource = _ports;
        }

        /// <summary>
        /// 配置端口表格。
        /// </summary>
        private void ConfigureGrid()
        {
            dataGridViewPorts.AutoGenerateColumns = false;
            dataGridViewPorts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewPorts.RowTemplate.Height = 30;
            ColumnName.DataPropertyName = "Name";
            ColumnValueType.DataPropertyName = "ValueType";
            ColumnDefaultValue.DataPropertyName = "DefaultValue";
            ColumnNote.DataPropertyName = "Note";
        }

        /// <summary>
        /// 绑定端口类型下拉选项。
        /// </summary>
        private void BindValueTypeColumn()
        {
            ColumnValueType.DataSource = GetValueTypeOptions();
            ColumnValueType.DisplayMember = "Text";
            ColumnValueType.ValueMember = "Value";
        }

        /// <summary>
        /// 新增端口。
        /// </summary>
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            EndGridEdit();
            _ports.Add(CreateDefaultPort(_ports.Count));
            SelectRow(_ports.Count - 1);
        }

        /// <summary>
        /// 删除端口。
        /// </summary>
        private void buttonRemove_Click(object sender, EventArgs e)
        {
            EndGridEdit();
            int index = GetCurrentRowIndex();
            if (index < 0)
                return;

            _ports.RemoveAt(index);
            if (_ports.Count == 0)
                _ports.Add(CreateDefaultPort(0));

            SelectRow(Math.Min(index, _ports.Count - 1));
        }

        /// <summary>
        /// 上移端口。
        /// </summary>
        private void buttonMoveUp_Click(object sender, EventArgs e)
        {
            MoveCurrentRow(-1);
        }

        /// <summary>
        /// 下移端口。
        /// </summary>
        private void buttonMoveDown_Click(object sender, EventArgs e)
        {
            MoveCurrentRow(1);
        }

        /// <summary>
        /// 保存端口配置。
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            try
            {
                Params = BuildParamFromForm();
                Hide();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 取消编辑。
        /// </summary>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            SetParam2Form();
            Hide();
        }

        /// <summary>
        /// 表格数据错误处理。
        /// </summary>
        private void dataGridViewPorts_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        /// <summary>
        /// 从界面构建参数。
        /// </summary>
        /// <returns>组合输入参数。</returns>
        private NodeParamCompositeInput BuildParamFromForm()
        {
            EndGridEdit();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NodeParamCompositeInput param = new NodeParamCompositeInput();
            for (int index = 0; index < _ports.Count; index++)
            {
                InputPortViewModel row = _ports[index];
                if (row == null)
                    continue;

                string name = (row.Name ?? string.Empty).Trim();
                ValidatePortName(name, names, index);
                param.Ports.Add(row.ToPort());
            }

            if (param.Ports.Count == 0)
                throw new Exception("至少需要配置一个输入端口。");

            return param;
        }

        /// <summary>
        /// 校验端口名称。
        /// </summary>
        /// <param name="name">端口名称。</param>
        /// <param name="names">已存在名称集合。</param>
        /// <param name="index">行索引。</param>
        private static void ValidatePortName(string name, HashSet<string> names, int index)
        {
            string message;
            if (!DynamicResultVariableResolver.IsValidVariableName(name, out message))
                throw new Exception("第" + (index + 1) + "行输入端口名无效：" + message);

            if (!names.Add(name))
                throw new Exception("第" + (index + 1) + "行输入端口名“" + name + "”重复。");
        }

        /// <summary>
        /// 创建默认端口。
        /// </summary>
        /// <param name="index">行索引。</param>
        /// <returns>端口视图模型。</returns>
        private static InputPortViewModel CreateDefaultPort(int index)
        {
            return new InputPortViewModel
            {
                Name = "输入" + (index + 1),
                ValueType = CompositePortValueType.Number,
                DefaultValue = "0",
                Note = string.Empty
            };
        }

        /// <summary>
        /// 获取端口类型选项。
        /// </summary>
        /// <returns>端口类型选项。</returns>
        private static List<ValueTypeOption> GetValueTypeOptions()
        {
            return new List<ValueTypeOption>
            {
                new ValueTypeOption("自动", CompositePortValueType.Any),
                new ValueTypeOption("数值", CompositePortValueType.Number),
                new ValueTypeOption("布尔", CompositePortValueType.Boolean),
                new ValueTypeOption("文本", CompositePortValueType.String),
                new ValueTypeOption("图像", CompositePortValueType.Image)
            };
        }

        /// <summary>
        /// 结束表格编辑。
        /// </summary>
        private void EndGridEdit()
        {
            dataGridViewPorts.EndEdit();
            BindingContext[dataGridViewPorts.DataSource]?.EndCurrentEdit();
        }

        /// <summary>
        /// 获取当前行索引。
        /// </summary>
        /// <returns>当前行索引。</returns>
        private int GetCurrentRowIndex()
        {
            if (dataGridViewPorts.CurrentRow == null)
                return -1;

            return dataGridViewPorts.CurrentRow.Index;
        }

        /// <summary>
        /// 选择指定行。
        /// </summary>
        /// <param name="index">行索引。</param>
        private void SelectRow(int index)
        {
            if (index < 0 || index >= dataGridViewPorts.Rows.Count)
                return;

            dataGridViewPorts.ClearSelection();
            dataGridViewPorts.Rows[index].Selected = true;
            dataGridViewPorts.CurrentCell = dataGridViewPorts.Rows[index].Cells[0];
        }

        /// <summary>
        /// 移动当前行。
        /// </summary>
        /// <param name="direction">移动方向。</param>
        private void MoveCurrentRow(int direction)
        {
            EndGridEdit();
            int index = GetCurrentRowIndex();
            int targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 || targetIndex >= _ports.Count)
                return;

            InputPortViewModel row = _ports[index];
            _ports.RemoveAt(index);
            _ports.Insert(targetIndex, row);
            SelectRow(targetIndex);
        }

        /// <summary>
        /// 端口类型选项。
        /// </summary>
        private sealed class ValueTypeOption
        {
            /// <summary>
            /// 创建端口类型选项。
            /// </summary>
            /// <param name="text">显示文本。</param>
            /// <param name="value">端口类型。</param>
            public ValueTypeOption(string text, CompositePortValueType value)
            {
                Text = text;
                Value = value;
            }

            /// <summary>
            /// 显示文本。
            /// </summary>
            public string Text { get; private set; }

            /// <summary>
            /// 端口类型。
            /// </summary>
            public CompositePortValueType Value { get; private set; }
        }

        /// <summary>
        /// 输入端口表格视图模型。
        /// </summary>
        private sealed class InputPortViewModel
        {
            /// <summary>
            /// 端口名称。
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// 端口类型。
            /// </summary>
            public CompositePortValueType ValueType { get; set; }

            /// <summary>
            /// 默认值。
            /// </summary>
            public string DefaultValue { get; set; }

            /// <summary>
            /// 备注。
            /// </summary>
            public string Note { get; set; }

            /// <summary>
            /// 从参数端口创建视图模型。
            /// </summary>
            /// <param name="port">端口定义。</param>
            /// <param name="index">行索引。</param>
            /// <returns>视图模型。</returns>
            public static InputPortViewModel FromPort(CompositeInputPortDefinition port, int index)
            {
                if (port == null)
                    return CreateDefaultPort(index);

                return new InputPortViewModel
                {
                    Name = string.IsNullOrWhiteSpace(port.Name) ? "输入" + (index + 1) : port.Name,
                    ValueType = port.ValueType,
                    DefaultValue = port.DefaultValue,
                    Note = port.Note
                };
            }

            /// <summary>
            /// 转换为参数端口定义。
            /// </summary>
            /// <returns>端口定义。</returns>
            public CompositeInputPortDefinition ToPort()
            {
                return new CompositeInputPortDefinition
                {
                    Name = (Name ?? string.Empty).Trim(),
                    ValueType = ValueType,
                    DefaultValue = DefaultValue,
                    Note = Note
                };
            }
        }
    }
}
