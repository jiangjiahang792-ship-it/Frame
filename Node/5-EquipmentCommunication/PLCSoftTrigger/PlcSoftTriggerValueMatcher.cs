using System;
using System.Globalization;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger
{
    /// <summary>PLC软触发读取值与用户配置对比值的统一比较工具。</summary>
    public static class PlcSoftTriggerValueMatcher
    {
        /// <summary>判断读取值是否等于配置的触发对比值。</summary>
        /// <param name="actualValue">PLC实际读取值。</param>
        /// <param name="dataType">读取数据类型名称。</param>
        /// <param name="compareValue">用户配置的对比值文本。</param>
        /// <returns>相等时返回 true。</returns>
        public static bool IsMatch(object actualValue, string dataType, string compareValue)
        {
            if (string.IsNullOrWhiteSpace(dataType)) dataType = typeof(bool).Name;
            if (compareValue == null) compareValue = string.Empty;

            if (dataType == typeof(bool).Name)
                return Convert.ToBoolean(actualValue, CultureInfo.InvariantCulture) == ParseBool(compareValue);
            if (dataType == typeof(short).Name)
                return Convert.ToInt16(actualValue, CultureInfo.InvariantCulture) == short.Parse(compareValue, CultureInfo.InvariantCulture);
            if (dataType == typeof(int).Name)
                return Convert.ToInt32(actualValue, CultureInfo.InvariantCulture) == int.Parse(compareValue, CultureInfo.InvariantCulture);
            if (dataType == typeof(long).Name)
                return Convert.ToInt64(actualValue, CultureInfo.InvariantCulture) == long.Parse(compareValue, CultureInfo.InvariantCulture);
            if (dataType == typeof(float).Name)
                return Math.Abs(Convert.ToSingle(actualValue, CultureInfo.InvariantCulture) - float.Parse(compareValue, CultureInfo.InvariantCulture)) < 0.0001F;
            if (dataType == typeof(string).Name)
                return string.Equals(Convert.ToString(actualValue, CultureInfo.InvariantCulture)?.Trim(), compareValue, StringComparison.Ordinal);

            throw new InvalidOperationException($"PLC软触发不支持读取数据类型：{dataType}。");
        }

        /// <summary>验证并规范化对比值文本，保存前用于提前发现配置错误。</summary>
        /// <param name="dataType">读取数据类型名称。</param>
        /// <param name="compareValue">用户输入的对比值。</param>
        /// <returns>规范化后的对比值。</returns>
        public static string NormalizeCompareValue(string dataType, string compareValue)
        {
            if (string.IsNullOrWhiteSpace(dataType)) dataType = typeof(bool).Name;
            if (compareValue == null) compareValue = string.Empty;
            string text = compareValue.Trim();

            if (dataType == typeof(bool).Name)
                return ParseBool(text) ? "True" : "False";
            if (dataType == typeof(short).Name)
                return short.Parse(text, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (dataType == typeof(int).Name)
                return int.Parse(text, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (dataType == typeof(long).Name)
                return long.Parse(text, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (dataType == typeof(float).Name)
                return float.Parse(text, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (dataType == typeof(string).Name)
                return text;

            throw new InvalidOperationException($"PLC软触发不支持读取数据类型：{dataType}。");
        }

        /// <summary>解析布尔对比值，兼容 True/False、1/0、是/否。</summary>
        private static bool ParseBool(string value)
        {
            string text = (value ?? string.Empty).Trim();
            if (bool.TryParse(text, out bool boolValue)) return boolValue;
            if (text == "1" || text == "是" || text.Equals("ON", StringComparison.OrdinalIgnoreCase)) return true;
            if (text == "0" || text == "否" || text.Equals("OFF", StringComparison.OrdinalIgnoreCase)) return false;
            throw new FormatException("布尔对比值必须为 True/False、1/0、是/否 或 ON/OFF。");
        }
    }
}
