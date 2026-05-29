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
    public class NodeParamTDAI : INodeParam
    {
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
        public bool IsAutoStudy { get; set; }
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
    }
}
