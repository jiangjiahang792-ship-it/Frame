using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal class NodeParamTDAI : INodeParam
    {
        /// <summary>
        /// 模型句柄
        /// </summary>
        [JsonIgnore]
        public IntPtr TreePredictHandle { get; set; }
        /// <summary>
        /// 订阅得节点文本（反序列化用）
        /// </summary>
        public string Text1 { get; set; }
        /// <summary>
        /// 订阅得节点属性文本（反序列化用）
        /// </summary>
        public string Text2 { get; set; }
        /// <summary>
        /// 标签文件（反序列化用）
        /// </summary>
        public string LabelPath { get; set; }
        /// <summary>
        /// 模型文件（反序列化用）
        /// </summary>
        public string ModelPath { get; set; }
        /// <summary>
        /// 模型类别（反序列化用）
        /// </summary>
        public string ModelType { get; set; }
        /// <summary>
        /// AI得分阈值
        /// </summary>
        public float ScoreThreshold { get; set; } = 0.5f;
        /// <summary>
        /// 抑制分数
        /// </summary>
        public float ScoreNMS { get; set; } = 0.5f;
    }
}
