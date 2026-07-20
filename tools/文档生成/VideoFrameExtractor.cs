using System;
using System.Globalization;
using System.IO;
using OpenCvSharp;

namespace TDJSVision.Tools.DocumentGeneration
{
    /// <summary>
    /// 从操作录屏中读取视频元数据，并按指定时间间隔导出关键画面。
    /// </summary>
    internal static class VideoFrameExtractor
    {
        /// <summary>
        /// 程序入口，参数依次为视频路径、输出目录和抽帧间隔秒数。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        /// <returns>成功返回 0，参数或视频读取失败返回非 0。</returns>
        private static int Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 3)
            {
                Console.Error.WriteLine("用法：VideoFrameExtractor <视频路径> <输出目录> [抽帧间隔秒数]");
                return 2;
            }

            string videoPath = Path.GetFullPath(args[0]);
            string outputDirectory = Path.GetFullPath(args[1]);
            double intervalSeconds = args.Length == 3
                ? double.Parse(args[2], CultureInfo.InvariantCulture)
                : 10.0;

            if (!File.Exists(videoPath))
            {
                Console.Error.WriteLine("视频文件不存在：" + videoPath);
                return 3;
            }

            if (intervalSeconds <= 0)
            {
                Console.Error.WriteLine("抽帧间隔必须大于 0 秒。");
                return 4;
            }

            Directory.CreateDirectory(outputDirectory);

            using (var capture = new VideoCapture(videoPath))
            {
                if (!capture.IsOpened())
                {
                    Console.Error.WriteLine("无法打开视频：" + videoPath);
                    return 5;
                }

                double framesPerSecond = capture.Fps;
                long frameCount = (long)capture.FrameCount;
                double durationSeconds = framesPerSecond > 0 ? frameCount / framesPerSecond : 0;
                int width = capture.FrameWidth;
                int height = capture.FrameHeight;

                Console.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "元数据|时长秒={0:F3}|帧率={1:F3}|总帧数={2}|宽度={3}|高度={4}",
                        durationSeconds,
                        framesPerSecond,
                        frameCount,
                        width,
                        height));

                if (durationSeconds <= 0)
                {
                    Console.Error.WriteLine("视频时长无效，无法抽帧。");
                    return 6;
                }

                int imageIndex = 1;
                for (double second = 0; second < durationSeconds; second += intervalSeconds)
                {
                    capture.Set(VideoCaptureProperties.PosMsec, second * 1000.0);
                    using (var frame = new Mat())
                    {
                        if (!capture.Read(frame) || frame.Empty())
                        {
                            Console.Error.WriteLine(
                                string.Format(CultureInfo.InvariantCulture, "跳过无法读取的时间点：{0:F3} 秒", second));
                            continue;
                        }

                        string outputPath = Path.Combine(
                            outputDirectory,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "frame-{0:D4}-{1:D6}ms.jpg",
                                imageIndex,
                                (long)Math.Round(second * 1000.0)));

                        Cv2.ImWrite(outputPath, frame, new ImageEncodingParam(ImwriteFlags.JpegQuality, 90));
                        Console.WriteLine(
                            string.Format(CultureInfo.InvariantCulture, "画面|序号={0}|时间秒={1:F3}|文件={2}", imageIndex, second, outputPath));
                        imageIndex++;
                    }
                }
            }

            return 0;
        }
    }
}
