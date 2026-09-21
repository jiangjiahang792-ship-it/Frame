using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Logger;
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._7_ResultProcessing.ImageSave;

/// <summary>用真实图像租约、保存工作池和日志验证命名异常不会丢图。</summary>
internal static class ImageSaveInvalidNameTests
{
    /// <summary>已执行的行为断言数量。</summary>
    private static int _checks;

    /// <summary>验证实际运行结果。</summary>
    private static void Check(bool passed, string message)
    {
        _checks++;
        if (!passed) throw new InvalidOperationException(message);
    }

    /// <summary>等待真实日志工作线程排空，使落盘日志断言不依赖固定延时。</summary>
    private static void FlushLogs()
    {
        using (var completed = new ManualResetEventSlim())
        {
            var queue = (BlockingCollection<Action>)typeof(LogHelper).GetField("_logQueue", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            queue.Add(() => completed.Set());
            Check(completed.Wait(10000), "错误日志未及时写入文件。");
        }
    }

    /// <summary>投递真实小图，入队后立即释放生产方引用，验证后台租约仍能保住图像。</summary>
    private static void Enqueue(ImageQueueProcessor processor, string imageName, string[] directories, bool compress)
    {
        using (var output = OutputImage.FromOwnedSingleImage(new Mat(24, 32, MatType.CV_8UC3, Scalar.All(127))))
        {
            var task = new ImageQueueProcessor.SaveImageTask {
                TargetDirectories = directories.ToList(), ImageName = imageName,
                NeedCompress = compress, CompressValue = 45, OwnerKey = "37.保存图片",
                EstimatedBytes = output.GetRetainedImageBytesEstimateForAdmission() };
            var admission = processor.EnqueueImage(task, () => output.AcquireSaveSnapshot());
            if (!admission.Accepted) task.Dispose();
            Check(admission.Accepted, "测试图片未被保存队列接纳。");
        }
    }

    /// <summary>执行包含PLC尾部空字符、Windows非法字符及并发重名的真实落盘测试。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(root);
            Environment.CurrentDirectory = root;
            LogLevelSettings.SetEnabled(MsgLevel.Exception, true);
            string[] targets = { Path.Combine(root, "目录一"), Path.Combine(root, "目录二") };
            var invalidNames = new List<string> {
                "工艺\0尾码-CAN", "A<B", "A>B", "A:B", "A\"B", "A/B", "A\\B", "A|B", "A?B", "A*B",
                "A\r\nB-CAN", "A\tB", "CON", "NUL.txt", "COM1", "LPT9", "", null,
                "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³"
            };
            // 同一个非法名称连续出现时，每一张都必须保留，不能覆盖。
            invalidNames.AddRange(Enumerable.Repeat("重复/工艺-CAN", 20));
            int expectedTasks = invalidNames.Count + 3;
            var processor = new ImageQueueProcessor(4, 128, 32L * 1024 * 1024, 10000, "全方案共享");
            try
            {
                processor.StartProcessing();
                for (int index = 0; index < invalidNames.Count; index++)
                    Enqueue(processor, invalidNames[index], targets, index % 2 == 0);
                for (int index = 0; index < 3; index++) Enqueue(processor, "合法工艺_0012-CAN", targets, true);
                Check(SpinWait.SpinUntil(() => processor.CompletedTaskCount + processor.FailedTaskCount >= expectedTasks, 15000),
                    "后台存图未及时完成。");
                Check(processor.StopProcessing(), "保存工作池未正常停止。");
                Check(processor.FailedTaskCount == 0, "非法命名仍导致图片丢失，失败数：" + processor.FailedTaskCount);
                Check(processor.CompletedTaskCount == expectedTasks, "完成数量与入队图片数量不一致。");
            }
            finally { processor.StopProcessing(); }

            var nameSets = new List<string[]>();
            foreach (string target in targets)
            {
                string[] files = Directory.GetFiles(target, "*.jpg");
                Check(files.Length == expectedTasks, "实际JPEG数量不符或重名图片被覆盖。");
                string[] fallbackFiles = files.Where(path => Path.GetFileName(path).StartsWith("非法字符_")).ToArray();
                Check(fallbackFiles.Length == invalidNames.Count, "非法名称未统一回退为指定格式。");
                foreach (string file in fallbackFiles)
                    Check(Regex.IsMatch(Path.GetFileName(file), @"^非法字符_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}-\d{3}(_\d+)?\.jpg$"), "备用名称日期格式或冲突后缀错误。");
                Check(File.Exists(Path.Combine(target, "合法工艺_0012-CAN.jpg")) &&
                    File.Exists(Path.Combine(target, "合法工艺_0012-CAN_1.jpg")) &&
                    File.Exists(Path.Combine(target, "合法工艺_0012-CAN_2.jpg")), "正常名称或防覆盖机制受到影响。");
                foreach (string file in files)
                    using (var image = Image.FromFile(file)) Check(image.Width == 32 && image.Height == 24, "保存的JPEG不可读取或尺寸错误。");
                Check(Directory.GetFiles(target, "*.saving").Length == 0, "保存后残留临时文件。");
                nameSets.Add(files.Select(Path.GetFileName).OrderBy(name => name).ToArray());
            }
            // 多目录可能并发竞争后缀，但各目录必须保留相同数量和正常命名文件。
            Check(nameSets[0].Length == nameSets[1].Length, "同一任务的多个目标目录发生丢图。");
            FlushLogs();
            string[] logLines = Directory.GetFiles(Path.Combine(root, "Logs"), "*.log").SelectMany(File.ReadAllLines).ToArray();
            string[] errors = logLines.Where(line => line.Contains("保存图像命名异常")).ToArray();
            Check(errors.Length == invalidNames.Count, "每张非法名称图片必须且只记录一次命名错误。");
            Check(errors.All(line => line.Contains("Exception") && line.Contains("非法字符_") && line.Contains("原名称=")),
                "日志缺少错误级别、原始名称或备用名称。");
            Check(errors.Any(line => line.Contains(@"\u0000")) && errors.Any(line => line.Contains(@"\r\n")),
                "PLC空字符和换行应转义记录，不能破坏日志行。");
            Check(!logLines.Any(line => line.Contains("保存图像失败")), "命名已回退成功却仍记录为图片保存失败。");
            Console.WriteLine("PASS: {0}项断言，{1}张图片×2目录实际保存并解码，错误日志完整；输出={2}", _checks, expectedTasks, root);
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
