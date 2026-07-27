using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 定义订阅值的数据类别。基础值按真实 CLR 类型归类，视觉复合值按数据结构归类。
    /// </summary>
    public enum SubscriptionDataCategory
    {
        /// <summary>尚未明确分类的旧版对象。</summary>
        Unknown,
        /// <summary>布尔值，包括 OK/NG 和普通开关量。</summary>
        Boolean,
        /// <summary>整数、浮点数和十进制数。</summary>
        Number,
        /// <summary>字符串或字符数据。</summary>
        Text,
        /// <summary>图像数据。</summary>
        Image,
        /// <summary>单个二维点。</summary>
        Point,
        /// <summary>不具有闭合语义的点集合。</summary>
        PointCollection,
        /// <summary>有序轮廓数据。</summary>
        Contour,
        /// <summary>直线数据。</summary>
        Line,
        /// <summary>圆数据。</summary>
        Circle,
        /// <summary>椭圆数据。</summary>
        Ellipse,
        /// <summary>普通矩形或旋转矩形数据。</summary>
        Rectangle,
        /// <summary>多边形或封闭区域数据。</summary>
        Region,
        /// <summary>包含坐标、角度和缩放的目标位姿。</summary>
        Pose,
        /// <summary>从基准位姿到当前位姿的位置修正数据。</summary>
        PositionCorrection,
        /// <summary>供绘制或汇总使用的完整算法结果。</summary>
        AlgorithmResult,
        /// <summary>测量工具的结构化结果。</summary>
        MeasurementResult,
        /// <summary>不能拆成基础值的通信或业务结构化对象。</summary>
        StructuredObject
    }

    /// <summary>
    /// 定义订阅值的数量形态。
    /// </summary>
    public enum SubscriptionValueMultiplicity
    {
        /// <summary>单个值。</summary>
        Single,
        /// <summary>同一类别的一组值。</summary>
        Collection,
        /// <summary>与多个视觉目标一一对应的结果集合。</summary>
        MultiTarget
    }

    /// <summary>
    /// 定义输出在新建订阅界面中的显示级别。
    /// </summary>
    public enum SubscriptionOutputVisibility
    {
        /// <summary>默认显示。</summary>
        Core,
        /// <summary>默认收起，仅在类型明确需要或用户展开时显示。</summary>
        Advanced,
        /// <summary>禁止新选，仅保留旧方案兼容读取。</summary>
        Hidden
    }

    /// <summary>
    /// 定义基础数值之间允许采用的转换方式。
    /// </summary>
    public enum NumericConversionMode
    {
        /// <summary>不允许不同数值类型之间转换。</summary>
        None,
        /// <summary>只允许不会缩小数值范围的安全扩宽转换。</summary>
        SafeWidening,
        /// <summary>允许经过溢出和格式检查的显式数值转换。</summary>
        Checked
    }

    /// <summary>
    /// 声明一个结果属性可以作为订阅输出，并设置其数据类别和显示策略。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class SubscriptionOutputAttribute : Attribute
    {
        /// <summary>
        /// 初始化一个由 CLR 类型自动判断数据类别的输出声明。
        /// </summary>
        public SubscriptionOutputAttribute()
            : this(SubscriptionDataCategory.Unknown)
        {
        }

        /// <summary>
        /// 初始化一个具有明确数据类别的输出声明。
        /// </summary>
        /// <param name="category">输出数据类别。</param>
        public SubscriptionOutputAttribute(SubscriptionDataCategory category)
        {
            Category = category;
            Multiplicity = SubscriptionValueMultiplicity.Single;
            Visibility = SubscriptionOutputVisibility.Core;
        }

        /// <summary>获取输出数据类别；Unknown 表示按 CLR 类型推断。</summary>
        public SubscriptionDataCategory Category { get; private set; }

        /// <summary>获取或设置输出数量形态。</summary>
        public SubscriptionValueMultiplicity Multiplicity { get; set; }

        /// <summary>获取或设置输出显示级别。</summary>
        public SubscriptionOutputVisibility Visibility { get; set; }
    }

    /// <summary>
    /// 描述一个已经统一分类的静态或动态订阅输出端口。
    /// </summary>
    public sealed class SubscriptionOutputDescriptor
    {
        /// <summary>获取或设置来源节点；静态类型测试时可以为空。</summary>
        public object SourceNode { get; set; }

        /// <summary>获取或设置稳定属性路径或动态变量路径。</summary>
        public string PropertyPath { get; set; }

        /// <summary>获取或设置用户界面显示名称。</summary>
        public string DisplayName { get; set; }

        /// <summary>获取或设置实际 CLR 值类型。</summary>
        public Type ValueType { get; set; }

        /// <summary>获取或设置数据类别。</summary>
        public SubscriptionDataCategory Category { get; set; }

        /// <summary>获取或设置数量形态。</summary>
        public SubscriptionValueMultiplicity Multiplicity { get; set; }

        /// <summary>获取或设置显示级别。</summary>
        public SubscriptionOutputVisibility Visibility { get; set; }

        /// <summary>获取或设置该输出是否来自动态变量。</summary>
        public bool IsDynamic { get; set; }

        /// <summary>获取或设置该输出是否仅因旧方案当前选择而显示。</summary>
        public bool IsLegacySelection { get; set; }

        /// <summary>获取或设置保存的结果路径是否已经不存在。</summary>
        public bool IsMissing { get; set; }

        /// <summary>获取或设置静态输出对应的反射属性。</summary>
        public PropertyInfo Property { get; set; }
    }

    /// <summary>
    /// 定义一个订阅输入允许接收的数据类别、实际类型、数量形态和数值转换方式。
    /// </summary>
    public sealed class SubscriptionInputContract
    {
        /// <summary>允许的数据类别集合。</summary>
        private readonly HashSet<SubscriptionDataCategory> _acceptedCategories;

        /// <summary>允许的数量形态集合。</summary>
        private readonly HashSet<SubscriptionValueMultiplicity> _acceptedMultiplicities;

        /// <summary>
        /// 初始化输入契约。
        /// </summary>
        /// <param name="categories">允许的数据类别。</param>
        /// <param name="expectedValueType">调用方最终读取的 CLR 类型；为空表示只检查类别。</param>
        /// <param name="multiplicities">允许的数量形态；为空表示全部允许。</param>
        /// <param name="numericConversionMode">数值转换方式。</param>
        public SubscriptionInputContract(
            IEnumerable<SubscriptionDataCategory> categories,
            Type expectedValueType,
            IEnumerable<SubscriptionValueMultiplicity> multiplicities,
            NumericConversionMode numericConversionMode)
        {
            _acceptedCategories = new HashSet<SubscriptionDataCategory>(
                categories ?? Enumerable.Empty<SubscriptionDataCategory>());
            _acceptedMultiplicities = new HashSet<SubscriptionValueMultiplicity>(
                multiplicities ?? Enum.GetValues(typeof(SubscriptionValueMultiplicity)).Cast<SubscriptionValueMultiplicity>());
            ExpectedValueType = expectedValueType;
            NumericConversionMode = numericConversionMode;
        }

        /// <summary>获取允许的数据类别集合。</summary>
        public IReadOnlyCollection<SubscriptionDataCategory> AcceptedCategories
        {
            get { return _acceptedCategories; }
        }

        /// <summary>获取允许的数量形态集合。</summary>
        public IReadOnlyCollection<SubscriptionValueMultiplicity> AcceptedMultiplicities
        {
            get { return _acceptedMultiplicities; }
        }

        /// <summary>获取调用方最终读取的 CLR 类型。</summary>
        public Type ExpectedValueType { get; private set; }

        /// <summary>获取允许的数值转换方式。</summary>
        public NumericConversionMode NumericConversionMode { get; private set; }

        /// <summary>
        /// 创建一个按最终 CLR 类型判断的输入契约。
        /// </summary>
        /// <param name="expectedType">最终 CLR 类型。</param>
        /// <returns>与该类型对应的输入契约。</returns>
        public static SubscriptionInputContract ForType(Type expectedType)
        {
            if (expectedType == null)
                return AnyVisible();

            SubscriptionDataCategory category = SubscriptionTypeCompatibility.ResolveCategory(expectedType);
            return new SubscriptionInputContract(
                new[] { category },
                expectedType,
                null,
                SubscriptionTypeCompatibility.IsNumericType(expectedType)
                    ? NumericConversionMode.SafeWidening
                    : NumericConversionMode.None);
        }

        /// <summary>
        /// 创建一个按数据类别判断的输入契约。
        /// </summary>
        /// <param name="categories">允许的数据类别。</param>
        /// <param name="numericConversionMode">数值转换方式。</param>
        /// <returns>类别输入契约。</returns>
        public static SubscriptionInputContract ForCategories(
            IEnumerable<SubscriptionDataCategory> categories,
            NumericConversionMode numericConversionMode)
        {
            return new SubscriptionInputContract(categories, null, null, numericConversionMode);
        }

        /// <summary>
        /// 创建一个按数据类别判断且采用安全数值扩宽的输入契约。
        /// </summary>
        /// <param name="categories">允许的数据类别。</param>
        /// <returns>类别输入契约。</returns>
        public static SubscriptionInputContract ForCategories(
            IEnumerable<SubscriptionDataCategory> categories)
        {
            return ForCategories(categories, NumericConversionMode.SafeWidening);
        }

        /// <summary>
        /// 创建一个允许所有已分类输出的通用输入契约。
        /// </summary>
        /// <returns>通用输入契约。</returns>
        public static SubscriptionInputContract AnyVisible()
        {
            IEnumerable<SubscriptionDataCategory> categories = Enum
                .GetValues(typeof(SubscriptionDataCategory))
                .Cast<SubscriptionDataCategory>()
                .Where(item => item != SubscriptionDataCategory.Unknown);
            return new SubscriptionInputContract(
                categories,
                null,
                null,
                NumericConversionMode.Checked);
        }

        /// <summary>
        /// 判断输出端口能否连接到当前输入契约。
        /// </summary>
        /// <param name="output">待检查输出端口。</param>
        /// <param name="reason">不兼容时的中文原因。</param>
        /// <returns>兼容时返回 true。</returns>
        public bool Accepts(SubscriptionOutputDescriptor output, out string reason)
        {
            reason = string.Empty;
            if (output == null || output.IsMissing)
            {
                reason = "订阅输出不存在。";
                return false;
            }

            if (!_acceptedCategories.Contains(output.Category))
            {
                reason = string.Format("输入不接受“{0}”类别。", output.Category);
                return false;
            }

            if (!_acceptedMultiplicities.Contains(output.Multiplicity))
            {
                reason = string.Format("输入不接受“{0}”数量形态。", output.Multiplicity);
                return false;
            }

            if (ExpectedValueType != null &&
                !SubscriptionTypeCompatibility.CanAssign(
                    output.ValueType,
                    ExpectedValueType,
                    NumericConversionMode))
            {
                reason = string.Format(
                    "输出类型 {0} 不能用于输入类型 {1}。",
                    output.ValueType == null ? "未知" : output.ValueType.Name,
                    ExpectedValueType.Name);
                return false;
            }

            return true;
        }
    }
}
