using System;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._1_Acquisition.ImageSource3D;
using TDJS_Vision.Node._1_Acquisition.ImageShow3D;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;
using TDJS_Vision.Node._2_ImagePreprocessing.ImagePreprocess;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageSplit;
using TDJS_Vision.Node._3_Detection.BatteryEar;
using TDJS_Vision.Node._3_Detection.BinaryAnalysis;
using TDJS_Vision.Node._3_Detection.ColorDiscern;
using TDJS_Vision.Node._3_Detection.FindCircle;
using TDJS_Vision.Node._3_Detection.FindLine;
using TDJS_Vision.Node._3_Detection.LargeModel;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
using TDJS_Vision.Node._3_Detection.QRScan;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.Unsupervised;
using TDJS_Vision.Node._4_Measurement.CaliperCircle;
using TDJS_Vision.Node._4_Measurement.CaliperEllipse;
using TDJS_Vision.Node._4_Measurement.CaliperLine;
using TDJS_Vision.Node._4_Measurement.FindPoint;
using TDJS_Vision.Node._4_Measurement.LineLineAngle;
using TDJS_Vision.Node._4_Measurement.PointLineDistance;
using TDJS_Vision.Node._4_Measurement.PointPointDistance;
using TDJS_Vision.Node._4_Measurement.PointRegionDistance;
using TDJS_Vision.Node._4_Measurement.PositionCorrection;
using TDJS_Vision.Node._5_EquipmentCommunication.AIResultSend;
using TDJS_Vision.Node._5_EquipmentCommunication.CameraIO;
using TDJS_Vision.Node._5_EquipmentCommunication.CamerIOImprovement;
using TDJS_Vision.Node._5_EquipmentCommunication.ComSend;
using TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO;
using TDJS_Vision.Node._5_EquipmentCommunication.LightOpen;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusSoftTrigger;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;
using TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte;
using TDJS_Vision.Node._5_EquipmentCommunication.TcpClient;
using TDJS_Vision.Node._5_EquipmentCommunication.TcpServer;
using TDJS_Vision.Node._6_LogicTool.ArithmeticOperation;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Node._6_LogicTool.ConditionRun;
using TDJS_Vision.Node._6_LogicTool.CSharpScript;
using TDJS_Vision.Node._6_LogicTool.Else;
using TDJS_Vision.Node._6_LogicTool.EndIf;
using TDJS_Vision.Node._6_LogicTool.If;
using TDJS_Vision.Node._6_LogicTool.MessageBox;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._6_LogicTool.ProcessSignal;
using TDJS_Vision.Node._6_LogicTool.ProcessTrigger;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;
using TDJS_Vision.Node._6_LogicTool.SleepTool;
using TDJS_Vision.Node._6_LogicTool.WaitProcessComplete;
using TDJS_Vision.Node._7_ResultProcessing.DataShow;
using TDJS_Vision.Node._7_ResultProcessing.GenerateExcelSpreadsheet;
using TDJS_Vision.Node._7_ResultProcessing.ImageDelete;
using TDJS_Vision.Node._7_ResultProcessing.ImageDraw;
using TDJS_Vision.Node._7_ResultProcessing.ImageSave;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2;
using TDJS_Vision.Node._7_ResultProcessing.ResultSummarize;
using TDJS_Vision.Node._8_GeometryCreation.LineMergeFit;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 流程节点实例工厂，集中维护节点类型到节点控件类的创建关系。
    /// </summary>
    public static class NodeFactory
    {
        /// <summary>
        /// 根据节点类型创建对应的节点实例。
        /// </summary>
        /// <param name="nodeId">节点 ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">节点所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        /// <returns>创建好的节点实例。</returns>
        public static NodeBase CreateNode(int nodeId, string nodeName, Process process, NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.LightSourceControl:
                    return new NodeLight(nodeId, nodeName, process, nodeType);
                case NodeType.PLCRead:
                    return new NodePlcRead(nodeId, nodeName, process, nodeType);
                case NodeType.PLCWrite:
                    return new NodePlcWrite(nodeId, nodeName, process, nodeType);
                case NodeType.AITD:
                    return new NodeTDAI(nodeId, nodeName, process, nodeType);
                case NodeType.UnsupervisedDetection:
                    return new NodeUnsupervisedDetection(nodeId, nodeName, process, nodeType);
                case NodeType.LargeModelDetection:
                    return new NodeLargeModelDetection(nodeId, nodeName, process, nodeType);
                case NodeType.ImageSave:
                    return new NodeImageSave(nodeId, nodeName, process, nodeType);
                case NodeType.SleepTool:
                    return new NodeSleepTool(nodeId, nodeName, process, nodeType);
                case NodeType.WaitSoftTrigger:
                    return new NodeWaitSoftTrigger(nodeId, nodeName, process, nodeType);
                case NodeType.DetectResultShow:
                    return new NodeDataShow(nodeId, nodeName, process, nodeType);
                case NodeType.Summarize:
                    return new NodeSummarize(nodeId, nodeName, process, nodeType);
                case NodeType.LineFind:
                    return new NodeFIndLine(nodeId, nodeName, process, nodeType);
                case NodeType.CircleFind:
                    return new NodeFIndCircle(nodeId, nodeName, process, nodeType);
                case NodeType.CaliperLine:
                    return new NodeCaliperLine(nodeId, nodeName, process, nodeType);
                case NodeType.CaliperCircle:
                    return new NodeCaliperCircle(nodeId, nodeName, process, nodeType);
                case NodeType.CaliperEllipse:
                    return new NodeCaliperEllipse(nodeId, nodeName, process, nodeType);
                case NodeType.FindPoint:
                    return new NodeFindPoint(nodeId, nodeName, process, nodeType);
                case NodeType.PositionCorrection:
                    return new NodePositionCorrection(nodeId, nodeName, process, nodeType);
                case NodeType.LineLineAngle:
                    return new NodeLineLineAngle(nodeId, nodeName, process, nodeType);
                case NodeType.PointPointDistance:
                    return new NodePointPointDistance(nodeId, nodeName, process, nodeType);
                case NodeType.PointLineDistance:
                    return new NodePointLineDistance(nodeId, nodeName, process, nodeType);
                case NodeType.PointRegionDistance:
                    return new NodePointRegionDistance(nodeId, nodeName, process, nodeType);
                case NodeType.LineMergeFit:
                    return new NodeLineMergeFit(nodeId, nodeName, process, nodeType);
                case NodeType.ImageCrop:
                    return new NodeImageCrop(nodeId, nodeName, process, nodeType);
                case NodeType.ImageShow:
                    return new NodeImageShow(nodeId, nodeName, process, nodeType);
                case NodeType.ImageShow3D:
                    return new NodeImageShow3D(nodeId, nodeName, process, nodeType);
                case NodeType.ModbusRead:
                    return new NodeModbusRead(nodeId, nodeName, process, nodeType);
                case NodeType.ModbusWrite:
                    return new NodeModbusWrite(nodeId, nodeName, process, nodeType);
                case NodeType.TCPClientRequest:
                    return new NodeTCPClient(nodeId, nodeName, process, nodeType);
                case NodeType.TCPServerResponse:
                    return new NodeTCPServer(nodeId, nodeName, process, nodeType);
                case NodeType.ImageRotate:
                    return new NodeImageRotate(nodeId, nodeName, process, nodeType);
                case NodeType.ModbusSoftTrigger:
                    return new NodeModbusSoftTrigger(nodeId, nodeName, process, nodeType);
                case NodeType.AIResultSend:
                    return new NodeSignalSend(nodeId, nodeName, process, nodeType);
                case NodeType.CameraIO:
                    return new NodeCameraIO(nodeId, nodeName, process, nodeType);
                case NodeType.ImageSource:
                    return new NodeImageSource(nodeId, nodeName, process, nodeType);
                case NodeType.ImageSource3D:
                    return new NodeImageSource3D(nodeId, nodeName, process, nodeType);
                case NodeType.ImageSplit:
                    return new NodeImageSplit(nodeId, nodeName, process, nodeType);
                case NodeType.ImagePreprocess:
                    return new NodeImagePreprocess(nodeId, nodeName, process, nodeType);
                case NodeType.QRScan:
                    return new NodeQRScan(nodeId, nodeName, process, nodeType);
                case NodeType.MatchTemplate:
                    return new NodeMatchTemplate(nodeId, nodeName, process, nodeType);
                case NodeType.ImageFileDelete:
                    return new NodeImageDelete(nodeId, nodeName, process, nodeType);
                case NodeType.SharedVariable:
                    return new NodeSharedVariable(nodeId, nodeName, process, nodeType);
                case NodeType.GenerateExcel:
                    return new NodeGenerateExcel(nodeId, nodeName, process, nodeType);
                case NodeType.DrawAIResult:
                    return new NodeImageDraw(nodeId, nodeName, process, nodeType);
                case NodeType.ResultOverlayDraw:
                    return new NodeResultOverlayDraw(nodeId, nodeName, process, nodeType);
                case NodeType.ResultOverlayDraw2:
                    return new NodeResultOverlayDraw2(nodeId, nodeName, process, nodeType);
                case NodeType.ConditionRun:
                    return new NodeConditionRun(nodeId, nodeName, process, nodeType);
                case NodeType.ProcessTrigger:
                    return new NodeProcessTrigger(nodeId, nodeName, process, nodeType);
                case NodeType.ProcessSignal:
                    return new NodeProcessSignal(nodeId, nodeName, process, nodeType);
                case NodeType.If:
                    return new NodeIf(nodeId, nodeName, process, nodeType);
                case NodeType.MultiCondition:
                    return new NodeMultiCondition(nodeId, nodeName, process, nodeType);
                case NodeType.ArithmeticOperation:
                    return new NodeArithmeticOperation(nodeId, nodeName, process, nodeType);
                case NodeType.CompositeModule:
                    return new NodeCompositeModule(nodeId, nodeName, process, nodeType);
                case NodeType.CompositeInput:
                    return new NodeCompositeInput(nodeId, nodeName, process, nodeType);
                case NodeType.CompositeOutput:
                    return new NodeCompositeOutput(nodeId, nodeName, process, nodeType);
                case NodeType.Else:
                    return new NodeElse(nodeId, nodeName, process, nodeType);
                case NodeType.EndIf:
                    return new NodeEndIf(nodeId, nodeName, process, nodeType);
                case NodeType.BatteryEar:
                    return new NodeBatteryEar(nodeId, nodeName, process, nodeType);
                case NodeType.CSharpScript:
                    return new NodeCSharpScript(nodeId, nodeName, process, nodeType);
                case NodeType.ComSend:
                    return new NodeComSend(nodeId, nodeName, process, nodeType);
                case NodeType.WaitProcessComplete:
                    return new NodeWaitProcessComplete(nodeId, nodeName, process, nodeType);
                case NodeType.MessageBox:
                    return new NodeMessageBox(nodeId, nodeName, process, nodeType);
                case NodeType.ERUIIO:
                    return new NodeERUIIO(nodeId, nodeName, process, nodeType);
                case NodeType.RGBDiscern:
                    return new NodeColorDiscern(nodeId, nodeName, process, nodeType);
                case NodeType.BinarizationAnalysis:
                    return new NodeBinaryAnalysis(nodeId, nodeName, process, nodeType);
                case NodeType.CameraIOManual:
                    return new NodeCameraIOManual(nodeId, nodeName, process, nodeType);
                case NodeType.ReadFlag:
                    return new NodeFlagRead(nodeId, nodeName, process, nodeType); 
                default:
                    throw new NotSupportedException("未支持的节点类型：" + nodeType);
            }
        }
    }
}
