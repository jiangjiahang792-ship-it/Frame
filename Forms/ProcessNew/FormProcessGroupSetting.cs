using System;

namespace TDJS_Vision.Forms.ProcessNew
{
    public partial class FormProcessGroupSetting : FormBase
    {
        private Process _process;
        public FormProcessGroupSetting(Process process)
        {
            InitializeComponent();
            BindLanguage();
            Shown += FormProcessGroupSetting_show;
            _process = process;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(this, "ProcessNew.SetGroup");
            LanguageManager.Bind(label1, "ProcessNew.Group");
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
            int selected = comboBox1.SelectedIndex;
            comboBox1.Items.Clear();
            for (int i = 1; i <= 6; i++)
                comboBox1.Items.Add(LanguageManager.Format("ProcessNew.GroupName", i));
            if (selected >= 0 && selected < comboBox1.Items.Count)
                comboBox1.SelectedIndex = selected;
        }

        private void FormProcessGroupSetting_show(object sender, EventArgs e)
        {
            switch (_process.Group)
            {
                case ProcessGroup.Group1:
                    comboBox1.SelectedIndex = 0;
                    break;
                case ProcessGroup.Group2:
                    comboBox1.SelectedIndex = 1;
                    break;
                case ProcessGroup.Group3:
                    comboBox1.SelectedIndex = 2;
                    break;
                case ProcessGroup.Group4:
                    comboBox1.SelectedIndex = 3;
                    break;
                case ProcessGroup.Group5:
                    comboBox1.SelectedIndex = 4;
                    break;
                case ProcessGroup.Group6:
                    comboBox1.SelectedIndex = 5;
                    break;
                default:
                    break;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            switch (comboBox1.SelectedIndex)
            {
                case 0:
                    _process.Group = ProcessGroup.Group1;
                    break;
                case 1:
                    _process.Group = ProcessGroup.Group2;
                    break;
                case 2:
                    _process.Group = ProcessGroup.Group3;
                    break;
                case 3:
                    _process.Group = ProcessGroup.Group4;
                    break;
                case 4:
                    _process.Group = ProcessGroup.Group5;
                    break;
                case 5:
                    _process.Group = ProcessGroup.Group6;
                    break;
            }
            Hide();
        }
    }
}
