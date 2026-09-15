using System.Collections.Generic;
using System.Linq;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision
{
    /// <summary>
    /// 表示一个会阻止流程启动的相机触发配置错误。
    /// </summary>
    public sealed class ProcessCameraConfigurationError
    {
        /// <summary>
        /// 创建相机触发配置错误。
        /// </summary>
        public ProcessCameraConfigurationError(
            string processName,
            int nodeId,
            string nodeName,
            TriggerSource triggerSource)
        {
            ProcessName = processName;
            NodeId = nodeId;
            NodeName = nodeName;
            TriggerSource = triggerSource;
        }

        /// <summary>
        /// 配置错误所属流程名称。
        /// </summary>
        public string ProcessName { get; }

        /// <summary>
        /// 配置错误所属图像源节点编号。
        /// </summary>
        public int NodeId { get; }

        /// <summary>
        /// 配置错误所属图像源节点名称。
        /// </summary>
        public string NodeName { get; }

        /// <summary>
        /// 当前图像源使用的触发源。
        /// </summary>
        public TriggerSource TriggerSource { get; }

        /// <summary>
        /// 生成供界面展示的简体中文错误内容。
        /// </summary>
        public string ToDisplayText()
        {
            return $"流程【{ProcessName}】的图像源节点({NodeId}.{NodeName})使用触发源 {TriggerSource}，但该节点存在上游节点。硬触发图像源不能存在上游节点。";
        }
    }

    /// <summary>
    /// 定义流程运行前相机触发配置校验接口。
    /// </summary>
    public interface IProcessCameraConfigurationValidator
    {
        /// <summary>
        /// 校验指定流程集合中的相机图像源配置。
        /// </summary>
        IReadOnlyList<ProcessCameraConfigurationError> Validate(IEnumerable<Process> processes);
    }

    /// <summary>
    /// 校验硬触发图像源不得存在有效上游节点的默认实现。
    /// </summary>
    public sealed class ProcessCameraConfigurationValidator : IProcessCameraConfigurationValidator
    {
        /// <summary>
        /// 校验指定流程集合中的相机图像源配置。
        /// </summary>
        public IReadOnlyList<ProcessCameraConfigurationError> Validate(IEnumerable<Process> processes)
        {
            List<ProcessCameraConfigurationError> errors = new List<ProcessCameraConfigurationError>();
            if (processes == null)
                return errors;

            foreach (Process process in processes)
            {
                if (process == null || !process.Enable || process.Nodes == null)
                    continue;

                foreach (NodeBase node in process.Nodes)
                {
                    NodeImageSource imageSource = node as NodeImageSource;
                    if (imageSource == null || !imageSource.Active)
                        continue;

                    NodeParamImageSoucre param = imageSource.ParamForm?.Params as NodeParamImageSoucre;
                    if (param == null ||
                        param.ImageSource != "相机" ||
                        IsSoftwareTriggerSource(param.TriggerSource) ||
                        !HasActiveUpstreamNode(process, imageSource))
                    {
                        continue;
                    }

                    errors.Add(new ProcessCameraConfigurationError(
                        process.ProcessName,
                        imageSource.ID,
                        imageSource.NodeName,
                        param.TriggerSource));
                }
            }

            return errors;
        }

        /// <summary>Auto沿用参数界面的软触发默认语义，只有明确线路源属于硬触发。</summary>
        private static bool IsSoftwareTriggerSource(TriggerSource triggerSource)
        {
            return triggerSource == TriggerSource.Auto || triggerSource == TriggerSource.SOFT;
        }

        /// <summary>
        /// 判断指定图像源在画布图或旧顺序流程中是否存在有效上游节点。
        /// </summary>
        private static bool HasActiveUpstreamNode(Process process, NodeImageSource imageSource)
        {
            if (process.HasCanvasGraph)
                return process.GetUpstreamNodes(imageSource).Any(node => node != null && node.Active);

            int imageIndex = process.Nodes.IndexOf(imageSource);
            return imageIndex > 0 &&
                process.Nodes.Take(imageIndex).Any(node => node != null && node.Active);
        }
    }
}
