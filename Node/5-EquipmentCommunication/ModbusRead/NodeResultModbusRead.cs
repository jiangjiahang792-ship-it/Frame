using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using TDJS_Vision.Device.Modbus;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead
{
    /// <summary>
    /// Modbus 读取节点运行结果。
    /// </summary>
    public class NodeResultModbusRead : INodeResult, IDynamicResultVariables, IDynamicResultVariableTypeProvider
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// Modbus 原始读取结果。
        /// </summary>
        [DisplayName("读取结果")]
        public ModbusReadResult ReadData { get; set; }

        /// <summary>
        /// 读取到的值数量。
        /// </summary>
        [DisplayName("读取数量")]
        public int ValueCount
        {
            get { return ModbusReadDynamicVariable.GetValueCount(ReadData == null ? null : ReadData.Data); }
        }

        /// <summary>
        /// 获取当前结果中可订阅的每个 Modbus 值名称。
        /// </summary>
        public IEnumerable<string> GetDynamicVariableNames()
        {
            if (ReadData == null)
                return new string[0];

            return ModbusReadDynamicVariable.BuildNames(
                ReadData.StartAddress,
                ValueCount);
        }

        /// <summary>
        /// 尝试按动态变量名读取单个 Modbus 值。
        /// </summary>
        public bool TryGetDynamicVariable(string variableName, out object value)
        {
            return ModbusReadDynamicVariable.TryGetValue(
                ReadData == null ? null : ReadData.Data,
                ReadData == null ? null : ReadData.StartAddress,
                variableName,
                out value);
        }

        /// <summary>
        /// 尝试获取单个 Modbus 值的真实类型。
        /// </summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = ModbusReadDynamicVariable.GetValueType(ReadData);
            return ReadData != null &&
                ModbusReadDynamicVariable.ContainsVariable(ReadData.StartAddress, ValueCount, variableName);
        }
    }

    /// <summary>
    /// Modbus 读取结果类。
    /// </summary>
    public class ModbusReadResult
    {
        /// <summary>
        /// 读取到的数据数组。
        /// </summary>
        public object Data;

        /// <summary>
        /// 数据数组类型名称。
        /// </summary>
        public string DataType;

        /// <summary>
        /// 本次读取的起始地址。
        /// </summary>
        public string StartAddress;

        /// <summary>
        /// 创建读取结果，兼容旧的两参数调用。
        /// </summary>
        public ModbusReadResult(object data, string dataType)
            : this(data, dataType, string.Empty)
        {
        }

        /// <summary>
        /// 创建读取结果。
        /// </summary>
        public ModbusReadResult(object data, string dataType, string startAddress)
        {
            Data = data;
            DataType = dataType;
            StartAddress = startAddress;
        }
    }

    /// <summary>
    /// Modbus 读取值动态变量命名和取值工具。
    /// </summary>
    internal static class ModbusReadDynamicVariable
    {
        /// <summary>
        /// 根据起始地址和读取数量生成可订阅变量名。
        /// </summary>
        public static IEnumerable<string> BuildNames(string startAddress, int count)
        {
            List<string> names = new List<string>();
            if (count <= 0)
                return names;

            int width = Math.Max(2, count.ToString(CultureInfo.InvariantCulture).Length);
            int firstAddress;
            bool hasAddress = TryParseAddress(startAddress, out firstAddress);
            for (int index = 0; index < count; index++)
            {
                string orderText = (index + 1).ToString("D" + width, CultureInfo.InvariantCulture);
                if (hasAddress)
                    names.Add($"值{orderText}(地址{firstAddress + index})");
                else
                    names.Add($"值{orderText}");
            }

            return names;
        }

        /// <summary>
        /// 判断变量名是否属于当前读取结果。
        /// </summary>
        public static bool ContainsVariable(string startAddress, int count, string variableName)
        {
            int index;
            return TryResolveIndex(startAddress, count, variableName, out index);
        }

        /// <summary>
        /// 从数组中读取指定变量名对应的值。
        /// </summary>
        public static bool TryGetValue(object data, string startAddress, string variableName, out object value)
        {
            value = null;
            Array array = data as Array;
            if (array == null)
                return false;

            int index;
            if (!TryResolveIndex(startAddress, array.Length, variableName, out index))
                return false;

            value = array.GetValue(index);
            return true;
        }

        /// <summary>
        /// 获取数组元素数量。
        /// </summary>
        public static int GetValueCount(object data)
        {
            Array array = data as Array;
            return array == null ? 0 : array.Length;
        }

        /// <summary>
        /// 根据节点参数获取值类型。
        /// </summary>
        public static Type GetValueType(RegistersType dataType)
        {
            switch (dataType)
            {
                case RegistersType.Bool:
                case RegistersType.线圈:
                case RegistersType.离散输入:
                    return typeof(bool);
                case RegistersType.Short:
                    return typeof(short);
                case RegistersType.UShort:
                    return typeof(ushort);
                case RegistersType.Int:
                    return typeof(int);
                case RegistersType.UInt:
                    return typeof(uint);
                case RegistersType.Float:
                    return typeof(float);
                case RegistersType.Double:
                    return typeof(double);
                case RegistersType.Long:
                    return typeof(long);
                case RegistersType.ULong:
                    return typeof(ulong);
                default:
                    return typeof(object);
            }
        }

        /// <summary>
        /// 根据运行结果获取值类型。
        /// </summary>
        public static Type GetValueType(ModbusReadResult readData)
        {
            Array array = readData == null ? null : readData.Data as Array;
            if (array != null)
                return array.GetType().GetElementType() ?? typeof(object);

            return typeof(object);
        }

        /// <summary>
        /// 根据节点参数获取数组类型名称。
        /// </summary>
        public static string GetArrayTypeName(RegistersType dataType)
        {
            return GetValueType(dataType).MakeArrayType().Name;
        }

        /// <summary>
        /// 将变量名解析为数组索引。
        /// </summary>
        private static bool TryResolveIndex(string startAddress, int count, string variableName, out int index)
        {
            index = -1;
            if (count <= 0 || string.IsNullOrWhiteSpace(variableName))
                return false;

            string name = DynamicResultVariableResolver.ExtractVariableName(variableName).Trim();
            int valueOrder;
            if (TryParsePrefixedNumber(name, "值", out valueOrder))
            {
                index = valueOrder - 1;
                return index >= 0 && index < count;
            }

            int firstAddress;
            int address;
            if (TryParseAddress(startAddress, out firstAddress) &&
                TryParsePrefixedNumber(name, "地址", out address))
            {
                index = address - firstAddress;
                return index >= 0 && index < count;
            }

            int compareIndex = 0;
            foreach (string expectedName in BuildNames(startAddress, count))
            {
                if (string.Equals(expectedName, name, StringComparison.OrdinalIgnoreCase))
                {
                    index = compareIndex;
                    return true;
                }

                compareIndex++;
            }

            return false;
        }

        /// <summary>
        /// 从“值01(地址1)”或“地址1”这类名称中提取数字。
        /// </summary>
        private static bool TryParsePrefixedNumber(string text, string prefix, out int number)
        {
            number = 0;
            if (string.IsNullOrWhiteSpace(text) ||
                !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int start = prefix.Length;
            int end = start;
            while (end < text.Length && char.IsDigit(text[end]))
                end++;

            if (end == start)
                return false;

            return int.TryParse(text.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>
        /// 解析 Modbus 起始地址。
        /// </summary>
        private static bool TryParseAddress(string text, out int address)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out address) ||
                int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out address);
        }
    }
}
