using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Startup;

namespace TDJS_Vision.Forms.YTMessageBox
{
    /// <summary>
    /// 自定义消息弹窗，统一软件内提示框样式。
    /// </summary>
    public partial class MessageBoxTD : FormBase
    {
        /// <summary>
        /// 用户点击后的对话框结果。
        /// </summary>
        private DialogResult result = DialogResult.None;

        /// <summary>
        /// 当前弹窗按钮类型。
        /// </summary>
        private MessageBoxButtons buttons;

        /// <summary>
        /// 初始化自定义消息弹窗。
        /// </summary>
        public MessageBoxTD()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 设置图标
        /// </summary>
        /// <param name="icon"></param>
        public void SetIcon(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.None:
                    pictureBox1.Visible = false;
                    tableLayoutPanel3.ColumnStyles[0].Width = 0; // 隐藏图标列
                    break;
                case MessageBoxIcon.Information:
                    pictureBox1.Image = imageList1.Images[0];
                    break;
                case MessageBoxIcon.Question:
                    pictureBox1.Image = imageList1.Images[1];
                    break;
                case MessageBoxIcon.Warning:
                    pictureBox1.Image = imageList1.Images[2];
                    break;
                case MessageBoxIcon.Error:
                    pictureBox1.Image = imageList1.Images[3];
                    break;
                default:
                    pictureBox1.Visible = false;
                    tableLayoutPanel3.ColumnStyles[0].Width = 0; // 隐藏图标列
                    break;
            }
        }

        public static DialogResult Show(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(null, message, title, buttons, icon);
        }

        /// <summary>
        /// 显示带 owner 的消息弹窗，避免二级模态窗体中弹框隐藏到父窗体后方。
        /// </summary>
        /// <param name="owner">弹窗所属窗口。</param>
        /// <param name="message">提示内容。</param>
        /// <param name="title">标题。</param>
        /// <param name="buttons">按钮类型。</param>
        /// <param name="icon">图标类型。</param>
        /// <returns>用户点击结果。</returns>
        public static DialogResult Show(IWin32Window owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (StartupDisplayMode.IsBackgroundAcceptance)
                return DialogResult.Cancel;

            using (var msgBox = new MessageBoxTD())
            {
                msgBox.Text = title; // 设置窗体标题
                msgBox.labelMessage.Text = message;

                // 设置按钮
                msgBox.SetButtons(buttons);
                msgBox.SetIcon(icon);
                if (owner != null)
                {
                    msgBox.StartPosition = FormStartPosition.CenterParent;
                    msgBox.ShowInTaskbar = false;
                    msgBox.ShowDialog(owner);
                }
                else
                {
                    msgBox.ShowDialog();
                }
                return msgBox.result;
            }
        }
        public static DialogResult Show(string message)
        {
            return Show(null, message);
        }

        /// <summary>
        /// 显示带 owner 的默认 OK 消息弹窗。
        /// </summary>
        /// <param name="owner">弹窗所属窗口。</param>
        /// <param name="message">提示内容。</param>
        /// <returns>用户点击结果。</returns>
        public static DialogResult Show(IWin32Window owner, string message)
        {
            if (StartupDisplayMode.IsBackgroundAcceptance)
                return DialogResult.Cancel;

            using (var msgBox = new MessageBoxTD())
            {
                msgBox.Text = "";
                msgBox.labelMessage.Text = message;
                msgBox.SetButtons(MessageBoxButtons.OK); // 默认按钮为 OK
                if (owner != null)
                {
                    msgBox.StartPosition = FormStartPosition.CenterParent;
                    msgBox.ShowInTaskbar = false;
                    msgBox.ShowDialog(owner);
                }
                else
                {
                    msgBox.ShowDialog();
                }
                return msgBox.result;
            }
        }

        /// <summary>
        /// 根据按钮类型构建弹窗按钮。
        /// </summary>
        /// <param name="buttons">按钮类型。</param>
        private void SetButtons(MessageBoxButtons buttons)
        {
            foreach (Control control in tableLayoutPanel2.Controls)
            {
                if(control is Button bt)
                {
                    tableLayoutPanel2.Controls.Remove(bt);
                }
            }
            this.buttons = buttons;
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    var btnOk = new Button { Text = "确定", Width = 80, DialogResult = DialogResult.Yes,
                        Anchor = AnchorStyles.None,
                        BackColor = Color.LightSeaGreen,
                        ForeColor = SystemColors.Control,
                        FlatStyle = FlatStyle.Flat,
                        TabStop = false
                    };
                    btnOk.Click += (s, e) => { result = DialogResult.OK; this.Close(); };
                    tableLayoutPanel2.Controls.Add(btnOk, 1, 1);
                    break;

                case MessageBoxButtons.OKCancel:
                    var btnCancel = new Button { Text = "取消", Width = 80, DialogResult = DialogResult.Cancel,
                        Anchor = AnchorStyles.None,
                        BackColor = Color.IndianRed,
                        ForeColor = SystemColors.Control,
                        FlatStyle = FlatStyle.Flat,
                        TabStop = false
                    };
                    var btnOk1 = new Button { Text = "确定", Width = 80, DialogResult = DialogResult.OK,
                        Anchor = AnchorStyles.None,
                        BackColor = Color.LightSeaGreen,
                        ForeColor = SystemColors.Control,
                        FlatStyle = FlatStyle.Flat,
                        TabStop = false
                    };
                    btnOk1.Click += (s, e) => { result = DialogResult.OK; this.Close(); };
                    btnCancel.Click += (s, e) => { result = DialogResult.Cancel; this.Close(); };
                    tableLayoutPanel2.Controls.Add(btnOk1, 1, 1);
                    tableLayoutPanel2.Controls.Add(btnCancel, 0, 1);
                    break;

                case MessageBoxButtons.YesNo:
                    var btnYes = new Button { Text = "是", Width = 80, DialogResult = DialogResult.Yes,
                        Anchor = AnchorStyles.None,
                        BackColor = Color.LightSeaGreen,
                        ForeColor = SystemColors.Control,
                        FlatStyle = FlatStyle.Flat,
                        TabStop = false
                    };
                    var btnNo = new Button { Text = "否", Width = 80, DialogResult = DialogResult.No,
                        Anchor = AnchorStyles.None,
                        BackColor = Color.IndianRed,
                        ForeColor = SystemColors.Control,
                        FlatStyle = FlatStyle.Flat,
                        TabStop = false
                    };
                    btnYes.Click += (s, e) => { result = DialogResult.Yes; this.Close(); };
                    btnNo.Click += (s, e) => { result = DialogResult.No; this.Close(); };
                    tableLayoutPanel2.Controls.Add(btnYes, 1, 1);
                    tableLayoutPanel2.Controls.Add(btnNo, 0, 1);
                    break;
                default:
                    var btnOk2 = new Button
                    {
                        Text = "确定",
                        Width = 80,
                        DialogResult = DialogResult.Yes,
                        Anchor = AnchorStyles.None,
                        BackColor = Color.LightSeaGreen,
                        ForeColor = SystemColors.Control,
                        FlatStyle = FlatStyle.Flat,
                        TabStop = false
                    };
                    btnOk2.Click += (s, e) => { result = DialogResult.OK; this.Close(); };
                    tableLayoutPanel2.Controls.Add(btnOk2, 1, 1);
                    break;
            }
        }

        internal static void Show(string v1, string v2, System.Windows.MessageBoxButton oK, MessageBoxIcon error)
        {
            throw new NotImplementedException();
        }
    }
}
