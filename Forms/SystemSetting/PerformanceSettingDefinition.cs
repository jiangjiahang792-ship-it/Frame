using System;
using System.Globalization;

namespace TDJS_Vision.Forms.SystemSetting
{
    /// <summary>
    /// 性能参数保存后对应的运行应用区域。
    /// </summary>
    internal enum PerformanceSettingApplyKind
    {
        /// <summary>CPU调度、CPU采样或当前档案可立即更新。</summary>
        CpuAndRuntime,

        /// <summary>每个图像窗口的显示帧率立即读取最新档案。</summary>
        UserInterface,

        /// <summary>图像大小采样容量只在没有活动流程时替换。</summary>
        ImageSample,

        /// <summary>保存工作池空闲时应用，繁忙时等待下一轮。</summary>
        ImageSave,

        /// <summary>运行停止读取最新值，但现有保存工作池可能等待下一轮重建。</summary>
        RuntimeAndImageSave,

        /// <summary>当前只参与档案计算和诊断，尚未形成内存硬控制。</summary>
        PlanningOnly
    }

    /// <summary>
    /// 系统设置表格中一个可编辑性能参数的稳定定义。
    /// </summary>
    internal sealed class PerformanceSettingDefinition
    {
        /// <summary>创建一项性能参数定义。</summary>
        public PerformanceSettingDefinition(
            string key,
            string category,
            string displayName,
            string rangeText,
            decimal minimum,
            decimal maximum,
            bool allowDecimal,
            PerformanceSettingApplyKind applyKind)
        {
            Key = key;
            Category = category;
            DisplayName = displayName;
            RangeText = rangeText;
            Minimum = minimum;
            Maximum = maximum;
            AllowDecimal = allowDecimal;
            ApplyKind = applyKind;
        }

        /// <summary>机器设置字典使用的稳定键。</summary>
        public string Key { get; }

        /// <summary>界面分组名称。</summary>
        public string Category { get; }

        /// <summary>简体中文参数名称。</summary>
        public string DisplayName { get; }

        /// <summary>界面显示的推荐范围。</summary>
        public string RangeText { get; }

        /// <summary>允许的最小输入值。</summary>
        public decimal Minimum { get; }

        /// <summary>允许的最大输入值。</summary>
        public decimal Maximum { get; }

        /// <summary>是否允许输入小数。</summary>
        public bool AllowDecimal { get; }

        /// <summary>参数对应的运行应用区域。</summary>
        public PerformanceSettingApplyKind ApplyKind { get; }

        /// <summary>
        /// 校验并转换为不受系统区域设置影响的保存文本。
        /// </summary>
        public bool TryNormalize(string text, decimal maximum, out string normalized, out string error)
        {
            decimal value;
            const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            bool parsed = decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out value) ||
                decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out value);
            if (!parsed)
            {
                normalized = null;
                error = DisplayName + "必须填写数字。";
                return false;
            }

            if (!AllowDecimal && value != decimal.Truncate(value))
            {
                normalized = null;
                error = DisplayName + "必须填写整数。";
                return false;
            }

            decimal normalizedMaximum = Math.Max(Minimum, maximum);
            if (value < Minimum || value > normalizedMaximum)
            {
                normalized = null;
                error = $"{DisplayName}必须在{Minimum:0.###}～{normalizedMaximum:0.###}之间。";
                return false;
            }

            normalized = value.ToString("0.###", CultureInfo.InvariantCulture);
            error = string.Empty;
            return true;
        }
    }
}
