"""生成标准WinForms设计器声明；每个控件均显式保存在Designer文件中。"""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
target = root / 'Node/7-ResultProcessing/ResultSend/ParamFormResultSend.Designer.cs'
controls = {
    'mainLayout': ('TableLayoutPanel', '窗体主布局'), 'topLayout': ('FlowLayoutPanel', '设备选择栏'),
    'labelDevice': ('Label', '设备标签'), 'comboDevice': ('ComboBox', '通信设备'),
    'middleLayout': ('TableLayoutPanel', '发送列表与订阅树布局'), 'groupRows': ('GroupBox', '发送列表分组'),
    'rowsLayout': ('TableLayoutPanel', '发送列表内部布局'), 'gridRows': ('DataGridView', '发送配置表格'),
    'rowButtons': ('FlowLayoutPanel', '行编辑按钮栏'), 'buttonAdd': ('Button', '添加发送项'),
    'buttonDelete': ('Button', '删除发送项'), 'buttonUp': ('Button', '上移发送项'), 'buttonDown': ('Button', '下移发送项'),
    'groupSources': ('GroupBox', '上游节点结果分组'), 'sourcesLayout': ('TableLayoutPanel', '结果树布局'),
    'treeSources': ('TreeView', '上游节点结果树'), 'buttonSubscribe': ('Button', '订阅选中字段'),
    'groupEditor': ('GroupBox', '当前行参数分组'), 'editorLayout': ('TableLayoutPanel', '参数编辑布局'),
    'bottomLayout': ('TableLayoutPanel', '预览和保存布局'), 'labelPreview': ('Label', '预览与状态文本'),
    'buttonPreview': ('Button', '预览发送值'), 'buttonSave': ('Button', '保存参数')
}
fields = [
    ('comboMode','ComboBox','取值方式'),('textAddress','TextBox','目标地址'),('comboType','ComboBox','写入类型'),('textItem','TextBox','AI检测项名称'),
    ('numberIndex','NumericUpDown','明细序号'),('comboAggregate','ComboBox','汇总方式'),('numberCapacity','NumericUpDown','预留容量'),('textCount','TextBox','数量地址'),
    ('checkClear','CheckBox','剩余位置补默认值'),('numberStringBytes','NumericUpDown','字符串字节数'),('comboEncoding','ComboBox','字符串编码')
]
for name, kind, label in fields:
    controls[name] = (kind, label)
    controls['label'+name] = ('Label',label+'标签')
columns = [('columnEnabled','DataGridViewCheckBoxColumn','Enabled','启用',45),('columnSource','DataGridViewTextBoxColumn','SourceText','订阅结果',290),('columnMode','DataGridViewTextBoxColumn','Mode','取值方式',100),('columnAddress','DataGridViewTextBoxColumn','Address','目标地址',110),('columnType','DataGridViewTextBoxColumn','DataType','写入类型',80)]
for name, kind, prop, label, width in columns: controls[name] = (kind,label+'列')
lines = ['namespace TDJS_Vision.Node._7_ResultProcessing.ResultSend','{','    partial class ParamFormResultSend','    {',
         '        /// <summary>设计器组件容器。</summary>','        private System.ComponentModel.IContainer components = null;',
         '        /// <summary>释放控件及节点事件订阅。</summary>','        protected override void Dispose(bool disposing)','        {',
         '            if (disposing) { DetachEvents(); if (components != null) components.Dispose(); }','            base.Dispose(disposing);','        }',
         '        /// <summary>设计器生成的标准控件布局，便于直接在设计器中调整。</summary>','        private void InitializeComponent()','        {',
         '            this.components = new System.ComponentModel.Container();']
def emit(code):
    lines.append('            '+code)
