using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using MvCamCtrl.NET;

namespace HikDualCameraMatBenchmark
{
    /// <summary>
    /// 在相机停止后导出逐帧CSV和便于直接对比的汇总报告。
    /// </summary>
    internal static class ResultExporter
    {
        /// <summary>
        /// 导出两台相机的计时明细和汇总结果。
        /// </summary>
        /// <param name="baseDirectory">结果根目录。</param>
        /// <param name="settings">本轮测试参数。</param>
        /// <param name="camera1">相机会话1。</param>
        /// <param name="camera2">相机会话2。</param>
        /// <param name="ui1">相机1的UI计时。</param>
        /// <param name="ui2">相机2的UI计时。</param>
        /// <returns>本轮结果目录。</returns>
        public static string Export(
            string baseDirectory,
            BenchmarkSettings settings,
            CameraBenchmarkSession camera1,
            CameraBenchmarkSession camera2,
            UiTimingCollector ui1,
            UiTimingCollector ui2)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (camera1 == null)
                throw new ArgumentNullException(nameof(camera1));
            if (camera2 == null)
                throw new ArgumentNullException(nameof(camera2));

            string resultDirectory = Path.Combine(
                baseDirectory,
                DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(resultDirectory);

            WriteCameraCsv(Path.Combine(resultDirectory, "相机1_逐帧耗时.csv"), settings, camera1);
            WriteCameraCsv(Path.Combine(resultDirectory, "相机2_逐帧耗时.csv"), settings, camera2);
            WriteSummary(
                Path.Combine(resultDirectory, "测试汇总.txt"),
                settings,
                camera1,
                camera2,
                ui1,
                ui2);
            return resultDirectory;
        }

        /// <summary>
        /// 写入单台相机的全部逐帧计时，包括被正式统计排除的预热帧。
        /// </summary>
        /// <param name="path">CSV路径。</param>
        /// <param name="settings">测试参数。</param>
        /// <param name="session">相机会话。</param>
        private static void WriteCameraCsv(
            string path,
            BenchmarkSettings settings,
            CameraBenchmarkSession session)
        {
            FrameTimingRecord[] records = session.TimingCollector.Snapshot();
            using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(
                    "序号,是否预热帧,SDK帧号,进入回调相对时间ms,宽,高,输入像素格式,输出像素格式," +
                    "ConvertPixelType_ms,Mat建立_ms,Mat独立复制_ms,发布最新帧_ms,回调业务总耗时_ms,返回码");

                for (int index = 0; index < records.Length; index++)
                {
                    FrameTimingRecord record = records[index];
                    writer.WriteLine(string.Join(",", new[]
                    {
                        (index + 1).ToString(CultureInfo.InvariantCulture),
                        (index < settings.WarmupFrames ? "是" : "否"),
                        record.FrameNumber.ToString(CultureInfo.InvariantCulture),
                        Format(TimingCollector.TicksToMilliseconds(record.ElapsedTicks)),
                        record.Width.ToString(CultureInfo.InvariantCulture),
                        record.Height.ToString(CultureInfo.InvariantCulture),
                        Escape(record.SourcePixelType.ToString()),
                        Escape(record.DestinationPixelType.ToString()),
                        Format(TimingCollector.TicksToMilliseconds(record.ConvertTicks)),
                        Format(TimingCollector.TicksToMilliseconds(record.MatWrapTicks)),
                        Format(TimingCollector.TicksToMilliseconds(record.MatCloneTicks)),
                        Format(TimingCollector.TicksToMilliseconds(record.PublishTicks)),
                        Format(TimingCollector.TicksToMilliseconds(record.CallbackTicks)),
                        string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", record.ResultCode)
                    }));
                }
            }
        }

