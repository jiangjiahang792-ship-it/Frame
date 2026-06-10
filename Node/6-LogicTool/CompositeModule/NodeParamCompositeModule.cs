using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合模块节点参数，保存来源流程在导入时刻的节点和连线快照。
    /// </summary>
    public class NodeParamCompositeModule : INodeParam
    {
        /// <summary>
        /// 模块显示名称，默认使用来源流程名称。
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// 来源流程 ID，用于界面回显和重新导入时定位。
        /// </summary>
        public int SourceProcessId { get; set; }

        /// <summary>
        /// 来源流程名称，用于来源流程不存在时仍能阅读快照来源。
        /// </summary>
        public string SourceProcessName { get; set; }

        /// <summary>
        /// 快照流程是否使用画布图结构执行。
        /// </summary>
        public bool HasCanvasGraph { get; set; } = true;

        /// <summary>
        /// 快照内的节点配置列表。
        /// </summary>
        public List<NodeConfig> NodeInfos { get; set; } = new List<NodeConfig>();

        /// <summary>
        /// 快照内的流程连线配置列表。
        /// </summary>
        public List<ProcessConnectionConfig> ConnectionInfos { get; set; } = new List<ProcessConnectionConfig>();

        /// <summary>
        /// 来源流程内声明的组合模块输入端口。
        /// </summary>
        public List<CompositeInputPortDefinition> InputPorts { get; set; } = new List<CompositeInputPortDefinition>();

        /// <summary>
        /// 当前组合模块实例对输入端口的外部绑定。
        /// </summary>
        public List<CompositeInputBinding> InputBindings { get; set; } = new List<CompositeInputBinding>();

        /// <summary>
        /// 来源流程内声明的组合模块输出端口。
        /// </summary>
        public List<CompositeOutputPortDefinition> OutputPorts { get; set; } = new List<CompositeOutputPortDefinition>();
    }

    /// <summary>
    /// 组合模块端口值类型，用于配置界面、运行时转换和订阅类型提示。
    /// </summary>
    public enum CompositePortValueType
    {
        /// <summary>
        /// 自动类型，保留原始对象。
        /// </summary>
        Any,

        /// <summary>
        /// 数值类型，运行时按 double 处理。
        /// </summary>
        Number,

        /// <summary>
        /// 布尔类型。
        /// </summary>
        Boolean,

        /// <summary>
        /// 文本类型。
        /// </summary>
        String,

        /// <summary>
        /// 图像输出类型。
        /// </summary>
        Image
    }

    /// <summary>
    /// 组合模块输入值来源。
    /// </summary>
    public enum CompositePortValueSourceMode
    {
        /// <summary>
        /// 使用常量值。
        /// </summary>
        Constant,

        /// <summary>
        /// 从外部上游节点订阅。
        /// </summary>
        Subscription
    }

    /// <summary>
    /// 组合模块输入端口定义，来源流程通过组合输入节点声明。
    /// </summary>
    public class CompositeInputPortDefinition
    {
        /// <summary>
        /// 输入端口名称，也是内部流程订阅时看到的变量名。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 输入端口值类型。
        /// </summary>
        public CompositePortValueType ValueType { get; set; } = CompositePortValueType.Any;

        /// <summary>
        /// 未由外部绑定时使用的默认常量值。
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// 端口备注。
        /// </summary>
        public string Note { get; set; }
    }

    /// <summary>
    /// 组合模块输入端口在外部实例上的绑定配置。
    /// </summary>
    public class CompositeInputBinding
    {
        /// <summary>
        /// 输入端口名称。
        /// </summary>
        public string PortName { get; set; }

        /// <summary>
        /// 输入端口值类型。
        /// </summary>
        public CompositePortValueType ValueType { get; set; } = CompositePortValueType.Any;

        /// <summary>
        /// 输入值来源模式。
        /// </summary>
        public CompositePortValueSourceMode SourceMode { get; set; } = CompositePortValueSourceMode.Constant;

        /// <summary>
        /// 常量来源时的文本值。
        /// </summary>
        public string ConstantText { get; set; }

        /// <summary>
        /// 订阅来源节点 ID。
        /// </summary>
        public int SourceNodeId { get; set; }

        /// <summary>
        /// 订阅来源节点显示文本。
        /// </summary>
        public string SourceNodeText { get; set; }

        /// <summary>
        /// 订阅结果属性路径。
        /// </summary>
        public string PropertyPath { get; set; }

        /// <summary>
        /// 订阅结果属性显示名。
        /// </summary>
        public string PropertyDisplayName { get; set; }

        /// <summary>
        /// 订阅结果值类型名称。
        /// </summary>
        public string ValueTypeName { get; set; }

        /// <summary>
        /// 端口备注。
        /// </summary>
        public string Note { get; set; }
    }

    /// <summary>
    /// 组合模块输出端口定义，来源流程通过组合输出节点声明。
    /// </summary>
    public class CompositeOutputPortDefinition
    {
        /// <summary>
        /// 输出端口名称，也是外部流程订阅时看到的变量名。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 内部订阅来源节点 ID。
        /// </summary>
        public int SourceNodeId { get; set; }

        /// <summary>
        /// 内部订阅来源节点显示文本。
        /// </summary>
        public string SourceNodeText { get; set; }

        /// <summary>
        /// 内部订阅结果属性路径。
        /// </summary>
        public string PropertyPath { get; set; }

        /// <summary>
        /// 内部订阅结果属性显示名。
        /// </summary>
        public string PropertyDisplayName { get; set; }

        /// <summary>
        /// 内部订阅结果值类型名称。
        /// </summary>
        public string ValueTypeName { get; set; }

        /// <summary>
        /// 输出端口值类型。
        /// </summary>
        public CompositePortValueType ValueType { get; set; } = CompositePortValueType.Any;

        /// <summary>
        /// 输出端口备注。
        /// </summary>
        public string Note { get; set; }
    }

    /// <summary>
    /// 组合模块端口值转换和结果读取工具。
    /// </summary>
    internal static class CompositePortValueHelper
    {
        /// <summary>
        /// 获取端口类型的中文显示文本。
        /// </summary>
        /// <param name="valueType">端口值类型。</param>
        /// <returns>中文显示文本。</returns>
        public static string GetValueTypeText(CompositePortValueType valueType)
        {
            switch (valueType)
            {
                case CompositePortValueType.Number:
                    return "数值";
                case CompositePortValueType.Boolean:
                    return "布尔";
                case CompositePortValueType.String:
                    return "文本";
                case CompositePortValueType.Image:
                    return "图像";
                default:
                    return "自动";
            }
        }

        /// <summary>
        /// 获取端口值类型对应的运行时类型。
        /// </summary>
        /// <param name="valueType">端口值类型。</param>
        /// <returns>运行时类型。</returns>
        public static Type GetRuntimeType(CompositePortValueType valueType)
        {
            switch (valueType)
            {
                case CompositePortValueType.Number:
                    return typeof(double);
                case CompositePortValueType.Boolean:
                    return typeof(bool);
                case CompositePortValueType.String:
                    return typeof(string);
                case CompositePortValueType.Image:
                    return typeof(OutputImage);
                default:
                    return typeof(object);
            }
        }

        /// <summary>
        /// 根据结果属性类型推断组合端口值类型。
        /// </summary>
        /// <param name="type">结果属性类型。</param>
        /// <returns>组合端口值类型。</returns>
        public static CompositePortValueType InferValueType(Type type)
        {
            if (type == null)
                return CompositePortValueType.Any;

            Type valueType = Nullable.GetUnderlyingType(type) ?? type;
            if (typeof(OutputImage).IsAssignableFrom(valueType))
                return CompositePortValueType.Image;
            if (valueType == typeof(bool))
                return CompositePortValueType.Boolean;
            if (valueType == typeof(string))
                return CompositePortValueType.String;

            switch (Type.GetTypeCode(valueType))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return CompositePortValueType.Number;
                default:
                    return CompositePortValueType.Any;
            }
        }

        /// <summary>
        /// 判断结果属性类型是否可以作为端口值。
        /// </summary>
        /// <param name="type">结果属性类型。</param>
        /// <returns>可以作为端口值时返回 true。</returns>
        public static bool IsSelectableMemberType(Type type)
        {
            if (type == null)
                return false;

            Type valueType = Nullable.GetUnderlyingType(type) ?? type;
            if (valueType.IsPrimitive ||
                valueType.IsEnum ||
                valueType == typeof(string) ||
                valueType == typeof(decimal) ||
                typeof(OutputImage).IsAssignableFrom(valueType))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断结果属性类型是否应继续展开子属性。
        /// </summary>
        /// <param name="type">结果属性类型。</param>
        /// <returns>可以展开时返回 true。</returns>
        public static bool CanInspectMemberType(Type type)
        {
            if (type == null || IsSelectableMemberType(type))
                return false;

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                return false;

            return true;
        }

        /// <summary>
        /// 读取对象的公开属性和字段。
        /// </summary>
        /// <param name="type">对象类型。</param>
        /// <returns>公开可读成员列表。</returns>
        public static List<MemberInfo> GetReadableMembers(Type type)
        {
            List<MemberInfo> members = new List<MemberInfo>();
            if (type == null)
                return members;

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                members.Add(property);
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                members.Add(field);

            return members;
        }

        /// <summary>
        /// 获取成员显示名称。
        /// </summary>
        /// <param name="member">成员信息。</param>
        /// <returns>显示名称。</returns>
        public static string GetDisplayName(MemberInfo member)
        {
            DisplayNameAttribute displayName = member.GetCustomAttribute<DisplayNameAttribute>();
            if (displayName != null && !string.IsNullOrWhiteSpace(displayName.DisplayName))
                return displayName.DisplayName;

            return member.Name;
        }

        /// <summary>
        /// 获取成员类型。
        /// </summary>
        /// <param name="member">成员信息。</param>
        /// <returns>成员类型。</returns>
        public static Type GetMemberType(MemberInfo member)
        {
            PropertyInfo property = member as PropertyInfo;
            if (property != null)
                return property.PropertyType;

            FieldInfo field = member as FieldInfo;
            if (field != null)
                return field.FieldType;

            return typeof(object);
        }

        /// <summary>
        /// 从节点结果读取端口配置指定的值。
        /// </summary>
        /// <param name="currentNode">当前读取节点。</param>
        /// <param name="sourceNodeId">来源节点 ID。</param>
        /// <param name="sourceNodeText">来源节点显示文本。</param>
        /// <param name="propertyPath">结果属性路径。</param>
        /// <param name="propertyDisplayName">结果属性显示名。</param>
        /// <returns>读取到的结果值。</returns>
        public static object ReadSourceValue(
            NodeBase currentNode,
            int sourceNodeId,
            string sourceNodeText,
            string propertyPath,
            string propertyDisplayName)
        {
            if (currentNode == null || currentNode.Process == null)
                throw new Exception("当前节点未绑定到流程。");

            NodeBase sourceNode = FindSourceNode(currentNode.Process.Nodes, sourceNodeId, sourceNodeText);
            if (sourceNode == null)
                throw new Exception("找不到订阅来源节点：" + sourceNodeText);
            if (!sourceNode.Active)
                throw new Exception("订阅来源节点已禁用：" + GetNodeText(sourceNode));
            if (sourceNode.Result == null)
                throw new Exception("订阅来源节点还没有运行结果：" + GetNodeText(sourceNode));
            if (currentNode.Process.IsRuning && !sourceNode.HasSuccessfulResultForRun(currentNode.Process.CurrentRunId))
                throw new Exception("订阅来源节点本次流程未成功运行：" + GetNodeText(sourceNode));
            if (string.IsNullOrWhiteSpace(propertyPath))
                throw new Exception("没有选择订阅结果属性。");

            object dynamicValue;
            if (DynamicResultVariableResolver.TryGetValue(sourceNode.Result, propertyPath, out dynamicValue))
                return dynamicValue;

            object value = GetMemberPathValue(sourceNode.Result, propertyPath);
            if (value == null)
            {
                string propertyText = string.IsNullOrWhiteSpace(propertyDisplayName)
                    ? propertyPath
                    : propertyDisplayName;
                throw new Exception("订阅结果属性为空：" + GetNodeText(sourceNode) + "." + propertyText);
            }

            return value;
        }

        /// <summary>
        /// 按端口类型转换输入值。
        /// </summary>
        /// <param name="value">原始输入值。</param>
        /// <param name="valueType">目标端口类型。</param>
        /// <param name="label">错误提示标签。</param>
        /// <returns>转换后的输入值。</returns>
        public static object ConvertValue(object value, CompositePortValueType valueType, string label)
        {
            if (valueType == CompositePortValueType.Any)
                return value;

            if (value == null)
                throw new Exception(label + "为空。");

            switch (valueType)
            {
                case CompositePortValueType.Number:
                    return ConvertToDouble(value, label);
                case CompositePortValueType.Boolean:
                    return ConvertToBoolean(value, label);
                case CompositePortValueType.String:
                    return Convert.ToString(value, CultureInfo.CurrentCulture);
                case CompositePortValueType.Image:
                    OutputImage outputImage = value as OutputImage;
                    if (outputImage == null)
                        throw new Exception(label + "不是图像输出类型。");
                    return outputImage;
                default:
                    return value;
            }
        }

        /// <summary>
        /// 从常量文本构建端口值。
        /// </summary>
        /// <param name="text">常量文本。</param>
        /// <param name="valueType">端口类型。</param>
        /// <param name="label">错误提示标签。</param>
        /// <returns>转换后的常量值。</returns>
        public static object ConvertConstant(string text, CompositePortValueType valueType, string label)
        {
            if (valueType == CompositePortValueType.Image)
                throw new Exception(label + "为图像端口，不能使用常量输入。");

            if (valueType == CompositePortValueType.String || valueType == CompositePortValueType.Any)
                return text ?? string.Empty;

            return ConvertValue(text, valueType, label);
        }

        /// <summary>
        /// 在节点列表中查找订阅来源节点。
        /// </summary>
        /// <param name="nodes">节点列表。</param>
        /// <param name="sourceNodeId">来源节点 ID。</param>
        /// <param name="sourceNodeText">来源节点显示文本。</param>
        /// <returns>匹配到的节点。</returns>
        public static NodeBase FindSourceNode(List<NodeBase> nodes, int sourceNodeId, string sourceNodeText)
        {
            if (nodes == null)
                return null;

            if (sourceNodeId > 0)
            {
                foreach (NodeBase node in nodes)
                {
                    if (node != null && node.ID == sourceNodeId)
                        return node;
                }
            }

            foreach (NodeBase node in nodes)
            {
                if (node != null && string.Equals(GetNodeText(node), sourceNodeText, StringComparison.OrdinalIgnoreCase))
                    return node;
            }

            return null;
        }

        /// <summary>
        /// 获取节点订阅显示文本。
        /// </summary>
        /// <param name="node">流程节点。</param>
        /// <returns>节点显示文本。</returns>
        public static string GetNodeText(NodeBase node)
        {
            return node == null ? string.Empty : node.ID + "." + node.NodeName;
        }

        /// <summary>
        /// 读取成员路径对应的对象值。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <param name="memberPath">成员路径。</param>
        /// <returns>成员值。</returns>
        private static object GetMemberPathValue(object target, string memberPath)
        {
            if (target == null)
                return null;

            object current = target;
            string[] segments = memberPath.Split('.');
            foreach (string segment in segments)
            {
                if (current == null)
                    return null;

                MemberInfo member = FindReadableMember(current.GetType(), segment);
                if (member == null)
                    throw new Exception("结果类型“" + current.GetType().Name + "”中找不到属性或字段“" + segment + "”。");

                current = GetMemberValue(current, member);
            }

            return current;
        }

        /// <summary>
        /// 查找指定名称的公开可读成员。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="memberName">成员名称。</param>
        /// <returns>成员信息。</returns>
        private static MemberInfo FindReadableMember(Type type, string memberName)
        {
            if (type == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            foreach (MemberInfo member in GetReadableMembers(type))
            {
                if (string.Equals(member.Name, memberName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(GetDisplayName(member), memberName, StringComparison.OrdinalIgnoreCase))
                {
                    return member;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取成员值。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <param name="member">成员信息。</param>
        /// <returns>成员值。</returns>
        private static object GetMemberValue(object target, MemberInfo member)
        {
            PropertyInfo property = member as PropertyInfo;
            if (property != null)
                return property.GetValue(target, null);

            FieldInfo field = member as FieldInfo;
            if (field != null)
                return field.GetValue(target);

            return null;
        }

        /// <summary>
        /// 转换为 double。
        /// </summary>
        /// <param name="value">原始值。</param>
        /// <param name="label">错误提示标签。</param>
        /// <returns>double 值。</returns>
        private static double ConvertToDouble(object value, string label)
        {
            if (value is double)
                return (double)value;
            if (value is float)
                return Convert.ToDouble((float)value, CultureInfo.InvariantCulture);
            if (value is decimal)
                return Convert.ToDouble((decimal)value, CultureInfo.InvariantCulture);

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
            }

            double parsed;
            string text = Convert.ToString(value, CultureInfo.CurrentCulture);
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ||
                double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            throw new Exception(label + "的值“" + text + "”不能转换为数值。");
        }

        /// <summary>
        /// 转换为 bool。
        /// </summary>
        /// <param name="value">原始值。</param>
        /// <param name="label">错误提示标签。</param>
        /// <returns>bool 值。</returns>
        private static bool ConvertToBoolean(object value, string label)
        {
            if (value is bool)
                return (bool)value;

            string text = Convert.ToString(value, CultureInfo.CurrentCulture);
            bool boolValue;
            if (bool.TryParse(text, out boolValue))
                return boolValue;

            double number;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out number) ||
                double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out number))
            {
                return Math.Abs(number) > 1e-12;
            }

            throw new Exception(label + "的值“" + text + "”不能转换为布尔。");
        }
    }

    /// <summary>
    /// 组合模块快照构建器，负责从流程提取可序列化的节点和连线配置。
    /// </summary>
    internal static class CompositeModuleSnapshotBuilder
    {
        /// <summary>
        /// 根据指定流程创建组合模块参数快照。
        /// </summary>
        /// <param name="sourceProcess">需要封装为组合模块的来源流程。</param>
        /// <returns>包含节点、参数和连线的组合模块参数。</returns>
        public static NodeParamCompositeModule CreateFromProcess(Process sourceProcess)
        {
            if (sourceProcess == null)
                throw new ArgumentNullException(nameof(sourceProcess));

            NodeParamCompositeModule param = new NodeParamCompositeModule
            {
                ModuleName = sourceProcess.ProcessName,
                SourceProcessId = sourceProcess.ID,
                SourceProcessName = sourceProcess.ProcessName,
                HasCanvasGraph = sourceProcess.HasCanvasGraph,
                NodeInfos = new List<NodeConfig>(),
                ConnectionInfos = new List<ProcessConnectionConfig>(),
                InputPorts = new List<CompositeInputPortDefinition>(),
                InputBindings = new List<CompositeInputBinding>(),
                OutputPorts = new List<CompositeOutputPortDefinition>()
            };

            foreach (NodeBase node in sourceProcess.Nodes)
            {
                if (node == null)
                    continue;

                param.NodeInfos.Add(new NodeConfig
                {
                    NodeType = node.NodeType,
                    NodeName = node.NodeName,
                    ID = node.ID,
                    Active = node.Active,
                    OutputLog = node.OutputLog,
                    Selected = false,
                    HasCanvasLayout = true,
                    CanvasX = node.CanvasLocation.X,
                    CanvasY = node.CanvasLocation.Y,
                    CanvasWidth = node.CanvasSize.Width,
                    CanvasHeight = node.CanvasSize.Height,
                    IsStartNode = node.IsStartNode,
                    NodeParam = CloneNodeParam(node.ParamForm == null ? null : node.ParamForm.Params)
                });
            }

            foreach (ProcessConnection connection in sourceProcess.Connections)
            {
                if (connection == null)
                    continue;

                param.ConnectionInfos.Add(new ProcessConnectionConfig
                {
                    ID = connection.ID,
                    FromNodeId = connection.FromNodeId,
                    ToNodeId = connection.ToNodeId,
                    FromAnchor = connection.FromAnchor,
                    ToAnchor = connection.ToAnchor,
                    Branch = connection.Branch
                });
            }

            CollectPortDefinitions(sourceProcess, param);
            param.InputBindings = BuildDefaultInputBindings(param.InputPorts);

            return param;
        }

        /// <summary>
        /// 根据快照内的组合输入/输出节点收集端口定义。
        /// </summary>
        /// <param name="sourceProcess">来源流程。</param>
        /// <param name="param">组合模块参数。</param>
        private static void CollectPortDefinitions(Process sourceProcess, NodeParamCompositeModule param)
        {
            if (sourceProcess == null || param == null)
                return;

            foreach (NodeBase node in sourceProcess.Nodes)
            {
                if (node == null || node.ParamForm == null)
                    continue;

                NodeParamCompositeInput inputParam = node.ParamForm.Params as NodeParamCompositeInput;
                if (inputParam != null && inputParam.Ports != null)
                {
                    foreach (CompositeInputPortDefinition port in inputParam.Ports)
                    {
                        if (port == null || string.IsNullOrWhiteSpace(port.Name))
                            continue;

                        param.InputPorts.Add(ClonePort(port));
                    }
                }

                NodeParamCompositeOutput outputParam = node.ParamForm.Params as NodeParamCompositeOutput;
                if (outputParam != null && outputParam.Ports != null)
                {
                    foreach (CompositeOutputPortDefinition port in outputParam.Ports)
                    {
                        if (port == null || string.IsNullOrWhiteSpace(port.Name))
                            continue;

                        param.OutputPorts.Add(ClonePort(port));
                    }
                }
            }
        }

        /// <summary>
        /// 基于输入端口定义生成默认外部绑定。
        /// </summary>
        /// <param name="ports">输入端口定义。</param>
        /// <returns>输入绑定列表。</returns>
        public static List<CompositeInputBinding> BuildDefaultInputBindings(IEnumerable<CompositeInputPortDefinition> ports)
        {
            List<CompositeInputBinding> bindings = new List<CompositeInputBinding>();
            if (ports == null)
                return bindings;

            foreach (CompositeInputPortDefinition port in ports)
            {
                if (port == null || string.IsNullOrWhiteSpace(port.Name))
                    continue;

                bindings.Add(new CompositeInputBinding
                {
                    PortName = port.Name.Trim(),
                    ValueType = port.ValueType,
                    SourceMode = CompositePortValueSourceMode.Constant,
                    ConstantText = port.DefaultValue,
                    Note = port.Note
                });
            }

            return bindings;
        }

        /// <summary>
        /// 按最新端口定义同步输入绑定，保留同名端口已有设置。
        /// </summary>
        /// <param name="ports">输入端口定义。</param>
        /// <param name="oldBindings">旧输入绑定。</param>
        /// <returns>同步后的输入绑定。</returns>
        public static List<CompositeInputBinding> SyncInputBindings(
            IEnumerable<CompositeInputPortDefinition> ports,
            IEnumerable<CompositeInputBinding> oldBindings)
        {
            List<CompositeInputBinding> result = new List<CompositeInputBinding>();
            Dictionary<string, CompositeInputBinding> oldMap = new Dictionary<string, CompositeInputBinding>(StringComparer.OrdinalIgnoreCase);
            if (oldBindings != null)
            {
                foreach (CompositeInputBinding binding in oldBindings)
                {
                    if (binding == null || string.IsNullOrWhiteSpace(binding.PortName))
                        continue;

                    oldMap[binding.PortName.Trim()] = binding;
                }
            }

            if (ports == null)
                return result;

            foreach (CompositeInputPortDefinition port in ports)
            {
                if (port == null || string.IsNullOrWhiteSpace(port.Name))
                    continue;

                string name = port.Name.Trim();
                CompositeInputBinding oldBinding;
                if (oldMap.TryGetValue(name, out oldBinding) && oldBinding != null)
                {
                    CompositeInputBinding binding = CloneInputBinding(oldBinding);
                    binding.PortName = name;
                    binding.ValueType = port.ValueType;
                    binding.Note = string.IsNullOrWhiteSpace(binding.Note) ? port.Note : binding.Note;
                    if (string.IsNullOrWhiteSpace(binding.ConstantText))
                        binding.ConstantText = port.DefaultValue;
                    result.Add(binding);
                    continue;
                }

                result.Add(new CompositeInputBinding
                {
                    PortName = name,
                    ValueType = port.ValueType,
                    SourceMode = CompositePortValueSourceMode.Constant,
                    ConstantText = port.DefaultValue,
                    Note = port.Note
                });
            }

            return result;
        }

        /// <summary>
        /// 深拷贝输入端口定义。
        /// </summary>
        /// <param name="port">输入端口定义。</param>
        /// <returns>端口副本。</returns>
        private static CompositeInputPortDefinition ClonePort(CompositeInputPortDefinition port)
        {
            return new CompositeInputPortDefinition
            {
                Name = port.Name,
                ValueType = port.ValueType,
                DefaultValue = port.DefaultValue,
                Note = port.Note
            };
        }

        /// <summary>
        /// 深拷贝输出端口定义。
        /// </summary>
        /// <param name="port">输出端口定义。</param>
        /// <returns>端口副本。</returns>
        private static CompositeOutputPortDefinition ClonePort(CompositeOutputPortDefinition port)
        {
            return new CompositeOutputPortDefinition
            {
                Name = port.Name,
                SourceNodeId = port.SourceNodeId,
                SourceNodeText = port.SourceNodeText,
                PropertyPath = port.PropertyPath,
                PropertyDisplayName = port.PropertyDisplayName,
                ValueTypeName = port.ValueTypeName,
                ValueType = port.ValueType,
                Note = port.Note
            };
        }

        /// <summary>
        /// 深拷贝输入绑定。
        /// </summary>
        /// <param name="binding">输入绑定。</param>
        /// <returns>绑定副本。</returns>
        private static CompositeInputBinding CloneInputBinding(CompositeInputBinding binding)
        {
            return new CompositeInputBinding
            {
                PortName = binding.PortName,
                ValueType = binding.ValueType,
                SourceMode = binding.SourceMode,
                ConstantText = binding.ConstantText,
                SourceNodeId = binding.SourceNodeId,
                SourceNodeText = binding.SourceNodeText,
                PropertyPath = binding.PropertyPath,
                PropertyDisplayName = binding.PropertyDisplayName,
                ValueTypeName = binding.ValueTypeName,
                Note = binding.Note
            };
        }

        /// <summary>
        /// 深拷贝组合模块参数，避免运行时内部流程修改界面参数对象。
        /// </summary>
        /// <param name="sourceParam">需要拷贝的组合模块参数。</param>
        /// <returns>独立的组合模块参数副本。</returns>
        public static NodeParamCompositeModule Clone(NodeParamCompositeModule sourceParam)
        {
            if (sourceParam == null)
                return null;

            string json = JsonConvert.SerializeObject(sourceParam);
            return JsonConvert.DeserializeObject<NodeParamCompositeModule>(json);
        }

        /// <summary>
        /// 深拷贝普通节点参数，保留具体参数类型和其内部多态成员。
        /// </summary>
        /// <param name="sourceParam">需要拷贝的节点参数。</param>
        /// <returns>独立的节点参数副本。</returns>
        public static INodeParam CloneNodeParam(INodeParam sourceParam)
        {
            if (sourceParam == null)
                return null;

            string json = JsonConvert.SerializeObject(sourceParam);
            return JsonConvert.DeserializeObject(json, sourceParam.GetType()) as INodeParam;
        }
    }
}
