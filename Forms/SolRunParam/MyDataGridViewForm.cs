using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using Sunny.UI;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Forms.Login;

namespace TDJS_Vision.Forms.SolRunParam
{
    /// <summary>
    /// 运行参数表格行的数据来源。
    /// </summary>
    public enum RunParamGridSourceType
    {
        /// <summary>
        /// AI检测项配置字典中的检测项。
        /// </summary>
        DetectItem,

        /// <summary>
        /// 多条件判定节点中开启运行参数调整的条件。
        /// </summary>
        MultiCondition
    }

    /// <summary>
    /// 运行参数表格行，统一承载AI检测项和多条件上下限。
    /// </summary>
    public class RunParamGridItem
    {
        /// <summary>
        /// 表格行的数据来源类型。
        /// </summary>
        public RunParamGridSourceType SourceType { get; set; }

        /// <summary>
        /// 来源节点ID，多条件行用于保存时定位节点。
        /// </summary>
        public int SourceNodeId { get; set; }

        /// <summary>
        /// 来源节点对象，多条件行用于区分组合模块内部重复编号节点。
        /// </summary>
        public NodeBase SourceNode { get; set; }

        /// <summary>
        /// 来源条件索引，多条件行用于保存时定位条件。
        /// </summary>
        public int SourceIndex { get; set; }

        /// <summary>
        /// 表格中展示的名称，多条件行优先显示条件注释。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 表格复用的上下限、当前值和启用状态数据。
        /// </summary>
        public DetectItemInfo Info { get; set; }

        /// <summary>
        /// 从AI检测项创建运行参数表格行。
        /// </summary>
        /// <param name="info">AI检测项配置。</param>
        /// <returns>运行参数表格行。</returns>
        public static RunParamGridItem FromDetectItem(DetectItemInfo info)
        {
            return new RunParamGridItem
            {
                SourceType = RunParamGridSourceType.DetectItem,
                Info = CloneInfo(info)
            };
        }

        /// <summary>
        /// 复制检测项信息，避免表格编辑时直接改动原始集合。
        /// </summary>
        /// <param name="info">待复制的检测项信息。</param>
        /// <returns>复制后的检测项信息。</returns>
        private static DetectItemInfo CloneInfo(DetectItemInfo info)
        {
            if (info == null)
                return new DetectItemInfo();

            return new DetectItemInfo
            {
                Name = info.Name,
                MinValue = info.MinValue,
                MaxValue = info.MaxValue,
                Enable = info.Enable,
                IsCountItem = info.IsCountItem,
                IsManualTuningHidden = info.IsManualTuningHidden,
                Compensation = info.Compensation,
                PlugColor = info.PlugColor?.Clone(),
                CurValue = info.CurValue
            };
        }
    }

    public partial class MyDataGridViewForm : UserControl
    {
        /// <summary>
        /// 启用列索引。
        /// </summary>
        private const int EnableColumnIndex = 0;

        /// <summary>
        /// 名称列索引。
        /// </summary>
        private const int NameColumnIndex = 1;

        /// <summary>
        /// 下限列索引。
        /// </summary>
        private const int MinValueColumnIndex = 2;

        /// <summary>
        /// 当前值列索引。
        /// </summary>
        private const int CurrentValueColumnIndex = 3;

        /// <summary>
        /// 上限列索引。
        /// </summary>
        private const int MaxValueColumnIndex = 4;

        /// <summary>
        /// 数量型列索引。
        /// </summary>
        private const int CountItemColumnIndex = 6;
        /// <summary>是否对非厂家权限隐藏列索引。</summary>
        private const int HiddenColumnIndex = 7;
        /// <summary>补偿值位于倒数第二列。</summary>
        private const int CompensationColumnIndex = 5;

        public MyDataGridViewForm()
        {
            InitializeComponent();
            TDJS_Vision.UiLocalization.Attach(this);
            Init();
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            UserPermissionContext.RoleChanged += UserPermissionContext_RoleChanged;
            Disposed += (s, e) =>
            {
                LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
                UserPermissionContext.RoleChanged -= UserPermissionContext_RoleChanged;
            };
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
            RefreshDetectItemNames();
        }
        /// <summary>
        /// 初始化数据表格
        /// </summary>
        private void Init()
        {

            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            if (dataGridView1.Columns.Contains("Enable"))
                dataGridView1.Columns["Enable"].HeaderText = LanguageManager.T("SolRunParam.Enable");
            if (dataGridView1.Columns.Contains("Name"))
                dataGridView1.Columns["Name"].HeaderText = LanguageManager.T("SolRunParam.DetectItem");
            if (dataGridView1.Columns.Contains("MinValue"))
                dataGridView1.Columns["MinValue"].HeaderText = LanguageManager.T("SolRunParam.MinValue");
            if (dataGridView1.Columns.Contains("Value"))
                dataGridView1.Columns["Value"].HeaderText = LanguageManager.T("SolRunParam.CurrentValue");
            if (dataGridView1.Columns.Contains("MaxValue"))
                dataGridView1.Columns["MaxValue"].HeaderText = LanguageManager.T("SolRunParam.MaxValue");
            dataGridView1.Columns[CompensationColumnIndex].HeaderText = LanguageManager.T("补偿值");
            dataGridView1.Columns[CountItemColumnIndex].HeaderText = LanguageManager.T("SolRunParam.CountItem");
            dataGridView1.Columns[HiddenColumnIndex].HeaderText = LanguageManager.T("是否隐藏");
            ApplyPermissionVisibility();
        }

