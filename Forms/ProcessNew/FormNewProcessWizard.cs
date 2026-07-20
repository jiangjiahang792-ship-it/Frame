using gCursorLib;
using Logger;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.COMAdd;
using TDJS_Vision.Forms.TCPAdd;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node;
using TDJS_Vision.Startup;

namespace TDJS_Vision.Forms.ProcessNew
{
    public partial class FormNewProcessWizard : FormBase
    {
        // 编辑流程时通过快捷键保存方案的事件
        public event EventHandler OnShotKeySavePressed;
        private const int ToolboxExpandedWidth = 74;
        private const int ToolboxCollapsedWidth = 74;
        private const int ToolboxCategoryButtonSize = 58;
        private const int ToolboxModuleCardWidth = 118;
        private const int ToolboxModuleCardHeight = 86;
        private const int ToolboxFlyoutWidth = 286;
        private const int ProcessAddButtonWidth = 48;
        private const int ProcessActionButtonWidth = 48;
        private bool _toolboxCollapsed;
        private readonly List<Button> _toolboxCategoryButtons = new List<Button>();
        private TreeNode _selectedToolboxCategory;

        public FormNewProcessWizard()
        {
            InitializeComponent();
            ConfigureToolboxLayout();
            FrmCOMListView.OnCOMDeserializationCompletionEvent += Deserialization;
            this.KeyPreview = true;
            Init();
            BindLanguage();
            Shown += FormNewProcessWizard_Shown;
            FormProcessRename.ProcessRenameChanged += FormProcessRename_ProcessRenameChanged;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            panelEditorSurface.Resize += (s, e) =>
            {
                LayoutProcessActionStrip();
                if (panelModuleArea.Visible)
                    PositionToolboxFlyout();
            };
            tabControl1.SelectedIndexChanged += (s, e) =>
            {
                HideToolboxFlyout();
                LayoutProcessActionStrip();
            };
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(this, "ProcessNew.EditorTitle");
            LanguageManager.Bind(label1, "ProcessNew.Toolbox");
            LanguageManager.Bind(buttonAdd, "ProcessNew.AddProcess");
            LanguageManager.Bind(buttonRemove, "ProcessNew.DeleteProcess");
            LanguageManager.Bind(全部展开ToolStripMenuItem, "ProcessNew.ExpandAll");
            LanguageManager.Bind(全部折叠ToolStripMenuItem, "ProcessNew.CollapseAll");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
            ApplyToolboxLanguage();
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
            LanguageManager.ApplyToolStripItem(全部展开ToolStripMenuItem);
            LanguageManager.ApplyToolStripItem(全部折叠ToolStripMenuItem);
            ApplyToolboxCollapsedState(_toolboxCollapsed);
            ApplyCompactProcessActionText();
        }
        /// <summary>
        /// 流程重命名事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormProcessRename_ProcessRenameChanged(object sender, (string, string) e)
        {
            RenameTabPage(tabControl1, e.Item1, e.Item2);
        }
        /// <summary>
        /// 重命名选项卡
        /// </summary>
        /// <param name="tabControl"></param>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        public static void RenameTabPage(TabControl tabControl, string oldName, string newName)
        {
            // 遍历 TabPages 集合
            foreach (TabPage tabPage in tabControl.TabPages)
            {
                // 检查 TabPage 的 Text 属性是否等于 oldName
                var splitArr = tabPage.Text.Split('.');
                if (splitArr[1] == oldName)
                {
                    // 更改 TabPage 的名称
                    tabPage.Text = $"{splitArr[0]}.{newName}";
                    tabControl.Invalidate();
                    return; // 找到并重命名后退出循环
                }
            }

            // 如果没有找到指定名称的 TabPage，则输出提示信息
            LogHelper.AddLog(MsgLevel.Exception, LanguageManager.Format("ProcessNew.TabPageNotFound", oldName),true);
        }

        private void FormNewProcessWizard_Shown(object sender, EventArgs e)
        {
            ApplyToolboxLanguage();
            ApplyGeneratedToolboxIcons();
            BuildToolboxNavigation();
            LayoutProcessActionStrip();
        }

        private void ApplyToolboxLanguage()
        {
            foreach (TreeNode node in treeView1.Nodes)
                ApplyToolboxNodeLanguage(node);

            ApplyGeneratedToolboxIcons();
            BuildToolboxNavigation();
        }

        private void ApplyToolboxNodeLanguage(TreeNode node)
        {
            if (node == null)
                return;

            if (node.Tag is NodeType nodeType && nodeType != NodeType.UNKNOWN)
            {
                node.Text = LanguageManager.T("ProcessNew.NodeType." + nodeType);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(node.Name))
                    node.Name = GetToolboxCategoryKey(node.Text);

                node.Text = LanguageManager.T(node.Name);
            }

            foreach (TreeNode child in node.Nodes)
                ApplyToolboxNodeLanguage(child);
        }

