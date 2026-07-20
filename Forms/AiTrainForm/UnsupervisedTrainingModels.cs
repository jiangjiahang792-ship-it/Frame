using System;
using System.Collections.Generic;
using System.Drawing;
using OpenCvSharp;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 无监督训练图片在检查页中的分类。
    /// </summary>
    public enum UnsupervisedImageCategory
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
    /// 无监督训练界面中的单张图片信息。
    /// </summary>
    public sealed class UnsupervisedImageItem : IDisposable
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
        public UnsupervisedImageCategory Category { get; set; }

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
    /// 无监督训练请求参数。
    /// </summary>
    public sealed class UnsupervisedTrainingRequest
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
        public IReadOnlyList<UnsupervisedImageItem> Images { get; set; }

        /// <summary>
        /// 算法类型。
        /// </summary>
        public string ModelType { get; set; }

        /// <summary>
        /// 异常阈值。
        /// </summary>
        public float Threshold { get; set; }

        /// <summary>
        /// 最小异常面积。
        /// </summary>
        public float MiniArea { get; set; }

        /// <summary>
        /// 训练输入宽度。
        /// </summary>
        public int InputWidth { get; set; }

        /// <summary>
        /// 训练输入高度。
        /// </summary>
        public int InputHeight { get; set; }

        /// <summary>
        /// 最大训练轮数。
        /// </summary>
        public int MaxEpochs { get; set; }

        /// <summary>
        /// 批次大小。
        /// </summary>
        public int BatchSize { get; set; }

        /// <summary>
        /// 训练设备。
        /// </summary>
        public string Device { get; set; }

        /// <summary>
        /// 可选 ROI；为空时按整图训练。
        /// </summary>
        public Rect? RoiRect { get; set; }
    }

    /// <summary>
    /// 无监督训练输出结果。
    /// </summary>
    public sealed class UnsupervisedTrainingResult
    {
        /// <summary>
        /// 打包后的模板路径。
        /// </summary>
        public string TemplatePath { get; set; }

        /// <summary>
        /// 原始 ONNX 模型路径。
        /// </summary>
        public string OnnxPath { get; set; }

        /// <summary>
        /// OK 训练图片数量。
        /// </summary>
        public int OkCount { get; set; }

        /// <summary>
        /// NG 训练图片数量。
        /// </summary>
        public int NgCount { get; set; }
    }

    /// <summary>
    /// 无监督运行环境检查结果。
    /// </summary>
    public sealed class UnsupervisedRuntimeStatus
    {
        /// <summary>
        /// 环境是否可用于训练。
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
    /// 无监督模板文件中的元数据清单。
    /// </summary>
    public sealed class UnsupervisedTemplateManifest
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
        /// 算法类型。
        /// </summary>
        public string ModelType { get; set; }

        /// <summary>
        /// 异常阈值。
        /// </summary>
        public float Threshold { get; set; }

        /// <summary>
        /// 最小异常面积。
        /// </summary>
        public float MiniArea { get; set; }

        /// <summary>
        /// 训练输入宽度。
        /// </summary>
        public int InputWidth { get; set; }

        /// <summary>
        /// 训练输入高度。
        /// </summary>
        public int InputHeight { get; set; }

        /// <summary>
        /// 批次大小。
        /// </summary>
        public int BatchSize { get; set; }

        /// <summary>
        /// 训练设备。
        /// </summary>
        public string Device { get; set; }

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
        /// NG 训练图数量。
        /// </summary>
        public int NgCount { get; set; }
    }
}