        /// <summary>
        /// 写入测试环境、分位数、长尾数量和主程序对照口径。
        /// </summary>
        /// <param name="path">汇总文件路径。</param>
        /// <param name="settings">测试参数。</param>
        /// <param name="camera1">相机会话1。</param>
        /// <param name="camera2">相机会话2。</param>
        /// <param name="ui1">相机1 UI计时。</param>
        /// <param name="ui2">相机2 UI计时。</param>
        private static void WriteSummary(
            string path,
            BenchmarkSettings settings,
            CameraBenchmarkSession camera1,
            CameraBenchmarkSession camera2,
            UiTimingCollector ui1,
            UiTimingCollector ui2)
        {
            StringBuilder builder = new StringBuilder();
            Process process = Process.GetCurrentProcess();
            builder.AppendLine("海康双相机官方回调 -> BGR/Mono内存 -> OpenCvSharp Mat 稳定性测试");
            builder.AppendLine("生成时间=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            builder.AppendLine("SDK版本=0x" + MyCamera.MV_CC_GetSDKVersion_NET().ToString("X8", CultureInfo.InvariantCulture));
            builder.AppendLine("系统=" + Environment.OSVersion);
            builder.AppendLine("逻辑处理器=" + Environment.ProcessorCount);
            builder.AppendLine("64位进程=" + Environment.Is64BitProcess);
            builder.AppendLine("触发方式=" + (settings.TriggerMode == CameraTriggerMode.Line0 ? "Line0硬触发" : "连续采集"));
            builder.AppendLine("预热帧=" + settings.WarmupFrames);
            builder.AppendLine("正式样本目标=" + settings.SampleFrames);
            builder.AppendLine("实时显示=" + settings.DisplayEnabled);
            builder.AppendLine("显示刷新上限=" + settings.DisplayFps + " FPS");
            builder.AppendLine("导出时工作集MB=" + (process.WorkingSet64 / 1024.0 / 1024.0).ToString("F1", CultureInfo.InvariantCulture));
            builder.AppendLine("导出时私有内存MB=" + (process.PrivateMemorySize64 / 1024.0 / 1024.0).ToString("F1", CultureInfo.InvariantCulture));
            builder.AppendLine("GC次数=" + GC.CollectionCount(0) + "/" + GC.CollectionCount(1) + "/" + GC.CollectionCount(2));
            builder.AppendLine();

            AppendCameraSummary(builder, camera1, ui1);
            builder.AppendLine();
            AppendCameraSummary(builder, camera2, ui2);
            builder.AppendLine();
            builder.AppendLine("主程序关闭AI对照（2026-08-21日志，仅用于相同机器条件下参考）");
            builder.AppendLine("海康：回调平均10.73ms，P99=14ms，首帧最大24ms，稳态最大16ms；回调到Mat平均7.47ms。");
            builder.AppendLine("CCD1：回调平均11.03ms，P99=14ms，首帧最大24ms，稳态最大18ms；回调到Mat平均7.53ms。");
            builder.AppendLine("注意：本Demo采用官方旧版原生回调和预分配目标缓冲；主程序采用托管IImage转换，比较时必须分别看ConvertPixelType、Mat复制和回调总耗时。");

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
        }

        /// <summary>
        /// 追加单台相机全部阶段的统计结果。
        /// </summary>
        /// <param name="builder">汇总文本构造器。</param>
        /// <param name="session">相机会话。</param>
        /// <param name="ui">UI计时器。</param>
        private static void AppendCameraSummary(
            StringBuilder builder,
            CameraBenchmarkSession session,
            UiTimingCollector ui)
        {
            builder.AppendLine("[相机" + session.SlotNumber + "]");
            builder.AppendLine("设备=" + session.DeviceItem);
            builder.AppendLine("收到帧=" + session.TimingCollector.TotalReceived);
            builder.AppendLine("正式有效样本=" + session.TimingCollector.FormalSampleCount);
            builder.AppendLine("明细溢出=" + session.TimingCollector.OverflowCount);
            builder.AppendLine("回调错误=" + session.CallbackErrorCount);
            builder.AppendLine("UI覆盖旧帧=" + session.LatestFrame.ReplacedCount);
            if (!string.IsNullOrWhiteSpace(session.LastError))
                builder.AppendLine("最近错误=" + session.LastError);

            AppendStatistics(builder, "ConvertPixelType", session.TimingCollector.Calculate(item => item.ConvertTicks));
            AppendStatistics(builder, "Mat建立", session.TimingCollector.Calculate(item => item.MatWrapTicks));
            AppendStatistics(builder, "Mat独立复制", session.TimingCollector.Calculate(item => item.MatCloneTicks));
            AppendStatistics(builder, "发布最新帧", session.TimingCollector.Calculate(item => item.PublishTicks));
            AppendStatistics(builder, "回调业务总耗时", session.TimingCollector.Calculate(item => item.CallbackTicks));
            AppendStatistics(builder, "UI Mat转Bitmap", ui.CalculateToBitmap());
            AppendStatistics(builder, "UI更换图像", ui.CalculateSetImage());
        }

        /// <summary>
        /// 以统一格式追加一行统计结果。
        /// </summary>
        /// <param name="builder">汇总文本构造器。</param>
        /// <param name="name">统计阶段名称。</param>
        /// <param name="statistics">分位统计。</param>
        private static void AppendStatistics(
            StringBuilder builder,
            string name,
            TimingStatistics statistics)
        {
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: N={1}, 平均={2:F3}ms, P50={3:F3}ms, P95={4:F3}ms, P99={5:F3}ms, P99.9={6:F3}ms, 最大={7:F3}ms, >20ms={8}, >30ms={9}, >50ms={10}",
                name,
                statistics.Count,
                statistics.AverageMs,
                statistics.P50Ms,
                statistics.P95Ms,
                statistics.P99Ms,
                statistics.P999Ms,
                statistics.MaximumMs,
                statistics.Over20Ms,
                statistics.Over30Ms,
                statistics.Over50Ms));
        }

        /// <summary>
        /// 使用固定六位小数输出毫秒。
        /// </summary>
        /// <param name="value">毫秒。</param>
        /// <returns>不受区域设置影响的数字文本。</returns>
        private static string Format(double value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 转义CSV文本字段。
        /// </summary>
        /// <param name="value">原始文本。</param>
        /// <returns>CSV安全文本。</returns>
        private static string Escape(string value)
        {
            string safe = value ?? string.Empty;
            return '"' + safe.Replace("\"", "\"\"") + '"';
        }
    }
}
