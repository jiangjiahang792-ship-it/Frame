using System;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Forms.Login
{
    public partial class FormLogin : FormBase
    {
        public static EventHandler<UserRole> LoginEvent;
        public static EventHandler LogoutEvent;
        public FormLogin()
        {
            InitializeComponent();
            comboBox1.SelectedIndex = 3;//默认选择初级权限。
        }
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonLogin_Click(object sender, System.EventArgs e)
        {
            if (string.IsNullOrEmpty(textBoxPassword.Text))
            {
                MessageBoxTD.Show("请输入密码！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 厂家拥有最高权限，额外开放窗口布局操作。
            if (comboBox1.SelectedIndex == 0 && textBoxPassword.Text == "tdjs")
            {
                UserPermissionContext.UpdateRole(UserRole.Manufacturer);
                LoginEvent?.Invoke(this, UserRole.Manufacturer);
            }
            else if (comboBox1.SelectedIndex == 1 && textBoxPassword.Text == "1111")
            {
                UserPermissionContext.UpdateRole(UserRole.Hight);
                LoginEvent?.Invoke(this, UserRole.Hight);
            }//中级
            else if (comboBox1.SelectedIndex == 2 && textBoxPassword.Text == "888")
            {
                UserPermissionContext.UpdateRole(UserRole.Medium);
                LoginEvent?.Invoke(this, UserRole.Medium);
            }//初级
            else if (comboBox1.SelectedIndex == 3 && textBoxPassword.Text == "666")
            {
                UserPermissionContext.UpdateRole(UserRole.Low);
                LoginEvent?.Invoke(this, UserRole.Low);
            }//密码错误
            else
            {
                MessageBoxTD.Show("密码错误！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            comboBox1.Enabled = false;
            textBoxPassword.Enabled = false;
            buttonLogin.Enabled = false;
            buttonQuit.Enabled = true;
            this.Hide();
        }

        /// <summary>
        /// 登录角色测试
        /// </summary>
        /// <param name="role"></param>
        public static void LoginTest(UserRole role)
        {
            UserPermissionContext.UpdateRole(role);
            LoginEvent?.Invoke(null, role);
        }
        /// <summary>
        /// 退出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonQuit_Click(object sender, EventArgs e)
        {
            UserPermissionContext.UpdateRole(UserRole.Unknown);
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
        /// <summary>高级权限，保留原有高级功能但不可修改布局。</summary>
        Hight = 0,
        /// <summary>中级权限。</summary>
        Medium = 1,
        /// <summary>初级权限。</summary>
        Low = 2,
        /// <summary>未登录或未知权限。</summary>
        Unknown = 3,
        /// <summary>厂家最高权限；追加枚举值以兼容原有角色数值。</summary>
        Manufacturer = 4
    }
}
