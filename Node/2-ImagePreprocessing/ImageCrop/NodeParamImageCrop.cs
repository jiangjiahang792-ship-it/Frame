using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using OpenCvSharp;
using TDJS_Vision.Forms.ShapeDraw;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop
{
    /// <summary>图像裁剪参数，兼容旧控件坐标并保存新的图像坐标ROI。</summary>
    public class NodeParamImageCrop : INodeParam
    {
        /// <summary>
        /// 订阅的节点名称（反序列化用）
        /// </summary>
        public string Text1 { get; set; }

        /// <summary>
        /// 订阅的节点结果属性名（反序列化用）
        /// </summary>
        public string Text2 { get; set; }

        /// <summary>
        /// ROIs（反序列化用）
        /// </summary>
        [JsonConverter(typeof(ROIListConverter<ROI>))]
        public List<ROI> ROIs { get; set; }

        /// <summary>图像坐标ROI；null表示尚未迁移的旧方案，空列表表示使用全图。</summary>
        public List<ImageCropRoiRegion> ImageRois { get; set; }
        /// <summary>
        /// 是否启用ROI裁剪
        /// </summary>
        public bool RoiEnable { get; set; }

    }

    /// <summary>独立于显示控件的旋转裁剪区域，角度单位为度。</summary>
    public sealed class ImageCropRoiRegion
    {
        /// <summary>中心横坐标，单位为图像像素。</summary>
        public float CenterX { get; set; }
        /// <summary>中心纵坐标，单位为图像像素。</summary>
        public float CenterY { get; set; }
        /// <summary>旋转前的矩形宽度。</summary>
        public float Width { get; set; }
        /// <summary>旋转前的矩形高度。</summary>
        public float Height { get; set; }
        /// <summary>图像坐标系中顺时针旋转角度。</summary>
        public float Angle { get; set; }

        /// <summary>取得完整外接范围，允许坐标超出源图，保持ROI的原位置和大小。</summary>
        public Rect GetImageRect(int imageWidth, int imageHeight)
        {
            if (!IsFinite(CenterX) || !IsFinite(CenterY) || !IsFinite(Width) ||
                !IsFinite(Height) || !IsFinite(Angle) || Width <= 0 || Height <= 0)
                throw new InvalidOperationException("裁剪ROI的位置、尺寸或角度无效。");

            double angle = Angle % 180;
            double radians = angle * Math.PI / 180;
            double halfWidth = (Math.Abs(Width * Math.Cos(radians)) + Math.Abs(Height * Math.Sin(radians))) / 2;
            double halfHeight = (Math.Abs(Width * Math.Sin(radians)) + Math.Abs(Height * Math.Cos(radians))) / 2;
            if (Math.Abs(angle) < 0.0001)
                return new Rect((int)Math.Floor(CenterX - Width / 2), (int)Math.Floor(CenterY - Height / 2),
                    Math.Max(1, (int)Width), Math.Max(1, (int)Height));

            int left = (int)Math.Floor(CenterX - halfWidth + 0.0001);
            int top = (int)Math.Floor(CenterY - halfHeight + 0.0001);
            int right = (int)Math.Ceiling(CenterX + halfWidth - 0.0001);
            int bottom = (int)Math.Ceiling(CenterY + halfHeight - 0.0001);
            return new Rect(left, top, right - left, bottom - top);
        }

        /// <summary>判断坐标是否为有限数值。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>是否改变像素坐标，用于选择输出图像的坐标基准。</summary>
        [JsonIgnore]
        public bool RequiresRectification => Math.Abs(Angle % 360) > 0.0001 ||
            CenterX - Width / 2 != Math.Round(CenterX - Width / 2) ||
            CenterY - Height / 2 != Math.Round(CenterY - Height / 2) ||
            Width != Math.Round(Width) || Height != Math.Round(Height);

        /// <summary>判断旋转或补边后是否需要使用输出图像的独立坐标基准。</summary>
        public bool RequiresOutputCoordinates(int imageWidth, int imageHeight)
        {
            Rect bounds = GetImageRect(imageWidth, imageHeight);
            return RequiresRectification || bounds.X < 0 || bounds.Y < 0 ||
                (long)bounds.X + bounds.Width > imageWidth || (long)bounds.Y + bounds.Height > imageHeight;
        }

        /// <summary>将旧版像素矩形转换为同一裁剪策略使用的ROI。</summary>
        public static ImageCropRoiRegion FromRectangle(Rect rect)
        {
            return new ImageCropRoiRegion
            {
                CenterX = rect.X + rect.Width / 2F, CenterY = rect.Y + rect.Height / 2F,
                Width = rect.Width, Height = rect.Height
            };
        }

        /// <summary>直接从源图采样出摆正的ROI，避免整图旋转、黑色画布及二次插值。</summary>
        public Mat CropRectified(Mat source)
        {
            if (!OutputImage.HasValidImage(source))
                throw new InvalidOperationException("订阅的图像为空！");
            Rect bounds = GetImageRect(source.Width, source.Height);
            if (!RequiresOutputCoordinates(source.Width, source.Height))
            {
                using (var roi = new Mat(source, bounds))
                    return roi.Clone();
            }

            int width = Math.Max(1, checked((int)Math.Round(Width, MidpointRounding.AwayFromZero)));
            int height = Math.Max(1, checked((int)Math.Round(Height, MidpointRounding.AwayFromZero)));
            double angle = (Angle % 360) * Math.PI / 180;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            double stepX = Width / (double)width, stepY = Height / (double)height;
            var output = new Mat();
            try
            {
                using (var inverse = new Mat(2, 3, MatType.CV_64FC1))
                {
                    // ROI按像素边界定义，OpenCV按像素中心采样；半像素补偿使0度与直接裁剪对齐。
                    inverse.Set(0, 0, cos * stepX);
                    inverse.Set(0, 1, -sin * stepY);
                    inverse.Set(1, 0, sin * stepX);
                    inverse.Set(1, 1, cos * stepY);
                    inverse.Set(0, 2, CenterX - 0.5 - cos * stepX * (width - 1) / 2 + sin * stepY * (height - 1) / 2);
                    inverse.Set(1, 2, CenterY - 0.5 - sin * stepX * (width - 1) / 2 - cos * stepY * (height - 1) / 2);
                    // 越界部分使用最近边缘像素补齐；保持ROI尺寸，也不移动或缩放图内有效内容。
                    Cv2.WarpAffine(source, output, inverse, new Size(width, height),
                        InterpolationFlags.Linear | InterpolationFlags.WarpInverseMap, BorderTypes.Replicate);
                }
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }
    }
}
