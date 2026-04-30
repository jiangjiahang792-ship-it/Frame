using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>
    /// TDAI类
    /// </summary>
    public class TDAI
    {
        /// <summary>
        /// 用于程序全局访问所有检测检测项
        /// </summary>
        public static Dictionary<string, List<DetectItemInfo>> DetectItemMap = new Dictionary<string, List<DetectItemInfo>>();
    }

    /// <summary>
    /// AI输入信息
    /// </summary>
    public class AIInputInfo
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        public string ModelName { get; set; }
        /// <summary>
        /// 模型大小
        /// </summary>
        public int ModelSize { get; set; }
        /// <summary>
        /// 置信度阈值
        /// </summary>
        public float ScoreThreshold { get; set; } = 0.5f;
        /// <summary>
        /// NMS阈值
        /// </summary>
        public float ScoreNMS { get; set; } = 0.5f;
        /// <summary>
        /// 模型信息
        /// </summary>
        public ModelInfo ModelInfo { get; set; }
        /// <summary>
        /// 类别名称列表
        /// </summary>
        public List<string> ClassNames { get; set; } = new List<string>();
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public AIInputInfo() { }
        /// <summary>
        /// 保存当前JSON对象到指定路径
        /// </summary>
        /// <param name="path"></param>
        public void Save(string path)
        {
            // 序列化当前对象
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            // 可选：保存到文件
            System.IO.File.WriteAllText(path, json);
        }
    }

    /// <summary>
    /// 检测项信息
    /// </summary>
    public class DetectItemInfo
    {
        /// <summary>
        /// 检测项名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 检测项值的下限
        /// </summary>
        public string MinValue { get; set; }
        /// <summary>
        /// 检测项值的上限
        /// </summary>
        public string MaxValue { get; set; }
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enable { get; set; }
        /// <summary>
        /// 是否为统计“数量型”检测项
        /// </summary>
        public bool IsCountItem { get; set; }
        /// <summary>
        /// 当前检测项的值
        /// </summary>
        public string CurValue { get; set; }
    }

    /// <summary>
    /// AI模型信息类
    /// </summary>
    public struct ModelInfo
    {
        /// <summary>
        /// 模型类型
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ModelType ModelType { get; set; }
        /// <summary>
        /// 设备类型
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DeviceType DeviceType { get; set; }
        /// <summary>
        /// 是否是加密模型
        /// </summary>
        public bool IsEncrypted { get; set; }
        /// <summary>
        /// 加密模型路径文件
        /// </summary>
        public string ModelPath { get; set; }
        /// <summary>
        /// 姿态估计的关键点数量,仅在ModelType为POSE时使用
        /// </summary>
        public int KeyPointNum { get; set; }
    }

    /// <summary>
    /// AI模型类型
    /// </summary>
    public enum ModelType
    {
        /// <summary>
        /// 目标检测
        /// </summary>
        DET,
        /// <summary>
        /// 旋转边框
        /// </summary>
        OBB,
        /// <summary>
        /// 像素分割
        /// </summary>
        SEG,
        /// <summary>
        /// 姿态估计
        /// </summary>
        POSE
    }
    /// <summary>
    /// AI推理的设备类型
    /// </summary>
    public enum DeviceType 
    {
        CPU,
        GPU
    }

}