for name,(kind,label) in controls.items(): emit(f'this.{name} = new System.Windows.Forms.{kind}();')
emit('this.SuspendLayout();')
for name,(kind,label) in controls.items():
    emit(f'this.{name}.Name = "{name}";')
    if not kind.startswith('DataGridView') or kind=='DataGridView':
        emit(f'this.{name}.TabIndex = {list(controls).index(name)};')
    if kind=='ComboBox': emit(f'this.{name}.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;')
    if kind in ('TableLayoutPanel','GroupBox','TreeView','DataGridView'):
        emit(f'this.{name}.Dock = System.Windows.Forms.DockStyle.Fill;')
def table(name,cols,rows):
    emit(f'this.{name}.ColumnCount = {len(cols)}; this.{name}.RowCount = {len(rows)};')
    for absolute,size in cols: emit(f'this.{name}.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.{"Absolute" if absolute else "Percent"}, {size}F));')
    for absolute,size in rows: emit(f'this.{name}.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.{"Absolute" if absolute else "Percent"}, {size}F));')
def add(parent,child,location=''): emit(f'this.{parent}.Controls.Add(this.{child}{location});')
table('mainLayout',[(False,100)],[(True,48),(False,100),(True,172),(True,70)])
emit('this.mainLayout.Padding = new System.Windows.Forms.Padding(10);')
table('middleLayout',[(False,68),(False,32)],[(False,100)])
table('rowsLayout',[(False,100)],[(False,100),(True,40)])
table('sourcesLayout',[(False,100)],[(False,100),(True,40)])
table('bottomLayout',[(False,100),(True,120),(True,100)],[(False,100)])
table('editorLayout',[(True,135),(False,25),(True,110),(False,25),(True,110),(False,25),(True,115),(False,25)],[(True,46)]*3)
for child,index in [('topLayout',0),('middleLayout',1),('groupEditor',2),('bottomLayout',3)]: add('mainLayout',child,f', 0, {index}')
emit('this.topLayout.Dock = System.Windows.Forms.DockStyle.Fill; this.topLayout.Padding = new System.Windows.Forms.Padding(4, 8, 0, 0);')
emit('this.labelDevice.Text = "通信设备"; this.labelDevice.AutoSize = true; this.labelDevice.Margin = new System.Windows.Forms.Padding(0, 5, 12, 0);')
emit('this.comboDevice.Width = 370;')
add('topLayout','labelDevice'); add('topLayout','comboDevice')
add('middleLayout','groupRows',', 0, 0'); add('middleLayout','groupSources',', 1, 0')
emit('this.groupRows.Text = "发送列表"; this.groupSources.Text = "上游节点结果"; this.groupEditor.Text = "当前行参数";')
add('groupRows','rowsLayout');add('rowsLayout','gridRows',', 0, 0');add('rowsLayout','rowButtons',', 0, 1')
emit('this.rowButtons.Dock = System.Windows.Forms.DockStyle.Fill;')
for name,label,event in [('buttonAdd','添加','AddClicked'),('buttonDelete','删除','DeleteClicked'),('buttonUp','上移','UpClicked'),('buttonDown','下移','DownClicked'),('buttonSubscribe','订阅到当前行','SubscribeClicked'),('buttonPreview','预览发送值','PreviewClicked'),('buttonSave','保存','SaveClicked')]:
    emit(f'this.{name}.Text = "{label}"; this.{name}.Size = new System.Drawing.Size({160 if name=="buttonSubscribe" else 110 if name=="buttonPreview" else 85}, 32);')
    emit(f'this.{name}.UseVisualStyleBackColor = true; this.{name}.Click += new System.EventHandler(this.{event});')
    if name in ('buttonAdd','buttonDelete','buttonUp','buttonDown'): add('rowButtons',name)
emit('this.buttonSave.BackColor = System.Drawing.Color.MediumTurquoise; this.buttonSave.UseVisualStyleBackColor = false;')
emit('this.gridRows.AutoGenerateColumns = false; this.gridRows.AllowUserToAddRows = false; this.gridRows.AllowUserToDeleteRows = false;')
emit('this.gridRows.BackgroundColor = System.Drawing.Color.White; this.gridRows.RowHeadersVisible = false; this.gridRows.MultiSelect = false;')
emit('this.gridRows.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill; this.gridRows.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;')
emit('this.gridRows.RowTemplate.Height = 32; this.gridRows.ColumnHeadersHeight = 34;')
for name,kind,prop,label,width in columns:
    emit(f'this.{name}.DataPropertyName = "{prop}"; this.{name}.HeaderText = "{label}"; this.{name}.FillWeight = {width}F; this.{name}.MinimumWidth = {45 if prop=="Enabled" else 70};')
    emit(f'this.{name}.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;')
    if prop!='Enabled': emit(f'this.{name}.ReadOnly = true;')
