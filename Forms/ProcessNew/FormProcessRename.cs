using System;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Forms.ProcessNew
{
    public partial class FormProcessRename : FormBase
    {
        Process process;
        public static event EventHandler<(string, string)> ProcessRenameChanged;
        public FormProcessRename()
        {
            InitializeComponent();
            BindLanguage();
            Shown += FormProcessRename_Shown;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(this, "ProcessNew.RenameProcess");
            LanguageManager.Bind(label1, "ProcessNew.NewProcessName");
            LanguageManager.Bind(button1, "Common.OK");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
        }

        private void FormProcessRename_Shown(object sender, EventArgs e)
        {
            textBox1.Text = this.process.ProcessName;
        }

        public void SetProcess(Process process)
        {
            this.process = process;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(textBox1.Text) && !textBox1.Text.Contains("."))
            {
                string oldName = this.process.ProcessName;
                this.process.ProcessName = textBox1.Text;
                ProcessRenameChanged.Invoke(this, (oldName, textBox1.Text));
                Close();
            }
            else
            {
                MessageBoxTD.Show(LanguageManager.T("ProcessNew.InvalidProcessName"));
            }
        }
    }
}