        private string GetToolboxCategoryKey(string text)
        {
            switch (text)
            {
                case "图像采集":
                case "Image Acquisition":
                    return "ProcessNew.Category.ImageAcquisition";
                case "图像处理":
                case "Image Processing":
                    return "ProcessNew.Category.ImageProcessing";
                case "检测识别":
                case "Detection":
                    return "ProcessNew.Category.Detection";
                case "测量工具":
                case "Measurement Tools":
                    return "ProcessNew.Category.Measurement";
                case "图形创建":
                case "Geometry Creation":
                    return "ProcessNew.Category.GeometryCreation";
                case "设备通信":
                case "Device Communication":
                    return "ProcessNew.Category.Communication";
                case "逻辑工具":
                case "Logic Tools":
                    return "ProcessNew.Category.Logic";
                case "结果处理":
                case "Result Processing":
                    return "ProcessNew.Category.ResultProcessing";
                default:
                    return text;
            }
        }

        private void ConfigureToolboxLayout()
        {
            tableLayoutPanel1.ColumnStyles[0].Width = ToolboxExpandedWidth;
            treeView1.DrawMode = TreeViewDrawMode.OwnerDrawText;
            treeView1.DrawNode += treeView1_DrawNode;
            treeView1.Visible = false;
            ApplyToolboxCollapsedState(false);
        }

        private void ApplyToolboxCollapsedState(bool collapsed)
        {
            _toolboxCollapsed = collapsed;
            tableLayoutPanel1.SuspendLayout();
            panelToolbox.SuspendLayout();
            try
            {
                tableLayoutPanel1.ColumnStyles[0].Width = collapsed ? ToolboxCollapsedWidth : ToolboxExpandedWidth;
                if (collapsed)
                    HideToolboxFlyout();

                panelToolbox.Padding = new Padding(0);
                panelCategoryRail.Width = ToolboxCollapsedWidth;
                buttonToggleToolbox.Dock = DockStyle.Fill;
                buttonToggleToolbox.Text = "☰";
            }
            finally
            {
                panelToolbox.ResumeLayout();
                tableLayoutPanel1.ResumeLayout();
            }
        }

        private void buttonToggleToolbox_Click(object sender, EventArgs e)
        {
            HideToolboxFlyout();
        }

        private void ApplyCompactProcessActionText()
        {
            buttonAdd.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            buttonRemove.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            buttonAdd.Text = "+";
            buttonRemove.Text = "-";
            buttonAdd.TextAlign = ContentAlignment.MiddleCenter;
            buttonRemove.TextAlign = ContentAlignment.MiddleCenter;
            buttonAdd.Size = new Size(ProcessActionButtonWidth, tabControl1.ItemSize.Height);
            buttonRemove.Size = new Size(ProcessActionButtonWidth, tabControl1.ItemSize.Height);
            buttonRemove.Visible = true;
            flowLayoutPanelProcessActions.AutoSize = true;
            flowLayoutPanelProcessActions.WrapContents = false;
            panelProcessActions.AutoSize = false;
        }

        private void LayoutProcessActionStrip()
        {
            if (panelProcessActions == null || tabControl1 == null)
                return;

            Size actionContentSize = flowLayoutPanelProcessActions.GetPreferredSize(Size.Empty);
            if (actionContentSize.Width <= 0)
                actionContentSize.Width = ProcessAddButtonWidth;
            if (actionContentSize.Height <= 0)
                actionContentSize.Height = tabControl1.ItemSize.Height;

            int left = 0;
            if (tabControl1.TabPages.Count > 0 && tabControl1.IsHandleCreated)
                left = tabControl1.GetTabRect(tabControl1.TabPages.Count - 1).Right;
            else
                left = tabControl1.ItemSize.Width * Math.Max(1, tabControl1.TabPages.Count);

            int maxLeft = Math.Max(0, panelEditorSurface.ClientSize.Width - actionContentSize.Width);
            left = Math.Min(left, maxLeft);
            panelProcessActions.Location = new Point(left, 0);
            panelProcessActions.Size = new Size(
                Math.Max(actionContentSize.Width, panelEditorSurface.ClientSize.Width - left),
                tabControl1.ItemSize.Height);
            flowLayoutPanelProcessActions.Location = Point.Empty;
            panelProcessActions.BringToFront();
        }

        private void PositionToolboxFlyout()
        {
            int top = tabControl1.ItemSize.Height;
            int height = Math.Max(260, panelEditorSurface.ClientSize.Height - top - 18);
            panelModuleArea.Location = new Point(0, top);
            panelModuleArea.Size = new Size(ToolboxFlyoutWidth, height);
            panelModuleArea.BringToFront();
        }

        private void ShowToolboxFlyout(TreeNode categoryNode)
        {
            if (categoryNode == null)
                return;

            label1.Text = categoryNode.Text;
            BuildToolboxModules(categoryNode);
            PositionToolboxFlyout();
            panelModuleArea.Visible = true;
            panelModuleArea.BringToFront();
            LayoutProcessActionStrip();
        }

