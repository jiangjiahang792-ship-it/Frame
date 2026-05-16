using Logger;
using System;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.ProcessNew
{
    public partial class FormSetProcessLv : FormBase
    {
        private Process _process;
        public FormSetProcessLv(Process process)
        {
            InitializeComponent();
            BindLanguage();
            Shown += FormSetProcessLv_Shown;
            _process = process;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(this, "ProcessNew.SetPriority");
            LanguageManager.Bind(label1, "ProcessNew.Priority");
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
            for (int i = 1; i <= 5; i++)
                comboBox1.Items.Add(LanguageManager.Format("ProcessNew.LevelName", i));
            if (selected >= 0 && selected < comboBox1.Items.Count)
                comboBox1.SelectedIndex = selected;
        }

        private void FormSetProcessLv_Shown(object sender, EventArgs e)
        {
            switch (_process.RunLv)
            {
                case ProcessLvEnum.Lv1:
                    comboBox1.SelectedIndex = 0;
                    break;
                case ProcessLvEnum.Lv2:
                    comboBox1.SelectedIndex = 1;
                    break;
                case ProcessLvEnum.Lv3:
                    comboBox1.SelectedIndex = 2;
                    break;
                case ProcessLvEnum.Lv4:
                    comboBox1.SelectedIndex = 3;
                    break;
                case ProcessLvEnum.Lv5:
                    comboBox1.SelectedIndex = 4;
                    break;
                default:
                    break;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            switch(comboBox1.SelectedIndex)
            {
                case 0:
                    _process.RunLv = ProcessLvEnum.Lv1;
                    break;
                case 1:
                    _process.RunLv = ProcessLvEnum.Lv2;
                    break;
                case 2:
                    _process.RunLv = ProcessLvEnum.Lv3;
                    break;
                case 3:
                    _process.RunLv = ProcessLvEnum.Lv4;
                    break;
                case 4:
                    _process.RunLv = ProcessLvEnum.Lv5;
                    break;
            }
            Hide();
        }
    }
}
