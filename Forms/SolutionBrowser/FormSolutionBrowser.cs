using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.SolutionBrowser
{
    /// <summary>通过名称和路径选择已保存方案，不在列表中加载方案内容。</summary>
    public partial class FormSolutionBrowser : Form
    {
        /// <summary>可替换的方案目录服务。</summary>
        private readonly ISolutionCatalog catalog;
        /// <summary>额外查找的目录快照。</summary>
        private readonly string[] directories;
        /// <summary>当前元数据快照，搜索时不重复访问磁盘。</summary>
        private IReadOnlyList<SolutionCatalogEntry> entries = new List<SolutionCatalogEntry>();
        /// <summary>用户确认打开的完整路径。</summary>
        public string SelectedSolutionPath { get; private set; }

        /// <summary>为窗体设计器提供默认构造入口。</summary>
        public FormSolutionBrowser() : this(FileSolutionCatalog.Default, new[] { FileSolutionCatalog.DefaultDirectory }) { }

        /// <summary>注入目录服务及本次需要发现的目录。</summary>
        public FormSolutionBrowser(ISolutionCatalog catalog, IEnumerable<string> directories)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.directories = directories.ToArray();
            InitializeComponent();
            browseDialog.InitialDirectory = FileSolutionCatalog.DefaultDirectory;
            UiLocalization.Attach(this);
        }

        /// <summary>首次显示后后台读取元数据，避免磁盘读取阻塞主界面。</summary>
        private async void FormSolutionBrowser_Shown(object sender, EventArgs e) { await RefreshEntriesAsync(); }

        /// <summary>重新读取目录，等待期间关闭窗体也不再访问已释放控件。</summary>
        private async Task RefreshEntriesAsync()
        {
            refreshButton.Enabled = false;
            statusLabel.Text = LanguageManager.T("正在读取方案列表…");
            try
            {
                var result = await Task.Run(() => catalog.Read(directories));
                if (IsDisposed || Disposing) return;
                entries = result;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing) statusLabel.Text = LanguageManager.T("方案列表读取失败：") + ex.Message;
            }
            finally { if (!IsDisposed && !Disposing) refreshButton.Enabled = true; }
        }

        /// <summary>按名称或路径进行不区分大小写的内存搜索。</summary>
        private void ApplyFilter()
        {
            string query = searchBox.Text.Trim();
            var matches = entries.Where(p => p.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || p.FullPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            solutionGrid.DataSource = matches;
            openButton.Enabled = matches.Count > 0;
            statusLabel.Text = matches.Count == 0 ? LanguageManager.T("没有找到方案，可点击“浏览其他方案”添加。")
                : string.Format(LanguageManager.T("共 {0} 个方案，双击即可打开。"), matches.Count);
        }

        /// <summary>搜索框变化时即时过滤。</summary>
        private void SearchBox_TextChanged(object sender, EventArgs e) { ApplyFilter(); }
        /// <summary>用户主动刷新磁盘上的方案列表。</summary>
        private async void RefreshButton_Click(object sender, EventArgs e) { await RefreshEntriesAsync(); }
        /// <summary>确认当前行对应的方案。</summary>
        private void OpenButton_Click(object sender, EventArgs e)
        {
            var entry = solutionGrid.CurrentRow?.DataBoundItem as SolutionCatalogEntry;
            if (entry != null) SelectSolution(entry.FullPath);
        }
        /// <summary>双击数据行时直接确认，忽略表头。</summary>
        private void SolutionGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        { if (e.RowIndex >= 0) OpenButton_Click(sender, EventArgs.Empty); }

        /// <summary>只在用户明确选择其他方案时打开系统文件浏览器。</summary>
        private void BrowseButton_Click(object sender, EventArgs e)
        {
            if (browseDialog.ShowDialog(this) == DialogResult.OK) SelectSolution(browseDialog.FileName);
        }

        /// <summary>提交前再次检查文件存在，取消或失效时不触发方案切换。</summary>
        private void SelectSolution(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(this, LanguageManager.T("方案文件已被移动或删除，请刷新列表或浏览其他方案。"),
                    LanguageManager.T("打开方案"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SelectedSolutionPath = path;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
