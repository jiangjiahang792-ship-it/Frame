
using System;
using System.IO.Ports;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 节点参数界面接口类
    /// </summary>
    public interface INodeParamForm
    {
        /// <summary>
        /// 节点运行参数
        /// </summary>
        INodeParam Params { get; set; }
        /// <summary>
        /// 给节点参数界面类设置所属的节点,需要订阅结果必须调用
        /// </summary>
        /// <param name="node"></param>
        void SetNodeBelong(NodeBase node);
        /// <summary>
        /// 将反序列化的参数设置到界面
        /// </summary>
        void SetParam2Form();
    }

    /// <summary>
    /// 节点参数接口类
    /// </summary>
    public interface INodeParam { }

    /// <summary>
    /// 节点运行结果接口类
    /// </summary>
    public interface INodeResult 
    {
        int RunTime { get; set; } // 运行时间
    }

    /// <summary>
    /// 可被后续条件节点回写判定状态的结果接口。
    /// </summary>
    public interface IJudgmentResult
    {
        /// <summary>
        /// 产品或几何结果的判定状态，默认 true，由多条件模块按条件结果改写。
        /// </summary>
        bool JudgeOk { get; set; }
    }

    /// <summary>
    /// 节点运行结果类
    /// </summary>
    public class NodeReturn
    {
        /// <summary>
        /// 节点运行标志，用来标记当前节点运行完后是否继续运行流程下一个节点
        /// </summary>
        public NodeRunFlag Flag;
        /// <summary>
        /// 当前流程下一个要运行的节点索引
        /// </summary>
        public int NextIndex = -1;
        /// <summary>
        /// 图执行模式下要跟随的输出分支。
        /// </summary>
        public ProcessConnectionBranch NextBranch = ProcessConnectionBranch.Default;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="nextIndex"></param>
        public NodeReturn(NodeRunFlag flag = NodeRunFlag.ContinueRun, int nextIndex = -1)
        {
            Flag = flag;
            NextIndex = nextIndex;
        }

        public NodeReturn(NodeRunFlag flag, ProcessConnectionBranch nextBranch)
        {
            Flag = flag;
            NextBranch = nextBranch;
        }
    }

    public enum NodeStatus
    {
        /// <summary>
        /// 未运行
        /// </summary>
        Unexecuted,
        /// <summary>
        /// 运行中
        /// </summary>
        Running,
        /// <summary>
        /// 运行成功
        /// </summary>
        Successful,
        /// <summary>
        /// 运行失败
        /// </summary>
        Failed
    }

    /// <summary>
    /// 用来控制当前节点运行完是否继续运行流程下一个节点
    /// </summary>
    public enum NodeRunFlag
    {
        ContinueRun,
        StopRun,
        /// <summary>
        /// 图执行模式下只停止当前节点的下游分支；旧顺序模式按提前结束流程处理。
        /// </summary>
        StopBranch
    }

    /// <summary>
    /// 并行批次运行前可预登记的信号等待节点接口，用于避免同一信号被并排节点抢先复位。
    /// </summary>
    public interface IParallelSignalWaitNode
    {
        /// <summary>
        /// 当前节点是否需要参与并行信号等待预登记。
        /// </summary>
        bool CanPrepareParallelSignalWait { get; }

        /// <summary>
        /// 可比较的等待信号键，同一键的并排节点会作为一组预登记。
        /// </summary>
        string ParallelSignalWaitKey { get; }

        /// <summary>
        /// 并行批次启动前登记一个等待名额。
        /// </summary>
        void BeginParallelSignalWait();

        /// <summary>
        /// 并行批次结束后释放未被节点运行消费的等待名额。
        /// </summary>
        void EndParallelSignalWait();
    }

    /// <summary>
    /// 被流程跳过执行的节点可以提前准备本次运行结果，供下游订阅读取当前批次数据。
    /// </summary>
    public interface ISkippedNodeRunResultProvider
    {
        /// <summary>
        /// 为当前流程批次准备跳过节点的运行结果。
        /// </summary>
        /// <param name="startTime">结果耗时统计起点。</param>
        /// <param name="message">准备失败或诊断信息。</param>
        /// <returns>准备成功返回 true。</returns>
        bool TryPrepareSkippedRunResult(DateTime startTime, out string message);
    }

    /// <summary>
    /// 节点运行状态码
    /// </summary>
    public enum NodeRunStatusCode
    {
        /// <summary>
        /// 成功
        /// </summary>
        OK,
        /// <summary>
        /// 未运行
        /// </summary>
        UNEXECUTED = 0x00099,
        /// <summary>
        /// 参数有误
        /// </summary>
        PARAM_ERROR,
        /// <summary>
        /// 超时
        /// </summary>
        TIMEOUT,
        /// <summary>
        /// 未知错误
        /// </summary>
        UNKNOW_ERROR
    }

    /// <summary>
    /// 节点类型
    /// </summary>
    public enum NodeType
    {
        UNKNOWN,
        /// <summary>
        /// 光源控制
        /// </summary>
        LightSourceControl,
        /// <summary>
        /// 软触发等待
        /// </summary>
        WaitSoftTrigger,
        /// <summary>
        /// 相机拍照
        /// </summary>
        CameraShot,
        /// <summary>
        /// 本地图像
        /// </summary>
        LocalPicture,
        /// <summary>
        /// PLC寄存器读取
        /// </summary>
        PLCRead,
        /// <summary>
        /// PLC寄存器写入
        /// </summary>
        PLCWrite,
        /// <summary>
        /// 瞳达AI节点
        /// </summary>
        AITD,
        /// <summary>
        /// 存图节点
        /// </summary>
        ImageSave,
        /// <summary>
        /// 图像显示
        /// </summary>
        ImageShow,
        /// <summary>
        /// 3D图像显示
        /// </summary>
        ImageShow3D,
        /// <summary>
        /// 延迟工具
        /// </summary>
        SleepTool,
        /// <summary>
        /// 检测结果显示
        /// </summary>
        DetectResultShow,
        /// <summary>
        /// 结果总判断
        /// </summary>
        Summarize,
        /// <summary>
        /// 图像裁剪
        /// </summary>
        ImageCrop,
        /// <summary>
        /// 灰度图像
        /// </summary>
        GrayScale,
        /// <summary>
        /// Blob分析
        /// </summary>
        BlobAnalysis,
        /// <summary>
        /// 直线查找
        /// </summary>
        LineFind,
        /// <summary>
        /// 圆查找
        /// </summary>
        CircleFind,
        /// <summary>
        /// 卡尺找线
        /// </summary>
        CaliperLine,
        /// <summary>
        /// 卡尺找圆
        /// </summary>
        CaliperCircle,
        /// <summary>
        /// 卡尺找椭圆
        /// </summary>
        CaliperEllipse,
        /// <summary>
        /// 找点
        /// </summary>
        FindPoint,
        /// <summary>
        /// 位置修正
        /// </summary>
        PositionCorrection,
        /// <summary>
        /// 模板匹配
        /// </summary>
        TemplateMatch,
        /// <summary>
        /// ModbusRead读取
        /// </summary>
        ModbusRead,
        /// <summary>
        /// 写入
        /// </summary>
        ModbusWrite,
        /// <summary>
        /// TCP客户端请求
        /// </summary>
        TCPClientRequest,
        /// <summary>
        /// TCP服务器响应
        /// </summary>
        TCPServerResponse,
        /// <summary>
        /// 图像旋转节点
        /// </summary>
        ImageRotate,
        /// <summary>
        /// Modbus软触发
        /// </summary>
        ModbusSoftTrigger,
        /// <summary>
        /// 发送AI结果
        /// </summary>
        AIResultSend,
        /// <summary>
        /// 相机IO
        /// </summary>
        CameraIO,
        /// <summary>
        /// 图像源
        /// </summary>
        ImageSource,
        /// <summary>
        /// 3D图像源
        /// </summary>
        ImageSource3D,
        /// <summary>
        /// 图像分割
        /// </summary>
        ImageSplit,
        /// <summary>
        /// 二维码识别
        /// </summary>
        QRScan,
        /// <summary>
        /// 模版匹配
        /// </summary>
        MatchTemplate,
        /// <summary>
        /// NCC模板匹配
        /// </summary>
        NccMatchTemplate,
        /// <summary>
        /// 图片定时删除
        /// </summary>
        ImageFileDelete,
        /// <summary>
        /// 共享变量
        /// </summary>
        SharedVariable,
        /// <summary>
        /// 生成Excel表格
        /// </summary>
        GenerateExcel,
        /// <summary>
        /// AI结果绘制
        /// </summary>
        DrawAIResult,
        /// <summary>
        /// ROI结果绘制
        /// </summary>
        ResultOverlayDraw,
        /// <summary>
        /// 条件运行
        /// </summary>
        ConditionRun,
        /// <summary>
        /// 触发流程
        /// </summary>
        ProcessTrigger,
        /// <summary>
        /// 流程信号
        /// </summary>
        ProcessSignal,
        /// <summary>
        /// IF节点
        /// </summary>
        If,
        /// <summary>
        /// 多条件判断节点
        /// </summary>
        MultiCondition,
        /// <summary>
        /// Else节点
        /// </summary>
        Else,
        /// <summary>
        /// EndIf节点
        /// </summary>
        EndIf,
        /// <summary>
        /// 锂电池极耳检测
        /// </summary>
        BatteryEar,
        /// <summary>
        /// C#脚本
        /// </summary>
        CSharpScript,
        /// <summary>
        /// 串口发送
        /// </summary>
        ComSend,
        /// <summary>
        /// 等待流程完成
        /// </summary>
        WaitProcessComplete,
        /// <summary>
        /// 弹窗
        /// </summary>
        MessageBox,
        /// <summary>
        /// 科锐ERUI IO
        /// </summary>
        ERUIIO,
        /// <summary>
        /// RGB识别
        /// </summary>
        RGBDiscern,
        /// <summary>
        /// 二值化分析
        /// </summary>
        BinarizationAnalysis,
        /// <summary>
        /// 相机IO手动控制
        /// </summary>
        CameraIOManual,
        /// <summary>
        /// 监听信号读取
        /// </summary>
        ReadFlag,
        /// <summary>
        /// 线到线夹角
        /// </summary>
        LineLineAngle,
        /// <summary>
        /// 点到点距离
        /// </summary>
        PointPointDistance,
        /// <summary>
        /// 点到线距离
        /// </summary>
        PointLineDistance,
        /// <summary>
        /// 点到区域距离
        /// </summary>
        PointRegionDistance,
        /// <summary>
        /// 线组合拟合
        /// </summary>
        LineMergeFit,
        /// <summary>
        /// 四则运算
        /// </summary>
        ArithmeticOperation,
        /// <summary>
        /// 组合模块
        /// </summary>
        CompositeModule,
        /// <summary>
        /// 组合输入
        /// </summary>
        CompositeInput,
        /// <summary>
        /// 组合输出
        /// </summary>
        CompositeOutput,
        /// <summary>
        /// ROI结果绘制2
        /// </summary>
        ResultOverlayDraw2,
        /// <summary>
        /// 图像预处理
        /// </summary>
        ImagePreprocess,
        /// <summary>
        /// 无监督检测
        /// </summary>
        UnsupervisedDetection,
        /// <summary>
        /// 大模型调用
        /// </summary>
        LargeModelDetection,
        /// <summary>
        /// 相机曝光增益
        /// </summary>
        CameraExposureGain,
        /// <summary>将上游基础结果写入PLC或Modbus设备。</summary>
        ResultSend,
        /// <summary>保留原生Demo创建与搜索参数的轮廓模板匹配。</summary>
        ContourMatch,
    }
}
