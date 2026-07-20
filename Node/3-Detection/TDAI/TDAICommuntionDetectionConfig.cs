using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Device;
using TDJS_Vision.Forms.FlowDirectionPanelContrls;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.ColorDiscern;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    public partial class TDAICommuntionDetectionConfig : FormBase
    {
        public BindingList<TDAICommuntionParam> TDAICommuntionParamsDb { get; set; }
        public string Adress { get; set; }

        /// <summary>
        /// 用于自动补齐检测项配置的默认模板名称。
        /// </summary>
        private readonly string _defaultTemplateName;

        /// <summary>
        /// 初始化AI通信检测项配置窗口。
        /// </summary>
        /// <param name="device">当前用于读取触发值的通信设备。</param>
        /// <param name="tDAICommuntionParams">通信触发值和检测项名称映射集合。</param>
        /// <param name="adress">通信读取地址。</param>
        /// <param name="defaultTemplateName">自动创建检测项时复制的默认模板名称。</param>
        public TDAICommuntionDetectionConfig(IDevice device, BindingList<TDAICommuntionParam> tDAICommuntionParams, string adress, string defaultTemplateName)
        {
            InitializeComponent();
            // 初始化界面文本
            label2.Text = device.UserDefinedName;
            textBox1.Text = adress;
            // 接收主窗体传入的数据
            this.TDAICommuntionParamsDb = tDAICommuntionParams ?? new BindingList<TDAICommuntionParam>();
            this.Adress = adress;
            _defaultTemplateName = defaultTemplateName;
            // 地址文本框变更事件
            textBox1.TextChanged += TextBox1_TextChanged;
            // ✅ 必加：拦截DataGridView数据异常弹窗（彻底屏蔽报错）
            dgvAIParam.DataError += DgvAIParam_DataError;
        }

        #region ✅ 基础事件：地址文本框变更、DataError异常拦截
        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            Adress = textBox1.Text;
        }

        /// <summary>
        /// 拦截所有DataGridView数据异常，杜绝弹窗
        /// </summary>
        private void DgvAIParam_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false; // 核心：禁止弹出系统错误框
        }
        #endregion

        private void TDAICommuntionDetectionConfig_Load(object sender, EventArgs e)
        {
            // DataGridView基础配置
            dgvAIParam.AutoGenerateColumns = false;
            dgvAIParam.AllowUserToAddRows = false;
            dgvAIParam.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvAIParam.Rows.Clear(); // 清空默认行

            NodeTDAI.EnsureCommunicationDetectItemConfigs(TDAICommuntionParamsDb, _defaultTemplateName);

            // 1. 手动创建【触发值】文本列
            DataGridViewTextBoxColumn colTriggerVal = new DataGridViewTextBoxColumn()
            {
                Name = "colTriggerVal",
                HeaderText = "触发值",
                Width = 120
            };
            dgvAIParam.Columns.Add(colTriggerVal);

            // 2. 手动创建【检测项名称】下拉列（核心配置）
            DataGridViewComboBoxColumn colDetection = new DataGridViewComboBoxColumn()
            {
                Name = "colDetection",
                HeaderText = "检测项名称",
                Width = 220,
                ValueType = typeof(string) // 强指定字符串类型
            };
            dgvAIParam.Columns.Add(colDetection);

            // 3. 加载下拉框数据源
            LoadComboBoxDataSource(colDetection);
            // 4. ✅ 核心：手动将传入的集合数据 → 赋值到DataGridView每行单元格
            LoadDataToDgvManually();
        }

        #region ✅ 核心方法1：加载下拉框数据源（从字典获取）
        private void LoadComboBoxDataSource(DataGridViewComboBoxColumn comboCol)
        {
            if (comboCol == null || Solution.Instance.DetectItemDic == null || Solution.Instance.DetectItemDic.Count == 0)
                return;

            comboCol.Items.Clear();
            // 遍历字典加载下拉项
            foreach (var pair in Solution.Instance.DetectItemDic)
            {
                comboCol.Items.Add(pair.Key);
            }
        }
        #endregion

        #region ✅ 核心方法2：纯手动赋值 → 集合数据 → DataGridView（彻底解绑BindingList）
        /// <summary>
        /// 手动遍历传入的List<TDAICommuntionParam>
        /// 逐行、逐单元格赋值到DataGridView，完全抛弃绑定模式
        /// </summary>
        private void LoadDataToDgvManually()
        {
            if (TDAICommuntionParamsDb == null || TDAICommuntionParamsDb.Count == 0) return;
            if (dgvAIParam.Columns.Count == 0) return;

            var comboCol = dgvAIParam.Columns["colDetection"] as DataGridViewComboBoxColumn;
            foreach (var model in TDAICommuntionParamsDb)
            {
                // 1. 手动新增一行
                int rowIndex = dgvAIParam.Rows.Add();
                var row = dgvAIParam.Rows[rowIndex];

                // 2. ✅ 手动给【触发值】单元格赋值
                row.Cells["colTriggerVal"].Value = model.TriggerVal ?? "";

                // 3. ✅ 手动给【下拉框】单元格赋值（精准回显原始值，核心逻辑）
                if (comboCol != null && comboCol.Items.Count > 0)
                {
                    if (!string.IsNullOrEmpty(model.DetectionName) && comboCol.Items.Contains(model.DetectionName))
                    {
                        // 原始值有效 → 赋值原始值，不覆盖
                        row.Cells["colDetection"].Value = model.DetectionName;
                    }
                    else
                    {
                        // 原始值为空/无效 → 兜底选下拉第0项
                        row.Cells["colDetection"].Value = comboCol.Items[0];
                    }
                }
            }
        }
        #endregion

        #region ✅ 按钮事件：新增行、删除行（纯手动操作）
        /// <summary>
        /// 新增行 → 纯手动创建行+赋值，不依赖任何绑定
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            var comboCol = dgvAIParam.Columns["colDetection"] as DataGridViewComboBoxColumn;
            string defaultDetName = "";

            // 新增行默认值：下拉框选第0项
            if (comboCol != null && comboCol.Items.Count > 0)
            {
                defaultDetName = comboCol.Items[0].ToString();
            }

            // ✅ 手动新增一行，并赋值默认值
            int newRowIndex = dgvAIParam.Rows.Add();
            var newRow = dgvAIParam.Rows[newRowIndex];
            newRow.Cells["colTriggerVal"].Value = "1"; // 触发值默认值
            newRow.Cells["colDetection"].Value = defaultDetName;
        }

        /// <summary>
        /// 删除行 → 纯手动删除选中行，带校验
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定需要删除该行吗?", "提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            if (dgvAIParam.SelectedRows.Count <= 0)
            {
                MessageBox.Show("请先选中要删除的行！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // ✅ 手动删除选中行（倒序删除，避免索引错乱）
            foreach (DataGridViewRow row in dgvAIParam.SelectedRows)
            {
                if (!row.IsNewRow)
                {
                    dgvAIParam.Rows.Remove(row);
                }
            }
        }
        #endregion

        #region ✅ 窗体关闭前：纯手动取值 → DataGridView → 组装为List集合（回传给主窗体）
        /// <summary>
        /// 重写窗体关闭事件，关闭前手动组装数据
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            // ✅ 核心：手动遍历DataGridView所有行 → 组装为新的List<TDAICommuntionParam>
            TDAICommuntionParamsDb = new BindingList<TDAICommuntionParam>();

            foreach (DataGridViewRow row in dgvAIParam.Rows)
            {
                if (row.IsNewRow) continue;

                // 手动获取每个单元格的值
                string triggerVal = row.Cells["colTriggerVal"].Value?.ToString() ?? "";
                string detName = row.Cells["colDetection"].Value?.ToString() ?? "";

                // 组装为实体对象，加入集合
                TDAICommuntionParamsDb.Add(new TDAICommuntionParam()
                {
                    TriggerVal = triggerVal,
                    DetectionName = detName
                });
            }
        }
        #endregion


        /// <summary>
        /// 导入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            if (openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    TDAICommuntionParamsDb = JsonProjectSerializer.LoadProject<BindingList<TDAICommuntionParam>>(openFileDialog2.FileName);

                    NodeTDAI.EnsureCommunicationDetectItemConfigs(TDAICommuntionParamsDb, _defaultTemplateName);
                    LoadComboBoxDataSource(dgvAIParam.Columns["colDetection"] as DataGridViewComboBoxColumn);
                    dgvAIParam.Rows.Clear();
                    LoadDataToDgvManually();

                    Close();
                }
                catch (Exception ex)
                {
                    MessageBoxTD.Show($"打开失败! 原因:{ex.Message}");
                }
            }
        }


        /// <summary>
        /// 导出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    JsonProjectSerializer.SaveProject<BindingList<TDAICommuntionParam>>(TDAICommuntionParamsDb, saveFileDialog1.FileName);
                }
                catch (Exception)
                {
                    MessageBoxTD.Show("保存失败！");
                    return;
                }
                MessageBoxTD.Show("保存成功！");
            }
        }
    }
}
