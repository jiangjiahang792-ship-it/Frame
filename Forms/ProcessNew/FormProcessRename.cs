using System;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.ProcessNew
{
    internal partial class FormProcessRename : Form
    {
        Process process;
        public static event EventHandler<(string, string)> ProcessRenameChanged;
        public FormProcessRename()
        {
            InitializeComponent();
        }

        public void SetProcess(Process process)
        {
            this.process = process;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(textBox1.Text))
            {
                string oldName = this.process.ProcessName;
                this.process.ProcessName = textBox1.Text;
                ProcessRenameChanged.Invoke(this, (oldName, textBox1.Text));
                Close();
            }
            else
            {
                MessageBox.Show("流程名称不能为空！");
            }
        }
    }
}
