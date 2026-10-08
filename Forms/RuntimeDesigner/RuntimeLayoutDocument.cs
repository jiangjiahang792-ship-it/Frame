using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>第一阶段可用的运行界面组件。</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RuntimeWidgetKind
    {
        /// <summary>保持比例显示内嵌图片。</summary>
        Image,
        /// <summary>显示预览运行耗时。</summary>
        Elapsed,
        /// <summary>显示可配置的多行文本。</summary>
        Text,
        /// <summary>显示布尔状态的信号灯。</summary>
        Signal,
        /// <summary>单次运行和循环/停止控制。</summary>
        RunControl,
        /// <summary>订阅全部或指定流程及其组合模块日志。</summary>
        Log,
        /// <summary>打开指定流程的原有手动参数窗口。</summary>
        ManualParameters,
        /// <summary>对指定流程调用原有一键学习入口。</summary>
        OneClickLearning
    }

    /// <summary>文本框的水平对齐方式，模板保存稳定的枚举名称。</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RuntimeTextAlignment
    {
        /// <summary>靠左对齐，兼容旧布局。</summary>
        Left,
        /// <summary>水平居中。</summary>
        Center,
        /// <summary>靠右对齐。</summary>
        Right
    }

    /// <summary>不依赖窗体和设备的可移植运行界面文档。</summary>
    public sealed class RuntimeLayoutDocument
    {
        /// <summary>模板格式版本。</summary>
        public int Version { get; set; } = 1;
        /// <summary>页面名称。</summary>
        [DisplayName("页面名称"), Category("页面")]
        public string Name { get; set; } = "运行界面";
        /// <summary>页面逻辑宽度。</summary>
        [DisplayName("宽度"), Category("页面")]
        public int Width { get; set; } = 1280;
        /// <summary>页面逻辑高度。</summary>
        [DisplayName("高度"), Category("页面")]
        public int Height { get; set; } = 720;
        /// <summary>页面背景色的 HTML 表达。</summary>
        public string Background { get; set; } = "#303235";
        /// <summary>组件按从底层到顶层的顺序保存。</summary>
        public List<RuntimeLayoutElement> Elements { get; set; } = new List<RuntimeLayoutElement>();
    }

    /// <summary>组件的序列化属性；不包含运行时设备引用和 GDI 对象。</summary>
    public sealed class RuntimeLayoutElement
    {
        /// <summary>稳定的组件标识，复制时重新生成。</summary>
        [Browsable(false)]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>组件类型。</summary>
        [Browsable(false)]
        public RuntimeWidgetKind Kind { get; set; }
        /// <summary>组件列表中使用的名称。</summary>
        [DisplayName("名称"), Category("标识")]
        public string Name { get; set; } = "组件";
        /// <summary>页面内横坐标。</summary>
        [DisplayName("左边距"), Category("布局")]
        public int X { get; set; }
        /// <summary>页面内纵坐标。</summary>
        [DisplayName("上边距"), Category("布局")]
        public int Y { get; set; }
        /// <summary>逻辑宽度。</summary>
        [DisplayName("宽度"), Category("布局")]
        public int Width { get; set; } = 240;
        /// <summary>逻辑高度。</summary>
        [DisplayName("高度"), Category("布局")]
        public int Height { get; set; } = 80;
        /// <summary>文本框内容或其他组件的标题。</summary>
        [DisplayName("文本"), Category("内容"), Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", typeof(System.Drawing.Design.UITypeEditor))]
        public string Text { get; set; } = "文本";
        /// <summary>文本框水平对齐方式，缺省保持原有左对齐效果。</summary>
        [DisplayName("文本对齐"), Category("外观")]
        public RuntimeTextAlignment TextAlignment { get; set; } = RuntimeTextAlignment.Left;
        /// <summary>字体逻辑像素大小。</summary>
        [DisplayName("字号"), Category("外观")]
        public float FontSize { get; set; } = 20;
        /// <summary>是否使用粗体。</summary>
        [DisplayName("粗体"), Category("外观")]
        public bool Bold { get; set; }
        /// <summary>文字颜色。</summary>
        [Browsable(false)]
        public string Foreground { get; set; } = "#ECEEF0";
        /// <summary>背景颜色。</summary>
        [Browsable(false)]
        public string Background { get; set; } = "#383B3F";
        /// <summary>预览耗时，尚不绑定实际流程。</summary>
        [DisplayName("预览耗时（毫秒）"), Category("预览数据")]
        public double ElapsedMilliseconds { get; set; } = 12.5;
        /// <summary>预览信号状态，尚不绑定实际设备。</summary>
        [DisplayName("预览点亮"), Category("预览数据")]
        public bool SignalOn { get; set; } = true;
        /// <summary>信号点亮颜色。</summary>
        [Browsable(false)]
        public string OnColor { get; set; } = "#45C484";
        /// <summary>信号熄灭颜色。</summary>
        [Browsable(false)]
        public string OffColor { get; set; } = "#686D73";
        /// <summary>随模板保存的 PNG 图片，不引用外部文件路径。</summary>
        [Browsable(false)]
        public string ImageData { get; set; }
        /// <summary>可选的真实结果绑定；为空时保留原有静态内容行为。</summary>
        public RuntimeDataBinding Binding { get; set; }
        /// <summary>图像组件的独立ROI来源；为空时不绘制生产结果叠加层。</summary>
        [Browsable(false)]
        public RuntimeDataBinding RoiBinding { get; set; }
        /// <summary>运行控制的根流程编号；为空表示按方案规则运行全部启用流程。</summary>
        [Browsable(false)]
        public int? RunProcessId { get; set; }
        /// <summary>手动参数或一键学习绑定的根流程编号；空值表示未绑定，绝不表示全部流程。</summary>
        [Browsable(false)]
        public int? ActionProcessId { get; set; }
        /// <summary>日志订阅的根流程编号，空值表示全部流程。</summary>
        [Browsable(false)]
        public int? LogProcessId { get; set; }
        /// <summary>日志框最多保留的记录数，正文不持久化。</summary>
        [Browsable(false)]
        public int LogCapacity { get; set; } = 2000;
        /// <summary>日志框的最低显示等级，仍受系统日志开关约束。</summary>
        [Browsable(false)]
        public RuntimeLogLevel LogMinimumLevel { get; set; } = RuntimeLogLevel.Debug;
        /// <summary>新记录到来时是否自动滚动到末尾。</summary>
        [Browsable(false)]
        public bool LogAutoScroll { get; set; } = true;
        /// <summary>获得设计坐标中的组件边界。</summary>
        [JsonIgnore, Browsable(false)]
        public Rectangle Bounds => new Rectangle(X, Y, Width, Height);

        /// <summary>复制属性草稿和绑定路径，不复制或解码不可变图片文本。</summary>
        public RuntimeLayoutElement Copy()
        {
            var copy = (RuntimeLayoutElement)MemberwiseClone();
            copy.Binding = Binding?.Copy();
            copy.RoiBinding = RoiBinding?.Copy();
            return copy;
        }
    }
}
