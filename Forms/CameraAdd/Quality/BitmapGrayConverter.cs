using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TDJS_Vision.Forms.CameraAdd.Quality
{
    /// <summary>
    /// 将预览位图转换为连续灰度数组。
    /// </summary>
    public static class BitmapGrayConverter
    {
        /// <summary>
        /// 转换位图为 Mono8 灰度数据。
        /// </summary>
        /// <param name="source">源位图。</param>
        /// <returns>连续灰度数据。</returns>
        public static byte[] Convert(Bitmap source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            using (Bitmap normalized = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics graphics = Graphics.FromImage(normalized))
                {
                    graphics.DrawImageUnscaled(source, 0, 0);
                }

                Rectangle area = new Rectangle(0, 0, normalized.Width, normalized.Height);
                BitmapData data = normalized.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    byte[] gray = new byte[normalized.Width * normalized.Height];
                    int stride = Math.Abs(data.Stride);
                    byte[] rgb = new byte[stride * normalized.Height];
                    Marshal.Copy(data.Scan0, rgb, 0, rgb.Length);
                    for (int y = 0; y < normalized.Height; y++)
                    {
                        int row = y * stride;
                        int destination = y * normalized.Width;
                        for (int x = 0; x < normalized.Width; x++)
                        {
                            int offset = row + x * 3;
                            gray[destination + x] = (byte)((rgb[offset + 2] * 77 + rgb[offset + 1] * 150 + rgb[offset] * 29) >> 8);
                        }
                    }

                    return gray;
                }
                finally
                {
                    normalized.UnlockBits(data);
                }
            }
        }
    }
}
