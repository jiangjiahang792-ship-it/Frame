using System;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace TDJS_Vision.Forms.Login
{
    public partial class FormLogin : Form
    {
        public static EventHandler<UserRole> LoginEvent;
        public static EventHandler LogoutEvent;
        public FormLogin()
        {
            InitializeComponent();
            comboBox1.SelectedIndex = 2;//默认选择“操作员”
        }
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonLogin_Click(object sender, System.EventArgs e)
        {
            if(string.IsNullOrEmpty(textBoxPassword.Text))
            {
                MessageBox.Show("请输入密码！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            //工程师
            if (comboBox1.SelectedIndex == 0 && textBoxPassword.Text == "td666")
            {
                LoginEvent?.Invoke(this, UserRole.Engineer);
            }//管理员
            else if(comboBox1.SelectedIndex == 1 && textBoxPassword.Text == "888888")
            {
                LoginEvent?.Invoke(this, UserRole.Manager);
            }//操作员
            else if(comboBox1.SelectedIndex == 2 && textBoxPassword.Text == "666666")
            {
                LoginEvent?.Invoke(this, UserRole.Operator);
            }//密码错误
            else
            {
                MessageBox.Show("密码错误！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            comboBox1.Enabled = false;
            textBoxPassword.Enabled = false;
            buttonLogin.Enabled = false;
            buttonQuit.Enabled = true;
            this.Hide();
        }
        /// <summary>
        /// 退出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonQuit_Click(object sender, EventArgs e)
        {
            LogoutEvent?.Invoke(this, EventArgs.Empty);
            comboBox1.Enabled = true;
            textBoxPassword.Enabled = true;
            buttonLogin.Enabled = true;
            buttonQuit.Enabled = false;
            textBoxPassword.Text = string.Empty;
        }
    }
    /// <summary>
    /// 用户角色枚举
    /// </summary>
    public enum UserRole
    {
        Engineer, // 最高权限
        Manager,  // 中等权限
        Operator,  // 最低权限
        Unknown  // 未知权限
    }
}