        /// <summary>登录角色变化后立即刷新厂家专属列和隐藏行。</summary>
        private void UserPermissionContext_RoleChanged(object sender, UserRole role)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action(ApplyPermissionVisibility));
                return;
            }

            ApplyPermissionVisibility();
        }

        /// <summary>仅厂家显示隐藏设置列；非厂家同时过滤已标记隐藏的参数行。</summary>
        private void ApplyPermissionVisibility()
        {
            bool isManufacturer = UserPermissionContext.CurrentRole == UserRole.Manufacturer;
            hiddenColumn.Visible = isManufacturer;
            hiddenColumn.ReadOnly = !isManufacturer;
            if (!isManufacturer && dataGridView1.CurrentRow?.Tag is RunParamGridItem currentItem &&
                currentItem.Info?.IsManualTuningHidden == true)
            {
                // 当前单元格所在行不能直接隐藏，先释放选中位置再批量应用可见性。
                dataGridView1.CurrentCell = null;
            }

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.IsNewRow) continue;
                RunParamGridItem item = row.Tag as RunParamGridItem;
                row.Visible = isManufacturer || item?.Info?.IsManualTuningHidden != true;
            }
        }

        private void RefreshDetectItemNames()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.IsNewRow)
                    continue;

                RunParamGridItem item = row.Tag as RunParamGridItem;
                if (item != null && item.SourceType == RunParamGridSourceType.MultiCondition)
                {
                    row.Cells[NameColumnIndex].Value = GetDisplayName(item);
                    continue;
                }

                var key = row.Cells[NameColumnIndex].Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(key))
                    row.Cells[NameColumnIndex].Value = DetectItemLanguage.GetDisplayName(key);
            }
        }
        /// <summary>
        /// 加载检测项
        /// </summary>
        /// <param name="info"></param>
        public void LoadConfig(List<DetectItemInfo> infos)
        {
            List<RunParamGridItem> items = new List<RunParamGridItem>();
            if (infos != null)
            {
                foreach (DetectItemInfo info in infos)
                    items.Add(RunParamGridItem.FromDetectItem(info));
            }

            LoadRunParamItems(items);
        }

        /// <summary>
        /// 加载运行参数表格行，保持与AI检测项一致的列显示。
        /// </summary>
        /// <param name="items">运行参数表格行集合。</param>
        public void LoadRunParamItems(List<RunParamGridItem> items)
        {
            dataGridView1.Rows.Clear();
            if (items == null) return;

            List<DetectItemInfo> detectItems = items
                .Where(item => item != null &&
                               item.SourceType == RunParamGridSourceType.DetectItem &&
                               item.Info != null)
                .Select(item => item.Info)
                .ToList();
            DetectItemLanguage.NormalizeItems(detectItems);

            foreach (RunParamGridItem item in items)
            {
                if (item == null || item.Info == null)
                    continue;

                DetectItemInfo info = item.Info;
                var row = new DataGridViewRow();
                row.CreateCells(dataGridView1);

                row.Tag = item;
                row.Cells[EnableColumnIndex].Value = info.Enable;
                row.Cells[NameColumnIndex].Value = GetDisplayName(item);
                row.Cells[NameColumnIndex].Tag = info.Name;
                row.Cells[MinValueColumnIndex].Value = info.MinValue;
                row.Cells[CurrentValueColumnIndex].Value = info.CurValue;
                row.Cells[MaxValueColumnIndex].Value = info.MaxValue;
                row.Cells[CountItemColumnIndex].Value = info.IsCountItem;
                row.Cells[CompensationColumnIndex].Value = info.Compensation;
                row.Cells[HiddenColumnIndex].Value = info.IsManualTuningHidden;
                row.Cells[CompensationColumnIndex].ReadOnly = item.SourceType == RunParamGridSourceType.MultiCondition;

                dataGridView1.Rows.Add(row);
            }

            ApplyPermissionVisibility();
        }

        /// <summary>
        /// 从表格中获取检测项配置
        /// </summary>
        /// <returns></returns>
        public List<DetectItemInfo> GetConfig()
        {
            List<RunParamGridItem> items = GetRunParamItems();
            if (items == null)
                return null;

            return items
                .Where(item => item.SourceType == RunParamGridSourceType.DetectItem)
                .Select(item => item.Info)
                .ToList();
        }

        /// <summary>
        /// 从表格中获取运行参数配置，保留每行来源信息。
        /// </summary>
        /// <returns>运行参数表格行集合。</returns>
        public List<RunParamGridItem> GetRunParamItems()
        {
            if (!dataGridView1.EndEdit()) return null;
            var items = new List<RunParamGridItem>();
            try
            {
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (row.IsNewRow) continue;
                    RunParamGridItem item = row.Tag as RunParamGridItem;
                    if (item == null)
                    {
                        item = new RunParamGridItem
                        {
                            SourceType = RunParamGridSourceType.DetectItem,
                            DisplayName = ToCellText(row.Cells[NameColumnIndex].Value)
                        };
                    }

                    var info = new DetectItemInfo
                    {
                        Enable = ToCellBool(row.Cells[EnableColumnIndex].Value),
                        Name = GetStoredName(row, item),
                        MinValue = ToCellText(row.Cells[MinValueColumnIndex].Value),
                        CurValue = ToCellText(row.Cells[CurrentValueColumnIndex].Value),
                        MaxValue = ToCellText(row.Cells[MaxValueColumnIndex].Value),
                        Compensation = ReadCompensation(row.Cells[CompensationColumnIndex].Value),
                        PlugColor = item.Info?.PlugColor?.Clone(),
                        IsCountItem = ToCellBool(row.Cells[CountItemColumnIndex].Value),
                        IsManualTuningHidden = UserPermissionContext.CurrentRole == UserRole.Manufacturer
                            ? ToCellBool(row.Cells[HiddenColumnIndex].Value)
                            : item.Info?.IsManualTuningHidden == true
                    };

                    item.Info = info;
                    if (item.SourceType == RunParamGridSourceType.MultiCondition &&
                        string.IsNullOrWhiteSpace(item.DisplayName))
                    {
                        item.DisplayName = ToCellText(row.Cells[NameColumnIndex].Value);
                    }

                    items.Add(item);
                }
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
                return null;
            }
            return items;
        }

        /// <summary>
        /// 获取表格行显示名称，AI检测项使用语言表，多条件项使用注释。
        /// </summary>
        /// <param name="item">运行参数表格行。</param>
        /// <returns>显示名称。</returns>
        private static string GetDisplayName(RunParamGridItem item)
        {
            if (item == null || item.Info == null)
                return string.Empty;

            if (item.SourceType == RunParamGridSourceType.MultiCondition)
                return LanguageManager.T(string.IsNullOrWhiteSpace(item.DisplayName) ? item.Info.Name : item.DisplayName);

            return DetectItemLanguage.GetDisplayName(item.Info.Name);
        }

        /// <summary>
        /// 获取保存用名称，AI检测项保留标准化键，多条件项保留隐藏行键。
        /// </summary>
        /// <param name="row">表格行。</param>
        /// <param name="item">运行参数表格行。</param>
        /// <returns>保存用名称。</returns>
        private static string GetStoredName(DataGridViewRow row, RunParamGridItem item)
        {
            string name = ToCellText(row.Cells[NameColumnIndex].Tag);
            if (string.IsNullOrWhiteSpace(name))
                name = ToCellText(row.Cells[NameColumnIndex].Value);

            if (item != null && item.SourceType == RunParamGridSourceType.MultiCondition)
            {
                if (item.Info != null && !string.IsNullOrWhiteSpace(item.Info.Name))
                    return item.Info.Name;

                return name;
            }

            return DetectItemLanguage.NormalizeName(name);
        }

        /// <summary>
        /// 转换表格单元格文本，统一处理空值。
        /// </summary>
        /// <param name="value">单元格原始值。</param>
        /// <returns>文本值。</returns>
        private static string ToCellText(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        /// <summary>空补偿按0处理，拒绝无效数字及非有限值，防止保存后污染检测结果。</summary>
        private static float ReadCompensation(object value)
        {
            string text = ToCellText(value);
            if (string.IsNullOrWhiteSpace(text)) return 0;
            if (!float.TryParse(text, out float result) || float.IsNaN(result) || float.IsInfinity(result))
                throw new InvalidOperationException("补偿值必须是有效的有限数字。");
            return result;
        }

        /// <summary>在编辑补偿值时即时提示无效输入，并阻止提交该单元格。</summary>
        private void ValidateCompensationCell(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != CompensationColumnIndex || e.RowIndex < 0) return;
            var row = dataGridView1.Rows[e.RowIndex];
            try
            {
                ReadCompensation(e.FormattedValue);
                row.ErrorText = string.Empty;
            }
            catch (InvalidOperationException)
            {
                e.Cancel = true;
                row.ErrorText = LanguageManager.T("补偿值必须是有效的有限数字。");
            }
        }

        /// <summary>
        /// 转换表格单元格布尔值，空值按false处理。
        /// </summary>
        /// <param name="value">单元格原始值。</param>
        /// <returns>布尔值。</returns>
        private static bool ToCellBool(object value)
        {
            if (value == null)
                return false;

            bool result;
            if (bool.TryParse(value.ToString(), out result))
                return result;

            return false;
        }
    }
}
