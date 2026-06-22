using Logger;
using System;
using System.Windows.Forms;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._1_Acquisition.ImageShow3D
{
    /// <summary>
    /// 3D 图像显示节点参数窗体。
    /// </summary>
    public partial class ParamFormImageShow3D : FormBase, INodeParamForm
    {
        /// <summary>
        /// 创建 3D 图像显示节点参数窗体。
        /// </summary>
        public ParamFormImageShow3D()
        {
            InitializeComponent();
            LoadWindowNameList();
        }

        /// <summary>
        /// 节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 设置参数窗体所属节点。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 将参数恢复到窗体。
        /// </summary>
        public void SetParam2Form()
        {
            if (!(Params is NodeParamImageShow3D param))
                return;

            nodeSubscription1.SetText(param.Text1, param.Text2);
            SelectWindowKey(param.WindowName);
        }

        /// <summary>
        /// 获取订阅的 3D 帧数据。
        /// </summary>
        /// <returns>3D 帧数据。</returns>
        public Camera3DFrameData GetFrameData()
        {
            return nodeSubscription1.GetValue<Camera3DFrameData>();
        }

        /// <summary>
        /// 加载 3D 图像窗口列表。
        /// </summary>
        private void LoadWindowNameList()
        {
            string selectedKey = GetSelectedWindowKey();
            comboBoxWindowName.Items.Clear();
            for (int i = 0; i < FrmImageViewer.FrmSingle3DImages.Count; i++)
                comboBoxWindowName.Items.Add(new Image3DWindowListItem(FrmImageViewer.FrmSingle3DImages[i].FormName));

            if (!SelectWindowKey(selectedKey) && comboBoxWindowName.Items.Count > 0)
                comboBoxWindowName.SelectedIndex = 0;
        }

        /// <summary>
        /// 获取当前选中的窗口键名。
        /// </summary>
        /// <returns>窗口键名。</returns>
        private string GetSelectedWindowKey()
        {
            if (comboBoxWindowName.SelectedItem is Image3DWindowListItem selectedItem)
                return selectedItem.Key;

            return FrmSingle3DImage.NormalizeWindowKey(comboBoxWindowName.Text);
        }

        /// <summary>
        /// 选中指定窗口。
        /// </summary>
        /// <param name="key">窗口键名。</param>
        /// <returns>选中成功返回 true。</returns>
        private bool SelectWindowKey(string key)
        {
            key = FrmSingle3DImage.NormalizeWindowKey(key);
            if (string.IsNullOrWhiteSpace(key))
                return false;

            for (int i = 0; i < comboBoxWindowName.Items.Count; i++)
            {
                if (comboBoxWindowName.Items[i] is Image3DWindowListItem item && item.Key == key)
                {
                    comboBoxWindowName.SelectedIndex = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 确定按钮单击事件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(nodeSubscription1.GetText1()))
            {
                MessageBoxTD.Show("请选择订阅节点！");
                LogHelper.AddLog(MsgLevel.Fatal, "3D图像显示未选择订阅节点！", true);
                return;
            }

            if (nodeSubscription1.GetText2() != "3D帧数据")
            {
                MessageBoxTD.Show("3D图像显示需要订阅“3D帧数据”！");
                LogHelper.AddLog(MsgLevel.Fatal, "3D图像显示需要订阅“3D帧数据”！", true);
                return;
            }

            string windowKey = GetSelectedWindowKey();
            if (string.IsNullOrWhiteSpace(windowKey))
            {
                MessageBoxTD.Show("请选择3D图像窗口！");
                LogHelper.AddLog(MsgLevel.Fatal, "3D图像显示未选择3D图像窗口！", true);
                return;
            }

            Params = new NodeParamImageShow3D
            {
                WindowName = windowKey,
                Text1 = nodeSubscription1.GetText1(),
                Text2 = nodeSubscription1.GetText2()
            };

            Hide();
        }

        /// <summary>
        /// 3D 图像窗口列表项。
        /// </summary>
        private sealed class Image3DWindowListItem
        {
            /// <summary>
            /// 创建 3D 图像窗口列表项。
            /// </summary>
            /// <param name="key">窗口键名。</param>
            public Image3DWindowListItem(string key)
            {
                Key = FrmSingle3DImage.NormalizeWindowKey(key);
            }

            /// <summary>
            /// 窗口键名。
            /// </summary>
            public string Key { get; }

            /// <summary>
            /// 获取显示文本。
            /// </summary>
            /// <returns>显示文本。</returns>
            public override string ToString()
            {
                return FrmSingle3DImage.GetWindowDisplayName(Key);
            }
        }
    }
}
