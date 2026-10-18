using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Basler.Pylon;
using Newtonsoft.Json;
using TDJS_Vision.Device;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>通信检测项触发值的读取类型，零值兼容旧方案。</summary>
    public enum TDAIReadType
    {
        /// <summary>读取一个无符号十六位整数。</summary>
        UInt16 = 0,
        /// <summary>读取连续寄存器中的字符串。</summary>
        String = 1,
        /// <summary>读取一个布尔值。</summary>
        Boolean = 2
    }

    /// <summary>通信检测项读取配置，随节点参数保存。</summary>
    public class TDAIReadOptions
    {
        /// <summary>读取类型，默认兼容原有无符号整数。</summary>
        public TDAIReadType ReadType { get; set; }
        /// <summary>字符串占用的寄存器个数，每个寄存器两个字节。</summary>
        public ushort RegisterCount { get; set; } = 10;
        /// <summary>字符串编码名称。</summary>
        public string EncodingName { get; set; } = "us-ascii";
        /// <summary>寄存器内是否低字节在前。</summary>
        public bool LowByteFirst { get; set; }

        /// <summary>通过设备接口读取触发值；同步通信不额外创建线程。</summary>
        public string Read(TDJS_Vision.Device.Modbus.IModbus device, string address)
        {
            switch (ReadType)
            {
                case TDAIReadType.UInt16:
                    ushort[] values = device.ReadUInt16(address, 1);
                    if (values == null || values.Length != 1)
                        throw new InvalidOperationException("通信读取无符号整数失败：返回数量不正确。");
                    return values[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
                case TDAIReadType.Boolean:
                    bool[] bits = device.ReadBool(address, 1);
                    if (bits == null || bits.Length != 1)
                        throw new InvalidOperationException("通信读取布尔值失败：返回数量不正确。");
                    return bits[0] ? "true" : "false";
                case TDAIReadType.String:
                    // 通信字符串仅去除首尾空白，中间空格保留，供检测项触发值匹配使用。
                    return TDJS_Vision.Device.Modbus.ModbusStringReader.ReadString(device, address, RegisterCount, EncodingName, LowByteFirst).Trim();
                default:
                    throw new InvalidOperationException("不支持的通信读取类型。");
            }
        }

        /// <summary>匹配触发值；布尔值兼容数字写法，字符串保留有效空格并区分大小写。</summary>
        public bool Matches(string configured, string actual)
        {
            if (ReadType != TDAIReadType.Boolean)
                return string.Equals(configured, actual, StringComparison.Ordinal);
            string value = configured == null ? "" : configured.Trim();
            if (value == "1") value = "true";
            if (value == "0") value = "false";
            return string.Equals(value, actual, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class NodeParamTDAI : INodeParam
    {
        /// <summary>通信读取配置，旧方案缺少该字段时使用无符号整数。</summary>
        public TDAIReadOptions CommunicationReadOptions { get; set; } = new TDAIReadOptions();
        /// <summary>
        /// 订阅的节点文本
        /// </summary>
        public string Text1 { get; set; }
        /// <summary>
        /// 订阅的节点
        /// </summary>
        public string Text2 { get; set; }
        /// <summary>
        /// 配置文件路径
        /// </summary>
        public string ConfigPath { get; set; }
        /// <summary>
        /// AI模型配置(不参与序列化)
        /// </summary>
        [JsonIgnore]
        public AIInputInfo AIInputInfo { get; set; }
        /// <summary>
        /// 是否使用固定的检测项
        /// </summary>
        public bool IsFixed { get; set; }
        /// <summary>
        /// 当前检测项名称
        /// </summary>
        public string CurDetectItemName { get; set; }
        /// <summary>
        /// 固定检测项名称
        /// </summary>
        public string DetectItemName1 { get; set; }
        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get; set; }
        /// <summary>
        /// 通过通信获取检测项的设备
        /// </summary>
        [JsonIgnore]
        public IDevice Device { get; set; }
        /// <summary>
        /// 被复制的检测项名称
        /// </summary>
        public string DetectItemName2 { get; set; }
        /// <summary>
        /// 是否一键学习
        /// </summary>
        public bool IsAutoStudy
        {
            get { return _isAutoStudy; }
            set { if (value && !_isAutoStudy) StudyGeneration++; _isAutoStudy = value; }
        }
        /// <summary>学习开关状态。</summary>
        private bool _isAutoStudy;
        /// <summary>本进程的学习轮次，用于区分取消后重新开始的学习。</summary>
        [JsonIgnore]
        public int StudyGeneration { get; private set; }
        /// <summary>
        /// 学习次数
        /// </summary>
        public int StudyNum { get; set; }
        /// <summary>
        /// 学习上下限比例
        /// </summary>
        public float StudyPercentage { get; set; }
        /// <summary>
        /// 是否需要转换
        /// </summary>
        public bool NeedConvert { get; set; }
        /// <summary>
        /// 比例尺（毫米每像素）
        /// </summary>
        public float Scale { get; set; }

        /// <summary>
        /// 当前已加载的AI模型句柄，不参与方案序列化。
        /// </summary>
        [JsonIgnore]
        public IYolo8 Yolo8 { get; set; }
        /// <summary>
        /// 当前AI模型加载任务，运行前用于等待异步加载完成。
        /// </summary>
        [JsonIgnore]
        public Task ModelLoadTask { get; set; } = Task.CompletedTask;
        /// <summary>
        /// 当前句柄对应的模型路径，用于防止配置变更后误用旧模型。
        /// </summary>
        [JsonIgnore]
        public string LoadedModelPath { get; set; }
        /// <summary>
        /// 最近一次模型加载失败原因，用于运行时报出明确诊断。
        /// </summary>
        [JsonIgnore]
        public string LastModelLoadError { get; set; }
        /// <summary>
        /// 模型加载版本号，用于避免旧的后台加载任务覆盖新配置。
        /// </summary>
        private int _modelLoadVersion;

        /// <summary>
        /// 生成新的模型加载版本号。
        /// </summary>
        /// <returns></returns>
        public int NextModelLoadVersion()
        {
            return Interlocked.Increment(ref _modelLoadVersion);
        }

        /// <summary>
        /// 判断指定版本是否仍是当前最新模型加载任务。
        /// </summary>
        /// <param name="modelLoadVersion"></param>
        /// <returns></returns>
        public bool IsCurrentModelLoadVersion(int modelLoadVersion)
        {
            return Volatile.Read(ref _modelLoadVersion) == modelLoadVersion;
        }

        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; set; }
        /// <summary>
        /// 模型名称
        /// </summary>
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ModelName ModelName { get; set; }

        /// <summary>
        /// TDAI检测项监听地址
        /// </summary>
        public string Adress { get; set; }

        /// <summary>
        /// TDAI通信检测项配置
        /// </summary>
        public BindingList<TDAICommuntionParam> TDAICommuntionParams { get; set; } = new BindingList<TDAICommuntionParam>();

        /// <summary>
        /// 获取或设置是否启用本节点内部 ROI 检测区域。
        /// </summary>
        public bool RoiEnable { get; set; }

        /// <summary>
        /// 获取或设置本节点内部配置的多个 ROI 检测区域。
        /// </summary>
        public List<TDAIRoiRegion> RoiRegions { get; set; } = new List<TDAIRoiRegion>();
    }

    /// <summary>
    /// AI检测节点内部使用的矩形 ROI 检测区域参数。
    /// </summary>
    public class TDAIRoiRegion
    {
        /// <summary>
        /// 获取或设置 ROI 名称，用于界面标签和日志定位。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置 ROI 中心点 X 坐标，单位为图像像素。
        /// </summary>
        public float CenterX { get; set; }

        /// <summary>
        /// 获取或设置 ROI 中心点 Y 坐标，单位为图像像素。
        /// </summary>
        public float CenterY { get; set; }

        /// <summary>
        /// 获取或设置 ROI 宽度，单位为图像像素。
        /// </summary>
        public float Width { get; set; }

        /// <summary>
        /// 获取或设置 ROI 高度，单位为图像像素。
        /// </summary>
        public float Height { get; set; }

        /// <summary>
        /// 获取或设置 ROI 角度，单位为度；运行时使用其外接矩形裁剪图像。
        /// </summary>
        public float Angle { get; set; }

        /// <summary>
        /// 创建当前 ROI 参数的独立副本。
        /// </summary>
        /// <returns>复制后的 ROI 参数。</returns>
        public TDAIRoiRegion Clone()
        {
            return new TDAIRoiRegion
            {
                Name = Name,
                CenterX = CenterX,
                CenterY = CenterY,
                Width = Width,
                Height = Height,
                Angle = Angle
            };
        }
    }

    /// <summary>
    /// AI模型名称枚举
    /// </summary>
    public enum ModelName
    {
        RL_12类线芯模型,
        RL_线芯截面,
        合压模型,
        XM_Fakra模型,
        多端子模型,
        /// <summary>
        /// 超声波焊接侧面三类检测：线芯、焊接区域和飞丝。
        /// </summary>
        超声波焊接侧面三类模型,
        /// <summary>AST工位1五类目标检测模型，输入尺寸为640。</summary>
        AST_工位1模型,
        /// <summary>AST工位2九类CPU目标检测模型，输入尺寸为640。</summary>
        AST_工位2模型,
        /// <summary>AST工位3三类CPU目标检测模型，输入尺寸为640。</summary>
        AST_工位3模型,
        /// <summary>AST工位1六类实例分割模型，输入尺寸为640。</summary>
        AST工位1分割模型,
        /// <summary>前后防水栓独立识别的新端子12类检测模型。</summary>
        新端子12类模型,
    }
}
