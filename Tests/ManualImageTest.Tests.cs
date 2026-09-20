using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.ResourceManagement;

/// <summary>验证停止后首次运行和手动图像测试的异步参数生命周期。</summary>
internal static class ManualImageTestTests
{
    /// <summary>累计通过的断言数。</summary>
    private static int _checks;

    /// <summary>访问真实界面入口及流程执行门闩的反射标志。</summary>
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>验证条件并输出可定位的失败信息。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    /// <summary>驱动真实窗体消息循环，等待异步流程而不阻塞界面线程。</summary>
    private static void Complete(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(5);
        }
        Check(task.IsCompleted, "流程未在验证期限内完成。");
        task.GetAwaiter().GetResult();
    }

    /// <summary>使用生成的本地图片运行真实图像源，无需连接现场相机。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ManualImageTest.fixture.png");
            using (var bitmap = new Bitmap(32, 24)) bitmap.Save(path);
            var process = new Process("首次单图验证") { Enable = true, HasCanvasGraph = false };
            using (var node = new NodeImageSource(1, "图像源", process, NodeType.ImageSource))
            {
                node.Active = true;
                node.ParamForm.Params = new NodeParamImageSoucre { ImageSource = "本地图像", ImagePath = path };
                process.Nodes.Add(node);
                Solution.Instance.AllProcesses.Add(process);
                Complete(process.Run(false));
                Check(process.Success, "基线本地图像运行失败。");
                Solution.Instance.Stop();
                Check(Solution.Instance.CancellationToken.IsCancellationRequested, "未建立停止后的旧取消状态。");
                Complete(process.Run(false));
                Check(process.Success, "停止后首次本地图像运行必须成功，不能要求再点击一次。");
                Complete(process.Run(false));
                Check(process.Success, "后续本地图像运行失败。");
                using (var cancelledSource = new CancellationTokenSource())
                {
                    cancelledSource.Cancel();
                    var explicitRun = typeof(Process).GetMethod("RunWithResultAsync", PrivateInstance);
                    bool explicitCancellationPreserved = false;
                    try { Complete((Task)explicitRun.Invoke(process, new object[] { false, cancelledSource.Token })); }
                    catch (OperationCanceledException) { explicitCancellationPreserved = true; }
                    Check(explicitCancellationPreserved, "调用者显式传入的取消令牌仍必须生效。");
                }
                var parameter = (NodeParamImageSoucre)node.ParamForm.Params;
                parameter.ImageSource = "相机";
                parameter.ImagePath = "保留原图像路径";
                parameter.TriggerModel = TriggerModel.On;
                parameter.TriggerSource = TriggerSource.LINE0;
                parameter.TriggerEdge = TriggerEdge.Rising;
                using (var control = new SolRunParamControl())
                {
                    control.Init(process);
                    var entry = typeof(SolRunParamControl).GetMethod("RunImageSourceOnceAsync", PrivateInstance);
                    Check(entry != null, "缺少可等待完成并恢复参数的手动图像测试入口。");
                    var gate = (ProcessWorkpieceGate)typeof(Process).GetField("_workpieceRunGate", PrivateInstance).GetValue(process);
                    using (var lease = gate.EnterAsync(CancellationToken.None).GetAwaiter().GetResult())
                    {
                        var pending = (Task)entry.Invoke(control, new object[] { node, "本地图像", path });
                        Check(!pending.IsCompleted, "未模拟到异步排队。");
                        Check(parameter.ImageSource == "本地图像" && parameter.ImagePath == path,
                            "排队中的单图测试不能提前恢复相机参数。");
                        string savedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ManualImageTest.saved.Sol");
                        Solution.Instance.Save(savedPath);
                        string savedJson = File.ReadAllText(savedPath);
                        var savedDocument = JObject.Parse(savedJson);
                        Check(!savedJson.Contains("ManualImageTest.fixture.png"),
                            "测试期间保存不能把临时图片路径写入方案。");
                        Check((string)savedDocument.SelectToken("$..ImagePath") == "保留原图像路径" &&
                            (string)savedDocument.SelectToken("$..ImageSource") == "相机", "测试期间保存必须保留原始相机来源与路径。");
                        Check(parameter.ImageSource == "本地图像" && parameter.ImagePath == path, "保存快照不能干扰正在运行的测试参数。");
                        var duplicate = (Task)entry.Invoke(control, new object[] { node, "本地图像", path });
                        bool duplicateRejected = false;
                        try { Complete(duplicate); } catch (InvalidOperationException) { duplicateRejected = true; }
                        Check(duplicateRejected, "重复点击必须明确拒绝，不能覆盖正在运行的临时参数。");
                        lease.Dispose();
                        Complete(pending);
                    }
                    Check(process.Success, "线路硬触发配置下的本地单图测试必须完成。");
                    Check(parameter.ImageSource == "相机" && parameter.ImagePath == "保留原图像路径",
                        "完成后未恢复原始图像源和路径。");
                    Check(parameter.TriggerModel == TriggerModel.On && parameter.TriggerSource == TriggerSource.LINE0 && parameter.TriggerEdge == TriggerEdge.Rising,
                        "本地图像测试不能改写方案的硬触发配置。");
                    using (var lease = gate.EnterAsync(CancellationToken.None).GetAwaiter().GetResult())
                    {
                        var pending = (Task)entry.Invoke(control, new object[] { node, "本地图像", path });
                        Solution.Instance.Stop();
                        bool cancelled = false;
                        try { Complete(pending); } catch (OperationCanceledException) { cancelled = true; }
                        Check(cancelled, "运行中停止必须取消当前图像测试。");
                    }
                    Check(parameter.ImageSource == "相机" && parameter.ImagePath == "保留原图像路径", "取消后必须恢复原始参数。");
                    Complete((Task)entry.Invoke(control, new object[] { node, "本地图像", path }));
                    Check(process.Success, "取消后的第一次重新测试必须成功。");
                    bool failed = false;
                    try { Complete((Task)entry.Invoke(control, new object[] { node, "本地图像", path + ".missing" })); }
                    catch (Exception) { failed = true; }
                    Check(failed && parameter.ImageSource == "相机" && parameter.ImagePath == "保留原图像路径", "异常后必须报告失败并恢复参数。");
                    failed = false;
                    try { Complete((Task)entry.Invoke(control, new object[] { node, "相机", parameter.ImagePath })); }
                    catch (Exception) { failed = true; }
                    Check(failed && parameter.ImageSource == "相机" && parameter.ImagePath == "保留原图像路径", "流程执行异常后必须恢复参数并释放测试门闩。");
                    Complete((Task)entry.Invoke(control, new object[] { node, "本地图像", path }));
                    Check(process.Success, "异常后的第一次本地图像测试必须恢复正常。");
                    foreach (TriggerModel mode in new[] { TriggerModel.Off, TriggerModel.On })
                    foreach (TriggerSource source in new[] { TriggerSource.SOFT, TriggerSource.LINE0 })
                    foreach (TriggerEdge edge in new[] { TriggerEdge.Rising, TriggerEdge.Falling, TriggerEdge.Hight, TriggerEdge.Low })
                    {
                        parameter.TriggerModel = mode;
                        parameter.TriggerSource = source;
                        parameter.TriggerEdge = edge;
                        Solution.Instance.Stop();
                        Complete((Task)entry.Invoke(control, new object[] { node, "本地图像", path }));
                        Check(process.Success && parameter.ImageSource == "相机" && parameter.TriggerModel == mode &&
                            parameter.TriggerSource == source && parameter.TriggerEdge == edge,
                            "触发模式切换后首次本地图像测试失败或改写了方案触发配置。");
                    }
                }
                Solution.Instance.AllProcesses.Remove(process);
            }
            Console.WriteLine("手动图像测试回归通过：" + _checks + "项。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
