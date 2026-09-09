using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcRead
{
    /// <summary>保留原始结果，同时公开可供条件判定订阅的单个 PLC 值。</summary>
    public class NodeResultPlcRead : INodeResult, IDynamicResultVariables, IDynamicResultVariableTypeProvider
    {
        /// <summary>节点运行耗时，单位为毫秒。</summary>
        public int RunTime { get; set; }

        /// <summary>原始结果对象，兼容已有流程触发、消息显示和脚本订阅。</summary>
        [SubscriptionOutput]
        [DisplayName("读取结果")]
        public PlcReadResult ReadResult { get; set; }

        /// <summary>获取本次读取结果对应的变量名。</summary>
        public IEnumerable<string> GetDynamicVariableNames()
        {
            return PlcReadDynamicVariable.BuildNames(ReadResult?.Address, GetElementType()?.Name);
        }

        /// <summary>按变量路径取出单值；缺少结果或地址已变化时返回失败。</summary>
        public bool TryGetDynamicVariable(string variableName, out object value)
        {
            value = null;
            if (ReadResult?.Data == null)
                return false;
            int index = PlcReadDynamicVariable.ResolveIndex(ReadResult.Address, GetElementType()?.Name, variableName);
            if (index < 0)
                return false;
            if (ReadResult.Data is Array array)
            {
                if (index >= array.Length)
                    return false;
                value = array.GetValue(index);
            }
            else
            {
                if (index != 0)
                    return false;
                value = ReadResult.Data;
            }
            return true;
        }

        /// <summary>提供实际单值类型，不依赖旧结果里可能错误的类型字符串。</summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = GetElementType();
            return valueType != null &&
                PlcReadDynamicVariable.ResolveIndex(ReadResult?.Address, valueType.Name, variableName) >= 0;
        }

        /// <summary>获取标量类型或数组元素类型。</summary>
        private Type GetElementType()
        {
            var dataType = ReadResult?.Data?.GetType();
            return dataType != null && dataType.IsArray ? dataType.GetElementType() : dataType;
        }
    }

    /// <summary>PLC 读取结果，保留旧字段和构造方式。</summary>
    public class PlcReadResult
    {
        /// <summary>读取到的标量或数组。</summary>
        public object Data;

        /// <summary>实际数据的 CLR 类型名称。</summary>
        public string DataType;

        /// <summary>本次读取地址，多地址按原有规则使用连字符分隔。</summary>
        public string Address;

        /// <summary>兼容原有两参数结果构造方式。</summary>
        [Newtonsoft.Json.JsonConstructor]
        public PlcReadResult(object data, string dataType)
            : this(data, dataType, string.Empty)
        {
        }

        /// <summary>记录读取值及其地址，确保动态订阅与本次结果对应。</summary>
        public PlcReadResult(object data, string dataType, string address)
        {
            Data = data;
            DataType = data?.GetType().Name ?? dataType;
            Address = address;
        }
    }

    /// <summary>统一配置阶段和运行阶段的 PLC 单值订阅名称、地址和类型规则。</summary>
    internal static class PlcReadDynamicVariable
    {
        /// <summary>按读取顺序生成变量名，保留地址文本以支持不同厂商的地址格式。</summary>
        public static IEnumerable<string> BuildNames(string address, string dataType)
        {
            Type valueType = GetValueType(dataType);
            if (valueType == typeof(object) || string.IsNullOrWhiteSpace(address))
                return new string[0];
            string[] addresses = address.Split('-');
            if (addresses.Any(string.IsNullOrWhiteSpace) ||
                (addresses.Length > 1 && (valueType == typeof(float) || valueType == typeof(string))))
                return new string[0];

            int width = Math.Max(2, addresses.Length.ToString(CultureInfo.InvariantCulture).Length);
            var names = new string[addresses.Length];
            for (int index = 0; index < addresses.Length; index++)
            {
                string order = (index + 1).ToString("D" + width, CultureInfo.InvariantCulture);
                names[index] = $"值{order}(地址{addresses[index].Trim()})";
            }
            return names;
        }

        /// <summary>只匹配当前配置的完整变量名，防止修改地址后误用旧地址订阅。</summary>
        public static int ResolveIndex(string address, string dataType, string variableName)
        {
            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            int index = 0;
            foreach (string expected in BuildNames(address, dataType))
            {
                if (string.Equals(expected, name, StringComparison.Ordinal))
                    return index;
                index++;
            }
            return -1;
        }

        /// <summary>把参数保存的类型名映射为订阅目录使用的 CLR 类型。</summary>
        public static Type GetValueType(string dataType)
        {
            switch (dataType)
            {
                case "Boolean": return typeof(bool);
                case "Int16": return typeof(short);
                case "Int32": return typeof(int);
                case "Int64": return typeof(long);
                case "Single": return typeof(float);
                case "String": return typeof(string);
                default: return typeof(object);
            }
        }
    }
}
