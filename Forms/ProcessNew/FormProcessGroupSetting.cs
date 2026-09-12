using System;

namespace TDJS_Vision.Forms.ProcessNew
{
    /// <summary>设置流程所属的独立调度组。</summary>
    public partial class FormProcessGroupSetting : FormBase
    {
        /// <summary>按枚举编号缓存全部组别，供显示、保存和回显共用。</summary>
        private static readonly ProcessGroup[] AvailableGroups = (ProcessGroup[])Enum.GetValues(typeof(ProcessGroup));

        /// <summary>当前需要设置组别的流程。</summary>
        private readonly Process _process;

        /// <summary>初始化组别设置窗口。</summary>
        /// <param name="process">需要修改组别的流程。</param>
        public FormProcessGroupSetting(Process process)
        {
            InitializeComponent();
            BindLanguage();
            Shown += FormProcessGroupSetting_show;
            _process = process;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        /// <summary>绑定既有界面语言资源。</summary>
        private void BindLanguage()
        {
            LanguageManager.Bind(this, "ProcessNew.SetGroup");
            LanguageManager.Bind(label1, "ProcessNew.Group");
            LanguageManager.Bind(button1, "Common.OK");
            ApplyLanguage();
        }

        /// <summary>切换语言时刷新显示，保留当前选中组别。</summary>
        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        /// <summary>按全部枚举值生成组别选项，避免新增组别后仍被界面数量限制。</summary>
        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
            int selected = comboBox1.SelectedIndex;
            comboBox1.Items.Clear();
            foreach (ProcessGroup group in AvailableGroups)
                comboBox1.Items.Add(LanguageManager.Format("ProcessNew.GroupName", (int)group + 1));
            if (selected >= 0 && selected < comboBox1.Items.Count)
                comboBox1.SelectedIndex = selected;
        }

        /// <summary>打开窗口时选回流程当前组别。</summary>
        private void FormProcessGroupSetting_show(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = Array.IndexOf(AvailableGroups, _process.Group);
        }

        /// <summary>保存选中的组别；未选中时不覆盖原配置。</summary>
        private void button1_Click(object sender, EventArgs e)
        {
            int selected = comboBox1.SelectedIndex;
            if (selected >= 0 && selected < AvailableGroups.Length)
                _process.Group = AvailableGroups[selected];
            Hide();
        }
    }
}
