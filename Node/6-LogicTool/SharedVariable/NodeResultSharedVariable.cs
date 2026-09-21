using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;

namespace TDJS_Vision.Node._6_LogicTool.SharedVariable
{
    /// <summary>保留共享变量包装输出，并向基础值订阅方提供解包后的实际值。</summary>
    public class NodeResultSharedVariable : INodeResult, IDynamicResultVariables, IDynamicResultVariableTypeProvider
    {
        /// <summary>实际值使用固定路径，变量名称变化时下游无需重建订阅。</summary>
        public const string ActualValueName = "共享变量值";

        /// <summary>节点运行耗时，单位为毫秒。</summary>
        public int RunTime { get; set; }

        /// <summary>原有包装输出，兼容图像、脚本和旧方案订阅。</summary>
        [SubscriptionOutput]
        [DisplayName("读到的共享变量")]
        public SharedVarValue Value { get; set; }

        /// <summary>获取已读取的实际值输出名称。</summary>
        public IEnumerable<string> GetDynamicVariableNames()
        {
            return Value == null ? new string[0] : new[] { ActualValueName };
        }

        /// <summary>读取当前节点的结果快照，避免绕过节点直接读取不断变化的全局变量。</summary>
        public bool TryGetDynamicVariable(string variableName, out object value)
        {
            value = null;
            if (!IsActualValue(variableName) || Value == null) return false;
            Type valueType;
            return TryUnwrap(Value, out value, out valueType);
        }

        /// <summary>根据当前快照提供真实类型，保留字符串前导零等原始内容。</summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = null;
            object value;
            return IsActualValue(variableName) && TryUnwrap(Value, out value, out valueType);
        }

        /// <summary>匹配实际值名称、内部路径或界面显示名称。</summary>
        internal static bool IsActualValue(string variableName)
        {
            return string.Equals(DynamicResultVariableResolver.ExtractVariableName(variableName),
                ActualValueName, StringComparison.Ordinal);
        }

        /// <summary>获取数组或泛型列表的元素类型，配置推导与运行时空元素使用同一规则。</summary>
        internal static Type GetElementType(Type valueType)
        {
            if (valueType == null) return null;
            if (valueType.IsArray) return valueType.GetElementType();
            Type listType = valueType.GetInterfaces().Concat(new[] { valueType })
                .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>));
            return listType?.GetGenericArguments()[0];
        }

        /// <summary>先解包再按索引取值，支持共享变量转存的数组，并保留空元素的类型。</summary>
        internal static SharedVarValue GetIndexedValue(SharedVarValue wrapped, int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), "索引不能为负数。");
            object data;
            Type valueType;
            if (!TryUnwrap(wrapped, out data, out valueType) || data == null)
                throw new InvalidOperationException("共享变量为空或包含循环引用，无法按索引读取。");
            object element;
            if (data is Array array)
            {
                if (array.Rank != 1) throw new InvalidOperationException("仅支持一维数组索引。");
                if (index >= array.Length) throw new IndexOutOfRangeException("共享变量数组索引超出范围。");
                element = array.GetValue(index + array.GetLowerBound(0));
            }
            else if (data is IList list)
            {
                if (index >= list.Count) throw new IndexOutOfRangeException("共享变量列表索引超出范围。");
                element = list[index];
            }
            else
                throw new InvalidOperationException("共享变量不是数组或列表，无法按索引读取。");
            return new SharedVarValue(element, element?.GetType() ?? GetElementType(valueType));
        }

        /// <summary>解开共享变量和PLC整包读取结果，保留旧包装输出并拒绝循环引用。</summary>
        internal static bool TryUnwrap(SharedVarValue wrapped, out object value, out Type valueType)
        {
            value = null;
            valueType = null;
            if (wrapped == null) return false;
            object current = wrapped;
            HashSet<object> visited = null;
            while (current is SharedVarValue || current is PlcReadResult)
            {
                object next;
                if (current is SharedVarValue shared)
                {
                    next = shared.Data;
                    valueType = shared.Type;
                }
                else
                {
                    var plcResult = (PlcReadResult)current;
                    next = plcResult.Data;
                    valueType = PlcReadDynamicVariable.GetValueType(plcResult.DataType);
                }
                if (next is SharedVarValue || next is PlcReadResult)
                {
                    // 常见标量不创建集合；仅嵌套包装需要循环检测。
                    if (visited == null) visited = new HashSet<object>();
                    if (!visited.Add(current)) return false;
                }
                current = next;
            }
            value = current;
            valueType = value?.GetType() ?? valueType;
            return true;
        }
    }
    /// <summary>
    /// 共享变量值类
    /// </summary>
    public class SharedVarValue
    {
        /// <summary>原始共享数据，可以是标量、数组或复杂结果。</summary>
        public object Data;
        /// <summary>共享数据类型，空值时仍可保留类型信息。</summary>
        public Type Type;
        /// <summary>创建尚未写入有效数据的共享变量。</summary>
        public SharedVarValue()
        {
            Data = default(object);
            Type = Data?.GetType();
        }
        /// <summary>用原始数据和类型创建共享变量。</summary>
        public SharedVarValue(object data, Type type)
        {
            Data = data;
            Type = type;
        }
    }
}
