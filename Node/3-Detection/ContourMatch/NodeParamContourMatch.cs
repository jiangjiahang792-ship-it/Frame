using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TDJS_Vision.Node._3_Detection.MatchTemplate;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>可随方案完整保存的轮廓匹配参数；不依赖桌面Demo及外部图片路径。</summary>
    public sealed class NodeParamContourMatch : INodeParam
    {
        /// <summary>订阅节点显示文本，与框架连接依赖识别保持一致。</summary>
        public string Text1 { get; set; }
        /// <summary>订阅的输出图像显示名称。</summary>
        public string Text2 { get; set; }
        /// <summary>订阅源的稳定节点编号。</summary>
        public int SourceNodeId { get; set; } = -1;
        /// <summary>完整来源图PNG快照，保留ROI边界的原始建模上下文。</summary>
        public byte[] ModelImageBytes { get; set; }
        /// <summary>已确认的模板ROI，使用来源图像素坐标。</summary>
        public Rectangle ModelRoi { get; set; }
        /// <summary>已确认的组合区域；null兼容旧矩形模板，空列表不代表全图。</summary>
        public List<TemplateRegion> ModelRegions { get; set; }
        /// <summary>界面当前创建参数；修改后仅在重新创建模型时生效。</summary>
        public CreateModelOptions CreateOptions { get; set; } = new CreateModelOptions { AutoContrast = true };
        /// <summary>已确认模板实际使用的创建参数，避免未重建的参数改变现有模型。</summary>
        public CreateModelOptions ModelOptions { get; set; }
        /// <summary>按确认顺序保存删除遮罩，恢复时重放原Demo每次删除行为。</summary>
        public List<byte[]> EraseMasks { get; set; } = new List<byte[]>();
        /// <summary>当前完整运行参数。</summary>
        public FindOptions FindOptions { get; set; } = new FindOptions();
        /// <summary>画笔直径，保持Demo编辑体验。</summary>
        public int BrushSize { get; set; } = 16;

        /// <summary>受管理的特征模板；空列表明确表示无模板，null表示旧版单模板方案。</summary>
        public List<ContourTemplateDefinition> Templates { get; set; }
        /// <summary>是否搜索整幅输入图像。</summary>
        public bool AllSearch { get; set; } = true;
        /// <summary>输入图像坐标系中的固定矩形搜索范围。</summary>
        public Rectangle SearchRegion { get; set; }

        /// <summary>读取多模板列表，兼容第一次集成保存的单模板方案。</summary>
        internal List<ContourTemplateDefinition> GetTemplates()
        {
            if (Templates != null) return Templates;
            if (ModelOptions == null || ModelImageBytes == null) return new List<ContourTemplateDefinition>();
            return new List<ContourTemplateDefinition> { ContourTemplateDefinition.FromModel(this, "模板1", "legacy-1") };
        }

        /// <summary>深复制所有可修改数据；用于参数变更、交换和恢复，不在每帧复制图像。</summary>
        public NodeParamContourMatch Copy()
        {
            return new NodeParamContourMatch { Text1 = Text1, Text2 = Text2, SourceNodeId = SourceNodeId,
                Templates = Templates?.Select(item => item.Copy()).ToList(), AllSearch = AllSearch, SearchRegion = SearchRegion,
                ModelImageBytes = ModelImageBytes == null ? null : (byte[])ModelImageBytes.Clone(), ModelRoi = ModelRoi,
                ModelRegions = ModelRegions?.Select(region => region.Copy()).ToList(),
                CreateOptions = CopyCreate(CreateOptions), ModelOptions = CopyCreate(ModelOptions),
                FindOptions = CopyFind(FindOptions), BrushSize = BrushSize,
                EraseMasks = (EraseMasks ?? new List<byte[]>()).Select(mask => mask == null ? null : (byte[])mask.Clone()).ToList() };
        }

        /// <summary>复制创建参数，保持建模快照不受控件修改影响。</summary>
        internal static CreateModelOptions CopyCreate(CreateModelOptions value)
        {
            if (value == null) return null;
            return new CreateModelOptions { AutoContrast = value.AutoContrast, PyramidLevels = value.PyramidLevels,
                AngleStartDegrees = value.AngleStartDegrees, AngleEndDegrees = value.AngleEndDegrees,
                AngleStepDegrees = value.AngleStepDegrees, Contrast = value.Contrast, MinimumContrast = value.MinimumContrast,
                FeatureCount = value.FeatureCount, Metric = value.Metric };
        }

        /// <summary>复制搜索参数。</summary>
        internal static FindOptions CopyFind(FindOptions value)
        {
            if (value == null) return null;
            return new FindOptions { AngleStartDegrees = value.AngleStartDegrees, AngleEndDegrees = value.AngleEndDegrees,
                MinimumScore = value.MinimumScore, MaximumMatches = value.MaximumMatches, MaximumOverlap = value.MaximumOverlap,
                SubPixel = value.SubPixel, PyramidLevels = value.PyramidLevels, Mode = value.Mode };
        }
    }
}
