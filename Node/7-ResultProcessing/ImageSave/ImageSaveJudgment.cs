using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageSave
{
    /// <summary>
    /// 保存图像节点使用的统一 OK/NG 判定结果，隔离不同上游节点的具体结果类型。
    /// </summary>
    public sealed class ImageSaveJudgment
    {
        /// <summary>
        /// 无 NG 分类时复用的只读空集合，避免高频保存流程产生无意义的小对象。
        /// </summary>
        private static readonly ReadOnlyCollection<string> EmptyNgCategoryNames =
            new List<string>(0).AsReadOnly();

        /// <summary>
        /// NG 检测项分类的只读集合。
        /// </summary>
        private readonly ReadOnlyCollection<string> _ngCategoryNames;

        /// <summary>
        /// 获取当前订阅结果是否判定为 OK。
        /// </summary>
        public bool IsOk { get; private set; }

        /// <summary>
        /// 获取需要单独保存图片的 NG 检测项分类。
        /// </summary>
        public IReadOnlyList<string> NgCategoryNames
        {
            get { return _ngCategoryNames; }
        }

        /// <summary>
        /// 获取没有 NG 检测项分类时是否仍保存到通用 NG 目录。
        /// </summary>
        public bool UseGenericNgDirectoryWhenNoCategory { get; private set; }

        /// <summary>
        /// 初始化统一保存判定结果。
        /// </summary>
        /// <param name="isOk">当前订阅结果是否为 OK。</param>
        /// <param name="ngCategoryNames">需要单独保存的 NG 检测项分类。</param>
        /// <param name="useGenericNgDirectoryWhenNoCategory">没有分类时是否保存到通用 NG 目录。</param>
        public ImageSaveJudgment(
            bool isOk,
            IEnumerable<string> ngCategoryNames,
            bool useGenericNgDirectoryWhenNoCategory)
        {
            IsOk = isOk;
            UseGenericNgDirectoryWhenNoCategory = useGenericNgDirectoryWhenNoCategory;
            if (ngCategoryNames == null)
            {
                _ngCategoryNames = EmptyNgCategoryNames;
            }
            else
            {
                List<string> normalizedNames = ngCategoryNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                _ngCategoryNames = normalizedNames.Count == 0
                    ? EmptyNgCategoryNames
                    : normalizedNames.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// 定义单一订阅结果类型到统一保存判定结果的适配接口。
    /// </summary>
    public interface IImageSaveJudgmentAdapter
    {
        /// <summary>
        /// 尝试把订阅值转换为统一保存判定结果。
        /// </summary>
        /// <param name="subscriptionValue">订阅控件读取到的原始值。</param>
        /// <param name="judgment">转换成功后的统一保存判定结果。</param>
        /// <returns>当前适配器支持该订阅值类型时返回 true。</returns>
        bool TryResolve(object subscriptionValue, out ImageSaveJudgment judgment);
    }

    /// <summary>
    /// 定义保存图像判定结果解析器，便于后续注入新的结果类型适配器。
    /// </summary>
    public interface IImageSaveJudgmentResolver
    {
        /// <summary>
        /// 把订阅控件读取到的原始值解析为统一保存判定结果。
        /// </summary>
        /// <param name="subscriptionValue">订阅控件读取到的原始值。</param>
        /// <returns>统一保存判定结果。</returns>
        ImageSaveJudgment Resolve(object subscriptionValue);
    }

    /// <summary>
    /// 按已注册适配器顺序解析保存图像 OK/NG 订阅值。
    /// </summary>
    public sealed class ImageSaveJudgmentResolver : IImageSaveJudgmentResolver
    {
        /// <summary>
        /// 当前解析器使用的只读适配器集合。
        /// </summary>
        private readonly ReadOnlyCollection<IImageSaveJudgmentAdapter> _adapters;

        /// <summary>
        /// 使用 AI 算法结果和布尔结果两个默认适配器初始化解析器。
        /// </summary>
        public ImageSaveJudgmentResolver()
            : this(new IImageSaveJudgmentAdapter[]
            {
                new AlgorithmResultImageSaveJudgmentAdapter(),
                new BooleanImageSaveJudgmentAdapter()
            })
        {
        }

        /// <summary>
        /// 使用指定适配器集合初始化解析器，供后续可插拔扩展新的结果类型。
        /// </summary>
        /// <param name="adapters">保存判定结果适配器集合。</param>
        public ImageSaveJudgmentResolver(IEnumerable<IImageSaveJudgmentAdapter> adapters)
        {
            List<IImageSaveJudgmentAdapter> normalizedAdapters = (adapters ?? Enumerable.Empty<IImageSaveJudgmentAdapter>())
                .Where(adapter => adapter != null)
                .ToList();
            if (normalizedAdapters.Count == 0)
                throw new ArgumentException("保存图像判定解析器至少需要一个结果适配器！", "adapters");

            _adapters = normalizedAdapters.AsReadOnly();
        }

        /// <summary>
        /// 把订阅控件读取到的原始值解析为统一保存判定结果。
        /// </summary>
        /// <param name="subscriptionValue">订阅控件读取到的原始值。</param>
        /// <returns>统一保存判定结果。</returns>
        public ImageSaveJudgment Resolve(object subscriptionValue)
        {
            if (subscriptionValue == null)
                throw new Exception("订阅的 OK/NG 判定结果为空！");

            foreach (IImageSaveJudgmentAdapter adapter in _adapters)
            {
                ImageSaveJudgment judgment;
                if (adapter.TryResolve(subscriptionValue, out judgment))
                {
                    if (judgment == null)
                        throw new Exception(string.Format("保存图像判定适配器 {0} 返回了空结果！", adapter.GetType().Name));

                    return judgment;
                }
            }

            throw new InvalidCastException(
                string.Format(
                    "保存图像节点不支持订阅结果类型 {0}，请选择 AI 算法结果或多条件的条件结果！",
                    subscriptionValue.GetType().Name));
        }
    }

    /// <summary>
    /// 把 AI 算法结果适配为保存图像统一判定结果。
    /// </summary>
    internal sealed class AlgorithmResultImageSaveJudgmentAdapter : IImageSaveJudgmentAdapter
    {
        /// <summary>
        /// 尝试解析 AI 算法结果，并保留原有按 NG 检测项分类保存的行为。
        /// </summary>
        /// <param name="subscriptionValue">订阅控件读取到的原始值。</param>
        /// <param name="judgment">转换成功后的统一保存判定结果。</param>
        /// <returns>原始值为 AI 算法结果时返回 true。</returns>
        public bool TryResolve(object subscriptionValue, out ImageSaveJudgment judgment)
        {
            judgment = null;
            AlgorithmResult algorithmResult = subscriptionValue as AlgorithmResult;
            if (algorithmResult == null)
                return false;

            bool isOk = algorithmResult.DetectResults != null &&
                algorithmResult.DetectResults.Count > 0 &&
                algorithmResult.DetectResults.All(
                    pair => pair.Value != null &&
                    pair.Value.All(item => item != null && item.IsOk));

            List<string> ngCategoryNames = new List<string>();
            if (algorithmResult.DetectResults != null)
            {
                foreach (KeyValuePair<string, List<SingleDetectResult>> pair in algorithmResult.DetectResults)
                {
                    if (pair.Value == null || !pair.Value.TrueForAll(item => item != null && item.IsOk))
                        ngCategoryNames.Add(pair.Key);
                }
            }

            judgment = new ImageSaveJudgment(isOk, ngCategoryNames, false);
            return true;
        }
    }

    /// <summary>
    /// 把多条件节点输出的布尔结果适配为保存图像统一判定结果。
    /// </summary>
    internal sealed class BooleanImageSaveJudgmentAdapter : IImageSaveJudgmentAdapter
    {
        /// <summary>
        /// 多条件为 True 时复用的不可变 OK 判定对象。
        /// </summary>
        private static readonly ImageSaveJudgment BooleanOkJudgment =
            new ImageSaveJudgment(true, null, true);

        /// <summary>
        /// 多条件为 False 时复用的不可变 NG 判定对象。
        /// </summary>
        private static readonly ImageSaveJudgment BooleanNgJudgment =
            new ImageSaveJudgment(false, null, true);

        /// <summary>
        /// 尝试解析布尔结果，NG 时使用通用 NG 目录保存图片。
        /// </summary>
        /// <param name="subscriptionValue">订阅控件读取到的原始值。</param>
        /// <param name="judgment">转换成功后的统一保存判定结果。</param>
        /// <returns>原始值为布尔结果时返回 true。</returns>
        public bool TryResolve(object subscriptionValue, out ImageSaveJudgment judgment)
        {
            judgment = null;
            if (!(subscriptionValue is bool))
                return false;

            bool isOk = (bool)subscriptionValue;
            judgment = isOk ? BooleanOkJudgment : BooleanNgJudgment;
            return true;
        }
    }
}
