using System;
using System.Windows.Forms;

namespace TDJS_Vision.Node
{
    /// <summary>编辑节点备注，打开时回填已有内容，仅确认时写回节点。</summary>
    public partial class FormAddNotes : FormBase
    {
        /// <summary>当前正在编辑备注的节点。</summary>
        private readonly NodeBase _node;

        /// <summary>创建备注编辑窗口并显示节点已经保存的内容。</summary>
        public FormAddNotes(NodeBase node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            InitializeComponent();
            textBox1.Text = _node.Notes ?? string.Empty;
        }

        /// <summary>确认修改后保存备注；直接关闭窗口不会覆盖原有内容。</summary>
        private void button1_Click(object sender, EventArgs e)
        {
            _node.SetNotes(textBox1.Text);
            Close();
        }
    }
}
