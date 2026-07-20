using System;
using System.Collections.Generic;
using System.Drawing;
using OpenCvSharp;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 大模型训练图片在检查页中的分类。
    /// </summary>
    public enum LargeModelImageCategory
    {
        /// <summary>
        /// 全部图片。
        /// </summary>
        All,

        /// <summary>
        /// 正常图片。
        /// </summary>
        OK,

        /// <summary>
        /// 异常图片。
        /// </summary>
        NG
    }

    /// <summary>
    /// 大模型训练界面中的单张图片信息。
    /// </summary>
    public sealed class LargeModelImageItem : IDisposable
    {
        /// <summary>
        /// 图片完整路径。
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 图片显示名称。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 图片分类。
        /// </summary>
        public LargeModelImageCategory Category { get; set; }

        /// <summary>
        /// 检查页缩略图。
        /// </summary>
        public Bitmap Thumbnail { get; set; }

        /// <summary>
        /// 释放缩略图对象。
        /// </summary>
        public void Dispose()
        {
            if (Thumbnail != null)
            {
                Thumbnail.Dispose();
                Thumbnail = null;
            }
        }
    }

    /// <summary>
    /// 大模型训练请求参数。
    /// </summary>
    public sealed class LargeModelTrainingRequest
    {
        /// <summary>
        /// 模板名称。
        /// </summary>
        public string TemplateName { get; set; }

        /// <summary>
        /// 图片根目录。
        /// </summary>
        public string SourceFolder { get; set; }

        /// <summary>
        /// 输出模板文件路径。
        /// </summary>
        public string OutputTemplatePath { get; set; }

        /// <summary>
        /// 参与训练的图片集合。
        /// </summary>
        public IReadOnlyList<LargeModelImageItem> Images { get; set; }

        /// <summary>
        /// 模型类别，当前固定为 DINOv2，预留给后续可插拔算法。
        /// </summary>
        public string ModelType { get; set; }

        /// <summary>
        /// TensorRT 模型精度，GPU 模式下支持 fp16 或 fp32。
        /// </summary>
        public string Precision { get; set; }

        /// <summary>
        /// 图像级异常阈值，0 表示使用 bank 中自动阈值。
        /// </summary>
        public float ImageThreshold { get; set; }

        /// <summary>
        /// 异常框最小面积阈值，单位为像素。
        /// </summary>
        public int AreaThreshold { get; set; }

        /// <summary>
        /// 模型输入宽度。
        /// </summary>
        public int InputWidth { get; set; }

        /// <summary>
        /// 模型输入高度。
        /// </summary>
        public int InputHeight { get; set; }

        /// <summary>
        /// 运行设备，训练界面按 C# demo 提供 GPU、CPU。
        /// </summary>
        public string Device { get; set; }

        /// <summary>
        /// 可选 ROI；为空时按整图训练。
        /// </summary>
        public Rect? RoiRect { get; set; }
    }

    /// <summary>
    /// 大模型训练输出结果。
    /// </summary>
    public sealed class LargeModelTrainingResult
    {
        /// <summary>
        /// 打包后的模板路径。
        /// </summary>
        public string TemplatePath { get; set; }

        /// <summary>
        /// 原始 memory bank 路径。
        /// </summary>
        public string BankPath { get; set; }

        /// <summary>
        /// OK 训练图片数量。
        /// </summary>
        public int OkCount { get; set; }

        /// <summary>
        /// NG 校准图片数量。
        /// </summary>
        public int NgCount { get; set; }
    }

    /// <summary>
    /// 大模型运行环境检查结果。
    /// </summary>
    public sealed class LargeModelRuntimeStatus
    {
        /// <summary>
        /// 环境是否可用于训练或推理。
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// 运行环境根目录。
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// 状态提示。
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// 大模型模板文件中的元数据清单。
    /// </summary>
    public sealed class LargeModelTemplateManifest
    {
        /// <summary>
        /// 模板格式版本。
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// 模板名称。
        /// </summary>
        public string TemplateName { get; set; }

        /// <summary>
        /// 创建时间。
        /// </summary>
        public string CreatedAt { get; set; }

        /// <summary>
        /// 模型类别。
        /// </summary>
        public string ModelType { get; set; }

        /// <summary>
        /// 模型精度。
        /// </summary>
        public string Precision { get; set; }

        /// <summary>
        /// native 设备模式，0 自动、1 GPU TensorRT、2 CPU ONNX。
        /// </summary>
        public int DeviceMode { get; set; }

        /// <summary>
        /// 训练设备文本。
        /// </summary>
        public string Device { get; set; }

        /// <summary>
        /// 模板包内 native 模型文件名。
        /// </summary>
        public string ModelFileName { get; set; }

        /// <summary>
        /// 模板包内 memory bank 文件名。
        /// </summary>
        public string BankFileName { get; set; }

        /// <summary>
        /// 图像级异常阈值，0 表示使用 bank 自动阈值。
        /// </summary>
        public float ImageThreshold { get; set; }

        /// <summary>
        /// 异常框最小面积阈值，单位为像素。
        /// </summary>
        public int AreaThreshold { get; set; }

        /// <summary>
        /// 模型输入宽度。
        /// </summary>
        public int InputWidth { get; set; }

        /// <summary>
        /// 模型输入高度。
        /// </summary>
        public int InputHeight { get; set; }

        /// <summary>
        /// 是否使用 ROI。
        /// </summary>
        public bool RoiEnabled { get; set; }

        /// <summary>
        /// ROI 左上角 X 坐标。
        /// </summary>
        public int RoiX { get; set; }

        /// <summary>
        /// ROI 左上角 Y 坐标。
        /// </summary>
        public int RoiY { get; set; }

        /// <summary>
        /// ROI 宽度。
        /// </summary>
        public int RoiWidth { get; set; }

        /// <summary>
        /// ROI 高度。
        /// </summary>
        public int RoiHeight { get; set; }

        /// <summary>
        /// 源图总数。
        /// </summary>
        public int SourceImageCount { get; set; }

        /// <summary>
        /// OK 训练图数量。
        /// </summary>
        public int OkCount { get; set; }

        /// <summary>
        /// NG 校准图数量。
        /// </summary>
        public int NgCount { get; set; }
    }
}
