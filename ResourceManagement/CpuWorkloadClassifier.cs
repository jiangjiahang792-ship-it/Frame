using TDJS_Vision.Node;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 默认CPU重任务分类器；只标记已确认的传统视觉、测量和图像绘制节点。
    /// </summary>
    public sealed class DefaultCpuWorkloadClassifier : ICpuWorkloadClassifier
    {
        /// <summary>
        /// 尝试读取指定节点的CPU工作类型。
        /// </summary>
        public bool TryClassify(NodeBase node, out CpuWorkloadKind workloadKind)
        {
            workloadKind = CpuWorkloadKind.TraditionalVisionAlgorithm;
            if (node == null)
                return false;

            switch (node.NodeType)
            {
                case NodeType.ImageCrop:
                case NodeType.GrayScale:
                case NodeType.BlobAnalysis:
                case NodeType.LineFind:
                case NodeType.CircleFind:
                case NodeType.CaliperLine:
                case NodeType.CaliperCircle:
                case NodeType.CaliperEllipse:
                case NodeType.FindPoint:
                case NodeType.PositionCorrection:
                case NodeType.TemplateMatch:
                case NodeType.ImageRotate:
                case NodeType.ImageSplit:
                case NodeType.QRScan:
                case NodeType.MatchTemplate:
                case NodeType.NccMatchTemplate:
                case NodeType.DrawAIResult:
                case NodeType.ResultOverlayDraw:
                case NodeType.ResultOverlayDraw2:
                case NodeType.BatteryEar:
                case NodeType.RGBDiscern:
                case NodeType.BinarizationAnalysis:
                case NodeType.LineLineAngle:
                case NodeType.PointPointDistance:
                case NodeType.PointLineDistance:
                case NodeType.PointRegionDistance:
                case NodeType.LineMergeFit:
                case NodeType.ImagePreprocess:
                    return true;
                default:
                    return false;
            }
        }
    }
}
