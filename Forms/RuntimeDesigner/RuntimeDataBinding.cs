using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>本阶段允许接入的只读运行结果种类。</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RuntimeBindingKind
    {
        /// <summary>节点的图像输出。</summary>
        Image,
        /// <summary>根流程的既有运行耗时。</summary>
        ProcessElapsed,
        /// <summary>节点的既有运行耗时。</summary>
        NodeElapsed,
        /// <summary>节点明确公开的算法显示结果，独立于图像来源。</summary>
        Roi
    }

    /// <summary>保存稳定作用域路径，不把设备、节点和图像实例写入模板。</summary>
    public sealed class RuntimeDataBinding
    {
        /// <summary>结果种类。</summary>
        public RuntimeBindingKind Kind { get; set; }
        /// <summary>方案内根流程编号。</summary>
        public int ProcessId { get; set; }
        /// <summary>逐级组合模块及最终节点编号；流程耗时为空数组。</summary>
        public int[] NodePath { get; set; } = new int[0];
        /// <summary>节点公开的静态属性或动态变量路径。</summary>
        public string PropertyPath { get; set; } = string.Empty;
        /// <summary>失效时仍可显示的原始中文路径，不用于定位来源。</summary>
        public string DisplayPath { get; set; } = string.Empty;
        /// <summary>创建独立草稿，防止复制或撤销共享可变路径。</summary>
        public RuntimeDataBinding Copy()
        {
            var copy = (RuntimeDataBinding)MemberwiseClone();
            copy.NodePath = NodePath == null ? null : (int[])NodePath.Clone();
            return copy;
        }
    }

    /// <summary>选择器的目录节点；只有带绑定的叶子可以确认。</summary>
    public sealed class RuntimeBindingChoice
    {
        /// <summary>中文目录名称。</summary>
        public string Name { get; set; }
        /// <summary>叶子的绑定值，分组为空。</summary>
        public RuntimeDataBinding Binding { get; set; }
        /// <summary>嵌套目录项。</summary>
        public List<RuntimeBindingChoice> Children { get; set; } = new List<RuntimeBindingChoice>();
    }

    /// <summary>可替换的数据源目录，设计器不直接依赖方案单例。</summary>
    public interface IRuntimeBindingCatalog
    {
        /// <summary>按控件类型筛选流程及输出树。</summary>
        IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind);
        /// <summary>解析当前名称，或明确提示来源已失效。</summary>
        string Describe(RuntimeDataBinding binding);
    }

    /// <summary>可选的ROI目录能力；原有目录实现不必提供ROI结果。</summary>
    public interface IRuntimeRoiBindingCatalog : IRuntimeBindingCatalog
    {
        /// <summary>只返回框架显示结果契约兼容的静态属性与动态变量。</summary>
        IReadOnlyList<RuntimeBindingChoice> GetRoiSources();
    }
}