emit('this.gridRows.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { '+', '.join('this.'+entry[0] for entry in columns)+' });')
emit('this.gridRows.SelectionChanged += new System.EventHandler(this.GridSelectionChanged);')
emit('this.gridRows.CurrentCellChanged += new System.EventHandler(this.GridSelectionChanged);')
emit('this.gridRows.CurrentCellDirtyStateChanged += new System.EventHandler(this.GridDirtyChanged);')
emit('this.gridRows.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.GridCellFormatting);')
add('groupSources','sourcesLayout');add('sourcesLayout','treeSources',', 0, 0');add('sourcesLayout','buttonSubscribe',', 0, 1')
emit('this.treeSources.HideSelection = false; this.treeSources.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.SourceDoubleClicked);')
add('groupEditor','editorLayout')
for i,(name,kind,label) in enumerate(fields):
    row,col=divmod(i,4); col*=2
    emit(f'this.label{name}.Text = "{label}"; this.label{name}.AutoSize = true; this.label{name}.Anchor = System.Windows.Forms.AnchorStyles.Left;')
    emit(f'this.{name}.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;')
    if kind=='NumericUpDown':
        positive = name in ('numberIndex','numberCapacity','numberStringBytes')
        maxvalue = 1024 if name in ('numberCapacity','numberIndex') else 4096 if name=='numberStringBytes' else 1000000000
        emit(f'this.{name}.Minimum = {1 if positive else -1000000000}; this.{name}.Maximum = {maxvalue};')
        if not positive: emit(f'this.{name}.DecimalPlaces = 6;')
        emit(f'this.{name}.ValueChanged += new System.EventHandler(this.EditorChanged);')
    elif kind=='ComboBox': emit(f'this.{name}.SelectedIndexChanged += new System.EventHandler(this.EditorChanged);')
    elif kind=='TextBox': emit(f'this.{name}.TextChanged += new System.EventHandler(this.EditorChanged);')
    else:
        emit(f'this.{name}.Text = "启用"; this.{name}.CheckedChanged += new System.EventHandler(this.EditorChanged);')
    add('editorLayout','label'+name,f', {col}, {row}');add('editorLayout',name,f', {col+1}, {row}')
emit('this.labelPreview.Text = "先选择上游结果，再配置目标地址；预览不执行通信。"; this.labelPreview.Dock = System.Windows.Forms.DockStyle.Fill; this.labelPreview.AutoEllipsis = true; this.labelPreview.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;')
for name,col in [('labelPreview',0),('buttonPreview',1),('buttonSave',2)]: add('bottomLayout',name,f', {col}, 0')
emit('this.buttonPreview.Anchor = System.Windows.Forms.AnchorStyles.Right; this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.Right;')
emit('this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F); this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;')
emit('this.Font = new System.Drawing.Font("宋体", 10.5F); this.ClientSize = new System.Drawing.Size(1220, 850); this.MinimumSize = new System.Drawing.Size(1100, 760);')
emit('this.Name = "ParamFormResultSend"; this.Text = "结果发送"; this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;')
emit('this.Controls.Add(this.mainLayout); this.Controls.SetChildIndex(this.mainLayout, 0);')
emit('this.ResumeLayout(false);')
lines.append('        }')
for name,(kind,label) in controls.items(): lines.extend([f'        /// <summary>{label}。</summary>',f'        private System.Windows.Forms.{kind} {name};'])
lines.extend(['    }','}',''])
target.write_text('\n'.join(lines),encoding='utf-8-sig')
print(target)
