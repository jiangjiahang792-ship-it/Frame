using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>组合模式取色的主色筛选器；只在学习时运行，不增加逐帧检测成本。</summary>
    internal static class GuideColorSampleSelector
    {
        /// <summary>提取有明确优势的颜色簇；多色接近时拒绝学习，避免合并背景与线芯。</summary>
        public static List<Vec3b> Select(IReadOnlyList<Vec3b> samples)
        {
            if (samples == null || samples.Count == 0) throw new InvalidOperationException("取色框没有有效像素，请重新绘制。");
            var features = new int[samples.Count];
            var histogram = new int[616];
            for (int i = 0; i < samples.Count; i++)
            {
                features[i] = Feature(samples[i]);
                histogram[features[i]]++;
            }
            int selected = FindPeak(histogram);
            int count = CountNear(histogram, selected);
            // 第二主色必须在第一主色以外统计，避免同一色峰被重复计数。
            var remaining = (int[])histogram.Clone();
            for (int i = 0; i < remaining.Length; i++) if (Near(i, selected)) remaining[i] = 0;
            int runner = CountNear(remaining, FindPeak(remaining));
            if (count < Math.Max(1, samples.Count * 0.25) || runner >= count * 0.8)
                throw new InvalidOperationException("取色框中有多种接近的颜色，无法确定目标。请缩小取色框，保留更多目标线芯后重新学习。");
            var result = new List<Vec3b>(count);
            for (int i = 0; i < samples.Count; i++) if (Near(features[i], selected)) result.Add(samples[i]);
            return result;
        }

        /// <summary>彩色按环形色相分组；低饱和及极暗像素按亮度分组，避免反光拉宽彩色范围。</summary>
        private static int Feature(Vec3b pixel)
        {
            double b = pixel.Item0, g = pixel.Item1, r = pixel.Item2;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            if (max < 35 || delta < max * 0.22) return 360 + (int)Math.Round((r + g + b) / 3);
            double hue = max == r ? (g - b) / delta : max == g ? 2 + (b - r) / delta : 4 + (r - g) / delta;
            return (int)Math.Round((hue * 60 + 360) % 360) % 360;
        }

        /// <summary>使用固定直方图寻找主峰，计算量与图像分辨率无关。</summary>
        private static int FindPeak(int[] histogram)
        {
            int selected = 0, best = -1;
            for (int i = 0; i < histogram.Length; i++)
            {
                int count = CountNear(histogram, i);
                // 同样覆盖量优先选择自身样本较多的中心，保持结果确定性。
                if (count > best || (count == best && histogram[i] > histogram[selected])) { best = count; selected = i; }
            }
            return selected;
        }

        /// <summary>统计色相邻域或灰度邻域的样本数。</summary>
        private static int CountNear(int[] histogram, int center)
        {
            int count = 0;
            for (int i = 0; i < histogram.Length; i++) if (Near(i, center)) count += histogram[i];
            return count;
        }

        /// <summary>同色允许正常明暗变化；彩色与无彩色背景不能进入同一个学习簇。</summary>
        private static bool Near(int value, int center)
        {
            if ((value < 360) != (center < 360)) return false;
            int distance = Math.Abs(value - center);
            return center < 360 ? Math.Min(distance, 360 - distance) <= 20 : distance <= 28;
        }
    }
}