        private void HideToolboxFlyout()
        {
            if (panelModuleArea != null)
                panelModuleArea.Visible = false;
        }

        private void BuildToolboxNavigation()
        {
            if (flowLayoutPanelCategories == null ||
                flowLayoutPanelModules == null ||
                treeView1 == null)
            {
                return;
            }

            TreeNode preferredCategory = _selectedToolboxCategory;
            flowLayoutPanelCategories.SuspendLayout();
            try
            {
                flowLayoutPanelCategories.Controls.Clear();
                _toolboxCategoryButtons.Clear();

                foreach (TreeNode categoryNode in treeView1.Nodes)
                {
                    Button button = CreateToolboxCategoryButton(categoryNode);
                    flowLayoutPanelCategories.Controls.Add(button);
                    _toolboxCategoryButtons.Add(button);

                    if (preferredCategory == null)
                        preferredCategory = categoryNode;
                }
            }
            finally
            {
                flowLayoutPanelCategories.ResumeLayout();
            }

            if (preferredCategory != null && !treeView1.Nodes.Contains(preferredCategory))
                preferredCategory = null;

            _selectedToolboxCategory = preferredCategory;
            UpdateToolboxCategoryButtonStyles();
            HideToolboxFlyout();
        }

        private Button CreateToolboxCategoryButton(TreeNode categoryNode)
        {
            Button button = new Button
            {
                BackColor = Color.FromArgb(35, 38, 43),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 8F, FontStyle.Regular, GraphicsUnit.Point, 134),
                ForeColor = Color.FromArgb(194, 204, 214),
                Image = GetToolboxImage(categoryNode),
                ImageAlign = ContentAlignment.TopCenter,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(0, 5, 0, 3),
                Size = new Size(ToolboxCategoryButtonSize, ToolboxCategoryButtonSize + 8),
                Tag = categoryNode,
                Text = GetRailCategoryText(categoryNode.Text),
                TextAlign = ContentAlignment.BottomCenter,
                TextImageRelation = TextImageRelation.ImageAboveText,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = 0;
            button.Click += ToolboxCategoryButton_Click;
            return button;
        }

        private void ToolboxCategoryButton_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            TreeNode categoryNode = button == null ? null : button.Tag as TreeNode;
            if (ReferenceEquals(categoryNode, _selectedToolboxCategory) && panelModuleArea.Visible)
            {
                HideToolboxFlyout();
                return;
            }

