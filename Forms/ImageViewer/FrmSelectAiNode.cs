using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Forms.ImageViewer
{
    /// <summary>
    /// 多个AI检测节点时用于选择目标节点的窗体。
    /// </summary>
    public partial class FrmSelectAiNode : Form
    {
        /// <summary>
        /// 当前选中的AI检测节点。
        /// </summary>
        public NodeTDAI SelectedAiNode { get; private set; }

        /// <summary>
        /// 初始化AI节点选择窗体。
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="aiNodes"></param>
        public FrmSelectAiNode(string processName, IEnumerable<NodeTDAI> aiNodes)
        {
            InitializeComponent();
            Text = "选择AI检测节点";
            labelMessage.Text = $"流程“{processName}”中存在多个AI检测节点，请选择本窗口按钮要操作的节点：";

            foreach (var node in aiNodes ?? Enumerable.Empty<NodeTDAI>())
                listBoxAiNodes.Items.Add(new AiNodeListItem(node));

            if (listBoxAiNodes.Items.Count > 0)
                listBoxAiNodes.SelectedIndex = 0;
        }

        /// <summary>
        /// 弹出选择窗体并返回用户选择的AI检测节点。
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="processName"></param>
        /// <param name="aiNodes"></param>
        /// <returns></returns>
        public static NodeTDAI SelectNode(IWin32Window owner, string processName, IEnumerable<NodeTDAI> aiNodes)
        {
            using (var form = new FrmSelectAiNode(processName, aiNodes))
            {
                return form.ShowDialog(owner) == DialogResult.OK ? form.SelectedAiNode : null;
            }
        }

        /// <summary>
        /// 确认选择。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOk_Click(object sender, EventArgs e)
        {
            if (listBoxAiNodes.SelectedItem is AiNodeListItem item)
            {
                SelectedAiNode = item.Node;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        /// <summary>
        /// 取消选择。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// 双击列表项时直接确认。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBoxAiNodes_DoubleClick(object sender, EventArgs e)
        {
            buttonOk_Click(sender, e);
        }

        /// <summary>
        /// AI检测节点列表显示项。
        /// </summary>
        private sealed class AiNodeListItem
        {
            /// <summary>
            /// AI检测节点。
            /// </summary>
            public NodeTDAI Node { get; }

            /// <summary>
            /// 初始化列表显示项。
            /// </summary>
            /// <param name="node"></param>
            public AiNodeListItem(NodeTDAI node)
            {
                Node = node;
            }

            /// <summary>
            /// 返回列表中显示的节点名称和模型信息。
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                string detail = string.Empty;
                if (Node?.ParamForm?.Params is NodeParamTDAI param)
                    detail = $" - {param.CurDetectItemName} / {param.ModelName}";

                return Node == null ? string.Empty : $"{Node.ID}.{Node.NodeName}{detail}";
            }
        }
    }
}
