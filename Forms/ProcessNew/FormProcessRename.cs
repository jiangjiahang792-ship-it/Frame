using System;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Forms.ProcessNew
{
    /// <summary>编辑完整流程名称，允许名称包含英文句点。</summary>
    public partial class FormProcessRename : FormBase
    {
        /// <summary>当前需要重命名的流程。</summary>
        Process process;
        /// <summary>通知流程选项卡同步旧名称和新名称。</summary>
        public static event EventHandler<(string, string)> ProcessRenameChanged;
        /// <summary>初始化重命名窗体及语言绑定。</summary>
        public FormProcessRename()
        {
            InitializeComponent();
            TDJS_Vision.UiLocalization.Attach(this);
            BindLanguage();
            Shown += FormProcessRename_Shown;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        /// <summary>绑定窗体控件的语言资源。</summary>
        private void BindLanguage()
        {
            LanguageManager.Bind(this, "ProcessNew.RenameProcess");
            LanguageManager.Bind(label1, "ProcessNew.NewProcessName");
            LanguageManager.Bind(button1, "Common.OK");
            ApplyLanguage();
        }

        /// <summary>切换语言时刷新显示文本。</summary>
        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        /// <summary>应用当前语言。</summary>
        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
        }

        /// <summary>打开时显示未经截断的完整流程名称。</summary>
        private void FormProcessRename_Shown(object sender, EventArgs e)
        {
            textBox1.Text = this.process.ProcessName;
        }

        /// <summary>设置当前编辑的流程。</summary>
        public void SetProcess(Process process)
        {
            this.process = process;
        }

        /// <summary>保存合法名称；句点属于名称内容，只拒绝空白名称。</summary>
        private void button1_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(textBox1.Text))
            {
                string oldName = this.process.ProcessName;
                this.process.ProcessName = textBox1.Text;
                ProcessRenameChanged?.Invoke(this, (oldName, textBox1.Text));
                Close();
            }
            else
            {
                MessageBoxTD.Show(LanguageManager.T("流程名称不能为空！"));
            }
        }
    }
}
