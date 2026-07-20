using Logger;
using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImagePreprocess
{
    /// <summary>
    /// 图像预处理节点，负责执行边缘增强、纹理滤波和中值滤波。
    /// </summary>
    public class NodeImagePreprocess : NodeBase
    {
        /// <summary>
        /// 初始化图像预处理节点。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeImagePreprocess(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormImagePreprocess(process, this);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImagePreprocess();
        }

        /// <summary>
        /// 运行图像预处理节点。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出运行日志。</param>
        /// <returns>节点运行返回标志。</returns>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                var form = ParamForm as NodeParamFormImagePreprocess;
                var param = ParamForm.Params as NodeParamImagePreprocess;
                if (form == null || param == null)
                    throw new Exception("图像预处理参数异常！");

                OutputImage inputImage = form.GetInputOutputImage();
                Mat inputMat = form.GetInputMat(inputImage);
                Mat processed = ImagePreprocessAlgorithm.Execute(inputMat, param);
                var result = new NodeResultImagePreprocess
                {
                    OutputImage = OutputImage.FromSingleImage(processed),
                    ModeName = NodeParamFormImagePreprocess.GetModeDisplayName(param.Mode)
                };

                int time = SetRunResult(startTime, NodeStatus.Successful);
                result.RunTime = time;
                Result = result;

                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，{result.ModeName})", true);

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                SetRunResult(startTime, NodeStatus.Unexecuted);
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }
    }
}
