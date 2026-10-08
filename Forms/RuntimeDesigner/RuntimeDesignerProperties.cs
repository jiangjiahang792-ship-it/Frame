using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>属性栏的组件工作副本，过滤与当前组件无关的属性。</summary>
    public sealed class RuntimeElementProperties : ICustomTypeDescriptor
    {
        /// <summary>尚未提交到编辑会话的属性副本。</summary>
        [Browsable(false)]
        public RuntimeLayoutElement Draft { get; }
        /// <summary>初始化属性副本。</summary>
        public RuntimeElementProperties(RuntimeLayoutElement element) { Draft = element; }
        /// <summary>组件名称。</summary>
        [Category("标识"), DisplayName("名称")]
        public string Name { get => Draft.Name; set => Draft.Name = value; }
        /// <summary>组件的逻辑横坐标。</summary>
        [Category("布局"), DisplayName("左边距")]
        public int X { get => Draft.X; set => Draft.X = value; }
        /// <summary>组件的逻辑纵坐标。</summary>
        [Category("布局"), DisplayName("上边距")]
        public int Y { get => Draft.Y; set => Draft.Y = value; }
        /// <summary>组件逻辑宽度。</summary>
        [Category("布局"), DisplayName("宽度")]
        public int Width { get => Draft.Width; set => Draft.Width = value; }
        /// <summary>组件逻辑高度。</summary>
        [Category("布局"), DisplayName("高度")]
        public int Height { get => Draft.Height; set => Draft.Height = value; }
        /// <summary>多行文本或组件标题。</summary>
        [Category("内容"), DisplayName("文本"), Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", typeof(System.Drawing.Design.UITypeEditor))]
        public string Text { get => Draft.Text; set => Draft.Text = value; }
        /// <summary>文本框的水平对齐方式。</summary>
        [Category("外观"), DisplayName("文本对齐"), TypeConverter(typeof(RuntimeTextAlignmentConverter))]
        public RuntimeTextAlignment TextAlignment { get => Draft.TextAlignment; set => Draft.TextAlignment = value; }
        /// <summary>文字大小。</summary>
        [Category("外观"), DisplayName("字号")]
        public float FontSize { get => Draft.FontSize; set => Draft.FontSize = value; }
        /// <summary>文字粗细。</summary>
        [Category("外观"), DisplayName("粗体"), TypeConverter(typeof(RuntimeChineseBooleanConverter))]
        public bool Bold { get => Draft.Bold; set => Draft.Bold = value; }
        /// <summary>文字颜色。</summary>
        [Category("外观"), DisplayName("文字颜色")]
        public Color Foreground { get => RuntimeLayoutSerializer.ParseColor(Draft.Foreground); set => Draft.Foreground = RuntimeLayoutSerializer.FormatColor(value); }
        /// <summary>组件背景色。</summary>
        [Category("外观"), DisplayName("背景颜色")]
        public Color Background { get => RuntimeLayoutSerializer.ParseColor(Draft.Background); set => Draft.Background = RuntimeLayoutSerializer.FormatColor(value); }
        /// <summary>耗时组件使用的示例数值。</summary>
        [Category("预览数据"), DisplayName("耗时（毫秒）")]
        public double ElapsedMilliseconds { get => Draft.ElapsedMilliseconds; set => Draft.ElapsedMilliseconds = value; }
        /// <summary>信号灯使用的示例状态。</summary>
        [Category("预览数据"), DisplayName("点亮"), TypeConverter(typeof(RuntimeChineseBooleanConverter))]
        public bool SignalOn { get => Draft.SignalOn; set => Draft.SignalOn = value; }
        /// <summary>点亮时的信号颜色。</summary>
        [Category("外观"), DisplayName("亮灯颜色")]
        public Color OnColor { get => RuntimeLayoutSerializer.ParseColor(Draft.OnColor); set => Draft.OnColor = RuntimeLayoutSerializer.FormatColor(value); }
        /// <summary>熄灭时的信号颜色。</summary>
        [Category("外观"), DisplayName("灭灯颜色")]
        public Color OffColor { get => RuntimeLayoutSerializer.ParseColor(Draft.OffColor); set => Draft.OffColor = RuntimeLayoutSerializer.FormatColor(value); }
        /// <summary>日志显示缓冲保留条数。</summary>
        [Category("日志"), DisplayName("保留条数")]
        public int LogCapacity { get => Draft.LogCapacity; set => Draft.LogCapacity = value; }
        /// <summary>最低显示等级不改变系统实际记录开关。</summary>
        [Category("日志"), DisplayName("最低等级"), TypeConverter(typeof(RuntimeLogLevelConverter))]
        public RuntimeLogLevel LogMinimumLevel { get => Draft.LogMinimumLevel; set => Draft.LogMinimumLevel = value; }
        /// <summary>新记录到来时自动滚动。</summary>
        [Category("日志"), DisplayName("自动滚动"), TypeConverter(typeof(RuntimeChineseBooleanConverter))]
        public bool LogAutoScroll { get => Draft.LogAutoScroll; set => Draft.LogAutoScroll = value; }

        /// <summary>获得公共组件属性及类型专属属性。</summary>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var properties = TypeDescriptor.GetProperties(GetType(), attributes).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable);
            return new PropertyDescriptorCollection(properties.Where(p =>
                (p.Name != nameof(TextAlignment) || Draft.Kind == RuntimeWidgetKind.Text) &&
                (p.Name != nameof(LogCapacity) && p.Name != nameof(LogMinimumLevel) && p.Name != nameof(LogAutoScroll) || Draft.Kind == RuntimeWidgetKind.Log) &&
                (Draft.Kind != RuntimeWidgetKind.Log || (p.Name != nameof(Text) && p.Name != nameof(FontSize) && p.Name != nameof(Bold))) &&
                (p.Name != nameof(ElapsedMilliseconds) || Draft.Kind == RuntimeWidgetKind.Elapsed) &&
                (p.Name != nameof(SignalOn) && p.Name != nameof(OnColor) && p.Name != nameof(OffColor) || Draft.Kind == RuntimeWidgetKind.Signal)).ToArray());
        }
        /// <summary>获得未指定筛选条件的属性集合。</summary>
        public PropertyDescriptorCollection GetProperties() => GetProperties(new Attribute[0]);
        /// <summary>属性宿主就是当前代理。</summary>
        public object GetPropertyOwner(PropertyDescriptor pd) => this;
        /// <summary>代理的元数据。</summary>
        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(GetType());
        /// <summary>代理类名。</summary>
        public string GetClassName() => GetType().Name;
        /// <summary>组件名称。</summary>
        public string GetComponentName() => Draft.Name;
        /// <summary>默认类型转换器。</summary>
        public TypeConverter GetConverter() => new TypeConverter();
        /// <summary>属性栏无默认事件。</summary>
        public EventDescriptor GetDefaultEvent() => null;
        /// <summary>优先选中名称属性。</summary>
        public PropertyDescriptor GetDefaultProperty() => GetProperties()[nameof(Name)];
        /// <summary>代理不提供全局编辑器。</summary>
        public object GetEditor(Type editorBaseType) => null;
        /// <summary>不暴露业务事件。</summary>
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => EventDescriptorCollection.Empty;
        /// <summary>不暴露业务事件。</summary>
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;
    }

    /// <summary>属性栏使用中文对齐选项，不改变模板枚举值。</summary>
    public sealed class RuntimeTextAlignmentConverter : EnumConverter
    {
        /// <summary>按枚举顺序排列的中文对齐名称。</summary>
        private static readonly string[] Names = { "左对齐", "居中", "右对齐" };
        /// <summary>初始化文本对齐选项。</summary>
        public RuntimeTextAlignmentConverter() : base(typeof(RuntimeTextAlignment)) { }
        /// <summary>将对齐方式转换为中文显示。</summary>
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is RuntimeTextAlignment alignment && Enum.IsDefined(typeof(RuntimeTextAlignment), alignment)) return Names[(int)alignment];
            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <summary>将中文选项还原为对齐方式。</summary>
        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            int index = value is string text ? Array.IndexOf(Names, text) : -1;
            return index >= 0 ? (RuntimeTextAlignment)index : base.ConvertFrom(context, culture, value);
        }
    }

    /// <summary>属性栏使用中文日志等级，模板仍保存稳定的枚举值。</summary>
    public sealed class RuntimeLogLevelConverter : EnumConverter
    {
        /// <summary>按等级顺序排列的中文名称。</summary>
        private static readonly string[] Names = { "调试", "信息", "警告", "异常", "严重错误" };
        /// <summary>初始化可选等级。</summary>
        public RuntimeLogLevelConverter() : base(typeof(RuntimeLogLevel)) { }
        /// <summary>将等级转换为中文显示。</summary>
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is RuntimeLogLevel level && Enum.IsDefined(typeof(RuntimeLogLevel), level)) return Names[(int)level];
            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <summary>将中文选项还原为枚举。</summary>
        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            int index = value is string text ? Array.IndexOf(Names, text) : -1;
            return index >= 0 ? (RuntimeLogLevel)index : base.ConvertFrom(context, culture, value);
        }
    }

    /// <summary>把属性栏的布尔值显示为简体中文。</summary>
    public sealed class RuntimeChineseBooleanConverter : BooleanConverter
    {
        /// <summary>显示与实际布尔值对应的中文选项。</summary>
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is bool flag) return flag ? "是" : "否";
            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <summary>将属性栏选项还原为布尔值。</summary>
        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is string text)
            {
                if (text == "是") return true;
                if (text == "否") return false;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    /// <summary>页面属性的隔离代理。</summary>
    public sealed class RuntimePageProperties
    {
        /// <summary>页面名称。</summary>
        [Category("页面"), DisplayName("页面名称")]
        public string Name { get; set; }
        /// <summary>页面宽度。</summary>
        [Category("页面"), DisplayName("宽度")]
        public int Width { get; set; }
        /// <summary>页面高度。</summary>
        [Category("页面"), DisplayName("高度")]
        public int Height { get; set; }
        /// <summary>页面背景。</summary>
        [Category("外观"), DisplayName("背景颜色")]
        public Color Background { get; set; }
        /// <summary>从当前页面创建可编辑副本。</summary>
        public RuntimePageProperties(RuntimeLayoutDocument document)
        {
            Name = document.Name; Width = document.Width; Height = document.Height;
            Background = RuntimeLayoutSerializer.ParseColor(document.Background);
        }
    }
}