            SelectToolboxCategory(categoryNode);
        }

        private void SelectToolboxCategory(TreeNode categoryNode)
        {
            if (categoryNode == null)
                return;

            _selectedToolboxCategory = categoryNode;
            UpdateToolboxCategoryButtonStyles();
            ShowToolboxFlyout(categoryNode);
        }

        private void UpdateToolboxCategoryButtonStyles()
        {
            foreach (Button button in _toolboxCategoryButtons)
            {
                bool selected = ReferenceEquals(button.Tag, _selectedToolboxCategory);
                button.BackColor = selected
                    ? Color.FromArgb(23, 185, 237)
                    : Color.FromArgb(35, 38, 43);
                button.ForeColor = selected
                    ? Color.White
                    : Color.FromArgb(194, 204, 214);
            }
        }

        private void BuildToolboxModules(TreeNode categoryNode)
        {
            flowLayoutPanelModules.SuspendLayout();
            try
            {
                flowLayoutPanelModules.Controls.Clear();
                if (categoryNode == null || categoryNode.Nodes.Count == 0)
                {
                    flowLayoutPanelModules.Controls.Add(CreateEmptyToolboxLabel());
                    return;
                }

                foreach (TreeNode moduleNode in categoryNode.Nodes)
                {
                    if (!(moduleNode.Tag is NodeType nodeType) || nodeType == NodeType.UNKNOWN)
                        continue;

                    flowLayoutPanelModules.Controls.Add(CreateToolboxModuleCard(moduleNode));
                }
            }
            finally
            {
                flowLayoutPanelModules.ResumeLayout();
            }
        }

        private Control CreateEmptyToolboxLabel()
        {
            return new Label
            {
                AutoSize = false,
                ForeColor = Color.FromArgb(150, 160, 170),
                Margin = new Padding(4, 12, 4, 4),
                Size = new Size(230, 40),
                Text = "暂无模块",
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private Control CreateToolboxModuleCard(TreeNode moduleNode)
        {
            Panel card = new Panel
            {
                BackColor = Color.FromArgb(32, 36, 40),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 4, 6, 8),
                Size = new Size(ToolboxModuleCardWidth, ToolboxModuleCardHeight),
                Tag = moduleNode
            };

            PictureBox icon = new PictureBox
            {
                Image = GetToolboxImage(moduleNode),
                Location = new Point((ToolboxModuleCardWidth - 30) / 2, 12),
                Size = new Size(30, 30),
                SizeMode = PictureBoxSizeMode.Zoom,
                Tag = moduleNode
            };

            Label title = new Label
            {
                AutoEllipsis = true,
                Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134),
                ForeColor = Color.FromArgb(224, 231, 239),
                Location = new Point(6, 50),
                Size = new Size(ToolboxModuleCardWidth - 12, 26),
                Tag = moduleNode,
                Text = moduleNode.Text,
                TextAlign = ContentAlignment.MiddleCenter
            };

            card.Controls.Add(icon);
            card.Controls.Add(title);
            AttachToolboxModuleEvents(card);
            AttachToolboxModuleEvents(icon);
            AttachToolboxModuleEvents(title);
            return card;
        }

        private void AttachToolboxModuleEvents(Control control)
        {
            control.MouseDown += ToolboxModule_MouseDown;
            control.MouseMove += ToolboxModule_MouseMove;
            control.MouseEnter += ToolboxModule_MouseEnter;
            control.MouseLeave += ToolboxModule_MouseLeave;
        }

        private void ToolboxModule_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            TreeNode node = GetToolboxNodeFromSender(sender);
            if (node == null || !(node.Tag is NodeType nodeType) || nodeType == NodeType.UNKNOWN)
                return;

            nodeToDrag = node;
            m_MouseDown = true;
        }

        private void ToolboxModule_MouseMove(object sender, MouseEventArgs e)
        {
            StartToolboxNodeDrag();
        }

        private void ToolboxModule_MouseEnter(object sender, EventArgs e)
        {
            Panel card = GetToolboxCardFromSender(sender);
            if (card != null)
                card.BackColor = Color.FromArgb(43, 49, 55);
        }

        private void ToolboxModule_MouseLeave(object sender, EventArgs e)
        {
            Panel card = GetToolboxCardFromSender(sender);
            if (card != null)
                card.BackColor = Color.FromArgb(32, 36, 40);
        }

        private TreeNode GetToolboxNodeFromSender(object sender)
        {
            Control control = sender as Control;
            while (control != null)
            {
                TreeNode node = control.Tag as TreeNode;
                if (node != null)
                    return node;

                control = control.Parent;
            }

            return null;
        }

        private Panel GetToolboxCardFromSender(object sender)
        {
            Control control = sender as Control;
            while (control != null)
            {
                if (control is Panel && control.Tag is TreeNode)
                    return (Panel)control;

                control = control.Parent;
            }

            return null;
        }

        private void StartToolboxNodeDrag()
        {
            if (!m_MouseDown || nodeToDrag == null || !(nodeToDrag.Tag is NodeType nodeType))
                return;
            if (nodeType == NodeType.UNKNOWN)
                return;

            DataObject dragData = new DataObject(DragDataFormat, new DragData(nodeToDrag.Text, nodeType));
            DoDragDrop(dragData, DragDropEffects.Move);
            m_MouseDown = false;
            HideToolboxFlyout();
        }

        private Image GetToolboxImage(TreeNode node)
        {
            if (node == null || imageList1.Images.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(node.ImageKey) && imageList1.Images.ContainsKey(node.ImageKey))
                return imageList1.Images[node.ImageKey];

            int imageIndex = Math.Max(0, Math.Min(node.ImageIndex, imageList1.Images.Count - 1));
            return imageList1.Images[imageIndex];
        }

        private string GetRailCategoryText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= 3)
                return text;

            if (text.Length == 4)
                return text.Substring(0, 2) + Environment.NewLine + text.Substring(2);

            return text.Substring(0, 2) + Environment.NewLine + text.Substring(2, Math.Min(2, text.Length - 2));
        }

        private void tabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabControl1.TabPages.Count)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            bool selected = e.Index == tabControl1.SelectedIndex;
            Rectangle bounds = e.Bounds;
            bounds.Inflate(1, 0);
            Color backColor = selected
                ? Color.FromArgb(50, 54, 58)
                : Color.FromArgb(38, 42, 46);
            Color textColor = selected
                ? Color.White
                : Color.FromArgb(218, 224, 230);

            using (SolidBrush brush = new SolidBrush(backColor))
                e.Graphics.FillRectangle(brush, bounds);

            using (Pen separator = new Pen(Color.FromArgb(24, 27, 30)))
            {
                e.Graphics.DrawLine(separator, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);
                e.Graphics.DrawLine(separator, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
            }

            Rectangle statusDot = new Rectangle(bounds.Left + 13, bounds.Top + ((bounds.Height - 11) / 2), 11, 11);
            using (SolidBrush dot = new SolidBrush(Color.FromArgb(232, 25, 31)))
                e.Graphics.FillEllipse(dot, statusDot);

            string displayText = GetProcessTabDisplayText(tabControl1.TabPages[e.Index].Text);
            Rectangle textBounds = new Rectangle(bounds.Left + 34, bounds.Top, bounds.Width - 42, bounds.Height);
            using (Font font = new Font(tabControl1.Font, selected ? FontStyle.Bold : FontStyle.Regular))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    displayText,
                    font,
                    textBounds,
                    textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static string GetProcessTabDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            int dotIndex = text.IndexOf('.');
            if (dotIndex >= 0 && dotIndex < text.Length - 1)
                return text.Substring(dotIndex + 1).Trim();

            return text;
        }

        private void treeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null)
                return;

            Rectangle bounds = new Rectangle(0, e.Bounds.Top, treeView1.ClientSize.Width, e.Bounds.Height);
            bool selected = (e.State & TreeNodeStates.Selected) == TreeNodeStates.Selected;
            bool category = !(e.Node.Tag is NodeType nodeType) || nodeType == NodeType.UNKNOWN;
            Color backColor = selected
                ? Color.FromArgb(64, 72, 82)
                : category
                    ? Color.FromArgb(42, 47, 52)
                    : treeView1.BackColor;
            Color textColor = selected
                ? Color.White
                : category
                    ? Color.FromArgb(91, 219, 213)
                    : Color.FromArgb(224, 231, 239);

            using (SolidBrush backgroundBrush = new SolidBrush(backColor))
                e.Graphics.FillRectangle(backgroundBrush, bounds);

            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                treeView1.Font,
                e.Bounds,
                textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (selected)
            {
                using (Pen pen = new Pen(Color.FromArgb(255, 132, 32), 2F))
                    e.Graphics.DrawLine(pen, bounds.Left + 1, bounds.Top + 4, bounds.Left + 1, bounds.Bottom - 4);
            }
        }

        private void ApplyGeneratedToolboxIcons()
        {
            imageList1.Images.Clear();
            imageList1.ImageSize = new Size(26, 26);
            imageList1.ColorDepth = ColorDepth.Depth32Bit;

            foreach (TreeNode node in treeView1.Nodes)
                ApplyGeneratedToolboxIcon(node);
        }

        private void ApplyGeneratedToolboxIcon(TreeNode node)
        {
            if (node == null)
                return;

            string key = GetToolboxIconKey(node);
            if (!imageList1.Images.ContainsKey(key))
                imageList1.Images.Add(key, CreateToolboxIcon(node));

            node.ImageKey = key;
            node.SelectedImageKey = key;

            foreach (TreeNode child in node.Nodes)
                ApplyGeneratedToolboxIcon(child);
        }

        private string GetToolboxIconKey(TreeNode node)
        {
            if (node.Tag is NodeType nodeType && nodeType != NodeType.UNKNOWN)
                return "node:" + nodeType;

            if (!string.IsNullOrWhiteSpace(node.Name))
                return "category:" + node.Name;

            return "category:" + node.Text;
        }

        private Bitmap CreateToolboxIcon(TreeNode node)
        {
            Bitmap bitmap = new Bitmap(26, 26);
            Color color = GetToolboxIconColor(node);
            string text = GetToolboxIconText(node);
            bool category = !(node.Tag is NodeType nodeType) || nodeType == NodeType.UNKNOWN;

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                graphics.Clear(Color.Transparent);

                Rectangle body = new Rectangle(2, 2, 22, 22);
                using (GraphicsPath path = CreateRoundRect(body, category ? 8 : 5))
                using (SolidBrush fill = new SolidBrush(color))
                {
                    graphics.FillPath(fill, path);
                }

                using (Pen shine = new Pen(Color.FromArgb(80, Color.White), 1F))
                    graphics.DrawLine(shine, body.Left + 4, body.Top + 4, body.Right - 5, body.Top + 4);

                float fontSize = text.Length > 2 ? 6.4F : 7.4F;
                using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold))
                using (SolidBrush brush = new SolidBrush(Color.White))
                using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.DrawString(text, font, brush, body, format);
                }
            }

            return bitmap;
        }

        private GraphicsPath CreateRoundRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Color GetToolboxIconColor(TreeNode node)
        {
            if (node.Tag is NodeType nodeType && nodeType != NodeType.UNKNOWN)
                return GetToolboxIconColor(nodeType);

            string key = node.Name ?? string.Empty;
            if (key.Contains("ImageAcquisition"))
                return Color.FromArgb(18, 132, 219);
            if (key.Contains("ImageProcessing"))
                return Color.FromArgb(77, 171, 247);
            if (key.Contains("Detection"))
                return Color.FromArgb(125, 84, 188);
            if (key.Contains("Measurement"))
                return Color.FromArgb(70, 166, 190);
            if (key.Contains("GeometryCreation"))
                return Color.FromArgb(64, 170, 112);
            if (key.Contains("Communication"))
                return Color.FromArgb(226, 140, 32);
            if (key.Contains("Logic"))
                return Color.FromArgb(134, 96, 220);
            if (key.Contains("ResultProcessing"))
                return Color.FromArgb(42, 157, 143);

            return Color.FromArgb(91, 219, 213);
        }

        private Color GetToolboxIconColor(NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.ImageSource:
                case NodeType.ImageSource3D:
                case NodeType.ImageShow:
                case NodeType.ImageShow3D:
                case NodeType.ImageCrop:
                case NodeType.ImageRotate:
                case NodeType.ImageSplit:
                case NodeType.ImagePreprocess:
                    return Color.FromArgb(18, 132, 219);
                case NodeType.AITD:
                case NodeType.LineFind:
                case NodeType.CircleFind:
                case NodeType.QRScan:
                case NodeType.MatchTemplate:
                case NodeType.BatteryEar:
                case NodeType.RGBDiscern:
                case NodeType.BinarizationAnalysis:
                case NodeType.UnsupervisedDetection:
                case NodeType.LargeModelDetection:
                    return Color.FromArgb(125, 84, 188);
                case NodeType.CaliperLine:
                case NodeType.CaliperCircle:
                case NodeType.CaliperEllipse:
                case NodeType.FindPoint:
                case NodeType.LineLineAngle:
                case NodeType.PointPointDistance:
                case NodeType.PointLineDistance:
                case NodeType.PointRegionDistance:
                    return Color.FromArgb(70, 166, 190);
                case NodeType.LineMergeFit:
                    return Color.FromArgb(64, 170, 112);
                case NodeType.LightSourceControl:
                case NodeType.CameraIO:
                case NodeType.CameraIOManual:
                case NodeType.ComSend:
                case NodeType.PLCRead:
                case NodeType.PLCWrite:
                case NodeType.WaitSoftTrigger:
                case NodeType.TCPClientRequest:
                case NodeType.TCPServerResponse:
                case NodeType.ModbusRead:
                case NodeType.ModbusWrite:
                case NodeType.ModbusSoftTrigger:
                case NodeType.ReadFlag:
                case NodeType.ERUIIO:
                    return Color.FromArgb(226, 140, 32);
                case NodeType.SharedVariable:
                case NodeType.ConditionRun:
                case NodeType.SleepTool:
                case NodeType.ProcessTrigger:
                case NodeType.WaitProcessComplete:
                case NodeType.ProcessSignal:
                case NodeType.If:
                case NodeType.MultiCondition:
                case NodeType.ArithmeticOperation:
                case NodeType.CompositeModule:
                case NodeType.CompositeInput:
                case NodeType.CompositeOutput:
                case NodeType.Else:
                case NodeType.EndIf:
                case NodeType.CSharpScript:
                case NodeType.MessageBox:
                    return Color.FromArgb(134, 96, 220);
                case NodeType.DrawAIResult:
                case NodeType.ResultOverlayDraw:
                case NodeType.ResultOverlayDraw2:
                case NodeType.AIResultSend:
                case NodeType.ImageSave:
                case NodeType.ImageFileDelete:
                case NodeType.Summarize:
                case NodeType.DetectResultShow:
                case NodeType.GenerateExcel:
                    return Color.FromArgb(42, 157, 143);
                default:
                    return Color.FromArgb(91, 219, 213);
            }
        }

        private string GetToolboxIconText(TreeNode node)
        {
            if (!(node.Tag is NodeType nodeType) || nodeType == NodeType.UNKNOWN)
                return GetCategoryIconText(node.Name);

            switch (nodeType)
            {
                case NodeType.ImageSource:
                    return "IN";
                case NodeType.ImageSource3D:
                    return "3D";
                case NodeType.ImageShow:
                    return "VW";
                case NodeType.ImageShow3D:
                    return "3V";
                case NodeType.ImageCrop:
                    return "CP";
                case NodeType.ImageRotate:
                    return "RT";
                case NodeType.ImageSplit:
                    return "SP";
                case NodeType.ImagePreprocess:
                    return "PR";
                case NodeType.AITD:
                    return "AI";
                case NodeType.LineFind:
                    return "LN";
                case NodeType.CircleFind:
                    return "CR";
                case NodeType.CaliperLine:
                    return "CL";
                case NodeType.CaliperCircle:
                    return "CC";
                case NodeType.CaliperEllipse:
                    return "CE";
                case NodeType.FindPoint:
                    return "FP";
                case NodeType.LineLineAngle:
                    return "LA";
                case NodeType.PointPointDistance:
                    return "PP";
                case NodeType.PointLineDistance:
                    return "PL";
                case NodeType.PointRegionDistance:
                    return "PR";
                case NodeType.LineMergeFit:
                    return "LM";
                case NodeType.QRScan:
                    return "QR";
                case NodeType.MatchTemplate:
                    return "MT";
                case NodeType.BatteryEar:
                    return "BE";
                case NodeType.RGBDiscern:
                    return "RGB";
                case NodeType.BinarizationAnalysis:
                    return "BW";
                case NodeType.UnsupervisedDetection:
                    return "US";
                case NodeType.LargeModelDetection:
                    return "LM";
                case NodeType.LightSourceControl:
                    return "LT";
                case NodeType.CameraIO:
                case NodeType.CameraIOManual:
                    return "IO";
                case NodeType.PLCRead:
                case NodeType.PLCWrite:
                case NodeType.WaitSoftTrigger:
                case NodeType.ReadFlag:
                    return "PL";
                case NodeType.TCPClientRequest:
                case NodeType.TCPServerResponse:
                    return "TCP";
                case NodeType.ModbusRead:
                case NodeType.ModbusWrite:
                case NodeType.ModbusSoftTrigger:
                    return "MB";
                case NodeType.ComSend:
                    return "COM";
                case NodeType.ERUIIO:
                    return "IO";
                case NodeType.SharedVariable:
                    return "SV";
                case NodeType.ConditionRun:
                    return "CN";
                case NodeType.SleepTool:
                    return "DL";
                case NodeType.ProcessTrigger:
                    return "TR";
                case NodeType.WaitProcessComplete:
                    return "WT";
                case NodeType.ProcessSignal:
                    return "SG";
                case NodeType.If:
                    return "IF";
                case NodeType.MultiCondition:
                    return "MC";
                case NodeType.ArithmeticOperation:
                    return "OP";
                case NodeType.CompositeModule:
                    return "CM";
                case NodeType.CompositeInput:
                    return "IN";
                case NodeType.CompositeOutput:
                    return "OUT";
                case NodeType.CSharpScript:
                    return "C#";
                case NodeType.MessageBox:
                    return "MSG";
                case NodeType.DrawAIResult:
                    return "DR";
                case NodeType.ResultOverlayDraw:
                    return "ROI";
                case NodeType.ResultOverlayDraw2:
                    return "ROI2";
                case NodeType.AIResultSend:
                    return "TX";
                case NodeType.ImageSave:
                    return "SV";
                case NodeType.ImageFileDelete:
                    return "DEL";
                case NodeType.Summarize:
                    return "SUM";
                case NodeType.DetectResultShow:
                    return "OK";
                case NodeType.GenerateExcel:
                    return "XL";
                default:
                    return "N";
            }
        }

        private string GetCategoryIconText(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "TB";
            if (key.Contains("ImageAcquisition"))
                return "CA";
            if (key.Contains("ImageProcessing"))
                return "IM";
            if (key.Contains("Detection"))
                return "AI";
            if (key.Contains("Measurement"))
                return "MS";
            if (key.Contains("GeometryCreation"))
                return "GC";
            if (key.Contains("Communication"))
                return "IO";
            if (key.Contains("Logic"))
                return "LG";
            if (key.Contains("ResultProcessing"))
                return "RS";

            return "TB";
        }

        /// <summary>
        /// 初始化算法工具箱
        /// </summary>
        private void Init()
        {
            string filePath = "ToolTreeView.xml";
            if (File.Exists(filePath))
            {
                try
                {
                    TreeViewSerializer.DeserializeTreeView(treeView1, filePath);
                    treeView1.ImageList = imageList1;
                    ApplyToolboxLanguage();
                    ApplyGeneratedToolboxIcons();
                    BuildToolboxNavigation();
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, LanguageManager.Format("ProcessNew.ToolboxDeserializeFailed", ex.Message), true);
                }
            }
            else
            {
                LogHelper.AddLog(MsgLevel.Exception, LanguageManager.T("ProcessNew.ToolboxMissing"), true);
            }
        }

        bool m_MouseDown = false;
        TreeNode nodeToDrag;
        private void treeView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 获取鼠标点击位置处的节点
                nodeToDrag = treeView1.GetNodeAt(e.X, e.Y);
                treeView1.SelectedNode = nodeToDrag;

                if (nodeToDrag != null &&
                    nodeToDrag.Tag is NodeType nodeType &&
                    nodeType != NodeType.UNKNOWN)
                {
                    m_MouseDown = true;
                }
            }
        }

        private void treeView1_MouseMove(object sender, MouseEventArgs e)
        {
            StartToolboxNodeDrag();
        }


        /// <summary>
        /// 拖拽的数据
        /// </summary>
        public struct DragData
        {
            public string Text;
            public NodeType NodeType;
            public DragData(string text, NodeType type)
            {
                Text = text;
                NodeType = type;
            }
        }

        /// <summary>
        /// 自定义的拖拽数据格式
        /// </summary>
        internal const string DragDataFormat = "DragData";

        /// <summary>
        /// 为了拖拽时实时显示效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (nodeToDrag == null)
                return;

            e.UseDefaultCursors = false;

            gCursor1.gText = nodeToDrag.Text;
            gCursor1.gEffect = gCursor.eEffect.Move;
            gCursor1.gImage = GetDragCursorImage(nodeToDrag);
            gCursor1.gImageBox = new Size(22, 22);
            gCursor1.gTextAlignment = ContentAlignment.TopLeft;
            gCursor1.gType = gCursor.eType.Both;
            gCursor1.MakeCursor();
            Cursor.Current = gCursor1.gCursor;
        }

        private Bitmap GetDragCursorImage(TreeNode node)
        {
            if (node == null || imageList1.Images.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(node.ImageKey) && imageList1.Images.ContainsKey(node.ImageKey))
                return new Bitmap(imageList1.Images[node.ImageKey]);

            int imageIndex = Math.Max(0, Math.Min(node.ImageIndex, imageList1.Images.Count - 1));
            return new Bitmap(imageList1.Images[imageIndex]);
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Deserialization(object sender, bool e)
        {
            try
            {
                // 清空流程控件
                tabControl1.Controls.Clear();

                // 根据加载的配置重新添加流程控件
                var processInfos = ConfigHelper.SolConfig.ProcessInfos.ToList();
                int startupNodeTotal = processInfos.Sum(processInfo => processInfo.NodeInfos?.Count ?? 0);
                int startupNodeOffset = 0;
                if (processInfos.Count == 0)
                    StartupProgressContext.ReportItem("正在恢复检测流程", "没有需要恢复的流程", 0, 0, 84, 95);
                for (int index = 0; index < processInfos.Count; index++)
                {
                    var processInfo = processInfos[index];
                    StartupProgressContext.ReportItem("正在恢复检测流程", processInfo.ProcessName, index + 1, processInfos.Count, 84, 86);
                    TabPage tabPage = new TabPage();
                    tabPage.Name = processInfo.ProcessName;
                    tabPage.Padding = new Padding(3);
                    tabPage.Size = new Size(465, 643);
                    tabPage.Text = $"{processInfo.ID}.{processInfo.ProcessName}";
                    tabPage.UseVisualStyleBackColor = true;

                    ProcessEditPanel nodeEditPanel = new ProcessEditPanel(
                        processInfo.ProcessName,
                        e,
                        processInfo,
                        startupNodeOffset,
                        startupNodeTotal);
                    startupNodeOffset += processInfo.NodeInfos?.Count ?? 0;
                    nodeEditPanel.Dock = DockStyle.Fill;
                    tabPage.Controls.Add(nodeEditPanel);
                    tabControl1.Controls.Add(tabPage);
                }

                LayoutProcessActionStrip();
            }
            catch (Exception ex)
            {
                StartupProgressContext.ReportFailure("恢复检测流程", ex);
                LogHelper.AddLog(MsgLevel.Exception, $"恢复检测流程失败：{ex}", true);
            }
        }

        /// <summary>
        /// 快捷键保存方案
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.S)
            {
                // 触发保存方案事件
                OnShotKeySavePressed?.Invoke(this, EventArgs.Empty);
            }
        }
        /// <summary>
        /// 添加一个流程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            Solution.Instance.ProcessCount++;
            TabPage tabPage = new TabPage();
            tabPage.Name = $"process{Solution.Instance.ProcessCount}";
            tabPage.Padding = new Padding(3);
            tabPage.Size = new System.Drawing.Size(465, 643);
            tabPage.Text = $"{Solution.Instance.ProcessCount}.{LanguageManager.Format("ProcessNew.DefaultProcessName", Solution.Instance.ProcessCount)}";
            tabPage.UseVisualStyleBackColor = true;

            ProcessEditPanel nodeEditPanel = new ProcessEditPanel(LanguageManager.Format("ProcessNew.DefaultProcessName", Solution.Instance.ProcessCount));
            nodeEditPanel.Dock = DockStyle.Fill;
            tabPage.Controls.Add(nodeEditPanel);
            tabControl1.Controls.Add(tabPage);
            tabControl1.SelectedTab = tabPage;
            LayoutProcessActionStrip();
        }

        /// <summary>
        /// 删除选中的流程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, System.EventArgs e)
        {
            if(tabControl1.Controls.Count == 0)
                return;
            var res = MessageBoxTD.Show(LanguageManager.T("ProcessNew.DeleteProcessConfirm"), LanguageManager.T("Common.Warning"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (res == DialogResult.No || res == DialogResult.None)
                return;

            TabPage tabPageToDelete = null;

            // 遍历所有选项卡，找到选中的选项卡
            foreach (Control control in tabControl1.Controls)
            {
                if (control is TabPage page && page == tabControl1.SelectedTab)
                {
                    tabPageToDelete = page;
                    break; // 找到后立即退出循环
                }
            }

            // 检查是否找到了要删除的选项卡，然后删除
            if (tabPageToDelete != null)
            {
                tabControl1.Controls.Remove(tabPageToDelete);
                Solution.Instance.RemoveProcess(tabPageToDelete.Text);
                LayoutProcessActionStrip();
            }
        }

        private void 全部展开ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.ExpandAll();
        }

        private void 全部折叠ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.CollapseAll();
        }
    }
}
